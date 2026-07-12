using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

public sealed class CtiHandler
{
    private readonly SwyxConnector _connector;
    private bool _cstaSessionActive;
    private bool _cstaMonitorActive;

    public CtiHandler(SwyxConnector connector)
    {
        _connector = connector;
        _cstaSessionActive = false;
        _cstaMonitorActive = false;
    }

    public bool CanHandle(string method)
    {
        return method is "startCstaSession" or "stopCstaSession" or 
                       "startCstaMonitor" or "stopCstaMonitor" or 
                       "saveCtiPairing" or "getCtiPairing" or 
                       "getCtiSettings" or "setCtiSettings";
    }

    public void Handle(JsonRpcRequest request)
    {
        try
        {
            object? result = request.Method switch
            {
                "startCstaSession" => HandleStartCstaSession(request.Params),
                "stopCstaSession" => HandleStopCstaSession(request.Params),
                "startCstaMonitor" => HandleStartCstaMonitor(request.Params),
                "stopCstaMonitor" => HandleStopCstaMonitor(request.Params),
                "saveCtiPairing" => HandleSaveCtiPairing(request.Params),
                "getCtiPairing" => HandleGetCtiPairing(request.Params),
                "getCtiSettings" => HandleGetCtiSettings(request.Params),
                "setCtiSettings" => HandleSetCtiSettings(request.Params),
                _ => throw new InvalidOperationException($"Unknown method: {request.Method}")
            };

            if (request.Id.HasValue)
                JsonRpcEmitter.EmitResponse(request.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            if (request.Id.HasValue)
                JsonRpcEmitter.EmitError(request.Id.Value, -32603, ex.Message);
        }
    }

    private object HandleStartCstaSession(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            int result = clmgr.StartCstaSession();
            _cstaSessionActive = (result == 0);

            if (_cstaSessionActive)
            {
                Logging.Info("CtiHandler: CSTA Session gestartet");
                return new { ok = true };
            }
            else
            {
                return new { ok = false, error = $"CSTA Session Fehler: {result}" };
            }
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: StartCstaSession failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleStopCstaSession(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            clmgr.StopCstaSession();
            _cstaSessionActive = false;
            Logging.Info("CtiHandler: CSTA Session gestoppt");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: StopCstaSession failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleStartCstaMonitor(JsonElement? param)
    {
        if (param == null) return new { ok = false, error = "Invalid params" };

        var elem = param.Value;
        var deviceId = elem.TryGetProperty("deviceId", out var d) ? d.GetString() : null;

        if (string.IsNullOrEmpty(deviceId))
        {
            return new { ok = false, error = "deviceId erforderlich" };
        }

        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            clmgr.StartCstaMonitor(deviceId);
            _cstaMonitorActive = true;
            Logging.Info($"CtiHandler: CSTA Monitor gestartet für Device={deviceId}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: StartCstaMonitor failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleStopCstaMonitor(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            clmgr.StopCstaMonitor();
            _cstaMonitorActive = false;
            Logging.Info("CtiHandler: CSTA Monitor gestoppt");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: StopCstaMonitor failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleSaveCtiPairing(JsonElement? param)
    {
        if (param == null) return new { ok = false, error = "Invalid params" };

        var elem = param.Value;
        var deviceId = elem.TryGetProperty("deviceId", out var d) ? d.GetString() : null;

        if (string.IsNullOrEmpty(deviceId))
        {
            return new { ok = false, error = "deviceId erforderlich" };
        }

        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            clmgr.SaveCtiPairing(deviceId);
            Logging.Info($"CtiHandler: CTI Pairing gespeichert für Device={deviceId}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: SaveCtiPairing failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleGetCtiPairing(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            var pairing = clmgr.GetCstaPairing();
            return new { ok = true, pairing };
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: GetCtiPairing failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleGetCtiSettings(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            var settings = clmgr.get_DispCtiSettings();
            return new { ok = true, settings };
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: GetCtiSettings failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleSetCtiSettings(JsonElement? param)
    {
        if (param == null) return new { ok = false, error = "Invalid params" };

        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            clmgr.put_DispCtiSettings(param.Value);
            Logging.Info("CtiHandler: CTI Settings aktualisiert");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Error($"CtiHandler: SetCtiSettings failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }
}