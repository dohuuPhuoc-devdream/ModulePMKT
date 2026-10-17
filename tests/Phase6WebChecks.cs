using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using System.Threading;
using System.Linq;
using System.Data;
using ReportingModule.Web;

[Serializable]
public class DebtResult { public string Body,Cookie,Location; public int Status; }
public class DebtRequest : SimpleWorkerRequest
{
 readonly string post,cookie; readonly ManualResetEvent done=new ManualResetEvent(false);
 public int Status=200; public string Cookie="",Location="";
 private readonly MemoryStream responseBytes=new MemoryStream();
 public string Body { get { return Encoding.UTF8.GetString(responseBytes.ToArray()); } }
 public override void SendResponseFromMemory(byte[] data,int length) { responseBytes.Write(data,0,length); }
 public DebtRequest(string query,string post,string cookie,TextWriter writer):base("Commerce.aspx",query,writer) { this.post=post; this.cookie=cookie; }
 public override string GetHttpVerbName() { return post==null?"GET":"POST"; }
 public override string GetKnownRequestHeader(int index)
 {
  if(index==HeaderCookie) return cookie;
  if(index==HeaderContentType && post!=null) return "application/x-www-form-urlencoded";
  if(index==HeaderContentLength && post!=null) return Encoding.UTF8.GetByteCount(post).ToString();
  return base.GetKnownRequestHeader(index);
 }
 public override byte[] GetPreloadedEntityBody() { return post==null?null:Encoding.UTF8.GetBytes(post); }
 public override bool IsEntireEntityBodyIsPreloaded() { return true; }
 public override int GetTotalEntityBodyLength() { return post==null?0:Encoding.UTF8.GetByteCount(post); }
 public override void SendStatus(int code,string description) { Status=code; }
 public override void SendKnownResponseHeader(int index,string value) { if(index==HeaderSetCookie) Cookie=value.Split(';')[0]; if(index==HeaderLocation) Location=value; }
 public override void EndOfRequest() { done.Set(); base.EndOfRequest(); }
 public void Wait() { if(!done.WaitOne(30000)) throw new Exception("Request timed out"); }
}
public class DebtRunner : MarshalByRefObject
{
 public DebtResult Run(string query,string post,string cookie)
 {
  var writer=new StringWriter(); var request=new DebtRequest(query,post,PhaseTestAuth.Cookie(cookie),writer);
  HttpRuntime.ProcessRequest(request); request.Wait();
  return new DebtResult { Body=request.Body,Cookie=request.Cookie,Status=request.Status,Location=request.Location };
 }
 public override object InitializeLifetimeService() { return null; }
}
class Phase6WebChecks
{
 static string unit; static DebtRunner runner; static CommerceStore store; static string cookie,query;
 static DateTime first=new DateTime(2026,1,1),last=new DateTime(2026,12,31);
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static DebtResult Request(string post=null)
 {
  var r=runner.Run(query,post,cookie); if(!string.IsNullOrEmpty(r.Cookie)) cookie=r.Cookie;
  File.WriteAllText("artifacts/phase6/web-last-response.html",r.Body);
  if(r.Status>=500) throw new Exception("ASP.NET error; see artifacts/phase6/web-last-response.html");
  return r;
 }
 static Dictionary<string,string> Hidden(string html)
 {
  var values=new Dictionary<string,string>();
  foreach(Match m in Regex.Matches(html,"<input[^>]+>",RegexOptions.IgnoreCase))
  {
   var name=Regex.Match(m.Value,"name=\"([^\"]*)\""); var value=Regex.Match(m.Value,"value=\"([^\"]*)\"");
   if(name.Success && m.Value.Contains("type=\"hidden\"")) values[HttpUtility.HtmlDecode(name.Groups[1].Value)]=HttpUtility.HtmlDecode(value.Groups[1].Value);
  }
  return values;
 }
 static DebtResult Post(DebtResult page,string button,Dictionary<string,string> controls=null,Dictionary<string,string> raw=null)
 {
  var fields=Hidden(page.Body); fields["ctl00$WorkspaceContent$"+button]="1";
  if(controls!=null) foreach(var kv in controls) fields["ctl00$WorkspaceContent$"+kv.Key]=kv.Value;
  if(raw!=null) foreach(var kv in raw) fields[kv.Key]=kv.Value;
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 static void Screen(string screen) { query="screen="+screen+"&unit="+unit+"&from=2026-01-01&to=2026-12-31"; }
 static Dictionary<string,string> Criteria(string item="HANG002")
 { return new Dictionary<string,string>{{"Unit",""+unit+""},{"From","2026-01-01"},{"To","2026-12-31"},{"Filter",item}}; }
 static Dictionary<string,string> Line(string qty)
 { return new Dictionary<string,string>{{"lineCount","1"},{"item0","HANG002"},{"qty0",qty},{"price0","100"}}; }
 static DebtResult Select(DebtResult page)
 {
  var fields=Hidden(page.Body); fields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records"; fields["__EVENTARGUMENT"]="Select$0";
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 static DebtResult Save(DebtResult page,string button,string number,string partner,string qty,string receiptId=null)
 { var controls=new Dictionary<string,string>{{"Number",number},{"DocumentDate","2026-01-05"},{"Note","Phase 6 isolated web test"},{"Partner",partner}}; if(receiptId!=null)controls["PurchaseReceipt"]=receiptId; return Post(page,button,controls,Line(qty)); }
 static void Main(string[] args)
 {
  Directory.CreateDirectory("artifacts/phase6");
  string cs="Data Source=.\\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10";
  store=new CommerceStore(cs); unit="W6"+Guid.NewGuid().ToString("N").Substring(0,10);
  new CatalogStore(cs).Create(CatalogDefinition.Get("units"),new[]{unit,"Phase 6 UI test","2026-01-01"});
  try {
   runner=(DebtRunner)ApplicationHost.CreateApplicationHost(typeof(DebtRunner),"/",Path.GetFullPath(args[0]));
   foreach(string screen in new[]{"sales-order","purchase-order"}) {
    Screen(screen); var p=Post(Request(),"New"); p=Save(p,"SaveDraft","ORDER",screen=="sales-order"?"KHDEMO001":"NCCDEMO001","1");
    Check(p.Status==302,screen+" draft saved");
    p=Post(Select(Request()),"Edit"); p=Save(p,"SaveDraft","ORDER",screen=="sales-order"?"KHDEMO001":"NCCDEMO001","2");
    Check(p.Status==302,screen+" draft edited");
    p=Post(Select(Request()),"Delete"); Check(p.Status==302,screen+" draft deleted");
   }
   long receipt=new StockStore(cs).Save(null,null,unit,"N","WEB-PR",first,"test receipt",new[]{new StockLine("HANG002",10,100)},true,"NCCDEMO001",true);
   Check(Navigation.PageFor("purchase-receipt")=="Stock.aspx","purchase receipt opens its own inventory workflow");
   Screen("ap-invoice"); var page=Post(Request(),"New"); page=Save(page,"Post","WEB-PI","NCCDEMO001","10",receipt.ToString());
   Check(page.Status==302,"AP invoice posts against a purchase receipt");
   Check((decimal)new StockStore(cs).Report(unit,first,last,"HANG002","T").Rows[0]["TonCuoi"]==10,"AP invoice does not add inventory a second time");
   page=Request(); Check(page.Body.Contains("WEB-PI"),"AP invoice appears in payables module");
   Screen("sales-invoice"); page=Post(Request(),"New"); page=Save(page,"Post","WEB-SI","KHDEMO001","3");
   Check(page.Status==302,"UI sales invoice posts");
   Screen("ar-invoice"); page=Request(); Check(page.Body.Contains("WEB-SI"),"sales aliases share invoice");
   page=Post(Select(page),"View"); Check(page.Body.Contains("readonly")&&page.Body.Contains("WEB-SI"),"posted invoice read-only view");
   Screen("sales-invoice"); page=Post(Request(),"New"); page=Save(page,"Post","FAIL","KHDEMO001","200");
   Check(page.Status==200&&HttpUtility.HtmlDecode(page.Body).Contains("Không đủ tồn kho"),"UI oversell error displayed");
   Check(store.List(unit,"SALES_INVOICE",first,last,"FAIL").Rows.Count==0,"UI failed invoice not persisted");
   foreach(string screen in new[]{"sales-detail","revenue","sales-analysis","purchase-detail","purchase-analysis"}) {
    Screen(screen); page=Request(); Check(!page.Body.Contains("value=\"Mới\""),screen+" report toolbar");
    page=Post(page,"Apply",Criteria(screen.StartsWith("purchase")?"NCCDEMO001":"KHDEMO001"));
    string amount=screen.StartsWith("purchase")?"1.000,0000":"300,0000";
    Check(page.Status==200&&HttpUtility.HtmlDecode(page.Body).Contains(amount),screen+" SQL total rendered");
    page=Post(page,"Refresh"); Check(HttpUtility.HtmlDecode(page.Body).Contains(amount),screen+" refresh retains active criteria");
    File.WriteAllText("artifacts/phase6/"+screen+".html",page.Body);
   }
   foreach(string screen in new[]{"ar-invoice","ap-invoice"}) {
    Screen(screen); page=Post(Select(Request()),"View");
    Check(page.Body.Contains("RecordPayment"),screen+" payment section on posted view");
    page=Post(page,"RecordPayment",new Dictionary<string,string>{{"PaymentNumber","PAY1"},{"PaymentDate","2026-01-06"},{"PaymentAmount","100.1234"},{"PaymentNote","UI partial payment"}});
    Check(page.Status==302,screen+" payment postback saved");
    page=Post(Select(Request()),"View"); Check(page.Body.Contains("PAY1"),screen+" payment history loads");
    page=Post(page,"RecordPayment",new Dictionary<string,string>{{"PaymentNumber","OVER"},{"PaymentDate","2026-01-07"},{"PaymentAmount","2000"},{"PaymentNote",""}});
    Check(page.Status==200&&HttpUtility.HtmlDecode(page.Body).Contains("vượt phần còn"),screen+" overpayment error");
   }
   foreach(string screen in new[]{"receivables","payables"}) {
    Screen(screen); page=Request(); Check(!page.Body.Contains("value=\"Mới\""),screen+" report-only toolbar");
    page=Post(page,"Apply",Criteria(screen=="receivables"?"KHDEMO001":"NCCDEMO001"));
    string balance=screen=="receivables"?"199,8766":"899,8766";
    Check(page.Status==200&&HttpUtility.HtmlDecode(page.Body).Contains(balance),screen+" closing balance rendered");
    page=Post(page,"Refresh",new Dictionary<string,string>{{"Filter","UNKNOWN"}});
    Check(HttpUtility.HtmlDecode(page.Body).Contains(balance),screen+" refresh uses applied filters");
    page=Post(page,"Apply",new Dictionary<string,string>{{"Unit",unit},{"From","2026-01-07"},{"To","2026-12-31"},{"Filter",screen=="receivables"?"KHDEMO001":"NCCDEMO001"}});
    Check(HttpUtility.HtmlDecode(page.Body).Contains(balance),screen+" carried opening displayed");
    File.WriteAllText("artifacts/phase6/"+screen+".html",page.Body);
   }
   query="screen=unknown"; Check(Request().Status==404,"unknown route rejected");
  } finally {
   using(var db=new System.Data.SqlClient.SqlConnection(cs)) { db.Open(); using(var cmd=db.CreateCommand()) {
    cmd.CommandText="SET QUOTED_IDENTIFIER ON; SET ARITHABORT ON; DELETE p FROM dbo.ThanhToanHoaDon p JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=p.ChungTuId WHERE h.MaDonVi=@u; UPDATE dbo.ChungTuThuongMai SET ChungTuNhapMuaId=NULL WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuKho d JOIN dbo.ChungTuKho h ON h.ChungTuKhoId=d.ChungTuKhoId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuKho WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuThuongMai d JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=d.ChungTuId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuThuongMai WHERE MaDonVi=@u; DELETE dbo.DonVi WHERE MaDonVi=@u;";
    cmd.Parameters.AddWithValue("@u",unit); cmd.ExecuteNonQuery();
   } }
  }
  Console.WriteLine("PHASE 6 WEB FORMS CHECKS PASSED; fixtures removed.");
 }
}

