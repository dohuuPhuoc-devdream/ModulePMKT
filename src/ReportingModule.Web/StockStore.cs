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
 public sealed class StockLine
 {
  public string Item; public decimal Quantity, Price;
  public StockLine(string item,decimal quantity,decimal price) { Item=item; Quantity=quantity; Price=price; }
 }
 public sealed class StockDocument
 {
  public DataRow Header; public DataTable Lines;
 }
 public sealed class StockStore
 {
  private readonly string connection;
  public StockStore():this(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString) {}
  public StockStore(string connection) { this.connection=connection; }
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
  public DataTable Suppliers() { return Query("SELECT MaNCC,TenNCC FROM dbo.NhaCungCap ORDER BY MaNCC"); }
  // Inventory posting and opening balances are currently tracked at one warehouse per unit.
  // Keep the selector explicit in the form, but only expose the supported default warehouse.
  public DataTable Warehouses(string unit) { UnitAccess.Demand(unit); return Query("SELECT MaKho,TenKho FROM dbo.Kho WHERE MaDonVi=@u AND MaKho='KHO01' ORDER BY MaKho",P("@u",SqlDbType.VarChar,unit,20)); }
  public static DateTime Date(string text)
  {
   DateTime result;
   if(!DateTime.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out result)) throw new ArgumentException("Ngày phải có định dạng yyyy-MM-dd.");
   return result;
  }
  public static List<StockLine> ParseLines(IList<string[]> rows)
  {
   if(rows==null||rows.Count>100) throw new ArgumentException("Tối đa 100 dòng.");
   var lines=new List<StockLine>();
   foreach(var row in rows)
   {
    if(row==null||row.Length!=3) throw new ArgumentException("Chi tiết không hợp lệ.");
    if(row.All(string.IsNullOrWhiteSpace)) continue;
    decimal q,p;
    if(!NumberParser.TryParse(row[1],out q)||!NumberParser.TryParse(row[2],out p))
      throw new ArgumentException("Số lượng/đơn giá không đúng định dạng. "+NumberParser.Hint);
    lines.Add(new StockLine((row[0]??"").Trim(),q,p));
   }
   ValidateLines(lines); return lines;
  }
  // Match SQL Server ROUND(..., 0): VND line amounts are stored as whole đồng;
  // document totals sum the rounded persisted line amounts.
  public static decimal CalculateLineAmount(decimal quantity,decimal price)
  { return decimal.Round(quantity*price,0,MidpointRounding.AwayFromZero); }
  internal static void ValidateLines(IList<StockLine> lines)
  {
   if(lines==null||lines.Count<1||lines.Count>100) throw new ArgumentException("Phiếu cần từ 1 đến 100 dòng.");
   foreach(var line in lines)
   {
    if(line==null||string.IsNullOrWhiteSpace(line.Item)||line.Item.Length>20) throw new ArgumentException("Mã hàng bắt buộc, tối đa 20 ký tự.");
    if(line.Quantity<=0||line.Quantity>999999999999999.999m||decimal.Round(line.Quantity,3)!=line.Quantity) throw new ArgumentException("Số lượng phải dương, tối đa 15 chữ số nguyên và 3 chữ số lẻ.");
    if(line.Price<0||line.Price>999999999999999.9999m||decimal.Round(line.Price,4)!=line.Price) throw new ArgumentException("Đơn giá không âm, tối đa 15 chữ số nguyên và 4 chữ số lẻ.");
    try { if(decimal.Round(line.Quantity*line.Price,0,MidpointRounding.AwayFromZero)>999999999999999999999999.9999m) throw new OverflowException(); }
    catch(OverflowException) { throw new ArgumentException("Thành tiền vượt giới hạn decimal(28,4)."); }
   }
  }
  public long Save(long? id,byte[] version,string unit,string kind,string number,DateTime date,string note,IList<StockLine> lines,bool post,string supplier=null,bool purchaseReceipt=false,string warehouse="KHO01",string deliveryPerson=null)
  {
   UnitAccess.Demand(unit);
   ValidateLines(lines);
   if(kind!="N"&&kind!="X") throw new ArgumentException("Loại phiếu không hợp lệ.");
   if(string.IsNullOrWhiteSpace(unit)||unit.Length>20) throw new ArgumentException("Chọn đơn vị hợp lệ.");
   number=(number??"").Trim();
   if(!Regex.IsMatch(number,@"\A[A-Za-z0-9_/-]{1,30}\z")) throw new ArgumentException("Số phiếu: 1-30 ký tự không dấu, số, _, / hoặc -.");
   if((note??"").Length>500) throw new ArgumentException("Diễn giải tối đa 500 ký tự.");
   warehouse=(warehouse??"").Trim(); deliveryPerson=(deliveryPerson??"").Trim();
   if(purchaseReceipt && warehouse!="KHO01") throw new ArgumentException("Phiên bản hiện tại ghi nhận tồn tại kho chính KHO01.");
   if(deliveryPerson.Length>120) throw new ArgumentException("Người giao hàng tối đa 120 ký tự.");
   if(id.HasValue && (version==null||version.Length!=8)) throw new ArgumentException("Chọn lại phiếu trước khi sửa.");
   var table=new DataTable(); table.Columns.Add("Dong",typeof(int)); table.Columns.Add("MaHang",typeof(string)); table.Columns.Add("SoLuong",typeof(decimal)); table.Columns.Add("DonGia",typeof(decimal));
   for(int i=0;i<lines.Count;i++) table.Rows.Add(i+1,lines[i].Item,lines[i].Quantity,lines[i].Price);
   using(var c=Open()) using(var cmd=new SqlCommand("dbo.SaveStockDocument",c))
   {
    cmd.CommandType=CommandType.StoredProcedure;
    cmd.Parameters.AddRange(new[]{P("@Id",SqlDbType.BigInt,id.HasValue?(object)id.Value:null),P("@Version",SqlDbType.Binary,version,8),
     P("@Unit",SqlDbType.VarChar,unit,20),P("@Kind",SqlDbType.Char,kind,1),P("@Number",SqlDbType.VarChar,number,30),P("@Date",SqlDbType.Date,date.Date),
     P("@Note",SqlDbType.NVarChar,note,500),P("@Post",SqlDbType.Bit,post),P("@Supplier",SqlDbType.VarChar,supplier,20),P("@PurchaseReceipt",SqlDbType.Bit,purchaseReceipt),P("@Warehouse",SqlDbType.VarChar,purchaseReceipt?warehouse:null,20),P("@DeliveryPerson",SqlDbType.NVarChar,purchaseReceipt?deliveryPerson:null,120)});
    var p=cmd.Parameters.Add("@Lines",SqlDbType.Structured); p.TypeName="dbo.StockLines"; p.Value=table;
    return Convert.ToInt64(cmd.ExecuteScalar());
   }
  }
  public void Delete(long id,byte[] version,string unit,string kind)
  {
   UnitAccess.Demand(unit);
   if(version==null||version.Length!=8) throw new ArgumentException("Chọn lại phiếu.");
   using(var c=Open()) using(var cmd=new SqlCommand("dbo.DeleteStockDraft",c))
   {
    cmd.CommandType=CommandType.StoredProcedure; cmd.Parameters.AddRange(new[]{P("@Id",SqlDbType.BigInt,id),P("@Version",SqlDbType.Binary,version,8),P("@Unit",SqlDbType.VarChar,unit,20),P("@Kind",SqlDbType.Char,kind,1)});
    cmd.ExecuteNonQuery();
   }
  }
  public DataTable List(string unit,string kind,DateTime from,DateTime to,string search)
  {
   UnitAccess.Demand(unit);
   ValidateRange(unit,from,to);
   if(kind!="N"&&kind!="X") throw new ArgumentException("Loại phiếu không hợp lệ.");
   if((search??"").Length>100) throw new ArgumentException("Từ khóa tối đa 100 ký tự.");
   string q=(search??"").Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[");
   return Query(@"SELECT h.ChungTuKhoId,h.Ngay,h.SoChungTu,h.MaNCC,h.TrangThai,h.DienGiai,COALESCE(SUM(d.ThanhTien),0) AS TongTien
    FROM dbo.ChungTuKho h LEFT JOIN dbo.CTChungTuKho d ON d.ChungTuKhoId=h.ChungTuKhoId
    WHERE h.MaDonVi=@u AND h.Loai=@k AND h.Ngay>=@f AND h.Ngay<=@t AND h.SoChungTu LIKE @q ESCAPE '~'
    GROUP BY h.ChungTuKhoId,h.Ngay,h.SoChungTu,h.MaNCC,h.TrangThai,h.DienGiai ORDER BY h.Ngay DESC,h.ChungTuKhoId DESC",
    P("@u",SqlDbType.VarChar,unit,20),P("@k",SqlDbType.Char,kind,1),P("@f",SqlDbType.Date,from),P("@t",SqlDbType.Date,to),P("@q",SqlDbType.VarChar,"%"+q+"%",210));
  }
  public StockDocument Load(long id,string unit,string kind)
  {
   UnitAccess.Demand(unit);
   using(var c=Open()) using(var tx=c.BeginTransaction(IsolationLevel.RepeatableRead))
   {
    var ds=new DataSet();
    using(var cmd=new SqlCommand(@"SELECT h.*,u.TenDonVi,u.TenPhapLy,u.DiaChi AS DiaChiDonVi,u.MaSoThue,u.NguoiDaiDien,ISNULL(n.TenNCC,k.TenKH) AS TenDoiTac,ISNULL(n.DiaChi,k.DiaChi) AS DiaChiDoiTac
     FROM dbo.ChungTuKho h JOIN dbo.DonVi u ON u.MaDonVi=h.MaDonVi LEFT JOIN dbo.NhaCungCap n ON n.MaNCC=h.MaNCC
     LEFT JOIN dbo.ChungTuThuongMai tm ON tm.ChungTuId=h.ChungTuThuongMaiId LEFT JOIN dbo.KhachHang k ON k.MaKH=tm.MaKH
     WHERE h.ChungTuKhoId=@id AND h.MaDonVi=@u AND h.Loai=@k;
     SELECT d.Dong,d.MaHang,i.TenHang,i.DonViTinh,d.SoLuong,d.DonGia,d.ThanhTien
      FROM dbo.CTChungTuKho d JOIN dbo.ChungTuKho h ON h.ChungTuKhoId=d.ChungTuKhoId JOIN dbo.HangHoa i ON i.MaHang=d.MaHang
      WHERE h.ChungTuKhoId=@id AND h.MaDonVi=@u AND h.Loai=@k ORDER BY d.Dong;",c,tx))
    {
     cmd.Parameters.AddRange(new[]{P("@id",SqlDbType.BigInt,id),P("@u",SqlDbType.VarChar,unit,20),P("@k",SqlDbType.Char,kind,1)});
     using(var da=new SqlDataAdapter(cmd)) da.Fill(ds);
    }
    tx.Commit();
    if(ds.Tables[0].Rows.Count!=1) throw new ArgumentException("Phiếu không tồn tại trong đơn vị/loại đang chọn.");
    return new StockDocument { Header=ds.Tables[0].Rows[0],Lines=ds.Tables[1] };
   }
  }
  private static void ValidateRange(string unit,DateTime from,DateTime to)
  { if(string.IsNullOrWhiteSpace(unit)||unit.Length>20||from.Date>to.Date) throw new ArgumentException("Chọn đơn vị và khoảng ngày hợp lệ."); }
  public DataTable Report(string unit,DateTime from,DateTime to,string item,string mode)
  {
   UnitAccess.Demand(unit);
   ValidateRange(unit,from,to);
   if(mode!="N"&&mode!="X"&&mode!="T") throw new ArgumentException("Báo cáo không hợp lệ.");
   if((item??"").Length>20) throw new ArgumentException("Mã hàng tối đa 20 ký tự.");
   using(var c=Open()) using(var tx=c.BeginTransaction(IsolationLevel.RepeatableRead)) using(var cmd=new SqlCommand("dbo.InventoryReport",c,tx))
   {
    // Hold a shared unit lock for the result query; posting takes this row's exclusive lock first.
    using(var gate=new SqlCommand("SELECT MaDonVi FROM dbo.DonVi WITH(HOLDLOCK) WHERE MaDonVi=@u",c,tx))
    { gate.Parameters.Add("@u",SqlDbType.VarChar,20).Value=unit; gate.ExecuteScalar(); }
    cmd.CommandType=CommandType.StoredProcedure;
    cmd.Parameters.AddRange(new[]{P("@Unit",SqlDbType.VarChar,unit,20),P("@From",SqlDbType.Date,from.Date),P("@To",SqlDbType.Date,to.Date),
     P("@Item",SqlDbType.VarChar,string.IsNullOrWhiteSpace(item)?null:item.Trim(),20),P("@Mode",SqlDbType.Char,mode,1)});
    var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); tx.Commit(); return dt;
   }
  }
 }
}

