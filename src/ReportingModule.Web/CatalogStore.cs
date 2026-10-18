using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace ReportingModule.Web
{
 public sealed class CatalogStore
 {
  private readonly string connectionString;
  public CatalogStore() : this(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString) {}
  public CatalogStore(string connectionString) { this.connectionString=connectionString; }
  private SqlConnection Open()
  {
   var c=new SqlConnection(connectionString);
   try {
    c.Open();
    using(var cmd=new SqlCommand("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; SET XACT_ABORT ON;",c)) cmd.ExecuteNonQuery();
    return c;
   } catch { c.Dispose(); throw; }
  }
  private static string Columns(CatalogDefinition d) { return string.Join(",",d.Fields.Select(f=>"["+f.Name+"]")); }
  private static void Param(SqlCommand cmd,string name,CatalogField f,object value)
  {
   var p=cmd.Parameters.Add(name,f.Kind=="decimal"?SqlDbType.Decimal:f.Kind=="date"?SqlDbType.Date:
    (f.Name.StartsWith("Ma")||f.Name=="DienThoai")?SqlDbType.VarChar:SqlDbType.NVarChar);
   if(f.Kind=="decimal") { p.Precision=19; p.Scale=4; }
   else if(f.Kind=="text") p.Size=f.Length;
   p.Value=value??DBNull.Value;
  }
  public DataTable List(CatalogDefinition d,string filter)
  {
   if((filter??"").Length>100) throw new ArgumentException("Từ khóa tối đa 100 ký tự.");
   Guid? user=CurrentIdentity.CurrentUserId();
   string scope=d.Id!="units"?"":user.HasValue&&!PermissionService.IsAllowed(user.Value,"SYSTEM.UNIT.ADMIN")?" AND MaDonVi IN(SELECT MaDonVi FROM dbo.AppUserUnit WHERE UserId=@user)":System.Web.HttpContext.Current!=null?" AND 1=0":"";
   using(var c=Open())
   using(var cmd=new SqlCommand("SELECT "+Columns(d)+" FROM dbo.["+d.Table+"] WHERE (["+d.Fields[0].Name+"] LIKE @q ESCAPE '~' OR ["+d.Fields[1].Name+"] LIKE @q ESCAPE '~')"+scope+" ORDER BY ["+d.Fields[0].Name+"]",c))
   {
    string q=(filter??"").Trim().Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[");
    cmd.Parameters.Add("@q",SqlDbType.NVarChar,210).Value="%"+q+"%";
    if(scope.Contains("@user"))cmd.Parameters.Add("@user",SqlDbType.UniqueIdentifier).Value=user.Value;
    var dt=new DataTable(); using(var adapter=new SqlDataAdapter(cmd)) adapter.Fill(dt); return dt;
   }
  }
  public object[] Find(CatalogDefinition d,string key)
  {
   Guid? user=CurrentIdentity.CurrentUserId();
   string scope=d.Id!="units"?"":user.HasValue&&!PermissionService.IsAllowed(user.Value,"SYSTEM.UNIT.ADMIN")?" AND MaDonVi IN(SELECT MaDonVi FROM dbo.AppUserUnit WHERE UserId=@user)":System.Web.HttpContext.Current!=null?" AND 1=0":"";
   using(var c=Open())
   using(var cmd=new SqlCommand("SELECT "+Columns(d)+" FROM dbo.["+d.Table+"] WHERE ["+d.Fields[0].Name+"]=@id"+scope,c))
   {
    if(scope.Contains("@user"))cmd.Parameters.Add("@user",SqlDbType.UniqueIdentifier).Value=user.Value;
    Param(cmd,"@id",d.Fields[0],key);
    using(var r=cmd.ExecuteReader()) { if(!r.Read()) throw new ArgumentException("Bản ghi không còn tồn tại. Hãy làm tươi."); var values=new object[d.Fields.Length]; r.GetValues(values); return values; }
   }
  }
  private static void Insert(SqlConnection c,SqlTransaction tx,CatalogDefinition d,object[] values)
  {
   using(var cmd=new SqlCommand("INSERT dbo.["+d.Table+"]("+Columns(d)+") VALUES("+string.Join(",",d.Fields.Select((f,i)=>"@p"+i))+")",c,tx))
   { for(int i=0;i<values.Length;i++) Param(cmd,"@p"+i,d.Fields[i],values[i]); cmd.ExecuteNonQuery(); }
  }
  public void Create(CatalogDefinition d,string[] values)
  {
   DemandUnitAdmin(d);
   var valid=d.Validate(values);
   using(var c=Open()) Insert(c,null,d,valid);
  }
  private static string Original(SqlCommand cmd,CatalogDefinition d,object[] original)
  {
   if(original==null || original.Length!=d.Fields.Length) throw new ArgumentException("Chọn lại bản ghi.");
   var clauses=new List<string>();
   for(int i=0;i<original.Length;i++)
   {
    Param(cmd,"@o"+i,d.Fields[i],original[i]);
    string column="["+d.Fields[i].Name+"]"+(d.Fields[i].Kind=="text"?" COLLATE Latin1_General_100_BIN2":"");
    clauses.Add("("+column+"=@o"+i+" OR (["+d.Fields[i].Name+"] IS NULL AND @o"+i+" IS NULL))");
   }
   return string.Join(" AND ",clauses);
  }
  public void Update(CatalogDefinition d,string[] values,object[] original)
  {
   DemandUnitAdmin(d);
   var valid=d.Validate(values);
   if(original==null || !Equals(valid[0],original[0])) throw new ArgumentException("Không được đổi mã.");
   using(var c=Open())
   using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
   using(var cmd=c.CreateCommand())
   {
    cmd.Transaction=tx;
    // Unit opening date must not invalidate existing stock/history.
    if(d.Id=="units" && !Equals(valid[2],original[2]))
    {
     cmd.CommandText="SELECT (SELECT COUNT_BIG(*) FROM dbo.TonDauKy WITH (UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)+(SELECT COUNT_BIG(*) FROM dbo.ChungTuKho WITH (UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)+(SELECT COUNT_BIG(*) FROM dbo.ChungTuThuongMai WITH (UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)+(SELECT COUNT_BIG(*) FROM dbo.PhieuKeToan WITH (UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)";
     cmd.Parameters.Add("@unit",SqlDbType.VarChar,20).Value=valid[0];
     if(Convert.ToInt64(cmd.ExecuteScalar())>0) throw new ArgumentException("Đơn vị đã có tồn đầu hoặc chứng từ; không được đổi ngày bắt đầu.");
     cmd.Parameters.Clear();
    }
    string where=Original(cmd,d,original);
    cmd.CommandText="UPDATE dbo.["+d.Table+"] SET "+string.Join(",",d.Fields.Skip(1).Select((f,i)=>"["+f.Name+"]=@p"+(i+1)))+" WHERE "+where;
    for(int i=1;i<valid.Length;i++) Param(cmd,"@p"+i,d.Fields[i],valid[i]);
    if(cmd.ExecuteNonQuery()!=1) throw new ArgumentException("Dữ liệu đã thay đổi. Hãy làm tươi và chọn lại.");
    tx.Commit();
   }
  }
  public void Delete(CatalogDefinition d,object[] original)
  {
   DemandUnitAdmin(d);
   if(d.Id=="units")
   {
    using(var c=Open())using(var tx=c.BeginTransaction(IsolationLevel.Serializable))using(var cmd=c.CreateCommand())
    {
     cmd.Transaction=tx;
     cmd.CommandText="SELECT MaDonVi FROM dbo.DonVi WITH(UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit";cmd.Parameters.Add("@unit",SqlDbType.VarChar,20).Value=original==null||original.Length==0?"":original[0];
     if(cmd.ExecuteScalar()==null)throw new ArgumentException("Bản ghi không còn tồn tại. Hãy làm tươi.");
     cmd.Parameters.Clear();
     cmd.CommandText=@"SELECT (SELECT COUNT_BIG(*) FROM dbo.TonDauKy WITH(UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)+
      (SELECT COUNT_BIG(*) FROM dbo.ChungTuKho WITH(UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)+
      (SELECT COUNT_BIG(*) FROM dbo.ChungTuThuongMai WITH(UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)+
      (SELECT COUNT_BIG(*) FROM dbo.PhieuKeToan WITH(UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)+
      (SELECT COUNT_BIG(*) FROM dbo.AppUserUnit WITH(UPDLOCK,HOLDLOCK) WHERE MaDonVi=@unit)";
     cmd.Parameters.Add("@unit",SqlDbType.VarChar,20).Value=original[0];
     if(Convert.ToInt64(cmd.ExecuteScalar())>0)throw new ArgumentException("Đơn vị đã có dữ liệu hoặc người dùng được gán; không thể xóa.");
     cmd.Parameters.Clear();string where=Original(cmd,d,original);
     cmd.CommandText="SELECT COUNT(*) FROM dbo.DonVi WHERE "+where;
     if(Convert.ToInt32(cmd.ExecuteScalar())!=1)throw new ArgumentException("Dữ liệu đã thay đổi. Hãy làm tươi và chọn lại.");
     cmd.Parameters.Clear();where=Original(cmd,d,original);
     cmd.CommandText="DELETE dbo.Kho WHERE MaDonVi=@unit; DELETE dbo.DonVi WHERE "+where;
     cmd.Parameters.Add("@unit",SqlDbType.VarChar,20).Value=original[0];
     cmd.ExecuteNonQuery();
     tx.Commit();return;
    }
   }
   using(var c=Open())
   using(var cmd=c.CreateCommand())
   {
    cmd.CommandText="DELETE dbo.["+d.Table+"] WHERE "+Original(cmd,d,original);
    if(cmd.ExecuteNonQuery()!=1) throw new ArgumentException("Dữ liệu đã thay đổi. Hãy làm tươi và chọn lại.");
   }
  }
  public int Import(CatalogDefinition d,IList<string[]> rows)
  {
   DemandUnitAdmin(d);
   if(rows.Count==0 || rows.Count>5000) throw new ArgumentException("Import cần từ 1 đến 5000 dòng.");
   var prepared=new List<object[]>(); var codes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var errors=new List<string>();
   for(int i=0;i<rows.Count;i++)
   {
    try {
     var values=d.Validate(rows[i]);
     if(!codes.Add((string)values[0])) throw new ArgumentException("Mã trùng trong file.");
     prepared.Add(values);
    } catch(ArgumentException e) { errors.Add("Dòng "+(i+2)+": "+e.Message); if(errors.Count==20) break; }
   }
   if(errors.Count>0) throw new ArgumentException(string.Join("\n",errors)+"\nKhông có dòng nào được ghi.");
   using(var c=Open())
   using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
   {
    for(int i=0;i<prepared.Count;i++)
    {
     try { Insert(c,tx,d,prepared[i]); }
     catch(SqlException ex) {
      if(ex.Number==2601||ex.Number==2627) throw new ArgumentException("Dòng "+(i+2)+": mã đã tồn tại. Toàn bộ import được hủy.");
      throw;
     }
    }
    tx.Commit();
   }
   return prepared.Count;
  }
  private static void DemandUnitAdmin(CatalogDefinition d)
  {var id=CurrentIdentity.CurrentUserId();if(d.Id=="units"){if(!id.HasValue&&System.Web.HttpContext.Current!=null)throw new System.Web.HttpException(401,"Yêu cầu đăng nhập để quản lý đơn vị.");if(id.HasValue)PermissionService.Demand(id.Value,"SYSTEM.UNIT.ADMIN");}}
 }
}
