using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using System.Threading;
using System.Linq;
using ReportingModule.Web;

[Serializable]
public class Result { public string Body; public string Cookie; public int Status; public string Location; }
public class Request : SimpleWorkerRequest
{
 readonly string post,cookie; readonly ManualResetEvent done=new ManualResetEvent(false);
 public int Status=200; public string Cookie="",Location="";
 private readonly MemoryStream responseBytes=new MemoryStream();
 public string Body { get { return Encoding.UTF8.GetString(responseBytes.ToArray()); } }
 public override void SendResponseFromMemory(byte[] data,int length) { responseBytes.Write(data,0,length); }
 public Request(string query,string post,string cookie,TextWriter writer):base("Catalog.aspx",query,writer) { this.post=post; this.cookie=cookie; }
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
 public override void SendKnownResponseHeader(int index,string value) {
  if(index==HeaderSetCookie) Cookie=value.Split(';')[0];
  if(index==HeaderLocation) Location=value;
 }
 public override void EndOfRequest() { done.Set(); base.EndOfRequest(); }
 public void Wait() { if(!done.WaitOne(30000)) throw new Exception("ASP.NET request timed out"); }
}
public class Runner : MarshalByRefObject
{
 public Result Run(string query,string post,string cookie)
 {
  var writer=new StringWriter(); var request=new Request(query,post,PhaseTestAuth.Cookie(cookie),writer);
  HttpRuntime.ProcessRequest(request); request.Wait();
  return new Result { Body=request.Body,Cookie=request.Cookie,Status=request.Status,Location=request.Location };
 }
 public override object InitializeLifetimeService() { return null; }
}
public class WebChecks
{
 static Runner runner; static string cookie; static string query="screen=items";
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static Result Request(string post=null)
 {
  var result=runner.Run(query,post,cookie); if(!string.IsNullOrEmpty(result.Cookie)) cookie=result.Cookie;
  File.WriteAllText("artifacts/phase3/web-last-response.html",result.Body);
  if(result.Status>=500) throw new Exception("ASP.NET error; inspect artifacts/phase3/web-last-response.html");
  return result;
 }
 static Dictionary<string,string> Fields(string body)
 {
  var result=new Dictionary<string,string>();
  foreach(Match m in Regex.Matches(body,"<input[^>]+>",RegexOptions.IgnoreCase))
  {
   string tag=m.Value;
   var name=Regex.Match(tag,"name=\"([^\"]*)\"");
   var value=Regex.Match(tag,"value=\"([^\"]*)\"");
   if(name.Success && tag.Contains("type=\"hidden\"")) result[HttpUtility.HtmlDecode(name.Groups[1].Value)]=HttpUtility.HtmlDecode(value.Groups[1].Value);
  }
  return result;
 }
 static Result Post(Result page,string button,Dictionary<string,string> extra=null)
 {
  var fields=Fields(page.Body); fields["ctl00$WorkspaceContent$"+button]="1";
  if(extra!=null) foreach(var kv in extra) fields["ctl00$WorkspaceContent$"+kv.Key]=kv.Value;
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 public static void Main(string[] args)
 {
  runner=(Runner)ApplicationHost.CreateApplicationHost(typeof(Runner),"/",Path.GetFullPath(args[0]));
  Directory.CreateDirectory("artifacts/phase3");
  var page=Request();
  Check(page.Status==200 && page.Body.Contains("HANG001"),"live ASP.NET catalog render reads SQL");
  Check(Regex.Matches(page.Body,"class=\"module").Count==8,"shared seven-module shell retained");
  Check(page.Body.Contains("__VIEWSTATE"),"Web Forms viewstate generated");
  page=Post(page,"ApplyFilter",new Dictionary<string,string>{{"Filter","HANG001"}});
  Check(page.Body.Contains("HANG001")&&!page.Body.Contains(">HANG002<"),"filter postback limits rows");
  page=Post(page,"Refresh");
  Check(page.Body.Contains("HANG001")&&!page.Body.Contains(">HANG002<"),"refresh preserves effective filter");
  page=Post(page,"New");
  Check(page.Body.Contains("Value0"),"new editor opens on postback");
  page=Post(page,"Save",new Dictionary<string,string>{{"Value0","BAD CODE"},{"Value1","Name"},{"Value2","Cai"},{"Value3","1"},{"Value4","2"}});
  Check(page.Body.Contains("BAD CODE") && page.Status==200,"invalid editor remains available without redirect");
  page=Post(page,"Cancel");
  Check(!page.Body.Contains("name=\"ctl00$WorkspaceContent$Value0\""),"cancel closes editor");
  page=Post(page,"Import");
  Check(page.Body.Contains("Workbook"),"Excel import panel opens");
  string testKey="WEB"+Guid.NewGuid().ToString("N").Substring(0,10);
  var definition=CatalogDefinition.Get("items");
  var store=new CatalogStore("Data Source=.\\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10");
  try {
   page=Post(page,"New");
   page=Post(page,"Save",new Dictionary<string,string>{{"Value0",testKey},{"Value1","Web form create"},{"Value2","Cai"},{"Value3","12.3456"},{"Value4","20"}});
   Check(page.Status==302,"valid save redirects after write");
   Check((string)store.Find(definition,testKey)[1]=="Web form create","postback creates database row");
   query="screen=items&q="+testKey; page=Request();
   var selectedFields=Fields(page.Body);
   selectedFields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records";
   selectedFields["__EVENTARGUMENT"]="Select$0";
   page=Request(string.Join("&",selectedFields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
   page=Post(page,"Edit");
   Check(page.Body.Contains("Web form create"),"selected row loads edit form");
   page=Post(page,"Save",new Dictionary<string,string>{{"Value0",testKey},{"Value1","Web form edited"},{"Value2","Cai"},{"Value3","13.1234"},{"Value4","21"}});
   Check(page.Status==302 && (string)store.Find(definition,testKey)[1]=="Web form edited","postback updates database row");
   page=Request(); selectedFields=Fields(page.Body);
   selectedFields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records";
   selectedFields["__EVENTARGUMENT"]="Select$0";
   page=Request(string.Join("&",selectedFields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
   page=Post(page,"Delete");
   Check(page.Status==302 && store.List(definition,testKey).Rows.Count==0,"postback deletes unreferenced row");
  } finally {
   var leftover=store.List(definition,testKey);
   if(leftover.Rows.Count==1) store.Delete(definition,leftover.Rows[0].ItemArray);
  }
  foreach(string id in new[]{"customers","suppliers","units"}) { query="screen="+id; page=Request(); Check(page.Status==200&&page.Body.Contains("Records"),id+" ASP.NET render"); }
  query="screen=not-a-catalog"; page=Request(); Check(page.Status==404,"unknown catalog returns 404");
  Console.WriteLine("WEB FORMS HOSTED REQUEST SMOKE PASSED (not a visual browser test)");
 }
}
