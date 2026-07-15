using System.Text;
using System.Windows.Forms;
using SwyxStandalone.Com;
using SwyxStandalone.Handlers;
using SwyxStandalone.JsonRpc;
using SwyxStandalone.Utils;

namespace SwyxStandalone;

// Architecture:
//   [Background-Thread] → reads stdin → posts to STA via SynchronizationContext.Post()
//   [STA Main-Thread]   → Message Pump → dispatches COM Events → writes to stdout
//
// CRITICAL: COM objects must only be created/used on the STA thread!
// CRITICAL: stdout = JSON-RPC only. Everything else → stderr (Logging).
//
// Standalone mode difference from SwyxBridge:
//   - No running SwyxIt! required
//   - Use "login" JSON-RPC method or --server/--user/--password args for initial login
//   - RegisterUserEx() / ReleaseUserEx() handle session lifecycle
static class Program
{
    // Static references prevent GC collection of COM objects
    private static StandaloneConnector? _connector;
    private static LineManager? _lineManager;
    private static AudioManager? _audioManager;
    private static CallHandler? _callHandler;
    private static PresenceHandler? _presenceHandler;
    private static AudioHandler? _audioHandler;
    private static SystemHandler? _systemHandler;
    private static ContactHandler? _contactHandler;
    private static HistoryHandler? _historyHandler;
    private static VoicemailHandler? _voicemailHandler;
    private static ForwardingHandler? _forwardingHandler;
    private static ConferenceHandler? _conferenceHandler;
    private static RecordingHandler? _recordingHandler;
    private static SystemInfoHandler? _systemInfoHandler;
    private static ChatHandler? _chatHandler;
    private static CtiHandler? _ctiHandler;
    private static ComSocketClient? _comSocket;
    private static ComSocketHandler? _comSocketHandler;
    private static SwyxItSuppressor? _swyxItSuppressor;
    private static int _comSocketPort;
    private static JsonRpcServer? _rpcServer;
    private static System.Timers.Timer? _heartbeat;

    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding  = Encoding.UTF8;

        Logging.Info("SwyxStandalone startet...");
        ParseArgs(args, out string? argServer, out string? argUser, out string? argPass, out string? argBackupServer, out string? argPublicServer, out string? argPublicBackupServer, out int argAuthMode);

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
                InitializeBridge(sta, argServer, argUser, argPass, argBackupServer, argPublicServer, argPublicBackupServer, argAuthMode);
            }
            catch (Exception ex)
            {
                Logging.Error($"Bridge-Initialisierung fehlgeschlagen: {ex.Message}");
                JsonRpcEmitter.EmitEvent("error", new { message = ex.Message });
            }
        };
        initTimer.Start();

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

        Logging.Info("SwyxStandalone beendet.");
        _heartbeat?.Stop();
        _rpcServer?.Stop();
        _swyxItSuppressor?.Dispose();
        EventSink.Unsubscribe();
        _connector?.Dispose();
    }

    private static void InitializeBridge(
        StaDispatcher sta,
        string? argServer, string? argUser, string? argPass, string? argBackupServer,
        string? argPublicServer, string? argPublicBackupServer, int argAuthMode)
    {
        _connector    = new StandaloneConnector();
        _lineManager  = new LineManager(_connector);
        _audioManager = new AudioManager(_connector);

        // Suppress classic SwyxIt! — it pops up on call events and conflicts with our UI.
        // SwyxItSuppressor starts SwyxIt! briefly (20s) to initialize CLMgr, then kills it.
        _swyxItSuppressor = new SwyxItSuppressor();
        _swyxItSuppressor.Start();

        // v1.5.0: DispInit is called in CreateComObject(). Audio plugins load without SwyxIt!.
        // Only a short delay needed for CLMgr COM server to be ready.
        Logging.Info("InitializeBridge: Waiting 3s for CLMgr...");
        System.Threading.Thread.Sleep(3000);
        Logging.Info("InitializeBridge: Proceeding with login.");

        _callHandler      = new CallHandler(_lineManager);
        _presenceHandler  = new PresenceHandler(_connector);
        _audioHandler     = new AudioHandler(_audioManager);
        _systemHandler    = new SystemHandler(_connector, _lineManager);
        _contactHandler   = new ContactHandler(_connector);
        _historyHandler   = new HistoryHandler(_connector);
        _voicemailHandler = new VoicemailHandler(_connector);
        _forwardingHandler = new ForwardingHandler(_connector, _lineManager);
        _conferenceHandler = new ConferenceHandler(_connector);
        _recordingHandler  = new RecordingHandler(_connector);
        _systemInfoHandler = new SystemInfoHandler(_connector);
        _chatHandler       = new ChatHandler(_connector);
        _ctiHandler        = new CtiHandler(_connector);

        // ComSocket (SignalR) — connects to SwyxItHub in CLMgr for rich data
        _comSocket = new ComSocketClient();
        _comSocketHandler = new ComSocketHandler(_comSocket);

        // Wire ComSocket events → JSON-RPC push events to Electron
        _comSocket.LineStateChanged += data => JsonRpcEmitter.EmitEvent("cs.lineStateChanged", data);
        _comSocket.LineDetailsChanged += data => JsonRpcEmitter.EmitEvent("cs.lineDetailsChanged", data);
        _comSocket.UserDataChanged += data => JsonRpcEmitter.EmitEvent("cs.userDataChanged", data);
        _comSocket.NotificationCallsChanged += data => JsonRpcEmitter.EmitEvent("cs.notificationCallsChanged", data);

        // === LOGIN ===
        // After SwyxIt initialized CLMgr (and was killed), try to attach to the session.
        // First try Auto-Attach (if SwyxIt left a valid session), then fall back to RC-Login.
        try
        {
            _connector.CreateComObject();
            var com = _connector.GetCom();
            int isLoggedIn = 0;
            try { isLoggedIn = (int)com.DispIsLoggedIn; } catch { }
            string server = "";
            string user = "";
            try { server = (string)(com.DispGetCurrentServer ?? ""); } catch { }
            try { user = (string)(com.DispGetCurrentUser ?? ""); } catch { }

            if (isLoggedIn != 0)
            {
                // Auto-Attach: SwyxIt left a valid logged-in session
                EventSink.Subscribe(_connector, _lineManager);
                Logging.Info($"Standalone: Attached to existing session (user={user}, server={server}).");

                // Apply audio devices after attach
                _connector.ApplyAudioDevices();

                // Auto-connect ComSocket
                StartComSocket();

                JsonRpcEmitter.EmitEvent("bridgeState", new
                {
                    state = "connected",
                    server,
                    username = user,
                    mode = "attached"
                });
            }
            else if (!string.IsNullOrEmpty(argServer) && !string.IsNullOrEmpty(argUser) && !string.IsNullOrEmpty(argPass))
            {
                // RC-Tunnel Login (SwyxIt didn't leave a session, or wasn't running)
                if (!string.IsNullOrEmpty(argPublicServer))
                {
                    Logging.Info($"Standalone: RC-Login mit PublicServer='{argPublicServer}'...");
                    _connector.LoginViaRemoteConnector(
                        argPublicServer, argServer, argUser, argPass,
                        argPublicBackupServer ?? "", argBackupServer ?? "",
                        argAuthMode, ctiMaster: true);
                }
                else
                {
                    _connector.Login(argServer, argUser, argPass, argBackupServer ?? "", argAuthMode);
                }

                EventSink.Subscribe(_connector, _lineManager);
                Logging.Info("Standalone: Via CLI-Args eingeloggt.");

                // Apply audio devices AFTER login — CLMgr only binds devices post-login
                _connector.ApplyAudioDevices();

                StartComSocket();

                JsonRpcEmitter.EmitEvent("bridgeState", new
                {
                    state = "connected",
                    server = argServer,
                    username = argUser
                });
            }
            else
            {
                Logging.Info("Standalone: COM created but not logged in. Waiting for 'login' JSON-RPC method.");
                JsonRpcEmitter.EmitEvent("bridgeState", new { state = "ready", info = "Send {method:'login',params:{server,username,password}} to connect." });
            }
        }
        catch (Exception ex)
        {
            Logging.Error($"Login fehlgeschlagen: {ex.Message}");
            JsonRpcEmitter.EmitEvent("bridgeState", new { state = "disconnected", error = ex.Message });
        }

        _rpcServer = new JsonRpcServer(sta, DispatchRequest);
        var serverThread = new Thread(_rpcServer.Run)
        {
            IsBackground = true,
            Name = "JsonRpcServer"
        };
        serverThread.Start();
    }

    private static void StartComSocket()
    {
        Logging.Info("Starting ComSocket discovery thread...");
        var comSocketThread = new Thread(async () =>
        {
            try
            {
                _comSocketPort = await ComSocketClient.DiscoverPortAsync();
                Logging.Info($"ComSocket DiscoverPortAsync returned port={_comSocketPort}");
                if (_comSocketPort > 0)
                {
                    await _comSocket!.ConnectAsync(_comSocketPort);
                    Logging.Info($"ComSocket connected on port {_comSocketPort}");
                    JsonRpcEmitter.EmitEvent("comSocketState", new { connected = true, port = _comSocketPort });
                }
                else
                {
                    JsonRpcEmitter.EmitEvent("comSocketState", new { connected = false, port = 0, reason = "port-discovery-failed" });
                }
            }
            catch (Exception ex)
            {
                Logging.Error($"ComSocket connect failed: {ex.Message}");
                JsonRpcEmitter.EmitEvent("comSocketState", new { connected = false, port = 0, error = ex.Message });
            }
        })
        {
            IsBackground = true,
            Name = "ComSocketAutoConnect"
        };
        comSocketThread.Start();
    }

    private static void DispatchRequest(JsonRpcRequest req)
    {
        // Order matters: SystemHandler handles login/status/line-management first.
        if (_systemHandler?.CanHandle(req.Method) == true)
        {
            _systemHandler.Handle(req);
        }
        else if (_callHandler?.CanHandle(req.Method) == true)
        {
            _callHandler.Handle(req);
        }
        else if (_presenceHandler?.CanHandle(req.Method) == true)
        {
            _presenceHandler.Handle(req);
        }
        else if (_audioHandler?.CanHandle(req.Method) == true)
        {
            _audioHandler.Handle(req);
        }
        else if (_contactHandler?.CanHandle(req.Method) == true)
        {
            _contactHandler.Handle(req);
        }
        else if (_historyHandler?.CanHandle(req.Method) == true)
        {
            _historyHandler.Handle(req);
        }
        else if (_voicemailHandler?.CanHandle(req.Method) == true)
        {
            _voicemailHandler.Handle(req);
        }
        else if (_forwardingHandler?.CanHandle(req.Method) == true)
        {
            _forwardingHandler.Handle(req);
        }
        else if (_conferenceHandler?.CanHandle(req.Method) == true)
        {
            _conferenceHandler.Handle(req);
        }
        else if (_recordingHandler?.CanHandle(req.Method) == true)
        {
            _recordingHandler.Handle(req);
        }
        else if (_systemInfoHandler?.CanHandle(req.Method) == true)
        {
            _systemInfoHandler.Handle(req);
        }
        else if (_chatHandler?.CanHandle(req.Method) == true)
        {
            _chatHandler.Handle(req);
        }
        else if (_ctiHandler?.CanHandle(req.Method) == true)
        {
            _ctiHandler.Handle(req);
        }
        else if (_comSocketHandler?.CanHandle(req.Method) == true)
        {
            _comSocketHandler.Handle(req);
        }
        else
        {
            Logging.Warn($"Unbekannte Methode: {req.Method}");
            if (req.Id.HasValue)
            {
                JsonRpcEmitter.EmitError(
                    req.Id.Value,
                    JsonRpcConstants.MethodNotFound,
                    $"Methode '{req.Method}' nicht gefunden.");
            }
        }
    }

    private static void ParseArgs(
        string[] args,
        out string? server,
        out string? user,
        out string? pass,
        out string? backupServer,
        out string? publicServer,
        out string? publicBackupServer,
        out int authMode)
    {
        server            = null;
        user              = null;
        pass              = null;
        backupServer      = null;
        publicServer      = null;
        publicBackupServer = null;
        authMode          = 1;

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--server":    server   = args[++i]; break;
                case "--user":
                case "--username":  user     = args[++i]; break;
                case "--password":
                case "--pass":      pass     = args[++i]; break;
                case "--backup-server":
                case "--backup":    backupServer = args[++i]; break;
                case "--public-server":
                case "--public":    publicServer = args[++i]; break;
                case "--public-backup": publicBackupServer = args[++i]; break;
                case "--auth-mode":
                case "--authmode":
                    if (int.TryParse(args[++i], out int m)) authMode = m;
                    break;
            }
        }
    }
}
