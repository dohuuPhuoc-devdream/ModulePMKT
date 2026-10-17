using System;
using System.Web;
using System.Web.UI;
namespace ReportingModule.Web
{
 public partial class FunctionPage:AppPage
 {
  protected bool Found {get;private set;}
  protected string ScreenTitle {get {return Found?"Thông tin chung":"Không tìm thấy chức năng";}}
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e); string module,id=Request.QueryString["screen"]??"general"; var node=Navigation.Find(id,out module);
   Found=node!=null;
   if(Found&&id!="general") {Response.Redirect(Navigation.PageFor(id)+"?screen="+HttpUtility.UrlEncode(id),false);Context.ApplicationInstance.CompleteRequest();}
   if(!Found) {Response.StatusCode=404;Response.TrySkipIisCustomErrors=true;}
   Title=ScreenTitle+" | Kế toán";
  }
 }
}
