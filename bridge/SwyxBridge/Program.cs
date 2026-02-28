using System.Text;
using System.Windows.Forms;
using SwyxBridge.Com;
using SwyxBridge.Handlers;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;
using SwyxBridge.Standalone;
using System.ServiceModel;

namespace SwyxBridge;

/// <summary>
/// SwyxBridge — JSON-RPC ↔ COM/SIP Bridge für den Electron Softphone Client.
/// 
/// Architektur:
///   [Background-Thread] → liest stdin → postet auf STA via SynchronizationContext.Post()
///   [STA Main-Thread]   → Message Pump → dispatched COM Events → schreibt auf stdout
/// 
/// CRITICAL: COM-Objekte NUR auf dem STA-Thread erstellen/verwenden!
/// CRITICAL: stdout = NUR JSON-RPC. Alles andere → stderr (Logging).
/// </summary>
static class Program
{
    // Static references to prevent GC collection
    private static SwyxConnector? _connector;
    private static LineManager? _lineManager;
    private static CallHandler? _callHandler;
    private static PresenceHandler? _presenceHandler;
    private static ContactHandler? _contactHandler;
    private static HistoryHandler? _historyHandler;
    private static VoicemailHandler? _voicemailHandler;
    private static JsonRpcServer? _rpcServer;
    private static System.Timers.Timer? _heartbeat;
    private static StandaloneKestrelHost? _kestrelHost;

    private static string? _cdsAccessToken;
    private static string? _cdsRefreshToken;
    private static int _cdsUserId;
    private static string? _cdsUsername;

    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Logging.Info("SwyxBridge startet...");

        WindowsFormsSynchronizationContext.AutoInstall = true;
        var appCtx = new ApplicationContext();
        StaDispatcher? sta = null;

        var initTimer = new System.Windows.Forms.Timer { Interval = 1 };
        initTimer.Tick += (s, e) =>
        {
            initTimer.Stop();
            initTimer.Dispose();
            try
            {
                sta = new StaDispatcher();
                InitializeBridge(sta);
            }
            catch (Exception ex)
            {
                Logging.Error($"Bridge-Initialisierung fehlgeschlagen: {ex.Message}");
                JsonRpcEmitter.EmitEvent("error", new { message = ex.Message });
            }
        };
        initTimer.Start();

        // Heartbeat
        int uptimeSeconds = 0;
        _heartbeat = new System.Timers.Timer(5000);
        _heartbeat.Elapsed += (s, e) =>
        {
            uptimeSeconds += 5;
            JsonRpcEmitter.EmitEvent("heartbeat", new { uptime = uptimeSeconds });
        };
        _heartbeat.Start();

        Logging.Info("Message Pump startet...");
        Application.Run(appCtx);

        // Cleanup
        Logging.Info("SwyxBridge beendet.");
        _heartbeat?.Stop();
        _rpcServer?.Stop();
        EventSink.Unsubscribe();
        _connector?.Dispose();
        _kestrelHost?.Dispose();
    }

    private static void InitializeBridge(StaDispatcher sta)
    {
        // Manual Connect Mode — COM wird NICHT automatisch gestartet
        _connector = new SwyxConnector();
        _lineManager = new LineManager(_connector);
        _callHandler = new CallHandler(_lineManager);
        _presenceHandler = new PresenceHandler(_connector);
        _contactHandler = new ContactHandler(_connector);
        _historyHandler = new HistoryHandler(_connector);
        _voicemailHandler = new VoicemailHandler(_connector);

        _rpcServer = new JsonRpcServer(sta, DispatchRequest);
        var serverThread = new Thread(_rpcServer.Run) { IsBackground = true, Name = "JsonRpcServer" };
        serverThread.Start();

        // Status: disconnected bis "connect" explizit aufgerufen wird
        JsonRpcEmitter.EmitEvent("bridgeState", new { state = "disconnected" });
        Logging.Info("SwyxBridge bereit (manual connect mode + standalone probes).");
    }

    private static void DispatchRequest(JsonRpcRequest req)
    {
        // --- Connection Management ---
        if (req.Method == "connect")
        {
            try
            {
                string? server = null;
                if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
                    if (req.Params.Value.TryGetProperty("serverName", out var sv))
                        server = sv.GetString();
                _connector?.Connect(server);
                if (_connector?.IsConnected == true && _lineManager != null)
                    EventSink.Subscribe(_connector, _lineManager);
                var info = _connector?.GetConnectionInfo() ?? new { connected = false };
                if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, info);
                JsonRpcEmitter.EmitEvent("bridgeState", new { state = _connector?.IsConnected == true ? "connected" : "disconnected" });
            }
            catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message); }
            return;
        }
        if (req.Method == "getConnectionInfo")
        {
            var info = _connector?.GetConnectionInfo() ?? new { connected = false };
            if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, info);
            return;
        }
        if (req.Method == "disconnect")
        {
            EventSink.Unsubscribe();
            _connector?.Disconnect();
            if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, new { ok = true });
            JsonRpcEmitter.EmitEvent("bridgeState", new { state = "disconnected" });
            return;
        }

        // --- Standalone Kestrel Host ---
        if (req.Method == "startStandaloneHost")
        {
            try
            {
                string? serverAddress = null, username = null, password = null;
                string? publicServer = null, publicAuthServer = null;
                int kestrelPort = 0, numberOfLines = 2, publicSipPort = 15021, publicAuthPort = 8021;

                if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var p = req.Params.Value;
                    if (p.TryGetProperty("serverAddress", out var sa)) serverAddress = sa.GetString();
                    if (p.TryGetProperty("username", out var un)) username = un.GetString();
                    if (p.TryGetProperty("password", out var pw)) password = pw.GetString();
                    if (p.TryGetProperty("port", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.Number) kestrelPort = pt.GetInt32();
                    if (p.TryGetProperty("numberOfLines", out var nl) && nl.ValueKind == System.Text.Json.JsonValueKind.Number) numberOfLines = Math.Clamp(nl.GetInt32(), 1, 8);
                    if (p.TryGetProperty("publicServer", out var ps)) publicServer = ps.GetString();
                    if (p.TryGetProperty("publicSipPort", out var psp) && psp.ValueKind == System.Text.Json.JsonValueKind.Number) publicSipPort = psp.GetInt32();
                    if (p.TryGetProperty("publicAuthServer", out var pas)) publicAuthServer = pas.GetString();
                    if (p.TryGetProperty("publicAuthPort", out var pap) && pap.ValueKind == System.Text.Json.JsonValueKind.Number) publicAuthPort = pap.GetInt32();
                }

                if (_kestrelHost?.IsRunning == true)
                {
                    if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, new { ok = true, port = _kestrelHost.ActualPort, message = "Bereits gestartet" });
                    return;
                }

                var config = new SipClientConfig
                {
                    ServerAddress = serverAddress ?? "",
                    Username = username ?? "",
                    Password = password ?? "",
                    SipDomain = serverAddress ?? "",
                    KestrelPort = kestrelPort,
                    NumberOfLines = numberOfLines,
                    PublicServer = publicServer ?? "",
                    PublicSipPort = publicSipPort,
                    PublicAuthServer = publicAuthServer ?? "",
                    PublicAuthPort = publicAuthPort
                };
                _kestrelHost = new StandaloneKestrelHost(config);

                Task.Run(async () =>
                {
                    try
                    {
                        int port = await _kestrelHost.StartAsync();
                        if (req.Id.HasValue)
                            JsonRpcEmitter.EmitResponse(req.Id.Value, new { ok = true, port, hubPaths = new[] { "/hubs/swyxit", "/hubs/comsocket" }, healthUrl = $"http://localhost:{port}/api/health" });
                        JsonRpcEmitter.EmitEvent("standaloneHostStarted", new { port });
                    }
                    catch (Exception ex)
                    {
                        Logging.Error($"Kestrel Start: {ex.Message}");
                        if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message);
                    }
                });
            }
            catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            return;
        }
        if (req.Method == "stopStandaloneHost")
        {
            Task.Run(async () =>
            {
                try
                {
                    if (_kestrelHost != null) { await _kestrelHost.StopAsync(); _kestrelHost.Dispose(); _kestrelHost = null; }
                    if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, new { ok = true });
                    JsonRpcEmitter.EmitEvent("standaloneHostStopped", new { });
                }
                catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            });
            return;
        }
        if (req.Method == "getStandaloneHostStatus")
        {
            if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, new { running = _kestrelHost?.IsRunning ?? false, port = _kestrelHost?.ActualPort ?? 0 });
            return;
        }

        // --- Network Probe (NEU) ---
        if (req.Method == "probeNetwork")
        {
            try
            {
                string? publicServer = null;
                int publicSipPort = 15021, publicAuthPort = 8021;

                if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var p = req.Params.Value;
                    if (p.TryGetProperty("publicServer", out var ps)) publicServer = ps.GetString();
                    if (p.TryGetProperty("publicSipPort", out var psp) && psp.ValueKind == System.Text.Json.JsonValueKind.Number) publicSipPort = psp.GetInt32();
                    if (p.TryGetProperty("publicAuthPort", out var pap) && pap.ValueKind == System.Text.Json.JsonValueKind.Number) publicAuthPort = pap.GetInt32();
                }

                Task.Run(async () =>
                {
                    try
                    {
                        var result = await NetworkProbe.ProbeAllAsync(publicServer, publicSipPort, publicAuthPort);
                        if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, result);
                    }
                    catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
                });
            }
            catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            return;
        }

        // --- CDS WCF Login Probe ---
        if (req.Method == "probeCds")
        {
            try
            {
                string? host = "127.0.0.1", username = null, password = null;
                int port = 9094;

                if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var p = req.Params.Value;
                    if (p.TryGetProperty("host", out var h)) host = h.GetString();
                    if (p.TryGetProperty("port", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.Number) port = pt.GetInt32();
                    if (p.TryGetProperty("username", out var un)) username = un.GetString();
                    if (p.TryGetProperty("password", out var pw)) password = pw.GetString();
                }

                Task.Run(() =>
                {
                    try
                    {
                        using var client = new CdsLoginClient(host ?? "127.0.0.1", port);
                        var result = client.FullProbe(username, password);
                        if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, result);
                    }
                    catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
                });
            }
            catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            return;
        }

        // --- SIP REGISTER Probe ---
        if (req.Method == "probeSipRegister")
        {
            try
            {
                string host = "127.0.0.1", username = "probe-user";
                int port = 5060;

                if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var p = req.Params.Value;
                    if (p.TryGetProperty("host", out var h) && h.GetString() is string hv) host = hv;
                    if (p.TryGetProperty("port", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.Number) port = pt.GetInt32();
                    if (p.TryGetProperty("username", out var un) && un.GetString() is string uv) username = uv;
                }

                Task.Run(async () =>
                {
                    try
                    {
                        var result = await NetworkProbe.ProbeSipRegisterAsync(host, port, username);
                        if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, result);
                    }
                    catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
                });
            }
            catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            return;
        }

        // --- SIP REGISTER with CDS Auth (LoginID + PSK) ---
        if (req.Method == "probeSipAuth")
        {
            if (_cdsAccessToken == null)
            {
                if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, "Zuerst cdsLogin aufrufen");
                return;
            }
            string sipHost = "127.0.0.1"; int sipPort = 5060; int cdsPort = 9094;
            if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var p = req.Params.Value;
                if (p.TryGetProperty("sipHost", out var h) && h.GetString() is string hv) sipHost = hv;
                if (p.TryGetProperty("sipPort", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.Number) sipPort = pt.GetInt32();
                if (p.TryGetProperty("cdsPort", out var cp) && cp.ValueKind == System.Text.Json.JsonValueKind.Number) cdsPort = cp.GetInt32();
            }
            var capturedToken = _cdsAccessToken!;
            var capturedUsername = _cdsUsername ?? "";
            var capturedUserId = _cdsUserId;
            Task.Run(async () =>
            {
                try
                {
                    // Get FRESH LoginID (IpPbxSrv type for SIP) + PSK from CDS — each LoginID is single-use!
                    var cdsHost = "127.0.0.1";
                    using var facade = new CdsPhoneClientFacade(cdsHost, cdsPort, capturedToken, capturedUsername);
                    // Try IpPbxSrv-typed LoginID first, fall back to generic GetUserLoginIDEx
                    Guid loginId;
                    try { loginId = facade.GetUserLoginIDEx2(capturedUserId, CdsLoginIdType.IpPbxSrv); }
                    catch { loginId = facade.GetUserLoginIDEx(capturedUserId); }
                    var psk = facade.GetDecryptedPSK(capturedUserId);
                    Logging.Info($"SIP Auth Probe: sipHost={sipHost}:{sipPort}, loginId={loginId}, pskLen={psk?.Length}");

                    var result = await NetworkProbe.ProbeSipRegisterWithAuthAsync(
                        sipHost, sipPort, loginId.ToString(), psk ?? "", capturedUsername);
                    if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, result);
                }
                catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            });
            return;
        }

        // --- CDS Login (AcquireToken) ---
        if (req.Method == "cdsLogin")
        {
            try
            {
                string host = "127.0.0.1", username = "", password = "";
                int port = 9094;

                if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var p = req.Params.Value;
                    if (p.TryGetProperty("host", out var h) && h.GetString() is string hv) host = hv;
                    if (p.TryGetProperty("port", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.Number) port = pt.GetInt32();
                    if (p.TryGetProperty("username", out var un) && un.GetString() is string uv) username = uv;
                    if (p.TryGetProperty("password", out var pw) && pw.GetString() is string pv) password = pv;
                }

                Task.Run(() =>
                {
                    try
                    {
                        using var client = new CdsLoginClient(host, port);
                        var result = client.AcquireToken(username, password);
                        if (result.Success)
                        {
                            _cdsAccessToken = result.AccessToken;
                            _cdsRefreshToken = result.RefreshToken;
                            _cdsUserId = result.UserId;
                            _cdsUsername = username;
                        }
                        var shortAt = result.AccessToken != null && result.AccessToken.Length > 20
                            ? result.AccessToken.Substring(0, 20) + "..."
                            : result.AccessToken;
                        var shortRt = result.RefreshToken != null && result.RefreshToken.Length > 20
                            ? result.RefreshToken.Substring(0, 20) + "..."
                            : result.RefreshToken;
                        if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, new
                        {
                            success = result.Success,
                            userId = result.UserId,
                            accessToken = shortAt,
                            refreshToken = shortRt,
                            error = result.Error
                        });
                    }
                    catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
                });
            }
            catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            return;
        }

        // --- CDS Get SIP Credentials ---
        if (req.Method == "getSipCredentials")
        {
            try
            {
                if (_cdsAccessToken == null)
                {
                    if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, "Zuerst cdsLogin aufrufen");
                    return;
                }

                string host = "127.0.0.1";
                int port = 9094;
                int? userId = null;

                if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var p = req.Params.Value;
                    if (p.TryGetProperty("host", out var h) && h.GetString() is string hv) host = hv;
                    if (p.TryGetProperty("port", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.Number) port = pt.GetInt32();
                    if (p.TryGetProperty("userId", out var uid) && uid.ValueKind == System.Text.Json.JsonValueKind.Number) userId = uid.GetInt32();
                }

                var resolvedUserId = userId ?? _cdsUserId;
                var capturedToken = _cdsAccessToken!;
                var capturedUsername = _cdsUsername ?? "";

                Task.Run(() =>
                {
                    try
                    {
                        using var facade = new CdsPhoneClientFacade(host, port, capturedToken, capturedUsername);
                        var creds = facade.GetSipCredentials(resolvedUserId);
                        if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, new
                        {
                            sipRealm = creds?.SipRealm,
                            sipUserId = creds?.SipUserID,
                            sipUserName = creds?.SipUserName,
                            userId = creds?.UserID
                        });
                    }
                    catch (FaultException<CdsTException> fex)
                    {
                        if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, $"CDS Fault: {fex.Detail?.Message ?? fex.Message} (Code: {fex.Detail?.ErrorCode})");
                    }
                    catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
                });
            }
            catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            return;
        }

        // --- CDS User Info (all-in-one diagnostics after login) ---
        if (req.Method == "cdsGetUserInfo")
        {
            if (_cdsAccessToken == null)
            {
                if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, "Zuerst cdsLogin aufrufen");
                return;
            }
            string host = "127.0.0.1"; int port = 9094;
            if (req.Params?.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var p = req.Params.Value;
                if (p.TryGetProperty("host", out var h) && h.GetString() is string hv) host = hv;
                if (p.TryGetProperty("port", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.Number) port = pt.GetInt32();
            }
            Task.Run(() =>
            {
                var info = new Dictionary<string, object?>();
                try
                {
                    using var facade = new CdsPhoneClientFacade(host, port, _cdsAccessToken!, _cdsUsername ?? "");
                    try { info["currentUserId"] = facade.GetCurrentUserID(); } catch (Exception ex) { info["currentUserIdError"] = ex.Message; }
                    try { var (uid, name) = facade.GetCurrentUserName(); info["currentUserName"] = name; info["currentUserNameResult"] = uid; } catch (Exception ex) { info["currentUserNameError"] = ex.Message; }
                    try { var si = facade.GetServerInfo(); info["serverName"] = si?.ServerName; info["serverVersion"] = si?.ServerVersion; info["serverType"] = si?.ServerType; } catch (Exception ex) { info["serverInfoError"] = ex.Message; }
                    try { info["userLoginId"] = facade.GetUserLoginID().ToString(); } catch (Exception ex) { info["userLoginIdError"] = ex.Message; }
                    try { info["userLoginIdEx"] = facade.GetUserLoginIDEx(_cdsUserId).ToString(); } catch (Exception ex) { info["userLoginIdExError"] = ex.Message; }
                    try { var creds = facade.GetSipCredentials(_cdsUserId); info["sipRealm"] = creds?.SipRealm; info["sipUserId"] = creds?.SipUserID; info["sipUserName"] = creds?.SipUserName; } catch (Exception ex) { info["sipCredError"] = ex.Message; }
                    try { var psk = facade.GetDecryptedPSK(_cdsUserId); info["presharedKey"] = psk; info["pskLength"] = psk?.Length ?? 0; } catch (Exception ex) { info["pskError"] = ex.Message; }
                    try { var raw = facade.GetRawPSK(_cdsUserId); info["pskRawLength"] = raw?.Length ?? 0; } catch (Exception ex) { info["pskRawError"] = ex.Message; }
                    try { info["loginIdEx2_IpPbxSrv"] = facade.GetUserLoginIDEx2(_cdsUserId, CdsLoginIdType.IpPbxSrv).ToString(); } catch (Exception ex) { info["loginIdEx2_IpPbxSrvError"] = ex.Message; }
                    try { info["loginIdEx2_UaCstaSrv"] = facade.GetUserLoginIDEx2(_cdsUserId, CdsLoginIdType.UaCstaSrv).ToString(); } catch (Exception ex) { info["loginIdEx2_UaCstaSrvError"] = ex.Message; }
                    if (req.Id.HasValue) JsonRpcEmitter.EmitResponse(req.Id.Value, info);
                }
                catch (Exception ex) { if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message); }
            });
            return;
        }

        // --- Handler Dispatch ---
        if (_callHandler?.CanHandle(req.Method) == true) { _callHandler.Handle(req); }
        else if (_presenceHandler?.CanHandle(req.Method) == true) { _presenceHandler.Handle(req); }
        else if (_contactHandler?.CanHandle(req.Method) == true) { _contactHandler.Handle(req); }
        else if (_historyHandler?.CanHandle(req.Method) == true) { _historyHandler.Handle(req); }
        else if (_voicemailHandler?.CanHandle(req.Method) == true) { _voicemailHandler.Handle(req); }
        else
        {
            Logging.Warn($"Unbekannte Methode: {req.Method}");
            if (req.Id.HasValue) JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.MethodNotFound, $"Methode '{req.Method}' nicht gefunden.");
        }
    }
}
