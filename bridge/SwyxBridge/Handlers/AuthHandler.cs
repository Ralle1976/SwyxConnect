using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Verarbeitet Auth-Session JSON-RPC Methoden: login, logout, getSessionStatus
/// </summary>
public sealed class AuthHandler
{
    private readonly SessionManager _sessionManager;

    public AuthHandler(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public bool CanHandle(string method)
    {
        return method is "login" or "logout" or "getSessionStatus";
    }

    public void Handle(JsonRpcRequest request)
    {
        try
        {
            object? result = request.Method switch
            {
                "login" => HandleLogin(request.Params),
                "logout" => HandleLogout(request.Params),
                "getSessionStatus" => HandleGetSessionStatus(request.Params),
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

    private object HandleLogin(JsonElement? param)
    {
        if (param == null)
        {
            return new { ok = false, error = "Invalid params" };
        }

        var elem = param.Value;
        var server = elem.TryGetProperty("server", out var s) ? s.GetString() : null;
        var backupServer = elem.TryGetProperty("backupServer", out var bs) ? bs.GetString() : null;
        var username = elem.TryGetProperty("username", out var u) ? u.GetString() : null;
        var password = elem.TryGetProperty("password", out var p) ? p.GetString() : null;
        var authMode = elem.TryGetProperty("authMode", out var am) ? am.GetInt32() : 1;
        var ctiMaster = elem.TryGetProperty("ctiMaster", out var ct) && ct.GetBoolean();

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(username))
        {
            return new { ok = false, error = "Server und Benutzername sind erforderlich" };
        }

        var loginResult = _sessionManager.LoginAsync(
            server!,
            backupServer,
            username!,
            password ?? "",
            authMode,
            ctiMaster
        ).Result;

        return loginResult.Success
            ? new { ok = true, server = server, username = username }
            : new { ok = false, error = loginResult.ErrorMessage };
    }

    private object HandleLogout(JsonElement? param)
    {
        _sessionManager.LogoutAsync().Wait();
        return new { ok = true };
    }

    private object HandleGetSessionStatus(JsonElement? param)
    {
        var status = _sessionManager.GetSessionStatusAsync().Result;
        return new
        {
            isAuthenticated = status.IsAuthenticated,
            server = status.Server,
            username = status.Username,
            error = status.Error
        };
    }
}