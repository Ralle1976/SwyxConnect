using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Anrufverlauf via DispClientConfig.CallerEnumerator.
/// 
/// CallerEnumerator liefert eine IDispatch Collection mit Anrufhistorie-Einträgen.
/// Jeder Eintrag hat typischerweise: Name, Number, Date/Time, Duration, CallType.
///
/// Fallback: get_DispNumberHistory(out name, out number, out date, out time, out duration, out type)
/// → Liefert nur den letzten Anruf.
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

        // Versuch 1: DispClientConfig.CallerEnumerator
        try
        {
            return GetHistoryViaCallerEnumerator(com);
        }
        catch (Exception ex)
        {
            Logging.Warn($"HistoryHandler: CallerEnumerator fehlgeschlagen: {ex.Message}");
        }

        // Versuch 2: get_DispNumberHistory (nur letzter Eintrag)
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
    /// Liest die Anrufliste über DispClientConfig.CallerEnumerator.
    /// </summary>
    private object[] GetHistoryViaCallerEnumerator(dynamic com)
    {
        dynamic cfg = com.DispClientConfig;
        dynamic callerEnum = cfg.CallerEnumerator;

        if (callerEnum == null)
        {
            Logging.Warn("HistoryHandler: CallerEnumerator ist null.");
            return Array.Empty<object>();
        }

        int count = 0;
        try { count = (int)callerEnum.Count; }
        catch
        {
            try { count = (int)callerEnum.DispCount; } catch { }
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
                dynamic item = callerEnum.Item(i);

                // Verschiedene Property-Namen probieren (COM-Interface-Varianten)
                string callerName = TryGetString(item, "Name", "CallerName", "DispName", "DisplayName") ?? "";
                string callerNumber = TryGetString(item, "Number", "CallerNumber", "DispNumber", "PhoneNumber") ?? "";
                string direction = "inbound";
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int duration = 0;

                // Richtung (CallType / Direction / Type)
                try
                {
                    int callType = TryGetInt(item, "CallType", "Type", "Direction", "DispCallType");
                    direction = callType switch
                    {
                        0 => "inbound",
                        1 => "outbound",
                        2 => "missed",
                        3 => "forwarded",
                        _ => "inbound"
                    };
                }
                catch { }

                // Zeitstempel
                try
                {
                    object dateObj = TryGetProperty(item, "Date", "TimeStamp", "DateTime", "DispDate", "StartTime");
                    if (dateObj is DateTime dt)
                        timestamp = new DateTimeOffset(dt).ToUnixTimeSeconds();
                    else if (dateObj is string dateStr && DateTime.TryParse(dateStr, out var parsed))
                        timestamp = new DateTimeOffset(parsed).ToUnixTimeSeconds();
                }
                catch { }

                // Dauer
                try
                {
                    duration = TryGetInt(item, "Duration", "DispDuration", "CallDuration");
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

        Logging.Info($"HistoryHandler: {entries.Count} History-Einträge geladen via CallerEnumerator.");
        return entries.ToArray();
    }

    /// <summary>
    /// Fallback: Liest nur den letzten Anruf über get_DispNumberHistory.
    /// </summary>
    private object[] GetHistoryViaDispNumberHistory(dynamic com)
    {
        string name = "", number = "", date = "", time = "", duration = "", type = "";
        com.get_DispNumberHistory(ref name, ref number, ref date, ref time, ref duration, ref type);

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

    // ─── COM Property Helper ─────────────────────────────────────────────────

    private static string? TryGetString(dynamic item, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                object val = item.GetType().InvokeMember(name,
                    System.Reflection.BindingFlags.GetProperty, null, item, null);
                if (val != null) return val.ToString();
            }
            catch { }
        }
        return null;
    }

    private static int TryGetInt(dynamic item, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                object val = item.GetType().InvokeMember(name,
                    System.Reflection.BindingFlags.GetProperty, null, item, null);
                if (val != null) return Convert.ToInt32(val);
            }
            catch { }
        }
        return 0;
    }

    private static object? TryGetProperty(dynamic item, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                return item.GetType().InvokeMember(name,
                    System.Reflection.BindingFlags.GetProperty, null, item, null);
            }
            catch { }
        }
        return null;
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
