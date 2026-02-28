using System.Runtime.InteropServices;
using IpPbx.CLMgrLib;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Erstellt und verwaltet die COM-Verbindung zum Swyx Client Line Manager.
/// Verwendet das typisierte Swyx.Client.ClmgrAPI NuGet-Paket (v14.21.0).
/// MUSS auf dem STA-Thread instanziiert und verwendet werden.
/// </summary>
public sealed class SwyxConnector : IDisposable
{
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);

    // CRITICAL: Static reference prevents GC collection while COM holds reference
    private static SwyxConnector? _instance;

    private dynamic? _clmgr;
    private bool _disposed;

    public bool IsConnected => _clmgr != null;

    public SwyxConnector()
    {
        _instance = this;
    }

    /// <summary>
    /// Verbindet sich mit dem CLMgr COM-Objekt via typisiertem SDK (Standalone-Modus).
    /// Versucht zuerst Auto-Detektion, dann DispInit mit ServerName.
    /// </summary>
    public void Connect(string? serverName = null)
    {
        if (_clmgr != null) { Logging.Warn("Already connected"); return; }

        Logging.Info("SwyxConnector: Creating COM object via ProgID...");

        try
        {
            // ProgID activation → returns __ComObject.
            // __ComObject can only be cast to COM interfaces, not to coclass types
            // like ClientLineMgrClass. We store as dynamic for late-bound dispatch.
            var type = Type.GetTypeFromProgID("CLMgr.ClientLineMgr", throwOnError: true);
            var comObj = Activator.CreateInstance(type!);
            _clmgr = comObj ?? throw new InvalidOperationException("COM object creation returned null");
            Logging.Info("SwyxConnector: COM object created via ProgID (dynamic dispatch).");
        }
        catch (COMException ex) when (ex.HResult == E_ACCESSDENIED)
        {
            throw new UnauthorizedAccessException(
                "Zugriff verweigert. Ist der Swyx Client korrekt installiert?", ex);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM-Fehler: 0x{ex.HResult:X8} - {ex.Message}", ex);
        }

        // Step 1: Try auto-detection if no server specified
        if (string.IsNullOrEmpty(serverName))
        {
            try
            {
                string detectedServer, detectedBackup;
                int autoEnabled, serverAvailable;
                _clmgr.PubGetServerFromAutoDetection(
                    out detectedServer, out detectedBackup,
                    out autoEnabled, out serverAvailable);
                if (serverAvailable != 0 && !string.IsNullOrEmpty(detectedServer))
                {
                    serverName = detectedServer;
                    Logging.Info($"SwyxConnector: Auto-detected server: {serverName}");
                }
            }
            catch (Exception ex) { Logging.Warn($"Auto-detection failed: {ex.Message}"); }
        }

        // Step 2: DispInit with server (standalone mode)
        if (!string.IsNullOrEmpty(serverName))
        {
            Logging.Info($"SwyxConnector: DispInit({serverName})...");
            try
            {
                int result = _clmgr.DispInit(serverName);
                Logging.Info($"SwyxConnector: DispInit returned {result}");
            }
            catch (Exception ex)
            {
                Logging.Warn($"SwyxConnector: DispInit failed: {ex.Message}");
            }
        }
        else
        {
            Logging.Info("SwyxConnector: No server specified, using attached SwyxIt! session.");
        }

        // Step 3: Check connection state
        try
        {
            bool isLoggedIn = (int)_clmgr.DispIsLoggedIn != 0;
            bool isServerUp = (int)_clmgr.DispIsServerUp != 0;
            string currentServer = (string)(_clmgr.DispGetCurrentServer ?? "");
            string currentUser = (string)(_clmgr.DispGetCurrentUser ?? "");
            Logging.Info($"SwyxConnector: LoggedIn={isLoggedIn}, ServerUp={isServerUp}, Server={currentServer}, User={currentUser}");
        }
        catch (Exception ex) { Logging.Warn($"SwyxConnector: Status check failed: {ex.Message}"); }
    }

    /// <summary>
    /// Gibt das typisierte COM-Objekt zurück für direkte Aufrufe.
    /// </summary>
    public dynamic? GetCom() => _clmgr;

    /// <summary>
    /// Gibt Verbindungsinformationen zurück.
    /// </summary>
    public object GetConnectionInfo()
    {
        if (_clmgr == null) return new { connected = false, server = "", user = "", loggedIn = false, serverUp = false };

        try
        {
            return new
            {
                connected = true,
                server = (string)(_clmgr.DispGetCurrentServer ?? ""),
                user = (string)(_clmgr.DispGetCurrentUser ?? ""),
                loggedIn = (int)_clmgr.DispIsLoggedIn != 0,
                serverUp = (int)_clmgr.DispIsServerUp != 0
            };
        }
        catch (Exception ex)
        {
            Logging.Warn($"GetConnectionInfo failed: {ex.Message}");
            return new { connected = IsConnected, server = "", user = "", loggedIn = false, serverUp = false };
        }
    }

    /// <summary>
    /// Trennt die COM-Verbindung sauber (UnInit + FinalReleaseComObject).
    /// </summary>
    public void Disconnect()
    {
        if (_clmgr == null) return;
        try
        {
            _clmgr.UnInit();
            Logging.Info("SwyxConnector: UnInit called.");
        }
        catch (Exception ex) { Logging.Warn($"UnInit failed: {ex.Message}"); }
        try
        {
            Marshal.FinalReleaseComObject((object)_clmgr);
            Logging.Info("SwyxConnector: COM released.");
        }
        catch (Exception ex) { Logging.Warn($"COM release failed: {ex.Message}"); }
        finally { _clmgr = null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
