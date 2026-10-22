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

    $projectDirectory = Join-Path $root 'src\ReportingModule.Web'
    [xml]$config = Get-Content -LiteralPath (Join-Path $projectDirectory 'packages.config') -Raw
    $packageVersions = @{}
    foreach ($package in $config.packages.package) {
        $packageVersions[[string]$package.id] = [string]$package.version
        $packageDirectory = Join-Path $packages ($package.id + '.' + $package.version)
        if (-not (Test-Path -LiteralPath $packageDirectory)) { throw "Restored package folder is missing: $packageDirectory" }
    }

    # Validate the files the project actually references. Package IDs and versions
    # come from packages.config; file paths come from the project file, so a package
    # upgrade does not require editing this script.
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectDirectory 'ReportingModule.Web.csproj') -Raw
    $requiredPaths = @()
    $requiredPaths += @($project.SelectNodes("//*[local-name()='HintPath']") | ForEach-Object { $_.InnerText })
    $requiredPaths += @($project.SelectNodes("//*[local-name()='Import' and @Project]") | ForEach-Object { $_.GetAttribute('Project') } | Where-Object { $_ -match '(^|[\\/])packages([\\/])' })
    foreach ($reference in $requiredPaths) {
        $normalized = $reference.Replace('/', '\\')
        if ($normalized -notmatch '(?:^|\\)packages\\(?<folder>[^\\]+)\\(?<asset>.+)$') { continue }
        $folder = $Matches.folder
        $asset = $Matches.asset
        $id = @($packageVersions.Keys | Sort-Object { $_.Length } -Descending | Where-Object { $folder.StartsWith($_ + '.', [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)
        if ($id.Count -eq 0) { throw "Project references a NuGet package folder not listed in packages.config: $folder" }
        $id = [string]$id[0]
        $version = $folder.Substring($id.Length + 1)
        if (-not $packageVersions.ContainsKey($id)) { throw "Project references package '$id' but it is missing from packages.config: $reference" }
        if ($packageVersions[$id] -ne $version) { throw "Project package version for '$id' ($version) does not match packages.config ($($packageVersions[$id]))." }
        $assetPath = Join-Path $packages ($folder + '\\' + $asset)
        if (-not (Test-Path -LiteralPath $assetPath)) { throw "Required project package file is missing: $assetPath" }
    }
    Write-Host 'PASS: every packages.config dependency and package file referenced by the project has been restored.'

    if (-not $SkipBuild) {
        $msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
        $msbuildPath = if ($msbuild) { $msbuild.Source } else { Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' }
        if (-not (Test-Path -LiteralPath $msbuildPath)) { throw 'Package restore passed, but MSBuild was not found. Run the documented Release build in Visual Studio Developer PowerShell.' }
        & $msbuildPath $solution /t:Build /p:Configuration=Release /nologo /verbosity:minimal
        if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE after restore." }
    }
}
finally { Pop-Location }
