using System;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
namespace ReportingModule.Web
{
 public partial class SystemAdminPage:AppPage
 {
  protected PlaceHolder Root;private AdminStore store;private string screen;private Label message;private GridView grid;
  private Guid? SelectedUser { get { Guid id;return Guid.TryParse(Request.QueryString["user"],out id)?(Guid?)id:null; } }
  protected override void OnInit(EventArgs e)
  {
   base.OnInit(e);screen=Request.QueryString["screen"];if(!new[]{"admin-users","admin-roles","admin-audit"}.Contains(screen))throw new HttpException(404,"Chức năng không tồn tại.");
   RequirePermission(PermissionMap.View(screen));store=new AdminStore();string module;var node=Navigation.Find(screen,out module);Title=node.Title+" | "+AppIdentity.ProductName;
   var crumb=new HtmlGenericControl("p");crumb.Attributes["class"]="breadcrumb";crumb.InnerText="Hệ thống / Quản trị";Root.Controls.Add(crumb);
   var h=new HtmlGenericControl("h1");h.InnerText=node.Title;Root.Controls.Add(h);
   var toolbar=new Panel{CssClass="context-toolbar"};Root.Controls.Add(toolbar);
   foreach(var item in new[]{new[]{"admin-users","Quản lý người dùng"},new[]{"admin-roles","Vai trò & phân quyền"},new[]{"admin-audit","Nhật ký hệ thống"}}){if(Can(PermissionMap.View(item[0])))toolbar.Controls.Add(new HyperLink{Text=item[1],NavigateUrl="SystemAdmin.aspx?screen="+item[0],CssClass="catalog-button"});}
   AddButton(toolbar,"Refresh","Làm tươi",()=>{Response.Redirect(Request.RawUrl,false);Context.ApplicationInstance.CompleteRequest();},PermissionMap.View(screen));
   message=new Label{CssClass="catalog-message",ID="Message"};message.Attributes["role"]="status";Root.Controls.Add(message);
   if(screen=="admin-users")BuildUsers();else if(screen=="admin-roles")BuildRoles();else BuildAudit();
  }
  private Button AddButton(Control parent,string id,string text,Action action,string permission)
  {
   var b=new Button{ID=id,Text=text,CssClass="catalog-button",CausesValidation=false,Visible=Can(permission)};
   b.Click+=(s,e)=>Guard(()=>{RequirePermission(permission);action();});parent.Controls.Add(b);return b;
  }
  private string AdminPermission {get{return PermissionMap.View(screen);}}
  private void Guard(Action work)
  {try{work();}catch(ArgumentException ex){message.Text=HttpUtility.HtmlEncode(ex.Message);}catch(System.Data.SqlClient.SqlException ex){if(ex.Number==2601||ex.Number==2627)message.Text="Mã hoặc tên đăng nhập đã tồn tại.";else{Log.Error("Admin","Administration query failed",ex);message.Text="Không thể xử lý yêu cầu quản trị.";}}catch(Exception ex){Log.Error("Admin","Administration failed",ex);message.Text="Không thể xử lý yêu cầu quản trị.";}}
  private void BuildUsers()
  {
   var search=new TextBox{ID="UserSearch",MaxLength=64};Root.Controls.Add(search);
   AddButton(Root,"SearchUsers","Tìm người dùng",()=>{Response.Redirect("SystemAdmin.aspx?screen=admin-users&q="+HttpUtility.UrlEncode(search.Text),false);Context.ApplicationInstance.CompleteRequest();},AdminPermission);
   var data=store.Users(Request.QueryString["q"]??"");
   grid=new GridView{ID="Users",CssClass="catalog-grid",AutoGenerateColumns=false,DataKeyNames=new[]{"UserId"},EmptyDataText="Chưa có người dùng."};
   grid.Columns.Add(new CommandField{ShowSelectButton=true,SelectText="Mở"});
   foreach(var col in new[]{new[]{"Username","Tên đăng nhập"},new[]{"DisplayName","Tên hiển thị"},new[]{"Roles","Vai trò"},new[]{"IsEnabled","Đang hoạt động"},new[]{"FailedLoginCount","Lần đăng nhập sai"},new[]{"LockedUntilUtc","Khóa đến UTC"},new[]{"LastLoginAtUtc","Đăng nhập gần nhất UTC"}})grid.Columns.Add(new BoundField{DataField=col[0],HeaderText=col[1],HtmlEncode=true});
   grid.SelectedIndexChanged+=(s,e)=>{var id=(Guid)grid.SelectedDataKey.Value;Response.Redirect("SystemAdmin.aspx?screen=admin-users&user="+id.ToString("D"),false);Context.ApplicationInstance.CompleteRequest();};
   grid.DataSource=data;grid.DataBind();if(grid.HeaderRow!=null)grid.HeaderRow.TableSection=TableRowSection.TableHeader;Root.Controls.Add(new Panel{CssClass="catalog-grid-wrap"});Root.Controls[Root.Controls.Count-1].Controls.Add(grid);
   BuildUserEditor();
  }
  private void BuildUserEditor()
  {
   DataRow row=SelectedUser.HasValue?store.User(SelectedUser.Value):null;var panel=new Panel{CssClass="catalog-editor"};Root.Controls.Add(panel);
   var title=new HtmlGenericControl("h2");title.InnerText=row==null?"Tạo người dùng":"Cập nhật người dùng";panel.Controls.Add(title);
   var fields=new Panel{CssClass="catalog-fields"};panel.Controls.Add(fields);
   TextBox username=Field(fields,"Tên đăng nhập",Convert.ToString(row==null?"":row["Username"]),"Username");
   TextBox display=Field(fields,"Tên hiển thị",Convert.ToString(row==null?"":row["DisplayName"]),"DisplayName");
   TextBox password=Field(fields,row==null?"Mật khẩu ban đầu":"Đặt mật khẩu mới (để trống nếu không đổi)","","NewPassword");password.TextMode=TextBoxMode.Password;password.Attributes["autocomplete"]="new-password";
   var enabled=new CheckBox{ID="Enabled",Text="Tài khoản đang hoạt động",Checked=row==null||Convert.ToBoolean(row["IsEnabled"])};panel.Controls.Add(enabled);
   var roles=new CheckBoxList{ID="Roles",RepeatLayout=RepeatLayout.Flow,RepeatDirection=RepeatDirection.Horizontal,CssClass="admin-check-list"};
   roles.DataSource=store.Roles();roles.DataTextField="RoleName";roles.DataValueField="RoleCode";roles.DataBind();var currentRoles=SelectedUser.HasValue?store.UserRoles(SelectedUser.Value):new[]{"User"};foreach(ListItem item in roles.Items)item.Selected=currentRoles.Contains(item.Value,StringComparer.OrdinalIgnoreCase);
   panel.Controls.Add(new Literal{Text="<h3>Vai trò</h3>"});panel.Controls.Add(roles);
   var units=new CheckBoxList{ID="Units",RepeatLayout=RepeatLayout.Flow,RepeatDirection=RepeatDirection.Horizontal,CssClass="admin-check-list"};
   units.DataSource=store.Units();units.DataTextField="TenDonVi";units.DataValueField="MaDonVi";units.DataBind();var currentUnits=SelectedUser.HasValue?store.UserUnits(SelectedUser.Value):new string[0];foreach(ListItem item in units.Items)item.Selected=currentUnits.Contains(item.Value,StringComparer.OrdinalIgnoreCase);
   panel.Controls.Add(new Literal{Text="<h3>Đơn vị được phép truy cập</h3>"});panel.Controls.Add(units);
   var defaultUnit=new DropDownList{ID="DefaultUnit",CssClass="admin-default-unit"};defaultUnit.Items.Add(new ListItem("Chọn đơn vị mặc định", ""));
   foreach(ListItem item in units.Items)defaultUnit.Items.Add(new ListItem(item.Text+" ("+item.Value+")",item.Value));
   string savedDefault=SelectedUser.HasValue?store.DefaultUnit(SelectedUser.Value):"";
   if(!string.IsNullOrEmpty(savedDefault)&&defaultUnit.Items.FindByValue(savedDefault)!=null)defaultUnit.SelectedValue=savedDefault;
   else if(currentUnits.Length==1&&defaultUnit.Items.FindByValue(currentUnits[0])!=null)defaultUnit.SelectedValue=currentUnits[0];
   var defaultGroup=new Panel{CssClass="catalog-field"};defaultGroup.Controls.Add(new Label{Text="Đơn vị mặc định khi đăng nhập",AssociatedControlID=defaultUnit.ID});defaultGroup.Controls.Add(defaultUnit);panel.Controls.Add(defaultGroup);
   panel.Controls.Add(new Literal{Text="<p class=\"muted\">Tài khoản không bị xóa để giữ nguyên tham chiếu nhật ký. Mật khẩu không được đọc lại.</p>"});
   AddButton(panel,"SaveUser","Lưu người dùng",()=>{var allowed=units.Items.Cast<ListItem>().Where(x=>x.Selected).Select(x=>x.Value).ToArray();store.SaveUser(SelectedUser,username.Text,display.Text,enabled.Checked,password.Text,roles.Items.Cast<ListItem>().Where(x=>x.Selected).Select(x=>x.Value).ToArray(),allowed,defaultUnit.SelectedValue,UserId);password.Text="";message.Text="Đã lưu người dùng.";},AdminPermission);
   if(SelectedUser.HasValue)AddButton(panel,"UnlockUser","Mở khóa đăng nhập",()=>{store.Unlock(SelectedUser.Value,UserId);message.Text="Đã mở khóa tài khoản.";;},AdminPermission);
  }
  private static TextBox Field(Control parent,string label,string value,string id)
  {var box=new TextBox{ID=id,Text=value,MaxLength=id=="Username"?64:120};var group=new Panel{CssClass="catalog-field"};group.Controls.Add(new Label{Text=label,AssociatedControlID=id});group.Controls.Add(box);parent.Controls.Add(group);return box;}
  private void BuildRoles()
  {
   var roles=store.Roles();var selectedId=int.TryParse(Request.QueryString["role"],out var parsed)?parsed:0;
   var selector=new DropDownList{ID="RoleId",AutoPostBack=true};selector.Items.Add(new ListItem("Tạo vai trò mới","0"));
   foreach(DataRow r in roles.Rows)selector.Items.Add(new ListItem(Convert.ToString(r["RoleName"])+(Convert.ToBoolean(r["IsSystem"])?" (dựng sẵn)":""),Convert.ToString(r["RoleId"])));
   selector.SelectedValue=selectedId.ToString();Root.Controls.Add(new Label{Text="Vai trò",AssociatedControlID="RoleId"});Root.Controls.Add(selector);
   selector.SelectedIndexChanged+=(s,e)=>{Response.Redirect("SystemAdmin.aspx?screen=admin-roles&role="+HttpUtility.UrlEncode(selector.SelectedValue),false);Context.ApplicationInstance.CompleteRequest();};
   DataRow role=roles.Rows.Cast<DataRow>().FirstOrDefault(r=>Convert.ToInt32(r["RoleId"])==selectedId);
   TextBox code=Field(Root,"Mã vai trò",role==null?"":Convert.ToString(role["RoleCode"]),"RoleCode");
   TextBox name=Field(Root,"Tên vai trò",role==null?"":Convert.ToString(role["RoleName"]),"RoleName");
   bool system=role!=null&&Convert.ToBoolean(role["IsSystem"]);code.ReadOnly=system;name.ReadOnly=system;
   string[] selected=role==null?new string[0]:store.RolePermissions(selectedId);
   var checks=new CheckBoxList{ID="Permissions",RepeatLayout=RepeatLayout.Flow,CssClass="permission-matrix"};
   foreach(DataRow p in store.Permissions().Rows)
   {
    string codeValue=Convert.ToString(p["PermissionCode"]);
    checks.Items.Add(new ListItem(AdminStore.PermissionLabel(Convert.ToString(p["ModuleCode"]),Convert.ToString(p["DisplayName"])),codeValue){Selected=selected.Contains(codeValue,StringComparer.OrdinalIgnoreCase)});
   }
   Root.Controls.Add(new Literal{Text="<h2>Quyền hiệu lực theo vai trò</h2><p class=\"muted\">Các quyền được kết hợp theo phép hợp của mọi vai trò đang bật. Thiếu quyền thì từ chối.</p>"});Root.Controls.Add(checks);
   if(!system)AddButton(Root,"SaveRole","Lưu vai trò và quyền",()=>{int? id=selectedId>0?(int?)selectedId:null;store.SaveRole(id,code.Text,name.Text,checks.Items.Cast<ListItem>().Where(x=>x.Selected).Select(x=>x.Value).ToArray(),UserId);message.Text="Đã lưu vai trò và quyền.";},AdminPermission);
   if(role!=null&&!system)AddButton(Root,"DisableRole","Vô hiệu hóa vai trò",()=>{store.DisableRole(selectedId,UserId);Response.Redirect("SystemAdmin.aspx?screen=admin-roles",false);Context.ApplicationInstance.CompleteRequest();},AdminPermission);
  }
  private void BuildAudit()
  {
   var fields=new Panel{CssClass="catalog-filter"};Root.Controls.Add(fields);
   var from=Field(fields,"Từ ngày",Request.QueryString["from"]??"","From");from.TextMode=TextBoxMode.Date;
   var to=Field(fields,"Đến ngày",Request.QueryString["to"]??"","To");to.TextMode=TextBoxMode.Date;
   var user=Field(fields,"Người dùng",Request.QueryString["user"]??"","AuditUser");
   var module=Field(fields,"Phân hệ",Request.QueryString["module"]??"","Module");
   var action=Field(fields,"Thao tác",Request.QueryString["action"]??"","Action");
   var outcome=Field(fields,"Kết quả",Request.QueryString["outcome"]??"","Outcome");
   AddButton(fields,"SearchAudit","Lọc nhật ký",()=>{Response.Redirect("SystemAdmin.aspx?screen=admin-audit&from="+HttpUtility.UrlEncode(from.Text)+"&to="+HttpUtility.UrlEncode(to.Text)+"&user="+HttpUtility.UrlEncode(user.Text)+"&module="+HttpUtility.UrlEncode(module.Text)+"&action="+HttpUtility.UrlEncode(action.Text)+"&outcome="+HttpUtility.UrlEncode(outcome.Text),false);Context.ApplicationInstance.CompleteRequest();},AdminPermission);
   grid=new GridView{ID="Audit",CssClass="catalog-grid",AutoGenerateColumns=false,EmptyDataText="Không có bản ghi phù hợp."};
   foreach(var col in new[]{new[]{"OccurredAtUtc","Thời điểm UTC"},new[]{"ActorUsername","Người dùng"},new[]{"UnitCode","Đơn vị"},new[]{"ModuleCode","Phân hệ"},new[]{"FunctionCode","Chức năng"},new[]{"ActionCode","Thao tác"},new[]{"EntityIdentifier","Mã đối tượng"},new[]{"Outcome","Kết quả"},new[]{"SafeSummary","Mô tả"}})grid.Columns.Add(new BoundField{DataField=col[0],HeaderText=col[1],HtmlEncode=true});
   grid.DataSource=store.Audit(from.Text,to.Text,user.Text,module.Text,action.Text,outcome.Text);grid.DataBind();if(grid.HeaderRow!=null)grid.HeaderRow.TableSection=TableRowSection.TableHeader;
   var wrap=new Panel{CssClass="catalog-grid-wrap"};wrap.Controls.Add(grid);Root.Controls.Add(wrap);
   Root.Controls.Add(new Literal{Text="<p class=\"muted\">Nhật ký chỉ đọc trong ứng dụng, mới nhất trước, tối đa 1.000 dòng mỗi truy vấn.</p>"});
  }
 }
}
