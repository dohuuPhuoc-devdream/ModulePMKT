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
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase8Checks.exe /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll /r:System.Data.dll /r:System.Core.dll /r:System.Web.dll /r:System.Web.Abstractions.dll /r:System.IO.Compression.dll tests\Phase8Checks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    Copy-Item -LiteralPath 'src\ReportingModule.Web\Web.config' -Destination 'src\ReportingModule.Web\bin\Phase8Checks.exe.config' -Force
    & '.\src\ReportingModule.Web\bin\Phase8Checks.exe' $ConnectionString
    if ($LASTEXITCODE -ne 0) { throw 'Commercial service tests failed.' }
    & $compiler /nologo /out:src\ReportingModule.Web\bin\Phase8WebChecks.exe /r:System.Web.dll /r:System.Core.dll /r:System.Data.dll /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll tests\PhaseTestAuth.cs tests\Phase8WebChecks.cs
    if ($LASTEXITCODE -ne 0) { throw 'Web test compilation failed.' }
    # Uses local demo configuration; removes isolated fixtures.
    & '.\src\ReportingModule.Web\bin\Phase8WebChecks.exe' '.\src\ReportingModule.Web'
    if ($LASTEXITCODE -ne 0) { throw 'Web Forms acceptance failed.' }
    $bundledPython=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    $pythonExe=if(Test-Path -LiteralPath $bundledPython){$bundledPython}else{(Get-Command python -ErrorAction Stop).Source}
    & $pythonExe 'tests\verify-phase8.py'
    if ($LASTEXITCODE -ne 0) { throw 'Independent PDF/Excel output checks failed.' }
} finally {
    if ($manageTestIdentity) { & '.\tests\set-test-identity.ps1' -Action Disable -ConnectionString $ConnectionString }
    foreach ($file in @('Phase8Checks.exe','Phase8Checks.exe.config','Phase8WebChecks.exe','Phase8WebChecks.exe.config')) {
        $target = Join-Path $root ('src\ReportingModule.Web\bin\' + $file)
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    Pop-Location
}


