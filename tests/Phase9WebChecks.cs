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
public class QualityResult { public string Body,Cookie,Location; public byte[] Bytes; public int Status; }
public class QualityRequest : SimpleWorkerRequest
{
 readonly string post,cookie; readonly ManualResetEvent done=new ManualResetEvent(false);
 public int Status=200; public string Cookie="",Location="";
 private readonly MemoryStream responseBytes=new MemoryStream();
 public byte[] Bytes { get { return responseBytes.ToArray(); } }
 public string Body { get { return Encoding.UTF8.GetString(responseBytes.ToArray()); } }
 public override void SendResponseFromMemory(byte[] data,int length) { responseBytes.Write(data,0,length); }
 public QualityRequest(string page,string query,string post,string cookie,TextWriter writer):base(page,query,writer) { this.post=post; this.cookie=cookie; }
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
public class QualityRunner : MarshalByRefObject
{
 public QualityResult Run(string page,string query,string post,string cookie)
 {
  var writer=new StringWriter(); var request=new QualityRequest(page,query,post,PhaseTestAuth.Cookie(cookie),writer);
  HttpRuntime.ProcessRequest(request); request.Wait();
  return new QualityResult { Bytes=request.Bytes,Body=request.Body,Cookie=request.Cookie,Status=request.Status,Location=request.Location };
 }
 public override object InitializeLifetimeService() { return null; }
}
class Phase9WebChecks
{
 static QualityRunner runner; static string cookie,query,pageName;
 static DateTime first=new DateTime(2026,1,1),last=new DateTime(2026,12,31);
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static QualityResult Request(string post=null)
 {
  var r=runner.Run(pageName,query,post,cookie); if(!string.IsNullOrEmpty(r.Cookie)) cookie=r.Cookie;
  File.WriteAllText("artifacts/phase9/web-last-response.html",r.Body);

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
 static QualityResult Post(QualityResult page,string button,Dictionary<string,string> controls=null,Dictionary<string,string> raw=null)
 {
  var fields=Hidden(page.Body); fields["ctl00$WorkspaceContent$"+button]="1";
  if(controls!=null) foreach(var kv in controls) fields["ctl00$WorkspaceContent$"+kv.Key]=kv.Value;
  if(raw!=null) foreach(var kv in raw) fields[kv.Key]=kv.Value;
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 static IEnumerable<MenuNode> Leaves(MenuNode n) {if(n.Children.Length==0) yield return n;else foreach(var child in n.Children) foreach(var leaf in Leaves(child)) yield return leaf;}
 static void Capture(string name,QualityResult p) {
  string html=p.Body;
  html=Regex.Replace(html,@"(Default|Catalog|Stock|Commerce|Journal)\.aspx\?screen=([a-z-]+)","$2.html");
  File.WriteAllText("artifacts/phase9/preview/"+name+".html",html);
 }
 static void Main(string[] args)
 {
  Directory.CreateDirectory("artifacts/phase9/preview/Content");Directory.CreateDirectory("artifacts/phase9/preview/Scripts");
  File.Copy("src/ReportingModule.Web/Content/site.css","artifacts/phase9/preview/Content/site.css",true);
  File.Copy("src/ReportingModule.Web/Scripts/site.js","artifacts/phase9/preview/Scripts/site.js",true);
  runner=(QualityRunner)ApplicationHost.CreateApplicationHost(typeof(QualityRunner),"/",Path.GetFullPath(args[0]));
  var leaves=Navigation.Modules.SelectMany(Leaves).ToArray();
  Check(Navigation.Modules.Length==7&&leaves.Length==28&&leaves.Select(x=>x.Id).Distinct().Count()==28,"seven modules, 25 approved business destinations and three administration destinations");
  foreach(var node in leaves) {
   pageName=Navigation.PageFor(node.Id);query="screen="+node.Id+"&unit=DVDEMO&from=2026-01-01&to=2026-12-31";
   var p=Request();Check(p.Status==200&&p.Body.Contains("main-navigation")&&HttpUtility.HtmlDecode(p.Body).Contains("Làm tươi"),"route, shell, refresh: "+node.Id);
   if(node.Kind=="report") {
    p=Post(p,"Apply",new Dictionary<string,string>{{"Unit","DVDEMO"},{"From","2026-01-01"},{"To","2026-12-31"},{"Filter",""}});
    Check(p.Status==200&&!HttpUtility.HtmlDecode(p.Body).Contains("Không thể xử lý"),"report query: "+node.Id);
   }
   Capture(node.Id,p);
  }
  pageName="Output.aspx";query="token=invalid";var error=Request();
  Check(error.Status==404&&HttpUtility.HtmlDecode(error.Body).Contains("Không thể kết xuất")&&!error.Body.Contains("Stack Trace"),"friendly output error without stack trace");Capture("output-error",error);
  pageName="Default.aspx";query="screen=unknown";Check(Request().Status==404,"unknown default route returns 404");
  string cs="Data Source=.\\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10";
  var catalog=new CatalogStore(cs);var definition=CatalogDefinition.Get("items");string prefix="Q9"+Guid.NewGuid().ToString("N").Substring(0,8);var keys=new List<string>();
  try {
   for(int i=0;i<25;i++) {string key=prefix+i.ToString("D2");catalog.Create(definition,new[]{key,i==0?"<script>alert(1)</script>":"Quality page item "+i,"Cai","1","2"});keys.Add(key);}
   pageName="Catalog.aspx";query="screen=items&q="+prefix;var p=Request();
   Check(p.Status==200&&p.Body.Contains(keys[0])&&!p.Body.Contains(keys[24]),"catalog first page bounded to 20 rows");
   Check(!p.Body.Contains("<script>alert(1)</script>")&&p.Body.Contains("&lt;script&gt;"),"stored markup encoded");
   var fields=Hidden(p.Body);fields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records";fields["__EVENTARGUMENT"]="Page$2";
   var next=Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
   Check(next.Status==200&&next.Body.Contains(keys[24]),"paging reaches remaining rows");
   fields=Hidden(p.Body);fields["__VIEWSTATE"]="tampered";fields["ctl00$WorkspaceContent$Refresh"]="1";
   Check(Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value)))).Status>=400,"tampered viewstate rejected");
   string originalCookie=cookie;cookie=null;Request();fields=Hidden(p.Body);fields["ctl00$WorkspaceContent$Refresh"]="1";
   Check(Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value)))).Status>=400,"cross-session postback rejected");cookie=originalCookie;
   fields=Hidden(p.Body);fields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records";fields["__EVENTARGUMENT"]="Select$999";
   Check(Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value)))).Status>=400,"forged grid event rejected");
  } finally {foreach(string key in keys) catalog.Delete(definition,catalog.Find(definition,key));}
  Console.WriteLine("PHASE 9 ROUTE, PAGING AND SECURITY CHECKS PASSED; fixtures removed.");
 }
}
