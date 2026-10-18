using System;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
namespace ReportingModule.Web
{
 public class OutputPage:AppPage
 {
  protected Literal Report; protected HyperLink Excel,Pdf,Back; private bool downloading;
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e); Response.Cache.SetCacheability(HttpCacheability.NoCache); Response.Cache.SetNoStore();
   try { LoadOutput(); }
   catch(HttpException ex) { if(ex.GetHttpCode()==403||ex.GetHttpCode()==404||ex.GetHttpCode()==410)try{IdentityStore.Audit("SECURITY","OUTPUT","ACCESS","DENIED","Bản kết xuất bị từ chối hoặc không còn hợp lệ.");}catch(Exception auditError){Log.Error("Audit","Output denial audit failed",auditError);}ShowError(ex.GetHttpCode(),ex.Message); }
   catch(ArgumentException ex) { ShowError(400,ex.Message); }
   catch(Exception ex) { Log.Error("Output","Output generation failed",ex); ShowError(503,"Chưa thể tạo bản kết xuất. Hãy trở lại báo cáo, làm tươi và thử lại."); }
  }
  private void LoadOutput()
  {
   string id=Request.QueryString["token"],format=Request.QueryString["format"]??"print";
   var s=ReportOutput.Get(new HttpSessionStateWrapper(Session),id);
   if(format!="print"&&format!="xlsx"&&format!="pdf") throw new HttpException(404,"Định dạng không tồn tại.");
   UnitAccess.Demand(s.UnitCode);
   // A PDF is a print rendering of the designed print view. Excel remains a
   // data-export action with its own permission.
   RequirePermission(ReportOutput.PermissionFor(s.Screen,format));
   Title=s.Title+" | Kết xuất";
   if(format=="print") {
    AuditOutput(s.Screen,format,s.UnitCode);
    Report.Text=ReportOutput.Html(s); Excel.NavigateUrl="Output.aspx?token="+id+"&format=xlsx"; Pdf.NavigateUrl="Output.aspx?token="+id+"&format=pdf";
    Excel.Visible=PermissionService.IsAllowed(UserId,ReportOutput.PermissionFor(s.Screen,"xlsx"));
    Pdf.Visible=PermissionService.IsAllowed(UserId,ReportOutput.PermissionFor(s.Screen,"pdf"));
    Back.NavigateUrl=s.SourceUrl??(Navigation.PageFor(s.Screen)+"?screen="+HttpUtility.UrlEncode(s.Screen)); return;
   }
   byte[] bytes=format=="xlsx"?ReportExcel.Export(s):ReportPdf.Export(s);
   AuditOutput(s.Screen,format,s.UnitCode);
   Response.Clear(); Response.ContentType=format=="xlsx"?"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":"application/pdf";
   Response.AddHeader("Content-Disposition","attachment; filename="+s.Screen+"."+format); Response.AddHeader("X-Content-Type-Options","nosniff");
   Response.BinaryWrite(bytes); downloading=true; Context.ApplicationInstance.CompleteRequest();
  }
  private void AuditOutput(string screen,string format,string unitCode)
  {
   try { IdentityStore.Audit(PermissionMap.Module(screen),PermissionMap.Function(screen),format=="xlsx"?"EXPORT":"PRINT","SUCCESS",format=="xlsx"?"Đã tải tệp Excel.":"Đã tạo bản in/PDF.",unitCode); }
   catch(Exception error) { Log.Error("Output","Output was generated but audit recording failed",error); }
  }
  private void ShowError(int status,string text)
  {
   Response.Clear(); Response.StatusCode=status;Response.TrySkipIisCustomErrors=true;Response.ContentType="text/html";
   Excel.Visible=Pdf.Visible=false;Back.NavigateUrl="Default.aspx";
   Report.Text="<section class=\"catalog-editor\"><h1>Không thể kết xuất</h1><p role=\"alert\">"+HttpUtility.HtmlEncode(text)+"</p><p>Chọn báo cáo từ menu, áp dụng điều kiện hoặc Làm tươi để tạo bản kết xuất mới.</p></section>";
  }
  protected override void Render(HtmlTextWriter writer) { if(!downloading) base.Render(writer); }
 }
}


