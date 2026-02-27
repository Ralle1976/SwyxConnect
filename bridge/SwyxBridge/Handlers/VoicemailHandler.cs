using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Voicemail-bezogene JSON-RPC Methoden.
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
                "getVoicemails" => GetVoicemails(),
                "markVoicemailRead" => MarkRead(req.Params),
                "deleteVoicemail" => Delete(req.Params),
                _ => null
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
        // TODO: Implementierung über VoiceMessagesCollection
        Logging.Info("VoicemailHandler: getVoicemails (Stub)");
        return new { messages = Array.Empty<object>() };
    }

    private object MarkRead(JsonElement? p)
    {
        string id = "";
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("id", out var val))
            id = val.GetString() ?? "";

        Logging.Info($"VoicemailHandler: markRead(id={id}) (Stub)");
        return new { id, ok = true };
    }

    private object Delete(JsonElement? p)
    {
        string id = "";
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("id", out var val))
            id = val.GetString() ?? "";

        Logging.Info($"VoicemailHandler: delete(id={id}) (Stub)");
        return new { id, ok = true };
    }
}
