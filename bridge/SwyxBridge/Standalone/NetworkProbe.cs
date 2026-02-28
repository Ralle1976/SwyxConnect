using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using SwyxBridge.Utils;

namespace SwyxBridge.Standalone;

/// <summary>
/// Netzwerk-Probes für SwyxWare Server-Erkennung.
/// Testet CDS (localhost:9094), SIP (localhost:5060), RemoteConnector-Tunnel (RC0321:15021).
/// Läuft als Windows x86 EXE und kann localhost-only Ports von CLMgr erreichen.
/// </summary>
public static class NetworkProbe
{
    /// <summary>
    /// Vollständige Netzwerk-Analyse: lokale CLMgr-Ports + öffentliche Server.
    /// </summary>
    public static async Task<object> ProbeAllAsync(string? publicServer = null, int publicSipPort = 15021, int publicAuthPort = 8021)
    {
        var results = new Dictionary<string, object>();

        // --- Lokale CLMgr-Ports (nur erreichbar wenn CLMgr.exe läuft) ---
        var localCds = await ProbeTcpPortAsync("127.0.0.1", 9094, "CDS-TCP");
        results["localCds9094"] = localCds;

        var localSipUdp = await ProbeUdpSipAsync("127.0.0.1", 5060);
        results["localSip5060"] = localSipUdp;

        var localSipRegister = await ProbeSipRegisterAsync("127.0.0.1", 5060, "Ralf Arnold");
        results["localSipRegister5060"] = localSipRegister;

        var localPort9100 = await ProbeTcpPortAsync("127.0.0.1", 9100, "CLMgr-9100");
        results["localPort9100"] = localPort9100;

        // CDS-Introspection: Wenn Port 9094 offen, versuche WCF-Handshake
        if (localCds is Dictionary<string, object> cdsDict && cdsDict.ContainsKey("open") && (bool)cdsDict["open"])
        {
            var cdsData = await ProbeCdsProtocolAsync("127.0.0.1", 9094);
            results["cdsProtocol"] = cdsData;
        }

        // --- Öffentliche Server ---
        if (!string.IsNullOrEmpty(publicServer))
        {
            // SIP-Proxy Port (TCP)
            var publicSip = await ProbeTcpPortAsync(publicServer, publicSipPort, "PublicSIP-TCP");
            results[$"public_{publicServer}_{publicSipPort}"] = publicSip;

            // Versuche SIP OPTIONS über TCP
            var sipOptions = await ProbeSipOptionsTcpAsync(publicServer, publicSipPort);
            results["publicSipOptions"] = sipOptions;

            // Auth-Server (HTTPS)
            var authProbe = await ProbeHttpsAsync(publicServer, publicAuthPort);
            results[$"publicAuth_{publicServer}_{publicAuthPort}"] = authProbe;

            // RemoteConnector Tunnel-Analyse: erste Bytes lesen
            var tunnelProbe = await ProbeTunnelProtocolAsync(publicServer, publicSipPort);
            results["tunnelProtocol"] = tunnelProbe;
        }

        // --- Prozesse ---
        var processes = ProbeProcesses();
        results["processes"] = processes;

        return new { ok = true, stage = "networkProbe", results };
    }

    /// <summary>TCP-Port Probe mit Timeout.</summary>
    private static async Task<object> ProbeTcpPortAsync(string host, int port, string label)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
            {
                await connectTask; // propagate exception if any
                // Versuche erste Bytes zu lesen (Server-Banner?)
                var stream = client.GetStream();
                stream.ReadTimeout = 2000;
                var buf = new byte[512];
                int read = 0;
                try
                {
                    // Manche Server senden sofort ein Banner
                    if (stream.DataAvailable)
                        read = await stream.ReadAsync(buf, 0, buf.Length);
                }
                catch { }

                return new Dictionary<string, object>
                {
                    ["open"] = true,
                    ["label"] = label,
                    ["bannerBytes"] = read,
                    ["bannerHex"] = read > 0 ? BitConverter.ToString(buf, 0, Math.Min(read, 64)) : "",
                    ["bannerText"] = read > 0 ? Encoding.ASCII.GetString(buf, 0, Math.Min(read, 128)).Replace("\0", ".") : ""
                };
            }
            return new Dictionary<string, object> { ["open"] = false, ["label"] = label, ["reason"] = "timeout" };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object> { ["open"] = false, ["label"] = label, ["reason"] = ex.Message };
        }
    }

    /// <summary>UDP SIP OPTIONS Probe.</summary>
    private static async Task<object> ProbeUdpSipAsync(string host, int port)
    {
        try
        {
            using var udp = new UdpClient();
            var optionsMsg = $"OPTIONS sip:{host}:{port} SIP/2.0\r\n" +
                             $"Via: SIP/2.0/UDP 127.0.0.1:15060;branch=z9hG4bK-probe-{Environment.TickCount}\r\n" +
                             $"Max-Forwards: 70\r\n" +
                             $"From: <sip:probe@127.0.0.1>;tag=p{Environment.TickCount}\r\n" +
                             $"To: <sip:{host}:{port}>\r\n" +
                             $"Call-ID: probe-{Environment.TickCount}@127.0.0.1\r\n" +
                             $"CSeq: 1 OPTIONS\r\n" +
                             $"Contact: <sip:probe@127.0.0.1:15060>\r\n" +
                             $"Accept: application/sdp\r\n" +
                             $"Content-Length: 0\r\n\r\n";

            var data = Encoding.UTF8.GetBytes(optionsMsg);
            await udp.SendAsync(data, data.Length, host, port);

            // Warte auf Antwort
            udp.Client.ReceiveTimeout = 3000;
            var recvTask = udp.ReceiveAsync();
            if (await Task.WhenAny(recvTask, Task.Delay(3000)) == recvTask)
            {
                var result = await recvTask;
                var responseText = Encoding.UTF8.GetString(result.Buffer);
                var firstLine = responseText.Split('\n')[0].Trim();

                return new { open = true, protocol = "SIP/UDP", response = firstLine, fullLength = result.Buffer.Length, responseText = responseText.Length > 500 ? responseText[..500] : responseText };
            }
            return new { open = false, protocol = "SIP/UDP", reason = "timeout (no response)" };
        }
        catch (Exception ex) { return new { open = false, protocol = "SIP/UDP", reason = ex.Message }; }
    }

    /// <summary>TCP SIP OPTIONS Probe.</summary>
    private static async Task<object> ProbeSipOptionsTcpAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                return new { open = false, protocol = "SIP/TCP", reason = "connect timeout" };
            await connectTask;

            var stream = client.GetStream();
            stream.ReadTimeout = 5000;
            stream.WriteTimeout = 3000;

            var optionsMsg = $"OPTIONS sip:{host} SIP/2.0\r\n" +
                             $"Via: SIP/2.0/TCP 10.0.0.1:5060;branch=z9hG4bK-probe-{Environment.TickCount}\r\n" +
                             $"Max-Forwards: 70\r\n" +
                             $"From: <sip:probe@10.0.0.1>;tag=p{Environment.TickCount}\r\n" +
                             $"To: <sip:{host}>\r\n" +
                             $"Call-ID: probe-{Environment.TickCount}@10.0.0.1\r\n" +
                             $"CSeq: 1 OPTIONS\r\n" +
                             $"Contact: <sip:probe@10.0.0.1:5060>\r\n" +
                             $"Content-Length: 0\r\n\r\n";

            var data = Encoding.UTF8.GetBytes(optionsMsg);
            await stream.WriteAsync(data);
            await stream.FlushAsync();

            // Lese Antwort
            var buf = new byte[4096];
            var readTask = stream.ReadAsync(buf, 0, buf.Length);
            if (await Task.WhenAny(readTask, Task.Delay(5000)) == readTask)
            {
                int read = await readTask;
                if (read > 0)
                {
                    var responseText = Encoding.UTF8.GetString(buf, 0, read);
                    var firstLine = responseText.Split('\n')[0].Trim();
                    return new { open = true, protocol = "SIP/TCP", sipResponse = firstLine, responseLength = read, responseHex = BitConverter.ToString(buf, 0, Math.Min(read, 64)), responseText = responseText.Length > 800 ? responseText[..800] : responseText };
                }
                return new { open = true, protocol = "SIP/TCP", sipResponse = "(empty)", responseLength = 0, responseHex = "", responseText = "" };
            }
            return new { open = true, protocol = "SIP/TCP", sipResponse = "(timeout reading)", responseLength = 0, responseHex = "", responseText = "" };
        }
        catch (Exception ex) { return new { open = false, protocol = "SIP/TCP", reason = ex.Message, sipResponse = "", responseLength = 0, responseHex = "", responseText = "" }; }
    }

    /// <summary>HTTPS Probe auf Auth-Server.</summary>
    private static async Task<object> ProbeHttpsAsync(string host, int port)
    {
        var results = new Dictionary<string, object>();
        try
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

            // Teste verschiedene Pfade
            foreach (var path in new[] { "/", "/IpPbx", "/IpPbx/WebClient", "/IpPbx/auth", "/IpPbx/REST", "/IpPbx/CDS", "/IpPbx/RemoteConnector", "/IpPbx/PhoneConnector" })
            {
                try
                {
                    var resp = await http.GetAsync($"https://{host}:{port}{path}");
                    var headers = new Dictionary<string, string>();
                    foreach (var h in resp.Headers)
                        headers[h.Key] = string.Join(", ", h.Value);
                    var body = "";
                    try { body = await resp.Content.ReadAsStringAsync(); if (body.Length > 200) body = body[..200]; } catch { }

                    results[path] = new { status = (int)resp.StatusCode, statusText = resp.StatusCode.ToString(), headers, body };
                }
                catch (Exception ex) { results[path] = new { status = 0, error = ex.Message }; }
            }
        }
        catch (Exception ex) { results["error"] = ex.Message; }
        return results;
    }

    /// <summary>
    /// RemoteConnector Tunnel-Protokoll: Verbinde TCP, sende nichts, lese erste Bytes.
    /// Dann sende CLMgr-typische Handshake-Bytes.
    /// </summary>
    private static async Task<object> ProbeTunnelProtocolAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                return new { connected = false, reason = "timeout" };
            await connectTask;

            var stream = client.GetStream();
            stream.ReadTimeout = 3000;

            // Phase 1: Lese ob Server sofort etwas sendet
            await Task.Delay(500);
            var buf = new byte[1024];
            int serverBanner = 0;
            if (stream.DataAvailable)
            {
                serverBanner = await stream.ReadAsync(buf, 0, buf.Length);
            }

            // Phase 2: Sende typische CLMgr-Handshake Patterns
            // CLMgr verbindet sich über TCP und sendet vermutlich eine TLS ClientHello
            // oder ein proprietäres Swyx-Framing-Protokoll
            var probes = new List<object>();

            // Probe A: TLS ClientHello (Version 1.2)
            var tlsClientHello = new byte[] { 0x16, 0x03, 0x01, 0x00, 0x05, 0x01, 0x00, 0x00, 0x01, 0x00 };
            try
            {
                await stream.WriteAsync(tlsClientHello);
                await stream.FlushAsync();
                await Task.Delay(1000);
                int read = 0;
                if (stream.DataAvailable)
                    read = await stream.ReadAsync(buf, 0, buf.Length);
                probes.Add(new
                {
                    probe = "TLS_ClientHello",
                    sent = BitConverter.ToString(tlsClientHello),
                    receivedBytes = read,
                    receivedHex = read > 0 ? BitConverter.ToString(buf, 0, Math.Min(read, 64)) : "",
                    receivedText = read > 0 ? Encoding.ASCII.GetString(buf, 0, Math.Min(read, 128)).Replace("\0", ".") : ""
                });
            }
            catch (Exception ex) { probes.Add(new { probe = "TLS_ClientHello", error = ex.Message }); }

            return new
            {
                connected = true,
                serverBannerBytes = serverBanner,
                serverBannerHex = serverBanner > 0 ? BitConverter.ToString(buf, 0, Math.Min(serverBanner, 64)) : "",
                serverBannerText = serverBanner > 0 ? Encoding.ASCII.GetString(buf, 0, Math.Min(serverBanner, 128)).Replace("\0", ".") : "",
                probes
            };
        }
        catch (Exception ex) { return new { connected = false, reason = ex.Message }; }
    }

    /// <summary>CDS-Protokoll-Introspection auf Port 9094.</summary>
    private static async Task<object> ProbeCdsProtocolAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            var stream = client.GetStream();
            stream.ReadTimeout = 3000;

            // CDS ist WCF/net.tcp — sende net.tcp Preamble
            // net.tcp Protokoll: Version(1) Mode(1) ViaLength(varInt) Via(string)
            var via = $"net.tcp://{host}:{port}/";
            var viaBytes = Encoding.UTF8.GetBytes(via);

            var preamble = new List<byte>();
            preamble.Add(0x00); // Version Record (Preamble)
            preamble.Add(0x01); // Major Version 1
            preamble.Add(0x00); // Minor Version 0
            preamble.Add(0x01); // Mode: Singleton-Unsized (Duplex)
            preamble.Add(0x02); // Via Record
            preamble.Add((byte)viaBytes.Length); // Via length
            preamble.AddRange(viaBytes);
            preamble.Add(0x03); // Known Encoding Record
            preamble.Add(0x08); // Binary with in-band dictionary

            var preambleArr = preamble.ToArray();
            await stream.WriteAsync(preambleArr);
            await stream.FlushAsync();

            await Task.Delay(1000);
            var buf = new byte[1024];
            int read = 0;
            if (stream.DataAvailable)
                read = await stream.ReadAsync(buf, 0, buf.Length);

            return new
            {
                protocol = "WCF/net.tcp",
                preambleSent = BitConverter.ToString(preambleArr),
                responseBytes = read,
                responseHex = read > 0 ? BitConverter.ToString(buf, 0, Math.Min(read, 64)) : "",
                responseText = read > 0 ? Encoding.ASCII.GetString(buf, 0, Math.Min(read, 128)).Replace("\0", ".") : "",
                // net.tcp response: 0x0B = Preamble Ack
                isPreambleAck = read > 0 && buf[0] == 0x0B
            };
        }
        catch (Exception ex) { return new { protocol = "WCF/net.tcp", error = ex.Message }; }
    }

    /// <summary>
    /// SIP REGISTER Probe über UDP. Sendet unauthentifiziertes REGISTER
    /// und analysiert die 401-Antwort (WWW-Authenticate Header = Auth-Mechanismus).
    /// </summary>
    public static async Task<object> ProbeSipRegisterAsync(string host, int port, string username)
    {
        var results = new Dictionary<string, object>
        {
            ["host"] = host,
            ["port"] = port,
            ["username"] = username,
            ["protocol"] = "SIP/UDP"
        };

        // Generate consistent identifiers
        var callId = $"reg-{Guid.NewGuid():N}";
        var tag = $"t{Environment.TickCount:x8}";
        var branch = $"z9hG4bK-reg-{Environment.TickCount}";
        var localPort = 15060;

        // --- Step 1: Unauthenticated REGISTER ---
        var registerMsg =
            $"REGISTER sip:{host} SIP/2.0\r\n" +
            $"Via: SIP/2.0/UDP 127.0.0.1:{localPort};branch={branch};rport\r\n" +
            $"Max-Forwards: 70\r\n" +
            $"From: <sip:{username.Replace(" ", ".")}@{host}>;tag={tag}\r\n" +
            $"To: <sip:{username.Replace(" ", ".")}@{host}>\r\n" +
            $"Call-ID: {callId}@127.0.0.1\r\n" +
            $"CSeq: 1 REGISTER\r\n" +
            $"Contact: <sip:{username.Replace(" ", ".")}@127.0.0.1:{localPort};transport=udp>\r\n" +
            $"Expires: 3600\r\n" +
            $"User-Agent: SwyxConnect/1.0\r\n" +
            $"Allow: INVITE,ACK,BYE,CANCEL,OPTIONS,NOTIFY,REFER,INFO,SUBSCRIBE,UPDATE\r\n" +
            $"Content-Length: 0\r\n\r\n";

        try
        {
            using var udp = new UdpClient(0);  // ephemeral port
            var data = Encoding.UTF8.GetBytes(registerMsg);
            await udp.SendAsync(data, data.Length, host, port);

            udp.Client.ReceiveTimeout = 5000;

            // Read responses in a loop — SIP servers send 100 Trying first, then final response
            var allResponses = new List<string>();
            string? finalResponse = null;
            int finalStatusCode = 0;
            var deadline = DateTime.UtcNow.AddSeconds(8);

            while (DateTime.UtcNow < deadline)
            {
                var recvTask = udp.ReceiveAsync();
                var remaining = deadline - DateTime.UtcNow;
                if (remaining.TotalMilliseconds < 100) break;
                if (await Task.WhenAny(recvTask, Task.Delay((int)remaining.TotalMilliseconds)) != recvTask)
                    break;

                var result = await recvTask;
                var responseText = Encoding.UTF8.GetString(result.Buffer);
                allResponses.Add(responseText);

                var lines = responseText.Split('\n');
                var statusLine = lines.Length > 0 ? lines[0].Trim() : "";
                var parts = statusLine.Split(' ', 3);
                if (parts.Length >= 2 && int.TryParse(parts[1], out int sc))
                {
                    if (sc >= 200)  // Final response (2xx, 3xx, 4xx, 5xx, 6xx)
                    {
                        finalResponse = responseText;
                        finalStatusCode = sc;
                        break;  // Got final answer
                    }
                    // 1xx = provisional, keep reading
                }
            }

            results["registerSent"] = true;
            results["responsesReceived"] = allResponses.Count;

            if (allResponses.Count == 0)
            {
                results["timeout"] = true;
                results["note"] = "Keine Antwort auf REGISTER";
            }
            else
            {
                // Use final response if available, otherwise first response
                var targetResponse = finalResponse ?? allResponses[0];
                var targetLines = targetResponse.Split('\n');
                var targetStatusLine = targetLines.Length > 0 ? targetLines[0].Trim() : "";
                var targetParts = targetStatusLine.Split(' ', 3);
                int statusCode = finalStatusCode;
                if (statusCode == 0 && targetParts.Length >= 2)
                    int.TryParse(targetParts[1], out statusCode);

                results["statusLine"] = targetStatusLine;
                results["statusCode"] = statusCode;

                if (statusCode == 200)
                {
                    results["registered"] = true;
                    results["note"] = "Registrierung ohne Auth erfolgreich (unüblich)";
                }
                else if (statusCode == 401 || statusCode == 407)
                {
                    results["challengeReceived"] = true;

                    // Parse WWW-Authenticate / Proxy-Authenticate
                    var authHeaders = new List<string>();
                    foreach (var line in targetLines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("WWW-Authenticate:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Proxy-Authenticate:", StringComparison.OrdinalIgnoreCase))
                        {
                            authHeaders.Add(trimmed);
                        }
                    }
                    results["authHeaders"] = authHeaders.ToArray();

                    // Extract auth scheme (Digest, NTLM, etc.)
                    var schemes = new List<string>();
                    foreach (var hdr in authHeaders)
                    {
                        var colonIdx = hdr.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var value = hdr[(colonIdx + 1)..].TrimStart();
                            var scheme = value.Split(' ', 2)[0];
                            schemes.Add(scheme);
                        }
                    }
                    results["authSchemes"] = schemes.ToArray();

                    // Extract realm and nonce if Digest
                    foreach (var hdr in authHeaders)
                    {
                        var colonIdx = hdr.IndexOf(':');
                        if (colonIdx <= 0) continue;
                        var value = hdr[(colonIdx + 1)..].TrimStart();

                        var realmIdx = value.IndexOf("realm=", StringComparison.OrdinalIgnoreCase);
                        if (realmIdx >= 0)
                        {
                            var realmStart = value.IndexOf('"', realmIdx);
                            var realmEnd = realmStart > 0 ? value.IndexOf('"', realmStart + 1) : -1;
                            if (realmStart > 0 && realmEnd > realmStart)
                                results["realm"] = value[(realmStart + 1)..realmEnd];
                        }

                        var nonceIdx = value.IndexOf("nonce=", StringComparison.OrdinalIgnoreCase);
                        if (nonceIdx >= 0)
                        {
                            results["hasNonce"] = true;
                        }

                        var algorithmIdx = value.IndexOf("algorithm=", StringComparison.OrdinalIgnoreCase);
                        if (algorithmIdx >= 0)
                        {
                            var algStart = value.IndexOf('=', algorithmIdx) + 1;
                            var algVal = value[algStart..].Split(new[] { ',', ' ', '\r', '\n' }, 2)[0].Trim('"');
                            results["algorithm"] = algVal;
                        }
                    }
                }
                else if (statusCode >= 100 && statusCode < 200)
                {
                    results["provisionalOnly"] = true;
                    results["note"] = $"Nur provisorische Antwort erhalten ({statusCode}) — kein finaler Response";
                }
                else
                {
                    results["note"] = $"Unerwarteter Status: {statusCode}";
                }

                // Full response (truncated)
                results["fullResponse"] = targetResponse.Length > 1500 ? targetResponse[..1500] : targetResponse;

                // All raw responses for debugging
                if (allResponses.Count > 1)
                    results["allStatusLines"] = allResponses.Select(r => r.Split('\n')[0].Trim()).ToArray();
            }
        }
        catch (Exception ex)
        {
            results["error"] = $"{ex.GetType().Name}: {ex.Message}";
        }

        return results;
    }

    /// <summary>TCP SIP REGISTER Probe (Fallback wenn UDP keine Antwort liefert).</summary>
    private static async Task<object> ProbeSipRegisterTcpAsync(string host, int port, string username, string callId, string tag)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                return new { protocol = "SIP/TCP", connected = false, reason = "connect timeout" };
            await connectTask;

            var stream = client.GetStream();
            stream.ReadTimeout = 5000;

            var branch = $"z9hG4bK-reg-tcp-{Environment.TickCount}";
            var registerMsg =
                $"REGISTER sip:{host} SIP/2.0\r\n" +
                $"Via: SIP/2.0/TCP 127.0.0.1:15060;branch={branch};rport\r\n" +
                $"Max-Forwards: 70\r\n" +
                $"From: <sip:{username.Replace(" ", ".")}@{host}>;tag={tag}-tcp\r\n" +
                $"To: <sip:{username.Replace(" ", ".")}@{host}>\r\n" +
                $"Call-ID: {callId}-tcp@127.0.0.1\r\n" +
                $"CSeq: 1 REGISTER\r\n" +
                $"Contact: <sip:{username.Replace(" ", ".")}@127.0.0.1:15060;transport=tcp>\r\n" +
                $"Expires: 3600\r\n" +
                $"User-Agent: SwyxConnect/1.0\r\n" +
                $"Content-Length: 0\r\n\r\n";

            var data = Encoding.UTF8.GetBytes(registerMsg);
            await stream.WriteAsync(data);
            await stream.FlushAsync();

            var buf = new byte[4096];
            var readTask = stream.ReadAsync(buf, 0, buf.Length);
            if (await Task.WhenAny(readTask, Task.Delay(5000)) == readTask)
            {
                int read = await readTask;
                if (read > 0)
                {
                    var responseText = Encoding.UTF8.GetString(buf, 0, read);
                    var statusLine = responseText.Split('\n')[0].Trim();

                    // Parse auth headers
                    var authHeaders = new List<string>();
                    foreach (var line in responseText.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("WWW-Authenticate:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Proxy-Authenticate:", StringComparison.OrdinalIgnoreCase))
                            authHeaders.Add(trimmed);
                    }

                    return new
                    {
                        protocol = "SIP/TCP",
                        connected = true,
                        statusLine,
                        responseLength = read,
                        authHeaders = authHeaders.ToArray(),
                        fullResponse = responseText.Length > 1500 ? responseText[..1500] : responseText
                    };
                }
                return new { protocol = "SIP/TCP", connected = true, statusLine = "(empty response)", responseLength = 0, authHeaders = Array.Empty<string>(), fullResponse = "" };
            }
            return new { protocol = "SIP/TCP", connected = true, statusLine = "(read timeout)", responseLength = 0, authHeaders = Array.Empty<string>(), fullResponse = "" };
        }
        catch (Exception ex)
        {
            return new { protocol = "SIP/TCP", connected = false, reason = ex.Message };
        }
    }

    /// <summary>Swyx-Prozesse erkennen.</summary>
    private static object ProbeProcesses()
    {
        var found = new List<object>();
        try
        {
            foreach (var proc in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    var name = proc.ProcessName;
                    if (name.Contains("Swyx", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("CLMgr", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("IpPbx", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(new { pid = proc.Id, name, memoryMB = proc.WorkingSet64 / 1024 / 1024 });
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { return new { error = ex.Message }; }
        return found;
    }

    /// <summary>
    /// SIP REGISTER with CDS authentication: tries multiple username formats
    /// (LoginID GUID, "Ralf Arnold", "Ralf.Arnold") with PSK as password.
    /// If server returns 401 Digest challenge, computes MD5 Digest auth response.
    /// </summary>
    public static async Task<object> ProbeSipRegisterWithAuthAsync(string host, int port, string loginId, string psk, string displayName)
    {
        var results = new Dictionary<string, object>
        {
            ["host"] = host,
            ["port"] = port,
            ["loginId"] = loginId,
            ["pskLength"] = psk?.Length ?? 0,
            ["displayName"] = displayName
        };

        // The LoginID GUID is the SIP username.
        // Try GUID only — all other formats get 403 Forbidden.
        // Use longer timeout (20s) since server sends 100 Trying and may take time.
        var attempts = new List<object>();

        // Attempt 1: GUID as username, no auth (see if we get 401 challenge or 200 after wait)
        var attempt1 = await TrySipRegisterAsync(host, port, loginId, psk, loginId, 20);
        attempts.Add(attempt1);

        // If step 1 returned 0 (timeout after 100 Trying), try with PSK embedded in Contact params
        if (attempt1.TryGetValue("statusCode", out var sc1) && sc1 is int code1 && code1 == 0)
        {
            // Attempt 2: Try with PSK as Digest pre-auth (no challenge, just compute one)
            var attempt2 = await TrySipRegisterPreAuthAsync(host, port, loginId, psk, 20);
            attempts.Add(attempt2);
        }

        results["attempts"] = attempts;
        return results;
    }

    /// <summary>
    /// Try SIP REGISTER with Digest pre-auth (compute auth without server challenge).
    /// Uses realm from the host, and a dummy nonce. Some SIP servers accept this.
    /// </summary>
    private static async Task<Dictionary<string, object>> TrySipRegisterPreAuthAsync(string host, int port, string loginId, string psk, int timeoutSeconds)
    {
        var result = new Dictionary<string, object> { ["username"] = loginId, ["method"] = "pre-auth-digest" };
        var callId = $"reg-{Guid.NewGuid():N}";
        var tag = $"t{Environment.TickCount:x8}";
        var branch = $"z9hG4bK-reg-{Environment.TickCount}";
        int localPort = 15060;

        try
        {
            using var udp = new UdpClient(0);
            udp.Client.ReceiveTimeout = 5000;

            // Compute pre-emptive Digest auth with PSK as password
            string realm = host;
            string nonce = Guid.NewGuid().ToString("N");
            string uri = $"sip:{host}";
            string authResponse = ComputeDigestAuth(loginId, realm, psk, nonce, "REGISTER", uri, "MD5");
            string authHeader = $"Digest username=\"{loginId}\", realm=\"{realm}\", nonce=\"{nonce}\", uri=\"{uri}\", response=\"{authResponse}\", algorithm=MD5";

            var registerMsg = BuildSipRegister(host, port, loginId, callId, tag, branch, localPort, 1, authHeader);
            result["registerSent"] = registerMsg;
            var data = Encoding.UTF8.GetBytes(registerMsg);
            await udp.SendAsync(data, data.Length, host, port);

            var (statusCode, responseText, allResponses) = await ReadSipResponses(udp, timeoutSeconds);
            result["statusCode"] = statusCode;
            result["statusLine"] = responseText?.Split('\n')[0].Trim() ?? "(no response)";
            result["responseCount"] = allResponses.Count;
            if (allResponses.Count > 0)
                result["allStatusLines"] = allResponses.Select(r => r.Split('\n')[0].Trim()).ToArray();
            if (responseText != null)
                result["fullResponse"] = responseText.Length > 1500 ? responseText[..1500] : responseText;
        }
        catch (Exception ex) { result["error"] = $"{ex.GetType().Name}: {ex.Message}"; }
        return result;
    }

    /// <summary>
    /// Single SIP REGISTER attempt. If 401 received, computes Digest auth and retries.
    /// </summary>
    private static async Task<Dictionary<string, object>> TrySipRegisterAsync(string host, int port, string username, string psk, string loginId, int timeoutSeconds = 8)
    {
        var result = new Dictionary<string, object> { ["username"] = username };
        var callId = $"reg-{Guid.NewGuid():N}";
        var tag = $"t{Environment.TickCount:x8}";
        var branch = $"z9hG4bK-reg-{Environment.TickCount}";
        int localPort = 15060;
        // Sanitize username for SIP URI (no spaces)
        var uriUser = username.Replace(" ", ".");

        try
        {
            using var udp = new UdpClient(0);
            udp.Client.ReceiveTimeout = 5000;

            // Step 1: Initial REGISTER (unauthenticated)
            var registerMsg = BuildSipRegister(host, port, uriUser, callId, tag, branch, localPort, 1, null);
            var data = Encoding.UTF8.GetBytes(registerMsg);
            await udp.SendAsync(data, data.Length, host, port);

            // Read responses
            var (statusCode, responseText, allResponses) = await ReadSipResponses(udp, timeoutSeconds);
            result["step1StatusCode"] = statusCode;
            result["step1StatusLine"] = responseText?.Split('\n')[0].Trim() ?? "(no response)";
            result["step1ResponseCount"] = allResponses.Count;

            // Step 2: If 401/407, extract Digest challenge and respond
            if (statusCode == 401 || statusCode == 407)
            {
                result["challengeReceived"] = true;

                // Parse WWW-Authenticate header
                var authHeader = ExtractAuthHeader(responseText!);
                result["authHeader"] = authHeader ?? "(not found)";

                if (authHeader != null)
                {
                    var realm = ExtractParam(authHeader, "realm");
                    var nonce = ExtractParam(authHeader, "nonce");
                    var algorithm = ExtractParam(authHeader, "algorithm") ?? "MD5";
                    result["realm"] = realm;
                    result["nonce"] = nonce;
                    result["algorithm"] = algorithm;

                    if (realm != null && nonce != null)
                    {
                        // Compute Digest auth response
                        var authResponse = ComputeDigestAuth(username, realm, psk, nonce, "REGISTER", $"sip:{host}", algorithm);
                        var branch2 = $"z9hG4bK-reg2-{Environment.TickCount}";
                        var authHeaderValue = $"Digest username=\"{username}\", realm=\"{realm}\", nonce=\"{nonce}\", uri=\"sip:{host}\", response=\"{authResponse}\", algorithm={algorithm}";
                        
                        var register2 = BuildSipRegister(host, port, uriUser, callId, tag, branch2, localPort, 2, authHeaderValue);
                        var data2 = Encoding.UTF8.GetBytes(register2);
                        await udp.SendAsync(data2, data2.Length, host, port);

                        var (sc2, resp2, allResp2) = await ReadSipResponses(udp, 6);
                        result["step2StatusCode"] = sc2;
                        result["step2StatusLine"] = resp2?.Split('\n')[0].Trim() ?? "(no response)";
                        result["step2ResponseCount"] = allResp2.Count;
                        result["statusCode"] = sc2;
                        if (resp2 != null)
                            result["step2FullResponse"] = resp2.Length > 1500 ? resp2[..1500] : resp2;
                    }
                    else
                    {
                        result["statusCode"] = statusCode;
                        result["note"] = "Missing realm or nonce in challenge";
                    }
                }
                else
                {
                    result["statusCode"] = statusCode;
                    result["note"] = "No WWW-Authenticate header found";
                }
            }
            else
            {
                result["statusCode"] = statusCode;
            }

            if (responseText != null)
                result["step1FullResponse"] = responseText.Length > 1500 ? responseText[..1500] : responseText;
        }
        catch (Exception ex)
        {
            result["error"] = $"{ex.GetType().Name}: {ex.Message}";
        }
        return result;
    }

    private static string BuildSipRegister(string host, int port, string uriUser, string callId, string tag, string branch, int localPort, int cseq, string? authHeader)
    {
        var sb = new StringBuilder();
        sb.Append($"REGISTER sip:{host} SIP/2.0\r\n");
        sb.Append($"Via: SIP/2.0/UDP 127.0.0.1:{localPort};branch={branch};rport\r\n");
        sb.Append($"Max-Forwards: 70\r\n");
        sb.Append($"From: <sip:{uriUser}@{host}>;tag={tag}\r\n");
        sb.Append($"To: <sip:{uriUser}@{host}>\r\n");
        sb.Append($"Call-ID: {callId}@127.0.0.1\r\n");
        sb.Append($"CSeq: {cseq} REGISTER\r\n");
        sb.Append($"Contact: <sip:{uriUser}@127.0.0.1:{localPort};transport=udp>\r\n");
        sb.Append($"Expires: 3600\r\n");
        sb.Append($"User-Agent: SwyxConnect/1.0\r\n");
        sb.Append($"Allow: INVITE,ACK,BYE,CANCEL,OPTIONS,NOTIFY,REFER,INFO,SUBSCRIBE,UPDATE\r\n");
        if (authHeader != null)
            sb.Append($"Authorization: {authHeader}\r\n");
        sb.Append($"Content-Length: 0\r\n\r\n");
        return sb.ToString();
    }

    private static async Task<(int statusCode, string? responseText, List<string> allResponses)> ReadSipResponses(UdpClient udp, int timeoutSeconds)
    {
        var allResponses = new List<string>();
        string? finalResponse = null;
        int finalStatusCode = 0;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var recvTask = udp.ReceiveAsync();
            var remaining = deadline - DateTime.UtcNow;
            if (remaining.TotalMilliseconds < 100) break;
            if (await Task.WhenAny(recvTask, Task.Delay((int)remaining.TotalMilliseconds)) != recvTask)
                break;

            var result = await recvTask;
            var responseText = Encoding.UTF8.GetString(result.Buffer);
            allResponses.Add(responseText);

            var lines = responseText.Split('\n');
            var statusLine = lines.Length > 0 ? lines[0].Trim() : "";
            var parts = statusLine.Split(' ', 3);
            if (parts.Length >= 2 && int.TryParse(parts[1], out int sc))
            {
                if (sc >= 200)
                {
                    finalResponse = responseText;
                    finalStatusCode = sc;
                    break;
                }
            }
        }
        return (finalStatusCode, finalResponse ?? (allResponses.Count > 0 ? allResponses[0] : null), allResponses);
    }

    private static string? ExtractAuthHeader(string response)
    {
        foreach (var line in response.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("WWW-Authenticate:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("WWW-Authenticate:".Length).Trim();
            if (trimmed.StartsWith("Proxy-Authenticate:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("Proxy-Authenticate:".Length).Trim();
        }
        return null;
    }

    private static string? ExtractParam(string header, string paramName)
    {
        var idx = header.IndexOf(paramName + "=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + paramName.Length + 1;
        if (start >= header.Length) return null;
        if (header[start] == '"')
        {
            var end = header.IndexOf('"', start + 1);
            return end > start ? header[(start + 1)..end] : null;
        }
        var endIdx = header.IndexOfAny(new[] { ',', ' ', '\r', '\n' }, start);
        return endIdx > start ? header[start..endIdx] : header[start..];
    }

    private static string ComputeDigestAuth(string username, string realm, string password, string nonce, string method, string uri, string algorithm)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        string ha1 = MD5Hash(md5, $"{username}:{realm}:{password}");
        string ha2 = MD5Hash(md5, $"{method}:{uri}");
        string response = MD5Hash(md5, $"{ha1}:{nonce}:{ha2}");
        return response;
    }

    private static string MD5Hash(System.Security.Cryptography.MD5 md5, string input)
    {
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

}
