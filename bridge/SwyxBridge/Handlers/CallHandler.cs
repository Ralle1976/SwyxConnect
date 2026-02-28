using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Anruf-bezogene JSON-RPC Methoden.
/// </summary>
public sealed class CallHandler
{
    private readonly LineManager _lm;

    public CallHandler(LineManager lm)
    {
        _lm = lm;
    }

    public bool CanHandle(string method) => method switch
    {
        "dial" or "answer" or "hangup" or "hold" or "activate"
        or "transfer" or "getLines" or "getLineState" or "getLineDetails"
        or "setNumberOfLines" => true,
        _ => false
    };
    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "dial" => HandleDial(req.Params),
                "answer" => HandleAnswer(req.Params),
                "hangup" => HandleHangup(req.Params),
                "hold" => HandleHold(req.Params),
                "activate" => HandleActivate(req.Params),
                "transfer" => HandleTransfer(req.Params),
                "getLines" => _lm.GetAllLines(),
                "getLineState" => HandleGetLineState(req.Params),
                "getLineDetails" => HandleGetLineDetails(req.Params),
                "setNumberOfLines" => HandleSetNumberOfLines(req.Params),
                _ => throw new InvalidOperationException($"Unbekannte Methode: {req.Method}")
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"CallHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object? HandleDial(JsonElement? p)
    {
        var number = GetString(p, "number")
            ?? throw new ArgumentException("Parameter 'number' fehlt.");
        _lm.Dial(number);
        return new { ok = true };
    }

    private object? HandleAnswer(JsonElement? p)
    {
        int lineId = GetInt(p, "lineId");
        _lm.HookOff(lineId);
        return new { ok = true };
    }

    private object? HandleHangup(JsonElement? p)
    {
        int? lineId = null;
        try { lineId = GetInt(p, "lineId"); } catch { }

        if (lineId.HasValue && lineId.Value >= 0)
        {
            _lm.HookOn(lineId.Value);
        }
        else
        {
            _lm.Hangup();
        }
        return new { ok = true };
    }

    private object? HandleHold(JsonElement? p)
    {
        int lineId = GetInt(p, "lineId");
        _lm.Hold(lineId);
        return new { ok = true };
    }

    private object? HandleActivate(JsonElement? p)
    {
        int lineId = GetInt(p, "lineId");
        _lm.Activate(lineId);
        return new { ok = true };
    }

    private object? HandleTransfer(JsonElement? p)
    {
        int lineId = GetInt(p, "lineId");
        var target = GetString(p, "target")
            ?? throw new ArgumentException("Parameter 'target' fehlt.");
        _lm.Transfer(lineId, target);
        return new { ok = true };
    }

    private object? HandleGetLineState(JsonElement? p)
    {
        int lineId = GetInt(p, "lineId");
        int state = _lm.GetLineState(lineId);
        return new { lineId, state };
    }

    private object? HandleGetLineDetails(JsonElement? p)
    {
        int lineId = GetInt(p, "lineId");
        return _lm.GetLineDetails(lineId);
    }

    private object? HandleSetNumberOfLines(JsonElement? p)
    {
        int count = GetInt(p, "count");
        _lm.SetNumberOfLines(count);
        // Nach dem Setzen: aktualisierte Leitungsdaten zurückgeben UND Event emittieren
        var linesResult = _lm.GetAllLines();
        JsonRpcEmitter.EmitEvent("lineStateChanged", linesResult);
        return linesResult;
    }

    // --- Param Helpers ---

    private static string? GetString(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetString();
        return null;
    }

    private static int GetInt(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        throw new ArgumentException($"Parameter '{key}' fehlt.");
    }
}
