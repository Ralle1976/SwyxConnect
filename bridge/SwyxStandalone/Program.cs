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
    private static JsonRpcServer? _rpcServer;
    private static System.Timers.Timer? _heartbeat;

    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding  = Encoding.UTF8;

        Logging.Info("SwyxStandalone startet...");
        ParseArgs(args, out string? argServer, out string? argUser, out string? argPass, out string? argDomain, out int argAuthMode);

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
                InitializeBridge(sta, argServer, argUser, argPass, argDomain, argAuthMode);
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
        EventSink.Unsubscribe();
        _connector?.Dispose();
    }

    private static void InitializeBridge(
        StaDispatcher sta,
        string? argServer, string? argUser, string? argPass, string? argDomain, int argAuthMode)
    {
        _connector    = new StandaloneConnector();
        _lineManager  = new LineManager(_connector);
        _audioManager = new AudioManager(_connector);

        _callHandler     = new CallHandler(_lineManager);
        _presenceHandler = new PresenceHandler(_connector);
        _audioHandler    = new AudioHandler(_audioManager);
        _systemHandler   = new SystemHandler(_connector, _lineManager);

        if (!string.IsNullOrEmpty(argServer) && !string.IsNullOrEmpty(argUser) && !string.IsNullOrEmpty(argPass))
        {
            try
            {
                _connector.CreateComObject();
                _connector.Login(argServer, argUser, argPass, argDomain ?? "", argAuthMode);
                EventSink.Subscribe(_connector, _lineManager);
                Logging.Info("Standalone: Via CLI-Args eingeloggt.");
                JsonRpcEmitter.EmitEvent("bridgeState", new
                {
                    state    = "connected",
                    server   = argServer,
                    username = argUser
                });
            }
            catch (Exception ex)
            {
                Logging.Error($"CLI-Login fehlgeschlagen: {ex.Message}");
                JsonRpcEmitter.EmitEvent("bridgeState", new { state = "disconnected", error = ex.Message });
            }
        }
        else
        {
            Logging.Info("Standalone: Kein Auto-Login — warte auf 'login' JSON-RPC-Methode.");
            JsonRpcEmitter.EmitEvent("bridgeState", new { state = "ready", info = "Send {method:'login',params:{server,username,password}} to connect." });
        }

        _rpcServer = new JsonRpcServer(sta, DispatchRequest);
        var serverThread = new Thread(_rpcServer.Run)
        {
            IsBackground = true,
            Name = "JsonRpcServer"
        };
        serverThread.Start();
    }

    private static void DispatchRequest(JsonRpcRequest req)
    {
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
        out string? domain,
        out int authMode)
    {
        server   = null;
        user     = null;
        pass     = null;
        domain   = null;
        authMode = 1;

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--server":    server   = args[++i]; break;
                case "--user":
                case "--username":  user     = args[++i]; break;
                case "--password":
                case "--pass":      pass     = args[++i]; break;
                case "--domain":    domain   = args[++i]; break;
                case "--auth-mode":
                case "--authmode":
                    if (int.TryParse(args[++i], out int m)) authMode = m;
                    break;
            }
        }
    }
}
