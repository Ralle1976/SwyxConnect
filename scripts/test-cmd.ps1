# Send a single JSON-RPC command to bridge and get the response
# Usage: test-cmd.ps1 -Cmd '{"jsonrpc":"2.0","id":1,"method":"getPresence"}'
param([string]$Cmd)

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "C:\Users\tango\Desktop\SwyIt-byRalle1976\out\bridge\SwyxBridge.exe"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

# Read stderr async to prevent buffer deadlock
$null = [System.Threading.Tasks.Task]::Run([System.Func[string]]{ $proc.StandardError.ReadToEnd() })

# Read stdout lines async using ReadLine in a loop
$lines = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
$null = [System.Threading.Tasks.Task]::Run([Action]{
    $sr = $proc.StandardOutput
    while ($true) {
        $line = $sr.ReadLine()
        if ($null -eq $line) { break }
        $lines.Add($line)
    }
})

# Wait for bridge to connect
Start-Sleep -Seconds 6

# Send command
$proc.StandardInput.WriteLine($Cmd)
$proc.StandardInput.Flush()

# Wait for response
Start-Sleep -Seconds 4

# Print lines containing "id"
foreach ($l in $lines) {
    if ($l -match '"id"') {
        Write-Host $l
    }
}

# Kill bridge
$proc.Kill()
$proc.WaitForExit(3000)
