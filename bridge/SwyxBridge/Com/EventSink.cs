using IpPbx.CLMgrLib;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Event-Sink für CLMgr PubOnLineMgrNotification Events.
/// Schickt bei Leitungsänderungen sofort die aktuellen Leitungsdaten mit (lineStateChanged).
/// CRITICAL: Instanz MUSS in einem static Feld gehalten werden.
/// Verwendet typisiertes Delegate: IClientLineMgrEventsPub_PubOnLineMgrNotificationEventHandler
/// </summary>
public sealed class EventSink
{
    private static EventSink? _staticInstance;

    // CRITICAL: Typed delegate MUSS als static Feld gehalten werden (GC-Schutz)
    private static IClientLineMgrEventsPub_PubOnLineMgrNotificationEventHandler? _staticDelegate;

    private readonly SwyxConnector _connector;
    private readonly LineManager _lineManager;

    private EventSink(SwyxConnector connector, LineManager lineManager)
    {
        _connector = connector;
        _lineManager = lineManager;
    }

    public static EventSink Subscribe(SwyxConnector connector, LineManager lineManager)
    {
        Unsubscribe();

        var sink = new EventSink(connector, lineManager);
        _staticInstance = sink;

        // Typisierter Delegate — kein dynamic cast nötig
        _staticDelegate = new IClientLineMgrEventsPub_PubOnLineMgrNotificationEventHandler(
            sink.OnLineMgrNotification);

        var com = connector.GetCom();
        if (com == null)
            throw new InvalidOperationException("COM nicht verbunden.");

        // Cast __ComObject to typed events interface for event subscription
        // __ComObject supports QueryInterface for interfaces (not coclasses)
        ClientLineMgrClass? typed = com as ClientLineMgrClass;

        try
        {
            if (typed != null)
            {
                typed.PubOnLineMgrNotification += _staticDelegate;
            }
            else
            {
                // Fallback: use dynamic event subscription
                ((dynamic)com).PubOnLineMgrNotification += _staticDelegate;
            }
            Logging.Info("EventSink: Registriert für PubOnLineMgrNotification.");
        }
        catch (Exception ex)
        {
            _staticInstance = null;
            _staticDelegate = null;
            throw new InvalidOperationException(
                $"Event-Registrierung fehlgeschlagen: {ex.Message}.", ex);
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
                ClientLineMgrClass? typed = com as ClientLineMgrClass;
                if (typed != null)
                    typed.PubOnLineMgrNotification -= _staticDelegate;
                else
                    ((dynamic)com).PubOnLineMgrNotification -= _staticDelegate;
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

    private void OnLineMgrNotification(int msg, int param)
    {
        // msg 0-3: Leitungsstatus-Änderungen → Leitungsdaten sofort abfragen und mitsenden
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

        // msg 9: Voicemail-Benachrichtigung
        if (msg == 9)
        {
            JsonRpcEmitter.EmitEvent("voicemailNotification", new { msg, param });
            return;
        }

        // msg 10: Presence-Änderung
        if (msg == 10)
        {
            JsonRpcEmitter.EmitEvent("presenceNotification", new { msg, param });
            return;
        }

        // Alle anderen Events
        string eventName = msg switch
        {
            4  => "configChanged",
            5  => "speedDialNotification",
            6  => "groupNotification",
            7  => "chatMessage",
            8  => "callbackNotification",
            _  => $"unknown_{msg}"
        };

        JsonRpcEmitter.EmitEvent(eventName, new { msg, param });
    }
}
