using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using ReportingModule.Web;
class Phase7Checks
{
 static string cs,u; static JournalStore store; static DateTime day=new DateTime(2026,1,1);
 static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static void Reject(Action action,string name) { bool bad=false; try { action(); } catch(ArgumentException) { bad=true; } catch(SqlException ex) { if((ex.Number>=51501&&ex.Number<=51507)||ex.Number==2627||ex.Number==2601) bad=true; else throw; } Check(bad,name); }
 static JournalLine[] Lines(decimal amount) { return new[]{new JournalLine("TS_TIEN",amount,0),new JournalLine("NV_VON",0,amount)}; }
 static long Save(string no,int date,JournalLine[] lines,bool post=true) { return store.Save(null,null,u,no,day.AddDays(date),"isolated test",lines,post); }
 static decimal Value(string code,string col,int from=0,int to=30) { return (decimal)store.Report(u,day.AddDays(from),day.AddDays(to)).Rows.Cast<DataRow>().Single(r=>(string)r["Ma"]==code)[col]; }
 static byte[] Version(long id) { return (byte[])store.Load(id,u).Header["PhienBan"]; }
 static object SQL(string sql) { using(var c=new SqlConnection(cs)) { c.Open(); using(var cmd=new SqlCommand("SET QUOTED_IDENTIFIER ON; SET ARITHABORT ON; "+sql,c)) { cmd.Parameters.AddWithValue("@u",u); return cmd.ExecuteScalar(); } } }
 static void Main(string[] args)
 {
  cs=args[0]; store=new JournalStore(cs); u="U7"+Guid.NewGuid().ToString("N").Substring(0,10);
  new CatalogStore(cs).Create(CatalogDefinition.Get("units"),new[]{u,"Phase 7 isolated test","2026-01-01"});
  try {
   Check(store.Accounts().Rows.Count==6,"six configured teaching accounts");
   Check(Value("A","CuoiKy")==0&&Value("SOURCE","CuoiKy")==0,"empty ledger renders zero balances");
   long draft=Save("DRAFT",1,new[]{new JournalLine("TS_TIEN",5,0)},false); var v=Version(draft);
   Check(Value("A","CuoiKy")==0,"unbalanced draft excluded");
   Reject(()=>store.Save(draft,v,u,"DRAFT",day.AddDays(1),"",new[]{new JournalLine("TS_TIEN",5,0)},true),"unbalanced posting blocked");
   Check((string)store.Load(draft,u).Header["TrangThai"]=="DRAFT"&&store.Load(draft,u).Lines.Rows.Count==1,"failed post retains original draft");
   store.Save(draft,v,u,"DRAFT",day.AddDays(1),"",Lines(5),false);
   Reject(()=>store.Delete(draft,v,u),"stale draft delete rejected");
   Reject(()=>store.Save(draft,v,u,"DRAFT",day.AddDays(1),"",Lines(5),false),"stale draft update rejected");
   store.Delete(draft,Version(draft),u); Check(store.List(u,day,day.AddDays(30),"DRAFT").Rows.Count==0,"draft deleted atomically");
   long posted=Save("CAPITAL",1,Lines(100.1234m));
   Save("PURCHASE",2,new[]{new JournalLine("TS_HANG",50,0),new JournalLine("NV_NCC",0,50)});
   Save("PAY",3,new[]{new JournalLine("NV_NCC",20,0),new JournalLine("TS_TIEN",0,20)});
   Check(Value("A","CuoiKy")==130.1234m&&Value("L","CuoiKy")==30&&Value("E","CuoiKy")==100.1234m,"asset liability equity totals");
   Check(Value("SOURCE","CuoiKy")==130.1234m&&Value("CHECK","CuoiKy")==0,"assets equal sources exactly");
   Check(Value("A","DauKy",3,3)==150.1234m&&Value("A","CuoiKy",3,3)==130.1234m,"opening before From and inclusive To");
   Check(Value("A","CuoiKy",0,1)==100.1234m,"later journals excluded");
   Reject(()=>Save("TINY",4,new[]{new JournalLine("TS_TIEN",1.0001m,0),new JournalLine("NV_VON",0,1)}),"0.0001 imbalance cannot round to zero");
   Check(store.List(u,day,day.AddDays(30),"TINY").Rows.Count==0,"failed new posting leaves no header");
   Reject(()=>Save("BOTH",4,new[]{new JournalLine("TS_TIEN",1,1)}),"two-sided line rejected");
   Reject(()=>Save("ZERO",4,new[]{new JournalLine("TS_TIEN",0,0)}),"zero line rejected");
   Reject(()=>Save("PREC",4,Lines(1.00001m)),"excess precision rejected");
   Reject(()=>Save("EMPTY",4,new JournalLine[0]),"empty lines rejected");
   Reject(()=>Save("UNKNOWN",4,new[]{new JournalLine("UNKNOWN",1,0),new JournalLine("NV_VON",0,1)}),"unmapped account rejected");
   Reject(()=>Save("EARLY",-1,Lines(1)),"before opening date rejected");
   Reject(()=>Save("CAPITAL",4,Lines(1)),"duplicate number rejected");
   Reject(()=>store.Delete(posted,Version(posted),u),"posted delete rejected");
   Reject(()=>store.Save(posted,Version(posted),u,"CAPITAL",day,"",Lines(1),true),"posted edit/repost rejected");
   Reject(()=>store.Load(posted,"DVDEMO"),"cross-unit load rejected");
   Check(store.List(u,day,day.AddDays(30),"' OR 1=1--").Rows.Count==0,"literal parameterized search");
   Reject(()=>store.Report(u,day.AddDays(3),day),"invalid report range rejected");
   int passed=0,rejected=0;
   Parallel.For(0,2,n=>{ try { Save("RACE",4,Lines(1)); System.Threading.Interlocked.Increment(ref passed); } catch(SqlException ex) { if(ex.Number!=2627&&ex.Number!=2601) throw; System.Threading.Interlocked.Increment(ref rejected); } });
   Check(passed==1&&rejected==1,"simultaneous duplicate posting persists once");
   decimal independent=Convert.ToDecimal(SQL("SELECT SUM(d.PhatSinhNo-d.PhatSinhCo) FROM dbo.CTPhieuKeToan d JOIN dbo.PhieuKeToan h ON h.PhieuId=d.PhieuId JOIN dbo.TaiKhoanBaoCao m ON m.MaTK=d.MaTK WHERE h.MaDonVi=@u AND h.TrangThai='POSTED' AND m.Nhom='A'"));
   Check(Value("A","CuoiKy")==independent,"independent SQL reconciliation");
   Save("CONTRA",5,new[]{new JournalLine("NV_VON",200,0),new JournalLine("TS_TIEN",0,200)});
   Check(Value("TS_TIEN","CuoiKy")<0&&Value("CHECK","CuoiKy")==0,"opposite-side balance retains sign without hiding mismatch");
   Check((decimal)new StockStore(cs).Report("DVDEMO",day,day.AddYears(1),"HANG001","T").Rows[0]["TonCuoi"]==130,"journals do not alter stock");
  } finally { SQL("DELETE d FROM dbo.CTPhieuKeToan d JOIN dbo.PhieuKeToan h ON h.PhieuId=d.PhieuId WHERE h.MaDonVi=@u; DELETE dbo.PhieuKeToan WHERE MaDonVi=@u; DELETE dbo.Kho WHERE MaDonVi=@u; DELETE dbo.DonVi WHERE MaDonVi=@u;"); }
  Console.WriteLine("PHASE 7 SERVICE CHECKS PASSED; fixtures removed.");
 }
}
