using System.Text.Json;
using IpPbx.CLMgrLib;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Anrufverlauf via DispClientConfig.CallerEnumerator.
///
/// Verwendet typisierte CallerCollectionClass und CallerItemClass aus IpPbx.CLMgrLib.
/// CallerItemClass bietet: Name, Number, Time (DateTime), CallDuration, CallState,
/// DialedNumber, DialedName, ConnectedName, CallbackState, Viewed, Idx.
///
/// Fallback: get_DispNumberHistory (nur letzter Eintrag) via dynamic cast.
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

        // Versuch 1: DispClientConfig.CallerEnumerator (typisiert)
        try
        {
            return GetHistoryViaCallerEnumerator(com);
        }
        catch (Exception ex)
        {
            Logging.Warn($"HistoryHandler: CallerEnumerator fehlgeschlagen: {ex.Message}");
        }

        // Versuch 2: get_DispNumberHistory (nur letzter Eintrag, dynamic da nicht im typed interface)
        try
        {
            return GetHistoryViaDispNumberHistory(com);
        }
        catch (Exception ex)
        {
            Logging.Info($"HistoryHandler: DispNumberHistory auch fehlgeschlagen: {ex.Message}");
        }

        return Array.Empty<object>();
    }

    /// <summary>
    /// Liest die Anrufliste über typisierte CallerCollectionClass / CallerItemClass.
    /// </summary>
    private object[] GetHistoryViaCallerEnumerator(ClientLineMgrClass com)
    {
        // DispClientConfig returns object — cast to ClientConfigClass for typed CallerEnumerator access
        var cfgObj = com.DispClientConfig;
        if (cfgObj == null)
        {
            Logging.Warn("HistoryHandler: DispClientConfig ist null.");
            return Array.Empty<object>();
        }

        // CallerEnumerator property is on ClientConfigClass — use dynamic to access it
        // since IClientConfig interface may not expose CallerEnumerator directly
        dynamic cfg = cfgObj;
        CallerCollectionClass? callerColl = null;

        try
        {
            var enumObj = cfg.CallerEnumerator;
            callerColl = enumObj as CallerCollectionClass;
        }
        catch (Exception ex)
        {
            Logging.Warn($"HistoryHandler: CallerEnumerator Zugriff fehlgeschlagen: {ex.Message}");
            return Array.Empty<object>();
        }

        if (callerColl == null)
        {
            Logging.Warn("HistoryHandler: CallerEnumerator ist null oder falscher Typ.");
            return Array.Empty<object>();
        }

        int count = 0;
        try { count = callerColl.Count; }
        catch (Exception ex)
        {
            Logging.Warn($"HistoryHandler: CallerCollection.Count fehlgeschlagen: {ex.Message}");
            return Array.Empty<object>();
        }

        if (count == 0)
        {
            Logging.Info("HistoryHandler: CallerEnumerator ist leer.");
            return Array.Empty<object>();
        }

        Logging.Info($"HistoryHandler: CallerEnumerator hat {count} Einträge.");

        var entries = new List<object>();
        int maxEntries = Math.Min(count, 100); // Max 100 Einträge

        for (int i = 0; i < maxEntries; i++)
        {
            try
            {
                // Item() takes object index (COM convention)
                var itemObj = callerColl.Item((object)i);
                if (itemObj is not CallerItemClass item) continue;

                string callerName   = item.Name ?? "";
                string callerNumber = item.Number ?? "";
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int duration = 0;
                string direction = "inbound";

                // Zeitstempel aus typisierten DateTime property
                try
                {
                    timestamp = new DateTimeOffset(item.Time).ToUnixTimeSeconds();
                }
                catch { }

                // Dauer
                try { duration = item.CallDuration; } catch { }

                // Richtung aus CallState: 0=inbound, 1=outbound, 2=missed, 3=forwarded
                try
                {
                    direction = item.CallState switch
                    {
                        0 => "inbound",
                        1 => "outbound",
                        2 => "missed",
                        3 => "forwarded",
                        _ => "inbound"
                    };
                }
                catch { }

                if (!string.IsNullOrEmpty(callerNumber) || !string.IsNullOrEmpty(callerName))
                {
                    entries.Add(new
                    {
                        id = $"hist_{i}_{timestamp}",
                        callerName,
                        callerNumber,
                        direction,
                        timestamp,
                        duration
                    });
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"HistoryHandler: Eintrag[{i}]: {ex.Message}");
            }
        }

        Logging.Info($"HistoryHandler: {entries.Count} History-Einträge geladen via CallerEnumerator (typisiert).");
        return entries.ToArray();
    }

    /// <summary>
    /// Fallback: Liest nur den letzten Anruf über get_DispNumberHistory.
    /// Diese Methode ist nicht im typed interface — dynamic cast erforderlich.
    /// </summary>
    private object[] GetHistoryViaDispNumberHistory(ClientLineMgrClass com)
    {
        string name = "", number = "", date = "", time = "", duration = "", type = "";
        // get_DispNumberHistory ist nicht im IClientLineMgrDisp interface dokumentiert — dynamic cast
        ((dynamic)com).get_DispNumberHistory(ref name, ref number, ref date, ref time, ref duration, ref type);

        if (string.IsNullOrEmpty(number) && string.IsNullOrEmpty(name))
            return Array.Empty<object>();

        string direction = type?.ToLowerInvariant() switch
        {
            "0" or "inbound" or "in" => "inbound",
            "1" or "outbound" or "out" => "outbound",
            "2" or "missed" => "missed",
            _ => "inbound"
        };

        return new object[]
        {
            new
            {
                id = $"last_{DateTime.Now.Ticks}",
                callerName = name ?? "",
                callerNumber = number ?? "",
                direction,
                timestamp = ParseDateTimeToUnix(date, time),
                duration = int.TryParse(duration, out var d) ? d : 0
            }
        };
    }

    private static long ParseDateTimeToUnix(string? date, string? time)
    {
        if (string.IsNullOrEmpty(date)) return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            string combined = $"{date} {time}".Trim();
            if (DateTime.TryParse(combined, out var dt))
                return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }
        catch { }
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
