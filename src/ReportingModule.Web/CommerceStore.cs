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
 public sealed partial class CommerceStore
 {
  private readonly string connection;
  public CommerceStore():this(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString) {}
  public CommerceStore(string connection) { this.connection=connection; }
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
  public DataTable Items() { return Query("SELECT MaHang,TenHang,DonViTinh FROM dbo.HangHoa ORDER BY MaHang"); }
  public long Save(long? id,byte[] version,string unit,string kind,string number,DateTime date,string note,IList<StockLine> lines,bool post,string partner,long? purchaseReceiptId=null)
  {
   UnitAccess.Demand(unit);
   StockStore.ValidateLines(lines);
   ValidateKind(kind);
   if(string.IsNullOrWhiteSpace(unit)||unit.Length>20) throw new ArgumentException("Chọn đơn vị hợp lệ.");
   if(string.IsNullOrWhiteSpace(partner)||partner.Length>20) throw new ArgumentException("Chọn khách hàng/nhà cung cấp hợp lệ.");
   number=(number??"").Trim();
   if(!Regex.IsMatch(number,@"\A[A-Za-z0-9_/-]{1,30}\z")) throw new ArgumentException("Số phiếu: 1-30 ký tự không dấu, số, _, / hoặc -.");
   if((note??"").Length>500) throw new ArgumentException("Diễn giải tối đa 500 ký tự.");
   if(id.HasValue && (version==null||version.Length!=8)) throw new ArgumentException("Chọn lại phiếu trước khi sửa.");
   var table=new DataTable(); table.Columns.Add("Dong",typeof(int)); table.Columns.Add("MaHang",typeof(string)); table.Columns.Add("SoLuong",typeof(decimal)); table.Columns.Add("DonGia",typeof(decimal));
   for(int i=0;i<lines.Count;i++) table.Rows.Add(i+1,lines[i].Item,lines[i].Quantity,lines[i].Price);
   using(var c=Open()) using(var cmd=new SqlCommand("dbo.SaveCommerceDocument",c))
   {
    cmd.CommandType=CommandType.StoredProcedure;
    cmd.Parameters.AddRange(new[]{P("@Id",SqlDbType.BigInt,id.HasValue?(object)id.Value:null),P("@Version",SqlDbType.Binary,version,8),
     P("@Unit",SqlDbType.VarChar,unit,20),P("@Kind",SqlDbType.VarChar,kind,20),P("@Number",SqlDbType.VarChar,number,30),P("@Date",SqlDbType.Date,date.Date),
     P("@Partner",SqlDbType.VarChar,partner,20),P("@Note",SqlDbType.NVarChar,note,500),P("@Post",SqlDbType.Bit,post),P("@PurchaseReceiptId",SqlDbType.BigInt,purchaseReceiptId)});
    var p=cmd.Parameters.Add("@Lines",SqlDbType.Structured); p.TypeName="dbo.StockLines"; p.Value=table;
    return Convert.ToInt64(cmd.ExecuteScalar());
   }
  }
  public void Delete(long id,byte[] version,string unit,string kind)
  {
   UnitAccess.Demand(unit);
   if(version==null||version.Length!=8) throw new ArgumentException("Chọn lại phiếu.");
   using(var c=Open()) using(var cmd=new SqlCommand("dbo.DeleteCommerceDraft",c))
   {
    cmd.CommandType=CommandType.StoredProcedure; cmd.Parameters.AddRange(new[]{P("@Id",SqlDbType.BigInt,id),P("@Version",SqlDbType.Binary,version,8),P("@Unit",SqlDbType.VarChar,unit,20),P("@Kind",SqlDbType.VarChar,kind,20)});
    cmd.ExecuteNonQuery();
   }
  }
  public DataTable List(string unit,string kind,DateTime from,DateTime to,string search)
  {
   UnitAccess.Demand(unit);
   ValidateRange(unit,from,to);
   ValidateKind(kind);
   if((search??"").Length>100) throw new ArgumentException("Từ khóa tối đa 100 ký tự.");
   string q=(search??"").Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[");
   return Query(@"SELECT h.ChungTuId,h.Ngay,h.SoChungTu,h.TrangThai,h.DienGiai,COALESCE(h.MaKH,h.MaNCC) AS MaDoiTac,COALESCE(SUM(d.ThanhTien),0) AS TongTien
    FROM dbo.ChungTuThuongMai h LEFT JOIN dbo.CTChungTuThuongMai d ON d.ChungTuId=h.ChungTuId
    WHERE h.MaDonVi=@u AND h.Loai=@k AND h.Ngay>=@f AND h.Ngay<=@t AND h.SoChungTu LIKE @q ESCAPE '~'
    GROUP BY h.ChungTuId,h.Ngay,h.SoChungTu,h.TrangThai,h.DienGiai,h.MaKH,h.MaNCC ORDER BY h.Ngay DESC,h.ChungTuId DESC",
    P("@u",SqlDbType.VarChar,unit,20),P("@k",SqlDbType.VarChar,kind,20),P("@f",SqlDbType.Date,from),P("@t",SqlDbType.Date,to),P("@q",SqlDbType.VarChar,"%"+q+"%",210));
  }
  public StockDocument Load(long id,string unit,string kind)
  {
   UnitAccess.Demand(unit);
   using(var c=Open()) using(var tx=c.BeginTransaction(IsolationLevel.RepeatableRead))
   {
    var ds=new DataSet();
    using(var cmd=new SqlCommand(@"SELECT h.*,u.TenDonVi,COALESCE(k.TenKH,n.TenNCC) AS TenDoiTac,COALESCE(k.DiaChi,n.DiaChi) AS DiaChiDoiTac
     FROM dbo.ChungTuThuongMai h JOIN dbo.DonVi u ON u.MaDonVi=h.MaDonVi LEFT JOIN dbo.KhachHang k ON k.MaKH=h.MaKH LEFT JOIN dbo.NhaCungCap n ON n.MaNCC=h.MaNCC
     WHERE h.ChungTuId=@id AND h.MaDonVi=@u AND h.Loai=@k;
     SELECT d.Dong,d.MaHang,i.TenHang,i.DonViTinh,d.SoLuong,d.DonGia,d.ThanhTien
      FROM dbo.CTChungTuThuongMai d JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=d.ChungTuId JOIN dbo.HangHoa i ON i.MaHang=d.MaHang
      WHERE h.ChungTuId=@id AND h.MaDonVi=@u AND h.Loai=@k ORDER BY d.Dong;",c,tx))
    {
     cmd.Parameters.AddRange(new[]{P("@id",SqlDbType.BigInt,id),P("@u",SqlDbType.VarChar,unit,20),P("@k",SqlDbType.VarChar,kind,20)});
     using(var da=new SqlDataAdapter(cmd)) da.Fill(ds);
    }
    tx.Commit();
    if(ds.Tables[0].Rows.Count!=1) throw new ArgumentException("Phiếu không tồn tại trong đơn vị/loại đang chọn.");
    return new StockDocument { Header=ds.Tables[0].Rows[0],Lines=ds.Tables[1] };
   }
  }
  private static void ValidateRange(string unit,DateTime from,DateTime to)
  { if(string.IsNullOrWhiteSpace(unit)||unit.Length>20||from.Date>to.Date) throw new ArgumentException("Chọn đơn vị và khoảng ngày hợp lệ."); }
  public static bool IsSales(string kind) { return kind=="SALES_ORDER"||kind=="SALES_INVOICE"; }
  private static void ValidateKind(string kind)
  { if(!new[]{"SALES_ORDER","SALES_INVOICE","PURCHASE_ORDER","PURCHASE_INVOICE"}.Contains(kind)) throw new ArgumentException("Loại chứng từ không hợp lệ."); }
  public DataTable Partners(string kind)
  {
   ValidateKind(kind);
   return Query(IsSales(kind)?"SELECT MaKH AS Ma,TenKH AS Ten FROM dbo.KhachHang ORDER BY MaKH":"SELECT MaNCC AS Ma,TenNCC AS Ten FROM dbo.NhaCungCap ORDER BY MaNCC");
  }
  public DataTable PurchaseReceipts(string unit,string partner=null,long? currentReceiptId=null)
  {
   UnitAccess.Demand(unit);
   return Query(@"SELECT k.ChungTuKhoId,k.SoChungTu,k.Ngay,k.MaNCC,
    k.SoChungTu+' | '+k.MaNCC+' | '+CONVERT(varchar(10),k.Ngay,103) AS HienThi FROM dbo.ChungTuKho k
    WHERE k.MaDonVi=@u AND k.Loai='N' AND k.TrangThai='POSTED' AND k.MaNCC IS NOT NULL
     AND (@partner IS NULL OR k.MaNCC=@partner)
     AND (k.ChungTuKhoId=@currentReceipt OR NOT EXISTS(SELECT 1 FROM dbo.ChungTuThuongMai h WHERE h.Loai='PURCHASE_INVOICE' AND h.ChungTuNhapMuaId=k.ChungTuKhoId))
    ORDER BY k.Ngay DESC,k.ChungTuKhoId DESC",
    P("@u",SqlDbType.VarChar,unit,20),P("@partner",SqlDbType.VarChar,string.IsNullOrWhiteSpace(partner)?null:partner,20),P("@currentReceipt",SqlDbType.BigInt,currentReceiptId));
  }
  public DataTable Report(string unit,DateTime from,DateTime to,string partner,string mode)
  {
   UnitAccess.Demand(unit);
   ValidateRange(unit,from,to);
   if(!new[]{"sales-detail","revenue","sales-analysis","purchase-detail","purchase-analysis","receivables","payables"}.Contains(mode)) throw new ArgumentException("Báo cáo không hợp lệ.");
   if((partner??"").Length>20) throw new ArgumentException("Mã đối tác tối đa 20 ký tự.");
   using(var c=Open()) using(var tx=c.BeginTransaction(IsolationLevel.RepeatableRead)) using(var cmd=new SqlCommand(mode=="receivables"||mode=="payables"?"dbo.DebtReport":"dbo.CommerceReport",c,tx))
   {
    using(var gate=new SqlCommand("SELECT MaDonVi FROM dbo.DonVi WITH(HOLDLOCK) WHERE MaDonVi=@u",c,tx))
    { gate.Parameters.Add("@u",SqlDbType.VarChar,20).Value=unit; gate.ExecuteScalar(); }
    cmd.CommandType=CommandType.StoredProcedure;
    cmd.Parameters.AddRange(new[]{P("@Unit",SqlDbType.VarChar,unit,20),P("@From",SqlDbType.Date,from.Date),P("@To",SqlDbType.Date,to.Date),
     P("@Partner",SqlDbType.VarChar,string.IsNullOrWhiteSpace(partner)?null:partner.Trim(),20),P("@Mode",SqlDbType.VarChar,mode,20)});
    var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); tx.Commit(); return dt;
   }
  }
 }
}


