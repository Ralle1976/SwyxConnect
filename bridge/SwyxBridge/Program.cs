using System.Text;
using System.Windows.Forms;
using SwyxBridge.Com;
using SwyxBridge.Handlers;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge;

/// <summary>
/// SwyxBridge — JSON-RPC ↔ COM Bridge für den Electron Softphone Client.
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

    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Logging.Info("SwyxBridge startet...");

        // WinForms SynchronizationContext einrichten
        // (wird automatisch installiert durch [STAThread] + WindowsFormsSynchronizationContext)
        WindowsFormsSynchronizationContext.AutoInstall = true;
        var appCtx = new ApplicationContext();

        // STA-Dispatcher erstellen (fängt den SynchronizationContext ein)
        StaDispatcher? sta = null;

        // Wir müssen den Dispatcher NACH dem Start der Message Pump erstellen,
        // dazu nutzen wir einen Timer-Trick:
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

        // Heartbeat Timer (System.Timers.Timer, feuert auf ThreadPool)
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
    }

    private static void InitializeBridge(StaDispatcher sta)
    {
        // COM-Verbindung herstellen (auf STA-Thread)
        _connector = new SwyxConnector();

        try
        {
            _connector.Connect();
            Logging.Info("COM-Verbindung hergestellt.");

            // Event-Sink registrieren
            EventSink.Subscribe(_connector);
            Logging.Info("Event-Sink registriert.");
        }
        catch (Exception ex)
        {
            Logging.Error($"COM-Verbindung fehlgeschlagen: {ex.Message}");
            JsonRpcEmitter.EmitEvent("bridgeState", new { state = "disconnected", error = ex.Message });
            // Bridge läuft trotzdem weiter für Heartbeat + Reconnect-Versuche
        }

        // Handler erstellen
        _lineManager = new LineManager(_connector);
        _callHandler = new CallHandler(_lineManager);
        _presenceHandler = new PresenceHandler(_connector);
        _contactHandler = new ContactHandler(_connector);
        _historyHandler = new HistoryHandler(_connector);
        _voicemailHandler = new VoicemailHandler(_connector);

        // JSON-RPC Server auf Background-Thread starten
        _rpcServer = new JsonRpcServer(sta, DispatchRequest);
        var serverThread = new Thread(_rpcServer.Run)
        {
            IsBackground = true,
            Name = "JsonRpcServer"
        };
        serverThread.Start();

        // Connected-Status melden
        JsonRpcEmitter.EmitEvent("bridgeState", new
        {
            state = _connector.IsConnected ? "connected" : "disconnected"
        });
    }

    /// <summary>
    /// Zentrale Request-Dispatch — wird auf dem STA-Thread aufgerufen.
    /// </summary>
    private static void DispatchRequest(JsonRpcRequest req)
    {
        if (_callHandler?.CanHandle(req.Method) == true)
        {
            _callHandler.Handle(req);
        }
        else if (_presenceHandler?.CanHandle(req.Method) == true)
        {
            _presenceHandler.Handle(req);
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
        else
        {
            Logging.Warn($"Unbekannte Methode: {req.Method}");
            if (req.Id.HasValue)
            {
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.MethodNotFound,
                    $"Methode '{req.Method}' nicht gefunden.");
            }
        }
    }
}
