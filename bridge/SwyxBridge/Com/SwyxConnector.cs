using System.Runtime.InteropServices;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Erstellt und verwaltet die COM-Verbindung zum Swyx Client Line Manager.
/// MUSS auf dem STA-Thread instanziiert und verwendet werden.
/// </summary>
public sealed class SwyxConnector : IDisposable
{
    private const string ProgId = "CLMgr.ClientLineMgr";
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
    /// Verbindet sich mit dem CLMgr COM-Objekt.
    /// Wirft bei Fehler eine Exception mit klarer Fehlermeldung.
    /// </summary>
    public void Connect()
    {
        if (_clmgr != null)
        {
            Logging.Warn("SwyxConnector: Bereits verbunden.");
            return;
        }

        Logging.Info("SwyxConnector: Verbinde mit CLMgr...");

        var comType = Type.GetTypeFromProgID(ProgId);
        if (comType == null)
        {
            throw new InvalidOperationException(
                $"ProgID '{ProgId}' nicht gefunden. Ist SwyxIt! installiert?");
        }

        try
        {
            _clmgr = Activator.CreateInstance(comType);
            Logging.Info("SwyxConnector: COM-Objekt erfolgreich erstellt.");
        }
        catch (COMException ex) when (ex.HResult == E_ACCESSDENIED)
        {
            throw new UnauthorizedAccessException(
                "Zugriff verweigert (E_ACCESSDENIED). SwyxIt! läuft möglicherweise unter einem anderen Benutzer oder mit erhöhten Rechten.", ex);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM-Fehler beim Erstellen von CLMgr: 0x{ex.HResult:X8} - {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gibt das COM-Objekt zurück für direkte Aufrufe.
    /// </summary>
    public dynamic? GetCom() => _clmgr;

    /// <summary>
    /// Trennt die COM-Verbindung sauber.
    /// </summary>
    public void Disconnect()
    {
        if (_clmgr == null) return;

        try
        {
            Marshal.FinalReleaseComObject(_clmgr);
            Logging.Info("SwyxConnector: COM-Objekt freigegeben.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxConnector: Fehler beim Freigeben: {ex.Message}");
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
