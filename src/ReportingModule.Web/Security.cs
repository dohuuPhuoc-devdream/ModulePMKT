using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace ReportingModule.Web
{
 public static class AppIdentity
 {
  public static string ProductName { get { return ConfigurationManager.AppSettings["ProductName"]??"Kế toán"; } }
 }

 public sealed class AppUserInfo
 {
  public Guid Id; public string Username,DisplayName; public bool Enabled,MustChangePassword; public DateTime? LockedUntilUtc;
  public string[] Roles=new string[0];
 }

 public static class PasswordHash
 {
  public const int Iterations=310000;
  public const short Version=1;
  private const int SaltLength=16,HashLength=32;
  public static byte[] NewSalt() { var b=new byte[SaltLength]; using(var rng=RandomNumberGenerator.Create()) rng.GetBytes(b); return b; }
  public static byte[] Create(string password,byte[] salt)
  {
   if(string.IsNullOrEmpty(password)||password.Length<12||password.Length>256) throw new ArgumentException("Mật khẩu phải có từ 12 đến 256 ký tự.");
   if(salt==null||salt.Length!=SaltLength) throw new ArgumentException("Muối mật khẩu không hợp lệ.");
   return Pbkdf2Sha256(password,salt,Iterations,HashLength);
  }
  public static bool Verify(string password,byte[] salt,byte[] expected,int iterations,int version)
  {
   if(password==null||salt==null||expected==null||salt.Length!=SaltLength||expected.Length!=HashLength||iterations<100000||iterations>1000000||version!=Version) return false;
   byte[] actual=Pbkdf2Sha256(password,salt,iterations,expected.Length);
   int mismatch=0; for(int i=0;i<actual.Length;i++) mismatch|=actual[i]^expected[i];
   Array.Clear(actual,0,actual.Length); return mismatch==0;
  }
  private static byte[] Pbkdf2Sha256(string password,byte[] salt,int iterations,int length)
  {
   using(var derivation=new Rfc2898DeriveBytes(password,salt,iterations,HashAlgorithmName.SHA256))
   {
    return derivation.GetBytes(length);
   }
  }
 }

 public sealed class AuthenticationResult
 {
  public AppUserInfo User; public string Error;
 }

 public static class IdentityStore
 {
  private static string Connection { get { return ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString; } }
  public static string NormalizeUsername(string value)
  {
   string normalized=(value??"").Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
   if(normalized.Length<3||normalized.Length>64||normalized.Any(c=>!(char.IsLetterOrDigit(c)||c=='.'||c=='_'||c=='-'||c=='@'))) throw new ArgumentException("Tên đăng nhập phải dài 3-64 ký tự và chỉ dùng chữ, số, ., _, - hoặc @.");
   return normalized;
  }
  public static AuthenticationResult Authenticate(string username,string password)
  {
   string normalized;
   try { normalized=NormalizeUsername(username); } catch { normalized=(username??"").Trim().ToUpperInvariant(); }
   using(var c=new SqlConnection(Connection))
   {
    c.Open(); using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
    {
     AppUserInfo user=null; byte[] hash=null,salt=null; int iterations=0,version=0,failures=0; DateTime? locked=null;
     using(var cmd=new SqlCommand("SELECT UserId,Username,DisplayName,PasswordHash,PasswordSalt,HashIterations,HashVersion,FailedLoginCount,LockedUntilUtc,IsEnabled FROM dbo.AppUser WITH(UPDLOCK,HOLDLOCK) WHERE NormalizedUsername=@name",c,tx))
     {
      cmd.Parameters.Add("@name",SqlDbType.NVarChar,64).Value=normalized.Length>64?normalized.Substring(0,64):normalized;
      using(var r=cmd.ExecuteReader()) if(r.Read()) { user=new AppUserInfo { Id=r.GetGuid(0),Username=r.GetString(1),DisplayName=r.GetString(2),Enabled=r.GetBoolean(9) }; hash=(byte[])r[3];salt=(byte[])r[4];iterations=r.GetInt32(5);version=r.GetInt16(6);failures=r.GetInt32(7);locked=r.IsDBNull(8)?(DateTime?)null:r.GetDateTime(8); }
     }
     bool ok=user!=null&&user.Enabled&&(!locked.HasValue||locked.Value<=DateTime.UtcNow)&&PasswordHash.Verify(password,salt,hash,iterations,version);
     if(user==null) PasswordHash.Verify(password??"",new byte[16],new byte[32],PasswordHash.Iterations,PasswordHash.Version);
     if(user!=null)
     {
      if(ok)
      {
       using(var cmd=new SqlCommand("UPDATE dbo.AppUser SET FailedLoginCount=0,LockedUntilUtc=NULL,LastLoginAtUtc=SYSUTCDATETIME() WHERE UserId=@id",c,tx)) { cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=user.Id;cmd.ExecuteNonQuery(); }
       WriteAudit(c,tx,user.Id,user.Username,null,"SYSTEM","AUTHENTICATION","LOGIN","SUCCESS","Đăng nhập thành công.");
      }
      else
      {
       int next=failures>=5?5:failures+1;
       using(var cmd=new SqlCommand("UPDATE dbo.AppUser SET FailedLoginCount=@n,LockedUntilUtc=CASE WHEN @n>=5 THEN CASE WHEN LockedUntilUtc>SYSUTCDATETIME() THEN LockedUntilUtc ELSE DATEADD(minute,15,SYSUTCDATETIME()) END ELSE NULL END WHERE UserId=@id",c,tx)) { cmd.Parameters.Add("@n",SqlDbType.Int).Value=next;cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=user.Id;cmd.ExecuteNonQuery(); }
       WriteAudit(c,tx,user.Id,user.Username,null,"SYSTEM","AUTHENTICATION","LOGIN","FAILURE","Đăng nhập không thành công.");
      }
     }
     else WriteAudit(c,tx,null,SafeAuditName(username),null,"SYSTEM","AUTHENTICATION","LOGIN","FAILURE","Đăng nhập không thành công.");
     tx.Commit();
     if(ok) { user.Roles=Roles(user.Id); return new AuthenticationResult { User=user }; }
    }
   }
   return new AuthenticationResult { Error="Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa." };
  }
  private static string SafeAuditName(string value) { value=(value??"").Trim(); return value.Length>64?value.Substring(0,64):value; }
  public static AppUserInfo Get(Guid id)
  {
   using(var c=new SqlConnection(Connection)) using(var cmd=new SqlCommand("SELECT UserId,Username,DisplayName,IsEnabled,LockedUntilUtc,MustChangePassword FROM dbo.AppUser WHERE UserId=@id",c))
   { cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;c.Open();using(var r=cmd.ExecuteReader()) if(r.Read()) { var x=new AppUserInfo{Id=r.GetGuid(0),Username=r.GetString(1),DisplayName=r.GetString(2),Enabled=r.GetBoolean(3),LockedUntilUtc=r.IsDBNull(4)?(DateTime?)null:r.GetDateTime(4),MustChangePassword=r.GetBoolean(5)};x.Roles=Roles(id);return x; } }
   return null;
  }
  public static bool ChangePassword(Guid id,string currentPassword,string newPassword)
  {
   byte[] salt=PasswordHash.NewSalt(),hash=PasswordHash.Create(newPassword,salt);
   using(var c=new SqlConnection(Connection))
   {
    c.Open(); using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
    {
    byte[] oldHash,oldSalt;int iterations,version;string username;
    using(var cmd=new SqlCommand("SELECT Username,PasswordHash,PasswordSalt,HashIterations,HashVersion FROM dbo.AppUser WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@id AND IsEnabled=1",c,tx))
    {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;using(var r=cmd.ExecuteReader()){if(!r.Read())return false;username=r.GetString(0);oldHash=(byte[])r[1];oldSalt=(byte[])r[2];iterations=r.GetInt32(3);version=r.GetInt16(4);}}
    if(!PasswordHash.Verify(currentPassword,oldSalt,oldHash,iterations,version)){tx.Rollback();return false;}
    using(var cmd=new SqlCommand("UPDATE dbo.AppUser SET PasswordHash=@h,PasswordSalt=@s,HashIterations=@i,HashVersion=@v,MustChangePassword=0,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@id WHERE UserId=@id",c,tx))
    {cmd.Parameters.Add("@h",SqlDbType.VarBinary,32).Value=hash;cmd.Parameters.Add("@s",SqlDbType.VarBinary,16).Value=salt;cmd.Parameters.Add("@i",SqlDbType.Int).Value=PasswordHash.Iterations;cmd.Parameters.Add("@v",SqlDbType.SmallInt).Value=PasswordHash.Version;cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;cmd.ExecuteNonQuery();}
    WriteAudit(c,tx,id,username,null,"SYSTEM","AUTHENTICATION","PASSWORD_CHANGE","SUCCESS","Người dùng đổi mật khẩu.");tx.Commit();return true;
    }
   }
  }
  public static string[] Roles(Guid id)
  {
   using(var c=new SqlConnection(Connection)) using(var cmd=new SqlCommand("SELECT r.RoleName FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId WHERE ur.UserId=@id AND r.IsEnabled=1 ORDER BY r.RoleName",c))
   {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;c.Open();var list=new List<string>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(r.GetString(0));return list.ToArray();}
  }
  internal static void WriteAudit(SqlConnection c,SqlTransaction tx,Guid? user,string username,string unit,string module,string function,string action,string outcome,string summary,string entity=null)
  {
   using(var cmd=new SqlCommand("INSERT dbo.AuditLog(ActorUserId,ActorUsername,UnitCode,ModuleCode,FunctionCode,ActionCode,Outcome,SafeSummary,EntityIdentifier) VALUES(@u,@n,@unit,@m,@f,@a,@o,@s,@e)",c,tx))
   {
    cmd.Parameters.Add("@u",SqlDbType.UniqueIdentifier).Value=(object)user??DBNull.Value;
    cmd.Parameters.Add("@n",SqlDbType.NVarChar,64).Value=(object)SafeAuditName(username)??"(không xác định)";
    cmd.Parameters.Add("@unit",SqlDbType.VarChar,20).Value=(object)unit??DBNull.Value;
    cmd.Parameters.Add("@m",SqlDbType.VarChar,30).Value=module;cmd.Parameters.Add("@f",SqlDbType.VarChar,50).Value=function;cmd.Parameters.Add("@a",SqlDbType.VarChar,30).Value=action;cmd.Parameters.Add("@o",SqlDbType.VarChar,12).Value=outcome;
    cmd.Parameters.Add("@s",SqlDbType.NVarChar,500).Value=(summary??"").Length>500?summary.Substring(0,500):(summary??"");cmd.Parameters.Add("@e",SqlDbType.NVarChar,150).Value=(object)((entity??"").Length>150?(entity??"").Substring(0,150):entity)??DBNull.Value;cmd.ExecuteNonQuery();
   }
  }
  public static void Audit(string module,string function,string action,string outcome,string summary,string unit=null,string entity=null)
  {
   var context=HttpContext.Current; Guid? id=CurrentIdentity.CurrentUserId(); string username=context!=null&&context.User!=null?context.User.Identity.Name:"(system)";
   if(id.HasValue) { var u=Get(id.Value); if(u!=null) username=u.Username; }
   using(var c=new SqlConnection(Connection)) { c.Open();WriteAudit(c,null,id,username,unit,module,function,action,outcome,summary,entity); }
  }
 }

 public static class PermissionService
 {
  private static string Connection { get { return ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString; } }
  public static bool IsAllowed(Guid user,string permission)
  {
   var key=HttpContext.Current==null?null:HttpContext.Current.Items["EffectivePermissions:"+user.ToString("N")] as HashSet<string>;
   if(key==null)
   {
    key=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using(var c=new SqlConnection(Connection)) using(var cmd=new SqlCommand("SELECT DISTINCT rp.PermissionCode FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId AND r.IsEnabled=1 JOIN dbo.AppRolePermission rp ON rp.RoleId=r.RoleId JOIN dbo.AppUser u ON u.UserId=ur.UserId AND u.IsEnabled=1 WHERE ur.UserId=@id",c))
    {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=user;c.Open();using(var r=cmd.ExecuteReader())while(r.Read())key.Add(r.GetString(0));}
    if(HttpContext.Current!=null) HttpContext.Current.Items["EffectivePermissions:"+user.ToString("N")]=key;
   }
   return key.Contains(permission);
  }
  public static void Demand(Guid user,string permission)
  { if(!IsAllowed(user,permission)) { var p=permission.Split('.');IdentityStore.Audit(p.Length>0?p[0]:"SYSTEM",p.Length>1?p[1]:"ACCESS",p.Length>2?p[2]:"ACCESS","DENIED","Yêu cầu bị từ chối do thiếu quyền.");throw new HttpException(403,"Bạn không có quyền thực hiện chức năng này."); } }
 }

 public static class PermissionMap
 {
  private static readonly Dictionary<string,string> Functions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
   {"general","GENERAL"},{"units","UNIT"},{"journal","JOURNAL"},{"balance","BALANCE"},{"ar-invoice","AR_INVOICE"},{"customers","CUSTOMERS"},{"receivables","RECEIVABLES"},{"ap-invoice","AP_INVOICE"},{"suppliers","SUPPLIERS"},{"payables","PAYABLES"},{"sales-order","SALES_ORDER"},{"sales-invoice","SALES_INVOICE"},{"sales-detail","SALES_DETAIL"},{"revenue","REVENUE"},{"sales-analysis","SALES_ANALYSIS"},{"purchase-order","PURCHASE_ORDER"},{"purchase-receipt","PURCHASE_RECEIPT"},{"purchase-detail","PURCHASE_DETAIL"},{"purchase-analysis","PURCHASE_ANALYSIS"},{"stock-receipt","STOCK_RECEIPT"},{"stock-issue","STOCK_ISSUE"},{"items","ITEMS"},{"inbound","INBOUND"},{"outbound","OUTBOUND"},{"inventory","INVENTORY"},
   {"admin-users","ADMIN_USERS"},{"admin-roles","ADMIN_ROLES"},{"admin-audit","ADMIN_AUDIT"}
  };
  public static string Function(string screen) { string f;return Functions.TryGetValue(screen??"",out f)?f:"GENERAL"; }
  public static string Module(string screen)
  { if(new[]{"general","units","admin-users","admin-roles","admin-audit"}.Contains(screen))return "SYSTEM";if(new[]{"journal","balance"}.Contains(screen))return "GENERAL";if(new[]{"ar-invoice","customers","receivables"}.Contains(screen))return "AR";if(new[]{"ap-invoice","suppliers","payables"}.Contains(screen))return "AP";if(screen.StartsWith("sales")||screen=="revenue")return "SALES";if(screen.StartsWith("purchase"))return "PURCHASE";return "STOCK"; }
  public static string View(string screen) { string f=Function(screen);return Module(screen)+"."+f+(f.StartsWith("ADMIN_")?".ADMIN":".VIEW"); }
  public static string Action(string screen,string id,bool report,bool editing=false)
  {
   string f=Function(screen),a;
   string module=Module(screen),baseCode=module+"."+f+".";
   if(id=="Delete")return baseCode+(new[]{"units","customers","suppliers","items"}.Contains(screen)?"DELETE":"DELETE_DRAFT");
   if(id=="Template")return baseCode+"IMPORT";
   if(id=="Save"||id=="SaveDraft")return baseCode+(editing?"EDIT":"CREATE");
   switch(id) { case "New": return baseCode+"CREATE"; case "Edit": return baseCode+"EDIT";case "Post":return baseCode+"POST";case "Import":case "CommitImport":return baseCode+"IMPORT";case "Export":case "ExcelOutput":return baseCode+"EXPORT";case "Output":case "PdfOutput":return baseCode+"PRINT";case "RecordPayment":return baseCode+"PAYMENT";default:a="VIEW";return baseCode+a; }
  }
 }

 public static class CurrentIdentity
 {
  public static Guid? CurrentUserId()
  {
   var c=HttpContext.Current; if(c==null||c.User==null||c.User.Identity==null||!c.User.Identity.IsAuthenticated)return null;
   Guid id;return Guid.TryParse(c.User.Identity.Name,out id)?(Guid?)id:null;
  }
  public static AppUserInfo Current { get { var id=CurrentUserId();return id.HasValue?IdentityStore.Get(id.Value):null; } }
 }

 public static class UnitAccess
 {
  public static string PreferredUnit(Guid user)
  {
   using(var c=new SqlConnection(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString))using(var cmd=new SqlCommand(@"SELECT TOP(1) a.MaDonVi FROM dbo.AppUserUnit a
    WHERE a.UserId=@id ORDER BY a.IsDefault DESC,a.MaDonVi",c))
   {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=user;c.Open();object value=cmd.ExecuteScalar();if(value!=null&&value!=DBNull.Value)return Convert.ToString(value);}
   return Items(user).Select(x=>x.Value).FirstOrDefault()??"";
  }
  public static bool IsAllowed(Guid user,string unit)
  {
   if(string.IsNullOrWhiteSpace(unit))return false;
   if(PermissionService.IsAllowed(user,"SYSTEM.UNIT.ADMIN"))return true;
   using(var c=new SqlConnection(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString)) using(var cmd=new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.AppUserUnit WHERE UserId=@id AND MaDonVi=@u) THEN 1 ELSE 0 END",c))
   {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=user;cmd.Parameters.Add("@u",SqlDbType.VarChar,20).Value=unit;c.Open();return Convert.ToInt32(cmd.ExecuteScalar())==1;}
  }
  public static void Demand(string unit)
  {
   var id=CurrentIdentity.CurrentUserId();
   if(!id.HasValue) { if(HttpContext.Current==null)return; throw new HttpException(401,"Yêu cầu đăng nhập để truy cập đơn vị."); }
   if(!IsAllowed(id.Value,unit)) { IdentityStore.Audit("SYSTEM","UNIT","ACCESS","DENIED","Truy cập đơn vị không được gán.",unit);throw new HttpException(403,"Bạn không được cấp quyền cho đơn vị đang chọn."); }
  }
  public static ListItem[] Items(Guid user)
  {
   string sql=PermissionService.IsAllowed(user,"SYSTEM.UNIT.ADMIN")?"SELECT MaDonVi,TenDonVi FROM dbo.DonVi ORDER BY MaDonVi":"SELECT d.MaDonVi,d.TenDonVi FROM dbo.DonVi d JOIN dbo.AppUserUnit a ON a.MaDonVi=d.MaDonVi WHERE a.UserId=@id ORDER BY d.MaDonVi";
   using(var c=new SqlConnection(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString))using(var cmd=new SqlCommand(sql,c)) {if(sql.Contains("@id"))cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=user;c.Open();var list=new List<ListItem>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(new ListItem(r.GetString(1),r.GetString(0)));return list.ToArray();}
  }
  public static DataTable Table(Guid user)
  {
   string sql=PermissionService.IsAllowed(user,"SYSTEM.UNIT.ADMIN")?"SELECT MaDonVi,TenDonVi,NgayBatDau FROM dbo.DonVi ORDER BY MaDonVi":"SELECT d.MaDonVi,d.TenDonVi,d.NgayBatDau FROM dbo.DonVi d JOIN dbo.AppUserUnit a ON a.MaDonVi=d.MaDonVi WHERE a.UserId=@id ORDER BY d.MaDonVi";
   using(var c=new SqlConnection(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString))using(var cmd=new SqlCommand(sql,c)){if(sql.Contains("@id"))cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=user;c.Open();var t=new DataTable();using(var da=new SqlDataAdapter(cmd))da.Fill(t);return t;}
  }
  public static DataTable CurrentTable()
  {
   var id=CurrentIdentity.CurrentUserId();
   if(id.HasValue)return Table(id.Value);
   if(HttpContext.Current==null)return null;
   using(var c=new SqlConnection(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString))using(var da=new SqlDataAdapter("SELECT MaDonVi,TenDonVi,NgayBatDau FROM dbo.DonVi WHERE 1=0",c)){var t=new DataTable();da.Fill(t);return t;}
  }
 }

 public class AppPage : Page
 {
  protected Guid UserId;
  protected AppUserInfo SignedInUser { get { return IdentityStore.Get(UserId); } }
  protected override void OnInit(EventArgs e)
  {
   base.OnInit(e);Response.Cache.SetCacheability(HttpCacheability.NoCache);Response.Cache.SetNoStore();
   Guid? id=CurrentIdentity.CurrentUserId();
   if(!id.HasValue) { string url=Request.RawUrl??"Default.aspx"; if(!IsLocalUrl(url))url="Default.aspx";Response.Redirect("Login.aspx?returnUrl="+HttpUtility.UrlEncode(url),true);return; }
   UserId=id.Value; var who=IdentityStore.Get(UserId); if(who==null||!who.Enabled) { FormsAuthentication.SignOut();Session.Clear();Session.Abandon();Response.Redirect("Login.aspx",true);return; }
   if(who.MustChangePassword&&!string.Equals(Request.AppRelativeCurrentExecutionFilePath,"~/ChangePassword.aspx",StringComparison.OrdinalIgnoreCase)) { Response.Redirect("ChangePassword.aspx?required=1",true);return; }
   object last=Session["LastActivityUtc"]; if(last is DateTime&&DateTime.UtcNow-((DateTime)last)>TimeSpan.FromMinutes(20)) {FormsAuthentication.SignOut();Session.Clear();Session.Abandon();Response.Redirect("Login.aspx?expired=1",true);return;}
   Session["LastActivityUtc"]=DateTime.UtcNow;if(Session["CsrfToken"]==null)Session["CsrfToken"]=Guid.NewGuid().ToString("N");ViewStateUserKey=Session.SessionID+":"+UserId.ToString("N");
   string screen=Request.QueryString["screen"];
   if(!string.IsNullOrEmpty(screen))
   {
    string module;if(Navigation.Find(screen,out module)==null)throw new HttpException(404,"Chức năng không tồn tại.");
    RequirePermission(PermissionMap.View(screen));
   }
  }
  protected override void OnLoad(EventArgs e)
  {
   base.OnLoad(e);
   if(IsPostBack)
   {
    string expected=Convert.ToString(Session["CsrfToken"]),actual=Request.Form["ctl00$CsrfToken"];
    if(string.IsNullOrEmpty(expected)||!FixedTextEquals(expected,actual))throw new HttpException(400,"Yêu cầu không hợp lệ. Hãy tải lại trang rồi thử lại.");
   }
  }
  private static bool FixedTextEquals(string a,string b)
  {if(a==null||b==null)return false;int diff=a.Length^b.Length;for(int i=0;i<Math.Min(a.Length,b.Length);i++)diff|=a[i]^b[i];return diff==0;}
  protected bool Can(string permission) { return PermissionService.IsAllowed(UserId,permission); }
  protected void RequirePermission(string permission) { PermissionService.Demand(UserId,permission); }
  protected void RunAuthorized(string screen,string action,Action work,string unit=null,string entity=null,bool editing=false)
  {
   string permission=PermissionMap.Action(screen,action,false,editing);
   if(!string.IsNullOrEmpty(unit))UnitAccess.Demand(unit);
   if(action=="Post")RequirePermission(PermissionMap.Action(screen,editing?"SaveDraft":"New",false,editing));
   RequirePermission(permission);
   try { work(); }
   catch(Exception) { try{IdentityStore.Audit(ModuleFor(screen),PermissionMap.Function(screen),ActionCode(action),"FAILURE","Thao tác nghiệp vụ không hoàn tất.",unit,entity);}catch(Exception auditError){Log.Error("Audit","Failed to record a business operation failure",auditError);} throw; }
   try { IdentityStore.Audit(ModuleFor(screen),PermissionMap.Function(screen),ActionCode(action),"SUCCESS","Thao tác nghiệp vụ đã hoàn tất.",unit,entity); }
   catch(Exception auditError) { Log.Error("Audit","Business operation succeeded but its audit record failed",auditError); }
  }
  protected static string ActionCode(string id)
  { switch(id){case "New":case "Save":case "SaveDraft":return "CREATE_OR_EDIT";case "Edit":return "EDIT";case "Delete":return "DELETE_DRAFT";case "Post":return "POST";case "Import":case "CommitImport":return "IMPORT";case "Export":return "EXPORT";case "Template":return "TEMPLATE";case "Output":case "ExcelOutput":case "PdfOutput":return "OUTPUT";case "RecordPayment":return "PAYMENT";default:return "VIEW";} }
  protected static string ModuleFor(string screen)
  { if(new[]{"general","units","admin-users","admin-roles","admin-audit"}.Contains(screen))return "SYSTEM";if(new[]{"journal","balance"}.Contains(screen))return "GENERAL";if(new[]{"ar-invoice","customers","receivables"}.Contains(screen))return "AR";if(new[]{"ap-invoice","suppliers","payables"}.Contains(screen))return "AP";if(screen.StartsWith("sales" )||screen=="revenue")return "SALES";if(screen.StartsWith("purchase"))return "PURCHASE";return "STOCK"; }
  public static bool IsLocalUrl(string url) { return !string.IsNullOrEmpty(url)&&url[0]=='/'&&(url.Length==1||(url[1]!='/'&&url[1]!='\\'))&&!url.Contains("\\")||(!string.IsNullOrEmpty(url)&&url[0]!='/'&&url[0]!='\\'&&!url.Contains(":")&&!url.StartsWith("//")); }
 }

 public class LoginPageBase : Page
 {
  protected override void OnInit(EventArgs e){base.OnInit(e);if(Session["CsrfToken"]==null)Session["CsrfToken"]=Guid.NewGuid().ToString("N");ViewStateUserKey=Session.SessionID+":login";}
  protected override void OnLoad(EventArgs e){base.OnLoad(e);if(IsPostBack){string a=Convert.ToString(Session["CsrfToken"]),b=Request.Form["Csrf"];if(string.IsNullOrEmpty(a)||a!=b)throw new HttpException(400,"Yêu cầu không hợp lệ. Hãy tải lại trang.");}}
 }
}
