using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
namespace ReportingModule.Web
{
 public sealed partial class CommerceStore
 {
  public void RecordPayment(long id,string unit,string kind,string number,DateTime date,decimal amount,string note,Guid requestId)
  {
   UnitAccess.Demand(unit);
   if(kind!="SALES_INVOICE"&&kind!="PURCHASE_INVOICE") throw new ArgumentException("Chọn hóa đơn bán/mua.");
   if(string.IsNullOrWhiteSpace(unit)||unit.Length>20) throw new ArgumentException("Đơn vị không hợp lệ.");
   number=(number??"").Trim();
   if(!Regex.IsMatch(number,@"\A[A-Za-z0-9_/-]{1,30}\z")) throw new ArgumentException("Số tham chiếu thanh toán: 1-30 ký tự không dấu, số, _, / hoặc -.");
   if(amount<=0||amount>999999999999999999999999.9999m||decimal.Round(amount,4)!=amount) throw new ArgumentException("Số tiền phải dương, tối đa 24 số nguyên và 4 số lẻ.");
   if((note??"").Length>500||requestId==Guid.Empty) throw new ArgumentException("Ghi chú hoặc yêu cầu thanh toán không hợp lệ.");
   using(var c=Open()) using(var cmd=new SqlCommand("dbo.RecordInvoicePayment",c))
   {
    cmd.CommandType=CommandType.StoredProcedure;
    cmd.Parameters.AddRange(new[]{P("@Id",SqlDbType.BigInt,id),P("@Unit",SqlDbType.VarChar,unit,20),P("@Kind",SqlDbType.VarChar,kind,20),
     P("@Number",SqlDbType.VarChar,number,30),P("@Date",SqlDbType.Date,date.Date),P("@Note",SqlDbType.NVarChar,note,500),P("@RequestId",SqlDbType.UniqueIdentifier,requestId)});
    var money=cmd.Parameters.Add("@Amount",SqlDbType.Decimal); money.Precision=28; money.Scale=4; money.Value=amount;
    cmd.ExecuteNonQuery();
   }
  }
  public DataTable Payments(long id,string unit,string kind)
  {
   UnitAccess.Demand(unit);
   return Query(@"SELECT p.SoThanhToan,p.Ngay,p.SoTien,p.DienGiai FROM dbo.ThanhToanHoaDon p
    JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=p.ChungTuId WHERE h.ChungTuId=@id AND h.MaDonVi=@u AND h.Loai=@k AND h.TrangThai='POSTED' ORDER BY p.Ngay,p.ThanhToanId",
    P("@id",SqlDbType.BigInt,id),P("@u",SqlDbType.VarChar,unit,20),P("@k",SqlDbType.VarChar,kind,20));
  }
 }
}
