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
public class JournalResult { public string Body,Cookie,Location; public int Status; }
public class JournalRequest : SimpleWorkerRequest
{
 readonly string post,cookie; readonly ManualResetEvent done=new ManualResetEvent(false);
 public int Status=200; public string Cookie="",Location="";
 private readonly MemoryStream responseBytes=new MemoryStream();
 public string Body { get { return Encoding.UTF8.GetString(responseBytes.ToArray()); } }
 public override void SendResponseFromMemory(byte[] data,int length) { responseBytes.Write(data,0,length); }
 public JournalRequest(string query,string post,string cookie,TextWriter writer):base("Journal.aspx",query,writer) { this.post=post; this.cookie=cookie; }
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
public class JournalRunner : MarshalByRefObject
{
 public JournalResult Run(string query,string post,string cookie)
 {
  var writer=new StringWriter(); var request=new JournalRequest(query,post,PhaseTestAuth.Cookie(cookie),writer);
  HttpRuntime.ProcessRequest(request); request.Wait();
  return new JournalResult { Body=request.Body,Cookie=request.Cookie,Status=request.Status,Location=request.Location };
 }
 public override object InitializeLifetimeService() { return null; }
}
class Phase7WebChecks
{
 static string unit; static JournalRunner runner; static JournalStore store; static string cookie,query;
 static DateTime first=new DateTime(2026,1,1),last=new DateTime(2026,12,31);
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static JournalResult Request(string post=null)
 {
  var r=runner.Run(query,post,cookie); if(!string.IsNullOrEmpty(r.Cookie)) cookie=r.Cookie;
  File.WriteAllText("artifacts/phase7/web-last-response.html",r.Body);
  if(r.Status>=500) throw new Exception("ASP.NET error; see artifacts/phase7/web-last-response.html");
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
 static JournalResult Post(JournalResult page,string button,Dictionary<string,string> controls=null,Dictionary<string,string> raw=null)
 {
  var fields=Hidden(page.Body); fields["ctl00$WorkspaceContent$"+button]="1";
  if(controls!=null) foreach(var kv in controls) fields["ctl00$WorkspaceContent$"+kv.Key]=kv.Value;
  if(raw!=null) foreach(var kv in raw) fields[kv.Key]=kv.Value;
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 static void Screen(string screen) { query="screen="+screen+"&unit="+unit+"&from=2026-01-01&to=2026-12-31"; }
 static Dictionary<string,string> Criteria(string item="HANG002")
 { return new Dictionary<string,string>{{"Unit",""+unit+""},{"From","2026-01-01"},{"To","2026-12-31"},{"Filter",item}}; }
 static JournalResult Select(JournalResult page)
 {
  var fields=Hidden(page.Body); fields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records"; fields["__EVENTARGUMENT"]="Select$0";
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 static Dictionary<string,string> Lines(string debit,string credit)
 { return new Dictionary<string,string>{{"lineCount","2"},{"item0","TS_TIEN"},{"qty0",debit},{"price0","0"},{"item1","NV_VON"},{"qty1","0"},{"price1",credit}}; }
 static JournalResult Save(JournalResult page,string button,string number,string debit,string credit)
 { return Post(page,button,new Dictionary<string,string>{{"Number",number},{"DocumentDate","2026-01-05"},{"Note","Phase 7 isolated test"}},Lines(debit,credit)); }
 static void Main(string[] args)
 {
  Directory.CreateDirectory("artifacts/phase7");
  string cs="Data Source=.\\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10";
  store=new JournalStore(cs); unit="W7"+Guid.NewGuid().ToString("N").Substring(0,10);
  new CatalogStore(cs).Create(CatalogDefinition.Get("units"),new[]{unit,"Phase 7 UI test","2026-01-01"});
  try {
   runner=(JournalRunner)ApplicationHost.CreateApplicationHost(typeof(JournalRunner),"/",Path.GetFullPath(args[0]));
   Screen("journal"); var page=Request(); Check(page.Status==200,"journal list renders");
   page=Post(page,"New"); page=Post(page,"AddLine",null,new Dictionary<string,string>{{"lineCount","1"},{"item0","TS_TIEN"},{"qty0","10"},{"price0","0"}});
   Check(page.Body.Contains("item1"),"add second journal line");
   page=Save(page,"Post","BAD","10.0001","10"); Check(page.Status==200&&HttpUtility.HtmlDecode(page.Body).Contains("Tổng Nợ phải bằng"),"UI shows exact imbalance error");
   Check(store.List(unit,first,last,"BAD").Rows.Count==0,"failed UI post leaves no header");
   page=Save(page,"SaveDraft","DRAFT","10","9"); Check(page.Status==302,"unbalanced draft saved");
   page=Post(Select(Request()),"Edit"); Check(page.Body.Contains("TS_TIEN"),"draft reloads accounts");
   page=Save(page,"SaveDraft","DRAFT","20","20"); Check(page.Status==302,"draft edited");
   page=Post(Select(Request()),"Delete"); Check(page.Status==302&&store.List(unit,first,last,"").Rows.Count==0,"draft deleted");
   page=Post(Request(),"New"); page=Save(page,"Post","CAPITAL","100.1234","100.1234"); Check(page.Status==302,"balanced journal posted");
   page=Post(Select(Request()),"View"); Check(page.Body.Contains("readonly")&&page.Body.Contains("CAPITAL"),"posted journal read-only");
   Screen("balance"); page=Request(); Check(!page.Body.Contains("balance-group"),"balance waits for criteria");
   var criteria=Criteria(""); page=Post(page,"Apply",criteria);
   Check(page.Status==200&&page.Body.Contains("balance-group")&&page.Body.Contains("balance-account"),"hierarchical balance rows rendered");
   Check(HttpUtility.HtmlDecode(page.Body).Contains("100,1234"),"report renders exact posted amount");
   Check(!page.Body.Contains("value=\"Mới\""),"report toolbar omits CRUD");
   page=Post(page,"Refresh",new Dictionary<string,string>{{"To","2026-01-01"}});
   Check(HttpUtility.HtmlDecode(page.Body).Contains("100,1234"),"refresh retains effective period");
   criteria["From"]="2026-01-06"; page=Post(page,"Apply",criteria);
   Check(HttpUtility.HtmlDecode(page.Body).Contains("100,1234"),"prior journals carried into opening");
   File.WriteAllText("artifacts/phase7/balance.html",page.Body);
   query="screen=unknown"; Check(Request().Status==404,"unknown route rejected");
  } finally {
   using(var db=new System.Data.SqlClient.SqlConnection(cs)) { db.Open(); using(var cmd=db.CreateCommand()) {
    cmd.CommandText="SET QUOTED_IDENTIFIER ON; SET ARITHABORT ON; DELETE d FROM dbo.CTPhieuKeToan d JOIN dbo.PhieuKeToan h ON h.PhieuId=d.PhieuId WHERE h.MaDonVi=@u; DELETE dbo.PhieuKeToan WHERE MaDonVi=@u; DELETE dbo.DonVi WHERE MaDonVi=@u;";
    cmd.Parameters.AddWithValue("@u",unit); cmd.ExecuteNonQuery();
   } }
  }
  Console.WriteLine("PHASE 7 WEB FORMS CHECKS PASSED; fixtures removed.");
 }
}
