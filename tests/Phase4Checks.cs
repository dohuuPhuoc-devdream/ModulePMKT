using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReportingModule.Web;
class Phase4Checks
{
 static string cs,unit,item; static StockStore store; static DateTime day=new DateTime(2026,1,1);
 static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static void Reject(Action a,string name)
 {
  bool rejected=false; try { a(); } catch(ArgumentException) { rejected=true; }
  catch(SqlException ex) { if((ex.Number>=51201&&ex.Number<=51208)||(ex.Number>=51701&&ex.Number<=51799)||ex.Number==2627||ex.Number==2601||ex.Number==547) rejected=true; else throw; }
  Check(rejected,name);
 }
 static object SQL(string sql,params SqlParameter[] ps)
 {
  using(var c=new SqlConnection(cs)) { c.Open(); using(var cmd=new SqlCommand("SET QUOTED_IDENTIFIER ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; "+sql,c)) { cmd.Parameters.AddRange(ps); return cmd.ExecuteScalar(); } }
 }
 static decimal Closing(DateTime from,DateTime to) { return (decimal)store.Report(unit,from,to,item,"T").Rows[0]["TonCuoi"]; }
 static StockLine[] Lines(decimal quantity) { return new[]{new StockLine(item,quantity,10)}; }
 static long Save(string kind,string no,int date,decimal qty,bool post=true) { return store.Save(null,null,unit,kind,no,day.AddDays(date),"Phase 4 test",Lines(qty),post); }
 static void Main(string[] args)
 {
  Check(StockStore.CalculateLineAmount(0.001m,0.06m)==0.0001m,"each detail amount rounds half up to four decimal places");
  Check(StockStore.CalculateLineAmount(0.001m,0.06m)+StockStore.CalculateLineAmount(0.001m,0.06m)==0.0002m,"document total sums rounded detail amounts");
  Check(decimal.Round(0.001m*0.06m+0.001m*0.06m,4,MidpointRounding.AwayFromZero)==0.0001m,"rounding test distinguishes detail-first from total-first rounding");
  cs=args[0]; store=new StockStore(cs);
  string tag=Guid.NewGuid().ToString("N").Substring(0,10); unit="U4"+tag; item="I4"+tag;
  var masters=new CatalogStore(cs); var units=CatalogDefinition.Get("units"); var items=CatalogDefinition.Get("items");
  masters.Create(units,new[]{unit,"Phase 4 isolated test","2026-01-01"});
  try {
   masters.Create(items,new[]{item,"Phase 4 test item","Cai","1","2"});
   SQL("INSERT dbo.TonDauKy(MaDonVi,MaHang,SoLuong) VALUES(@u,@i,100);",new SqlParameter("@u",unit),new SqlParameter("@i",item));
   Check(Closing(day,day.AddDays(30))==100,"opening from SQL");
   long draft=Save("X","DRAFT",1,1000,false);
   Check(Closing(day,day.AddDays(30))==100,"draft excluded from stock");
   var d=store.Load(draft,unit,"X"); byte[] v=(byte[])d.Header["PhienBan"];
   store.Save(draft,v,unit,"X","DRAFT",day.AddDays(1),"edited",Lines(999),false);
   Reject(()=>store.Save(draft,v,unit,"X","DRAFT",day.AddDays(1),"stale",Lines(1),false),"stale draft edit blocked");
   Reject(()=>store.Delete(draft,v,unit,"X"),"stale draft delete blocked");
   d=store.Load(draft,unit,"X"); v=(byte[])d.Header["PhienBan"];
   Reject(()=>store.Save(draft,v,unit,"X","DRAFT",day.AddDays(1),"bad item",new[]{new StockLine("NO-SUCH-ITEM",1,1)},false),"unknown item rejects whole edit");
   Check((decimal)store.Load(draft,unit,"X").Lines.Rows[0]["SoLuong"]==999,"failed edit retains previous details");
   Reject(()=>store.Save(draft,v,unit,"X","DRAFT",day.AddDays(1),"oversell",Lines(999),true),"failed draft posting rejected");
   Check((string)store.Load(draft,unit,"X").Header["TrangThai"]=="DRAFT","failed posting leaves draft state");
   store.Delete(draft,(byte[])store.Load(draft,unit,"X").Header["PhienBan"],unit,"X");
   Reject(()=>store.Load(draft,unit,"X"),"delete removes header and details");
   long receipt=Save("N","PN001",1,50);
   long issue=Save("X","PX001",2,20);
   Check(Closing(day,day.AddDays(30))==130,"100 + 50 - 20 = 130");
   Check(store.Report(unit,day,day.AddDays(30),item,"N").Rows.Cast<DataRow>().Sum(r=>(decimal)r["SoLuong"])==50,"inbound reconciles");
   Check(store.Report(unit,day,day.AddDays(30),item,"X").Rows.Cast<DataRow>().Sum(r=>(decimal)r["ThanhTien"])==200,"outbound amount reconciles");
   decimal independent=Convert.ToDecimal(SQL(@"SELECT o.SoLuong+(SELECT SUM(CASE h.Loai WHEN 'N' THEN d.SoLuong ELSE -d.SoLuong END)
    FROM dbo.ChungTuKho h JOIN dbo.CTChungTuKho d ON d.ChungTuKhoId=h.ChungTuKhoId WHERE h.MaDonVi=@u AND h.TrangThai='POSTED' AND d.MaHang=@i)
    FROM dbo.TonDauKy o WHERE o.MaDonVi=@u AND o.MaHang=@i",new SqlParameter("@u",unit),new SqlParameter("@i",item)));
   Check(independent==Closing(day,day.AddDays(30)),"inventory reconciles with independent SQL");
   Reject(()=>Save("X","FAIL200",3,200),"issue 200 rejected");
   Check(store.List(unit,"X",day,day.AddDays(30),"FAIL200").Rows.Count==0 && Closing(day,day.AddDays(30))==130,"failed new posting leaves no header/detail/stock change");
   Reject(()=>store.Save(null,null,unit,"X","REPEATED",day.AddDays(3),"",new[]{new StockLine(item,80,1),new StockLine(item,80,1)},true),"repeated item lines aggregated before stock check");
   Reject(()=>store.Save(null,null,unit,"N","EMPTY",day,"",new StockLine[0],true),"empty details rejected");
   Reject(()=>Save("N","ZERO",1,0),"zero quantity rejected");
   Reject(()=>Save("N","NEG",1,-1),"negative quantity rejected");
   Reject(()=>store.Save(null,null,unit,"N","PRICE",day,"",new[]{new StockLine(item,1,-1)},true),"negative price rejected");
   Reject(()=>Save("N","PREC",1,1.1234m),"quantity excess scale rejected");
   Reject(()=>Save("N","EARLY",-1,1),"date before opening rejected");
   Reject(()=>Save("N","PN001",1,1),"duplicate document number rejected");
   var posted=store.Load(receipt,unit,"N");
   Reject(()=>store.Save(receipt,(byte[])posted.Header["PhienBan"],unit,"N","PN001",day.AddDays(1),"",Lines(500),true),"posted edit rejected");
   Reject(()=>store.Delete(receipt,(byte[])posted.Header["PhienBan"],unit,"N"),"posted delete rejected");
   var later=store.Report(unit,day.AddDays(2),day.AddDays(2),item,"T").Rows[0];
   Check((decimal)later["TonDau"]==150 && (decimal)later["Xuat"]==20 && (decimal)later["TonCuoi"]==130,"period opening and inclusive date boundaries");
   Check(store.Report(unit,day.AddDays(20),day.AddDays(21),item,"N").Rows.Count==0,"empty movement range");
   Reject(()=>store.Report(unit,day.AddDays(1),day,item,"T"),"invalid date range");
   Reject(()=>store.Report(unit,day,day,"NO-SUCH-ITEM","T"),"invalid report item");

   Save("N","FUTURE-IN",10,100);
   Save("X","FUTURE-OUT",11,200); // later closing 30; a backdated issue of 50 must fail.
   Reject(()=>Save("X","BACKDATE",3,50),"backdated issue cannot make later balances negative");
   Check(Closing(day,day.AddDays(30))==30,"backdate rejection rolls back all effects");
   int accepted=0,rejected=0;
   var start=new ManualResetEventSlim(false);
   Action<string> concurrent=(no)=> {
    start.Wait();
    try { Save("X",no,12,20); Interlocked.Increment(ref accepted); }
    catch(SqlException ex) { if(ex.Number!=51708&&ex.Number!=51207) throw; Interlocked.Increment(ref rejected); }
   };
   var a=Task.Run(()=>concurrent("RACE-A")); var b=Task.Run(()=>concurrent("RACE-B")); start.Set(); Task.WaitAll(a,b);
   Check(accepted==1 && rejected==1 && Closing(day,day.AddDays(30))==10,"two concurrent issues cannot oversell");
   Save("X","EMPTY-STOCK",13,10);
   Reject(()=>Save("X","FRACTION-FAIL",14,0.001m),"fractional issue 0.001 cannot hide behind SQL decimal rounding");
   Check(Closing(day,day.AddDays(30))==0,"fractional rejection leaves exact zero");
   Save("N","FRACTION-IN",15,0.123m);
   Save("X","FRACTION-OUT",16,0.023m);
   var fractional=store.Report(unit,day.AddDays(16),day.AddDays(16),item,"T").Rows[0];
   Check((decimal)fractional["TonDau"]==0.123m && (decimal)fractional["Xuat"]==0.023m && (decimal)fractional["TonCuoi"]==0.100m,"inventory preserves three decimal places across period arithmetic");
   Console.WriteLine("ALL PHASE 4 SERVICE/SQL CHECKS PASSED");
  }
  finally {
   // Delete only this run's isolated unit/test item and its test documents, not demo/user documents.
   SQL(@"BEGIN TRANSACTION;
    DELETE d FROM dbo.CTChungTuKho d JOIN dbo.ChungTuKho h ON h.ChungTuKhoId=d.ChungTuKhoId WHERE h.MaDonVi=@u;
    DELETE dbo.ChungTuKho WHERE MaDonVi=@u;
    DELETE dbo.TonDauKy WHERE MaDonVi=@u;
    DELETE dbo.Kho WHERE MaDonVi=@u;
    DELETE dbo.DonVi WHERE MaDonVi=@u;
    DELETE dbo.HangHoa WHERE MaHang=@i;
    COMMIT;",new SqlParameter("@u",unit),new SqlParameter("@i",item));
   Console.WriteLine("Isolated fixtures removed.");
  }
 }
}
