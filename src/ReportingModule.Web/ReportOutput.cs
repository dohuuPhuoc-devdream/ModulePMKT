using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
namespace ReportingModule.Web
{
 [Serializable] public sealed class OutputColumn { public string Field,Title,Format; public OutputColumn(string field,string title,string format="") { Field=field;Title=title;Format=format; } }
 [Serializable] public sealed class OutputSnapshot
 {
  public string Screen,Title,Criteria,Summary,SourceUrl,UnitCode;
  public string DocumentNumber,DocumentStatus,DocumentNote,PartnerCode,PartnerName,PartnerAddress,PartnerLabel,UnitName,DocumentTotal,DocumentTotalLabel;
  public DateTime Created,DocumentDate; public DataTable Data; public OutputColumn[] Columns;
  public string Text(DataRow row,OutputColumn c) { object v=row[c.Field]; if(v==DBNull.Value) return ""; return string.IsNullOrEmpty(c.Format)?Convert.ToString(v,CultureInfo.GetCultureInfo("vi-VN")):string.Format(CultureInfo.GetCultureInfo("vi-VN"),c.Format,v); }
 }
 public static class ReportOutput
 {
  public static string Capture(HttpSessionStateBase session,OutputSnapshot s)
  {
   if(s.Data.Rows.Count>5000) return null;
   s.Created=DateTime.UtcNow; s.Data=s.Data.Copy();
   var ids=session["OutputIds"] as List<string>??new List<string>();
   while(ids.Count>=8) { session.Remove("Output:"+ids[0]); ids.RemoveAt(0); }
   string id=Guid.NewGuid().ToString("N"); ids.Add(id); session["OutputIds"]=ids; session["Output:"+id]=s; return id;
  }
  public static string Capture(HttpContext context,string screen,string criteria,string summary,DataTable data,GridView grid,string[] applied)
  {
   string module; var node=Navigation.Find(screen,out module);
   if(screen=="balance") summary+=" | Báo cáo chỉ gồm phiếu kế toán đã ghi sổ; chưa tự hạch toán hóa đơn, kho hoặc thanh toán; chưa gồm số dư chuyển sang.";
   if(new[]{"sales-detail","revenue","sales-analysis","purchase-detail","purchase-analysis"}.Contains(screen)) summary+=" | Tiền hàng chưa gồm thuế/chiết khấu.";
   string source=Navigation.PageFor(screen)+"?screen="+HttpUtility.UrlEncode(screen)+"&unit="+HttpUtility.UrlEncode(applied[0])+"&from="+HttpUtility.UrlEncode(applied[1])+"&to="+HttpUtility.UrlEncode(applied[2])+"&q="+HttpUtility.UrlEncode(applied[3]);
   return Capture(new HttpSessionStateWrapper(context.Session),new OutputSnapshot { Screen=screen,Title=node.Title,Criteria=criteria,Summary=HttpUtility.HtmlDecode(summary),Data=data,SourceUrl=source,UnitCode=applied[0],
    Columns=grid.Columns.OfType<BoundField>().Select(c=>new OutputColumn(c.DataField,c.HeaderText,c.DataFormatString)).ToArray() });
  }
  public static bool IsReport(string screen)
  {return new[]{"balance","receivables","payables","sales-detail","revenue","sales-analysis","purchase-detail","purchase-analysis","inbound","outbound","inventory"}.Contains(screen);}
  public static bool IsDocument(string screen)
  {return new[]{"journal","sales-order","sales-invoice","ar-invoice","purchase-order","purchase-invoice","ap-invoice","purchase-receipt","stock-receipt","stock-issue"}.Contains(screen);}
  public static string[] SignatureLabels(string screen)
  {
   if(screen=="purchase-receipt"||screen=="stock-receipt") return new[]{"Người giao hàng","Người lập phiếu","Người nhận hàng","Thủ kho"};
   if(screen=="stock-issue") return new[]{"Người lập phiếu","Người giao hàng","Người nhận hàng","Thủ kho"};
   return new[]{"Người lập chứng từ","Kế toán","Người duyệt"};
  }
  public static string PermissionFor(string screen,string format)
  {
   if(format!="print"&&format!="xlsx"&&format!="pdf") throw new ArgumentException("Định dạng không tồn tại.");
   return PermissionMap.Action(screen,format=="xlsx"?"ExcelOutput":"Output",IsReport(screen));
  }
  public static OutputSnapshot Get(HttpSessionStateBase session,string id)
  {
   Guid parsed; if(!Guid.TryParseExact(id,"N",out parsed)) throw new HttpException(404,"Bản kết xuất không tồn tại.");
   var s=session["Output:"+id] as OutputSnapshot;
   if(s==null||s.Created<DateTime.UtcNow.AddMinutes(-20)) throw new HttpException(410,"Bản kết xuất đã hết hạn. Hãy trở lại báo cáo và làm tươi.");
   return s;
  }
  public static string Document(HttpContext context,string screen,DataRow header,DataTable lines)
  {
   string module; var node=Navigation.Find(screen,out module);
   var titles=new Dictionary<string,string>{{"Dong","Dòng"},{"MaHang","Mã hàng"},{"TenHang","Tên hàng"},{"DonViTinh","ĐVT"},{"SoLuong","Số lượng"},{"DonGia","Đơn giá"},{"ThanhTien","Thành tiền"},{"MaTK","Tài khoản"},{"TenTK","Tên tài khoản"},{"PhatSinhNo","Nợ"},{"PhatSinhCo","Có"}};
   string number=Convert.ToString(header[header.Table.Columns.Contains("SoPhieu")?"SoPhieu":"SoChungTu"]);
   string partner=header.Table.Columns.Contains("MaKH")&&header["MaKH"]!=DBNull.Value?Convert.ToString(header["MaKH"]):header.Table.Columns.Contains("MaNCC")&&header["MaNCC"]!=DBNull.Value?Convert.ToString(header["MaNCC"]):"";
   string criteria="Đơn vị: "+header["MaDonVi"]+" | Số: "+number+" | Ngày: "+((DateTime)header["Ngay"]).ToString("dd/MM/yyyy")+" | "+header["TrangThai"];
   if(!string.IsNullOrEmpty(partner)) criteria+=" | Đối tác: "+partner;
   string summary="Diễn giải: "+Convert.ToString(header["DienGiai"]);
   foreach(string f in new[]{"ThanhTien","PhatSinhNo","PhatSinhCo"}) if(lines.Columns.Contains(f)) summary+=" | Tổng "+titles[f]+": "+lines.Rows.Cast<DataRow>().Sum(r=>(decimal)r[f]).ToString("N4",CultureInfo.GetCultureInfo("vi-VN"));
   string total="";
   if(lines.Columns.Contains("ThanhTien")) total=lines.Rows.Cast<DataRow>().Sum(r=>r["ThanhTien"]==DBNull.Value?0m:Convert.ToDecimal(r["ThanhTien"])).ToString("N4",CultureInfo.GetCultureInfo("vi-VN"));
   else if(lines.Columns.Contains("PhatSinhNo")) total="Nợ: "+lines.Rows.Cast<DataRow>().Sum(r=>r["PhatSinhNo"]==DBNull.Value?0m:Convert.ToDecimal(r["PhatSinhNo"])).ToString("N4",CultureInfo.GetCultureInfo("vi-VN"))+" | Có: "+lines.Rows.Cast<DataRow>().Sum(r=>r["PhatSinhCo"]==DBNull.Value?0m:Convert.ToDecimal(r["PhatSinhCo"])).ToString("N4",CultureInfo.GetCultureInfo("vi-VN"));
   string partnerLabel=header.Table.Columns.Contains("MaKH")&&header["MaKH"]!=DBNull.Value?"Khách hàng":header.Table.Columns.Contains("MaNCC")&&header["MaNCC"]!=DBNull.Value?"Nhà cung cấp":"";
   string partnerName=header.Table.Columns.Contains("TenDoiTac")&&header["TenDoiTac"]!=DBNull.Value?Convert.ToString(header["TenDoiTac"]):"";
   string partnerAddress=header.Table.Columns.Contains("DiaChiDoiTac")&&header["DiaChiDoiTac"]!=DBNull.Value?Convert.ToString(header["DiaChiDoiTac"]):"";
   return Capture(new HttpSessionStateWrapper(context.Session),new OutputSnapshot { Screen=screen,Title=node.Title,Criteria=criteria,Summary=summary,Data=lines,UnitCode=Convert.ToString(header["MaDonVi"]),
    DocumentNumber=number,DocumentDate=(DateTime)header["Ngay"],DocumentStatus=Convert.ToString(header["TrangThai"]),DocumentNote=Convert.ToString(header["DienGiai"]),PartnerCode=partner,DocumentTotal=total,DocumentTotalLabel=lines.Columns.Contains("PhatSinhNo")?"Tổng Nợ / Có":"Tổng tiền hàng",
    UnitName=header.Table.Columns.Contains("TenDonVi")&&header["TenDonVi"]!=DBNull.Value?Convert.ToString(header["TenDonVi"]):"",PartnerName=partnerName,PartnerAddress=partnerAddress,PartnerLabel=partnerLabel,
    Columns=lines.Columns.Cast<DataColumn>().Where(c=>titles.ContainsKey(c.ColumnName)).Select(c=>new OutputColumn(c.ColumnName,titles[c.ColumnName],c.DataType==typeof(decimal)?(c.ColumnName=="SoLuong"?"{0:N3}":"{0:N4}"):"")).ToArray() });
  }
  public static string Html(OutputSnapshot s)
  {
   if(IsDocument(s.Screen)) return DocumentHtml(s);
   Func<string,string> h=HttpUtility.HtmlEncode;
   var b=new StringBuilder("<section class=\"print-report\"><h1>").Append(h(s.Title)).Append("</h1><p>").Append(h(s.Criteria)).Append("</p><p class=\"muted\">Thời điểm dữ liệu (UTC): ").Append(s.Created.ToString("yyyy-MM-dd HH:mm:ss")).Append("</p><table class=\"catalog-grid output-grid\"><thead><tr>");
   foreach(var c in s.Columns) b.Append("<th>").Append(h(c.Title)).Append("</th>"); b.Append("</tr></thead><tbody>");
   foreach(DataRow row in s.Data.Rows) {
    bool group=s.Data.Columns.Contains("Cap")&&Convert.ToInt32(row["Cap"])==0;
    b.Append(group?"<tr class=\"balance-group\">":"<tr>");
    foreach(var c in s.Columns) b.Append(c.Format.Contains(":N")?"<td class=\"numeric\">":"<td>").Append(h(s.Text(row,c))).Append("</td>"); b.Append("</tr>");
   }
   if(s.Data.Rows.Count==0) b.Append("<tr><td colspan=\"").Append(s.Columns.Length).Append("\">Không có dữ liệu phù hợp.</td></tr>");
   return b.Append("</tbody></table><p>").Append(h(s.Summary)).Append("</p></section>").ToString();
  }
  private static string DocumentHtml(OutputSnapshot s)
  {
   Func<string,string> h=HttpUtility.HtmlEncode; var b=new StringBuilder("<article class=\"voucher-print\"><header class=\"voucher-brand\"><strong>PicoCorp Accounting</strong><span>").Append(h(s.UnitName)).Append("</span></header>");
   b.Append("<h1>").Append(h(s.Title)).Append("</h1><div class=\"voucher-date\">Ngày ").Append(s.DocumentDate.ToString("dd 'tháng' MM 'năm' yyyy",CultureInfo.GetCultureInfo("vi-VN"))).Append("</div><div class=\"voucher-meta\"><div><b>Số chứng từ</b><span>").Append(h(s.DocumentNumber)).Append("</span></div><div><b>Ngày chứng từ</b><span>").Append(s.DocumentDate.ToString("dd/MM/yyyy")).Append("</span></div><div><b>Đơn vị</b><span>").Append(h(s.UnitName)).Append(" (").Append(h(s.UnitCode)).Append(")</span></div>");
   if(!string.IsNullOrEmpty(s.PartnerCode)) b.Append("<div><b>").Append(h(s.PartnerLabel)).Append("</b><span>").Append(h(s.PartnerName)).Append(" (").Append(h(s.PartnerCode)).Append(")</span></div>");
   if(!string.IsNullOrEmpty(s.PartnerAddress)) b.Append("<div><b>Địa chỉ</b><span>").Append(h(s.PartnerAddress)).Append("</span></div>");
   if(!string.IsNullOrEmpty(s.DocumentNote)) b.Append("<div><b>Diễn giải</b><span>").Append(h(s.DocumentNote)).Append("</span></div>");
   b.Append("</div><table class=\"voucher-lines\"><thead><tr><th>STT</th>");
   foreach(var c in s.Columns) b.Append("<th>").Append(h(c.Title)).Append("</th>");
   b.Append("</tr></thead><tbody>");
   int index=0; foreach(DataRow row in s.Data.Rows) { b.Append("<tr><td>").Append(++index).Append("</td>"); foreach(var c in s.Columns) b.Append(c.Format.Contains(":N")?"<td class=\"numeric\">":"<td>").Append(h(s.Text(row,c))).Append("</td>"); b.Append("</tr>"); }
   if(s.Data.Rows.Count==0) b.Append("<tr><td colspan=\"").Append(s.Columns.Length+1).Append("\">Không có dòng chi tiết.</td></tr>");
   b.Append("</tbody></table>");
   if(!string.IsNullOrEmpty(s.DocumentTotal)) b.Append("<p class=\"voucher-total\"><b>").Append(h(s.DocumentTotalLabel??"Tổng tiền hàng")).Append("</b><strong>").Append(h(s.DocumentTotal)).Append("</strong></p>");
   string[] signatures=SignatureLabels(s.Screen);
   b.Append("<div class=\"voucher-signatures\">"); foreach(string label in signatures) b.Append("<div><b>").Append(h(label)).Append("</b><span>(Ký, họ tên)</span></div>"); b.Append("</div></article>");
   return b.ToString();
  }
 }
}


