# Monitor process starts during a dial operation - PS 5.1 compatible
$ErrorActionPreference = 'Continue'

Write-Host "=== Process Start Monitor (30s) ===" -ForegroundColor Cyan
Write-Host ""

# Record existing PIDs
$seenBefore = @{}
Get-Process | ForEach-Object { $seenBefore[$_.Id] = $_.ProcessName }

$newProcesses = @()

for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    $current = Get-Process -ErrorAction SilentlyContinue
    foreach ($p in $current) {
        if (-not $seenBefore.ContainsKey($p.Id)) {
            try {
                $wmi = Get-WmiObject Win32_Process -Filter "ProcessId=$($p.Id)" -ErrorAction SilentlyContinue
                $cmdLine = if ($wmi -and $wmi.CommandLine) { $wmi.CommandLine } else { "(unknown)" }
                $parentPid = if ($wmi -and $wmi.ParentProcessId) { $wmi.ParentProcessId } else { 0 }
                $parentName = "(unknown)"
                if ($parentPid -gt 0) {
                    try {
                        $parent = Get-Process -Id $parentPid -ErrorAction SilentlyContinue
                        if ($parent) { $parentName = $parent.ProcessName } else { $parentName = "(exited)" }
                    } catch { $parentName = "(error)" }
                }

                $ts = (Get-Date).ToString('HH:mm:ss.fff')
                Write-Host "[$ts] NEW: $($p.ProcessName) (PID=$($p.Id), Parent=$parentName/$parentPid)" -ForegroundColor Green
                Write-Host "  CmdLine: $cmdLine" -ForegroundColor Gray
                $newProcesses += [PSCustomObject]@{
                    Time = $ts
                    Name = $p.ProcessName
                    PID = $p.Id
                    Parent = $parentName
                    ParentPID = $parentPid
                    CmdLine = $cmdLine
                }
                $seenBefore[$p.Id] = $p.ProcessName
            } catch {
                # Process already exited
            }
        }
    }
}

Write-Host ""
Write-Host "=== Summary: $($newProcesses.Count) new processes ===" -ForegroundColor Cyan

# Check specifically for SwyxIt
$swyxIt = $newProcesses | Where-Object { $_.Name -like "*SwyxIt*" }
if ($swyxIt) {
    Write-Host ""
    Write-Host "!!! SWYXIT! PROCESS STARTED !!!" -ForegroundColor Red
    $swyxIt | Format-List Time, Name, PID, Parent, ParentPID, CmdLine
} else {
    Write-Host ""
    Write-Host "No SwyxIt! process started during monitoring." -ForegroundColor Green
}
