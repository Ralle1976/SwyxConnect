using System.Runtime.InteropServices;
using SwyxStandalone.JsonRpc;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Com;

public sealed class EventSink
{
    // CRITICAL: Static fields prevent GC from collecting the delegate while COM holds a reference
    private static EventSink? _staticInstance;
    private static Action<int, int>? _staticDelegate;

    private readonly StandaloneConnector _connector;
    private readonly LineManager _lineManager;

    // Connection Point cookie for DispOnLineMgrNotification (the other event interface)
    private static int _dispEventCookie;
    private static object? _dispEventSink;
    private static bool _dispEventSubscribed;

    private EventSink(StandaloneConnector connector, LineManager lineManager)
    {
        _connector = connector;
        _lineManager = lineManager;
    }

    public static EventSink Subscribe(StandaloneConnector connector, LineManager lineManager)
    {
        Unsubscribe();

        var sink = new EventSink(connector, lineManager);
        _staticInstance = sink;
        _staticDelegate = sink.OnLineMgrNotification;

        var com = connector.GetCom();
        if (com == null)
            throw new InvalidOperationException("COM nicht verbunden.");

        // Subscribe to PubOnLineMgrNotification (what we already do)
        try
        {
            ((dynamic)com).PubOnLineMgrNotification += _staticDelegate;
            Logging.Info("EventSink: Registriert für PubOnLineMgrNotification.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"EventSink: PubOnLineMgrNotification failed: {ex.Message}");
        }

        // ALSO subscribe to DispOnLineMgrNotification via disp-event (dynamic +=)
        // SwyxIt! subscribes to BOTH event interfaces. DispOnLineMgrNotification
        // might be the trigger that tells CLMgr "a full client is present" → load audio.
        try
        {
            ((dynamic)com).DispOnLineMgrNotification += _staticDelegate;
            _dispEventSubscribed = true;
            Logging.Info("EventSink: Registriert für DispOnLineMgrNotification (disp-event).");
        }
        catch (Exception ex)
        {
            Logging.Warn($"EventSink: DispOnLineMgrNotification failed: {ex.Message}");
        }

        return sink;
    }

    public static void Unsubscribe()
    {
        if (_staticInstance == null || _staticDelegate == null) return;

        try
        {
            var com = _staticInstance._connector.GetCom();
            if (com != null)
            {
                try { ((dynamic)com).PubOnLineMgrNotification -= _staticDelegate; } catch { }
                if (_dispEventSubscribed)
                {
                    try { ((dynamic)com).DispOnLineMgrNotification -= _staticDelegate; } catch { }
                    _dispEventSubscribed = false;
                }
            }
            Logging.Info("EventSink: Abgemeldet.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"EventSink: Fehler beim Abmelden: {ex.Message}");
        }
        finally
        {
            _staticInstance = null;
            _staticDelegate = null;
        }
    }

    // PubCLMgrNotificationMessages from Swyx SDK:
    //   0=LineStateChanged  1=LineConnected   2=LineDisconnected  3=LineBusy
    //   4=ConfigChanged     5=SpeedDial        6=Group             7=Chat
    //   8=Callback          9=Voicemail       10=Presence/Appearance
    //  11=LogonSucceeded   12=LogonFailed     13=LogoffSucceeded
    private void OnLineMgrNotification(int msg, int param)
    {
        if (msg is 0 or 1 or 2 or 3)
        {
            try
            {
                var linesResult = _lineManager.GetAllLines();
                JsonRpcEmitter.EmitEvent("lineStateChanged", linesResult);
                Logging.Info($"EventSink: lineStateChanged (msg={msg}, param={param})");
            }
            catch (Exception ex)
            {
                Logging.Warn($"EventSink: GetAllLines fehlgeschlagen: {ex.Message}");
                JsonRpcEmitter.EmitEvent("lineStateChanged", new { lines = Array.Empty<object>() });
            }
            return;
        }

        if (msg == 9)
        {
            JsonRpcEmitter.EmitEvent("voicemailNotification", new { msg, param });
            return;
        }

        if (msg == 10)
        {
            JsonRpcEmitter.EmitEvent("presenceNotification", new { msg, param });
            return;
        }

        if (msg == 11)
        {
            Logging.Info($"EventSink: Logon succeeded (param={param})");
            JsonRpcEmitter.EmitEvent("loginSucceeded", new
            {
                server = _connector.Server,
                username = _connector.Username
            });
            return;
        }

        if (msg == 12)
        {
            Logging.Warn($"EventSink: Logon failed (param={param})");
            JsonRpcEmitter.EmitEvent("loginFailed", new
            {
                server = _connector.Server,
                username = _connector.Username,
                errorCode = param
            });
            return;
        }

        if (msg == 13)
        {
            Logging.Info("EventSink: Logoff succeeded.");
            JsonRpcEmitter.EmitEvent("logoutSucceeded", new { });
            return;
        }

        string eventName = msg switch
        {
            4  => "configChanged",
            5  => "speedDialNotification",
            6  => "groupNotification",
            7  => "chatMessage",
            8  => "callbackNotification",
            _  => $"clmgrEvent_{msg}"
        };

        JsonRpcEmitter.EmitEvent(eventName, new { msg, param });
    }
}
