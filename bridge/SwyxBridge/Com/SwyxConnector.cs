using System.Runtime.InteropServices;
using IpPbx.CLMgrLib;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Erstellt und verwaltet die COM-Verbindung zum Swyx Client Line Manager.
/// Verwendet das typisierte Swyx.Client.ClmgrAPI NuGet-Paket (v14.21.0).
/// 
/// Verbindungsmodi:
///   ATTACH (Standard): SwyxIt! läuft → COM-Objekt nutzt dessen Session. Kein DispInit nötig.
///   STANDALONE: Ohne SwyxIt! → DispInit(serverName) für eigene Session.
///
/// WICHTIG: __ComObject aus ProgID kann nur IDispatch-Methoden (Disp*) direkt via dynamic aufrufen.
/// Methoden auf Pub/Ex-Interfaces (PubInit, UnInit, PubGetServerFromAutoDetection) 
/// erfordern typed interface cast (QueryInterface).
/// 
/// MUSS auf dem STA-Thread instanziiert und verwendet werden.
/// </summary>
public sealed class SwyxConnector : IDisposable
{
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);
    private const int E_NOTIMPL = unchecked((int)0x80004001);

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
    /// Verbindet sich mit dem CLMgr COM-Objekt.
    /// 
    /// Wenn serverName angegeben: Versucht Standalone-Modus via DispInit.
    /// Wenn kein serverName: Attach-Modus — nutzt laufende SwyxIt!-Session.
    /// </summary>
    public void Connect(string? serverName = null)
    {
        if (_clmgr != null) { Logging.Warn("Already connected"); return; }

        Logging.Info("SwyxConnector: Creating COM object via ProgID...");

        try
        {
            // ProgID activation → returns __ComObject.
            // __ComObject supports QueryInterface for COM interfaces (IClientLineMgrDisp, etc.)
            // but NOT direct cast to coclass (ClientLineMgrClass).
            var type = Type.GetTypeFromProgID("CLMgr.ClientLineMgr", throwOnError: true);
            var comObj = Activator.CreateInstance(type!);
            _clmgr = comObj ?? throw new InvalidOperationException("COM object creation returned null");
            Logging.Info("SwyxConnector: COM object created via ProgID.");
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

        // Step 1: Check if already logged in (Attach-Modus via SwyxIt!)
        bool alreadyLoggedIn = false;
        try
        {
            alreadyLoggedIn = (int)_clmgr.DispIsLoggedIn != 0;
        }
        catch { }

        if (alreadyLoggedIn)
        {
            Logging.Info("SwyxConnector: Already logged in via SwyxIt! (Attach-Modus).");
            LogConnectionState();
            return;
        }

        // Step 2: Try auto-detection via typed interface (IClientLineMgrPub2)
        if (string.IsNullOrEmpty(serverName))
        {
            try
            {
                var pub2 = (IClientLineMgrPub2)(object)_clmgr;
                pub2.PubGetServerFromAutoDetection(
                    out string detectedServer, out string detectedBackup,
                    out int autoEnabled, out int serverAvailable);
                if (serverAvailable != 0 && !string.IsNullOrEmpty(detectedServer))
                {
                    serverName = detectedServer;
                    Logging.Info($"SwyxConnector: Auto-detected server: {serverName}");
                }
            }
            catch (InvalidCastException)
            {
                Logging.Warn("SwyxConnector: IClientLineMgrPub2 interface not available (auto-detection skipped).");
            }
            catch (Exception ex)
            {
                Logging.Warn($"SwyxConnector: Auto-detection failed: {ex.Message}");
            }
        }

        // Step 3: DispInit with server (Standalone-Modus) — via IDispatch
        if (!string.IsNullOrEmpty(serverName))
        {
            Logging.Info($"SwyxConnector: DispInit({serverName})...");
            try
            {
                int result = _clmgr.DispInit(serverName);
                if (result == 0)
                    Logging.Info("SwyxConnector: DispInit erfolgreich.");
                else if (result == E_NOTIMPL)
                    Logging.Warn("SwyxConnector: DispInit returned E_NOTIMPL — Standalone-Modus nicht unterstützt. SwyxIt! muss laufen.");
                else
                    Logging.Warn($"SwyxConnector: DispInit returned 0x{result:X8} ({result})");
            }
            catch (Exception ex)
            {
                Logging.Warn($"SwyxConnector: DispInit failed: {ex.Message}");
            }
        }
        else
        {
            Logging.Info("SwyxConnector: No server specified. Attach-Modus erwartet laufende SwyxIt!-Instanz.");
        }

        // Step 4: Check connection state
        LogConnectionState();
    }

    private void LogConnectionState()
    {
        try
        {
            bool isLoggedIn = (int)_clmgr!.DispIsLoggedIn != 0;
            bool isServerUp = (int)_clmgr.DispIsServerUp != 0;
            string currentServer = (string)(_clmgr.DispGetCurrentServer ?? "");
            string currentUser = (string)(_clmgr.DispGetCurrentUser ?? "");
            Logging.Info($"SwyxConnector: LoggedIn={isLoggedIn}, ServerUp={isServerUp}, Server={currentServer}, User={currentUser}");

            if (!isLoggedIn)
                Logging.Warn("SwyxConnector: NICHT eingeloggt! Stellen Sie sicher, dass SwyxIt! läuft und angemeldet ist.");
        }
        catch (Exception ex) { Logging.Warn($"SwyxConnector: Status check failed: {ex.Message}"); }
    }

    /// <summary>
    /// Gibt das COM-Objekt zurück für direkte Aufrufe.
    /// Kann via typed cast auf Interfaces wie IClientLineMgrDisp zugreifen.
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
    /// Trennt die COM-Verbindung sauber.
    /// UnInit ist auf IClientLineMgrEx6 Interface (typed cast nötig).
    /// </summary>
    public void Disconnect()
    {
        if (_clmgr == null) return;

        // UnInit via typed interface (IClientLineMgrEx6)
        try
        {
            var ex6 = (IClientLineMgrEx6)(object)_clmgr;
            ex6.UnInit();
            Logging.Info("SwyxConnector: UnInit called via IClientLineMgrEx6.");
        }
        catch (InvalidCastException)
        {
            Logging.Info("SwyxConnector: IClientLineMgrEx6 not available (UnInit skipped — Attach-Modus).");
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxConnector: UnInit failed: {ex.Message}");
        }

        // Release COM object
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
