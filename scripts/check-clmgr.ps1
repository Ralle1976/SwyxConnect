<#
.SYNOPSIS
    Diagnose-Script fuer SwyxConnect CLMgr-Verbindung

.DESCRIPTION
    Prueft ob CLMgr verfuegbar ist, COM-Registrierung vorhanden ist,
    und ob eine Verbindung zum Swyx-Server moeglich ist.

.EXAMPLE
    .\check-clmgr.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'SilentlyContinue'

function Write-Status($msg) { Write-Host "  [*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)     { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg)   { Write-Host "  [!] $msg" -ForegroundColor Yellow }
function Write-Err($msg)    { Write-Host "  [X] $msg" -ForegroundColor Red }

Write-Host "`n=== SwyxConnect CLMgr Diagnose ===" -ForegroundColor Magenta
Write-Host ''

# ── 1. SwyxIt! Installation ─────────────────────────────────────────────────

Write-Host '  1. SwyxIt! Installation' -ForegroundColor White
$swyxitPaths = @(
    "${env:ProgramFiles(x86)}\Swyx\SwyxIt!",
    "$env:ProgramFiles\Swyx\SwyxIt!"
)
$swyxitFound = $false
foreach ($p in $swyxitPaths) {
    if (Test-Path (Join-Path $p 'SwyxIt!.exe')) {
        Write-Ok "SwyxIt! gefunden: $p"
        $swyxitFound = $true

        # Version pruefen
        $ver = (Get-Item (Join-Path $p 'SwyxIt!.exe')).VersionInfo.ProductVersion
        if ($ver) { Write-Status "Version: $ver" }
        break
    }
}
if (-not $swyxitFound) {
    Write-Warn 'SwyxIt! nicht installiert (Standalone-Modus erforderlich)'
}

# ── 2. CLMgr.exe ────────────────────────────────────────────────────────────

Write-Host ''
Write-Host '  2. CLMgr.exe' -ForegroundColor White

$clmgrLocations = @(
    "${env:ProgramFiles(x86)}\Swyx\SwyxIt!\CLMgr.exe",
    "$env:ProgramFiles\Swyx\SwyxIt!\CLMgr.exe",
    (Join-Path $env:LOCALAPPDATA 'SwyxConnect\clmgr\CLMgr.exe')
)
$clmgrPath = $null
foreach ($p in $clmgrLocations) {
    if (Test-Path $p) {
        $clmgrPath = $p
        Write-Ok "CLMgr.exe gefunden: $p"
        $ver = (Get-Item $p).VersionInfo.ProductVersion
        if ($ver) { Write-Status "Version: $ver" }
        break
    }
}
if (-not $clmgrPath) {
    Write-Err 'CLMgr.exe nicht gefunden!'
    Write-Host '  Fuehren Sie setup-clmgr.ps1 aus oder installieren Sie SwyxIt!' -ForegroundColor Gray
}

# ── 3. CLMgr Prozess ────────────────────────────────────────────────────────

Write-Host ''
Write-Host '  3. Laufende Prozesse' -ForegroundColor White

$clmgrProc = Get-Process -Name 'CLMgr' -ErrorAction SilentlyContinue
$swyxitProc = Get-Process -Name 'SwyxIt!' -ErrorAction SilentlyContinue

if ($clmgrProc) {
    Write-Ok "CLMgr laeuft (PID: $($clmgrProc.Id))"
} else {
    Write-Warn 'CLMgr laeuft nicht (wird bei COM-Aktivierung automatisch gestartet)'
}

if ($swyxitProc) {
    Write-Ok "SwyxIt! laeuft (PID: $($swyxitProc.Id))"
} else {
    Write-Status 'SwyxIt! laeuft nicht'
}

# ── 4. COM-Registrierung ────────────────────────────────────────────────────

Write-Host ''
Write-Host '  4. COM-Registrierung' -ForegroundColor White

$comClsid = '{F8E552F8-4C00-11D3-80BC-00105A653379}'
$clsidPaths = @(
    "HKLM:\SOFTWARE\Classes\CLSID\$comClsid",
    "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$comClsid",
    "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$comClsid"
)

$comRegistered = $false
foreach ($regPath in $clsidPaths) {
    $localServer = Get-ItemProperty -Path "$regPath\LocalServer32" -ErrorAction SilentlyContinue
    if ($localServer) {
        Write-Ok "COM CLSID registriert: $comClsid"
        Write-Status "LocalServer32: $($localServer.'(default)')"
        $comRegistered = $true
        break
    }
}

if (-not $comRegistered) {
    Write-Err 'COM CLSID nicht registriert!'
    Write-Host '  Fuehren Sie setup-clmgr.ps1 als Administrator aus' -ForegroundColor Gray
}

# ProgID pruefen
$progId = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Classes\CLMgr.ClientLineMgr\CLSID' -ErrorAction SilentlyContinue
if ($progId) {
    Write-Ok "ProgID registriert: CLMgr.ClientLineMgr"
} else {
    Write-Warn 'ProgID CLMgr.ClientLineMgr nicht gefunden'
}

# ── 5. Netzwerk-Ports ───────────────────────────────────────────────────────

Write-Host ''
Write-Host '  5. Lokale Ports (CLMgr)' -ForegroundColor White

$ports = @(
    @{ Port = 9094;  Proto = 'TCP'; Name = 'CDS (WCF net.tcp)' },
    @{ Port = 9100;  Proto = 'TCP'; Name = 'Windows-Login (WSHttp)' },
    @{ Port = 5060;  Proto = 'UDP'; Name = 'SIP (CLMgr Proxy)' },
    @{ Port = 12042; Proto = 'TCP'; Name = 'CLMgr intern' }
)

foreach ($p in $ports) {
    try {
        if ($p.Proto -eq 'TCP') {
            $conn = Test-NetConnection -ComputerName 127.0.0.1 -Port $p.Port -WarningAction SilentlyContinue
            if ($conn.TcpTestSucceeded) {
                Write-Ok "$($p.Name): Port $($p.Port)/$($p.Proto) OFFEN"
            } else {
                Write-Warn "$($p.Name): Port $($p.Port)/$($p.Proto) geschlossen"
            }
        } else {
            Write-Status "$($p.Name): Port $($p.Port)/$($p.Proto) (UDP, kein direkter Test)"
        }
    } catch {
        Write-Warn "$($p.Name): Port $($p.Port)/$($p.Proto) - Test fehlgeschlagen"
    }
}

# ── 6. .NET Runtime ─────────────────────────────────────────────────────────

Write-Host ''
Write-Host '  6. .NET Runtime' -ForegroundColor White

$dotnetExe = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetExe) {
    $runtimes = & dotnet --list-runtimes 2>&1 | Where-Object { $_ -match 'Microsoft.NETCore.App' }
    foreach ($rt in $runtimes) {
        if ($rt -match '(\d+\.\d+\.\d+)') {
            Write-Ok ".NET Runtime: $($Matches[1])"
        }
    }

    # x86 Runtime pruefen
    $x86Dotnet = "${env:ProgramFiles(x86)}\dotnet\dotnet.exe"
    if (Test-Path $x86Dotnet) {
        Write-Ok ".NET x86 Runtime verfuegbar"
    } else {
        Write-Warn '.NET x86 Runtime nicht gefunden (wird fuer SwyxBridge benoetigt)'
    }
} else {
    Write-Err '.NET nicht installiert!'
}

# ── 7. SwyxBridge ───────────────────────────────────────────────────────────

Write-Host ''
Write-Host '  7. SwyxBridge.exe' -ForegroundColor White

$bridgePaths = @(
    'C:\temp\SwyxBridge\SwyxBridge.exe',
    (Join-Path $PSScriptRoot '..\out\bridge\SwyxBridge.exe')
)
foreach ($bp in $bridgePaths) {
    if (Test-Path $bp) {
        Write-Ok "SwyxBridge.exe: $bp"
        $ver = (Get-Item $bp).VersionInfo.ProductVersion
        if ($ver) { Write-Status "Version: $ver" }
        break
    }
}

# ── Zusammenfassung ──────────────────────────────────────────────────────────

Write-Host ''
Write-Host '  === Zusammenfassung ===' -ForegroundColor White

$ready = $true
if (-not $clmgrPath -and -not $comRegistered) {
    Write-Err 'CLMgr nicht verfuegbar — SwyxConnect kann nicht starten'
    $ready = $false
}
if (-not $comRegistered) {
    Write-Err 'COM nicht registriert — CLMgr kann nicht per COM aktiviert werden'
    $ready = $false
}

if ($ready) {
    Write-Host ''
    Write-Ok 'System ist bereit fuer SwyxConnect'
    Write-Host ''
} else {
    Write-Host ''
    Write-Err 'System ist NICHT bereit — siehe Fehler oben'
    Write-Host ''
}
