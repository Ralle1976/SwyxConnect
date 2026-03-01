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

    // Letzte Login-Parameter für Reconnect
    private string _server = "";
    private string _username = "";
    private string _password = "";
    private string _domain = "";
    private int _authMode;

    public bool IsConnected => _clmgr != null;
    public bool IsLoggedIn => _loggedIn;
    public string Username => _username;
    public string Server => _server;

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
    /// Loggt sich am Swyx-Server ein via RegisterUserEx().
    ///
    /// authMode:
    ///   0 = None / SwyxIt!Now (anonyme Verbindung, einige Server erlauben dies nicht)
    ///   1 = Password (Standard: Benutzername + Passwort)
    ///   2 = WebServiceTrusted (Single Sign-On, nur in bestimmten Umgebungen)
    ///
    /// flags: Bit-Feld, normalerweise 0.
    ///   Bit 0 = 1: Kein automatisches Reconnect
    ///   Bit 1 = 2: Silent-Modus (keine UI-Dialoge)
    ///
    /// clientInfo: Freitext-String, wird im Server-Log gespeichert.
    /// </summary>
    public void Login(
        string server,
        string username,
        string password,
        string domain = "",
        int authMode = 1,
        int flags = 0,
        string clientInfo = "SwyxStandalone/1.0")
    {
        if (_clmgr == null)
            throw new InvalidOperationException("COM-Objekt nicht erstellt. Zuerst CreateComObject() aufrufen.");

        if (_loggedIn)
        {
            Logging.Warn("StandaloneConnector: Bereits eingeloggt. Bitte erst Logout().");
            return;
        }

        _server = server;
        _username = username;
        _password = password;
        _domain = domain;
        _authMode = authMode;

        Logging.Info($"StandaloneConnector: Logge ein als '{username}' auf Server '{server}' (authMode={authMode})...");

        try
        {
            // Signature: RegisterUserEx(server, user, password, domain, authMode, flags, clientInfo)
            // Returns int: 0=Success, non-zero=Fehler
            int result = (int)_clmgr.RegisterUserEx(
                server,
                username,
                password,
                domain,
                authMode,
                flags,
                clientInfo);

            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"RegisterUserEx fehlgeschlagen: Fehlercode {result}. " +
                    $"Mögliche Ursachen: Falsches Passwort, Server nicht erreichbar, kein SDK-Recht.");
            }

            _loggedIn = true;
            Logging.Info($"StandaloneConnector: Erfolgreich eingeloggt als '{username}'.");
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM-Fehler bei RegisterUserEx: 0x{ex.HResult:X8} - {ex.Message}", ex);
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
