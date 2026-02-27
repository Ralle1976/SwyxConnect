using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Event-Sink für CLMgr PubOnLineMgrNotification Events.
/// CRITICAL: Instanz MUSS in einem static Feld gehalten werden,
/// damit der GC sie nicht einsammelt während COM eine Referenz hält.
/// </summary>
public sealed class EventSink
{
    // CRITICAL: Static reference — GC darf dieses Objekt NICHT einsammeln!
    private static EventSink? _staticInstance;
    private static Action<int, int>? _staticDelegate;

    private readonly SwyxConnector _connector;

    private EventSink(SwyxConnector connector)
    {
        _connector = connector;
    }

    /// <summary>
    /// Erstellt und registriert einen neuen Event-Sink.
    /// MUSS auf dem STA-Thread aufgerufen werden.
    /// MUSS nach jedem Reconnect NEU erstellt werden (nie wiederverwenden).
    /// </summary>
    public static EventSink Subscribe(SwyxConnector connector)
    {
        // Alten Sink trennen falls vorhanden
        Unsubscribe();

        var sink = new EventSink(connector);
        _staticInstance = sink; // GC-Schutz
        _staticDelegate = sink.OnLineMgrNotification; // GC-Schutz für Delegate

        var com = connector.GetCom();
        if (com == null)
            throw new InvalidOperationException("COM nicht verbunden.");

        try
        {
            ((dynamic)com).PubOnLineMgrNotification += _staticDelegate;
            Logging.Info("EventSink: Registriert für PubOnLineMgrNotification.");
        }
        catch (Exception ex)
        {
            _staticInstance = null;
            _staticDelegate = null;
            throw new InvalidOperationException(
                $"Event-Registrierung fehlgeschlagen: {ex.Message}. " +
                "Möglicherweise funktionieren .NET 8 COM Events nicht — Fallback auf .NET Fx 4.8.", ex);
        }

        return sink;
    }

    /// <summary>
    /// Trennt den Event-Sink. Sicher aufrufbar, auch wenn nicht verbunden.
    /// </summary>
    public static void Unsubscribe()
    {
        if (_staticInstance == null || _staticDelegate == null) return;

        try
        {
            var com = _staticInstance._connector.GetCom();
            if (com != null)
            {
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

    /// <summary>
    /// Event-Handler — wird vom COM STA-Thread aufgerufen.
    /// Emitted JSON-RPC Events an stdout.
    /// </summary>
    private void OnLineMgrNotification(int msg, int param)
    {
        string eventName = msg switch
        {
            0 => "lineStateChanged",
            1 => "lineSelectionChanged",
            2 => "lineDetailsChanged",
            3 => "lineDetailsChangedEx",
            4 => "configChanged",
            5 => "speedDialNotification",
            6 => "groupNotification",
            7 => "chatMessage",
            8 => "callbackNotification",
            9 => "voicemailNotification",
            10 => "presenceNotification",
            _ => $"unknown_{msg}"
        };

        JsonRpcEmitter.EmitEvent(eventName, new { msg, param });
    }
}
