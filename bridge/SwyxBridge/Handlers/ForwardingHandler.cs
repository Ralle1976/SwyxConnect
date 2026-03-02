using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Weiterleitungs- und Anruf-Routing-bezogene JSON-RPC Methoden.
///
/// COM-Methoden:
///   line.DispForwardCall(string number)          → Leitet aktuellen Anruf weiter
///   com.OpenCallRouting()                         → Öffnet Anrufweiterleitung-Dialog
///   com.DispResolveNumber(string number)          → Löst Nummer in Namen auf
///   com.DispConvertNumber(uint format, string)    → Konvertiert Nummernformat
///   com.DispRequestCallbackOnBusy(name, number)   → Rückruf bei Besetzt anfordern
///   com.DispPickupGroupNotificationCall(int refId) → Gruppenruf übernehmen
///   com.GetNotificationCallRefIds()               → Aktive Gruppenruf-IDs (Variant)
/// </summary>
public sealed class ForwardingHandler
{
    private readonly SwyxConnector _connector;
    private readonly LineManager _lm;

    public ForwardingHandler(SwyxConnector connector, LineManager lm)
    {
        _connector = connector;
        _lm = lm;
    }

    public bool CanHandle(string method) => method switch
    {
        "forwardCall" or "openCallRouting" or "resolveNumber" or "convertNumber"
        or "requestCallbackOnBusy" or "pickupGroupCall" or "getGroupNotifications" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "forwardCall"            => HandleForwardCall(req.Params),
                "openCallRouting"        => HandleOpenCallRouting(),
                "resolveNumber"          => HandleResolveNumber(req.Params),
                "convertNumber"          => HandleConvertNumber(req.Params),
                "requestCallbackOnBusy"  => HandleRequestCallbackOnBusy(req.Params),
                "pickupGroupCall"        => HandlePickupGroupCall(req.Params),
                "getGroupNotifications"  => HandleGetGroupNotifications(),
                _ => throw new InvalidOperationException($"Unbekannte Methode: {req.Method}")
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"ForwardingHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    // ─── FORWARD CALL ────────────────────────────────────────────────────────

    private object HandleForwardCall(JsonElement? p)
    {
        int lineId = GetInt(p, "lineId");
        var number = GetString(p, "number")
            ?? throw new ArgumentException("Parameter 'number' fehlt.");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            dynamic line = com.DispGetLine(lineId);
            line.DispForwardCall(number);
            Logging.Info($"ForwardingHandler: forwardCall lineId={lineId} → '{number}'");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"ForwardingHandler: DispForwardCall(lineId={lineId}, number='{number}'): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── OPEN CALL ROUTING ──────────────────────────────────────────────────

    private object HandleOpenCallRouting()
    {
        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.OpenCallRouting();
            Logging.Info("ForwardingHandler: OpenCallRouting aufgerufen.");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"ForwardingHandler: OpenCallRouting: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── RESOLVE NUMBER ─────────────────────────────────────────────────────

    private object HandleResolveNumber(JsonElement? p)
    {
        var number = GetString(p, "number")
            ?? throw new ArgumentException("Parameter 'number' fehlt.");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        string resolvedName = number;
        try
        {
            resolvedName = (string)(com.DispResolveNumber(number) ?? number);
            Logging.Info($"ForwardingHandler: resolveNumber '{number}' → '{resolvedName}'");
        }
        catch (Exception ex)
        {
            Logging.Warn($"ForwardingHandler: DispResolveNumber('{number}'): {ex.Message}");
        }

        return new { number, name = resolvedName };
    }

    // ─── CONVERT NUMBER ─────────────────────────────────────────────────────

    private object HandleConvertNumber(JsonElement? p)
    {
        var number = GetString(p, "number")
            ?? throw new ArgumentException("Parameter 'number' fehlt.");
        int format = GetInt(p, "format");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        string converted = number;
        try
        {
            converted = (string)(com.DispConvertNumber((uint)format, number) ?? number);
            Logging.Info($"ForwardingHandler: convertNumber format={format} '{number}' → '{converted}'");
        }
        catch (Exception ex)
        {
            Logging.Warn($"ForwardingHandler: DispConvertNumber(format={format}, '{number}'): {ex.Message}");
        }

        return new { number, format, converted };
    }

    // ─── REQUEST CALLBACK ON BUSY ────────────────────────────────────────────

    private object HandleRequestCallbackOnBusy(JsonElement? p)
    {
        var name = GetString(p, "name") ?? "";
        var number = GetString(p, "number")
            ?? throw new ArgumentException("Parameter 'number' fehlt.");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.DispRequestCallbackOnBusy(name, number);
            Logging.Info($"ForwardingHandler: requestCallbackOnBusy name='{name}', number='{number}'");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"ForwardingHandler: DispRequestCallbackOnBusy('{name}', '{number}'): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── PICKUP GROUP CALL ───────────────────────────────────────────────────

    private object HandlePickupGroupCall(JsonElement? p)
    {
        int refId = GetInt(p, "refId");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.DispPickupGroupNotificationCall(refId);
            Logging.Info($"ForwardingHandler: pickupGroupCall refId={refId}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"ForwardingHandler: DispPickupGroupNotificationCall(refId={refId}): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── GET GROUP NOTIFICATIONS ─────────────────────────────────────────────

    private object HandleGetGroupNotifications()
    {
        var com = _connector.GetCom();
        if (com == null)
            return new { refIds = Array.Empty<int>() };

        var refIds = new List<int>();

        try
        {
            dynamic variant = com.GetNotificationCallRefIds();
            if (variant != null)
            {
                try
                {
                    // Variant kann ein Array oder eine Collection sein
                    int count = 0;
                    try { count = (int)variant.Count; }
                    catch
                    {
                        try { count = (int)variant.Length; } catch { }
                    }

                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            int id = Convert.ToInt32(variant[i]);
                            refIds.Add(id);
                        }
                        catch (Exception ex)
                        {
                            Logging.Warn($"ForwardingHandler: GetNotificationCallRefIds[{i}]: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Warn($"ForwardingHandler: GetNotificationCallRefIds iterieren: {ex.Message}");

                    // Fallback: versuche als einzelnen Wert
                    try
                    {
                        int id = Convert.ToInt32(variant);
                        refIds.Add(id);
                    }
                    catch { }
                }

                Logging.Info($"ForwardingHandler: getGroupNotifications → {refIds.Count} Einträge.");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ForwardingHandler: GetNotificationCallRefIds: {ex.Message}");
        }

        return new { refIds = refIds.ToArray() };
    }

    // ─── Param Helpers ────────────────────────────────────────────────────────

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
