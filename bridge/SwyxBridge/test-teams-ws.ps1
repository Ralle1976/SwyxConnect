$port = 8124
$url = "ws://127.0.0.1:$port/?token=&protocol-version=2.0.0&manufacturer=Tester&device=TestDevice&app=TestApp&app-version=1.0.0"

Add-Type -AssemblyName System.Net.WebSockets.Client

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$cts = New-Object System.Threading.CancellationTokenSource
$uri = New-Object System.Uri($url)

Write-Host "Connecting to $url ..."
try {
    $ws.ConnectAsync($uri, $cts.Token).Wait()
    Write-Host "Connected! State: $($ws.State)"
    
    $buffer = New-Object byte[] 4096
    $segment = New-Object System.ArraySegment[byte]($buffer)
    
    $receiveTask = $ws.ReceiveAsync($segment, $cts.Token)
    Write-Host "Waiting for response (Check your Teams window for a pairing prompt)..."
    
    if ($receiveTask.Wait(5000)) {
        $result = $receiveTask.Result
        $message = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
        Write-Host "Received: $message"
    } else {
        Write-Host "No immediate response after 5 seconds."
    }
    
    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", $cts.Token).Wait()
} catch {
    Write-Host "Error connecting to port $port : $_"
}

$port = 8125
$url = "ws://127.0.0.1:$port/?token=&protocol-version=2.0.0&manufacturer=Tester&device=TestDevice&app=TestApp&app-version=1.0.0"
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$uri = New-Object System.Uri($url)

Write-Host "`nConnecting to $url ..."
try {
    $ws.ConnectAsync($uri, $cts.Token).Wait()
    Write-Host "Connected! State: $($ws.State)"
} catch {
    Write-Host "Error connecting to port $port : $_"
}

