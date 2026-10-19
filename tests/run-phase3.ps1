param(
    [string]$ConnectionString = 'Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $manageTestIdentity = $env:MODULEPMKT_TEST_IDENTITY_PREPARED -ne '1'
    $msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
    $compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $msbuild ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($manageTestIdentity) { & '.\tests\set-test-identity.ps1' -Action Enable -ConnectionString $ConnectionString; if ($LASTEXITCODE -ne 0) { throw 'Test identity setup failed.' } }
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase3Checks.exe /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll /r:System.Data.dll /r:System.Xml.Linq.dll /r:System.IO.Compression.dll tests\Phase3Checks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Service test compilation failed.' }
    & '.\src\ReportingModule.Web\bin\Phase3Checks.exe' $ConnectionString
    if ($LASTEXITCODE -ne 0) { throw 'Service/Excel checks failed.' }
    # Hosted web checks use the application's Web.config and the local demo database.
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase3WebChecks.exe /r:System.Web.dll /r:System.Core.dll /r:System.Data.dll /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll tests\PhaseTestAuth.cs tests\Phase3WebChecks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Web test compilation failed.' }
    & '.\src\ReportingModule.Web\bin\Phase3WebChecks.exe' '.\src\ReportingModule.Web'
    if ($LASTEXITCODE -ne 0) { throw 'Hosted Web Forms checks failed.' }
} finally {
    if ($manageTestIdentity) { & '.\tests\set-test-identity.ps1' -Action Disable -ConnectionString $ConnectionString }
    # Remove only the two generated test executables from the application's bin.
    foreach ($file in @('Phase3Checks.exe','Phase3WebChecks.exe')) {
        $target = Join-Path $root ('src\ReportingModule.Web\bin\' + $file)
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    Pop-Location
}
