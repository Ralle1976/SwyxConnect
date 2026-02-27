using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Voicemail-Handler via CLMgr COM VoiceMessagesCollection.
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
        "getVoicemails" or "markVoicemailRead" or "deleteVoicemail" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "getVoicemails"     => GetVoicemails(),
                "markVoicemailRead" => MarkRead(req.Params),
                "deleteVoicemail"   => Delete(req.Params),
                _                  => null
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

    private object GetVoicemails()
    {
        var com = _connector.GetCom();
        if (com == null) return Array.Empty<object>();

        try
        {
            // Versuche VoiceMessagesCollection
            var messages = com.DispVoiceMessagesCollection;
            if (messages == null)
            {
                Logging.Warn("VoicemailHandler: DispVoiceMessagesCollection ist null.");
                return Array.Empty<object>();
            }

            int count = (int)messages.Count;
            Logging.Info($"VoicemailHandler: {count} Nachrichten gefunden.");

            var result = new List<object>();
            for (int i = 1; i <= count; i++)
            {
                try
                {
                    var msg = messages.Item(i);
                    if (msg == null) continue;

                    result.Add(new
                    {
                        id           = SafeString(() => (string)(msg.DispMsgId          ?? i.ToString())),
                        callerName   = SafeString(() => (string)(msg.DispCallerName      ?? "")),
                        callerNumber = SafeString(() => (string)(msg.DispCallerNumber    ?? "")),
                        timestamp    = SafeLong(  () => (long)msg.DispTimeStamp),
                        duration     = SafeInt(   () => (int)msg.DispDuration),
                        isNew        = SafeBool(  () => (bool)msg.DispIsNew),
                    });
                }
                catch (Exception ex)
                {
                    Logging.Warn($"VoicemailHandler: Eintrag {i} fehlgeschlagen: {ex.Message}");
                }
            }
            return result.ToArray();
        }
        catch (Exception ex)
        {
            Logging.Warn($"VoicemailHandler: COM-Zugriff fehlgeschlagen: {ex.Message}");
            return Array.Empty<object>();
        }
    }

    private object MarkRead(JsonElement? p)
    {
        string id = GetString(p, "id");
        Logging.Info($"VoicemailHandler: markRead(id={id}) – noch nicht über COM implementiert.");
        return new { id, ok = true };
    }

    private object Delete(JsonElement? p)
    {
        string id = GetString(p, "id");
        Logging.Info($"VoicemailHandler: delete(id={id}) – noch nicht über COM implementiert.");
        return new { id, ok = true };
    }

    private static string GetString(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetString() ?? "";
        return "";
    }

    private static string SafeString(Func<string> f) { try { return f(); } catch { return ""; } }
    private static long   SafeLong(Func<long> f)     { try { return f(); } catch { return 0; } }
    private static int    SafeInt(Func<int> f)        { try { return f(); } catch { return 0; } }
    private static bool   SafeBool(Func<bool> f)      { try { return f(); } catch { return false; } }
}
