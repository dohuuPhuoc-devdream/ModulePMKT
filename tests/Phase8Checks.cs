using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web;
using System.Collections.Generic;
using ReportingModule.Web;
class MemorySession:HttpSessionStateBase
{
 Dictionary<string,object> values=new Dictionary<string,object>();
 public override object this[string name] { get { object v;return values.TryGetValue(name,out v)?v:null; } set { values[name]=value; } }
 public override void Remove(string name) {values.Remove(name);}
}
class Phase8Checks
{
 static void Check(bool ok,string name) {if(!ok) throw new Exception(name);Console.WriteLine("PASS: "+name);}
 static void Main(string[] args)
 {
  Directory.CreateDirectory("artifacts/phase8");
  var data=new DataTable();data.Columns.Add("Ma",typeof(string));data.Columns.Add("Ten",typeof(string));data.Columns.Add("Ngay",typeof(DateTime));data.Columns.Add("Tien",typeof(decimal));
  for(int i=0;i<73;i++) data.Rows.Add("ROW"+i,i==0?"=HYPERLINK(\"x\") <script> Nguyễn Thị Ánh":i==1?new string('W',100)+" Tiếng Việt đầy đủ dấu":"Khách hàng thực hành số "+i,new DateTime(2026,1,1),i+0.1234m);
  var s=new OutputSnapshot {Screen="test",Title="BÁO CÁO KIỂM THỬ TIẾNG VIỆT",Criteria="Đơn vị thử | 01/01/2026 - 31/12/2026",Summary="73 dòng; dữ liệu tổng hợp kiểm thử. Không phải dữ liệu công ty.",Data=data,Columns=new[]{new OutputColumn("Ma","Mã"),new OutputColumn("Ten","Tên đối tác"),new OutputColumn("Ngay","Ngày","{0:dd/MM/yyyy}"),new OutputColumn("Tien","Số tiền","{0:N4}")} };
  var session=new MemorySession(); string token=ReportOutput.Capture(session,s); data.Rows[0]["Ten"]="CHANGED";var snapshot=ReportOutput.Get(session,token);
  Check((string)snapshot.Data.Rows[0]["Ten"]!="CHANGED","snapshot detached from later DB/result changes");
  string html=ReportOutput.Html(snapshot);Check(html.Contains("ROW72")&&!html.Contains("<script>"),"all rows and HTML escaping");
  Check(ReportOutput.PermissionFor("inventory","pdf")=="STOCK.INVENTORY.PRINT"&&ReportOutput.PermissionFor("inventory","xlsx")=="STOCK.INVENTORY.EXPORT","PDF is print permission; Excel is export permission");
  var voucherRows=new DataTable();voucherRows.Columns.Add("MaHang",typeof(string));voucherRows.Columns.Add("TenHang",typeof(string));voucherRows.Columns.Add("SoLuong",typeof(decimal));voucherRows.Columns.Add("DonGia",typeof(decimal));voucherRows.Columns.Add("ThanhTien",typeof(decimal));voucherRows.Rows.Add("HANG002","Vật tư 2",10m,100m,1000m);
  var voucherSnapshot=new OutputSnapshot { Screen="purchase-receipt",Title="Phiếu nhập mua hàng",DocumentNumber="DEMO-N-1610",DocumentDate=new DateTime(2026,10,16),UnitCode="DVDEMO",UnitName="Đơn vị PicoCorp",DocumentStatus="POSTED",DocumentNote="Nhập hàng theo đơn mua",PartnerCode="NCC001",PartnerName="Nhà cung cấp 1",PartnerAddress="12 Đường Mẫu",PartnerLabel="Nhà cung cấp",DocumentTotal="1.000,0000",Data=voucherRows,Columns=new[]{new OutputColumn("MaHang","Mã hàng"),new OutputColumn("TenHang","Tên hàng"),new OutputColumn("SoLuong","Số lượng","{0:N3}"),new OutputColumn("DonGia","Đơn giá","{0:N4}"),new OutputColumn("ThanhTien","Thành tiền","{0:N4}")} };
  string voucher=ReportOutput.Html(voucherSnapshot);
  string voucherText=HttpUtility.HtmlDecode(voucher);
  Check(voucher.Contains("voucher-print")&&voucher.Contains("voucher-signatures")&&voucher.Contains("DEMO-N-1610"),"document print uses a designed voucher layout");
  Check(voucherText.Contains("DVDEMO")&&voucherText.Contains("Nhà cung cấp 1")&&voucherText.Contains("12 Đường Mẫu"),"voucher includes saved unit and partner details");
  Check(!voucher.Contains("Chứng từ nội bộ")&&!voucher.Contains("phục vụ thực hành")&&!voucher.Contains("không thay thế hóa đơn"),"voucher template has no internal/training disclaimer");
  var voucherPdf=ReportPdf.Export(voucherSnapshot);
  Check(Encoding.ASCII.GetString(voucherPdf,0,5)=="%PDF-","document PDF uses the voucher print renderer");
  File.WriteAllBytes("artifacts/phase8/voucher-template.pdf",voucherPdf);
  byte[] voucherExcel=ReportExcel.Export(voucherSnapshot);File.WriteAllBytes("artifacts/phase8/voucher-template.xlsx",voucherExcel);
  using(var zip=new ZipArchive(new MemoryStream(voucherExcel),ZipArchiveMode.Read)) {
   string sheetXml,workbookXml,stylesXml;using(var reader=new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml").Open()))sheetXml=reader.ReadToEnd();using(var reader=new StreamReader(zip.GetEntry("xl/workbook.xml").Open()))workbookXml=reader.ReadToEnd();using(var reader=new StreamReader(zip.GetEntry("xl/styles.xml").Open()))stylesXml=reader.ReadToEnd();
   Check(sheetXml.Contains("mergeCells")&&sheetXml.Contains("pageSetup")&&workbookXml.Contains("Print_Area")&&stylesXml.Contains("FF174D73"),"voucher Excel contains merged header, A4 print area, page setup and designed styles");
   Check(sheetXml.Contains("Nhà cung cấp 1")&&sheetXml.Contains("Tổng cộng"),"voucher Excel prints saved partner and total");
  }
  File.WriteAllText("artifacts/phase8/multipage.html",html);
  File.WriteAllBytes("artifacts/phase8/multipage.xlsx",ReportExcel.Export(snapshot));
  File.WriteAllBytes("artifacts/phase8/multipage.pdf",ReportPdf.Export(snapshot));
  snapshot.Created=DateTime.UtcNow.AddMinutes(-21);bool expired=false;try{ReportOutput.Get(session,token);}catch(HttpException e){expired=e.GetHttpCode()==410;}Check(expired,"snapshot expires");
  var huge=data.Clone();for(int i=0;i<5001;i++) huge.Rows.Add("x","x",DateTime.Today,1);s.Data=huge;Check(ReportOutput.Capture(session,s)==null,"oversized output refused without truncation");
  Console.WriteLine("PHASE 8 FORMAT CHECKS PASSED");
 }
}
