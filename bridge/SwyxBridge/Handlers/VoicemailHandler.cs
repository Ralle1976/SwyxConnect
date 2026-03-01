using System.Text.Json;
using IpPbx.CLMgrLib;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Voicemail-Handler via DispClientConfig.VoiceMessagesEnumerator.
///
/// VoiceMessagesEnumerator liefert eine IDispatch Collection mit Voicemail-Einträgen.
/// DispClientConfig.NumberOfNewVoicemails → Anzahl neuer/ungelesener Nachrichten.
///
/// HINWEIS: DispClientConfig wird als dynamic gehalten, da VoiceMessagesEnumerator
/// und NumberOfNewVoicemails nicht auf einem standardisierten COM-Interface liegen.
/// InvokeVoicemailAction() und DispVoicemailRemoteInquiry() sind ebenfalls nicht
/// im typed interface — dynamic cast erforderlich.
///
/// Fallback:
///   - InvokeVoicemailAction() → Öffnet Standard-Mail-Client
///   - DispVoicemailRemoteInquiry() → Startet Remote-Abfrage
/// </summary>
public sealed class VoicemailHandler
{
    private readonly SwyxConnector _connector;

    public VoicemailHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method switch
    {
        "getVoicemails" or "invokeVoicemail" or "remoteInquiry" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "getVoicemails"   => GetVoicemails(),
                "invokeVoicemail" => InvokeVoicemail(),
                "remoteInquiry"   => RemoteInquiry(),
                _                 => null
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"VoicemailHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    /// <summary>
    /// Liest Voicemail-Liste über DispClientConfig.VoiceMessagesEnumerator
    /// und die Anzahl neuer Nachrichten über NumberOfNewVoicemails.
    /// </summary>
    private object GetVoicemails()
    {
        var com = _connector.GetCom();
        if (com == null) return new { messages = Array.Empty<object>(), newCount = 0 };

        int newCount = 0;

        // Versuch 1: DispClientConfig für Voicemails
        // dynamic cfg: VoiceMessagesEnumerator und NumberOfNewVoicemails sind nicht
        // im typed COM-Interface des SDKs verfügbar — dynamic Zugriff nötig.
        try
        {
            dynamic cfg = com.DispClientConfig;

            // Anzahl neuer Voicemails
            try
            {
                newCount = (int)cfg.NumberOfNewVoicemails;
                Logging.Info($"VoicemailHandler: NumberOfNewVoicemails = {newCount}");
            }
            catch (Exception ex)
            {
                Logging.Warn($"VoicemailHandler: NumberOfNewVoicemails: {ex.Message}");
            }

            // VoiceMessagesEnumerator
            try
            {
                dynamic vmEnum = cfg.VoiceMessagesEnumerator;
                if (vmEnum != null)
                {
                    return GetVoicemailsFromEnumerator(vmEnum, newCount);
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"VoicemailHandler: VoiceMessagesEnumerator: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"VoicemailHandler: DispClientConfig: {ex.Message}");
        }

        // Kein Enumerator verfügbar — nur newCount zurückgeben
        Logging.Info($"VoicemailHandler: Kein VoiceMessagesEnumerator, newCount={newCount}");
        return new { messages = Array.Empty<object>(), newCount };
    }

    /// <summary>
    /// Iteriert den VoiceMessagesEnumerator und extrahiert Voicemail-Details.
    /// </summary>
    private object GetVoicemailsFromEnumerator(dynamic vmEnum, int newCount)
    {
        int count = 0;
        try { count = (int)vmEnum.Count; }
        catch
        {
            try { count = (int)vmEnum.DispCount; } catch { }
        }

        if (count == 0)
        {
            Logging.Info("VoicemailHandler: VoiceMessagesEnumerator ist leer.");
            return new { messages = Array.Empty<object>(), newCount };
        }

        Logging.Info($"VoicemailHandler: VoiceMessagesEnumerator hat {count} Einträge.");

        var messages = new List<object>();
        int maxEntries = Math.Min(count, 50);

        for (int i = 0; i < maxEntries; i++)
        {
            try
            {
                dynamic item = vmEnum.Item(i);

                string callerName   = TryGetString(item, "CallerName", "Name", "DispCallerName", "SenderName") ?? "";
                string callerNumber = TryGetString(item, "CallerNumber", "Number", "DispCallerNumber", "SenderNumber") ?? "";
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int duration = 0;
                bool isNew = i < newCount; // Heuristik: die ersten N sind "neu"

                // Zeitstempel
                try
                {
                    object? dateObj = TryGetProperty(item, "TimeStamp", "Date", "DateTime", "ReceivedDate", "DispTimeStamp");
                    if (dateObj is DateTime dt)
                        timestamp = new DateTimeOffset(dt).ToUnixTimeSeconds();
                    else if (dateObj is string dateStr && DateTime.TryParse(dateStr, out var parsed))
                        timestamp = new DateTimeOffset(parsed).ToUnixTimeSeconds();
                }
                catch { }

                // Dauer
                try { duration = TryGetInt(item, "Duration", "DispDuration", "MessageDuration"); }
                catch { }

                // IsNew / IsRead
                try
                {
                    object? readObj = TryGetProperty(item, "IsNew", "IsRead", "New", "Read", "DispIsNew");
                    if (readObj is bool b) isNew = b;
                    else if (readObj is int n) isNew = n != 0;
                }
                catch { }

                messages.Add(new
                {
                    id = $"vm_{i}_{timestamp}",
                    callerName,
                    callerNumber,
                    timestamp,
                    duration,
                    isNew
                });
            }
            catch (Exception ex)
            {
                Logging.Warn($"VoicemailHandler: Eintrag[{i}]: {ex.Message}");
            }
        }

        Logging.Info($"VoicemailHandler: {messages.Count} Voicemails geladen, {newCount} neu.");
        return new { messages = messages.ToArray(), newCount };
    }

    private object InvokeVoicemail()
    {
        var com = _connector.GetCom();
        if (com == null) return new { ok = false, error = "COM not connected" };

        try
        {
            // InvokeVoicemailAction ist nicht im typed interface — dynamic cast
            ((dynamic)com).InvokeVoicemailAction();
            Logging.Info("VoicemailHandler: InvokeVoicemailAction aufgerufen.");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"VoicemailHandler: InvokeVoicemailAction: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object RemoteInquiry()
    {
        var com = _connector.GetCom();
        if (com == null) return new { ok = false, error = "COM not connected" };

        try
        {
            // DispVoicemailRemoteInquiry ist nicht im typed interface — dynamic cast
            ((dynamic)com).DispVoicemailRemoteInquiry();
            Logging.Info("VoicemailHandler: DispVoicemailRemoteInquiry aufgerufen.");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"VoicemailHandler: DispVoicemailRemoteInquiry: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── COM Property Helper (für untypisierte Voicemail-Items) ─────────────

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
}
