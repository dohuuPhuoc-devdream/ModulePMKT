using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using ReportingModule.Web;
class Phase5Checks
{
 static string cs,u,i; static CommerceStore c; static StockStore stock; static DateTime day=new DateTime(2026,1,1);
 static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS: "+name); }
 static void Reject(Action a,string name) { bool bad=false; try { a(); } catch(ArgumentException) { bad=true; } catch(SqlException e) { if((e.Number>=51201&&e.Number<=51799)||e.Number==2627||e.Number==2601) bad=true; else throw; } Check(bad,name); }
 static object SQL(string sql) { using(var db=new SqlConnection(cs)) { db.Open(); using(var cmd=new SqlCommand("SET QUOTED_IDENTIFIER ON; SET ARITHABORT ON; "+sql,db)) { cmd.Parameters.AddWithValue("@u",u); cmd.Parameters.AddWithValue("@i",i); return cmd.ExecuteScalar(); } } }
 static StockLine[] Lines(decimal q) { return new[]{new StockLine(i,q,2.1234m)}; }
 static long Save(string k,string no,decimal q,bool post=true) { return c.Save(null,null,u,k,no,day.AddDays(2),"test",Lines(q),post,CommerceStore.IsSales(k)?"KHDEMO001":"NCCDEMO001"); }
 static byte[] Version(long id,string k) { return (byte[])c.Load(id,u,k).Header["PhienBan"]; }
 static decimal Closing() { return (decimal)stock.Report(u,day,day.AddYears(1),i,"T").Rows[0]["TonCuoi"]; }
 static decimal Total(string mode,string partner="") { return c.Report(u,day,day.AddYears(1),partner,mode).Rows.Cast<DataRow>().Sum(r=>(decimal)r["ThanhTien"]); }
 static void Main(string[] args)
 {
  cs=args[0]; c=new CommerceStore(cs); stock=new StockStore(cs); string tag=Guid.NewGuid().ToString("N").Substring(0,10); u="U5"+tag; i="I5"+tag;
  var catalogs=new CatalogStore(cs); catalogs.Create(CatalogDefinition.Get("units"),new[]{u,"Phase 5 isolated test","2026-01-01"});
  try {
   catalogs.Create(CatalogDefinition.Get("items"),new[]{i,"Phase 5 item","Cai","1","2"}); SQL("INSERT dbo.TonDauKy VALUES(@u,@i,100)");
   Save("SALES_ORDER","SO",1000); Save("PURCHASE_ORDER","PO",1000);
   Check(Closing()==100 && Total("revenue")==0 && Total("purchase-detail")==0,"posted orders excluded from stock and invoice reports");
   long draft=Save("SALES_INVOICE","DRAFT",200,false); var v=Version(draft,"SALES_INVOICE");
   Reject(()=>c.Save(draft,v,u,"SALES_INVOICE","DRAFT",day.AddDays(2),"",Lines(200),true,"KHDEMO001"),"overselling draft rejects atomically");
   Check((string)c.Load(draft,u,"SALES_INVOICE").Header["TrangThai"]=="DRAFT" && Closing()==100,"failed posting preserves draft and stock");
   c.Save(draft,v,u,"SALES_INVOICE","DRAFT",day.AddDays(2),"",Lines(1),false,"KHDEMO001");
   Reject(()=>c.Delete(draft,v,u,"SALES_INVOICE"),"stale delete rejected");
   Reject(()=>c.Save(draft,v,u,"SALES_INVOICE","DRAFT",day.AddDays(2),"",Lines(2),false,"KHDEMO001"),"stale edit rejected");
   c.Delete(draft,Version(draft,"SALES_INVOICE"),u,"SALES_INVOICE");
   long receipt=stock.Save(null,null,u,"N","PR",day.AddDays(2),"receipt",Lines(50),true,"NCCDEMO001",true);
   long purchase=c.Save(null,null,u,"PURCHASE_INVOICE","PI",day.AddDays(2),"invoice",Lines(50),true,"NCCDEMO001",receipt); long sale=Save("SALES_INVOICE","SI",20);
   Check(Closing()==130,"purchase receipt +50 and sales invoice -20 update inventory exactly once");
   Check(Convert.ToInt32(SQL("SELECT COUNT(*) FROM dbo.ChungTuKho WHERE MaDonVi=@u AND ChungTuThuongMaiId IS NOT NULL"))==1,"purchase invoice does not create another stock movement");
   Reject(()=>stock.Save(null,null,u,"N","NO-SUPPLIER",day.AddDays(2),"",Lines(1),true,null,true),"purchase receipt requires a supplier");
   Reject(()=>c.Save(null,null,u,"PURCHASE_INVOICE","UNLINKED",day.AddDays(2),"",Lines(1),true,"NCCDEMO001"),"purchase invoice cannot post without receipt link");
   Reject(()=>c.Save(null,null,u,"PURCHASE_INVOICE","WRONG-RECEIPT",day.AddDays(2),"",Lines(1),true,"NCCDEMO001",999999),"purchase invoice rejects unknown receipt");
   Reject(()=>c.Save(null,null,u,"PURCHASE_INVOICE","WRONG-SUPPLIER",day.AddDays(2),"",Lines(1),true,"NCCDEMO002",receipt),"purchase invoice rejects a receipt for another supplier");
   foreach(var mode in new[]{"sales-detail","revenue","sales-analysis"}) Check(Total(mode)==42.468m,mode+" reconciles 20*2.1234");
   foreach(var mode in new[]{"purchase-detail","purchase-analysis"}) Check(Total(mode)==106.17m,mode+" reconciles 50*2.1234");
   Check(Total("revenue","KHDEMO002")==0 && Total("revenue","KHDEMO001")==42.468m,"partner filters");
   Check(c.Report(u,day,day.AddDays(1),"","revenue").Rows.Count==0,"date filter excludes later invoices");
   Check((int)c.Report(u,day,day.AddDays(2),"","revenue").Rows[0]["SoHoaDon"]==1,"inclusive end date and invoice count");
   Reject(()=>Save("SALES_INVOICE","FAIL",200),"new oversell rejected");
   Check(c.List(u,"SALES_INVOICE",day,day.AddYears(1),"FAIL").Rows.Count==0 && Closing()==130,"failed new invoice rolls back header details and stock");
   Reject(()=>c.Save(sale,Version(sale,"SALES_INVOICE"),u,"SALES_INVOICE","SI",day.AddDays(2),"",Lines(20),true,"KHDEMO001"),"reposting blocked");
   Reject(()=>c.Delete(sale,Version(sale,"SALES_INVOICE"),u,"SALES_INVOICE"),"posted delete blocked");
   Reject(()=>Save("SALES_INVOICE","SI",1),"duplicate invoice blocked");
   Reject(()=>c.Save(null,null,u,"SALES_INVOICE","BAD",day,"",Lines(1),true,"NCCDEMO001"),"wrong partner rejected");
   Reject(()=>c.Save(null,null,u,"SALES_INVOICE","BAD",day,"",new[]{new StockLine(i,0,1)},true,"KHDEMO001"),"zero quantity rejected");
   Reject(()=>c.Save(null,null,u,"SALES_INVOICE","BAD",day,"",new[]{new StockLine(i,1,-1)},true,"KHDEMO001"),"negative price rejected");
   Reject(()=>c.Save(null,null,u,"SALES_INVOICE","BAD",day.AddDays(-1),"",Lines(1),true,"KHDEMO001"),"pre-opening date rejected");
   Reject(()=>c.Save(null,null,u,"SALES_INVOICE","BAD",day,"",new[]{new StockLine("UNKNOWN",1,1)},true,"KHDEMO001"),"unknown item rejected");
   Check(c.List(u,"SALES_INVOICE",day,day.AddYears(1),"' OR 1=1--").Rows.Count==0,"literal search parameterization");
   int passed=0,rejected=0;
   Parallel.For(0,2,n=> { try { Save("SALES_INVOICE","RACE"+n,100); System.Threading.Interlocked.Increment(ref passed); } catch(SqlException ex) { if(ex.Number!=51708&&ex.Number!=51207) throw; System.Threading.Interlocked.Increment(ref rejected); } });
   Check(passed==1&&rejected==1&&Closing()==30,"concurrent invoices cannot oversell");
   Check(Convert.ToInt32(SQL("SELECT COUNT(*) FROM dbo.ChungTuKho k JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=k.ChungTuThuongMaiId WHERE k.MaDonVi=@u AND (h.TrangThai<>'POSTED' OR k.TrangThai<>'POSTED')"))==0,"no partially posted linked facts");
   Check((decimal)stock.Report("DVDEMO",day,day.AddYears(1),"HANG001","T").Rows[0]["TonCuoi"]==130,"existing HANG001 acceptance preserved");
  } finally {
   SQL("UPDATE dbo.ChungTuThuongMai SET ChungTuNhapMuaId=NULL WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuKho d JOIN dbo.ChungTuKho h ON h.ChungTuKhoId=d.ChungTuKhoId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuKho WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuThuongMai d JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=d.ChungTuId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuThuongMai WHERE MaDonVi=@u; DELETE dbo.TonDauKy WHERE MaDonVi=@u; DELETE dbo.DonVi WHERE MaDonVi=@u; DELETE dbo.HangHoa WHERE MaHang=@i;");
  }
  Console.WriteLine("PHASE 5 SERVICE CHECKS PASSED; isolated fixtures removed.");
 }
}
