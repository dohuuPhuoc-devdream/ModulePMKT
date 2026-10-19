param([string]$ConnectionString='Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 $manageTestIdentity = $env:MODULEPMKT_TEST_IDENTITY_PREPARED -ne '1'
 & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
 if($LASTEXITCODE -ne 0){throw 'Build failed'}
 if($manageTestIdentity){& '.\tests\set-test-identity.ps1' -Action Enable -ConnectionString $ConnectionString}
 & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /out:src\ReportingModule.Web\bin\Phase9WebChecks.exe /r:System.Web.dll /r:System.Core.dll /r:System.Data.dll /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll tests\PhaseTestAuth.cs tests\Phase9WebChecks.cs
 if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
 & '.\src\ReportingModule.Web\bin\Phase9WebChecks.exe' '.\src\ReportingModule.Web'
 if($LASTEXITCODE -ne 0){throw 'Phase 9 checks failed'}
} finally {
 if($manageTestIdentity){& '.\tests\set-test-identity.ps1' -Action Disable -ConnectionString $ConnectionString}
 $target=Join-Path $root 'src\ReportingModule.Web\bin\Phase9WebChecks.exe'
 if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
 Pop-Location
}
