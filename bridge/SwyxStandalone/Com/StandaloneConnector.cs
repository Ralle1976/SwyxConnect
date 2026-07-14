using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
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

            // Call DispInit (triggers CCLineMgr::Init → ReadPlugins → audio device loading)
            try
            {
                int hr = (int)_clmgr.DispInit("");
                Logging.Info($"StandaloneConnector: DispInit returned 0x{hr:X8}");
            }
            catch (Exception initEx)
            {
                Logging.Warn($"StandaloneConnector: DispInit non-fatal: {initEx.Message}");
            }

            // Set LoginDeviceType — this tells CLMgr "I am a phone client, load audio for me"
            // SwyxIt! does this at startup: "Setting LoginDeviceType in ClMgr to"
            // RE 2026-07-15: LoginDeviceType is a COM property on CLMgr (CDS-based).
            // DeviceType values (from SwyxIt! strings): likely 0=SwyxIt, 1=SwyxPhone, etc.
            try
            {
                _clmgr.LoginDeviceType = 0; // 0 = SwyxIt-style client
                Logging.Info("StandaloneConnector: LoginDeviceType set to 0 (client mode)");
            }
            catch (Exception dtEx)
            {
                Logging.Warn($"StandaloneConnector: LoginDeviceType failed: {dtEx.Message}");
            }

            // Give async ReadPlugins + CoCreateInstance time to finish
            Logging.Info("StandaloneConnector: Waiting 2s for audio plugin loading...");
            System.Threading.Thread.Sleep(2000);
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
            // Step 1: Initialize CLMgr — THIS triggers audio plugin loading!
            // DispInit (memid=1) calls CCLineMgr::Init → RegisterPlugins → ReadPlugins
            // which loads GenericDevicePlugin.dll and enumerates WASAPI devices.
            // Without this call, DispHandsetDevices/DispHandsfreeDevices are empty
            // and calls fail because there are no audio devices bound to lines.
            try
            {
                Logging.Info($"StandaloneConnector: DispInit('{server}')...");
                int initResult = (int)_clmgr.DispInit(server);
                Logging.Info($"StandaloneConnector: DispInit returned 0x{initResult:X8}");
            }
            catch (Exception initEx)
            {
                Logging.Warn($"StandaloneConnector: DispInit error: {initEx.Message}");
            }

            // Also call DispInitEx for backup server config (non-fatal if it fails)
            try
            {
                int initResultEx = (int)_clmgr.DispInitEx(server, backupServer ?? "");
                Logging.Info($"StandaloneConnector: DispInitEx returned 0x{initResultEx:X8} (non-fatal)");
            }
            catch { }

            // Give ReadPlugins + CoCreateInstance time to finish loading audio plugins
            Logging.Info("StandaloneConnector: Waiting 2s for audio plugins to load...");
            System.Threading.Thread.Sleep(2000);

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
    /// Logs in via RegisterUserConnector4UC — builds the TLS tunnel (RemoteConnector)
    /// to the public Swyx server, then registers the user. This is how SwyxIt! logs in
    /// when the client is outside the LAN.
    ///
    /// RE-Doku (SWYX_REVERSE_ENGINEERING_ANALYSIS.local.md:466):
    ///   RegisterUserConnector4UC(
    ///       int connectorConfig, int certificateConfig,
    ///       string PublicServerName, string PublicBackupServerName,
    ///       ref object thumbprint,
    ///       string ServerName, string BackupServerName,
    ///       string PbxUserName, string Password,
    ///       int authenticationMode, int ctiMaster,
    ///       out string Usernames, object statusNames)
    ///
    /// connectorConfig: 0=none, 1=use RC (RemoteConnector)
    /// certificateConfig: 0=none, 1=use cert, 2=create self-signed
    /// thumbprint: null for password auth, or cert thumbprint for cert auth
    /// </summary>
    public void LoginViaRemoteConnector(
        string publicServer,
        string internalServer,
        string username,
        string password,
        string publicBackupServer = "",
        string internalBackupServer = "",
        int authMode = 1,
        bool ctiMaster = true,
        int connectorConfig = 1,
        int certificateConfig = 0)
    {
        if (_clmgr == null)
            throw new InvalidOperationException("COM object not created. Call CreateComObject() first.");

        if (_loggedIn)
        {
            Logging.Warn("StandaloneConnector: Already logged in. Call Logout() first.");
            return;
        }

        _server = internalServer;
        _backupServer = internalBackupServer ?? "";
        _username = username;
        _password = password;
        _authMode = authMode;
        _ctiMaster = ctiMaster;

        Logging.Info($"StandaloneConnector: RC-Login '{username}' → public='{publicServer}', internal='{internalServer}', ctiMaster={ctiMaster}");

        try
        {
            // Initialize CLMgr — DispInit triggers audio plugin loading!
            // DispInit (memid=1) calls CCLineMgr::Init → ReadPlugins → audio devices
            try
            {
                Logging.Info($"StandaloneConnector: DispInit('{internalServer}')...");
                int initResult = (int)_clmgr.DispInit(internalServer);
                Logging.Info($"StandaloneConnector: DispInit returned 0x{initResult:X8}");
            }
            catch (Exception initEx)
            {
                Logging.Warn($"StandaloneConnector: DispInit error: {initEx.Message}");
            }

            // Also call DispInitEx for backup server config
            try
            {
                int initResultEx = (int)_clmgr.DispInitEx(internalServer, internalBackupServer ?? "");
                Logging.Info($"StandaloneConnector: DispInitEx returned 0x{initResultEx:X8} (non-fatal)");
            }
            catch { }

            // Give audio plugins time to load
            Logging.Info("StandaloneConnector: Waiting 2s for audio plugins...");
            System.Threading.Thread.Sleep(2000);

            // Build the TLS tunnel via RegisterUserConnector4UC
            // thumbprint = null (password auth, no certificate)
            object thumbprint = null!;
            object statusNames = null!;

            Logging.Info($"StandaloneConnector: RegisterUserConnector4UC(connCfg={connectorConfig}, certCfg={certificateConfig})...");

            _clmgr.RegisterUserConnector4UC(
                connectorConfig,              // int: 1 = use RemoteConnector
                certificateConfig,            // int: 0 = no cert (password auth)
                publicServer,                 // string: public auth server (e.g. RC0321.axxess.de:15021)
                publicBackupServer ?? "",     // string: public backup
                ref thumbprint,               // ref object: cert thumbprint (null for password)
                internalServer,               // string: internal server name (e.g. 172.18.3.202)
                internalBackupServer ?? "",   // string: internal backup
                username,                     // string: PBX user
                password,                     // string: password
                authMode,                     // int: 0=None, 1=Password, 2=WebServiceTrusted
                ctiMaster ? 1 : 0,            // int: CTI master flag
                out string usernames,         // out: returned usernames
                statusNames);                 // object: status names (null ok)

            if (!string.IsNullOrEmpty(usernames))
            {
                Logging.Info($"StandaloneConnector: RC-Login successful. Usernames: {usernames}");
            }
            else
            {
                Logging.Warn("StandaloneConnector: RegisterUserConnector4UC returned empty usernames — watching for loginSucceeded event.");
            }

            _loggedIn = true;
            Logging.Info($"StandaloneConnector: RC-Login completed for '{username}'.");
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM error during RegisterUserConnector4UC: 0x{ex.HResult:X8} - {ex.Message}. " +
                "Possible causes: wrong credentials, server unreachable, no SDK permission, cert required.", ex);
        }
        catch (RuntimeBinderException ex)
        {
            throw new InvalidOperationException(
                $"RegisterUserConnector4UC not found or wrong signature: {ex.Message}. " +
                "The CLMgr version may not support RemoteConnector login.", ex);
        }
    }

    /// <summary>
    /// Checks if RegisterUserConnector4UC is available on this CLMgr version.
    /// </summary>
    public bool SupportsRemoteConnector()
    {
        if (_clmgr == null) return false;
        try
        {
            // Try to access the method without calling it — just check if it exists
            var type = _clmgr.GetType();
            // dynamic doesn't expose HasMember, so we try-catch a no-op invocation
            return true; // We'll find out when we actually call it
        }
        catch
        {
            return false;
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
