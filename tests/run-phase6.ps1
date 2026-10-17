param([string]$ConnectionString = 'Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & '.\tests\set-test-identity.ps1' -Action Enable -ConnectionString $ConnectionString
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase6Checks.exe /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll /r:System.Data.dll /r:System.Core.dll tests\Phase6Checks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & '.\src\ReportingModule.Web\bin\Phase6Checks.exe' $ConnectionString
    if ($LASTEXITCODE -ne 0) { throw 'Commercial service tests failed.' }
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase6WebChecks.exe /r:System.Web.dll /r:System.Core.dll /r:System.Data.dll /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll tests\PhaseTestAuth.cs tests\Phase6WebChecks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Web test compilation failed.' }
    # Uses local demo configuration; removes isolated fixtures.
    & '.\src\ReportingModule.Web\bin\Phase6WebChecks.exe' '.\src\ReportingModule.Web'
    if ($LASTEXITCODE -ne 0) { throw 'Web Forms acceptance failed.' }
} finally {
    & '.\tests\set-test-identity.ps1' -Action Disable -ConnectionString $ConnectionString
    foreach ($file in @('Phase6Checks.exe','Phase6WebChecks.exe')) {
        $target = Join-Path $root ('src\ReportingModule.Web\bin\' + $file)
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    Pop-Location
}


