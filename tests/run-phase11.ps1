param([switch]$KeepArtifacts)
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
 $bundledPython=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
 $pythonExe=if(Test-Path -LiteralPath $bundledPython){$bundledPython}else{(Get-Command python -ErrorAction Stop).Source}
 & $pythonExe 'tests\verify-phase11-pdf.py'
 if($LASTEXITCODE -ne 0){throw 'QuestPDF searchable-text validation failed.'}
 & $pythonExe 'tests\verify-tt99-accounts.py'
 if($LASTEXITCODE -ne 0){throw 'TT99 source CSV validation failed.'}
} finally {
 foreach($file in @($exe,($exe+'.config'))){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
 if(-not $KeepArtifacts){foreach($name in @('purchase-receipt.pdf','fractional-purchase-receipt.pdf')){$pdf=Join-Path $root ('artifacts\phase11\'+$name);if(Test-Path -LiteralPath $pdf){Remove-Item -LiteralPath $pdf}}}
 Pop-Location
}
