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
 [Serializable] public sealed class VoucherModel
 {
  public string UnitCode,UnitName,UnitAddress,UnitTaxCode,UnitRepresentative,DocumentType,Title,DocumentNumber,DocumentStatus,DocumentNote;
  public string PartnerCode,PartnerName,PartnerAddress,PartnerLabel,DeliveryPerson,WarehouseCode,TotalLabel,Total,AmountInWords;
  public DateTime DocumentDate;
  public DataTable Lines;
  public OutputColumn[] Columns;
  public string[] SignatureLabels;
  public bool Landscape;
 }
 [Serializable] public sealed class OutputSnapshot
 {
  public string Screen,Title,Criteria,Summary,SourceUrl,UnitCode;
  public string DocumentNumber,DocumentStatus,DocumentNote,PartnerCode,PartnerName,PartnerAddress,PartnerLabel,UnitName,UnitLegalName,UnitAddress,UnitTaxCode,UnitRepresentative,DeliveryPerson,WarehouseCode,DocumentTotal,DocumentTotalLabel,AmountInWords;
  public DateTime Created,DocumentDate; public DataTable Data; public OutputColumn[] Columns; public VoucherModel Voucher;
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
   if(screen=="purchase-receipt") return new[]{"Người giao hàng","PT nhập hàng","Người nhận hàng","Thủ kho"};
   if(screen=="stock-receipt") return new[]{"Người giao hàng","Người lập phiếu","Người nhận hàng","Thủ kho"};
   if(screen=="stock-issue") return new[]{"Người lập phiếu","Người giao hàng","Người nhận hàng","Thủ kho"};
   return new[]{"Người lập chứng từ","Kế toán","Người duyệt"};
  }
  // Purchase-voucher templates display whole VND. Round each printed line away
  // from zero, then total those displayed lines; stored four-decimal values stay intact.
  public static decimal PurchaseReceiptDong(decimal amount)
  {return Decimal.Round(amount,0,MidpointRounding.AwayFromZero);}
  public static decimal PurchaseReceiptDongTotal(DataTable lines)
  {
   if(lines==null||!lines.Columns.Contains("ThanhTien"))return 0m;
   return lines.Rows.Cast<DataRow>().Sum(row=>row["ThanhTien"]==DBNull.Value?0m:PurchaseReceiptDong(Convert.ToDecimal(row["ThanhTien"])));
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
   var titles=new Dictionary<string,string>{{"Dong","Dòng"},{"MaKho","Mã kho"},{"MaHang","Mã vật tư"},{"TenHang","Tên vật tư"},{"DonViTinh","ĐVT"},{"SoLuong","Số lượng"},{"DonGia","Giá"},{"ThanhTien","Tiền"},{"MaTK","Tài khoản"},{"TenTK","Tên tài khoản"},{"PhatSinhNo","Nợ"},{"PhatSinhCo","Có"}};
   if(screen=="purchase-receipt" && !lines.Columns.Contains("MaKho")) { var c=lines.Columns.Add("MaKho",typeof(string)); c.SetOrdinal(0); foreach(DataRow row in lines.Rows)row[c]=Convert.ToString(header["MaKho"]); }
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
   string unitName=header.Table.Columns.Contains("TenPhapLy")&&header["TenPhapLy"]!=DBNull.Value&&!string.IsNullOrWhiteSpace(Convert.ToString(header["TenPhapLy"]))?Convert.ToString(header["TenPhapLy"]):header.Table.Columns.Contains("TenDonVi")?Convert.ToString(header["TenDonVi"]):"";
   decimal totalAmount=lines.Columns.Contains("ThanhTien")?(screen=="purchase-receipt"?PurchaseReceiptDongTotal(lines):lines.Rows.Cast<DataRow>().Sum(r=>r["ThanhTien"]==DBNull.Value?0m:Convert.ToDecimal(r["ThanhTien"]))):0m;
   string displayTitle=screen=="purchase-receipt"?"Phiếu nhập mua":node.Title;
   string legalAddress=header.Table.Columns.Contains("DiaChiDonVi")?Convert.ToString(header["DiaChiDonVi"]):"";
   string taxCode=header.Table.Columns.Contains("MaSoThue")?Convert.ToString(header["MaSoThue"]):"";
   string representative=header.Table.Columns.Contains("NguoiDaiDien")?Convert.ToString(header["NguoiDaiDien"]):"";
   string sender=header.Table.Columns.Contains("NguoiGiaoHang")?Convert.ToString(header["NguoiGiaoHang"]):"";
   string warehouseCode=header.Table.Columns.Contains("MaKho")?Convert.ToString(header["MaKho"]):"";
   var columns=(screen=="purchase-receipt"?new[]{"MaKho","MaHang","TenHang","DonViTinh","SoLuong","DonGia","ThanhTien"}:lines.Columns.Cast<DataColumn>().Where(c=>titles.ContainsKey(c.ColumnName)).Select(c=>c.ColumnName).ToArray()).Select(f=>new OutputColumn(f,titles[f],f=="SoLuong"?"{0:N2}":f=="DonGia"?"{0:N4}":f=="ThanhTien"?"{0:N0}":lines.Columns[f].DataType==typeof(decimal)?"{0:N4}":"")).ToArray();
   var voucher=new VoucherModel { UnitCode=Convert.ToString(header["MaDonVi"]),UnitName=unitName,UnitAddress=legalAddress,UnitTaxCode=taxCode,UnitRepresentative=representative,DocumentType=screen,Title=displayTitle,DocumentDate=(DateTime)header["Ngay"],DocumentNumber=number,DocumentStatus=Convert.ToString(header["TrangThai"]),DocumentNote=Convert.ToString(header["DienGiai"]),PartnerCode=partner,PartnerName=partnerName,PartnerAddress=partnerAddress,PartnerLabel=partnerLabel,DeliveryPerson=sender,WarehouseCode=warehouseCode,TotalLabel=screen=="purchase-receipt"?"Tổng cộng":lines.Columns.Contains("PhatSinhNo")?"Tổng Nợ / Có":"Tổng tiền hàng",Total=total,AmountInWords=screen=="purchase-receipt"?NumberToWords.Vietnamese(totalAmount):"",Lines=lines.Copy(),Columns=columns,SignatureLabels=SignatureLabels(screen),Landscape=true };
   return Capture(new HttpSessionStateWrapper(context.Session),new OutputSnapshot { Screen=screen,Title=displayTitle,Criteria=criteria,Summary=summary,Data=lines,UnitCode=Convert.ToString(header["MaDonVi"]),Voucher=voucher,
    DocumentNumber=number,DocumentDate=(DateTime)header["Ngay"],DocumentStatus=Convert.ToString(header["TrangThai"]),DocumentNote=Convert.ToString(header["DienGiai"]),PartnerCode=partner,DocumentTotal=total,
    UnitName=unitName,UnitLegalName=unitName,UnitAddress=header.Table.Columns.Contains("DiaChiDonVi")?Convert.ToString(header["DiaChiDonVi"]):"",UnitTaxCode=header.Table.Columns.Contains("MaSoThue")?Convert.ToString(header["MaSoThue"]):"",UnitRepresentative=header.Table.Columns.Contains("NguoiDaiDien")?Convert.ToString(header["NguoiDaiDien"]):"",
    DeliveryPerson=sender,WarehouseCode=warehouseCode,PartnerName=partnerName,PartnerAddress=partnerAddress,PartnerLabel=partnerLabel,
    DocumentTotalLabel=voucher.TotalLabel,AmountInWords=voucher.AmountInWords,Columns=columns });
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
   if(s.Screen=="purchase-receipt") return PurchaseReceiptHtml(s.Voucher??FromSnapshot(s));
   Func<string,string> h=HttpUtility.HtmlEncode; var b=new StringBuilder("<article class=\"voucher-print\"><header class=\"voucher-brand\"><strong>").Append(h(s.UnitLegalName??s.UnitName));
   if(!string.IsNullOrWhiteSpace(s.UnitAddress)) b.Append("<span>").Append(h(s.UnitAddress)).Append("</span>"); if(!string.IsNullOrWhiteSpace(s.UnitTaxCode))b.Append("<span>Mã số thuế: ").Append(h(s.UnitTaxCode)).Append("</span>");b.Append("</header>");
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
   if(!string.IsNullOrEmpty(s.AmountInWords)) b.Append("<p class=\"voucher-words\"><i>Số tiền viết bằng chữ: ").Append(h(s.AmountInWords)).Append("</i></p>");
   string[] signatures=SignatureLabels(s.Screen);
   b.Append("<div class=\"voucher-signatures\">"); foreach(string label in signatures) b.Append("<div><b>").Append(h(label)).Append("</b><span>(Ký, họ tên)</span></div>"); b.Append("</div></article>");
   return b.ToString();
  }
  public static VoucherModel FromSnapshot(OutputSnapshot s)
  {
   return s.Voucher??new VoucherModel { UnitCode=s.UnitCode,UnitName=s.UnitLegalName??s.UnitName,UnitAddress=s.UnitAddress,UnitTaxCode=s.UnitTaxCode,UnitRepresentative=s.UnitRepresentative,DocumentType=s.Screen,Title=s.Title,DocumentDate=s.DocumentDate,DocumentNumber=s.DocumentNumber,DocumentStatus=s.DocumentStatus,DocumentNote=s.DocumentNote,PartnerCode=s.PartnerCode,PartnerName=s.PartnerName,PartnerAddress=s.PartnerAddress,PartnerLabel=s.PartnerLabel,DeliveryPerson=s.DeliveryPerson,WarehouseCode=s.WarehouseCode,TotalLabel=s.DocumentTotalLabel,Total=s.DocumentTotal,AmountInWords=s.AmountInWords,Lines=s.Data,Columns=s.Columns,SignatureLabels=SignatureLabels(s.Screen),Landscape=true };
  }
  internal static void ApplyVoucherModel(OutputSnapshot s)
  {
   var v=FromSnapshot(s);if(v==null)return;
   s.Title=v.Title;s.DocumentDate=v.DocumentDate;s.DocumentNumber=v.DocumentNumber;s.DocumentStatus=v.DocumentStatus;s.DocumentNote=v.DocumentNote;
   s.UnitCode=v.UnitCode;s.UnitName=s.UnitLegalName=v.UnitName;s.UnitAddress=v.UnitAddress;s.UnitTaxCode=v.UnitTaxCode;s.UnitRepresentative=v.UnitRepresentative;
   s.PartnerCode=v.PartnerCode;s.PartnerName=v.PartnerName;s.PartnerAddress=v.PartnerAddress;s.PartnerLabel=v.PartnerLabel;
   s.DeliveryPerson=v.DeliveryPerson;s.WarehouseCode=v.WarehouseCode;s.DocumentTotal=v.Total;s.DocumentTotalLabel=v.TotalLabel;s.AmountInWords=v.AmountInWords;
   s.Data=v.Lines;s.Columns=v.Columns;
  }
  private static string PurchaseReceiptHtml(VoucherModel s)
  {
   Func<string,string> h=HttpUtility.HtmlEncode;var b=new StringBuilder("<article class=\"voucher-print purchase-voucher\"><header class=\"voucher-brand\"><strong>").Append(h(s.UnitName)).Append("</strong>");
   if(!string.IsNullOrWhiteSpace(s.UnitAddress))b.Append("<span>").Append(h(s.UnitAddress)).Append("</span>");if(!string.IsNullOrWhiteSpace(s.UnitTaxCode))b.Append("<span>Mã số thuế: ").Append(h(s.UnitTaxCode)).Append("</span>");
   b.Append("</header><h1>").Append(h(s.Title.ToUpperInvariant())).Append("</h1><div class=\"voucher-date\">Ngày ").Append(s.DocumentDate.ToString("dd 'tháng' MM 'năm' yyyy",CultureInfo.GetCultureInfo("vi-VN"))).Append("</div><div class=\"voucher-meta\"><div><b>Số phiếu</b><span>").Append(h(s.DocumentNumber)).Append("</span></div>");
   if(!string.IsNullOrWhiteSpace(s.DeliveryPerson))b.Append("<div><b>Người giao hàng</b><span>").Append(h(s.DeliveryPerson)).Append("</span></div>");if(!string.IsNullOrWhiteSpace(s.PartnerName))b.Append("<div><b>").Append(h(string.IsNullOrWhiteSpace(s.PartnerLabel)?"Nhà cung cấp":s.PartnerLabel)).Append("</b><span>").Append(h(s.PartnerName)).Append("</span></div>");
   if(!string.IsNullOrWhiteSpace(s.PartnerAddress))b.Append("<div><b>Địa chỉ</b><span>").Append(h(s.PartnerAddress)).Append("</span></div>");if(!string.IsNullOrWhiteSpace(s.DocumentNote))b.Append("<div><b>Diễn giải</b><span>").Append(h(s.DocumentNote)).Append("</span></div>");
   b.Append("</div><table class=\"voucher-lines\"><thead><tr><th>STT</th>");foreach(var c in s.Columns)b.Append("<th>").Append(h(c.Title)).Append("</th>");b.Append("</tr></thead><tbody>");
    int i=0;foreach(DataRow row in s.Lines.Rows){b.Append("<tr><td>").Append(++i).Append("</td>");foreach(var c in s.Columns){object v=row[c.Field];string t=v==DBNull.Value?"":c.Field=="SoLuong"?Convert.ToDecimal(v).ToString("N2",CultureInfo.GetCultureInfo("vi-VN")):c.Field=="DonGia"?Convert.ToDecimal(v).ToString("N4",CultureInfo.GetCultureInfo("vi-VN")):c.Field=="ThanhTien"?PurchaseReceiptDong(Convert.ToDecimal(v)).ToString("N0",CultureInfo.GetCultureInfo("vi-VN")):Convert.ToString(v,CultureInfo.GetCultureInfo("vi-VN"));b.Append(c.Field=="SoLuong"||c.Field=="DonGia"||c.Field=="ThanhTien"?"<td class=\"numeric\">":"<td>").Append(h(t)).Append("</td>");}b.Append("</tr>");}
   for(;i<10;i++){b.Append("<tr class=\"voucher-blank\"><td>").Append(i+1).Append("</td>");foreach(var c in s.Columns)b.Append("<td>&nbsp;</td>");b.Append("</tr>");}
    decimal total=PurchaseReceiptDongTotal(s.Lines);b.Append("<tr class=\"voucher-total-row\"><td colspan=\"").Append(s.Columns.Length).Append("\">Tổng cộng</td><td class=\"numeric\">").Append(total.ToString("N0",CultureInfo.GetCultureInfo("vi-VN"))).Append("</td></tr></tbody></table><p class=\"voucher-words\"><i>Số tiền viết bằng chữ: ").Append(h(s.AmountInWords)).Append("</i></p><p class=\"voucher-sign-date\">Ngày……tháng……năm…………</p><div class=\"voucher-signatures\">");
   foreach(string label in s.SignatureLabels)b.Append("<div><b>").Append(h(label)).Append("</b><span><i>(Ký, họ tên)</i></span></div>");return b.Append("</div></article>").ToString();
  }
 }
}


