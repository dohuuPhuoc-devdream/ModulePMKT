param(
 [string]$ConnectionString='Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$username=(Read-Host 'Administrator username').Trim()
$displayName=(Read-Host 'Display name').Trim()
$securePassword=Read-Host 'Initial password (minimum 12 characters)' -AsSecureString
$bstr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$password=$null;$salt=$null;$hash=$null;$kdf=$null
try {
 $password=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
 $normalized=$username.Trim().Normalize([Text.NormalizationForm]::FormKC).ToUpperInvariant()
 if($normalized.Length -lt 3 -or $normalized.Length -gt 64 -or $normalized -notmatch '\A[\p{L}\p{Nd}._@-]{3,64}\z') { throw 'Username must be 3-64 characters and use only letters, digits, ., _, - or @.' }
 if($password.Length -lt 12 -or $password.Length -gt 256) { throw 'Password must be 12-256 characters.' }
 $salt=New-Object byte[] 16
 $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
 try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
 $kdf=[Security.Cryptography.Rfc2898DeriveBytes]::new($password,$salt,310000,[Security.Cryptography.HashAlgorithmName]::SHA256)
 $hash=$kdf.GetBytes(32)
 $kdf.Dispose();$kdf=$null
 $userId=[Guid]::NewGuid()
 $connection=[System.Data.SqlClient.SqlConnection]::new($ConnectionString)
 $connection.Open()
 $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
 try {
  $cmd=$connection.CreateCommand();$cmd.Transaction=$transaction
  $cmd.CommandText=@'
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=6)
 THROW 51620,'Run database setup through schema version 6 first.',1;
IF EXISTS(SELECT 1 FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId JOIN dbo.AppUser u ON u.UserId=ur.UserId WHERE r.RoleCode='Administrator' AND r.IsEnabled=1 AND u.IsEnabled=1)
 THROW 51621,'An enabled administrator already exists. Use user administration to add another.',1;
'@
  $null=$cmd.ExecuteNonQuery()
  $cmd=$connection.CreateCommand();$cmd.Transaction=$transaction
  $cmd.CommandText=@'
INSERT dbo.AppUser(UserId,Username,NormalizedUsername,PasswordHash,PasswordSalt,HashIterations,HashVersion,DisplayName,IsEnabled)
VALUES(@id,@name,@norm,@hash,@salt,@iterations,@version,@display,1);
INSERT dbo.AppUserRole(UserId,RoleId)
SELECT @id,RoleId FROM dbo.AppRole WHERE RoleCode='Administrator' AND IsEnabled=1;
IF @@ROWCOUNT<>1 THROW 51622,'Administrator role missing.',1;
INSERT dbo.AuditLog(ActorUserId,ActorUsername,ModuleCode,FunctionCode,ActionCode,Outcome,SafeSummary,EntityIdentifier)
VALUES(@id,@name,'SYSTEM','ADMIN_USERS','BOOTSTRAP','SUCCESS',N'Initial administrator created by the local utility.',@name);
'@
  $null=$cmd.Parameters.Add('@id',[System.Data.SqlDbType]::UniqueIdentifier);$cmd.Parameters['@id'].Value=$userId
  $null=$cmd.Parameters.Add('@name',[System.Data.SqlDbType]::NVarChar,64);$cmd.Parameters['@name'].Value=$username
  $null=$cmd.Parameters.Add('@norm',[System.Data.SqlDbType]::NVarChar,64);$cmd.Parameters['@norm'].Value=$normalized
  $null=$cmd.Parameters.Add('@hash',[System.Data.SqlDbType]::VarBinary,32);$cmd.Parameters['@hash'].Value=$hash
  $null=$cmd.Parameters.Add('@salt',[System.Data.SqlDbType]::VarBinary,16);$cmd.Parameters['@salt'].Value=$salt
  $null=$cmd.Parameters.Add('@iterations',[System.Data.SqlDbType]::Int);$cmd.Parameters['@iterations'].Value=310000
  $null=$cmd.Parameters.Add('@version',[System.Data.SqlDbType]::SmallInt);$cmd.Parameters['@version'].Value=1
  $null=$cmd.Parameters.Add('@display',[System.Data.SqlDbType]::NVarChar,120);$cmd.Parameters['@display'].Value=$displayName
  $null=$cmd.ExecuteNonQuery()
  $transaction.Commit()
 } catch {$transaction.Rollback();throw} finally {$connection.Dispose()}
 Write-Output "Administrator '$username' created. The password was not written to disk or logged."
} finally {
 if($bstr -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)}
 if($kdf){$kdf.Dispose()};if($salt){[Array]::Clear($salt,0,$salt.Length)};if($hash){[Array]::Clear($hash,0,$hash.Length)}
 $password=$null;$securePassword.Dispose()
}
