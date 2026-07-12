using System.Text.Json;
using SwyxStandalone.Com;
using SwyxStandalone.JsonRpc;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Handlers;

public sealed class SystemHandler
{
    private readonly StandaloneConnector _connector;
    private readonly LineManager _lineManager;
    private EventSink? _eventSink;

    public SystemHandler(StandaloneConnector connector, LineManager lineManager)
    {
        _connector = connector;
        _lineManager = lineManager;
    }

    public bool CanHandle(string method) => method switch
    {
        "login" or "logout" or "getStatus" or "getSessionStatus" or "setLines" or "setNumberOfLines" or "ping" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "login"              => HandleLogin(req.Params),
                "logout"             => HandleLogout(),
                "getStatus"          => HandleGetStatus(),
                "getSessionStatus"   => HandleGetStatus(), // Alias for Electron compatibility
                "setLines"           => HandleSetLines(req.Params),
                "setNumberOfLines"   => HandleSetLines(req.Params), // Alias
                "ping"               => new { pong = true, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                _ => throw new InvalidOperationException($"Unbekannte Methode: {req.Method}")
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"SystemHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object HandleLogin(JsonElement? p)
    {
        if (p == null || p.Value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Parameter fehlt: { server, username, password, backupServer?, authMode?, ctiMaster? }");

        string server       = GetString(p, "server")       ?? throw new ArgumentException("'server' fehlt.");
        string username     = GetString(p, "username")     ?? throw new ArgumentException("'username' fehlt.");
        string password     = GetString(p, "password")     ?? throw new ArgumentException("'password' fehlt.");
        string backupServer = GetString(p, "backupServer") ?? "";
        int authMode        = GetOptionalInt(p, "authMode", 1);
        bool ctiMaster      = GetOptionalBool(p, "ctiMaster", false);

        if (_connector.IsLoggedIn)
        {
            Logging.Warn("SystemHandler: Login-Anfrage obwohl bereits eingeloggt — ignoriert.");
            return new { ok = false, error = "Already logged in. Call logout first." };
        }

        if (!_connector.IsConnected)
            _connector.CreateComObject();

        _connector.Login(server, username, password, backupServer, authMode, ctiMaster);

        _eventSink = EventSink.Subscribe(_connector, _lineManager);

        JsonRpcEmitter.EmitEvent("bridgeState", new { state = "connected", server, username });

        return new { ok = true, server, username };
    }

    private object HandleLogout()
    {
        if (!_connector.IsLoggedIn)
            return new { ok = false, error = "Not logged in." };

        EventSink.Unsubscribe();
        _eventSink = null;
        _connector.Logout();

        JsonRpcEmitter.EmitEvent("bridgeState", new { state = "disconnected" });
        return new { ok = true };
    }

    private object HandleGetStatus()
    {
        int lineCount = 0;
        try { lineCount = _lineManager.GetLineCount(); } catch { }

        return new
        {
            connected = _connector.IsConnected,
            loggedIn  = _connector.IsLoggedIn,
            server    = _connector.Server,
            username  = _connector.Username,
            lineCount
        };
    }

    private object HandleSetLines(JsonElement? p)
    {
        int count = 2;
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("count", out var v))
            count = v.GetInt32();

        if (count < 1 || count > 16)
            throw new ArgumentOutOfRangeException(nameof(count), "Leitungsanzahl muss zwischen 1 und 16 liegen.");

        _lineManager.SetNumberOfLines(count);
        return new { ok = true, count };
    }

    private static string? GetString(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetString();
        return null;
    }

    private static int GetOptionalInt(JsonElement? p, string key, int defaultValue)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        return defaultValue;
    }

    private static bool GetOptionalBool(JsonElement? p, string key, bool defaultValue)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.ValueKind == JsonValueKind.True || (val.ValueKind == JsonValueKind.Number && val.GetInt32() != 0);
        return defaultValue;
    }
}
