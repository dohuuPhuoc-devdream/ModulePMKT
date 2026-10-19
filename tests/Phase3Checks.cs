using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Xml.Linq;
using ReportingModule.Web;

class Phase3Checks
{
 static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static void Reject(Action a,string name) { bool failed=false; try { a(); } catch(ArgumentException) { failed=true; } Check(failed,name); }
 static string[] Row(CatalogDefinition d,string id,string name)
 {
  if(d.Id=="items") return new[]{id,name,"Cái","123.4567","456.7890"};
  if(d.Id=="units") return new[]{id,name,"2026-01-01","","","","","",""};
  return new[]{id,name,"","Địa chỉ thử nghiệm",""};
 }
 static string[] Strings(object[] row) { return row.Select(CatalogDefinition.Text).ToArray(); }
 static void Main(string[] args)
 {
  var store=new CatalogStore(args[0]);
  string prefix="P3"+Guid.NewGuid().ToString("N").Substring(0,10);
  var cleanup=new List<Tuple<CatalogDefinition,string>>();
  Directory.CreateDirectory("artifacts/phase3");
  try {
   foreach(string id in new[]{"items","customers","suppliers","units"})
   {
    var d=CatalogDefinition.Get(id); string key=prefix+id.Substring(0,2);
    cleanup.Add(Tuple.Create(d,key));
    store.Create(d,Row(d,key,"Thử nghiệm %_[] ' dữ liệu"));
    var original=store.Find(d,key);
    Check((string)original[1]=="Thử nghiệm %_[] ' dữ liệu",id+" create/reload Unicode");
    Check(store.List(d,key).Rows.Count==1,id+" code search");
    Check(store.List(d,"%_[] '").Rows.Cast<DataRow>().Any(r=>(string)r[0]==key),id+" literal wildcard search");
    Check(store.List(d,"' OR 1=1--").Rows.Count==0,id+" SQL injection remains literal");
    bool duplicate=false;
    try { store.Create(d,Row(d,key,"Duplicate")); } catch(System.Data.SqlClient.SqlException ex) { duplicate=ex.Number==2627||ex.Number==2601; }
    Check(duplicate,id+" duplicate rejected");
    var edited=Strings(original); edited[1]="Đã sửa";
    store.Update(d,edited,original);
    Check((string)store.Find(d,key)[1]=="Đã sửa",id+" update");
    Reject(()=>store.Update(d,edited,original),id+" stale update rejected");
    Reject(()=>store.Delete(d,original),id+" stale delete rejected");
    Reject(()=>d.Validate(Row(d,"bad code","Name")),id+" invalid code rejected");
    var blank=Row(d,key," "); Reject(()=>d.Validate(blank),id+" required field rejected");
    var table=store.List(d,key); byte[] file=CatalogExcel.Export(d,table);
    File.WriteAllBytes("artifacts/phase3/"+id+".xlsx",file);
    var rows=CatalogExcel.Import(d,new MemoryStream(file));
    Check(rows.Count==1 && rows[0][0]==key && rows[0][1]=="Đã sửa",id+" Excel round-trip");
    if(id=="units") Check(rows[0][2]=="2026-01-01","numeric Excel date round-trip");
    if(id=="items") Check(rows[0][3]=="123,4567","numeric Excel price round-trip");
    string importKey=key+"I"; cleanup.Add(Tuple.Create(d,importKey));
    rows[0][0]=importKey;
    Check(store.Import(d,rows)==1,id+" atomic Excel insert");
    string rollbackKey=key+"R"; cleanup.Add(Tuple.Create(d,rollbackKey));
    Reject(()=>store.Import(d,new[]{Row(d,rollbackKey,"Valid first"),Row(d,key,"Existing second")}),id+" duplicate rolls whole batch back");
    Check(store.List(d,rollbackKey).Rows.Count==0,id+" no partial import");
    Reject(()=>store.Import(d,new[]{Row(d,rollbackKey,"Valid"),Row(d,rollbackKey,"Duplicate")}),id+" duplicate within file");
    Reject(()=>store.Import(d,new[]{Row(d,rollbackKey,"Valid"),Row(d,key+"X","")}),id+" invalid row rejects batch");
    Check(store.List(d,rollbackKey).Rows.Count==0,id+" validation writes nothing");
    var template=CatalogExcel.Export(d,new DataTable());
    Check(CatalogExcel.Import(d,new MemoryStream(template)).Count==0,id+" empty template valid");
    store.Delete(d,store.Find(d,key));
    Check(store.List(d,key).Rows.Cast<DataRow>().All(r=>(string)r[0]!=key),id+" delete unreferenced");
   }
   var items=CatalogDefinition.Get("items");
   bool fk=false; try { store.Delete(items,store.Find(items,"HANG001")); } catch(System.Data.SqlClient.SqlException ex) { fk=ex.Number==547; }
   Check(fk,"referenced HANG001 delete blocked");
   var units=CatalogDefinition.Get("units"); var unit=store.Find(units,"DVDEMO"); var change=Strings(unit); change[2]="2027-01-01";
   Reject(()=>store.Update(units,change,unit),"unit opening date protected after opening balances");
   Check(units.Validate(new[]{"TAX10","Tax test","2026-01-01","","","0101234565","","",""}).Length==9,"valid 10 digit tax ID accepted");
   Check(units.Validate(new[]{"TAX10B","Tax test","2026-01-01","","","1000000001","","",""}).Length==9,"second valid 10 digit tax ID accepted");
   Reject(()=>units.Validate(new[]{"TAXBAD","Tax test","2026-01-01","","","0101234566","","",""}),"invalid tax ID check digit rejected");
   Reject(()=>units.Validate(new[]{"TAXZERO","Tax test","2026-01-01","","","1000000080","","",""}),"tax ID with checksum remainder zero rejected");
   Reject(()=>items.Validate(new[]{"NEG","Name","Cái","-1","0"}),"negative price");
   Reject(()=>items.Validate(new[]{"PREC","Name","Cái","1.12345","0"}),"excess decimal precision");
   Reject(()=>items.Validate(new[]{"MAX","Name","Cái","1000000000000000","0"}),"decimal overflow");
   var malicious=new DataTable(); foreach(var f in items.Fields) malicious.Columns.Add(f.Name,f.Kind=="decimal"?typeof(decimal):typeof(string));
   malicious.Rows.Add("FORMULA","=HYPERLINK(\"https://example.invalid\")","Cái",1m,2m);
   var safe=CatalogExcel.Export(items,malicious); File.WriteAllBytes("artifacts/phase3/formula-text.xlsx",safe);
   using(var stream=new MemoryStream())
   {
    stream.Write(safe,0,safe.Length); stream.Position=0;
    using(var zip=new ZipArchive(stream,ZipArchiveMode.Update,true))
    {
     var entry=zip.GetEntry("xl/worksheets/sheet1.xml"); XDocument doc;
     using(var input=entry.Open()) doc=XDocument.Load(input);
     XNamespace s="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
     doc.Descendants(s+"c").First().Add(new XElement(s+"f","1+1"));
     entry.Delete(); using(var output=zip.CreateEntry("xl/worksheets/sheet1.xml").Open()) doc.Save(output);
    }
    stream.Position=0;
    Reject(()=>CatalogExcel.Import(items,stream),"formula workbook rejected");
   }
   Reject(()=>CatalogExcel.Import(items,new MemoryStream(new byte[6*1024*1024])),"oversized input rejected");
   if(File.Exists("artifacts/phase3/openpyxl-items.xlsx"))
   {
    using(var input=File.OpenRead("artifacts/phase3/openpyxl-items.xlsx")) {
     var rows=CatalogExcel.Import(items,input);
     Check(rows.Count==1 && rows[0][0]=="EXTERNAL" && rows[0][3]=="12,3456","independent Excel writer import");
    }
   }
   Check((string)store.Find(items,"HANG001")[0]=="HANG001","seed item preserved");
   Console.WriteLine("ALL PHASE 3 SERVICE/EXCEL CHECKS PASSED");
  }
  finally {
   foreach(var item in cleanup.AsEnumerable().Reverse())
   {
    var rows=store.List(item.Item1,item.Item2).Rows.Cast<DataRow>().Where(r=>(string)r[0]==item.Item2).ToArray();
    if(rows.Length==1) store.Delete(item.Item1,rows[0].ItemArray);
   }
   Console.WriteLine("Temporary test masters removed.");
  }
 }
}

