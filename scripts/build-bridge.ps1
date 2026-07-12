# Build-Script fuer die C# COM Bridge
# Aufruf: powershell -ExecutionPolicy Bypass -File scripts/build-bridge.ps1

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BridgeProject = Join-Path $PSScriptRoot ".." "bridge" "SwyxBridge" "SwyxBridge.csproj"
$OutputDir = Join-Path $PSScriptRoot ".." "out" "bridge"

Write-Host "=== SwyxBridge Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Output: $OutputDir"
Write-Host ""

# Bridge bauen
Write-Host "Building SwyxBridge..." -ForegroundColor Yellow
dotnet publish $BridgeProject -c $Configuration -o $OutputDir --self-contained false -r win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Build erfolgreich!" -ForegroundColor Green
Write-Host "Bridge liegt in: $OutputDir"
