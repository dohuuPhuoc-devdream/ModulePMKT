using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace ReportingModule.Web
{
 public partial class JournalPage : AppPage
 {
  protected PlaceHolder Root;
  private JournalStore store;
  private string screen;
  private bool report,redirecting;
  private DropDownList unit;
  private TextBox from,to,filter,number,date,note;
  private Label message,summary,selection,editorUnit;
  private Panel editor;
  private GridView grid;
  private Literal detailHtml;
  private Button save,post,add,remove;
  private DataTable units,items;
  private List<string[]> lines=new List<string[]>();
  private long? SelectedId { get { return ViewState["SelectedId"]==null?(long?)null:(long)ViewState["SelectedId"]; } set { ViewState["SelectedId"]=value; } }
  private byte[] Version { get { return ViewState["Version"] as byte[]; } set { ViewState["Version"]=value; } }
  private bool ReadOnlyMode { get { return (bool?)ViewState["ReadOnlyMode"]??false; } set { ViewState["ReadOnlyMode"]=value; } }
  private string DraftUnit { get { return (string)ViewState["DraftUnit"]??""; } set { ViewState["DraftUnit"]=value; } }
  private string[] Criteria { get { return ViewState["Criteria"] as string[]; } set { ViewState["Criteria"]=value; } }
  protected override void OnInit(EventArgs e)
  {
   base.OnInit(e);
   screen=Request.QueryString["screen"];
   if(!new[]{"journal","balance"}.Contains(screen)) throw new HttpException(404,"Chức năng không tồn tại.");
   report=screen=="balance";
   Session["JournalSession"]=true; ViewStateUserKey=Session.SessionID+":"+screen;
   Response.Cache.SetCacheability(HttpCacheability.NoCache); Response.Cache.SetNoStore();
   store=new JournalStore();
   string module; var node=Navigation.Find(screen,out module); Title=node.Title+" | Kế toán";
   var breadcrumb=new HtmlGenericControl("p"); breadcrumb.Attributes["class"]="breadcrumb"; breadcrumb.InnerText=module; Root.Controls.Add(breadcrumb);
   var title=new HtmlGenericControl("h1"); title.InnerText=node.Title; Root.Controls.Add(title);
   Root.Controls.Add(new Literal { Text="<p class=\"muted\">Bảng cân đối được tổng hợp từ phiếu kế toán đã ghi sổ. Hóa đơn, kho và thanh toán chưa tự tạo bút toán; chưa có số dư chuyển sang. Mã tài khoản lấy theo danh mục.</p>" });
   var toolbar=new Panel { CssClass="context-toolbar" }; Root.Controls.Add(toolbar);
   if(!report)
   {
    Button(toolbar,"New","Mới",NewDocument);
    Button(toolbar,"Edit","Sửa",()=>LoadSelected(false));
    var delete=Button(toolbar,"Delete","Xóa",()=> {
     RequireSelection(); store.Delete(SelectedId.Value,Version,Criteria[0]); Done("Đã xóa phiếu nháp.");
    }); delete.OnClientClick="return confirm('Xóa phiếu nháp đang chọn?');";
   }
   Button(toolbar,"Search",report?"Tìm/Lọc":"Tìm",ApplyCriteria);
   if(!report) Button(toolbar,"View","Xem",()=>LoadSelected(true));
   Button(toolbar,"Output",report?"In/Kết xuất":"In",()=>Export("print"));
   if(report) { Button(toolbar,"ExcelOutput","Excel",()=>Export("xlsx")); Button(toolbar,"PdfOutput","PDF",()=>Export("pdf")); }
   Button(toolbar,"Refresh","Làm tươi",()=> { ClearEditor(); if(Criteria==null) message.Text="Nhập điều kiện và chọn Tìm/Lọc."; else message.Text="Đã tải lại theo điều kiện đang áp dụng."; });
   var filters=new Panel { CssClass="catalog-filter stock-filter" }; Root.Controls.Add(filters);
   unit=new DropDownList { ID="Unit" }; AddField(filters,"Đơn vị",unit);
   from=new TextBox { ID="From",TextMode=TextBoxMode.Date }; AddField(filters,"Từ ngày",from);
   to=new TextBox { ID="To",TextMode=TextBoxMode.Date }; AddField(filters,"Đến ngày",to);
   filter=new TextBox { ID="Filter",MaxLength=100 }; if(!report) AddField(filters,"Số phiếu (không bắt buộc)",filter);
   Button(filters,"Apply","Nhận / Xem",ApplyCriteria);
   message=new Label { ID="Message",CssClass="catalog-message" }; message.Attributes["role"]="status"; Root.Controls.Add(message);
   selection=new Label { CssClass="catalog-selection" }; Root.Controls.Add(selection);
   editor=new Panel { ID="Editor",CssClass="catalog-editor",Visible=false }; Root.Controls.Add(editor);
   editorUnit=new Label(); editor.Controls.Add(editorUnit);
   var header=new Panel { CssClass="catalog-fields" }; editor.Controls.Add(header);
   number=new TextBox { ID="Number",MaxLength=30 }; AddField(header,"Số phiếu *",number);
   date=new TextBox { ID="DocumentDate",TextMode=TextBoxMode.Date }; AddField(header,"Ngày *",date);
   note=new TextBox { ID="Note",MaxLength=500 }; AddField(header,"Diễn giải",note);
   var hint=new HtmlGenericControl("p"); hint.Attributes["class"]="muted"; hint.InnerText=NumberParser.Hint+" Nợ/Có tối đa 4 số lẻ, mỗi dòng chỉ ghi một bên; bên còn lại nhập 0. Ghi sổ yêu cầu tổng Nợ = tổng Có. Dòng trống được bỏ qua. Phiếu đã ghi sổ không được sửa/xóa."; editor.Controls.Add(hint);
   detailHtml=new Literal(); editor.Controls.Add(detailHtml);
   add=Button(editor,"AddLine","Thêm dòng",()=>{if(lines.Count>=100) throw new ArgumentException("Tối đa 100 dòng."); lines.Add(new[]{"","",""});});
   remove=Button(editor,"RemoveLine","Bỏ dòng cuối",()=>{if(lines.Count>0) lines.RemoveAt(lines.Count-1);});
   Button(editor,"Calculate","Tính lại",()=>{var parsed=JournalStore.ParseLines(lines); message.Text="Tổng Nợ: "+parsed.Sum(l=>l.Debit).ToString("N4")+" | Tổng Có: "+parsed.Sum(l=>l.Credit).ToString("N4");});
   save=Button(editor,"SaveDraft","Lưu nháp",()=>Save(false));
   post=Button(editor,"Post","Lưu và ghi sổ",()=>Save(true)); post.OnClientClick="return confirm('Ghi sổ phiếu kế toán? Phiếu đã ghi sổ không sửa/xóa tại đây.');";
   Button(editor,"Close","Đóng",()=>ClearEditor());
   var wrap=new Panel { CssClass="catalog-grid-wrap" }; Root.Controls.Add(wrap);
   grid=new GridView { ID="Records",CssClass="catalog-grid",AutoGenerateColumns=false,AllowPaging=true,PageSize=20,GridLines=GridLines.None,EmptyDataText="Không có dữ liệu phù hợp." };
   if(!report)
   {
    grid.DataKeyNames=new[]{"PhieuId"}; grid.Columns.Add(new CommandField { ShowSelectButton=true,SelectText="Chọn" });
    Column("Ngay","Ngày","date"); Column("SoPhieu","Số phiếu"); Column("TrangThai","Trạng thái"); Column("DienGiai","Diễn giải"); Column("TongTien","Tổng Nợ","money");
    grid.SelectedIndexChanged+=(s,args)=>Guard(()=> {
     var doc=store.Load(Convert.ToInt64(grid.SelectedDataKey.Value),Criteria[0]);
     SelectedId=Convert.ToInt64(doc.Header["PhieuId"]); Version=(byte[])doc.Header["PhienBan"]; editor.Visible=false;
     selection.Text="Đang chọn: "+HttpUtility.HtmlEncode(Convert.ToString(doc.Header["SoPhieu"]))+" • "+Convert.ToString(doc.Header["TrangThai"]);
    });
   }
   else
   {
    grid.AllowPaging=false;
    Column("Ma","Mã"); Column("ChiTieu","Chỉ tiêu"); Column("DauKy","Đầu kỳ","money"); Column("CuoiKy","Cuối kỳ","money");
    grid.RowDataBound+=(sender,args)=> {
     if(args.Row.RowType==DataControlRowType.DataRow) {
      var row=(DataRowView)args.Row.DataItem;
      args.Row.CssClass=Convert.ToInt32(row["Cap"])==0?"balance-group":"balance-account";
     }
    };
   }   grid.SelectedRowStyle.CssClass="selected";
   grid.PageIndexChanging+=(s,args)=>Guard(()=>{grid.PageIndex=args.NewPageIndex; ClearEditor();});
   wrap.Controls.Add(grid); summary=new Label { CssClass="screen-status" }; Root.Controls.Add(summary);
   Guard(()=> {
    units=store.Units(); items=store.Accounts();
    unit.DataSource=units; unit.DataTextField="TenDonVi"; unit.DataValueField="MaDonVi"; unit.DataBind();
   });
  }
  private void AddField(Control parent,string label,WebControl control)
  {
   var group=new Panel { CssClass="catalog-field" }; group.Controls.Add(new Label { Text=label,AssociatedControlID=control.ID }); group.Controls.Add(control); parent.Controls.Add(group);
  }
  private Button Button(Control parent,string id,string text,Action action)
  {
   string permission=PermissionMap.Action(screen,id,report,SelectedId.HasValue);
   var b=new Button { ID=id,Text=text,CssClass="catalog-button",CausesValidation=false };b.Visible=Can(permission);
   if(id=="SaveDraft")b.Visible=Can(PermissionMap.Action(screen,"New",false))||Can(PermissionMap.Action(screen,"Edit",false));
   if(id=="Post")b.Visible=Can(PermissionMap.Action(screen,"Post",false))&&(Can(PermissionMap.Action(screen,"New",false))||Can(PermissionMap.Action(screen,"Edit",false)));
   b.Click+=(s,e)=>Guard(()=>RunAuthorized(screen,id,action,Criteria==null?DraftUnit:Criteria[0],SelectedId.HasValue?SelectedId.Value.ToString():null,SelectedId.HasValue)); parent.Controls.Add(b); return b;
  }
  private void Column(string field,string title,string type="")
  {
   var b=new BoundField { DataField=field,HeaderText=title,HtmlEncode=true };
   if(type=="date") b.DataFormatString="{0:dd/MM/yyyy}";
   if(type=="money"||type=="quantity") { b.DataFormatString=type=="money"?"{0:N4}":"{0:N3}"; b.ItemStyle.CssClass="numeric"; }
   grid.Columns.Add(b);
  }
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e);
   if(!IsPostBack)
   {
    from.Text=units!=null&&units.Rows.Count>0?((DateTime)units.Rows[0]["NgayBatDau"]).ToString("yyyy-MM-dd"):"2026-01-01"; to.Text=DateTime.Today.ToString("yyyy-MM-dd");
    string preferredUnit=Request.QueryString["unit"]??Convert.ToString(Session["CurrentUnit"]);
    if(!string.IsNullOrEmpty(preferredUnit)&&unit.Items.FindByValue(preferredUnit)!=null) unit.SelectedValue=preferredUnit;
    DateTime parsed;
    if(DateTime.TryParseExact(Request.QueryString["from"],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed)) from.Text=parsed.ToString("yyyy-MM-dd");
    if(DateTime.TryParseExact(Request.QueryString["to"],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed)) to.Text=parsed.ToString("yyyy-MM-dd");
    filter.Text=(Request.QueryString["q"]??"");
    if(filter.Text.Length>(report?20:100)) filter.Text="";
    if(!report && unit.Items.Count>0) Guard(ApplyCriteria);
    if(Session["JournalFlash"]!=null) { message.Text=HttpUtility.HtmlEncode((string)Session["JournalFlash"]); Session.Remove("JournalFlash"); }
    if(report) message.Text="Chọn điều kiện rồi nhấn Tìm/Lọc hoặc Nhận / Xem.";
   }
   else Guard(()=> {
    int n;
    if(Request.Form["lineCount"]==null) return;
    if(!int.TryParse(Request.Form["lineCount"],out n)||n<0||n>100) throw new ArgumentException("Số dòng không hợp lệ.");
    for(int i=0;i<n;i++) lines.Add(new[]{Request.Form["item"+i]??"",Request.Form["qty"+i]??"",Request.Form["price"+i]??""});
   });
  }
  private void ApplyCriteria()
  {
   var start=JournalStore.Date(from.Text); var end=JournalStore.Date(to.Text);
   if(start>end||unit.SelectedValue=="") throw new ArgumentException("Khoảng ngày/đơn vị không hợp lệ.");
   UnitAccess.Demand(unit.SelectedValue);
   // Validate before replacing the active result criteria.
   if(report) store.Report(unit.SelectedValue,start,end);
   else store.List(unit.SelectedValue,start,end,filter.Text);
   Criteria=new[]{unit.SelectedValue,from.Text,to.Text,filter.Text.Trim()}; grid.PageIndex=0; ClearEditor(); Session["CurrentUnit"]=unit.SelectedValue;
  }
  private void ClearEditor() { SelectedId=null; Version=null; editor.Visible=false; selection.Text=""; }
  private void NewDocument()
  {
   if(Criteria==null) throw new ArgumentException("Chọn đơn vị trước.");
   SelectedId=null; Version=null; DraftUnit=Criteria[0]; ReadOnlyMode=false;
   number.Text=""; date.Text=DateTime.Today.ToString("yyyy-MM-dd"); note.Text=""; lines=new List<string[]> { new[]{"","",""} }; editor.Visible=true;
  }
  private void RequireSelection() { if(!SelectedId.HasValue||Criteria==null) throw new ArgumentException("Chọn một phiếu trước."); }
  private void LoadSelected(bool view)
  {
   RequireSelection(); var d=store.Load(SelectedId.Value,Criteria[0]);
   if(!view && Convert.ToString(d.Header["TrangThai"])!="DRAFT") throw new ArgumentException("Chỉ sửa được phiếu nháp.");
   Version=(byte[])d.Header["PhienBan"]; DraftUnit=Convert.ToString(d.Header["MaDonVi"]); ReadOnlyMode=view;
   number.Text=Convert.ToString(d.Header["SoPhieu"]); date.Text=((DateTime)d.Header["Ngay"]).ToString("yyyy-MM-dd"); note.Text=Convert.ToString(d.Header["DienGiai"]);
   lines=d.Lines.Rows.Cast<DataRow>().Select(r=>new[]{Convert.ToString(r["MaTK"]),NumberParser.Format(r["PhatSinhNo"]),NumberParser.Format(r["PhatSinhCo"])}).ToList(); editor.Visible=true;
  }
  private void Save(bool posting)
  {
   if(!editor.Visible || ReadOnlyMode) throw new ArgumentException("Mở phiếu nháp để lưu.");
   store.Save(SelectedId,Version,DraftUnit,number.Text,JournalStore.Date(date.Text),note.Text,JournalStore.ParseLines(lines),posting);
   Done(posting?"Đã ghi sổ phiếu kế toán.":"Đã lưu phiếu nháp; chưa vào báo cáo.");
  }
  private void Export(string format)
  {
   string token=ViewState["OutputToken"] as string;
   if(!report) { RequireSelection(); var doc=store.Load(SelectedId.Value,Criteria[0]); token=ReportOutput.Document(Context,screen,doc.Header,doc.Lines); }
   if(string.IsNullOrEmpty(token)) throw new ArgumentException("Chọn Tìm/Lọc trước khi kết xuất; tối đa 5000 dòng, hãy thu hẹp bộ lọc nếu cần.");
   ReportOutput.Get(new HttpSessionStateWrapper(Session),token);
   Response.Redirect("Output.aspx?token="+token+"&format="+format,false); Context.ApplicationInstance.CompleteRequest(); redirecting=true;
  }
  private void Done(string text)
  {
   Session["JournalFlash"]=text;
   Response.Redirect("Journal.aspx?screen="+HttpUtility.UrlEncode(screen)+"&unit="+HttpUtility.UrlEncode(Criteria[0])+"&from="+Criteria[1]+"&to="+Criteria[2]+"&q="+HttpUtility.UrlEncode(Criteria[3]),false);
   Context.ApplicationInstance.CompleteRequest(); redirecting=true;
  }
  private void Guard(Action action)
  {
   try { action(); }
   catch(HttpException ex) { message.Text=HttpUtility.HtmlEncode(ex.Message); }
   catch(ArgumentException ex) { message.Text=HttpUtility.HtmlEncode(ex.Message); }
   catch(SqlException ex)
   {
    if(ex.Number>=51501 && ex.Number<=51507) message.Text=HttpUtility.HtmlEncode(ex.Message);
    else if(ex.Number==2601||ex.Number==2627) message.Text="Số phiếu đã tồn tại trong đơn vị/loại này.";
    else { Log.Error("Stock","SQL failed",ex); message.Text="Không thể xử lý dữ liệu; hãy làm tươi và thử lại. Kiểm tra kết nối SQL nếu lỗi tiếp diễn."; }
   }
   catch(Exception ex) { Log.Error("Stock","Request failed",ex); message.Text="Không thể xử lý yêu cầu. Kiểm tra dữ liệu rồi thử lại."; }
  }
  private string Details()
  {
   var b=new StringBuilder("<input type=\"hidden\" name=\"lineCount\" value=\""+lines.Count+"\" /><div class=\"catalog-grid-wrap\"><table class=\"catalog-grid\"><thead><tr><th>Tài khoản</th><th>Tên tài khoản</th><th>Nợ</th><th>Có</th></tr></thead><tbody>");
   decimal debit=0,credit=0;
   for(int i=0;i<lines.Count;i++) {
    b.Append("<tr><td>"); Input(b,"item"+i,lines[i][0],"Tài khoản dòng "+(i+1),20,true); b.Append("</td><td>");
    if(items!=null) { var row=items.Rows.Cast<DataRow>().FirstOrDefault(r=>string.Equals((string)r["MaTK"],lines[i][0],StringComparison.OrdinalIgnoreCase)); if(row!=null) b.Append(HttpUtility.HtmlEncode((string)row["TenTK"])); }
    b.Append("</td><td>"); Input(b,"qty"+i,lines[i][1],"Nợ dòng "+(i+1),29,false);
    b.Append("</td><td>"); Input(b,"price"+i,lines[i][2],"Có dòng "+(i+1),29,false); b.Append("</td></tr>");
    try { foreach(var l in JournalStore.ParseLines(new[]{lines[i]})) { debit+=l.Debit; credit+=l.Credit; } } catch(ArgumentException) {}
   }
   b.Append("</tbody><tfoot><tr><td colspan=\"2\">Tổng các dòng hợp lệ (nhấn Tính lại sau khi sửa)</td><td class=\"numeric\">").Append(debit.ToString("N4")).Append("</td><td class=\"numeric\">").Append(credit.ToString("N4")).Append("</td></tr></tfoot></table></div><datalist id=\"journalAccounts\">");
   if(items!=null) foreach(DataRow r in items.Rows) b.Append("<option value=\"").Append(HttpUtility.HtmlAttributeEncode((string)r["MaTK"])).Append("\">").Append(HttpUtility.HtmlEncode((string)r["TenTK"])).Append("</option>");
   return b.Append("</datalist>").ToString();
  }  private void Input(StringBuilder b,string name,string value,string label,int length,bool item)
  {
   b.Append("<input name=\"").Append(name).Append("\" value=\"").Append(HttpUtility.HtmlAttributeEncode(value)).Append("\" aria-label=\"").Append(label).Append("\" maxlength=\"").Append(length).Append("\"");
   if(item) b.Append(" list=\"journalAccounts\""); else b.Append(" inputmode=\"decimal\"");
   if(ReadOnlyMode) b.Append(" readonly"); b.Append(" />");
  }
  protected override void OnPreRender(EventArgs e)
  {
   if(!redirecting) Guard(()=> {
    if(Criteria!=null)
    {
     var data=report?store.Report(Criteria[0],JournalStore.Date(Criteria[1]),JournalStore.Date(Criteria[2])):
      store.List(Criteria[0],JournalStore.Date(Criteria[1]),JournalStore.Date(Criteria[2]),Criteria[3]);
     grid.PageIndex=Math.Min(grid.PageIndex,Math.Max(0,(data.Rows.Count-1)/grid.PageSize)); grid.DataSource=data; grid.DataBind();
     if(grid.HeaderRow!=null) grid.HeaderRow.TableSection=TableRowSection.TableHeader;
     summary.Text=data.Rows.Count+" dòng • Điều kiện đang áp dụng: "+HttpUtility.HtmlEncode(string.Join(" | ",Criteria));
     if(report) {
      var check=data.Rows.Cast<DataRow>().Single(r=>(string)r["Ma"]=="CHECK");
      summary.Text+=" • Chênh lệch đầu/cuối: "+((decimal)check["DauKy"]).ToString("N4")+" / "+((decimal)check["CuoiKy"]).ToString("N4");
     }    }
    if(report && Criteria!=null && grid.DataSource is DataTable) ViewState["OutputToken"]=ReportOutput.Capture(Context,screen,"Điều kiện: "+string.Join(" | ",Criteria),summary.Text,(DataTable)grid.DataSource,grid,Criteria);
    if(editor.Visible)
    {
     editorUnit.Text="Đơn vị: "+HttpUtility.HtmlEncode(DraftUnit)+(ReadOnlyMode?" • Chế độ xem":" • Phiếu nháp");
     number.ReadOnly=date.ReadOnly=note.ReadOnly=ReadOnlyMode;
     save.Enabled=post.Enabled=add.Enabled=remove.Enabled=!ReadOnlyMode;
     detailHtml.Text=Details();
    }
   });
   base.OnPreRender(e);
  }
  protected override void Render(HtmlTextWriter writer) { if(!redirecting) base.Render(writer); }
 }
}




