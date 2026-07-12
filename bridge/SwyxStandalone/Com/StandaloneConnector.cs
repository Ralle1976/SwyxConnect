using System.Runtime.InteropServices;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Com;

/// <summary>
/// Erstellt die COM-Verbindung zum Swyx Client Line Manager im Standalone-Modus.
/// Verwendet RegisterUserEx() für direkten Server-Login OHNE laufendes SwyxIt!.
///
/// Gemäß SDK-Dokumentation:
///   "Logon and logoff should only be used via SDK when your application intends
///    to run stand alone (without SwyxIt! running aside)."
///
/// MUSS auf dem STA-Thread instanziiert und verwendet werden.
/// </summary>
public sealed class StandaloneConnector : IDisposable
{
    private const string ProgId = "CLMgr.ClientLineMgr";

    // CRITICAL: Static reference prevents GC collection while COM holds reference
    private static StandaloneConnector? _instance;

    private dynamic? _clmgr;
    private bool _disposed;
    private bool _loggedIn;

    // Last login parameters for reconnect
    private string _server = "";
    private string _backupServer = "";
    private string _username = "";
    private string _password = "";
    private int _authMode;
    private bool _ctiMaster;

    public bool IsConnected => _clmgr != null;
    public bool IsLoggedIn => _loggedIn;
    public string Username => _username;
    public string Server => _server;
    public string BackupServer => _backupServer;
    public int AuthMode => _authMode;
    public bool CtiMaster => _ctiMaster;

    public StandaloneConnector()
    {
        _instance = this;
    }

    /// <summary>
    /// Erstellt das COM-Objekt (ohne Login). Wirft bei Fehler.
    /// </summary>
    public void CreateComObject()
    {
        if (_clmgr != null)
        {
            Logging.Warn("StandaloneConnector: COM-Objekt existiert bereits.");
            return;
        }

        Logging.Info("StandaloneConnector: Erstelle CLMgr COM-Objekt...");

        var comType = Type.GetTypeFromProgID(ProgId);
        if (comType == null)
        {
            throw new InvalidOperationException(
                $"ProgID '{ProgId}' nicht gefunden. Ist das Swyx Client SDK installiert?");
        }

        try
        {
            _clmgr = Activator.CreateInstance(comType);
            Logging.Info("StandaloneConnector: COM-Objekt erfolgreich erstellt.");
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80070005))
        {
            throw new UnauthorizedAccessException(
                "Zugriff verweigert (E_ACCESSDENIED). Das Swyx SDK darf möglicherweise nur von bestimmten Benutzern verwendet werden.", ex);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM-Fehler beim Erstellen von CLMgr: 0x{ex.HResult:X8} - {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Logs into the Swyx Server via DispInitEx + RegisterUserEx().
    ///
    /// VERIFIED SIGNATURE (from Interop.CLMgr.dll reflection on 2026-07-11):
    ///   [Void] RegisterUserEx(
    ///       String ServerName, String BackupServerName, String PbxUserName,
    ///       String Password, Int32 iAuthenticationMode, Int32 bCtiMaster,
    ///       out String Usernames)
    ///
    /// The method returns VOID — success is determined by:
    ///   1. The out Usernames parameter (non-empty = success)
    ///   2. CLMgr Event msg 11 (loginSucceeded) / msg 12 (loginFailed)
    ///   3. COMException for hard failures
    ///
    /// authMode: 0=None/SwyxIt!Now, 1=Password (default), 2=WebServiceTrusted
    ///
    /// IMPORTANT: DispInitEx must be called first to initialize the CLMgr server connection.
    /// This is required when CLMgr is started fresh (no SwyxIt! running). When SwyxIt! is
    /// already running, CLMgr is pre-initialized and DispInitEx may return an error that
    /// can be safely ignored.
    /// </summary>
    public void Login(
        string server,
        string username,
        string password,
        string backupServer = "",
        int authMode = 1,
        bool ctiMaster = false)
    {
        if (_clmgr == null)
            throw new InvalidOperationException("COM object not created. Call CreateComObject() first.");

        if (_loggedIn)
        {
            Logging.Warn("StandaloneConnector: Already logged in. Call Logout() first.");
            return;
        }

        _server = server;
        _backupServer = backupServer ?? "";
        _username = username;
        _password = password;
        _authMode = authMode;
        _ctiMaster = ctiMaster;

        Logging.Info($"StandaloneConnector: Logging in as '{username}' on server '{server}' (authMode={authMode}, ctiMaster={ctiMaster})...");

        try
        {
            // Step 1: Initialize CLMgr server connection.
            // DispInitEx is required for fresh CLMgr instances (standalone mode without SwyxIt!).
            try
            {
                Logging.Info($"StandaloneConnector: DispInitEx('{server}', '{backupServer}')...");
                int initResult = (int)_clmgr.DispInitEx(server, backupServer ?? "");
                Logging.Info($"StandaloneConnector: DispInitEx returned {initResult}");
            }
            catch (Exception initEx)
            {
                // DispInit may fail if CLMgr is already initialized (SwyxIt! running).
                // Non-fatal — RegisterUserEx may still succeed.
                Logging.Warn($"StandaloneConnector: DispInitEx non-fatal error (may be already initialized): {initEx.Message}");
            }

            // Step 2: Register user with verified 7-arg void signature.
            _clmgr.RegisterUserEx(
                server,
                backupServer ?? "",
                username,
                password,
                authMode,
                ctiMaster ? 1 : 0,
                out string usernames);

            // void return — check the out parameter for confirmation.
            if (!string.IsNullOrEmpty(usernames))
            {
                Logging.Info($"StandaloneConnector: Login successful. Server returned usernames: {usernames}");
            }
            else
            {
                Logging.Warn("StandaloneConnector: RegisterUserEx returned empty usernames — login may be pending. Watch for loginSucceeded/loginFailed events.");
            }

            _loggedIn = true;
            Logging.Info($"StandaloneConnector: Logged in as '{username}'.");
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM error during RegisterUserEx: 0x{ex.HResult:X8} - {ex.Message}. " +
                "Possible causes: wrong password, server unreachable, no SDK permission.", ex);
        }
    }

    /// <summary>
    /// Loggt sich vom Swyx-Server aus via ReleaseUserEx().
    /// Muss vor Dispose() aufgerufen werden wenn eingeloggt.
    /// </summary>
    public void Logout()
    {
        if (_clmgr == null || !_loggedIn) return;

        try
        {
            Logging.Info("StandaloneConnector: Logge aus...");
            _clmgr!.ReleaseUserEx();
            _loggedIn = false;
            Logging.Info("StandaloneConnector: Ausgeloggt.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"StandaloneConnector: Fehler beim Logout: {ex.Message}");
            _loggedIn = false;
        }
    }

    /// <summary>
    /// Gibt das rohe COM-Objekt zurück für direkte Dispatch-Aufrufe.
    /// </summary>
    public dynamic? GetCom() => _clmgr;

    /// <summary>
    /// Trennt die COM-Verbindung sauber (Logout + Release).
    /// </summary>
    public void Disconnect()
    {
        if (_clmgr == null) return;

        try
        {
            if (_loggedIn)
                Logout();

            Marshal.FinalReleaseComObject(_clmgr);
            Logging.Info("StandaloneConnector: COM-Objekt freigegeben.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"StandaloneConnector: Fehler beim Disconnect: {ex.Message}");
        }
        finally
        {
            _clmgr = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
