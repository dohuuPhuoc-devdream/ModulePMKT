$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
$exe=Join-Path $root 'src\ReportingModule.Web\bin\Phase11Checks.exe'
try {
 & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
 if($LASTEXITCODE -ne 0){throw 'Release build failed.'}
 & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /out:$exe /r:System.Data.dll /r:System.IO.Compression.dll /r:System.Xml.Linq.dll /r:System.Web.dll /r:src\ReportingModule.Web\bin\ReportingModule.Web.dll tests\Phase11Checks.cs
 if($LASTEXITCODE -ne 0){throw 'Phase 11 test compilation failed.'}
 Copy-Item -LiteralPath 'src\ReportingModule.Web\Web.config' -Destination ($exe+'.config') -Force
 & $exe
 if($LASTEXITCODE -ne 0){throw 'Phase 11 checks failed.'}
} finally {
 foreach($file in @($exe,($exe+'.config'))){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
 Pop-Location
}
