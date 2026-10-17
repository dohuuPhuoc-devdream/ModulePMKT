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
public class StockResult { public string Body,Cookie,Location; public int Status; }
public class StockRequest : SimpleWorkerRequest
{
 readonly string post,cookie; readonly ManualResetEvent done=new ManualResetEvent(false);
 public int Status=200; public string Cookie="",Location="";
 private readonly MemoryStream responseBytes=new MemoryStream();
 public string Body { get { return Encoding.UTF8.GetString(responseBytes.ToArray()); } }
 public override void SendResponseFromMemory(byte[] data,int length) { responseBytes.Write(data,0,length); }
 public StockRequest(string query,string post,string cookie,TextWriter writer):base("Stock.aspx",query,writer) { this.post=post; this.cookie=cookie; }
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
public class StockRunner : MarshalByRefObject
{
 public StockResult Run(string query,string post,string cookie)
 {
  var writer=new StringWriter(); var request=new StockRequest(query,post,PhaseTestAuth.Cookie(cookie),writer);
  HttpRuntime.ProcessRequest(request); request.Wait();
  return new StockResult { Body=request.Body,Cookie=request.Cookie,Status=request.Status,Location=request.Location };
 }
 public override object InitializeLifetimeService() { return null; }
}
class Phase4WebChecks
{
 static StockRunner runner; static StockStore store; static string cookie,query;
 static DateTime first=new DateTime(2026,1,1),last=new DateTime(2026,12,31);
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static StockResult Request(string post=null)
 {
  var r=runner.Run(query,post,cookie); if(!string.IsNullOrEmpty(r.Cookie)) cookie=r.Cookie;
  File.WriteAllText("artifacts/phase4/web-last-response.html",r.Body);
  if(r.Status>=500) throw new Exception("ASP.NET error; see artifacts/phase4/web-last-response.html");
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
 static StockResult Post(StockResult page,string button,Dictionary<string,string> controls=null,Dictionary<string,string> raw=null)
 {
  var fields=Hidden(page.Body); fields["ctl00$WorkspaceContent$"+button]="1";
  if(controls!=null) foreach(var kv in controls) fields["ctl00$WorkspaceContent$"+kv.Key]=kv.Value;
  if(raw!=null) foreach(var kv in raw) fields[kv.Key]=kv.Value;
  return Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
 }
 static void Screen(string screen) { query="screen="+screen+"&unit=DVDEMO&from=2026-01-01&to=2026-12-31"; }
 static Dictionary<string,string> Criteria(string item="HANG001")
 { return new Dictionary<string,string>{{"Unit","DVDEMO"},{"From","2026-01-01"},{"To","2026-12-31"},{"Filter",item}}; }
 static Dictionary<string,string> Line(string qty)
 { return new Dictionary<string,string>{{"lineCount","1"},{"item0","HANG001"},{"qty0",qty},{"price0","100"}}; }
 static void Document(string kind,string number,string date,string qty)
 {
  var existing=store.List("DVDEMO",kind,first,last,number).Rows.Cast<DataRow>().Where(r=>(string)r["SoChungTu"]==number).ToArray();
  if(existing.Length>0)
  {
   var d=store.Load(Convert.ToInt64(existing[0]["ChungTuKhoId"]),"DVDEMO",kind);
   Check((string)d.Header["TrangThai"]=="POSTED" && d.Lines.Rows.Cast<DataRow>().All(r=>(string)r["MaHang"]=="HANG001") &&
    d.Lines.Rows.Cast<DataRow>().Sum(r=>(decimal)r["SoLuong"])==decimal.Parse(qty),"existing acceptance document matches "+number);
   return;
  }
  Screen(kind=="N"?"stock-receipt":"stock-issue");
  var page=Request(); Check(page.Status==200,"document list opens");
  page=Post(page,"New");
  Check(page.Body.Contains("name=\"item0\""),"header/detail editor opens");
  page=Post(page,"Post",new Dictionary<string,string>{{"Number",number},{"DocumentDate",date},{"Note","Synthetic acceptance HANG001"}},Line(qty));
  Check(page.Status==302,"Web Forms posts "+number);
  existing=store.List("DVDEMO",kind,first,last,number).Rows.Cast<DataRow>().Where(r=>(string)r["SoChungTu"]==number).ToArray();
  Check(existing.Length==1 && (string)existing[0]["TrangThai"]=="POSTED","SQL persisted posted "+number);
 }
 static void Main(string[] args)
 {
  Directory.CreateDirectory("artifacts/phase4");
  store=new StockStore("Data Source=.\\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10");
  runner=(StockRunner)ApplicationHost.CreateApplicationHost(typeof(StockRunner),"/",Path.GetFullPath(args[0]));
  Document("N","PN001","2026-01-02","50");
  Document("X","PX001","2026-01-03","20");
  var report=store.Report("DVDEMO",first,last,"HANG001","T").Rows[0];
  Check((decimal)report["TonDau"]==100 && (decimal)report["Nhap"]==50 && (decimal)report["Xuat"]==20 && (decimal)report["TonCuoi"]==130,"HANG001 DB-derived 100+50-20=130");
  int headers=store.List("DVDEMO","X",first,last,"").Rows.Count;
  Screen("stock-issue"); var page=Request(); page=Post(page,"New");
  page=Post(page,"Post",new Dictionary<string,string>{{"Number","REJECT200"},{"DocumentDate","2026-01-04"},{"Note","Must fail"}},Line("200"));
  Check(page.Status==200 && HttpUtility.HtmlDecode(page.Body).Contains("Không đủ tồn kho"),"Web Forms shows insufficient-stock error");
  Check(store.List("DVDEMO","X",first,last,"").Rows.Count==headers && store.List("DVDEMO","X",first,last,"REJECT200").Rows.Count==0,"failed UI posting writes no header");
  Check((decimal)store.Report("DVDEMO",first,last,"HANG001","T").Rows[0]["TonCuoi"]==130,"failed UI posting leaves stock at 130");
  foreach(string screen in new[]{"inbound","outbound","inventory"})
  {
   Screen(screen); page=Request();
   Check(!page.Body.Contains(">HANG001<"),screen+" waits for criteria");
   page=Post(page,"Apply",Criteria());
   Check(page.Status==200 && page.Body.Contains("HANG001"),screen+" filtered SQL result renders");
   Check(!page.Body.Contains("value=\"Mới\""),screen+" read-only toolbar");
   if(screen=="inventory") Check(HttpUtility.HtmlDecode(page.Body).Contains("130,000"),"inventory grid renders closing 130");
   page=Post(page,"Refresh");
   Check(page.Body.Contains("HANG001"),screen+" refresh keeps effective criteria");
   File.WriteAllText("artifacts/phase4/"+screen+".html",page.Body);
  }
  Screen("inventory"); page=Request();
  page=Post(page,"Apply",new Dictionary<string,string>{{"Unit","DVDEMO"},{"From","2026-01-03"},{"To","2026-01-03"},{"Filter","HANG001"}});
  Check(HttpUtility.HtmlDecode(page.Body).Contains("150,000") && HttpUtility.HtmlDecode(page.Body).Contains("130,000"),"period-opening report visible in grid");
  string draftNumber="WEB4"+Guid.NewGuid().ToString("N").Substring(0,8);
  long? draftId=null;
  try {
   Screen("stock-receipt"); page=Request(); page=Post(page,"New");
   page=Post(page,"AddLine",null,Line("1"));
   Check(page.Body.Contains("name=\"item1\""),"UI adds a second detail row");
   var raw=Line("1"); raw["lineCount"]="2"; raw["item1"]="HANG002"; raw["qty1"]="2"; raw["price1"]="3";
   page=Post(page,"SaveDraft",new Dictionary<string,string>{{"Number",draftNumber},{"DocumentDate","2026-01-05"},{"Note","Temporary UI draft"}},raw);
   Check(page.Status==302,"multi-line UI draft saved");
   draftId=Convert.ToInt64(store.List("DVDEMO","N",first,last,draftNumber).Rows[0]["ChungTuKhoId"]);
   var draft=store.Load(draftId.Value,"DVDEMO","N");
   Check(draft.Lines.Rows.Count==2 && draft.Lines.Rows.Cast<DataRow>().Sum(r=>(decimal)r["ThanhTien"])==106,"multi-line totals reload correctly");
   Check((decimal)store.Report("DVDEMO",first,last,"HANG001","T").Rows[0]["TonCuoi"]==130,"UI draft does not affect inventory");
   query+="&q="+draftNumber; page=Request();
   var fields=Hidden(page.Body); fields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records"; fields["__EVENTARGUMENT"]="Select$0";
   page=Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
   page=Post(page,"Edit");
   Check(page.Body.Contains("name=\"item1\"") && page.Body.Contains(draftNumber),"UI edit reloads header and both details");
   raw["qty0"]="4";
   page=Post(page,"SaveDraft",new Dictionary<string,string>{{"Number",draftNumber},{"DocumentDate","2026-01-05"},{"Note","Updated draft"}},raw);
   Check(page.Status==302 && (decimal)store.Load(draftId.Value,"DVDEMO","N").Lines.Rows[0]["SoLuong"]==4,"UI draft update persists");
   page=Request(); fields=Hidden(page.Body); fields["__EVENTTARGET"]="ctl00$WorkspaceContent$Records"; fields["__EVENTARGUMENT"]="Select$0";
   page=Request(string.Join("&",fields.Select(kv=>HttpUtility.UrlEncode(kv.Key)+"="+HttpUtility.UrlEncode(kv.Value))));
   page=Post(page,"Delete");
   Check(page.Status==302 && store.List("DVDEMO","N",first,last,draftNumber).Rows.Count==0,"UI draft delete removes all details");
   draftId=null;
  } finally {
   if(draftId.HasValue) { var draft=store.Load(draftId.Value,"DVDEMO","N"); store.Delete(draftId.Value,(byte[])draft.Header["PhienBan"],"DVDEMO","N"); }
  }
  Console.WriteLine("PHASE 4 WEB FORMS ACCEPTANCE PASSED; PN001/PX001 retained as demo.");
 }
}
