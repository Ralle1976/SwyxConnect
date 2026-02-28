using System.Net;
using System.Net.Sockets;
using System.Text;
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
}
