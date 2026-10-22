using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace ReportingModule.Web
{
 public sealed class PermissionRow
 {
  public string Code,Module,Function,Action,Name;
 }
 public sealed class AdminStore
 {
  private readonly string connection;
  public AdminStore():this(ConfigurationManager.ConnectionStrings["ReportingModule"].ConnectionString){}
  public AdminStore(string connection){this.connection=connection;}
  private SqlConnection Open(){var c=new SqlConnection(connection);c.Open();return c;}
  public DataTable Users(string search)
  {
   using(var c=Open())using(var cmd=new SqlCommand(@"SELECT u.UserId,u.Username,u.DisplayName,u.IsEnabled,u.FailedLoginCount,u.LockedUntilUtc,u.LastLoginAtUtc,
    COALESCE(STUFF((SELECT N', '+r.RoleName FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId WHERE ur.UserId=u.UserId ORDER BY r.RoleName FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,2,N''),N'') AS Roles
    FROM dbo.AppUser u WHERE (@q=N'' OR u.Username LIKE @like ESCAPE '~' OR u.DisplayName LIKE @like ESCAPE '~') ORDER BY u.NormalizedUsername",c))
   {string q=(search??"").Trim().Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[");cmd.Parameters.Add("@q",SqlDbType.NVarChar,64).Value=(search??"").Trim();cmd.Parameters.Add("@like",SqlDbType.NVarChar,130).Value="%"+q+"%";var t=new DataTable();using(var a=new SqlDataAdapter(cmd))a.Fill(t);return t;}
  }
  public DataRow User(Guid id)
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT UserId,Username,DisplayName,IsEnabled,FailedLoginCount,LockedUntilUtc,LastLoginAtUtc FROM dbo.AppUser WHERE UserId=@id",c)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;var t=new DataTable();using(var a=new SqlDataAdapter(cmd))a.Fill(t);return t.Rows.Count==0?null:t.Rows[0];}}
  public DataTable Roles()
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT RoleId,RoleCode,RoleName,IsSystem,IsEnabled FROM dbo.AppRole ORDER BY IsSystem DESC,RoleName",c)){var t=new DataTable();using(var a=new SqlDataAdapter(cmd))a.Fill(t);return t;}}
  public string[] UserRoles(Guid id)
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT r.RoleCode FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId WHERE ur.UserId=@id",c)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;var list=new List<string>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(r.GetString(0));return list.ToArray();}}
  public string[] UserUnits(Guid id)
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT MaDonVi FROM dbo.AppUserUnit WHERE UserId=@id",c)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;var list=new List<string>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(r.GetString(0));return list.ToArray();}}
  public string DefaultUnit(Guid id)
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT MaDonVi FROM dbo.AppUserUnit WHERE UserId=@id AND IsDefault=1",c)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;return Convert.ToString(cmd.ExecuteScalar());}}
  public DataTable Units()
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT MaDonVi,TenDonVi FROM dbo.DonVi ORDER BY MaDonVi",c)){var t=new DataTable();using(var a=new SqlDataAdapter(cmd))a.Fill(t);return t;}}
  public void SaveUser(Guid? id,string username,string displayName,bool enabled,string password,string[] roles,string[] units,Guid actor)
  { SaveUser(id,username,displayName,enabled,password,roles,units,null,actor); }
  public void SaveUser(Guid? id,string username,string displayName,bool enabled,string password,string[] roles,string[] units,string defaultUnit,Guid actor)
  {
   username=(username??"").Trim();string normalized=IdentityStore.NormalizeUsername(username);displayName=(displayName??"").Trim();
   if(displayName.Length<1||displayName.Length>120)throw new ArgumentException("Tên hiển thị là bắt buộc, tối đa 120 ký tự.");
   roles=(roles??new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();units=(units??new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   if(units.Length==1)defaultUnit=units[0];
   if(!string.IsNullOrEmpty(defaultUnit)&&!units.Contains(defaultUnit,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("Đơn vị mặc định phải nằm trong danh sách được phép truy cập.");
   if(units.Length>0&&string.IsNullOrEmpty(defaultUnit))defaultUnit=units[0];
   if(roles.Length==0)throw new ArgumentException("Chọn ít nhất một vai trò.");
   byte[] salt=null,hash=null;if(!string.IsNullOrEmpty(password)){salt=PasswordHash.NewSalt();hash=PasswordHash.Create(password,salt);}if(!id.HasValue&&hash==null)throw new ArgumentException("Mật khẩu ban đầu phải có từ 12 ký tự.");
   using(var c=Open())using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
   {
    using(var lockCmd=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.AppUser WITH(TABLOCKX,HOLDLOCK)",c,tx))lockCmd.ExecuteScalar();
    Guid userId=id??Guid.NewGuid();bool wasAdmin=false;
    if(id.HasValue)
    {
     using(var cmd=new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId WHERE ur.UserId=@id AND r.RoleCode='Administrator' AND r.IsEnabled=1) THEN 1 ELSE 0 END FROM dbo.AppUser WHERE UserId=@id",c,tx))
     {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;object value=cmd.ExecuteScalar();if(value==null)throw new ArgumentException("Người dùng không tồn tại.");wasAdmin=Convert.ToInt32(value)==1;}
    }
    bool willAdmin=roles.Contains("Administrator",StringComparer.OrdinalIgnoreCase);
    // Check the requested roles against the actor's permissions before changing
    // the target user's assignments. This matters when an actor edits their own
    // account: checking after replacement would compare against their new roles.
    foreach(string roleCode in roles)
    using(var cmd=new SqlCommand(@"SELECT COUNT(*) FROM dbo.AppRole targetRole JOIN dbo.AppRolePermission tp ON tp.RoleId=targetRole.RoleId
      WHERE targetRole.RoleCode=@role AND targetRole.IsEnabled=1 AND NOT EXISTS(
       SELECT 1 FROM dbo.AppUserRole actorRole JOIN dbo.AppRole ar ON ar.RoleId=actorRole.RoleId AND ar.IsEnabled=1
       JOIN dbo.AppRolePermission ap ON ap.RoleId=ar.RoleId AND ap.PermissionCode=tp.PermissionCode WHERE actorRole.UserId=@actor)",c,tx))
    {cmd.Parameters.Add("@role",SqlDbType.VarChar,40).Value=roleCode;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;if(Convert.ToInt32(cmd.ExecuteScalar())>0)throw new ArgumentException("Không thể gán vai trò có quyền mà bạn không có.");}
    if((wasAdmin&&!enabled)|| (wasAdmin&&!willAdmin))
    {
     using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.AppUser u JOIN dbo.AppUserRole ur ON ur.UserId=u.UserId JOIN dbo.AppRole r ON r.RoleId=ur.RoleId WHERE u.IsEnabled=1 AND r.IsEnabled=1 AND r.RoleCode='Administrator' AND u.UserId<>@id",c,tx))
     {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;if(Convert.ToInt32(cmd.ExecuteScalar())<1)throw new ArgumentException("Không thể vô hiệu hóa hoặc gỡ vai trò Quản trị viên cuối cùng.");}
    }
    if(id.HasValue)
    {
     using(var cmd=new SqlCommand("UPDATE dbo.AppUser SET Username=@n,NormalizedUsername=@norm,DisplayName=@d,IsEnabled=@enabled,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@actor,FailedLoginCount=CASE WHEN @enabled=0 THEN FailedLoginCount ELSE 0 END,LockedUntilUtc=CASE WHEN @enabled=0 THEN LockedUntilUtc ELSE NULL END WHERE UserId=@id",c,tx))
     {cmd.Parameters.Add("@n",SqlDbType.NVarChar,64).Value=username;cmd.Parameters.Add("@norm",SqlDbType.NVarChar,64).Value=normalized;cmd.Parameters.Add("@d",SqlDbType.NVarChar,120).Value=displayName;cmd.Parameters.Add("@enabled",SqlDbType.Bit).Value=enabled;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;cmd.ExecuteNonQuery();}
     if(hash!=null)using(var cmd=new SqlCommand("UPDATE dbo.AppUser SET PasswordHash=@h,PasswordSalt=@s,HashIterations=@i,HashVersion=@v,MustChangePassword=1,FailedLoginCount=0,LockedUntilUtc=NULL WHERE UserId=@id",c,tx)){AddHash(cmd,hash,salt,userId);cmd.ExecuteNonQuery();}
     using(var cmd=new SqlCommand("DELETE dbo.AppUserRole WHERE UserId=@id; DELETE dbo.AppUserUnit WHERE UserId=@id",c,tx)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;cmd.ExecuteNonQuery();}
    }
    else
    {
     using(var cmd=new SqlCommand("INSERT dbo.AppUser(UserId,Username,NormalizedUsername,PasswordHash,PasswordSalt,HashIterations,HashVersion,DisplayName,IsEnabled,MustChangePassword,CreatedBy) VALUES(@id,@n,@norm,@h,@s,@i,@v,@d,@enabled,1,@actor)",c,tx))
     {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;cmd.Parameters.Add("@n",SqlDbType.NVarChar,64).Value=username;cmd.Parameters.Add("@norm",SqlDbType.NVarChar,64).Value=normalized;AddHash(cmd,hash,salt,userId);cmd.Parameters.Add("@d",SqlDbType.NVarChar,120).Value=displayName;cmd.Parameters.Add("@enabled",SqlDbType.Bit).Value=enabled;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;cmd.ExecuteNonQuery();}
    }
    foreach(string code in roles)using(var cmd=new SqlCommand("INSERT dbo.AppUserRole(UserId,RoleId,AssignedBy) SELECT @u,RoleId,@actor FROM dbo.AppRole WHERE RoleCode=@role AND IsEnabled=1",c,tx)){cmd.Parameters.Add("@u",SqlDbType.UniqueIdentifier).Value=userId;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;cmd.Parameters.Add("@role",SqlDbType.VarChar,40).Value=code;if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("Vai trò không hợp lệ hoặc đã khóa.");}
    foreach(string code in units)using(var cmd=new SqlCommand("INSERT dbo.AppUserUnit(UserId,MaDonVi,AssignedBy,IsDefault) SELECT @u,MaDonVi,@actor,CASE WHEN MaDonVi=@default THEN 1 ELSE 0 END FROM dbo.DonVi WHERE MaDonVi=@unit",c,tx)){cmd.Parameters.Add("@u",SqlDbType.UniqueIdentifier).Value=userId;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;cmd.Parameters.Add("@default",SqlDbType.VarChar,20).Value=(object)defaultUnit??DBNull.Value;cmd.Parameters.Add("@unit",SqlDbType.VarChar,20).Value=code;if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("Đơn vị không hợp lệ.");}
    using(var cmd=new SqlCommand(@"SELECT COUNT(*) FROM dbo.AppUserRole targetRole JOIN dbo.AppRole tr ON tr.RoleId=targetRole.RoleId AND tr.IsEnabled=1
      JOIN dbo.AppRolePermission tp ON tp.RoleId=tr.RoleId WHERE targetRole.UserId=@target AND NOT EXISTS(
       SELECT 1 FROM dbo.AppUserRole actorRole JOIN dbo.AppRole ar ON ar.RoleId=actorRole.RoleId AND ar.IsEnabled=1
       JOIN dbo.AppRolePermission ap ON ap.RoleId=ar.RoleId AND ap.PermissionCode=tp.PermissionCode WHERE actorRole.UserId=@actor)",c,tx))
    {cmd.Parameters.Add("@target",SqlDbType.UniqueIdentifier).Value=userId;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;if(Convert.ToInt32(cmd.ExecuteScalar())>0)throw new ArgumentException("Không thể gán cho tài khoản quyền mà bạn không có.");}
    IdentityStore.WriteAudit(c,tx,actor,ActorName(c,tx,actor),null,"SYSTEM","ADMIN_USERS",id.HasValue?"UPDATE":"CREATE","SUCCESS",id.HasValue?"Cập nhật tài khoản người dùng.":"Tạo tài khoản người dùng.",username);
    tx.Commit();
   }
  }
  private static void AddHash(SqlCommand cmd,byte[] hash,byte[] salt,Guid id)
  {cmd.Parameters.Add("@h",SqlDbType.VarBinary,32).Value=hash;cmd.Parameters.Add("@s",SqlDbType.VarBinary,16).Value=salt;cmd.Parameters.Add("@i",SqlDbType.Int).Value=PasswordHash.Iterations;cmd.Parameters.Add("@v",SqlDbType.SmallInt).Value=PasswordHash.Version;if(!cmd.Parameters.Contains("@id"))cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;}
  public void Unlock(Guid id,Guid actor)
  {using(var c=Open())using(var tx=c.BeginTransaction())using(var cmd=new SqlCommand("UPDATE dbo.AppUser SET FailedLoginCount=0,LockedUntilUtc=NULL,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@actor WHERE UserId=@id; IF @@ROWCOUNT<>1 THROW 51610,'User not found.',1;",c,tx)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;cmd.ExecuteNonQuery();IdentityStore.WriteAudit(c,tx,actor,ActorName(c,tx,actor),null,"SYSTEM","ADMIN_USERS","UNLOCK","SUCCESS","Mở khóa tài khoản.",id.ToString("N"));tx.Commit();}}
  private static string ActorName(SqlConnection c,SqlTransaction tx,Guid actor)
  {using(var cmd=new SqlCommand("SELECT Username FROM dbo.AppUser WHERE UserId=@id",c,tx)){cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=actor;return Convert.ToString(cmd.ExecuteScalar());}}
  public DataTable Permissions()
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT PermissionCode,ModuleCode,FunctionCode,ActionCode,DisplayName,SortOrder FROM dbo.AppPermission ORDER BY ModuleCode,FunctionCode,SortOrder",c)){var t=new DataTable();using(var a=new SqlDataAdapter(cmd))a.Fill(t);return t;}}
  public static string PermissionLabel(string moduleCode,string displayName)
  {
   string module;
   switch(moduleCode)
   {
    case "SYSTEM":module="Hệ thống";break;case "GENERAL":module="Tổng hợp";break;
    case "AR":module="Phải thu";break;case "AP":module="Phải trả";break;
    case "SALES":module="Bán hàng";break;case "PURCHASE":module="Mua hàng";break;
    case "STOCK":module="Tồn kho";break;default:module=moduleCode??"";break;
   }
   return String.IsNullOrEmpty(module)?displayName:module+" · "+(displayName??"");
  }
  public string[] RolePermissions(int role)
  {using(var c=Open())using(var cmd=new SqlCommand("SELECT PermissionCode FROM dbo.AppRolePermission WHERE RoleId=@r",c)){cmd.Parameters.Add("@r",SqlDbType.Int).Value=role;var list=new List<string>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(r.GetString(0));return list.ToArray();}}
  public void SaveRole(int? id,string code,string name,string[] permissions,Guid actor)
  {
   code=(code??"").Trim();name=(name??"").Trim();if(!System.Text.RegularExpressions.Regex.IsMatch(code,@"\A[A-Z][A-Z0-9_]{1,39}\z"))throw new ArgumentException("Mã vai trò chỉ dùng A-Z, số và dấu gạch dưới.");if(name.Length<1||name.Length>100)throw new ArgumentException("Tên vai trò là bắt buộc, tối đa 100 ký tự.");permissions=(permissions??new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   using(var c=Open())using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
   {
    foreach(string permission in permissions)
    using(var access=new SqlCommand(@"SELECT COUNT(*) FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId AND r.IsEnabled=1
      JOIN dbo.AppRolePermission rp ON rp.RoleId=r.RoleId WHERE ur.UserId=@actor AND rp.PermissionCode=@p",c,tx))
    {access.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;access.Parameters.Add("@p",SqlDbType.VarChar,120).Value=permission;if(Convert.ToInt32(access.ExecuteScalar())==0)throw new ArgumentException("Không thể cấp quyền mà bạn không có.");}
    int roleId;
    if(id.HasValue)
    {
     using(var cmd=new SqlCommand("SELECT RoleCode,IsSystem FROM dbo.AppRole WITH(UPDLOCK,HOLDLOCK) WHERE RoleId=@id",c,tx)){cmd.Parameters.Add("@id",SqlDbType.Int).Value=id.Value;using(var r=cmd.ExecuteReader()){if(!r.Read())throw new ArgumentException("Vai trò không tồn tại.");if(r.GetBoolean(1))throw new ArgumentException("Vai trò dựng sẵn không thể thay đổi hoặc vô hiệu hóa.");}}
     roleId=id.Value;using(var cmd=new SqlCommand("UPDATE dbo.AppRole SET RoleCode=@c,RoleName=@n WHERE RoleId=@id",c,tx)){cmd.Parameters.Add("@c",SqlDbType.VarChar,40).Value=code;cmd.Parameters.Add("@n",SqlDbType.NVarChar,100).Value=name;cmd.Parameters.Add("@id",SqlDbType.Int).Value=roleId;cmd.ExecuteNonQuery();}
    }
    else {using(var cmd=new SqlCommand("INSERT dbo.AppRole(RoleCode,RoleName,IsSystem) VALUES(@c,@n,0);SELECT CONVERT(int,SCOPE_IDENTITY())",c,tx)){cmd.Parameters.Add("@c",SqlDbType.VarChar,40).Value=code;cmd.Parameters.Add("@n",SqlDbType.NVarChar,100).Value=name;roleId=(int)cmd.ExecuteScalar();}}
    using(var cmd=new SqlCommand("DELETE dbo.AppRolePermission WHERE RoleId=@id",c,tx)){cmd.Parameters.Add("@id",SqlDbType.Int).Value=roleId;cmd.ExecuteNonQuery();}
    foreach(string p in permissions)using(var cmd=new SqlCommand("INSERT dbo.AppRolePermission(RoleId,PermissionCode) SELECT @r,PermissionCode FROM dbo.AppPermission WHERE PermissionCode=@p",c,tx)){cmd.Parameters.Add("@r",SqlDbType.Int).Value=roleId;cmd.Parameters.Add("@p",SqlDbType.VarChar,120).Value=p;if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("Danh sách quyền chứa quyền không hợp lệ.");}
    IdentityStore.WriteAudit(c,tx,actor,ActorName(c,tx,actor),null,"SYSTEM","ADMIN_ROLES",id.HasValue?"UPDATE":"CREATE","SUCCESS","Cập nhật vai trò và phân quyền.",code);tx.Commit();
   }
  }
  public void DisableRole(int id,Guid actor)
  {using(var c=Open())using(var tx=c.BeginTransaction(IsolationLevel.Serializable))using(var cmd=new SqlCommand("UPDATE dbo.AppRole SET IsEnabled=0 WHERE RoleId=@id AND IsSystem=0; IF @@ROWCOUNT<>1 THROW 51611,'Role cannot be disabled.',1;",c,tx)){cmd.Parameters.Add("@id",SqlDbType.Int).Value=id;cmd.ExecuteNonQuery();IdentityStore.WriteAudit(c,tx,actor,ActorName(c,tx,actor),null,"SYSTEM","ADMIN_ROLES","DISABLE","SUCCESS","Vô hiệu hóa vai trò.",id.ToString());tx.Commit();}}
  public DataTable Audit(string from,string to,string user,string module,string action,string outcome)
  {
   using(var c=Open())using(var cmd=new SqlCommand(@"SELECT TOP(1000) a.OccurredAtUtc,a.ActorUsername,a.UnitCode,a.ModuleCode,a.FunctionCode,a.ActionCode,a.EntityIdentifier,a.Outcome,a.SafeSummary FROM dbo.AuditLog a
    WHERE (@from IS NULL OR a.OccurredAtUtc>=@from) AND (@to IS NULL OR a.OccurredAtUtc<DATEADD(day,1,@to)) AND (@user=N'' OR a.ActorUsername LIKE @user)
    AND (@module='' OR a.ModuleCode=@module) AND (@action='' OR a.ActionCode=@action) AND (@outcome='' OR a.Outcome=@outcome) ORDER BY a.AuditId DESC",c))
   {DateTime d;cmd.Parameters.Add("@from",SqlDbType.Date).Value=DateTime.TryParse(from,out d)?(object)d.Date:DBNull.Value;cmd.Parameters.Add("@to",SqlDbType.Date).Value=DateTime.TryParse(to,out d)?(object)d.Date:DBNull.Value;cmd.Parameters.Add("@user",SqlDbType.NVarChar,70).Value=string.IsNullOrWhiteSpace(user)?"":"%"+user.Trim()+"%";cmd.Parameters.Add("@module",SqlDbType.VarChar,30).Value=module??"";cmd.Parameters.Add("@action",SqlDbType.VarChar,30).Value=action??"";cmd.Parameters.Add("@outcome",SqlDbType.VarChar,12).Value=outcome??"";var t=new DataTable();using(var a=new SqlDataAdapter(cmd))a.Fill(t);return t;}
  }
 }
}
