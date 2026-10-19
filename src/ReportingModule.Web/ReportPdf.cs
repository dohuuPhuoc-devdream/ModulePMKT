using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace ReportingModule.Web
{
 // Bounded image-based PDF: installed Windows GDI+ renders Vietnamese; no external PDF package.
 public static class ReportPdf
 {
  private const int W=1754,H=1240,Margin=52,Bottom=1160;
  private static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }
  public static byte[] Export(OutputSnapshot s)
  {
   if(ReportOutput.IsDocument(s.Screen)) return ExportVoucher(s);
   if(s.Data.Rows.Count>5000) throw new ArgumentException("Tối đa 5000 dòng; hãy thu hẹp bộ lọc.");
   var pages=new List<byte[]>();
   using(var font=new Font("Arial",19,FontStyle.Regular,GraphicsUnit.Pixel))
   using(var bold=new Font("Arial",19,FontStyle.Bold,GraphicsUnit.Pixel))
   using(var title=new Font("Arial",27,FontStyle.Bold,GraphicsUnit.Pixel))
   using(var bitmap=new Bitmap(W,H)) using(var g=Graphics.FromImage(bitmap))
   {
    g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
    float[] widths=s.Columns.Select(c=>c.Format==""?1.5f:1f).ToArray(); float total=widths.Sum();
    for(int i=0;i<widths.Length;i++) widths[i]=(W-2*Margin)*widths[i]/total;
    float y=0;
    Action finish=()=> { g.DrawString("Trang "+(pages.Count+1),font,Brushes.Black,Margin,H-48); using(var ms=new MemoryStream()) { bitmap.Save(ms,ImageFormat.Jpeg); pages.Add(ms.ToArray()); } if(pages.Count>200) throw new ArgumentException("PDF vượt 200 trang; hãy thu hẹp bộ lọc."); };
    Action start=()=> {
     g.Clear(Color.White); y=Margin;
     y+=DrawParagraph(g,s.Title,title,Margin,y,W-2*Margin)+8;
     y+=DrawParagraph(g,s.Criteria,font,Margin,y,W-2*Margin)+4;
     y+=DrawParagraph(g,"Dữ liệu UTC: "+s.Created.ToString("yyyy-MM-dd HH:mm:ss"),font,Margin,y,W-2*Margin)+16;
     float height=s.Columns.Select((c,i)=>g.MeasureString(c.Title,bold,(int)widths[i]-16).Height+18).Max(); float x=Margin;
     for(int i=0;i<s.Columns.Length;i++) { g.FillRectangle(Brushes.Gainsboro,x,y,widths[i],height); g.DrawRectangle(Pens.Gray,x,y,widths[i],height); g.DrawString(s.Columns[i].Title,bold,Brushes.Black,new RectangleF(x+8,y+8,widths[i]-16,height-12)); x+=widths[i]; }
     y+=height;
    };
    start();
    foreach(DataRow row in s.Data.Rows) {
     bool group=s.Data.Columns.Contains("Cap")&&Convert.ToInt32(row["Cap"])==0;
     Font f=group?bold:font;
     var cells=s.Columns.Select(c=>s.Text(row,c)).ToArray();
     float height=cells.Select((c,i)=>g.MeasureString(c,f,(int)widths[i]-16).Height+18).Max();
     if(height>Bottom-300) throw new ArgumentException("Một dòng quá dài để in PDF. Hãy dùng Excel.");
     if(y+height>Bottom) { finish(); start(); }
     float x=Margin;
     for(int i=0;i<cells.Length;i++) {
      if(group) g.FillRectangle(Brushes.WhiteSmoke,x,y,widths[i],height);
      g.DrawRectangle(Pens.LightGray,x,y,widths[i],height);
      using(var format=new StringFormat()) { if(s.Columns[i].Format.Contains(":N")) format.Alignment=StringAlignment.Far;
       g.DrawString(cells[i],f,Brushes.Black,new RectangleF(x+8,y+8,widths[i]-16,height-12),format); }
      x+=widths[i];
     }
     y+=height;
    }
    if(s.Data.Rows.Count==0) { y+=10; y+=DrawParagraph(g,"Không có dữ liệu phù hợp.",font,Margin,y,W-2*Margin); }
    float summaryHeight=g.MeasureString(s.Summary,font,W-2*Margin).Height+24;
    if(y+summaryHeight>Bottom) { finish(); start(); }
    DrawParagraph(g,s.Summary,font,Margin,y+16,W-2*Margin); finish();
   }
   return Pack(pages);
  }
  private static byte[] ExportVoucher(OutputSnapshot s)
  {
   if(s.Screen=="purchase-receipt") ReportOutput.ApplyVoucherModel(s);
   const int w=1754,h=1240,margin=84,bottom=1080; var pages=new List<byte[]>(); float y=0;
   string face=s.Screen=="purchase-receipt"?"Times New Roman":"Arial";
   using(var regular=new Font(face,20,FontStyle.Regular,GraphicsUnit.Pixel))
   using(var bold=new Font(face,20,FontStyle.Bold,GraphicsUnit.Pixel))
   using(var title=new Font(face,34,FontStyle.Bold,GraphicsUnit.Pixel))
   using(var bitmap=new Bitmap(w,h)) using(var g=Graphics.FromImage(bitmap))
   {
    g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
    Action finish=()=>{g.DrawString("Trang "+(pages.Count+1),regular,Brushes.Black,margin,h-54);using(var ms=new MemoryStream()){bitmap.Save(ms,ImageFormat.Jpeg);pages.Add(ms.ToArray());}if(pages.Count>200)throw new ArgumentException("PDF vượt 200 trang; hãy thu hẹp dữ liệu.");};
    Action headers=()=>{
      float x=margin; float[] widths=s.Columns.Select(c=>c.Format==""?1.5f:1f).ToArray();float sum=widths.Sum();for(int i=0;i<widths.Length;i++)widths[i]=(w-2*margin-64)*widths[i]/sum;
      float rh=46;g.FillRectangle(Brushes.AliceBlue,x,y,64,rh);g.DrawRectangle(Pens.Gray,x,y,64,rh);g.DrawString("STT",bold,Brushes.Black,new RectangleF(x+6,y+10,52,rh-14));x+=64;
      for(int i=0;i<widths.Length;i++){g.FillRectangle(Brushes.AliceBlue,x,y,widths[i],rh);g.DrawRectangle(Pens.Gray,x,y,widths[i],rh);g.DrawString(s.Columns[i].Title,bold,Brushes.Black,new RectangleF(x+8,y+10,widths[i]-16,rh-14));x+=widths[i];}y+=rh;
    };
    Action start=()=>{
     g.Clear(Color.White);y=margin;
     g.DrawString(s.UnitLegalName??s.UnitName,bold,Brushes.Black,margin,y);y+=30;
     if(!string.IsNullOrWhiteSpace(s.UnitAddress)){g.DrawString(s.UnitAddress,regular,Brushes.Black,margin,y);y+=26;}
     if(!string.IsNullOrWhiteSpace(s.UnitTaxCode)){g.DrawString("Mã số thuế: "+s.UnitTaxCode,regular,Brushes.Black,margin,y);y+=26;}
     g.DrawLine(Pens.SteelBlue,margin,y,w-margin,y);y+=20;
     y+=DrawParagraph(g,s.Title.ToUpperInvariant(),title,margin,y,w-2*margin)+8;
     y+=DrawParagraph(g,"Ngày "+s.DocumentDate.ToString("dd 'tháng' MM 'năm' yyyy",CultureInfo.GetCultureInfo("vi-VN"))+"                         Số "+(s.Screen=="purchase-receipt"?"phiếu":"chứng từ")+": "+s.DocumentNumber,bold,margin,y,w-2*margin)+14;
     if(s.Screen=="purchase-receipt"&&!string.IsNullOrWhiteSpace(s.DeliveryPerson))y+=DrawParagraph(g,"Người giao hàng: "+s.DeliveryPerson,regular,margin,y,w-2*margin)+7;
     if(!string.IsNullOrEmpty(s.PartnerCode)) y+=DrawParagraph(g,(s.PartnerLabel??"Đối tác")+": "+s.PartnerName,regular,margin,y,w-2*margin)+7;
     if(!string.IsNullOrEmpty(s.PartnerAddress)) y+=DrawParagraph(g,"Địa chỉ: "+s.PartnerAddress,regular,margin,y,w-2*margin)+7;
     if(!string.IsNullOrEmpty(s.DocumentNote)) y+=DrawParagraph(g,"Diễn giải: "+s.DocumentNote,regular,margin,y,w-2*margin)+10;
     headers();
    };
    start();
    int rowNumber=0;
    foreach(DataRow row in s.Data.Rows){
     var cells=s.Columns.Select(c=>s.Text(row,c)).ToArray();float[] widths=s.Columns.Select(c=>c.Format==""?1.5f:1f).ToArray();float sum=widths.Sum();for(int i=0;i<widths.Length;i++)widths[i]=(w-2*margin-64)*widths[i]/sum;
     float height=cells.Select((c,i)=>g.MeasureString(c,regular,Math.Max(60,(int)widths[i]-16)).Height+20).Max();if(height>bottom-350)throw new ArgumentException("Một dòng quá dài để in PDF.");
     if(y+height>bottom){finish();g.Clear(Color.White);y=margin;g.DrawString(s.Title+" — tiếp theo",bold,Brushes.Black,margin,y);y+=48;headers();}
      float x=margin;g.DrawRectangle(Pens.LightGray,x,y,64,height);g.DrawString((++rowNumber).ToString(CultureInfo.InvariantCulture),regular,Brushes.Black,new RectangleF(x+6,y+8,52,height-12));x+=64;
      for(int i=0;i<cells.Length;i++){g.DrawRectangle(Pens.LightGray,x,y,widths[i],height);using(var f=new StringFormat()){if(s.Columns[i].Format.Contains(":N"))f.Alignment=StringAlignment.Far;g.DrawString(cells[i],regular,Brushes.Black,new RectangleF(x+8,y+8,widths[i]-16,height-12),f);}x+=widths[i];}y+=height;
    }
    if(s.Screen=="purchase-receipt"&&rowNumber<10){float[] blankWidths=s.Columns.Select(c=>c.Format==""?1.5f:1f).ToArray();float blankSum=blankWidths.Sum();for(int i=blankWidths.Length-1;i>=0;i--)blankWidths[i]=(w-2*margin-64)*blankWidths[i]/blankSum;while(rowNumber<10){float x=margin;float rh=34;g.DrawRectangle(Pens.LightGray,x,y,64,rh);g.DrawString((++rowNumber).ToString(CultureInfo.InvariantCulture),regular,Brushes.Black,x+6,y+8);x+=64;foreach(float cw in blankWidths){g.DrawRectangle(Pens.LightGray,x,y,cw,rh);x+=cw;}y+=rh;}}
    if(!string.IsNullOrEmpty(s.DocumentTotal)){y+=20;g.DrawString(s.DocumentTotalLabel??"Tổng cộng",bold,Brushes.Black,w-margin-390,y);string formatted=s.Screen=="purchase-receipt"?s.Data.Rows.Cast<DataRow>().Sum(r=>r["ThanhTien"]==DBNull.Value?0m:Convert.ToDecimal(r["ThanhTien"])).ToString("N0",CultureInfo.GetCultureInfo("vi-VN")):s.DocumentTotal;g.DrawString(formatted,bold,Brushes.Black,w-margin-180,y);y+=50;}
    if(s.Screen=="purchase-receipt"){y+=6;DrawParagraph(g,"Số tiền viết bằng chữ: "+s.AmountInWords,regular,margin,y,w-2*margin);y+=50;}
    if(y+230>bottom){finish();g.Clear(Color.White);y=margin;g.DrawString(s.Title+" — tiếp theo",bold,Brushes.Black,margin,y);y+=70;}
    string[] labels=ReportOutput.SignatureLabels(s.Screen);float block=(w-2*margin)/(float)labels.Length;
    for(int i=0;i<labels.Length;i++){float x=margin+i*block;var size=g.MeasureString(labels[i],bold);g.DrawString(labels[i],bold,Brushes.Black,x+(block-size.Width)/2,y);var hint=g.MeasureString("(Ký, họ tên)",regular);g.DrawString("(Ký, họ tên)",regular,Brushes.Black,x+(block-hint.Width)/2,y+30);}
    finish();
   }
   return Pack(pages,w,h,842,595);
  }
  private static float DrawParagraph(Graphics g,string text,Font font,float x,float y,int width)
  { float height=g.MeasureString(text,font,width).Height; g.DrawString(text,font,Brushes.Black,new RectangleF(x,y,width,height+4)); return height; }
  private static byte[] Pack(List<byte[]> pages,int pixelWidth=W,int pixelHeight=H,int pageWidth=842,int pageHeight=595)
  {
   using(var m=new MemoryStream()) {
    var offsets=new List<long>{0};
    Action<string> put=t=>{var bytes=Encoding.ASCII.GetBytes(t);m.Write(bytes,0,bytes.Length);};
    Action<int,string> obj=(id,t)=>{offsets.Add(m.Position);put(id+" 0 obj\n"+t+"\nendobj\n");};
    put("%PDF-1.4\n"); obj(1,"<< /Type /Catalog /Pages 2 0 R >>");
    obj(2,"<< /Type /Pages /Count "+pages.Count+" /Kids ["+string.Join(" ",Enumerable.Range(0,pages.Count).Select(i=>(3+i*3)+" 0 R"))+"] >>");
    for(int i=0;i<pages.Count;i++) {
     int p=3+i*3;
     obj(p,"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 "+pageWidth+" "+pageHeight+"] /Resources << /XObject << /Im "+(p+1)+" 0 R >> >> /Contents "+(p+2)+" 0 R >>");
     offsets.Add(m.Position); put((p+1)+" 0 obj\n<< /Type /XObject /Subtype /Image /Width "+pixelWidth+" /Height "+pixelHeight+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length "+pages[i].Length+" >>\nstream\n");
     m.Write(pages[i],0,pages[i].Length); put("\nendstream\nendobj\n");
     string content="q "+pageWidth+" 0 0 "+pageHeight+" 0 0 cm /Im Do Q";
     obj(p+2,"<< /Length "+content.Length+" >>\nstream\n"+content+"\nendstream");
    }
    long xref=m.Position; put("xref\n0 "+offsets.Count+"\n0000000000 65535 f \n");
    foreach(long offset in offsets.Skip(1)) put(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");
    put("trailer\n<< /Size "+offsets.Count+" /Root 1 0 R >>\nstartxref\n"+N(xref)+"\n%%EOF\n"); return m.ToArray();
   }
  }
}
}
