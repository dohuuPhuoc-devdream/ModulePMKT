using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using ReportingModule.Web;
class Phase6Checks
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
 static DataRow Debt(string mode,int from,int to) { return c.Report(u,day.AddDays(from),day.AddDays(to),"",mode).Rows[0]; }
 static void Pay(long id,string kind,string number,int date,decimal amount,Guid? request=null) { c.RecordPayment(id,u,kind,number,day.AddDays(date),amount,"test",request??Guid.NewGuid()); }
 static void Main(string[] args)
 {
  cs=args[0]; c=new CommerceStore(cs); stock=new StockStore(cs); string tag=Guid.NewGuid().ToString("N").Substring(0,10); u="U6"+tag; i="I6"+tag;
  var catalogs=new CatalogStore(cs); catalogs.Create(CatalogDefinition.Get("units"),new[]{u,"Phase 6 isolated test","2026-01-01"});
  try {
   catalogs.Create(CatalogDefinition.Get("items"),new[]{i,"Phase 6 item","Cai","1","2"}); SQL("INSERT dbo.TonDauKy VALUES(@u,@i,100)");
   foreach(var mode in new[]{"receivables","payables"}) Check(c.Report(u,day,day.AddYears(1),"",mode).Rows.Count==0,"empty "+mode);
   long order=Save("SALES_ORDER","ORDER",10); long draft=Save("SALES_INVOICE","DRAFT",10,false);
   Check(c.Report(u,day,day.AddYears(1),"","receivables").Rows.Count==0,"orders and drafts create no debt");
   Reject(()=>Pay(order,"SALES_ORDER","NO",3,1),"orders cannot be paid");
   Reject(()=>Pay(draft,"SALES_INVOICE","NO",3,1),"drafts cannot be paid");
   long receipt=stock.Save(null,null,u,"N","PR",day.AddDays(2),"receipt",Lines(50),true,"NCCDEMO001",true);
   long buy=c.Save(null,null,u,"PURCHASE_INVOICE","PI",day.AddDays(2),"invoice",Lines(50),true,"NCCDEMO001",receipt),sale=Save("SALES_INVOICE","SI",20);
   decimal stockBefore=Closing(),revenue=Total("revenue");
   Pay(sale,"SALES_INVOICE","AR1",3,10.1234m); Pay(buy,"PURCHASE_INVOICE","AP1",3,20.1234m);
   var r=Debt("receivables",0,3); Check((decimal)r["DuDau"]==0 && (decimal)r["PhatSinh"]==42.468m && (decimal)r["ThanhToan"]==10.1234m && (decimal)r["DuCuoi"]==32.3446m,"AR exact fractional balance");
   r=Debt("payables",0,3); Check((decimal)r["DuCuoi"]==86.0466m,"AP exact balance");
   var token=Guid.NewGuid(); Pay(sale,"SALES_INVOICE","AR2",5,2.0001m,token); Pay(sale,"SALES_INVOICE","AR2",5,2.0001m,token);
   Check(c.Payments(sale,u,"SALES_INVOICE").Rows.Count==2,"replayed request creates one payment");
   Reject(()=>Pay(sale,"SALES_INVOICE","AR3",5,2,token),"changed replay payload rejected");
   r=Debt("receivables",4,5); Check((decimal)r["DuDau"]==32.3446m && (decimal)r["PhatSinh"]==0 && (decimal)r["ThanhToan"]==2.0001m && (decimal)r["DuCuoi"]==30.3445m,"prior invoices/payments flow to period opening");
   Check((decimal)Debt("receivables",0,3)["DuCuoi"]==32.3446m,"future payments excluded from historical balance");
   Reject(()=>Pay(sale,"SALES_INVOICE","EARLY",1,1),"payment before invoice rejected");
   Reject(()=>Pay(sale,"SALES_INVOICE","OVER",6,31),"overpayment rejected");
   Reject(()=>Pay(sale,"SALES_INVOICE","ZERO",6,0),"zero payment rejected");
   Reject(()=>Pay(sale,"SALES_INVOICE","SCALE",6,0.00001m),"excess precision rejected");
   Reject(()=>Pay(sale,"PURCHASE_INVOICE","WRONG",6,1),"wrong invoice type rejected");
   Reject(()=>c.RecordPayment(sale,"DVDEMO","SALES_INVOICE","WRONG",day.AddDays(6),1,"",Guid.NewGuid()),"cross-unit payment rejected");
   Reject(()=>Pay(sale,"SALES_INVOICE","AR1",6,1),"duplicate reference rejected");
   Check(c.Payments(sale,u,"SALES_INVOICE").Rows.Count==2,"invalid payments leave no rows");
   int passed=0,rejected=0;
   Parallel.For(0,2,n=>{try {Pay(sale,"SALES_INVOICE","RACE"+n,7,20); System.Threading.Interlocked.Increment(ref passed);} catch(SqlException ex) {if(ex.Number!=51404) throw; System.Threading.Interlocked.Increment(ref rejected);} });
   Check(passed==1&&rejected==1,"simultaneous payments cannot overpay");
   Pay(sale,"SALES_INVOICE","FINAL",8,10.3445m);
   Check((decimal)Debt("receivables",0,8)["DuCuoi"]==0,"full settlement closes invoice exactly");
   Reject(()=>Pay(sale,"SALES_INVOICE","EXTRA",9,0.0001m),"settled invoice cannot receive extra payment");
   Check(c.Report(u,day,day.AddYears(1),"KHDEMO002","receivables").Rows.Count==0,"partner isolation");
   Reject(()=>c.Report(u,day,day.AddYears(1),"NCCDEMO001","receivables"),"wrong report partner rejected");
   Reject(()=>c.Report(u,day.AddDays(2),day,"","payables"),"invalid range rejected");
   Check(Closing()==stockBefore && Total("revenue")==revenue,"payments do not change inventory or revenue");
   decimal independent=Convert.ToDecimal(SQL("SELECT (SELECT SUM(d.ThanhTien) FROM dbo.CTChungTuThuongMai d JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=d.ChungTuId WHERE h.MaDonVi=@u AND h.Loai='PURCHASE_INVOICE' AND h.TrangThai='POSTED')-(SELECT SUM(p.SoTien) FROM dbo.ThanhToanHoaDon p JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=p.ChungTuId WHERE h.MaDonVi=@u AND h.Loai='PURCHASE_INVOICE')"));
   Check(independent==(decimal)Debt("payables",0,10)["DuCuoi"],"AP independent SQL reconciliation");
   Check((decimal)stock.Report("DVDEMO",day,day.AddYears(1),"HANG001","T").Rows[0]["TonCuoi"]==130,"HANG001 preserved");
  } finally {
   SQL("DELETE p FROM dbo.ThanhToanHoaDon p JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=p.ChungTuId WHERE h.MaDonVi=@u; UPDATE dbo.ChungTuThuongMai SET ChungTuNhapMuaId=NULL WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuKho d JOIN dbo.ChungTuKho h ON h.ChungTuKhoId=d.ChungTuKhoId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuKho WHERE MaDonVi=@u; DELETE d FROM dbo.CTChungTuThuongMai d JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=d.ChungTuId WHERE h.MaDonVi=@u; DELETE dbo.ChungTuThuongMai WHERE MaDonVi=@u; DELETE dbo.TonDauKy WHERE MaDonVi=@u; DELETE dbo.Kho WHERE MaDonVi=@u; DELETE dbo.DonVi WHERE MaDonVi=@u; DELETE dbo.HangHoa WHERE MaHang=@i;");
  }
  Console.WriteLine("PHASE 6 SERVICE CHECKS PASSED; fixtures removed.");
 }
}
