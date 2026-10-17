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
  protected override void OnInit(EventArgs e)
  {
   base.OnInit(e);
   if(Page.Session!=null && Page.Session["CsrfToken"]==null) Page.Session["CsrfToken"]=Guid.NewGuid().ToString("N");
   if(CsrfToken!=null&&Page.Session!=null) CsrfToken.Value=Convert.ToString(Page.Session["CsrfToken"]);
  }
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e);
   if(Page.Session!=null)
   {
    if(CsrfToken!=null) CsrfToken.Value=Convert.ToString(Page.Session["CsrfToken"]);
    var id=CurrentIdentity.CurrentUserId();
    if(id.HasValue && IdentityPanel!=null)
    {
     var who=IdentityStore.Get(id.Value);
     if(who!=null)
     {
      string[] units=UnitAccess.Items(id.Value).Select(x=>x.Value).ToArray();
      string current=Convert.ToString(Page.Session["CurrentUnit"]);
      if(!units.Contains(current)) { current=units.FirstOrDefault()??"";Page.Session["CurrentUnit"]=current; }
      var identity=new Literal { Text="<span class=\"identity-panel\"><span class=\"identity-name\">"+HttpUtility.HtmlEncode(who.DisplayName)+" / "+HttpUtility.HtmlEncode(who.Username)+"</span><span class=\"identity-role\">"+HttpUtility.HtmlEncode(string.Join(", ",who.Roles))+"</span><span class=\"identity-unit\">Đơn vị: "+HttpUtility.HtmlEncode(current)+"</span><a class=\"header-action\" href=\"ChangePassword.aspx\">Đổi mật khẩu</a><a class=\"header-action\" href=\"Logout.aspx\">Đăng xuất</a></span>" };
      IdentityPanel.Controls.Add(identity);
     }
    }
   }
   if(CsrfToken!=null&&Page.Session!=null&&Page.Session["CsrfToken"]!=null) CsrfToken.Value=Convert.ToString(Page.Session["CsrfToken"]);
  }
  protected string RenderNavigation()
  {
   var html = new StringBuilder();
   string selected = Request.QueryString["screen"] ?? "general";
   foreach (var module in Navigation.Modules)
   {
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
