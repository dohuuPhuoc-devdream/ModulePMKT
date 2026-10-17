using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReportingModule.Web
{
 public sealed class JournalLine
 {
  public string Account; public decimal Debit, Credit;
  public JournalLine(string account,decimal debit,decimal credit) { Account=account; Debit=debit; Credit=credit; }
 }
 public sealed class JournalDocument
 {
  public DataRow Header; public DataTable Lines;
 }
 public sealed class JournalStore
 {
  private readonly string connection;
  public JournalStore():this(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString) {}
  public JournalStore(string connection) { this.connection=connection; }
  private SqlConnection Open()
  {
   var c=new SqlConnection(connection);
   try { c.Open(); using(var cmd=new SqlCommand("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; SET XACT_ABORT ON;",c)) cmd.ExecuteNonQuery(); return c; }
   catch { c.Dispose(); throw; }
  }
  private static SqlParameter P(string name,SqlDbType type,object value,int size=0)
  { var p=new SqlParameter(name,type); if(size>0) p.Size=size; p.Value=value??DBNull.Value; return p; }
  private DataTable Query(string sql,params SqlParameter[] args)
  {
   using(var c=Open()) using(var cmd=new SqlCommand(sql,c)) { cmd.Parameters.AddRange(args); var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); return dt; }
  }
  public DataTable Units() { var current=UnitAccess.CurrentTable();return current??Query("SELECT MaDonVi,TenDonVi,NgayBatDau FROM dbo.DonVi ORDER BY MaDonVi"); }
  public DataTable Accounts() { return Query("SELECT t.MaTK,t.TenTK FROM dbo.TaiKhoan t JOIN dbo.TaiKhoanBaoCao m ON m.MaTK=t.MaTK ORDER BY m.ThuTu"); }
  public static DateTime Date(string text)
  {
   DateTime result;
   if(!DateTime.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out result)) throw new ArgumentException("Ngày phải có định dạng yyyy-MM-dd.");
   return result;
  }
  public static List<JournalLine> ParseLines(IList<string[]> rows)
  {
   if(rows==null||rows.Count>100) throw new ArgumentException("Tối đa 100 dòng.");
   var lines=new List<JournalLine>();
   foreach(var row in rows)
   {
    if(row==null||row.Length!=3) throw new ArgumentException("Chi tiết không hợp lệ.");
    if(row.All(string.IsNullOrWhiteSpace)) continue;
    decimal q,p;
    if(!NumberParser.TryParse(row[1],out q)||!NumberParser.TryParse(row[2],out p))
      throw new ArgumentException("Nợ/Có không đúng định dạng. "+NumberParser.Hint+" Bên không phát sinh nhập 0.");
    lines.Add(new JournalLine((row[0]??"").Trim(),q,p));
   }
   ValidateLines(lines); return lines;
  }
  internal static void ValidateLines(IList<JournalLine> lines)
  {
   if(lines==null||lines.Count<1||lines.Count>100) throw new ArgumentException("Phiếu cần 1-100 dòng.");
   foreach(var l in lines) {
    if(l==null||string.IsNullOrWhiteSpace(l.Account)||l.Account.Length>20) throw new ArgumentException("Chọn tài khoản hợp lệ.");
    if(!((l.Debit>0&&l.Credit==0)||(l.Credit>0&&l.Debit==0))) throw new ArgumentException("Mỗi dòng chỉ có một bên Nợ hoặc Có dương.");
    foreach(decimal value in new[]{l.Debit,l.Credit}) if(value>999999999999999999999999.9999m||decimal.Round(value,4)!=value) throw new ArgumentException("Số tiền tối đa 24 số nguyên và 4 số lẻ.");
   }
  }
  public long Save(long? id,byte[] version,string unit,string number,DateTime date,string note,IList<JournalLine> lines,bool post)
  {
   UnitAccess.Demand(unit);
   ValidateLines(lines);

   if(string.IsNullOrWhiteSpace(unit)||unit.Length>20) throw new ArgumentException("Chọn đơn vị hợp lệ.");
   number=(number??"").Trim();
   if(!Regex.IsMatch(number,@"\A[A-Za-z0-9_/-]{1,30}\z")) throw new ArgumentException("Số phiếu: 1-30 ký tự không dấu, số, _, / hoặc -.");
   if((note??"").Length>500) throw new ArgumentException("Diễn giải tối đa 500 ký tự.");
   if(id.HasValue && (version==null||version.Length!=8)) throw new ArgumentException("Chọn lại phiếu trước khi sửa.");
   var table=new DataTable(); table.Columns.Add("Dong",typeof(int)); table.Columns.Add("MaTK",typeof(string)); table.Columns.Add("PhatSinhNo",typeof(decimal)); table.Columns.Add("PhatSinhCo",typeof(decimal));
   for(int i=0;i<lines.Count;i++) table.Rows.Add(i+1,lines[i].Account,lines[i].Debit,lines[i].Credit);
   using(var c=Open()) using(var cmd=new SqlCommand("dbo.SaveJournal",c))
   {
    cmd.CommandType=CommandType.StoredProcedure;
    cmd.Parameters.AddRange(new[]{P("@Id",SqlDbType.BigInt,id.HasValue?(object)id.Value:null),P("@Version",SqlDbType.Binary,version,8),
     P("@Unit",SqlDbType.VarChar,unit,20),P("@Number",SqlDbType.VarChar,number,30),P("@Date",SqlDbType.Date,date.Date),
     P("@Note",SqlDbType.NVarChar,note,500),P("@Post",SqlDbType.Bit,post)});
    var p=cmd.Parameters.Add("@Lines",SqlDbType.Structured); p.TypeName="dbo.JournalLines"; p.Value=table;
    return Convert.ToInt64(cmd.ExecuteScalar());
   }
  }
  public void Delete(long id,byte[] version,string unit)
  {
   UnitAccess.Demand(unit);
   if(version==null||version.Length!=8) throw new ArgumentException("Chọn lại phiếu.");
   using(var c=Open()) using(var cmd=new SqlCommand("dbo.DeleteJournal",c))
   {
    cmd.CommandType=CommandType.StoredProcedure; cmd.Parameters.AddRange(new[]{P("@Id",SqlDbType.BigInt,id),P("@Version",SqlDbType.Binary,version,8),P("@Unit",SqlDbType.VarChar,unit,20)});
    cmd.ExecuteNonQuery();
   }
  }
  public DataTable List(string unit,DateTime from,DateTime to,string search)
  {
   UnitAccess.Demand(unit);
   ValidateRange(unit,from,to);

   if((search??"").Length>100) throw new ArgumentException("Từ khóa tối đa 100 ký tự.");
   string q=(search??"").Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[");
   return Query(@"SELECT h.PhieuId,h.Ngay,h.SoPhieu,h.TrangThai,h.DienGiai,COALESCE(SUM(d.PhatSinhNo),0) AS TongTien
    FROM dbo.PhieuKeToan h LEFT JOIN dbo.CTPhieuKeToan d ON d.PhieuId=h.PhieuId
    WHERE h.MaDonVi=@u AND h.Ngay>=@f AND h.Ngay<=@t AND h.SoPhieu LIKE @q ESCAPE '~'
    GROUP BY h.PhieuId,h.Ngay,h.SoPhieu,h.TrangThai,h.DienGiai ORDER BY h.Ngay DESC,h.PhieuId DESC",
    P("@u",SqlDbType.VarChar,unit,20),P("@f",SqlDbType.Date,from),P("@t",SqlDbType.Date,to),P("@q",SqlDbType.VarChar,"%"+q+"%",210));
  }
  public JournalDocument Load(long id,string unit)
  {
   UnitAccess.Demand(unit);
   using(var c=Open()) using(var tx=c.BeginTransaction(IsolationLevel.RepeatableRead))
   {
    var ds=new DataSet();
    using(var cmd=new SqlCommand(@"SELECT h.*,u.TenDonVi FROM dbo.PhieuKeToan h JOIN dbo.DonVi u ON u.MaDonVi=h.MaDonVi WHERE h.PhieuId=@id AND h.MaDonVi=@u;
     SELECT d.Dong,d.MaTK,i.TenTK,d.PhatSinhNo,d.PhatSinhCo
      FROM dbo.CTPhieuKeToan d JOIN dbo.PhieuKeToan h ON h.PhieuId=d.PhieuId JOIN dbo.TaiKhoan i ON i.MaTK=d.MaTK
      WHERE h.PhieuId=@id AND h.MaDonVi=@u ORDER BY d.Dong;",c,tx))
    {
     cmd.Parameters.AddRange(new[]{P("@id",SqlDbType.BigInt,id),P("@u",SqlDbType.VarChar,unit,20)});
     using(var da=new SqlDataAdapter(cmd)) da.Fill(ds);
    }
    tx.Commit();
    if(ds.Tables[0].Rows.Count!=1) throw new ArgumentException("Phiếu không tồn tại trong đơn vị/loại đang chọn.");
    return new JournalDocument { Header=ds.Tables[0].Rows[0],Lines=ds.Tables[1] };
   }
  }
  private static void ValidateRange(string unit,DateTime from,DateTime to)
  { if(string.IsNullOrWhiteSpace(unit)||unit.Length>20||from.Date>to.Date) throw new ArgumentException("Chọn đơn vị và khoảng ngày hợp lệ."); }
  public DataTable Report(string unit,DateTime from,DateTime to)
  {
   UnitAccess.Demand(unit);
   ValidateRange(unit,from,to);
   using(var c=Open()) using(var tx=c.BeginTransaction(IsolationLevel.RepeatableRead)) using(var cmd=new SqlCommand("dbo.BalanceReport",c,tx))
   {
    using(var gate=new SqlCommand("SELECT MaDonVi FROM dbo.DonVi WITH(HOLDLOCK) WHERE MaDonVi=@u",c,tx))
    { gate.Parameters.Add("@u",SqlDbType.VarChar,20).Value=unit; gate.ExecuteScalar(); }
    cmd.CommandType=CommandType.StoredProcedure;
    cmd.Parameters.AddRange(new[]{P("@Unit",SqlDbType.VarChar,unit,20),P("@From",SqlDbType.Date,from.Date),P("@To",SqlDbType.Date,to.Date)});
    var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); tx.Commit(); return dt;
   }
  }
 }
}

