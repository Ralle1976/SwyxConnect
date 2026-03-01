# Bridge test: send all commands, close stdin, read all output
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "C:\Users\tango\Desktop\SwyIt-byRalle1976\out\bridge\SwyxBridge.exe"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

# Read stderr async
$null = [System.Threading.Tasks.Task]::Run([System.Func[string]] { $proc.StandardError.ReadToEnd() })

# Wait for bridge to connect
Start-Sleep -Seconds 5

# Send all commands with small delays between
$commands = @(
    '{"jsonrpc":"2.0","id":1,"method":"getPresence"}'
    '{"jsonrpc":"2.0","id":2,"method":"setPresence","params":{"status":"DND"}}'
)

foreach ($cmd in $commands) {
    $proc.StandardInput.WriteLine($cmd)
    $proc.StandardInput.Flush()
    Start-Sleep -Milliseconds 1500
}

# Wait for DND to take effect, then verify
Start-Sleep -Seconds 2

$commands2 = @(
    '{"jsonrpc":"2.0","id":3,"method":"getPresence"}'
    '{"jsonrpc":"2.0","id":4,"method":"setPresence","params":{"status":"Available"}}'
)

foreach ($cmd in $commands2) {
    $proc.StandardInput.WriteLine($cmd)
    $proc.StandardInput.Flush()
    Start-Sleep -Milliseconds 1500
}

Start-Sleep -Seconds 2

$commands3 = @(
    '{"jsonrpc":"2.0","id":5,"method":"getPresence"}'
    '{"jsonrpc":"2.0","id":6,"method":"getCallHistory"}'
    '{"jsonrpc":"2.0","id":7,"method":"getVoicemails"}'
)

foreach ($cmd in $commands3) {
    $proc.StandardInput.WriteLine($cmd)
    $proc.StandardInput.Flush()
    Start-Sleep -Milliseconds 1500
}

# Close stdin to trigger EOF
$proc.StandardInput.Close()

# Read all output
Start-Sleep -Seconds 2
$output = $proc.StandardOutput.ReadToEnd()

# Parse and display results
$lines = $output -split "`n" | Where-Object { $_.Trim() -ne "" }
foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if ($trimmed -match '"id"\s*:\s*(\d+)') {
        $id = $matches[1]
        switch ($id) {
            "1" { Write-Host "TEST 1 - Initial Presence: $trimmed" }
            "2" { Write-Host "TEST 2 - Set DND:          $trimmed" }
            "3" { Write-Host "TEST 3 - Verify DND:       $trimmed" }
            "4" { Write-Host "TEST 4 - Set Available:     $trimmed" }
            "5" { Write-Host "TEST 5 - Verify Available:  $trimmed" }
            "6" { Write-Host "TEST 6 - Call History:      $trimmed" }
            "7" { Write-Host "TEST 7 - Voicemails:        $trimmed" }
        }
    }
}

Write-Host ""
Write-Host "=== ALL TESTS COMPLETE ==="

if (!$proc.HasExited) { try { $proc.Kill() } catch {} }
