using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ReportingModule.Web;

class Phase10Checks
{
 static readonly Guid Admin=new Guid("10000000-0000-4000-8000-000000000010");
 static readonly string TestUsername="P10_"+Guid.NewGuid().ToString("N").Substring(0,20);
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
 static void Reject(Action work,string name){try{work();}catch(ArgumentException){Console.WriteLine("PASS: "+name);return;}throw new Exception(name);}
 static void Main(string[] args)
 {
  string cs=args[0];var random=Guid.NewGuid().ToString("N")+"aB3!";
  byte[] salt=PasswordHash.NewSalt(),hash=PasswordHash.Create(random,salt);
  Check(PasswordHash.Iterations==310000&&PasswordHash.Version==1,"password hash parameters are versioned");
  Check(salt.Length==16&&hash.Length==32&&PasswordHash.Verify(random,salt,hash,310000,1),"PBKDF2-HMAC-SHA256 accepts matching password");
  Check(!PasswordHash.Verify(random+"x",salt,hash,310000,1),"password hash rejects mismatched password");
  Reject(()=>PasswordHash.Create("short",salt),"short password policy enforced");
  decimal parsed;
  Check(NumberParser.TryParse("15.000",out parsed)&&parsed==15000m,"Vietnamese thousands separator remains a full amount");
  Check(NumberParser.TryParse("1,5",out parsed)&&parsed==1.5m,"Vietnamese decimal comma parses fractions");
  Check(NumberParser.TryParse("123.4567",out parsed)&&parsed==123.4567m,"four-decimal legacy prices remain exact");
  Check(!NumberParser.TryParse("15.00",out parsed),"ambiguous dot decimals are rejected instead of silently changing value");

  var admin=new AdminStore(cs);
  string testPassword=Guid.NewGuid().ToString("N")+"aB3!";
  Guid userId=Guid.Empty;
  try
  {
   admin.SaveUser(null,TestUsername,"Tài khoản kiểm thử",true,testPassword,new[]{"User"},new[]{"DVDEMO"},Admin);
   using(var c=new SqlConnection(cs))using(var cmd=new SqlCommand("SELECT UserId FROM dbo.AppUser WHERE NormalizedUsername=@n",c)){cmd.Parameters.Add("@n",SqlDbType.NVarChar,64).Value=IdentityStore.NormalizeUsername(TestUsername);c.Open();userId=(Guid)cmd.ExecuteScalar();}
   var login=IdentityStore.Authenticate(TestUsername.ToLowerInvariant(),testPassword);
   Check(login.User!=null&&login.User.Id==userId,"normalized username authenticates");
   Check(IdentityStore.Get(userId).MustChangePassword,"newly provisioned account is required to change its initial password");
   Check(admin.UserRoles(userId).SequenceEqual(new[]{"User"})&&admin.UserUnits(userId).SequenceEqual(new[]{"DVDEMO"}),"admin user roles and unit assignments load without reopening connections");
   Check(admin.RolePermissions(Convert.ToInt32(admin.Roles().Rows.Cast<DataRow>().Single(r=>(string)r["RoleCode"]=="User")["RoleId"])).Length>0,"admin role permission matrix loads without reopening connections");
   Check(PermissionService.IsAllowed(userId,"SALES.SALES_INVOICE.VIEW")&&!PermissionService.IsAllowed(userId,"SYSTEM.ADMIN_USERS.ADMIN"),"safe User role excludes administration");
   Check(UnitAccess.IsAllowed(userId,"DVDEMO")&&!UnitAccess.IsAllowed(userId,"NOT_ASSIGNED"),"unit scope follows assigned units");
   Check(!IdentityStore.ChangePassword(userId,"wrong-current-password",testPassword),"wrong current password is rejected");
   string changedPassword=Guid.NewGuid().ToString("N")+"aB3!";
   Check(IdentityStore.ChangePassword(userId,testPassword,changedPassword),"password change opens connection before its transaction");
   Check(!IdentityStore.Get(userId).MustChangePassword,"password change clears the required-change flag");
   Check(IdentityStore.Authenticate(TestUsername,changedPassword).User!=null,"new password authenticates after change");
   testPassword=changedPassword;

   var permissions=admin.Permissions().Rows.Cast<DataRow>().Select(r=>Convert.ToString(r["PermissionCode"])).ToArray();
   Check(permissions.Contains("SYSTEM.UNIT.CREATE"),"stable allowlisted unit permission exists");
   admin.SaveRole(null,"PHASE10_EXTRA","Temporary extra permission",new[]{"SYSTEM.UNIT.CREATE"},Admin);
   DataRow extraRow=admin.Roles().Rows.Cast<DataRow>().Single(r=>(string)r["RoleCode"]=="PHASE10_EXTRA");int extra=Convert.ToInt32(extraRow["RoleId"]);
   admin.SaveUser(userId,TestUsername,"Tài khoản kiểm thử",true,"",new[]{"User","PHASE10_EXTRA"},new[]{"DVDEMO"},Admin);
   Check(PermissionService.IsAllowed(userId,"SYSTEM.UNIT.CREATE"),"multiple enabled roles union effective permissions");
   admin.DisableRole(extra,Admin);
   Check(!PermissionService.IsAllowed(userId,"SYSTEM.UNIT.CREATE"),"disabled role permissions are excluded");
   Reject(()=>admin.SaveRole(null,"P10_ESCALATE","Unauthorized role",new[]{"SYSTEM.UNIT.ADMIN"},userId),"user cannot create a role with permissions they do not hold");
   Reject(()=>admin.SaveUser(userId,TestUsername,"Tài khoản kiểm thử",true,"",new[]{"Administrator"},new[]{"DVDEMO"},userId),"user cannot assign themselves Administrator");

   for(int i=0;i<5;i++)IdentityStore.Authenticate(TestUsername,"wrong-password");
   DataRow state=admin.User(userId);
   Check(Convert.ToInt32(state["FailedLoginCount"])==5&&state["LockedUntilUtc"]!=DBNull.Value,"temporary lock state is stored");
   DateTime originalLock=(DateTime)state["LockedUntilUtc"];System.Threading.Thread.Sleep(1100);
   IdentityStore.Authenticate(TestUsername,"wrong-password");
   Check((DateTime)admin.User(userId)["LockedUntilUtc"]==originalLock,"attempts during a lock do not extend its expiry");
   var blocked=IdentityStore.Authenticate(TestUsername,testPassword);
   Check(blocked.User==null,"five failed logins lock account despite correct password");

   int otherAdmins;
   using(var c=new SqlConnection(cs))using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.AppUser u JOIN dbo.AppUserRole ur ON ur.UserId=u.UserId JOIN dbo.AppRole r ON r.RoleId=ur.RoleId WHERE r.RoleCode='Administrator' AND r.IsEnabled=1 AND u.IsEnabled=1 AND u.UserId<>@id",c)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=Admin;c.Open();otherAdmins=Convert.ToInt32(cmd.ExecuteScalar());}
   bool protectedLastAdmin=false;
   try{admin.SaveUser(Admin,"PHASE10_AUTOMATION","Automated acceptance account",false,"",new[]{"User"},new string[0],Admin);}
   catch(ArgumentException){protectedLastAdmin=true;}
   Check(otherAdmins>0?!protectedLastAdmin:protectedLastAdmin,"last enabled Administrator cannot be disabled or demoted");

   long auditId;using(var c=new SqlConnection(cs))using(var cmd=new SqlCommand("SELECT MAX(AuditId) FROM dbo.AuditLog WHERE ActorUserId=@id",c)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;c.Open();auditId=Convert.ToInt64(cmd.ExecuteScalar());}
   bool immutable=false;try{using(var c=new SqlConnection(cs))using(var cmd=new SqlCommand("DELETE dbo.AuditLog WHERE AuditId=@id",c)){cmd.Parameters.Add("@id",SqlDbType.BigInt).Value=auditId;c.Open();cmd.ExecuteNonQuery();}}catch(SqlException ex){immutable=ex.Number==51603;}
   Check(immutable,"audit data rejects ordinary update/delete attempts");
   Console.WriteLine("PHASE 10 SECURITY CHECKS PASSED");
  }
  finally
  {
   using(var c=new SqlConnection(cs))using(var cmd=new SqlCommand("DELETE dbo.AppUserRole WHERE UserId=@id; DELETE dbo.AppUserUnit WHERE UserId=@id; UPDATE dbo.AppUser SET IsEnabled=0,FailedLoginCount=0,LockedUntilUtc=NULL WHERE UserId=@id; DELETE dbo.AppRolePermission WHERE RoleId IN(SELECT RoleId FROM dbo.AppRole WHERE RoleCode='PHASE10_EXTRA'); DELETE dbo.AppRole WHERE RoleCode='PHASE10_EXTRA';",c))
   {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;c.Open();if(userId!=Guid.Empty)cmd.ExecuteNonQuery();}
  }
 }
}
