using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Präsenz-bezogene JSON-RPC Methoden.
/// </summary>
public sealed class PresenceHandler
{
    private readonly SwyxConnector _connector;

    public PresenceHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method switch
    {
        "getPresence" or "setPresence" or "getColleaguePresence" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "getPresence" => GetOwnPresence(),
                "setPresence" => SetOwnPresence(req.Params),
                "getColleaguePresence" => GetColleaguePresence(),
                _ => null
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"PresenceHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object GetOwnPresence()
    {
        // TODO: Echte Implementierung über ClientConfig COM-Interface
        var com = _connector.GetCom();
        if (com == null) return new { status = "unknown" };

        // Platzhalter — konkretes Interface hängt von verfügbarer COM-Version ab
        return new { status = "available" };
    }

    private object SetOwnPresence(JsonElement? p)
    {
        // TODO: Implementierung über COM Presence API
        Logging.Info("PresenceHandler: setPresence (Stub)");
        return new { ok = true };
    }

    private object GetColleaguePresence()
    {
        // TODO: Implementierung über UserAppearanceCollection
        Logging.Info("PresenceHandler: getColleaguePresence (Stub)");
        return new { colleagues = Array.Empty<object>() };
    }
}
