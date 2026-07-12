# Build the SwyxStandalone bridge to a LOCAL path (avoids MemoryMappedFile bug on Google Drive).
# Then copies the output back to out/bridge/ in the project.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/build-bridge.ps1

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# Resolve project root from the script location
# scripts/ → SwyIt-byRalle1976/
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$BridgeProject = Join-Path $ProjectRoot 'bridge\SwyxStandalone\SwyxStandalone.csproj'

# Fallback: if ProjectRoot doesn't contain the csproj, try the known path
if (-not (Test-Path $BridgeProject)) {
    $ProjectRoot = 'G:\Andere Computer\Mein Computer\Desktop\SwyIt-byRalle1976'
    $BridgeProject = Join-Path $ProjectRoot 'bridge\SwyxStandalone\SwyxStandalone.csproj'
}

# IMPORTANT: Google Drive (G:\) does not support MemoryMappedFiles, which the .NET SDK
# needs for CreateAppHost. Copy the bridge source to a LOCAL temp folder, build there,
# then copy results back.
$LocalSourceDir = Join-Path $env:TEMP 'swyxconnect-bridge-src'
$LocalBuildDir = Join-Path $env:TEMP 'swyxconnect-bridge-build'
$OutputDir = Join-Path $ProjectRoot 'out\bridge'
$BridgeSourceDir = Join-Path $ProjectRoot 'bridge\SwyxStandalone'

Write-Host "=== SwyxStandalone Bridge Build ===" -ForegroundColor Cyan
Write-Host "Source:     $BridgeSourceDir"
Write-Host "Local copy: $LocalSourceDir"
Write-Host "Final out:  $OutputDir"
Write-Host ""

# Clean local dirs
foreach ($d in @($LocalSourceDir, $LocalBuildDir)) {
    if (Test-Path $d) { Remove-Item $d -Recurse -Force }
    New-Item -ItemType Directory -Path $d -Force | Out-Null
}

# Copy bridge source to local temp (preserves relative paths)
Write-Host "Copying bridge source to local temp..." -ForegroundColor Yellow
Copy-Item -Path (Join-Path $BridgeSourceDir '*') -Destination $LocalSourceDir -Recurse -Force
$LocalProject = Join-Path $LocalSourceDir 'SwyxStandalone.csproj'

# Build from local temp (avoids MemoryMappedFile IOException)
Write-Host "Building (UseAppHost=true, framework-dependent)..." -ForegroundColor Yellow
& dotnet publish $LocalProject -c $Configuration -o $LocalBuildDir --self-contained false
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED!" -ForegroundColor Red
    exit 1
}

# Verify SwyxMessenger.exe was created
$bridgeExe = Join-Path $LocalBuildDir 'SwyxMessenger.exe'
if (-not (Test-Path $bridgeExe)) {
    Write-Host "ERROR: SwyxMessenger.exe not found in build output!" -ForegroundColor Red
    exit 1
}
Write-Host "SwyxMessenger.exe created: $((Get-Item $bridgeExe).Length) bytes" -ForegroundColor Green

# Copy output to project out/bridge/
Write-Host "Copying to $OutputDir ..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
Copy-Item -Path (Join-Path $LocalBuildDir '*') -Destination $OutputDir -Recurse -Force

Write-Host ""
Write-Host "Build erfolgreich!" -ForegroundColor Green
Write-Host "Bridge liegt in: $OutputDir"
Write-Host "Bridge EXE: $(Join-Path $OutputDir 'SwyxMessenger.exe')"
