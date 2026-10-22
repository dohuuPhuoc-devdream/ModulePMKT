using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ReportingModule.Web
{
 // Vector PDF output. The same saved output snapshot drives the on-screen print view,
 // PDF and Excel so each format contains the same effective data and filters.
 public static class ReportPdf
 {
  private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
  private static readonly object FontLock = new object();
  private static bool voucherFontRegistered;
  private static string voucherFontFamily;

  public static byte[] Export(OutputSnapshot snapshot)
  {
   if(snapshot==null) throw new ArgumentNullException("snapshot");
   if(snapshot.Data==null || snapshot.Columns==null) throw new ArgumentException("Bản kết xuất không hợp lệ.");
   if(snapshot.Data.Rows.Count>5000) throw new ArgumentException("Tối đa 5000 dòng; hãy thu hẹp bộ lọc.");
   // A Web Forms app is hosted from its site root, while NuGet places
   // QuestPDF's optional Lato archive under bin. We explicitly register a
   // locally installed serif family below, so allow QuestPDF to use that
   // registered fallback when its optional default archive is absent.
   QuestPDF.Settings.ThrowOnMissingFontFamilies=false;
   if(ReportOutput.IsDocument(snapshot.Screen))
   {
    if(snapshot.Screen=="purchase-receipt") ReportOutput.ApplyVoucherModel(snapshot);
    return ExportVoucher(ReportOutput.FromSnapshot(snapshot));
   }
   return ExportReport(snapshot);
  }

  private static byte[] ExportVoucher(VoucherModel voucher)
  {
   QuestPDF.Settings.License=LicenseType.Community;
   lock(FontLock)
   {
    EnsureVoucherFonts();
   }
   return Document.Create(document=>document.Page(page=>
   {
    page.Size(PageSizes.A4.Landscape());
    page.Margin(28);
    page.DefaultTextStyle(style=>style.FontFamily(voucherFontFamily).FontSize(9).FontColor(Colors.Black));
    page.Header().Column(header=>
    {
     if(!String.IsNullOrWhiteSpace(voucher.UnitName)) header.Item().Text(voucher.UnitName).SemiBold().FontSize(10);
     if(!String.IsNullOrWhiteSpace(voucher.UnitAddress)) header.Item().Text(voucher.UnitAddress);
     if(!String.IsNullOrWhiteSpace(voucher.UnitTaxCode)) header.Item().Text("Mã số thuế: "+voucher.UnitTaxCode);
     header.Item().PaddingTop(4).LineHorizontal(0.7f).LineColor(Colors.Grey.Medium);
    });
    page.Content().PaddingTop(8).Column(content=>
    {
     content.Spacing(7);
     content.Item().AlignCenter().Text((voucher.Title??"CHỨNG TỪ").ToUpperInvariant()).Bold().FontSize(17);
     content.Item().Row(row=>
     {
      row.RelativeItem().AlignCenter().Text("Ngày "+voucher.DocumentDate.ToString("dd 'tháng' MM 'năm' yyyy",Vi));
      row.RelativeItem().AlignRight().Text((voucher.ScreenLabel())+": "+(voucher.DocumentNumber??""));
     });
     if(!String.IsNullOrWhiteSpace(voucher.DeliveryPerson)) Metadata(content,"Người giao hàng",voucher.DeliveryPerson);
     if(!String.IsNullOrWhiteSpace(voucher.PartnerName)) Metadata(content,String.IsNullOrWhiteSpace(voucher.PartnerLabel)?(voucher.DocumentType=="purchase-receipt"?"Nhà cung cấp":"Đối tác"):voucher.PartnerLabel,voucher.PartnerName);
     if(!String.IsNullOrWhiteSpace(voucher.PartnerAddress)) Metadata(content,"Địa chỉ",voucher.PartnerAddress);
     if(!String.IsNullOrWhiteSpace(voucher.DocumentNote)) Metadata(content,"Diễn giải",voucher.DocumentNote);
     content.Item().Table(table=>
     {
      table.ColumnsDefinition(columns=>
      {
       columns.ConstantColumn(34);
       foreach(var column in voucher.Columns) columns.RelativeColumn(ColumnWeight(column));
      });
      table.Header(header=>
      {
       header.Cell().Element(TableHeader).Text("STT");
       foreach(var column in voucher.Columns) header.Cell().Element(TableHeader).Text(column.Title??"");
      });
      int index=0;
      foreach(DataRow row in voucher.Lines.Rows)
      {
       table.Cell().Element(TableCell).AlignCenter().Text((++index).ToString(CultureInfo.InvariantCulture));
       foreach(var column in voucher.Columns)
       {
        string value=VoucherText(row,column);
        var cell=table.Cell().Element(TableCell);
        if(IsNumeric(column)) cell.AlignRight().Text(value); else cell.Text(value);
       }
      }
      int minimumRows=voucher.DocumentType=="purchase-receipt"?10:0;
      while(index<minimumRows)
      {
       table.Cell().Element(BlankCell).AlignCenter().Text((++index).ToString(CultureInfo.InvariantCulture));
       foreach(var column in voucher.Columns) table.Cell().Element(BlankCell).Text(" ");
      }
     });
     string total=voucher.Total;
     if(voucher.DocumentType=="purchase-receipt"&&voucher.Lines.Columns.Contains("ThanhTien"))
      total=ReportOutput.PurchaseReceiptDongTotal(voucher.Lines).ToString("N0",Vi);
     if(!String.IsNullOrWhiteSpace(total))
      content.Item().AlignRight().Text(text=>{text.Span((voucher.TotalLabel??"Tổng cộng")+"     ").SemiBold();text.Span(total).SemiBold();});
     if(!String.IsNullOrWhiteSpace(voucher.AmountInWords))
      content.Item().Text("Số tiền viết bằng chữ: "+voucher.AmountInWords).Italic();
     if(voucher.DocumentType=="purchase-receipt")
      content.Item().PaddingTop(8).AlignRight().Text("Ngày……tháng……năm…………");
     content.Item().PaddingTop(13).Row(row=>
     {
      var roles=voucher.SignatureLabels??ReportOutput.SignatureLabels(voucher.DocumentType);
      foreach(string role in roles) row.RelativeItem().Column(column=>
      {
       column.Item().AlignCenter().Text(role).SemiBold();
       column.Item().PaddingTop(30).AlignCenter().Text("(Ký, họ tên)").Italic().FontSize(8);
      });
     });
    });
    page.Footer().AlignCenter().Text(text=>
    { text.Span("Trang ");text.CurrentPageNumber();text.Span(" / ");text.TotalPages(); });
   })).GeneratePdf();
  }

  private static byte[] ExportReport(OutputSnapshot snapshot)
  {
   QuestPDF.Settings.License=LicenseType.Community;
   // QuestPDF 2026 no longer discovers system fonts implicitly. Register the
   // same installed serif family used by voucher templates before rendering
   // reports, so a missing bundled Lato archive cannot break PDF downloads.
   lock(FontLock) { EnsureVoucherFonts(); }
   return Document.Create(document=>document.Page(page=>
   {
    page.Size(PageSizes.A4.Landscape());page.Margin(28);
    page.DefaultTextStyle(style=>style.FontFamily(voucherFontFamily).FontSize(8).FontColor(Colors.Black));
    page.Header().Column(header=>
    {
     header.Item().Text(snapshot.Title??"Báo cáo").Bold().FontSize(16);
     if(!String.IsNullOrWhiteSpace(snapshot.Criteria)) header.Item().Text(snapshot.Criteria).FontSize(8);
     header.Item().Text("Thời điểm kết xuất: "+snapshot.Created.ToLocalTime().ToString("dd/MM/yyyy HH:mm",Vi)).FontSize(7).FontColor(Colors.Grey.Darken2);
    });
    page.Content().PaddingTop(10).Column(content=>
    {
     content.Item().Table(table=>
     {
      table.ColumnsDefinition(columns=>
      { foreach(var column in snapshot.Columns) columns.RelativeColumn(ColumnWeight(column)); });
      table.Header(header=>
      { foreach(var column in snapshot.Columns) header.Cell().Element(TableHeader).Text(column.Title??""); });
      foreach(DataRow row in snapshot.Data.Rows)
      {
       bool group=snapshot.Data.Columns.Contains("Cap")&&row["Cap"]!=DBNull.Value&&Convert.ToInt32(row["Cap"])==0;
       foreach(var column in snapshot.Columns)
       {
        var cell=table.Cell().Element(group?(Func<IContainer,IContainer>)GroupCell:TableCell);
        string value=snapshot.Text(row,column);
        if(IsNumeric(column)) { if(group) cell.AlignRight().Text(value).SemiBold(); else cell.AlignRight().Text(value); }
        else { if(group) cell.Text(value).SemiBold(); else cell.Text(value); }
       }
      }
      if(snapshot.Data.Rows.Count==0)
       table.Cell().ColumnSpan((uint)Math.Max(1,snapshot.Columns.Length)).Element(TableCell).Text("Không có dữ liệu phù hợp.");
     });
     if(!String.IsNullOrWhiteSpace(snapshot.Summary)) content.Item().PaddingTop(8).Text(snapshot.Summary).SemiBold();
    });
    page.Footer().AlignCenter().Text(text=>{text.Span("Trang ");text.CurrentPageNumber();text.Span(" / ");text.TotalPages();});
   })).GeneratePdf();
  }

  private static void Metadata(ColumnDescriptor content,string label,string value)
  { content.Item().Row(row=>{row.ConstantItem(110).Text(label+":").SemiBold();row.RelativeItem().Text(value);}); }
  private static void EnsureVoucherFonts()
  {
   if(voucherFontRegistered)return;
   string directory=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
   string[] times={"times.ttf","timesbd.ttf","timesi.ttf","timesbi.ttf"};
   if(File.Exists(Path.Combine(directory,times[0])))
   {
    foreach(string name in times){string path=Path.Combine(directory,name);if(File.Exists(path))FontManager.RegisterFontFromFile(path);}
    voucherFontFamily="Times New Roman";
   }
   else
   {
    // Georgia is a metrically different but available Windows serif fallback.
    // Register its style files when present; never silently claim Times New Roman.
    string[] georgia={"georgia.ttf","georgiab.ttf","georgiai.ttf","georgiaz.ttf"};
    if(!File.Exists(Path.Combine(directory,georgia[0])))
     throw new InvalidOperationException("Không tìm thấy Times New Roman hoặc Georgia trong thư mục phông Windows; không thể tạo mẫu phiếu có chân.");
    foreach(string name in georgia){string path=Path.Combine(directory,name);if(File.Exists(path))FontManager.RegisterFontFromFile(path);}
    voucherFontFamily="Georgia";
   }
   voucherFontRegistered=true;
  }
  private static IContainer TableHeader(IContainer container)
  { return container.Background("#E7EEF4").Border(0.5f).BorderColor("#8393A1").PaddingVertical(5).PaddingHorizontal(4).DefaultTextStyle(style=>style.SemiBold()).AlignCenter(); }
  private static IContainer TableCell(IContainer container)
  { return container.BorderBottom(0.35f).BorderColor("#C7D0D8").PaddingVertical(4).PaddingHorizontal(4); }
  private static IContainer GroupCell(IContainer container)
  { return container.Background("#F3F6F8").BorderBottom(0.35f).BorderColor("#C7D0D8").PaddingVertical(4).PaddingHorizontal(4); }
  private static IContainer BlankCell(IContainer container)
  { return container.BorderBottom(0.5f).BorderColor(Colors.Grey.Medium).PaddingVertical(5).PaddingHorizontal(4); }
  private static float ColumnWeight(OutputColumn column)
  { return IsNumeric(column)?0.9f:(column.Field=="TenHang"||column.Field=="TenTK"||column.Field=="DienGiai"?2.1f:1.25f); }
  private static bool IsNumeric(OutputColumn column)
  { return column!=null&&column.Format!=null&&column.Format.Contains(":N"); }
  private static string VoucherText(DataRow row,OutputColumn column)
  {
   object value=row[column.Field];if(value==null||value==DBNull.Value)return "";
   decimal amount;
   if(column.Field=="SoLuong"&&Decimal.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out amount))return amount.ToString("N2",Vi);
   if(column.Field=="DonGia"&&Decimal.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out amount))return amount.ToString("N4",Vi);
   if(column.Field=="ThanhTien"&&Decimal.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out amount))return ReportOutput.PurchaseReceiptDong(amount).ToString("N0",Vi);
   return String.IsNullOrEmpty(column.Format)?Convert.ToString(value,Vi):String.Format(Vi,column.Format,value);
  }
 }

 internal static class VoucherPdfExtensions
 {
  public static string ScreenLabel(this VoucherModel model)
  { return model.DocumentType=="purchase-receipt"?"Số phiếu":"Số chứng từ"; }
 }
}
