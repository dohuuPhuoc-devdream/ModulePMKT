using System;
using System.Configuration;
using System.Web;
using System.Web.Security;

namespace ReportingModule.Web
{
 public class WebApplication:HttpApplication
 {
  protected void Application_BeginRequest(object sender,EventArgs e)
  {
   if(string.Equals(ConfigurationManager.AppSettings["RequireHttps"],"true",StringComparison.OrdinalIgnoreCase)&&!Request.IsLocal&&!Request.IsSecureConnection)
   {Response.StatusCode=403;Response.TrySkipIisCustomErrors=true;Response.ContentType="text/plain";Response.Write("HTTPS is required.");CompleteRequest();}
  }
  protected void Application_EndRequest(object sender,EventArgs e)
  {
   foreach(string name in Response.Cookies)
   {
    var cookie=Response.Cookies[name];if(cookie==null)continue;
    if(name==FormsAuthentication.FormsCookieName||name=="ReportingModule.Session"){cookie.HttpOnly=true;cookie.Secure=Request.IsSecureConnection;cookie.SameSite=SameSiteMode.Lax;}
   }
   if(Request.IsSecureConnection&&!Request.IsLocal)Response.Headers["Strict-Transport-Security"]="max-age=31536000";
  }
  protected void Application_Error(object sender,EventArgs e)
  {
   var ex=Server.GetLastError();if(ex!=null)Log.Error("Application","Unhandled request error",ex);
  }
 }
}
