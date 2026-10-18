using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Linq;
namespace ReportingModule.Web
{
 public partial class SiteShell : MasterPage
 {
  protected PlaceHolder IdentityPanel;
  protected HiddenField CsrfToken;
 private DropDownList workingUnit;
 private Literal workingUnitLabel;
  protected override void OnInit(EventArgs e)
  {
   base.OnInit(e);
   if(Page.Session!=null && Page.Session["CsrfToken"]==null) Page.Session["CsrfToken"]=Guid.NewGuid().ToString("N");
   if(CsrfToken!=null&&Page.Session!=null) CsrfToken.Value=Convert.ToString(Page.Session["CsrfToken"]);
   var id=CurrentIdentity.CurrentUserId();
   if(id.HasValue&&IdentityPanel!=null)
   {
    var who=IdentityStore.Get(id.Value);
    if(who!=null)
    {
     ListItem[] units=UnitAccess.Items(id.Value).ToArray();
     string current=Convert.ToString(Page.Session["CurrentUnit"]);
     if(!units.Any(x=>string.Equals(x.Value,current,StringComparison.OrdinalIgnoreCase)))
     { current=UnitAccess.PreferredUnit(id.Value);Page.Session["CurrentUnit"]=current; }
     IdentityPanel.Controls.Add(new Literal { Text="<span class=\"identity-panel\"><span class=\"identity-name\">"+HttpUtility.HtmlEncode(who.DisplayName)+" / "+HttpUtility.HtmlEncode(who.Username)+"</span><span class=\"identity-role\">"+HttpUtility.HtmlEncode(string.Join(", ",who.Roles))+"</span><span class=\"identity-unit\">Đơn vị:</span>" });
     workingUnit=new DropDownList { ID="WorkingUnit",CssClass="working-unit-picker",AutoPostBack=true,CausesValidation=false };
     workingUnit.Attributes["aria-label"]="Đơn vị làm việc";
     foreach(ListItem item in units) workingUnit.Items.Add(new ListItem(item.Text+" ("+item.Value+")",item.Value));
     if(workingUnit.Items.FindByValue(current)!=null) workingUnit.SelectedValue=current;
     workingUnit.Visible=units.Length>1;
     workingUnit.Visible=workingUnit.Visible&&!string.Equals(VirtualPathUtility.GetFileName(Page.AppRelativeVirtualPath),"Output.aspx",StringComparison.OrdinalIgnoreCase);
     workingUnit.SelectedIndexChanged+=ChangeWorkingUnit;
     IdentityPanel.Controls.Add(workingUnit);
     workingUnitLabel=new Literal { Text=HttpUtility.HtmlEncode(current) };
     workingUnitLabel.Visible=units.Length<=1;
     IdentityPanel.Controls.Add(workingUnitLabel);
     IdentityPanel.Controls.Add(new Literal { Text="<a class=\"header-action\" href=\"ChangePassword.aspx\">Đổi mật khẩu</a><a class=\"header-action\" href=\"Logout.aspx\">Đăng xuất</a></span>" });
    }
   }
  }
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e);
   if(CsrfToken!=null&&Page.Session!=null&&Page.Session["CsrfToken"]!=null) CsrfToken.Value=Convert.ToString(Page.Session["CsrfToken"]);
  }
  private void ChangeWorkingUnit(object sender,EventArgs e)
  {
   string expected=Convert.ToString(Page.Session["CsrfToken"]),actual=Page.Request.Form[CsrfToken.UniqueID];
   if(string.IsNullOrEmpty(expected)||!string.Equals(expected,actual,StringComparison.Ordinal)) throw new HttpException(400,"Yêu cầu không hợp lệ. Hãy tải lại trang.");
   var id=CurrentIdentity.CurrentUserId();
   if(!id.HasValue) throw new HttpException(401,"Yêu cầu đăng nhập.");
   UnitAccess.Demand(workingUnit.SelectedValue);
   Page.Session["CurrentUnit"]=workingUnit.SelectedValue;
   string screen=Page.Request.QueryString["screen"],module;
   string path=VirtualPathUtility.ToAbsolute(Page.Request.AppRelativeCurrentExecutionFilePath);
   string target=path;
   if(!string.IsNullOrEmpty(screen)&&Navigation.Find(screen,out module)!=null)
    target+="?screen="+HttpUtility.UrlEncode(screen)+"&unit="+HttpUtility.UrlEncode(workingUnit.SelectedValue);
   Page.Response.Redirect(target,true);
  }
  protected override void OnPreRender(EventArgs e)
  {
   base.OnPreRender(e);
   string current=Convert.ToString(Page.Session["CurrentUnit"]);
   if(workingUnit!=null&&workingUnit.Items.FindByValue(current)!=null) workingUnit.SelectedValue=current;
   if(workingUnitLabel!=null) workingUnitLabel.Text=HttpUtility.HtmlEncode(current);
  }
  protected string RenderNavigation()
  {
   var html = new StringBuilder();
   string selected = Request.QueryString["screen"] ?? "general";
   foreach (var module in Navigation.Modules)
   {
    if(!module.Children.Any(HasVisibleChild)) continue;
    bool active = module.Contains(selected);
    html.Append("<li class=\"module").Append(active ? " active" : "").Append("\"><details><summary>")
        .Append(HttpUtility.HtmlEncode(module.Title)).Append("</summary><ul class=\"dropdown\">");
    RenderChildren(html, module.Children, selected);
    html.Append("</ul></details></li>");
   }
   return html.ToString();
  }
  private static void RenderChildren(StringBuilder html, MenuNode[] nodes, string selected)
  {
   foreach (var node in nodes)
   {
    bool visible=node.Children.Length>0?node.Children.Any(x=>HasVisibleChild(x)):PermissionService.IsAllowed(CurrentIdentity.CurrentUserId()??Guid.Empty,PermissionMap.View(node.Id));
    if(!visible) continue;
    html.Append("<li>");
    if (node.Children.Length > 0)
    {
     html.Append("<details class=\"nested\"><summary>").Append(HttpUtility.HtmlEncode(node.Title)).Append("</summary><ul>");
     RenderChildren(html, node.Children, selected);
     html.Append("</ul></details>");
    }
    else
    {
     html.Append("<a href=\"").Append(Navigation.PageFor(node.Id)).Append("?screen=").Append(HttpUtility.UrlEncode(node.Id)).Append("\"");
     if (node.Id == selected) html.Append(" aria-current=\"page\"");
     html.Append(">").Append(HttpUtility.HtmlEncode(node.Title)).Append("</a>");
    }
    html.Append("</li>");
   }
  }
  private static bool HasVisibleChild(MenuNode node)
  {
   if(node.Children.Length>0)return node.Children.Any(HasVisibleChild);
   var id=CurrentIdentity.CurrentUserId();return id.HasValue&&PermissionService.IsAllowed(id.Value,PermissionMap.View(node.Id));
  }
 }
}
