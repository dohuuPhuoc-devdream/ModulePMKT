using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
namespace ReportingModule.Web
{
 public partial class CommercePage
 {
  private Panel paymentPanel;
  private TextBox paymentNumber,paymentDate,paymentAmount,paymentNote;
  private Label paymentBalance;
  private GridView paymentGrid;
  private Guid PaymentRequest { get { return (Guid?)ViewState["PaymentRequest"]??Guid.Empty; } set { ViewState["PaymentRequest"]=value; } }
  private bool DebtReport { get { return screen=="receivables"||screen=="payables"; } }
  private void BuildPayments()
  {
   paymentPanel=new Panel { ID="Payments",CssClass="catalog-editor",Visible=false }; editor.Controls.Add(paymentPanel);
   paymentPanel.Controls.Add(new Literal { Text="<h2>Thanh toán hóa đơn</h2><p class=\"muted\">Ghi nhận thu/trả tiền theo hóa đơn, chưa hạch toán sổ cái. "+HttpUtility.HtmlEncode(NumberParser.Hint)+" Khoản đã ghi nhận không sửa/xóa tại đây.</p>" });
   paymentBalance=new Label { CssClass="screen-status" }; paymentPanel.Controls.Add(paymentBalance);
   paymentGrid=new GridView { ID="PaymentHistory",CssClass="catalog-grid",AutoGenerateColumns=false,GridLines=GridLines.None,EmptyDataText="Chưa có thanh toán." };
   foreach(var pair in new[]{new[]{"SoThanhToan","Số tham chiếu"},new[]{"Ngay","Ngày"},new[]{"SoTien","Số tiền"},new[]{"DienGiai","Ghi chú"}})
   {
    var col=new BoundField { DataField=pair[0],HeaderText=pair[1],HtmlEncode=true };
    if(pair[0]=="Ngay") col.DataFormatString="{0:dd/MM/yyyy}";
    if(pair[0]=="SoTien") { col.DataFormatString="{0:N4}"; col.ItemStyle.CssClass="numeric"; }
    paymentGrid.Columns.Add(col);
   }
   var wrap=new Panel { CssClass="catalog-grid-wrap" }; wrap.Controls.Add(paymentGrid); paymentPanel.Controls.Add(wrap);
   var fields=new Panel { CssClass="catalog-fields" }; paymentPanel.Controls.Add(fields);
   paymentNumber=new TextBox { ID="PaymentNumber",MaxLength=30 }; AddField(fields,"Số tham chiếu *",paymentNumber);
   paymentDate=new TextBox { ID="PaymentDate",TextMode=TextBoxMode.Date }; AddField(fields,"Ngày thanh toán *",paymentDate);
   paymentAmount=new TextBox { ID="PaymentAmount",MaxLength=29 }; AddField(fields,"Số tiền *",paymentAmount);
   paymentNote=new TextBox { ID="PaymentNote",MaxLength=500 }; AddField(fields,"Ghi chú",paymentNote);
   var record=Button(paymentPanel,"RecordPayment",CommerceStore.IsSales(kind)?"Ghi nhận thu tiền":"Ghi nhận trả tiền",()=> {
    RequireSelection();
    if(!ReadOnlyMode||!editor.Visible||!paymentPanel.Visible) throw new ArgumentException("Mở xem hóa đơn đã ghi sổ trước.");
    decimal amount;
    if(!NumberParser.TryParse(paymentAmount.Text,out amount)) throw new ArgumentException("Số tiền không đúng định dạng. "+NumberParser.Hint);
    store.RecordPayment(SelectedId.Value,DraftUnit,kind,paymentNumber.Text,StockStore.Date(paymentDate.Text),amount,paymentNote.Text,PaymentRequest);
    Done("Đã ghi nhận thanh toán hóa đơn.");
   });
   record.OnClientClick="return confirm('Ghi nhận khoản thanh toán? Khoản đã ghi nhận không sửa/xóa tại đây.');";
   Button(paymentPanel,"RefreshPayments","Làm tươi",()=>LoadSelected(true));
  }
  private void BindPayments()
  {
   paymentPanel.Visible=false;
   if(!editor.Visible||!ReadOnlyMode||!SelectedId.HasValue||kind.EndsWith("ORDER")) return;
   var doc=store.Load(SelectedId.Value,DraftUnit,kind);
   if(Convert.ToString(doc.Header["TrangThai"])!="POSTED") return;
   paymentPanel.Visible=true;
   var data=store.Payments(SelectedId.Value,DraftUnit,kind); paymentGrid.DataSource=data; paymentGrid.DataBind();
   decimal total=doc.Lines.Rows.Cast<DataRow>().Sum(r=>(decimal)r["ThanhTien"]),paid=data.Rows.Cast<DataRow>().Sum(r=>(decimal)r["SoTien"]);
   paymentBalance.Text="Tiền hàng: "+total.ToString("N4")+" • Đã thanh toán (mọi ngày): "+paid.ToString("N4")+" • Còn lại: "+(total-paid).ToString("N4");
  }
 }
}
