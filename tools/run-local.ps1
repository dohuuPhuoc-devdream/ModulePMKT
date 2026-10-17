param([ValidateRange(1024,65535)][int]$Port=8080)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$hostExe=Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'
$msbuild=Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (!(Test-Path -LiteralPath $hostExe)) { throw 'IIS Express is not installed.' }
if (!(Test-Path -LiteralPath $msbuild)) { throw 'Visual Studio 2022 Community MSBuild was not found.' }
& $msbuild (Join-Path $root 'ReportingModule.sln') /t:Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Open http://localhost:$Port/Default.aspx . Keep this terminal running; press Q to stop IIS Express."
& $hostExe "/path:$(Join-Path $root 'src\ReportingModule.Web')" "/port:$Port" /clr:v4.0 /systray:false
if ($LASTEXITCODE -ne 0) { throw "IIS Express exited with code $LASTEXITCODE" }
