# ============================================================
#  SwyxConnect - Deployment-Paket erstellen
#  Baut Bridge + Electron und erstellt dist/SwyxConnect/
# ============================================================

param(
    [string]$OutputDir = "dist\SwyxConnect"
)

$ErrorActionPreference = "Stop"

Write-Host "=== SwyxConnect Deployment-Build ===" -ForegroundColor Cyan

# 1. Bridge bauen
Write-Host "`n[1/4] C# Bridge bauen..." -ForegroundColor Yellow
dotnet publish bridge\SwyxBridge\SwyxBridge.csproj -c Release -r win-x86 --self-contained false -o out\bridge
if ($LASTEXITCODE -ne 0) { throw "Bridge-Build fehlgeschlagen" }

# 2. Electron-App bauen
Write-Host "`n[2/4] Electron-App bauen..." -ForegroundColor Yellow
npx electron-vite build
if ($LASTEXITCODE -ne 0) { throw "Electron-Build fehlgeschlagen" }

# 3. Deployment-Ordner erstellen
Write-Host "`n[3/4] Deployment-Paket erstellen..." -ForegroundColor Yellow

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

# Struktur anlegen
New-Item -ItemType Directory -Path "$OutputDir\app\main" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputDir\app\preload" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputDir\app\renderer" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputDir\bridge" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputDir\resources" -Force | Out-Null

# Dateien kopieren
Copy-Item "out\main\*" "$OutputDir\app\main\" -Recurse -Force
Copy-Item "out\preload\*" "$OutputDir\app\preload\" -Recurse -Force
Copy-Item "out\renderer\*" "$OutputDir\app\renderer\" -Recurse -Force
Copy-Item "out\bridge\*" "$OutputDir\bridge\" -Recurse -Force
Copy-Item "resources\icon.ico" "$OutputDir\resources\" -Force -ErrorAction SilentlyContinue

# Starter und Anleitung
# (Diese werden aus dem Repo kopiert, falls vorhanden)
if (Test-Path "dist\SwyxConnect\SwyxConnect.bat") {
    # Bereits vorhanden
} else {
    Write-Host "  HINWEIS: SwyxConnect.bat und LIESMICH.txt manuell in $OutputDir ablegen" -ForegroundColor DarkYellow
}

# 4. Zusammenfassung
Write-Host "`n[4/4] Fertig!" -ForegroundColor Green
$files = Get-ChildItem $OutputDir -Recurse -File
$size = ($files | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "  Pfad:    $OutputDir"
Write-Host "  Dateien: $($files.Count)"
Write-Host "  Groesse: $([math]::Round($size, 1)) MB"
Write-Host "`nDeployment-Paket bereit zum Kopieren!" -ForegroundColor Green
