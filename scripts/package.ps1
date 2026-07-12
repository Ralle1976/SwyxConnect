# Package-Script: Baut Bridge + Electron App + Installer
# Aufruf: powershell -ExecutionPolicy Bypass -File scripts/package.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== SwyIt-byRalle197 Full Build ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: C# Bridge bauen
Write-Host "[1/3] Building C# COM Bridge..." -ForegroundColor Yellow
& "$PSScriptRoot\build-bridge.ps1" -Configuration Release
if ($LASTEXITCODE -ne 0) { exit 1 }

# Step 2: Electron App bauen
Write-Host ""
Write-Host "[2/3] Building Electron App..." -ForegroundColor Yellow
Set-Location (Join-Path $PSScriptRoot "..")
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Electron build FAILED!" -ForegroundColor Red
    exit 1
}

# Step 3: Installer erstellen
Write-Host ""
Write-Host "[3/3] Creating Windows Installer..." -ForegroundColor Yellow
npm run build:win
if ($LASTEXITCODE -ne 0) {
    Write-Host "Installer build FAILED!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Packaging komplett! ===" -ForegroundColor Green
Write-Host "Installer liegt in: dist/"
