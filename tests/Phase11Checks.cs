using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using ReportingModule.Web;

class Phase11Checks
{
 static void Check(string actual,string expected)
 {if(actual!=expected)throw new Exception("Expected: "+expected+"; actual: "+actual);Console.WriteLine("PASS: "+expected);}
 static void Main()
 {
  Check(NumberToWords.Vietnamese(0m),"Không đồng chẵn");
  Check(NumberToWords.Vietnamese(1m),"Một đồng chẵn");
  Check(NumberToWords.Vietnamese(10m),"Mười đồng chẵn");
  Check(NumberToWords.Vietnamese(11m),"Mười một đồng chẵn");
  Check(NumberToWords.Vietnamese(15m),"Mười lăm đồng chẵn");
  Check(NumberToWords.Vietnamese(21m),"Hai mươi mốt đồng chẵn");
  Check(NumberToWords.Vietnamese(24m),"Hai mươi tư đồng chẵn");
  Check(NumberToWords.Vietnamese(25m),"Hai mươi lăm đồng chẵn");
  Check(NumberToWords.Vietnamese(105m),"Một trăm linh năm đồng chẵn");
  Check(NumberToWords.Vietnamese(110m),"Một trăm mười đồng chẵn");
  Check(NumberToWords.Vietnamese(1000m),"Một nghìn đồng chẵn");
  Check(NumberToWords.Vietnamese(1001m),"Một nghìn không trăm linh một đồng chẵn");
  Check(NumberToWords.Vietnamese(320000m),"Ba trăm hai mươi nghìn đồng chẵn");
  Check(NumberToWords.Vietnamese(1234567m),"Một triệu hai trăm ba mươi tư nghìn năm trăm sáu mươi bảy đồng chẵn");
  Check(NumberToWords.Vietnamese(1000000000m),"Một tỷ đồng chẵn");
  Check(NumberToWords.Vietnamese(-1500m),"Âm một nghìn năm trăm đồng chẵn");
  Check(NumberToWords.Vietnamese(1.5m),"Hai đồng chẵn");
  Check(NumberToWords.Vietnamese(-1.5m),"Âm hai đồng chẵn");
  var lines=new DataTable();foreach(var field in new[]{"MaKho","MaHang","TenHang","DonViTinh","SoLuong","DonGia","ThanhTien"})lines.Columns.Add(field,field=="SoLuong"||field=="DonGia"||field=="ThanhTien"?typeof(decimal):typeof(string));lines.Rows.Add("KHO01","HANG001","Vật tư thử","Cái",10m,16000m,160000m);
  var snapshot=new OutputSnapshot{Screen="purchase-receipt",Title="Phiếu nhập mua",UnitName="Đơn vị",UnitLegalName="Công ty thử",UnitAddress="Địa chỉ thử",UnitTaxCode="0101234565",DeliveryPerson="Người giao",PartnerName="Nhà cung cấp",PartnerAddress="Địa chỉ NCC",DocumentNumber="PN-001",DocumentDate=new DateTime(2026,10,18),Data=lines,Columns=new[]{new OutputColumn("MaKho","Mã kho"),new OutputColumn("MaHang","Mã vật tư"),new OutputColumn("TenHang","Tên vật tư"),new OutputColumn("DonViTinh","ĐVT"),new OutputColumn("SoLuong","Số lượng","{0:N2}"),new OutputColumn("DonGia","Giá","{0:N4}"),new OutputColumn("ThanhTien","Tiền","{0:N0}")},AmountInWords=NumberToWords.Vietnamese(160000m)};
  string html=ReportOutput.Html(snapshot);CheckHtml(html,"Công ty thử");CheckHtml(html,"PHIẾU NHẬP MUA");CheckHtml(html,"KHO01");CheckHtml(html,"Số tiền viết bằng chữ: Một trăm sáu mươi nghìn đồng chẵn");CheckHtml(html,"Người giao hàng");CheckHtml(html,"voucher-blank");
  byte[] workbook=ReportExcel.Export(snapshot);using(var input=new MemoryStream(workbook))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)){XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";XDocument sheet;using(var stream=zip.GetEntry("xl/worksheets/sheet1.xml").Open())sheet=XDocument.Load(stream);CheckHtml(string.Join(",",sheet.Descendants(ns+"f").Select(f=>f.Value)),"SUM(H14:H23)");CheckHtml(sheet.Descendants(ns+"c").Where(c=>(string)c.Attribute("r")=="H14").Select(c=>c.Element(ns+"v")!=null&&c.Attribute("t")==null?"numeric":"not numeric").FirstOrDefault(),"numeric");}
  Console.WriteLine("ALL PHASE 11 NUMBER AND PURCHASE TEMPLATE CHECKS PASSED");
 }
 static void CheckHtml(string actual,string expected){if(actual==null||!actual.Contains(HttpUtility.HtmlEncode(expected)))throw new Exception("Missing output: "+expected+"; actual: "+actual);Console.WriteLine("PASS: purchase template includes "+expected);}
}
