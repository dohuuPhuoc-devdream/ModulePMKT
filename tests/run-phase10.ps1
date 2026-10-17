param([string]$ConnectionString='Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
$fixture=$false;$testExe=Join-Path $root 'src\ReportingModule.Web\bin\Phase10Checks.exe'
try {
 & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
 if($LASTEXITCODE -ne 0){throw 'Release build failed.'}
 $precompile=Join-Path $root ('artifacts\phase10-precompiled-'+(Get-Date -Format 'yyyyMMddHHmmss'))
 & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p (Join-Path $root 'src\ReportingModule.Web') $precompile
 if($LASTEXITCODE -ne 0){throw 'ASP.NET precompilation failed.'}
 & '.\tests\set-test-identity.ps1' -Action Enable -ConnectionString $ConnectionString
 $fixture=$true
 & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /out:$testExe /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll /r:System.Data.dll /r:System.Core.dll /r:System.Web.dll tests\Phase10Checks.cs
 if($LASTEXITCODE -ne 0){throw 'Phase 10 security test compilation failed.'}
 Copy-Item -LiteralPath 'src\ReportingModule.Web\Web.config' -Destination ($testExe+'.config') -Force
 & $testExe $ConnectionString
 if($LASTEXITCODE -ne 0){throw 'Phase 10 security checks failed.'}
} finally {
 if($fixture){& '.\tests\set-test-identity.ps1' -Action Disable -ConnectionString $ConnectionString}
 foreach($path in @($testExe,($testExe+'.config'))){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
 Pop-Location
}
