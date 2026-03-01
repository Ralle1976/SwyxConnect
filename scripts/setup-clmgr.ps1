#Requires -RunAsAdministrator
<#
.SYNOPSIS
    CLMgr Standalone-Setup fuer SwyxConnect

.DESCRIPTION
    Kopiert CLMgr-Dateien aus einer vorhandenen SwyxIt!-Installation
    und registriert den COM-Server fuer den Standalone-Betrieb.
    Erfordert eine gueltige SwyxIt!-Installation als Quelle.

.PARAMETER Uninstall
    Entfernt CLMgr-Dateien und COM-Registrierung

.PARAMETER SourcePath
    Pfad zur SwyxIt!-Installation (Standard: automatische Erkennung)

.EXAMPLE
    .\setup-clmgr.ps1
    .\setup-clmgr.ps1 -SourcePath "D:\Swyx\SwyxIt!"
    .\setup-clmgr.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [switch]$Uninstall,
    [string]$SourcePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Konfiguration ────────────────────────────────────────────────────────────

$TargetDir = Join-Path $env:LOCALAPPDATA 'SwyxConnect\clmgr'

$RequiredFiles = @(
    'CLMgr.exe',
    'CLMgr.exe.config',
    'IpPbxCDSClientLib.dll',
    'IpPbxCDSSharedLib.dll',
    'ClientShare.dll',
    'Interop.CLMgr.dll',
    'IpPbxTracing.dll'
)

$OptionalFiles = @(
    'IpPbxCDSWrap.dll',
    'IpPbxWin32.dll',
    'CallRoutingMgr.dll',
    'CallRoutingMgr.exe',
    'IpPbx.Client.Plugin.ComSocket.dll'
)

$ComClsid    = '{F8E552F8-4C00-11D3-80BC-00105A653379}'
$ComAppId    = '{F8E552A5-4C00-11D3-80BC-00105A653379}'
$ComTypeLib  = '{F8E552F7-4C00-11D3-80BC-00105A653379}'
$ComProgId   = 'CLMgr.ClientLineMgr'
$ComProgId2  = 'CLMgr.ClientLineMgr.2'

# ── Hilfsfunktionen ──────────────────────────────────────────────────────────

function Write-Status($msg) { Write-Host "  [*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)     { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg)   { Write-Host "  [!] $msg" -ForegroundColor Yellow }
function Write-Err($msg)    { Write-Host "  [X] $msg" -ForegroundColor Red }

function Find-SwyxItInstallation {
    # 1. Registry (x86 + x64)
    $regPaths = @(
        'HKLM:\SOFTWARE\WOW6432Node\Swyx\Client',
        'HKLM:\SOFTWARE\Swyx\Client',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\SwyxIt!.exe',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\SwyxIt!.exe'
    )

    foreach ($regPath in $regPaths) {
        if (Test-Path $regPath) {
            $val = (Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue)
            if ($val.InstallDir -and (Test-Path (Join-Path $val.InstallDir 'CLMgr.exe'))) {
                return $val.InstallDir
            }
            if ($val.'(default)' -and (Test-Path (Split-Path $val.'(default)' -Parent))) {
                $dir = Split-Path $val.'(default)' -Parent
                if (Test-Path (Join-Path $dir 'CLMgr.exe')) { return $dir }
            }
        }
    }

    # 2. Standard-Pfade
    $defaultPaths = @(
        "${env:ProgramFiles(x86)}\Swyx\SwyxIt!",
        "$env:ProgramFiles\Swyx\SwyxIt!",
        "${env:ProgramFiles(x86)}\SwyxIt!",
        "$env:ProgramFiles\SwyxIt!"
    )

    foreach ($p in $defaultPaths) {
        if (Test-Path (Join-Path $p 'CLMgr.exe')) { return $p }
    }

    # 3. Laufender Prozess
    $proc = Get-Process -Name 'CLMgr' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc) {
        $dir = Split-Path $proc.Path -Parent
        if (Test-Path (Join-Path $dir 'CLMgr.exe')) { return $dir }
    }

    return $null
}

# ── Deinstallation ───────────────────────────────────────────────────────────

if ($Uninstall) {
    Write-Host "`n=== SwyxConnect CLMgr Deinstallation ===" -ForegroundColor Magenta

    # COM-Registrierung entfernen
    Write-Status 'Entferne COM-Registrierung...'
    $clmgrExe = Join-Path $TargetDir 'CLMgr.exe'
    if (Test-Path $clmgrExe) {
        Start-Process -FilePath $clmgrExe -ArgumentList '/unregserver' -Wait -NoNewWindow -ErrorAction SilentlyContinue
    }

    # Registry-Eintraege manuell aufraeumen
    $regKeys = @(
        "HKLM:\SOFTWARE\Classes\CLSID\$ComClsid",
        "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$ComClsid",
        "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$ComClsid",
        "HKLM:\SOFTWARE\Classes\AppID\$ComAppId",
        "HKLM:\SOFTWARE\Classes\$ComProgId",
        "HKLM:\SOFTWARE\Classes\$ComProgId2"
    )
    foreach ($key in $regKeys) {
        if (Test-Path $key) {
            Remove-Item -Path $key -Recurse -Force -ErrorAction SilentlyContinue
            Write-Status "  Entfernt: $key"
        }
    }

    # Dateien loeschen
    if (Test-Path $TargetDir) {
        Write-Status "Loesche $TargetDir ..."
        Remove-Item -Path $TargetDir -Recurse -Force
        Write-Ok 'Verzeichnis entfernt'
    }

    Write-Ok 'Deinstallation abgeschlossen'
    exit 0
}

# ── Installation ─────────────────────────────────────────────────────────────

Write-Host "`n=== SwyxConnect CLMgr Setup ===" -ForegroundColor Magenta
Write-Host ''

# LIZENZHINWEIS
Write-Host '  HINWEIS: CLMgr.exe und zugehoerige DLLs sind proprietaere' -ForegroundColor Yellow
Write-Host '  Swyx/Enreach-Dateien. Sie werden aus einer vorhandenen' -ForegroundColor Yellow
Write-Host '  SwyxIt!-Installation kopiert und NICHT weiterverbreitet.' -ForegroundColor Yellow
Write-Host ''

# Quelle finden
if ($SourcePath) {
    if (-not (Test-Path (Join-Path $SourcePath 'CLMgr.exe'))) {
        Write-Err "CLMgr.exe nicht gefunden in: $SourcePath"
        exit 1
    }
    $source = $SourcePath
} else {
    Write-Status 'Suche SwyxIt!-Installation...'
    $source = Find-SwyxItInstallation
    if (-not $source) {
        Write-Err 'Keine SwyxIt!-Installation gefunden!'
        Write-Host '  Bitte installieren Sie SwyxIt! oder geben Sie den Pfad an:' -ForegroundColor Gray
        Write-Host '  .\setup-clmgr.ps1 -SourcePath "C:\Pfad\zu\SwyxIt!"' -ForegroundColor Gray
        exit 1
    }
}

Write-Ok "Quelle: $source"

# Zielverzeichnis erstellen
Write-Status "Zielverzeichnis: $TargetDir"
if (-not (Test-Path $TargetDir)) {
    New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null
}

# Dateien kopieren
Write-Status 'Kopiere erforderliche Dateien...'
$copied = 0
$missing = @()

foreach ($file in $RequiredFiles) {
    $src = Join-Path $source $file
    $dst = Join-Path $TargetDir $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $dst -Force
        Write-Status "  $file"
        $copied++
    } else {
        $missing += $file
        Write-Warn "  FEHLT: $file"
    }
}

if ($missing.Count -gt 0) {
    Write-Err "Erforderliche Dateien fehlen: $($missing -join ', ')"
    Write-Err 'Installation abgebrochen'
    exit 1
}

Write-Status 'Kopiere optionale Dateien...'
foreach ($file in $OptionalFiles) {
    $src = Join-Path $source $file
    $dst = Join-Path $TargetDir $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $dst -Force
        Write-Status "  $file"
        $copied++
    }
}

Write-Ok "$copied Dateien kopiert"

# COM-Registrierung
Write-Status 'Registriere COM-Server...'
$clmgrExe = Join-Path $TargetDir 'CLMgr.exe'

# Methode 1: /regserver
$regProc = Start-Process -FilePath $clmgrExe -ArgumentList '/regserver' -Wait -PassThru -NoNewWindow -ErrorAction SilentlyContinue
if ($regProc.ExitCode -eq 0) {
    Write-Ok 'COM-Server registriert via /regserver'
} else {
    Write-Warn '/regserver fehlgeschlagen, versuche manuelle Registrierung...'

    # Methode 2: Manuelle Registry-Eintraege
    try {
        # CLSID
        $clsidPath = "HKLM:\SOFTWARE\Classes\CLSID\$ComClsid"
        New-Item -Path $clsidPath -Force | Out-Null
        Set-ItemProperty -Path $clsidPath -Name '(default)' -Value 'Swyx CLMgr'

        New-Item -Path "$clsidPath\LocalServer32" -Force | Out-Null
        Set-ItemProperty -Path "$clsidPath\LocalServer32" -Name '(default)' -Value "`"$clmgrExe`""

        New-Item -Path "$clsidPath\ProgID" -Force | Out-Null
        Set-ItemProperty -Path "$clsidPath\ProgID" -Name '(default)' -Value $ComProgId2

        New-Item -Path "$clsidPath\VersionIndependentProgID" -Force | Out-Null
        Set-ItemProperty -Path "$clsidPath\VersionIndependentProgID" -Name '(default)' -Value $ComProgId

        New-Item -Path "$clsidPath\TypeLib" -Force | Out-Null
        Set-ItemProperty -Path "$clsidPath\TypeLib" -Name '(default)' -Value $ComTypeLib

        # AppID
        $appIdPath = "HKLM:\SOFTWARE\Classes\AppID\$ComAppId"
        New-Item -Path $appIdPath -Force | Out-Null
        Set-ItemProperty -Path $appIdPath -Name '(default)' -Value 'CLMgr'
        Set-ItemProperty -Path $appIdPath -Name 'RunAs' -Value 'Interactive User'

        Set-ItemProperty -Path $clsidPath -Name 'AppID' -Value $ComAppId

        # ProgID
        $progIdPath = "HKLM:\SOFTWARE\Classes\$ComProgId"
        New-Item -Path $progIdPath -Force | Out-Null
        Set-ItemProperty -Path $progIdPath -Name '(default)' -Value 'Swyx Client Line Manager'
        New-Item -Path "$progIdPath\CLSID" -Force | Out-Null
        Set-ItemProperty -Path "$progIdPath\CLSID" -Name '(default)' -Value $ComClsid
        New-Item -Path "$progIdPath\CurVer" -Force | Out-Null
        Set-ItemProperty -Path "$progIdPath\CurVer" -Name '(default)' -Value $ComProgId2

        $progId2Path = "HKLM:\SOFTWARE\Classes\$ComProgId2"
        New-Item -Path $progId2Path -Force | Out-Null
        Set-ItemProperty -Path $progId2Path -Name '(default)' -Value 'Swyx Client Line Manager'
        New-Item -Path "$progId2Path\CLSID" -Force | Out-Null
        Set-ItemProperty -Path "$progId2Path\CLSID" -Name '(default)' -Value $ComClsid

        Write-Ok 'COM-Server manuell registriert'
    } catch {
        Write-Err "Manuelle Registrierung fehlgeschlagen: $_"
        Write-Err 'Bitte fuehren Sie register-clmgr.reg als Administrator aus'
        exit 1
    }
}

# Verifizierung
Write-Status 'Verifiziere Installation...'
$verify = $true

# CLSID pruefen
$clsidCheck = Get-ItemProperty -Path "HKLM:\SOFTWARE\Classes\CLSID\$ComClsid\LocalServer32" -ErrorAction SilentlyContinue
if ($clsidCheck) {
    Write-Ok "CLSID registriert: $ComClsid"
} else {
    # WOW6432Node pruefen
    $clsidCheck = Get-ItemProperty -Path "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$ComClsid\LocalServer32" -ErrorAction SilentlyContinue
    if ($clsidCheck) {
        Write-Ok "CLSID registriert (WOW64): $ComClsid"
    } else {
        Write-Err 'CLSID nicht in Registry gefunden'
        $verify = $false
    }
}

# Dateien pruefen
$fileCount = (Get-ChildItem -Path $TargetDir -File).Count
Write-Ok "$fileCount Dateien in $TargetDir"

if ($verify) {
    Write-Host ''
    Write-Ok '=== Installation erfolgreich ==='
    Write-Host ''
    Write-Host "  CLMgr-Pfad: $TargetDir" -ForegroundColor Gray
    Write-Host "  CLMgr.exe startet automatisch bei COM-Aktivierung." -ForegroundColor Gray
    Write-Host "  SwyxConnect kann jetzt ohne SwyxIt! verwendet werden." -ForegroundColor Gray
    Write-Host ''
} else {
    Write-Err '=== Installation moeglicherweise unvollstaendig ==='
    Write-Host '  Bitte pruefen Sie die Fehlermeldungen oben.' -ForegroundColor Gray
    exit 1
}
