using System;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
namespace ReportingModule.Web
{
 public partial class ChangePasswordPage:AppPage
 {
  protected Label Message;protected TextBox Current,NewPassword,Confirm;
  protected void Save_Click(object sender,EventArgs e)
  {
   try
   {
    if(NewPassword.Text!=Confirm.Text)throw new ArgumentException("Mật khẩu nhập lại chưa khớp.");
    if(!IdentityStore.ChangePassword(UserId,Current.Text,NewPassword.Text))throw new ArgumentException("Mật khẩu hiện tại không đúng.");
    Current.Text=NewPassword.Text=Confirm.Text="";Message.Text="Đã đổi mật khẩu.";
   }
   catch(ArgumentException ex){Message.Text=HttpUtility.HtmlEncode(ex.Message);}
   catch(Exception ex){Trace.Warn("Authentication","Password change failed",ex);Message.Text="Chưa thể đổi mật khẩu. Hãy thử lại.";}
  }
 }
}
