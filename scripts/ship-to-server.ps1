[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "linux-x64")]
    [string]$Runtime,

    [string]$InstallPath,
    [string]$StopProcessName,
    [string]$StartExe,
    [string]$DeployZipPath,
    [string]$DeployHost,
    [string]$RemotePath,
    [string]$RemoteRestartCommand,
    [switch]$SkipBuild,
    [switch]$SkipDownloads,
    [switch]$NoZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
$localConfig = Join-Path $scriptRoot "ship-to-server.local.ps1"
$exampleConfig = Join-Path $scriptRoot "ship-to-server.local.ps1.example"
$cliRuntime = $Runtime
$cliInstallPath = $InstallPath
$cliStopProcessName = $StopProcessName
$cliStartExe = $StartExe
$cliDeployZipPath = $DeployZipPath
$cliDeployHost = $DeployHost
$cliRemotePath = $RemotePath
$cliRemoteRestartCommand = $RemoteRestartCommand

if (Test-Path -LiteralPath $localConfig) {
    Write-Host "Loading $localConfig" -ForegroundColor Cyan
    . $localConfig
}
elseif (-not $InstallPath -and -not $DeployHost -and -not $DeployZipPath) {
    Write-Host "No ship-to-server.local.ps1 found." -ForegroundColor Yellow
    Write-Host "Copy the example and set InstallPath to your running ErsatzTV folder:" -ForegroundColor Yellow
    Write-Host "  copy `"$exampleConfig`" `"$localConfig`""
    Write-Host ""
    Write-Host "Building a package anyway. You can copy it by hand from artifacts\test-build\"
}

if ($cliRuntime) { $Runtime = $cliRuntime }
if ($cliInstallPath) { $InstallPath = $cliInstallPath }
if ($cliStopProcessName) { $StopProcessName = $cliStopProcessName }
if ($cliStartExe) { $StartExe = $cliStartExe }
if ($cliDeployZipPath) { $DeployZipPath = $cliDeployZipPath }
if ($cliDeployHost) { $DeployHost = $cliDeployHost }
if ($cliRemotePath) { $RemotePath = $cliRemotePath }
if ($cliRemoteRestartCommand) { $RemoteRestartCommand = $cliRemoteRestartCommand }
if (-not $Runtime) { $Runtime = "win-x64" }

$packScript = Join-Path $scriptRoot "pack-test-build.ps1"
if (-not (Test-Path -LiteralPath $packScript)) {
    throw "Could not find $packScript"
}

if (-not $SkipBuild) {
    $packArgs = @{
        Configuration = $Configuration
        Runtime = $Runtime
    }
    if ($SkipDownloads) { $packArgs["SkipDownloads"] = $true }
    if ($NoZip) { $packArgs["NoZip"] = $true }

    & $packScript @packArgs
    if ($LASTEXITCODE -ne 0) {
        throw "pack-test-build.ps1 failed with exit code $LASTEXITCODE."
    }
}

$artifactsRoot = Join-Path (Join-Path $repoRoot "artifacts") "test-build"
$packages = Get-ChildItem -LiteralPath $artifactsRoot -Directory |
    Where-Object { $_.Name -like "ErsatzTV-Legacy-local-*-$Runtime" } |
    Sort-Object LastWriteTime -Descending

if ($packages.Count -eq 0) {
    throw "No packaged build found under $artifactsRoot for runtime $Runtime."
}

$packagePath = $packages[0].FullName
$zipPath = "$packagePath.zip"

Write-Host ""
Write-Host "Using package: $packagePath" -ForegroundColor Cyan

if ($StopProcessName) {
    $running = Get-Process -Name $StopProcessName -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping $StopProcessName..." -ForegroundColor Yellow
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
}

if ($InstallPath) {
    if (-not (Test-Path -LiteralPath $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }

    Write-Host "Replacing files in $InstallPath" -ForegroundColor Cyan
    Get-ChildItem -LiteralPath $packagePath -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $InstallPath -Recurse -Force
    }
    Write-Host "Install folder updated." -ForegroundColor Green
}

if ($DeployZipPath) {
    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "ZIP was not created at $zipPath. Run without -NoZip."
    }
    if (-not (Test-Path -LiteralPath $DeployZipPath)) {
        New-Item -ItemType Directory -Path $DeployZipPath -Force | Out-Null
    }
    Copy-Item -LiteralPath $zipPath -Destination $DeployZipPath -Force
    Write-Host "Copied ZIP to $DeployZipPath" -ForegroundColor Green
}

if ($DeployHost) {
    if ([string]::IsNullOrWhiteSpace($RemotePath)) {
        throw "DeployHost is set but RemotePath is empty."
    }
    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "ZIP was not created at $zipPath. Run without -NoZip."
    }

    $remoteZip = ($RemotePath.TrimEnd('/') + "/" + (Split-Path -Leaf $zipPath))
    Write-Host "Copying ZIP to ${DeployHost}:$remoteZip" -ForegroundColor Cyan
    & scp $zipPath "${DeployHost}:$remoteZip"
    if ($LASTEXITCODE -ne 0) {
        throw "scp failed with exit code $LASTEXITCODE."
    }

    if ($RemoteRestartCommand) {
        Write-Host "Running remote restart command..." -ForegroundColor Cyan
        & ssh $DeployHost $RemoteRestartCommand
        if ($LASTEXITCODE -ne 0) {
            throw "Remote restart failed with exit code $LASTEXITCODE."
        }
    }
    else {
        Write-Host "ZIP copied. Extract it over the server install and restart ErsatzTV." -ForegroundColor Yellow
    }
}

if ($StartExe -and $InstallPath) {
    $exe = Join-Path $InstallPath $StartExe
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "StartExe was not found at $exe"
    }
    Write-Host "Starting $exe" -ForegroundColor Cyan
    Start-Process -FilePath $exe -WorkingDirectory $InstallPath
}

Write-Host ""
Write-Host "Ship complete." -ForegroundColor Green
Write-Host "Package: $packagePath"
if (Test-Path -LiteralPath $zipPath) {
    Write-Host "ZIP:     $zipPath"
}
