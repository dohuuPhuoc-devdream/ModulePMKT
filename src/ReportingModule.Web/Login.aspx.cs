using System;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.SessionState;

namespace ReportingModule.Web
{
 public partial class LoginPage : LoginPageBase
 {
  protected HiddenField Csrf;
  protected Label Message;
  protected TextBox Username,Password;
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e);Response.Cache.SetCacheability(HttpCacheability.NoCache);Response.Cache.SetNoStore();
   if(!IsPostBack)
   {
    Csrf.Value=Convert.ToString(Session["CsrfToken"]);
    string requested=Request.QueryString["returnUrl"];ViewState["ReturnUrl"]=AppPage.IsLocalUrl(requested)?requested:"Default.aspx";
    if(Request.QueryString["expired"]=="1")Message.Text="Phiên làm việc đã hết hạn. Đăng nhập lại để tiếp tục.";
   }
  }
  protected void SignIn_Click(object sender,EventArgs e)
  {
   Message.Text="";AuthenticationResult result;
   try{result=IdentityStore.Authenticate(Username.Text,Password.Text);}catch(Exception ex){Log.Error("Authentication","Sign-in failed",ex);result=new AuthenticationResult{Error="Chưa thể đăng nhập. Hãy thử lại sau."};}
   Password.Text="";
   if(result.User==null){Message.Text=HttpUtility.HtmlEncode(result.Error??"Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.");return;}
   Session.Clear();Session.Abandon();
   var manager=new SessionIDManager();bool redirected,cookieAdded;string newId=manager.CreateSessionID(Context);manager.SaveSessionID(Context,newId,out redirected,out cookieAdded);
   var ticket=new FormsAuthenticationTicket(2,result.User.Id.ToString("D"),DateTime.Now,DateTime.Now.AddMinutes(20),false,result.User.Username,FormsAuthentication.FormsCookiePath);
   var cookie=new HttpCookie(FormsAuthentication.FormsCookieName,FormsAuthentication.Encrypt(ticket)){HttpOnly=true,Secure=Request.IsSecureConnection,Path=FormsAuthentication.FormsCookiePath,SameSite=SameSiteMode.Lax};
   Response.Cookies.Add(cookie);
   string destination=ViewState["ReturnUrl"] as string;
   if(!AppPage.IsLocalUrl(destination))destination="Default.aspx";
   Response.Redirect(destination,false);Context.ApplicationInstance.CompleteRequest();
  }
 }
}
