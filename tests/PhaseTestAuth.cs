using System;
using System.Web.Security;

public static class PhaseTestAuth
{
 private static readonly string UserId="10000000-0000-4000-8000-000000000010";
 public static string Cookie(string sessionCookie)
 {
  var ticket=new FormsAuthenticationTicket(2,UserId,DateTime.Now,DateTime.Now.AddMinutes(20),false,"automated acceptance",FormsAuthentication.FormsCookiePath);
  string auth=FormsAuthentication.FormsCookieName+"="+FormsAuthentication.Encrypt(ticket);
  return string.IsNullOrEmpty(sessionCookie)?auth:auth+"; "+sessionCookie;
 }
}
