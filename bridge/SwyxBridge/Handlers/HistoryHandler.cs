using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Anrufverlauf via CLMgr COM-Interface.
/// Versucht echte Daten, fällt bei Fehler auf leeres Array zurück.
/// </summary>
public sealed class HistoryHandler
{
    private readonly SwyxConnector _connector;

    public HistoryHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method == "getCallHistory";

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            var entries = GetCallHistoryEntries();
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, entries);
        }
        catch (Exception ex)
        {
            Logging.Error($"HistoryHandler: fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object[] GetCallHistoryEntries()
    {
        var com = _connector.GetCom();
        if (com == null) return Array.Empty<object>();

        try
        {
            // Versuche ClCallHistCollection über COM
            var history = com.DispCallHistCollection;
            if (history == null)
            {
                Logging.Warn("HistoryHandler: DispCallHistCollection ist null – kein Verlauf verfügbar.");
                return Array.Empty<object>();
            }

            int count = (int)history.Count;
            Logging.Info($"HistoryHandler: {count} Einträge gefunden.");

            var result = new List<object>();
            for (int i = 1; i <= count; i++) // COM-Collections sind 1-basiert
            {
                try
                {
                    var entry = history.Item(i);
                    if (entry == null) continue;

                    string callerName   = SafeString(() => (string)(entry.DispCallerName   ?? ""));
                    string callerNumber = SafeString(() => (string)(entry.DispCallerNumber ?? ""));
                    long   timestamp    = SafeLong(  () => (long)entry.DispCallTime);
                    int    duration     = SafeInt(   () => (int)entry.DispCallDuration);
                    // DispCallType: 0=inbound, 1=outbound, 2=missed (Swyx-abhängig)
                    int    callType     = SafeInt(   () => (int)entry.DispCallType);

                    string direction = callType switch
                    {
                        0 => "inbound",
                        1 => "outbound",
                        2 => "missed",
                        _ => "inbound"
                    };

                    result.Add(new
                    {
                        id           = i.ToString(),
                        callerName,
                        callerNumber,
                        direction,
                        timestamp,
                        duration
                    });
                }
                catch (Exception ex)
                {
                    Logging.Warn($"HistoryHandler: Eintrag {i} fehlgeschlagen: {ex.Message}");
                }
            }
            return result.ToArray();
        }
        catch (Exception ex)
        {
            Logging.Warn($"HistoryHandler: COM-Zugriff fehlgeschlagen: {ex.Message}");
            return Array.Empty<object>();
        }
    }

    private static string SafeString(Func<string> f) { try { return f(); } catch { return ""; } }
    private static long   SafeLong(Func<long> f)     { try { return f(); } catch { return 0; } }
    private static int    SafeInt(Func<int> f)       { try { return f(); } catch { return 0; } }
}
