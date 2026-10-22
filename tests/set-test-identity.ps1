param(
 [ValidateSet('Enable','Disable')][string]$Action='Enable',
 [string]$ConnectionString='Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10'
)
$ErrorActionPreference='Stop'
$id=[Guid]'10000000-0000-4000-8000-000000000010'
$connection=[System.Data.SqlClient.SqlConnection]::new($ConnectionString);$connection.Open()
$tx=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
try {
 $cmd=$connection.CreateCommand();$cmd.Transaction=$tx
 if($Action -eq 'Enable') {
  $raw=New-Object byte[] 48;[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($raw)
  $temporaryPassword=[Convert]::ToBase64String($raw)+'aB3!'
  $salt=New-Object byte[] 16
  $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
  try{$rng.GetBytes($salt)}finally{$rng.Dispose()}
  $kdf=[Security.Cryptography.Rfc2898DeriveBytes]::new($temporaryPassword,$salt,310000,[Security.Cryptography.HashAlgorithmName]::SHA256)
  try{$hash=$kdf.GetBytes(32)}finally{$kdf.Dispose()}
  $username='PHASE10_AUTOMATION';$normalized=$username.Trim().Normalize([Text.NormalizationForm]::FormKC).ToUpperInvariant()
  $cmd.CommandText=@'
IF EXISTS(SELECT 1 FROM dbo.AppUser WHERE UserId<>@id AND NormalizedUsername=@norm) THROW 51630,'Reserved test login conflicts with an application user.',1;
IF EXISTS(SELECT 1 FROM dbo.AppUser WHERE UserId=@id)
 UPDATE dbo.AppUser SET Username=@name,NormalizedUsername=@norm,PasswordHash=@hash,PasswordSalt=@salt,HashIterations=@iterations,HashVersion=@version,DisplayName=N'Automated acceptance account',IsEnabled=1,FailedLoginCount=0,LockedUntilUtc=NULL WHERE UserId=@id;
ELSE
 INSERT dbo.AppUser(UserId,Username,NormalizedUsername,PasswordHash,PasswordSalt,HashIterations,HashVersion,DisplayName,IsEnabled) VALUES(@id,@name,@norm,@hash,@salt,@iterations,@version,N'Automated acceptance account',1);
DELETE dbo.AppUserRole WHERE UserId=@id;
INSERT dbo.AppUserRole(UserId,RoleId) SELECT @id,RoleId FROM dbo.AppRole WHERE RoleCode='Administrator' AND IsEnabled=1;
IF @@ROWCOUNT<>1 THROW 51631,'Administrator role missing.',1;
IF EXISTS(SELECT 1 FROM dbo.DonVi WHERE MaDonVi='DVDEMO') AND NOT EXISTS(SELECT 1 FROM dbo.AppUserUnit WHERE UserId=@id AND MaDonVi='DVDEMO') INSERT dbo.AppUserUnit(UserId,MaDonVi) VALUES(@id,'DVDEMO');
'@
  $null=$cmd.Parameters.Add('@id',[System.Data.SqlDbType]::UniqueIdentifier);$cmd.Parameters['@id'].Value=$id
  $null=$cmd.Parameters.Add('@name',[System.Data.SqlDbType]::NVarChar,64);$cmd.Parameters['@name'].Value=$username
  $null=$cmd.Parameters.Add('@norm',[System.Data.SqlDbType]::NVarChar,64);$cmd.Parameters['@norm'].Value=$normalized
  $null=$cmd.Parameters.Add('@hash',[System.Data.SqlDbType]::VarBinary,32);$cmd.Parameters['@hash'].Value=$hash
  $null=$cmd.Parameters.Add('@salt',[System.Data.SqlDbType]::VarBinary,16);$cmd.Parameters['@salt'].Value=$salt
  $null=$cmd.Parameters.Add('@iterations',[System.Data.SqlDbType]::Int);$cmd.Parameters['@iterations'].Value=310000
  $null=$cmd.Parameters.Add('@version',[System.Data.SqlDbType]::SmallInt);$cmd.Parameters['@version'].Value=1
  $null=$cmd.ExecuteNonQuery();[Array]::Clear($raw,0,$raw.Length);[Array]::Clear($salt,0,$salt.Length);[Array]::Clear($hash,0,$hash.Length);$temporaryPassword=$null
 } else {
  $cmd.CommandText='UPDATE dbo.AppUser SET IsEnabled=0,FailedLoginCount=0,LockedUntilUtc=NULL WHERE UserId=@id; DELETE dbo.AppUserRole WHERE UserId=@id; DELETE dbo.AppUserUnit WHERE UserId=@id;'
  $null=$cmd.Parameters.Add('@id',[System.Data.SqlDbType]::UniqueIdentifier);$cmd.Parameters['@id'].Value=$id
  $null=$cmd.ExecuteNonQuery()
 }
 $tx.Commit()
} catch {$tx.Rollback();throw} finally {$connection.Dispose()}
