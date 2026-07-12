using System.Runtime.InteropServices;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

public sealed class SessionManager : IDisposable
{
    private readonly SwyxConnector _connector;

    private bool _isLoggedIn;
    private string? _server;
    private string? _username;
    private string? _password;
    private int _authMode;
    private bool _ctiMaster;
    private bool _useRemoteConnector;
    private bool _disposed;

    private const int S_OK = 0;

    public SessionManager(SwyxConnector connector)
    {
        _connector = connector;
    }

    /// <summary>
    /// Führt Login via RegisterUserEx durch
    /// </summary>
    public async Task<LoginResult> LoginAsync(
        string server,
        string? backupServer,
        string username,
        string password,
        int authMode,
        bool ctiMaster,
        bool useRemoteConnector = false)
    {
        if (_isLoggedIn)
        {
            Logging.Warn("SessionManager: Bereits angemeldet. Erst Logout durchführen.");
            return new LoginResult(false, "Bereits angemeldet");
        }

        try
        {
            Logging.Info($"SessionManager: Login attempt - Server={server}, User={username}, AuthMode={authMode}");

            // Ensure COM is connected
            if (!_connector.IsConnected)
            {
                _connector.Connect();
            }

            var clmgr = _connector.GetCom();
            if (clmgr == null)
            {
                return new LoginResult(false, "COM-Verbindung nicht verfügbar");
            }
            // Store RemoteConnector setting
            _useRemoteConnector = useRemoteConnector;

            // Call appropriate login method based on mode
            int result;
            try
            {
                if (useRemoteConnector)
                {
                    // RemoteConnector mode: RegisterUserConnector4UC
                    object thumbprint = null;
                    object statusNames = null;
                    result = clmgr.RegisterUserConnector4UC(
                        1, 0, server, backupServer ?? "",
                        ref thumbprint, server, backupServer ?? "",
                        username, password, authMode,
                        ctiMaster ? 1 : 0,
                        out string usernames, statusNames
                    );
                    Logging.Info("SessionManager: Using RemoteConnector mode");
                }
                else
                {
                    // Local mode: RegisterUserEx
                    result = clmgr.RegisterUserEx(
                        server, backupServer ?? "", username, password,
                        authMode, ctiMaster ? 1 : 0, out string usernames
                    );
                }
            }
            catch (COMException ex)
            {
                Logging.Error($"SessionManager: COM exception during login: {ex.Message}");
                return new LoginResult(false, $"COM-Fehler: {ex.Message}");
            }

            if (result == S_OK)
            {
                _isLoggedIn = true;
                _server = server;
                _username = username;
                _password = password;
                _authMode = authMode;
                _ctiMaster = ctiMaster;

                Logging.Info($"SessionManager: Login successful - User={username}");

                JsonRpcEmitter.EmitEvent("sessionStateChanged", new
                {
                    isAuthenticated = true,
                    server,
                    username
                });

                return new LoginResult(true, null);
            }
            else
            {
                var errorMsg = GetLoginErrorMessage(result);
                Logging.Error($"SessionManager: Login failed with code {result}: {errorMsg}");
                return new LoginResult(false, errorMsg);
            }
        }
        catch (Exception ex)
        {
            Logging.Error($"SessionManager: Login exception: {ex.Message}");
            return new LoginResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Führt Logout via ReleaseUserEx durch
    /// </summary>
    public async Task LogoutAsync()
    {
        if (!_isLoggedIn)
        {
            Logging.Warn("SessionManager: Nicht angemeldet, Logout übersprungen");
            return;
        }

        try
        {
            Logging.Info("SessionManager: Logout attempt");

            var clmgr = _connector.GetCom();
            if (clmgr != null)
            {
                clmgr.ReleaseUserEx();
            }

            _isLoggedIn = false;
            _server = null;
            _username = null;
            _password = null;

            Logging.Info("SessionManager: Logout successful");

            JsonRpcEmitter.EmitEvent("sessionStateChanged", new
            {
                isAuthenticated = false,
                server = (string?)null,
                username = (string?)null
            });
        }
        catch (Exception ex)
        {
            Logging.Error($"SessionManager: Logout exception: {ex.Message}");
            // Still clear local state even on error
            _isLoggedIn = false;
            _server = null;
            _username = null;
            _password = null;
        }
    }

    /// <summary>
    /// Gibt aktuellen Session-Status zurück
    /// </summary>
    public Task<SessionStatus> GetSessionStatusAsync()
    {
        return Task.FromResult(new SessionStatus
        {
            IsAuthenticated = _isLoggedIn,
            Server = _server,
            Username = _username,
            Error = null
        });
    }

    /// <summary>
    /// Versucht Reconnect nach Verbindungsverlust
    /// </summary>
    public async Task<bool> TryReconnectAsync()
    {
        if (!_isLoggedIn || string.IsNullOrEmpty(_server) || string.IsNullOrEmpty(_username))
        {
            return false;
        }

        Logging.Info("SessionManager: Attempting reconnect...");

        // First logout to clean up old session
        await LogoutAsync();

        // Small delay before reconnect
        await Task.Delay(1000);

        var result = await LoginAsync(_server!, null, _username!, _password!, _authMode, _ctiMaster, _useRemoteConnector);
        return result.Success;
    }

    private static string GetLoginErrorMessage(int errorCode)
    {
        // Common CLMgr login error codes
        return errorCode switch
        {
            1 => "Ungültige Anmeldeinformationen",
            2 => "Server nicht erreichbar",
            3 => "Benutzer bereits angemeldet",
            4 => "Lizenz nicht verfügbar",
            5 => "Authentifizierungsfehler",
            _ => $"Fehlercode {errorCode}"
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Ensure logout on dispose
        if (_isLoggedIn)
        {
            try
            {
                var clmgr = _connector.GetCom();
                clmgr?.ReleaseUserEx();
            }
            catch { /* Ignore errors during dispose */ }
        }
    }
}

public record LoginResult(bool Success, string? ErrorMessage);

public record SessionStatus
{
    public bool IsAuthenticated { get; init; }
    public string? Server { get; init; }
    public string? Username { get; init; }
    public string? Error { get; init; }
}
