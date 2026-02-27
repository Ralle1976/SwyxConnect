# Multi-command bridge test
# Starts bridge, sends multiple commands, collects all responses

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "C:\Users\tango\Desktop\SwyIt-byRalle1976\out\bridge\SwyxBridge.exe"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

# Drain stderr async
[void][System.Threading.Tasks.Task]::Run([System.Func[string]]{ $proc.StandardError.ReadToEnd() })

# Collect stdout lines async
$outputLines = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
[void][System.Threading.Tasks.Task]::Run([Action]{
    while ($true) {
        $line = $proc.StandardOutput.ReadLine()
        if ($null -eq $line) { break }
        $outputLines.Enqueue($line)
    }
})

# Wait for bridge connect
Start-Sleep -Seconds 6

# Helper to send and wait
function Send($json) {
    $proc.StandardInput.WriteLine($json)
    $proc.StandardInput.Flush()
    Start-Sleep -Milliseconds 2000
}

# Phase 1: Set DND
Send '{"jsonrpc":"2.0","id":10,"method":"setPresence","params":{"status":"DND"}}'
Start-Sleep -Seconds 2

# Phase 2: Verify DND
Send '{"jsonrpc":"2.0","id":11,"method":"getPresence"}'

# Phase 3: Reset Available  
Send '{"jsonrpc":"2.0","id":12,"method":"setPresence","params":{"status":"Available"}}'
Start-Sleep -Seconds 2

# Phase 4: Verify Available
Send '{"jsonrpc":"2.0","id":13,"method":"getPresence"}'

# Phase 5: Call History
Send '{"jsonrpc":"2.0","id":14,"method":"getCallHistory"}'

# Phase 6: Voicemails
Send '{"jsonrpc":"2.0","id":15,"method":"getVoicemails"}'

# Wait for last responses
Start-Sleep -Seconds 3

# Print all JSON-RPC responses with id
$result = $null
while ($outputLines.TryDequeue([ref]$result)) {
    if ($result -match '"id"\s*:\s*(\d+)') {
        $idVal = $Matches[1]
        switch ($idVal) {
            "10" { Write-Host "SET DND:         $result" }
            "11" { Write-Host "VERIFY DND:      $result" }
            "12" { Write-Host "SET AVAILABLE:   $result" }
            "13" { Write-Host "VERIFY AVAIL:    $result" }
            "14" { Write-Host "CALL HISTORY:    $result" }
            "15" { Write-Host "VOICEMAILS:      $result" }
            default { Write-Host "OTHER (id=$idVal): $result" }
        }
    }
}

$proc.Kill()
$proc.WaitForExit(3000)
Write-Host "=== DONE ==="
