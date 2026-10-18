using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace ReportingModule.Web
{
 public partial class CatalogPage : AppPage
 {
  protected PlaceHolder Root;
  private CatalogDefinition definition;
  private CatalogStore store;
  private GridView grid;
  private TextBox filter;
  private Panel editor, importer;
  private Label message, selected, count;
  private FileUpload upload;
  private readonly List<TextBox> inputs=new List<TextBox>();
  private bool downloading;
  private object[] Original { get { return ViewState["Original"] as object[]; } set { ViewState["Original"]=value; } }
  private string EffectiveFilter { get { return (string)ViewState["Filter"]??""; } set { ViewState["Filter"]=value; } }
  protected override void OnInit(EventArgs e)
  {
   base.OnInit(e);
   Session["CatalogSession"]=true;
   Response.Cache.SetCacheability(HttpCacheability.NoCache);
   Response.Cache.SetNoStore();
   try { definition=CatalogDefinition.Get(Request.QueryString["screen"]); }
   catch(ArgumentException) { throw new HttpException(404,"Danh mục không tồn tại."); }
   ViewStateUserKey=Session.SessionID+":"+definition.Id;
   store=new CatalogStore();
   Title=definition.Title+" | Kế toán";
   var heading=new HtmlGenericControl("h1"); heading.InnerText=definition.Title; Root.Controls.Add(heading);
   var toolbar=new Panel { CssClass="context-toolbar" }; Root.Controls.Add(toolbar);
   AddButton(toolbar,"New","Mới",()=>{Original=null; foreach(var input in inputs) input.Text=""; editor.Visible=true; importer.Visible=false;});
   AddButton(toolbar,"Edit","Sửa",()=>{RequireSelection(); FillEditor(); editor.Visible=true; importer.Visible=false;});
   var delete=AddButton(toolbar,"Delete","Xóa",()=>{RequireSelection(); store.Delete(definition,Original); Done("Đã xóa bản ghi.");});
   delete.OnClientClick="return confirm('Xóa bản ghi đang chọn?');";
   AddButton(toolbar,"Search","Tìm",()=>{EffectiveFilter=filter.Text.Trim(); grid.PageIndex=0; ClearSelection();});
   AddButton(toolbar,"Import","Import",()=>{importer.Visible=true; editor.Visible=false;});
   AddButton(toolbar,"Export","Export",()=>Download(false));
   AddButton(toolbar,"Refresh","Làm tươi",()=>{ClearSelection(); message.Text="Đã tải lại dữ liệu theo bộ lọc hiện tại.";});
   var filters=new Panel { CssClass="catalog-filter" }; Root.Controls.Add(filters);
   filter=new TextBox { ID="Filter", MaxLength=100 };
   filters.Controls.Add(new Label { AssociatedControlID="Filter", Text="Mã hoặc tên" });
   filters.Controls.Add(filter);
   AddButton(filters,"ApplyFilter","Áp dụng",()=>{EffectiveFilter=filter.Text.Trim(); grid.PageIndex=0; ClearSelection();});
   AddButton(filters,"ClearFilter","Bỏ lọc",()=>{EffectiveFilter=""; grid.PageIndex=0; ClearSelection();});
   message=new Label { ID="Message", CssClass="catalog-message" }; message.Attributes["role"]="status"; Root.Controls.Add(message);
   selected=new Label { ID="Selected", CssClass="catalog-selection" }; Root.Controls.Add(selected);
   editor=new Panel { ID="Editor", CssClass="catalog-editor", Visible=false }; Root.Controls.Add(editor);
   var editTitle=new HtmlGenericControl("h2"); editTitle.InnerText="Thông tin danh mục"; editor.Controls.Add(editTitle);
   var fields=new Panel { CssClass="catalog-fields" }; editor.Controls.Add(fields);
   for(int i=0;i<definition.Fields.Length;i++)
   {
    var field=definition.Fields[i]; var group=new Panel { CssClass="catalog-field" }; fields.Controls.Add(group);
    var input=new TextBox { ID="Value"+i, MaxLength=field.Kind=="text"?field.Length:30 };
    group.Controls.Add(new Label { AssociatedControlID=input.ID,Text=field.Label+(field.Required?" *":"") });
    if(field.Kind=="date") input.TextMode=TextBoxMode.Date;
    if(field.Kind=="decimal") input.Attributes["inputmode"]="decimal";
    group.Controls.Add(input); inputs.Add(input);
   }
   var help=new HtmlGenericControl("p"); help.Attributes["class"]="muted"; help.InnerText=definition.Id=="units"?"* Bắt buộc. Thông tin pháp lý để trống nếu chưa có dữ liệu xác thực.":"* Bắt buộc. Giá dùng dấu phẩy thập phân; mã không đổi khi sửa."; editor.Controls.Add(help);
   AddButton(editor,"Save","Lưu",()=> {
    var values=inputs.Select(t=>t.Text).ToArray();
    if(Original==null) store.Create(definition,values); else store.Update(definition,values,Original);
    Done("Đã lưu bản ghi.");
   });
   AddButton(editor,"Cancel","Hủy",()=>{editor.Visible=false;});
   importer=new Panel { ID="Importer", CssClass="catalog-editor", Visible=false }; Root.Controls.Add(importer);
   var note=new HtmlGenericControl("p"); note.InnerText="Nhập .xlsx một sheet, tối đa 5 MB / 5000 dòng. Dùng đúng cột trong file mẫu; không dùng công thức. Mã trùng hoặc dòng sai sẽ hủy toàn bộ import."; importer.Controls.Add(note);
   upload=new FileUpload { ID="Workbook" }; upload.Attributes["accept"]=".xlsx"; importer.Controls.Add(upload);
   AddButton(importer,"Template","Tải file mẫu",()=>Download(true));
   AddButton(importer,"CommitImport","Kiểm tra và nhập",()=> {
    if(!upload.HasFile || !string.Equals(Path.GetExtension(upload.FileName),".xlsx",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Chọn file .xlsx.");
    if(upload.PostedFile.ContentLength>5*1024*1024) throw new ArgumentException("File tối đa 5 MB.");
    var rows=CatalogExcel.Import(definition,upload.FileContent);
    int written=store.Import(definition,rows); Done("Đã nhập "+written+" dòng.");
   });
   AddButton(importer,"CloseImport","Đóng",()=>{importer.Visible=false;});
   var container=new Panel { CssClass="catalog-grid-wrap" }; Root.Controls.Add(container);
   grid=new GridView { ID="Records", CssClass="catalog-grid", AutoGenerateColumns=false, AllowPaging=true, PageSize=20,
    DataKeyNames=new[]{definition.Fields[0].Name}, EmptyDataText="Không có dữ liệu phù hợp.", GridLines=GridLines.None };
   grid.Columns.Add(new CommandField { ShowSelectButton=true, SelectText="Chọn" });
   foreach(var f in definition.Fields)
   {
    var col=new BoundField { DataField=f.Name,HeaderText=f.Label,HtmlEncode=true };
    if(f.Kind=="decimal") { col.DataFormatString="{0:N4}"; col.ItemStyle.CssClass="numeric"; }
    if(f.Kind=="date") col.DataFormatString="{0:dd/MM/yyyy}";
    grid.Columns.Add(col);
   }
   grid.SelectedIndexChanged+=(s,args)=>Guard(()=> {
    Original=store.Find(definition,Convert.ToString(grid.SelectedDataKey.Value)); editor.Visible=false;
   });
   grid.SelectedRowStyle.CssClass="selected";
   grid.PageIndexChanging+=(s,args)=>Guard(()=>{grid.PageIndex=args.NewPageIndex; ClearSelection();});
   container.Controls.Add(grid);
   count=new Label { CssClass="screen-status" }; Root.Controls.Add(count);
  }
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e);
   if(!IsPostBack)
   {
    EffectiveFilter=Request.QueryString["q"]??"";
    if(EffectiveFilter.Length>100) EffectiveFilter=EffectiveFilter.Substring(0,100);
    string flash=Session["CatalogFlash"] as string;
    if(flash!=null) { message.Text=HttpUtility.HtmlEncode(flash); Session.Remove("CatalogFlash"); }
   }
  }
  private Button AddButton(Control parent,string id,string text,Action action)
  {
   string permission=PermissionMap.Action(definition.Id,id,false);
   var button=new Button { ID=id, Text=text, CausesValidation=false, CssClass="catalog-button" };
   if(id=="Save")button.Visible=Can(PermissionMap.Action(definition.Id,"New",false))||Can(PermissionMap.Action(definition.Id,"Edit",false));else button.Visible=Can(permission);
   button.Click+=(s,e)=>Guard(()=>RunAuthorized(definition.Id,id,action,entity:definition.Id,editing:Original!=null)); parent.Controls.Add(button); return button;
  }
  private void RequireSelection() { if(Original==null) throw new ArgumentException("Hãy chọn một dòng trước."); }
  private void FillEditor() { for(int i=0;i<inputs.Count;i++) inputs[i].Text=definition.Fields[i].Kind=="decimal"?NumberParser.Format(Original[i]):CatalogDefinition.Text(Original[i]); }
  private void ClearSelection() { Original=null; editor.Visible=false; grid.SelectedIndex=-1; }
  private void Guard(Action action)
  {
   try { action(); }
   catch(ArgumentException ex) { message.Text=HttpUtility.HtmlEncode(ex.Message); }
   catch(InvalidDataException) { message.Text="File Excel không hợp lệ."; }
   catch(System.Xml.XmlException) { message.Text="XML trong file Excel không hợp lệ."; }
   catch(SqlException ex)
   {
    if(ex.Number==2601||ex.Number==2627) message.Text="Mã đã tồn tại; không ghi đè.";
    else if(ex.Number==547) message.Text="Không thể xóa dữ liệu đã được tham chiếu hoặc ghi dữ liệu vi phạm ràng buộc.";
    else { Log.Error("Catalog","SQL operation failed",ex); message.Text="Không thể truy cập dữ liệu. Kiểm tra SQL Server và quyền kết nối rồi thử lại."; }
   }
   catch(Exception ex)
   {
    Log.Error("Catalog","Operation failed",ex);
    message.Text="Không thể xử lý yêu cầu. File phải đúng mẫu và dữ liệu phải hợp lệ.";
   }
  }
  private void Done(string text)
  {
   Session["CatalogFlash"]=text;
   Response.Redirect("Catalog.aspx?screen="+HttpUtility.UrlEncode(definition.Id)+"&q="+HttpUtility.UrlEncode(EffectiveFilter),false);
   Context.ApplicationInstance.CompleteRequest();
   downloading=true;
  }
  private void Download(bool template)
  {
   var table=template?new DataTable():store.List(definition,EffectiveFilter);
   byte[] bytes=CatalogExcel.Export(definition,table);
   Response.Clear();
   Response.ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
   Response.AddHeader("Content-Disposition","attachment; filename="+definition.Id+(template?"-template":"")+".xlsx");
   Response.BinaryWrite(bytes);
   Context.ApplicationInstance.CompleteRequest(); downloading=true;
  }
  protected override void OnPreRender(EventArgs e)
  {
   if(!downloading) Guard(()=> {
    var table=store.List(definition,EffectiveFilter);
    int maxPage=Math.Max(0,(table.Rows.Count-1)/grid.PageSize);
    grid.PageIndex=Math.Min(grid.PageIndex,maxPage);
    grid.DataSource=table; grid.DataBind();
    if(grid.HeaderRow!=null) grid.HeaderRow.TableSection=TableRowSection.TableHeader;
    filter.Text=EffectiveFilter;
    inputs[0].ReadOnly=Original!=null;
    selected.Text=Original==null?"Chưa chọn bản ghi.":"Đang chọn: "+HttpUtility.HtmlEncode(CatalogDefinition.Text(Original[0]));
    count.Text=table.Rows.Count+" bản ghi • Trang "+(grid.PageIndex+1)+"/"+(maxPage+1);
   });
   base.OnPreRender(e);
  }
  protected override void Render(HtmlTextWriter writer) { if(!downloading) base.Render(writer); }
 }
}

