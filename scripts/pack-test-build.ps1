[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "linux-x64")]
    [string]$Runtime = "win-x64",

    [switch]$NoZip,
    [switch]$SkipDownloads,
    [switch]$KeepExisting,
    [switch]$OpenFolder
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$runtime = $Runtime
$framework = "net10.0"
$launcherVersion = "v1.0.0"
$ffmpegVersion = "8.1.2"
$ffmpegFile = "ffmpeg-n8.1.2-etv-g3c0520f9-win64-gpl-8.1.zip"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path

$mainProject = Join-Path (Join-Path $repoRoot "ErsatzTV") "ErsatzTV.csproj"
$scannerProject = Join-Path (Join-Path $repoRoot "ErsatzTV.Scanner") "ErsatzTV.Scanner.csproj"

$artifactsRoot = Join-Path (Join-Path $repoRoot "artifacts") "test-build"
$tempRoot = Join-Path $artifactsRoot "_work"
$mainPublish = Join-Path $tempRoot "main"
$scannerPublish = Join-Path $tempRoot "scanner"
$downloadRoot = Join-Path $tempRoot "downloads"

$commit = "unknown"
if (Get-Command git -ErrorAction SilentlyContinue) {
    $resolvedCommit = & git -C $repoRoot rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($resolvedCommit)) {
        $commit = $resolvedCommit.Trim()
    }
}

$packageName = "ErsatzTV-Legacy-local-$commit-$runtime"
$packagePath = Join-Path $artifactsRoot $packageName
$zipPath = Join-Path $artifactsRoot "$packageName.zip"

function Assert-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found."
    }
}

function Invoke-DotNetPublish {
    param(
        [Parameter(Mandatory = $true)][string]$Project,
        [Parameter(Mandatory = $true)][string]$Output
    )

    $arguments = @(
        "publish",
        $Project,
        "--framework", $framework,
        "--runtime", $runtime,
        "--configuration", $Configuration,
        "--output", $Output,
        "--self-contained", "true",
        "/p:RestoreEnablePackagePruning=true",
        "/p:InformationalVersion=local-$commit-$runtime",
        "/p:EnableCompressionInSingleFile=true",
        "/p:DebugType=Embedded",
        "/p:PublishSingleFile=true"
    )

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for '$Project' with exit code $LASTEXITCODE."
    }
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Get-GitHubHeaders {
    $headers = @{
        "Accept" = "application/vnd.github+json"
        "User-Agent" = "ErsatzTV-local-test-packager"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        $headers["Authorization"] = "Bearer $($env:GITHUB_TOKEN)"
    }

    return $headers
}

function Download-File {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Write-Host "Downloading $(Split-Path -Leaf $Destination)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $Uri -OutFile $Destination -Headers (Get-GitHubHeaders) -UseBasicParsing
}

function Add-ExternalLinuxTools {
    New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null

    $nextRelease = Invoke-RestMethod `
        -Uri "https://api.github.com/repos/ErsatzTV/next/releases/tags/develop" `
        -Headers (Get-GitHubHeaders) `
        -UseBasicParsing

    $nextAsset = $nextRelease.assets |
        Where-Object { $_.name -like "ersatztv-next-*-linux-x64.tar.gz" } |
        Select-Object -First 1

    if ($null -eq $nextAsset) {
        throw "Could not find the current ErsatzTV Next linux-x64 release asset."
    }

    $nextArchive = Join-Path $downloadRoot $nextAsset.name
    $nextExtracted = Join-Path $downloadRoot "next"
    Download-File -Uri $nextAsset.browser_download_url -Destination $nextArchive

    New-Item -ItemType Directory -Path $nextExtracted -Force | Out-Null
    tar -xf $nextArchive -C $nextExtracted --strip-components 1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to extract $($nextAsset.name)."
    }

    $channel = Get-ChildItem -LiteralPath $nextExtracted -Filter "ersatztv-channel" -Recurse |
        Select-Object -First 1

    if ($null -eq $channel) {
        throw "The downloaded ErsatzTV Next package did not contain ersatztv-channel."
    }

    Copy-Item -LiteralPath $channel.FullName -Destination (Join-Path $packagePath "ersatztv-channel") -Force
}

function Add-ExternalWindowsTools {
    New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null

    $launcherPath = Join-Path $packagePath "ErsatzTV-Windows.exe"
    Download-File `
        -Uri "https://github.com/ErsatzTV/ErsatzTV-Windows/releases/download/$launcherVersion/ErsatzTV-Windows.exe" `
        -Destination $launcherPath

    $nextRelease = Invoke-RestMethod `
        -Uri "https://api.github.com/repos/ErsatzTV/next/releases/tags/develop" `
        -Headers (Get-GitHubHeaders) `
        -UseBasicParsing

    $nextAsset = $nextRelease.assets |
        Where-Object { $_.name -like "ersatztv-next-*-windows-x64.zip" } |
        Select-Object -First 1

    if ($null -eq $nextAsset) {
        throw "Could not find the current ErsatzTV Next windows-x64 release asset."
    }

    $nextZip = Join-Path $downloadRoot $nextAsset.name
    $nextExtracted = Join-Path $downloadRoot "next"
    Download-File -Uri $nextAsset.browser_download_url -Destination $nextZip
    Expand-Archive -LiteralPath $nextZip -DestinationPath $nextExtracted -Force

    $channel = Get-ChildItem -LiteralPath $nextExtracted -Filter "ersatztv-channel.exe" -Recurse |
        Select-Object -First 1

    if ($null -eq $channel) {
        throw "The downloaded ErsatzTV Next package did not contain ersatztv-channel.exe."
    }

    Copy-Item -LiteralPath $channel.FullName -Destination (Join-Path $packagePath "ersatztv-channel.exe") -Force

    $ffmpegZip = Join-Path $downloadRoot $ffmpegFile
    $ffmpegExtracted = Join-Path $downloadRoot "ffmpeg"
    Download-File `
        -Uri "https://github.com/ErsatzTV/ErsatzTV-ffmpeg/releases/download/$ffmpegVersion/$ffmpegFile" `
        -Destination $ffmpegZip
    Expand-Archive -LiteralPath $ffmpegZip -DestinationPath $ffmpegExtracted -Force

    foreach ($toolName in @("ffmpeg.exe", "ffprobe.exe")) {
        $tool = Get-ChildItem -LiteralPath $ffmpegExtracted -Filter $toolName -Recurse |
            Select-Object -First 1

        if ($null -eq $tool) {
            throw "The downloaded FFmpeg package did not contain $toolName."
        }

        Copy-Item -LiteralPath $tool.FullName -Destination (Join-Path $packagePath $toolName) -Force
    }
}

function Write-TestLaunchers {
    $consoleLauncher = @'
@echo off
setlocal
cd /d "%~dp0"
echo.
echo Starting the locally-built ErsatzTV server.
echo Web UI: http://localhost:8409
echo Leave this window open. Errors will be visible here.
echo.
ErsatzTV.exe
set EXIT_CODE=%ERRORLEVEL%
echo.
echo ErsatzTV exited with code %EXIT_CODE%.
echo Logs are normally stored under %%LOCALAPPDATA%%\ersatztv\logs
pause
exit /b %EXIT_CODE%
'@

    $healthLauncher = @'
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$server = Join-Path $root "ErsatzTV.exe"
$url = "http://localhost:8409"

Write-Host ""
Write-Host "Starting ErsatzTV test build..." -ForegroundColor Cyan
Write-Host "Server: $server"
Write-Host "Web UI: $url"
Write-Host ""

$process = Start-Process -FilePath $server -WorkingDirectory $root -PassThru

for ($attempt = 1; $attempt -le 60; $attempt++) {
    if ($process.HasExited) {
        Write-Host "ErsatzTV exited before the Web UI became available." -ForegroundColor Red
        Write-Host "Exit code: $($process.ExitCode)"
        Write-Host "Run Start-ErsatzTV-Console.cmd to see startup errors."
        Read-Host "Press Enter to close"
        exit $process.ExitCode
    }

    try {
        Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 | Out-Null
        Write-Host "Web UI is ready; opening the browser." -ForegroundColor Green
        Start-Process $url
        $process.WaitForExit()
        exit $process.ExitCode
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

Write-Host "ErsatzTV is still running, but the Web UI did not answer within 60 seconds." -ForegroundColor Yellow
Write-Host "Check http://localhost:8409 and the logs under $env:LOCALAPPDATA\ersatztv\logs"
Read-Host "Press Enter to close"
exit 1
'@

    $healthCmd = @'
@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-ErsatzTV-Test.ps1"
exit /b %ERRORLEVEL%
'@

    Set-Content -LiteralPath (Join-Path $packagePath "Start-ErsatzTV-Console.cmd") -Value $consoleLauncher -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $packagePath "Start-ErsatzTV-Test.ps1") -Value $healthLauncher -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $packagePath "Start-ErsatzTV-Test.cmd") -Value $healthCmd -Encoding ASCII
}

Assert-Command -Name "dotnet"

if (-not (Test-Path -LiteralPath $mainProject)) {
    throw "Could not find the main project at '$mainProject'."
}

if (-not (Test-Path -LiteralPath $scannerProject)) {
    throw "Could not find the scanner project at '$scannerProject'."
}

if (-not $KeepExisting) {
    foreach ($path in @($packagePath, $zipPath, $tempRoot)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null

$originalProjectBytes = [IO.File]::ReadAllBytes($mainProject)

try {
    Write-Host ""
    Write-Host "Building release-style ErsatzTV test package" -ForegroundColor Cyan
    Write-Host "  Configuration: $Configuration"
    Write-Host "  Runtime:       $runtime"
    Write-Host "  Commit:        $commit"
    Write-Host "  Output:        $packagePath"
    Write-Host ""

    Write-Host "Publishing ErsatzTV.Scanner as a self-contained single file..." -ForegroundColor Cyan
    Invoke-DotNetPublish -Project $scannerProject -Output $scannerPublish

    $projectText = [Text.Encoding]::UTF8.GetString($originalProjectBytes)
    $scannerReferencePattern = '(?m)^\s*<ProjectReference Include="\.\.\\ErsatzTV\.Scanner\\ErsatzTV\.Scanner\.csproj"\s*/>\s*\r?\n?'
    $projectWithoutScanner = [Text.RegularExpressions.Regex]::Replace(
        $projectText,
        $scannerReferencePattern,
        "",
        1
    )

    if ($projectWithoutScanner -eq $projectText) {
        throw "Could not locate the scanner ProjectReference in ErsatzTV.csproj."
    }

    [IO.File]::WriteAllText(
        $mainProject,
        $projectWithoutScanner,
        (New-Object Text.UTF8Encoding($false))
    )

    Write-Host "Publishing ErsatzTV as a self-contained single file..." -ForegroundColor Cyan
    Invoke-DotNetPublish -Project $mainProject -Output $mainPublish
}
finally {
    [IO.File]::WriteAllBytes($mainProject, $originalProjectBytes)
}

Copy-DirectoryContents -Source $scannerPublish -Destination $packagePath
Copy-DirectoryContents -Source $mainPublish -Destination $packagePath

$resourcesPath = Join-Path $packagePath "Resources"
if (Test-Path -LiteralPath $resourcesPath) {
    Remove-Item -LiteralPath $resourcesPath -Recurse -Force
}

if (-not $SkipDownloads) {
    if ($runtime -eq "win-x64") {
        Add-ExternalWindowsTools
    }
    else {
        Add-ExternalLinuxTools
    }
}
else {
    Write-Warning "Skipping launcher, ErsatzTV Next engine, FFmpeg, and FFprobe downloads."
}

if ($runtime -eq "win-x64") {
    Write-TestLaunchers
}

$buildInfo = @"
ErsatzTV local test package
Created:       $(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")
Commit:        $commit
Configuration: $Configuration
Runtime:       $runtime
Publishing:    self-contained, single-file

Recommended Windows test launch:
  Start-ErsatzTV-Test.cmd

Visible-console troubleshooting:
  Start-ErsatzTV-Console.cmd

Web UI:
  http://localhost:8409

Official-style launcher:
  ErsatzTV-Windows.exe
  This starts ErsatzTV with its console hidden. Use the tray icon's
  "Launch Web UI" option. For startup failures, use the console launcher above.

Ship this package:
  ship-to-server.cmd
  Copy scripts\ship-to-server.local.ps1.example to ship-to-server.local.ps1
  and set InstallPath to the running ErsatzTV folder.

Before starting:
  Exit any existing ErsatzTV tray instance. ErsatzTV prevents a second process
  from using the same configuration folder.

Logs:
  %LOCALAPPDATA%\ersatztv\logs

This package does not copy your existing database, configuration, or media.
The locally-built ErsatzTV executables are not code-signed.
Linux packages do not include ffmpeg; install ffmpeg on the server or use Docker.
"@

Set-Content -LiteralPath (Join-Path $packagePath "TEST-BUILD.txt") -Value $buildInfo -Encoding UTF8

if (-not $NoZip) {
    Write-Host "Creating release-style ZIP..." -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $packagePath "*") -DestinationPath $zipPath -Force
}

if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

Write-Host ""
Write-Host "Test package ready." -ForegroundColor Green
Write-Host "Folder: $packagePath"
if (-not $NoZip) {
    Write-Host "ZIP:    $zipPath"
}
Write-Host ""
Write-Host "Start with: $(Join-Path $packagePath 'Start-ErsatzTV-Test.cmd')" -ForegroundColor Green

if ($OpenFolder) {
    Start-Process explorer.exe $artifactsRoot
}
