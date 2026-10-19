param([string]$ConnectionString = 'Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $manageTestIdentity = $env:MODULEPMKT_TEST_IDENTITY_PREPARED -ne '1'
    $compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($manageTestIdentity) { & '.\tests\set-test-identity.ps1' -Action Enable -ConnectionString $ConnectionString }
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase5Checks.exe /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll /r:System.Data.dll /r:System.Core.dll tests\Phase5Checks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & '.\src\ReportingModule.Web\bin\Phase5Checks.exe' $ConnectionString
    if ($LASTEXITCODE -ne 0) { throw 'Commercial service tests failed.' }
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase5WebChecks.exe /r:System.Web.dll /r:System.Core.dll /r:System.Data.dll /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll tests\PhaseTestAuth.cs tests\Phase5WebChecks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Web test compilation failed.' }
    # Uses local demo configuration; removes isolated fixtures.
    & '.\src\ReportingModule.Web\bin\Phase5WebChecks.exe' '.\src\ReportingModule.Web'
    if ($LASTEXITCODE -ne 0) {
        if (Test-Path 'artifacts/phase5/web-last-response.txt') { Get-Content 'artifacts/phase5/web-last-response.txt' }
        try {
            $checkConnection=[System.Data.SqlClient.SqlConnection]::new($ConnectionString);$checkConnection.Open()
            $checkCommand=$checkConnection.CreateCommand();$checkCommand.CommandText="SELECT IsEnabled,MustChangePassword FROM dbo.AppUser WHERE UserId='10000000-0000-4000-8000-000000000010'; SELECT r.RoleCode FROM dbo.AppUserRole ur JOIN dbo.AppRole r ON r.RoleId=ur.RoleId WHERE ur.UserId='10000000-0000-4000-8000-000000000010' AND r.RoleCode='Administrator';"
            $checkReader=$checkCommand.ExecuteReader();if($checkReader.Read()){Write-Output ("Test identity: IsEnabled={0}, MustChangePassword={1}" -f $checkReader.GetBoolean(0),$checkReader.GetBoolean(1))}else{Write-Output 'Test identity: missing'}
            if($checkReader.NextResult() -and $checkReader.Read()){Write-Output 'Test identity: enabled Administrator role'}else{Write-Output 'Test identity: Administrator role not assigned'}
            $checkReader.Dispose();$checkConnection.Dispose()
        } catch { Write-Warning "Could not inspect test identity status: $_" }
        throw 'Web Forms acceptance failed.'
    }
} finally {
    if ($manageTestIdentity) { & '.\tests\set-test-identity.ps1' -Action Disable -ConnectionString $ConnectionString }
    foreach ($file in @('Phase5Checks.exe','Phase5WebChecks.exe')) {
        $target = Join-Path $root ('src\ReportingModule.Web\bin\' + $file)
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    Pop-Location
}

