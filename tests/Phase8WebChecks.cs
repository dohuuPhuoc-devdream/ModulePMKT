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
public class OutputResult { public string Body,Cookie,Location; public byte[] Bytes; public int Status; }
public class OutputRequest : SimpleWorkerRequest
{
 readonly string post,cookie; readonly ManualResetEvent done=new ManualResetEvent(false);
 public int Status=200; public string Cookie="",Location="";
 private readonly MemoryStream responseBytes=new MemoryStream();
 public byte[] Bytes { get { return responseBytes.ToArray(); } }
 public string Body { get { return Encoding.UTF8.GetString(responseBytes.ToArray()); } }
 public override void SendResponseFromMemory(byte[] data,int length) { responseBytes.Write(data,0,length); }
 public OutputRequest(string page,string query,string post,string cookie,TextWriter writer):base(page,query,writer) { this.post=post; this.cookie=cookie; }
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
public class OutputRunner : MarshalByRefObject
{
 public OutputResult Run(string page,string query,string post,string cookie)
 {
  var writer=new StringWriter(); var request=new OutputRequest(page,query,post,PhaseTestAuth.Cookie(cookie),writer);
  HttpRuntime.ProcessRequest(request); request.Wait();
  return new OutputResult { Bytes=request.Bytes,Body=request.Body,Cookie=request.Cookie,Status=request.Status,Location=request.Location };
 }
 public override object InitializeLifetimeService() { return null; }
}
class Phase8WebChecks
{
 static string unit; static OutputRunner runner; static string cookie,query,pageName;
 static DateTime first=new DateTime(2026,1,1),last=new DateTime(2026,12,31);
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static OutputResult Request(string post=null)
 {
  var r=runner.Run(pageName,query,post,cookie); if(!string.IsNullOrEmpty(r.Cookie)) cookie=r.Cookie;
  File.WriteAllText("artifacts/phase8/web-last-response.html",r.Body);
  if(r.Status>=500) throw new Exception("ASP.NET error; see artifacts/phase8/web-last-response.html");
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
 static OutputResult Post(OutputResult page,string button,Dictionary<string,string> controls=null,Dictionary<string,string> raw=null)
 {
  var fields=Hidden(page.Body); fields["ctl00$WorkspaceContent$"+button]="1";
  if(controls!=null) foreach(var kv in controls) fields["ctl00$WorkspaceContent$"+kv.Key]=kv.Value;
  if(raw!=null) foreach(var kv in raw) fields[kv.Key]=kv.Value;
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 static OutputResult Follow(OutputResult redirect) {
  Check(redirect.Status==302,"output redirect"); var parts=redirect.Location.Split('?'); pageName="Output.aspx"; query=parts[1]; return Request();
 }
 static void Main(string[] args)
 {
  Directory.CreateDirectory("artifacts/phase8");
  runner=(OutputRunner)ApplicationHost.CreateApplicationHost(typeof(OutputRunner),"/",Path.GetFullPath(args[0]));
  string cs="Data Source=.\\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10";
  unit="W8"+Guid.NewGuid().ToString("N").Substring(0,10);
  new CatalogStore(cs).Create(CatalogDefinition.Get("units"),new[]{unit,"Phase 8 isolated output test","2026-01-01"});
  try {
   var commerce=new CommerceStore(cs);
   long receipt=new StockStore(cs).Save(null,null,unit,"N","P8-RECEIPT",first,"",new[]{new StockLine("HANG002",10,100)},true,"NCCDEMO001",true);
   long purchase=commerce.Save(null,null,unit,"PURCHASE_INVOICE","P8",first,"",new[]{new StockLine("HANG002",10,100)},true,"NCCDEMO001",receipt);
   long sale=commerce.Save(null,null,unit,"SALES_INVOICE","S8",first,"",new[]{new StockLine("HANG002",3,100)},true,"KHDEMO001");
   commerce.RecordPayment(purchase,unit,"PURCHASE_INVOICE","PAY",first,100,"",Guid.NewGuid());
   commerce.RecordPayment(sale,unit,"SALES_INVOICE","REC",first,100,"",Guid.NewGuid());
   new JournalStore(cs).Save(null,null,unit,"J8",first,"",new[]{new JournalLine("111",100,0),new JournalLine("4111",0,100)},true);
  foreach(string screen in new[]{"inbound","outbound","inventory","sales-detail","revenue","sales-analysis","purchase-detail","purchase-analysis","receivables","payables","balance"}) {
   pageName=Navigation.PageFor(screen); query="screen="+screen;
   var p=Request(); p=Post(p,"ExcelOutput"); Check(p.Status==200,"criteria required before output: "+screen);
   var fields=new Dictionary<string,string>{{"Unit",new[]{"inbound","outbound","inventory"}.Contains(screen)?"DVDEMO":unit},{"From","2026-01-01"},{"To","2026-12-31"},{"Filter",screen=="inventory"?"HANG001":""}};
   p=Post(p,"Apply",fields); string sourcePage=pageName,sourceQuery=query;
   foreach(var output in new[]{new[]{"Output","html"},new[]{"ExcelOutput","xlsx"},new[]{"PdfOutput","pdf"}}) {
    pageName=sourcePage;query=sourceQuery;
    var r=Follow(Post(p,output[0],new Dictionary<string,string>{{"Filter","UNKNOWN"},{"To","2026-01-01"}}));
    Check(r.Status==200,screen+" "+output[1]+" response");
    File.WriteAllBytes("artifacts/phase8/"+screen+"."+output[1],r.Bytes);
    if(output[1]=="html") { Check(r.Body.Contains("2026-12-31")&&!r.Body.Contains("UNKNOWN"),"effective filters in print"); Check(r.Body.Contains("main-navigation"),"persistent navigation in print preview"); if(screen=="inventory") Check(HttpUtility.HtmlDecode(r.Body).Contains("130,000"),"printed acceptance 130"); }
    if(output[1]=="pdf") Check(Encoding.ASCII.GetString(r.Bytes,0,5)=="%PDF-","real PDF signature");
    if(output[1]=="xlsx") Check(r.Bytes[0]==80&&r.Bytes[1]==75,"real XLSX zip");
   }
  }
  foreach(string screen in new[]{"stock-receipt","sales-invoice","journal"}) {
   pageName=Navigation.PageFor(screen);query="screen="+screen+"&unit="+unit+"&from=2026-01-01&to=2026-12-31";
   var p=Request(); if(screen=="stock-receipt") Check(p.Body.Contains("P8-RECEIPT"),"purchase receipt row rendered before print selection"); var fields=Hidden(p.Body);fields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records";fields["__EVENTARGUMENT"]="Select$0";
   p=Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
   var r=Follow(Post(p,"Output"));Check(r.Status==200&&r.Body.Contains(screen=="stock-receipt"?"P8-RECEIPT":screen=="journal"?"J8":"S8"),"selected document print: "+screen);
  }
  string savedOutputQuery=query;
  pageName=Navigation.PageFor("purchase-receipt"); query="screen=purchase-receipt&unit="+unit+"&from=2026-01-01&to=2026-12-31";
  var purchasePage=Post(Request(),"New");
  purchasePage=Post(purchasePage,"Post",new Dictionary<string,string>{{"Number","P8-UI-RECEIPT"},{"DocumentDate","2026-01-02"},{"Note","UI receipt test"},{"Supplier","NCCDEMO001"}},new Dictionary<string,string>{{"lineCount","1"},{"item0","HANG002"},{"qty0","1"},{"price0","20"}});
  Check(purchasePage.Status==302,"purchase receipt UI posts stock movement");
  Check(new StockStore(cs).List(unit,"N",first,last,"P8-UI-RECEIPT").Rows.Count==1,"purchase receipt UI record appears in its inventory list");
  // A token belongs to the originating session; an unrelated session cannot read it.
  string savedCookie=cookie; pageName="Output.aspx"; query=savedOutputQuery; cookie=null; var forbidden=Request(); Check(forbidden.Status==410,"foreign session cannot access snapshot"); cookie=savedCookie;
  pageName="Output.aspx";query="token=invalid";Check(Request().Status==404,"invalid snapshot token rejected");
  } finally {
   using(var db=new System.Data.SqlClient.SqlConnection(cs)) {db.Open();using(var cmd=db.CreateCommand()) {
    cmd.CommandText="SET QUOTED_IDENTIFIER ON; SET ARITHABORT ON; DELETE p FROM dbo.ThanhToanHoaDon p JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=p.ChungTuId WHERE h.MaDonVi=@u; UPDATE dbo.ChungTuThuongMai SET ChungTuNhapMuaId=NULL WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuKho d JOIN dbo.ChungTuKho h ON h.ChungTuKhoId=d.ChungTuKhoId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuKho WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuThuongMai d JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=d.ChungTuId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuThuongMai WHERE MaDonVi=@u; DELETE d FROM dbo.CTPhieuKeToan d JOIN dbo.PhieuKeToan h ON h.PhieuId=d.PhieuId WHERE h.MaDonVi=@u; DELETE dbo.PhieuKeToan WHERE MaDonVi=@u; DELETE dbo.Kho WHERE MaDonVi=@u; DELETE dbo.DonVi WHERE MaDonVi=@u;";
    cmd.Parameters.AddWithValue("@u",unit);cmd.ExecuteNonQuery();
   }}
  }
  Console.WriteLine("PHASE 8 WEB OUTPUT CHECKS PASSED; fixtures removed.");
 }
}

