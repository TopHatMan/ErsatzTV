[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release", "Debug No Sync")]
    [string]$Configuration = "Debug",

    [ValidateSet(
        "win-x64",
        "win-arm64",
        "linux-x64",
        "linux-arm",
        "linux-arm64",
        "linux-musl-x64",
        "osx-x64",
        "osx-arm64"
    )]
    [string]$Runtime = "win-x64",

    [switch]$SelfContained,
    [switch]$NoZip,
    [switch]$KeepExisting,
    [switch]$OpenFolder
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
$projectDirectory = Join-Path $repoRoot "ErsatzTV"
$projectPath = Join-Path $projectDirectory "ErsatzTV.csproj"
$artifactsDirectory = Join-Path $repoRoot "artifacts"
$artifactsRoot = Join-Path $artifactsDirectory "test-build"
$safeConfiguration = $Configuration.Replace(" ", "-").ToLowerInvariant()
$packageName = "ErsatzTV-$safeConfiguration-$Runtime"
$publishPath = Join-Path $artifactsRoot $packageName
$zipPath = Join-Path $artifactsRoot "$packageName.zip"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK was not found. Install the .NET 10 SDK and make sure 'dotnet' is on PATH."
}

if (-not (Test-Path $projectPath)) {
    throw "Could not find the ErsatzTV application project at '$projectPath'."
}

if (-not $KeepExisting) {
    if (Test-Path $publishPath) {
        Remove-Item $publishPath -Recurse -Force
    }

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
}

New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

$selfContainedValue = "false"
if ($SelfContained) {
    $selfContainedValue = "true"
}

$publishArguments = @(
    "publish",
    $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", $selfContainedValue,
    "--output", $publishPath,
    "/p:UseAppHost=true"
)

Write-Host ""
Write-Host "Publishing ErsatzTV test build" -ForegroundColor Cyan
Write-Host "  Configuration : $Configuration"
Write-Host "  Runtime       : $Runtime"
Write-Host "  Self-contained: $selfContainedValue"
Write-Host "  Output        : $publishPath"
Write-Host ""

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$commit = "unknown"
if (Get-Command git -ErrorAction SilentlyContinue) {
    $resolvedCommit = & git -C $repoRoot rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($resolvedCommit)) {
        $commit = $resolvedCommit.Trim()
    }
}

$buildInfo = @"
ErsatzTV test package
Created:       $(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")
Commit:        $commit
Configuration: $Configuration
Runtime:       $Runtime
Self-contained: $selfContainedValue

Windows:
  Run ErsatzTV.exe

Linux/macOS:
  Run ./ErsatzTV

This package intentionally does not copy your existing database, configuration,
or media. It contains only the published application files needed for testing.
"@

Set-Content -Path (Join-Path $publishPath "TEST-BUILD.txt") -Value $buildInfo -Encoding UTF8

if (-not $NoZip) {
    Write-Host "Creating ZIP package..." -ForegroundColor Cyan
    Compress-Archive -Path $publishPath -DestinationPath $zipPath -Force
}

Write-Host ""
Write-Host "Test build ready." -ForegroundColor Green
Write-Host "Folder: $publishPath"
if (-not $NoZip) {
    Write-Host "ZIP:    $zipPath"
}

if ($OpenFolder) {
    if ($env:OS -eq "Windows_NT") {
        Start-Process explorer.exe $artifactsRoot
    }
    elseif (Get-Command open -ErrorAction SilentlyContinue) {
        & open $artifactsRoot
    }
    elseif (Get-Command xdg-open -ErrorAction SilentlyContinue) {
        & xdg-open $artifactsRoot
    }
}
