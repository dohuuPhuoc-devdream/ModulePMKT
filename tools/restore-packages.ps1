param(
    [string]$NuGetPath,
    [switch]$NoBootstrap,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $root 'ReportingModule.sln'
$packages = Join-Path $root 'packages'
$downloadedNuGet = Join-Path $env:TEMP 'ReportingModule-nuget.exe'

function Find-NuGet {
    if ($NuGetPath -and (Test-Path -LiteralPath $NuGetPath)) { return (Resolve-Path -LiteralPath $NuGetPath).Path }
    $command = Get-Command nuget.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\NuGet.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.exe')
    )
    foreach ($candidate in $candidates) { if (Test-Path -LiteralPath $candidate) { return $candidate } }
    return $null
}

Push-Location $root
try {
    $nuget = Find-NuGet
    if (-not $nuget) {
        if ($NoBootstrap) { throw 'NuGet CLI was not found. Install the Visual Studio NuGet Package Manager or pass -NuGetPath <nuget.exe>.' }
        Write-Host 'Downloading the official NuGet CLI to the temporary directory...'
        Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $downloadedNuGet
        $signature = Get-AuthenticodeSignature -FilePath $downloadedNuGet
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
            Remove-Item -LiteralPath $downloadedNuGet -Force -ErrorAction SilentlyContinue
            throw 'Downloaded NuGet CLI did not have a valid Microsoft signature.'
        }
        $nuget = $downloadedNuGet
    }

    & $nuget restore $solution -PackagesDirectory $packages -NonInteractive -Verbosity normal
    if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed with exit code $LASTEXITCODE." }

    [xml]$config = Get-Content -LiteralPath 'src\ReportingModule.Web\packages.config' -Raw
    foreach ($package in $config.packages.package) {
        $packageDirectory = Join-Path $packages ($package.id + '.' + $package.version)
        if (-not (Test-Path -LiteralPath $packageDirectory)) { throw "Restored package folder is missing: $packageDirectory" }
    }
    $requiredFiles = @(
        'packages\QuestPDF.2026.9.1\lib\netstandard2.0\QuestPDF.dll',
        'packages\QuestPDF.2026.9.1\build\net4\QuestPDF.targets',
        'packages\System.Buffers.4.6.1\lib\net462\System.Buffers.dll',
        'packages\System.Memory.4.6.3\lib\net462\System.Memory.dll',
        'packages\System.Numerics.Vectors.4.6.1\lib\net462\System.Numerics.Vectors.dll',
        'packages\System.Runtime.CompilerServices.Unsafe.6.1.2\lib\net462\System.Runtime.CompilerServices.Unsafe.dll'
    )
    foreach ($file in $requiredFiles) { if (-not (Test-Path -LiteralPath $file)) { throw "Required NuGet restore file is missing: $file" } }
    Write-Host 'PASS: every packages.config dependency and project import has been restored.'

    if (-not $SkipBuild) {
        $msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
        $msbuildPath = if ($msbuild) { $msbuild.Source } else { Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' }
        if (-not (Test-Path -LiteralPath $msbuildPath)) { throw 'Package restore passed, but MSBuild was not found. Run the documented Release build in Visual Studio Developer PowerShell.' }
        & $msbuildPath $solution /t:Build /p:Configuration=Release /nologo /verbosity:minimal
        if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE after restore." }
    }
}
finally { Pop-Location }
