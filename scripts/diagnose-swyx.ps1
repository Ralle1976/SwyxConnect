# SwyxIt! Connection Diagnostics — DATENSCHUTZ-KONFORM
# Namen/Nummern werden maskiert: "Max Mustermann" → "M. M. (12)"
# Run: C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File scripts\diagnose-swyx.ps1

$ErrorActionPreference = "SilentlyContinue"

function Mask-Name($n) {
    if ([string]::IsNullOrWhiteSpace($n)) { return "" }
    $parts = $n.Trim() -split '\s+'
    $result = ($parts | ForEach-Object { $_.Substring(0,1) + "." }) -join " "
    return "$result ($($n.Length))"
}

function Mask-Number($num) {
    if ([string]::IsNullOrWhiteSpace($num)) { return "" }
    if ($num.Length -le 2) { return "**" }
    return $num.Substring(0,2) + "***" + $num.Substring($num.Length-1)
}

Write-Host ""
Write-Host "=== SwyxIt! / CLMgr Diagnostics (datenschutzkonform) ===" -ForegroundColor Cyan
Write-Host ""

$type = [Type]::GetTypeFromProgID('CLMgr.ClientLineMgr')
if ($type -eq $null) { Write-Host "ERROR: CLMgr COM not found" -ForegroundColor Red; exit 1 }

$obj = [Activator]::CreateInstance($type)
Write-Host "COM object created." -ForegroundColor Green
Write-Host ""

function SafeRead($label, $sb) {
    try { $val = & $sb; Write-Host "  $label = $val" } catch { Write-Host "  $label = [n/a]" }
}

# ─── Session ───
Write-Host "--- Session ---" -ForegroundColor Yellow
SafeRead "IsServerUp"     { $obj.DispIsServerUp }
SafeRead "IsLoggedIn"     { $obj.DispIsLoggedIn }
SafeRead "AuthMode"       { $obj.DispGetCurrentAuthMode }
SafeRead "CurrentUser"    { $obj.DispGetCurrentUser }
SafeRead "CurrentServer"  { $obj.DispGetCurrentServer }
Write-Host ""

# ─── RemoteConnector ───
Write-Host "--- RemoteConnector ---" -ForegroundColor Yellow
SafeRead "CloudConnectorServer" { $obj.CloudConnectorServer }
SafeRead "CloudConnectorStatus" { $obj.CloudConnectorStatus }
Write-Host "  GetRcEndpoints:" -NoNewline -ForegroundColor Gray
try {
    $a="";$b="";$c="";$d=""
    $obj.GetRcEndpoints([ref]$a,[ref]$b,[ref]$c,[ref]$d)
    Write-Host ""
    Write-Host "    PublicAuth: $a"
    Write-Host "    PublicRc:   $c"
} catch { Write-Host " [failed]" -ForegroundColor DarkGray }
Write-Host ""

# ─── Lines ───
Write-Host "--- Lines ---" -ForegroundColor Yellow
SafeRead "NumberOfLines"       { $obj.DispNumberOfLines }
SafeRead "SelectedLineNumber"  { $obj.DispSelectedLineNumber }
Write-Host ""

# ─── SpeedDials (maskiert) ───
Write-Host "--- SpeedDials (maskiert) ---" -ForegroundColor Yellow
try {
    $numSD = [int]$obj.DispNumberOfSpeedDials
    $named = 0
    $offline = 0; $available = 0; $busy = 0; $dnd = 0; $away = 0; $empty = 0
    $samples = @()
    if ($numSD -gt 0) {
        for ($i = 0; $i -lt $numSD; $i++) {
            $n = $obj.DispSpeedDialName($i)
            $num = $obj.DispSpeedDialNumber($i)
            $st = [int]$obj.DispSpeedDialState($i)
            if ([string]::IsNullOrWhiteSpace($n)) { $empty++; continue }
            $named++
            switch ($st) {
                0 { $offline++ }
                1 { $offline++ }
                2 { $available++ }
                3 { $busy++ }
                4 { $dnd++ }
                5 { $away++ }
            }
            if ($samples.Count -lt 5) {
                $samples += "    [$i] $(Mask-Name $n) ext=$(Mask-Number $num) state=$st"
            }
        }
    }
    Write-Host "  Total slots: $numSD"
    Write-Host "  Named colleagues: $named (empty slots: $empty)"
    Write-Host "  Status: Offline=$offline, Available=$available, Busy=$busy, DND=$dnd, Away=$away"
    Write-Host "  Samples (masked):"
    $samples | ForEach-Object { Write-Host $_ -ForegroundColor Gray }
} catch { Write-Host "  [n/a]" }
Write-Host ""

# ─── UserAppearances ───
Write-Host "--- UserAppearances ---" -ForegroundColor Yellow
try {
    $app = $obj.GetUserAppearances()
    if ($app -eq $null) {
        Write-Host "  GetUserAppearances = null (SpeedDials sind die einzige Quelle)"
    } else {
        $cnt = 0; try { $cnt = [int]$app.Count } catch {}
        Write-Host "  GetUserAppearances count = $cnt"
    }
} catch { Write-Host "  [n/a]" }
Write-Host ""

# ─── Phonebook Search (test ob mehr Kollegen gefunden werden) ───
Write-Host "--- Phonebook Search Test ---" -ForegroundColor Yellow
try {
    $plugins = $obj.GetContactDataPlugIns()
    if ($plugins -ne $null) {
        Write-Host "  ContactDataPlugIns available: $($plugins.Count)"
    } else {
        Write-Host "  GetContactDataPlugIns = null"
    }
} catch { Write-Host "  [n/a]" }
Write-Host ""

# ─── History count ───
Write-Host "--- History (count only) ---" -ForegroundColor Yellow
try {
    $cfg = $obj.DispClientConfig
    $enum = $cfg.CallerEnumerator
    $hc = 0; try { $hc = [int]$enum.Count } catch {}
    Write-Host "  CallerEnumerator entries: $hc"
    Write-Host "  NumberOfNewVoicemails: $($cfg.NumberOfNewVoicemails)"
} catch { Write-Host "  [n/a]" }
Write-Host ""

Write-Host "=== Done ===" -ForegroundColor Cyan
