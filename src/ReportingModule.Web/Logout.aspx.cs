using System;
using System.Web.Security;
using System.Web.UI;
namespace ReportingModule.Web
{
 public class LogoutPage:Page
 {
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e);var id=CurrentIdentity.CurrentUserId();
   if(id.HasValue)try{IdentityStore.Audit("SYSTEM","AUTHENTICATION","LOGOUT","SUCCESS","Đăng xuất.");}catch(Exception ex){Log.Error("Audit","Logout audit failed",ex);}
   FormsAuthentication.SignOut();Session.Clear();Session.Abandon();Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);Response.Cache.SetNoStore();
   Response.Redirect("Login.aspx",false);Context.ApplicationInstance.CompleteRequest();
  }
 }
}
