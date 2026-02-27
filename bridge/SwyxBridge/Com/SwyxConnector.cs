using System.Diagnostics;
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
    private const string SwyxItExeName = "SwyxIt!";
    private const string SwyxItExePath = @"C:\Program Files (x86)\Swyx\SwyxIt!\SwyxIt!.exe";
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);
    private const int MaxWaitForSwyxItSec = 30;

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

        // Schritt 1: SwyxIt!.exe sicherstellen
        EnsureSwyxItRunning();

        // Schritt 2: COM-Verbindung herstellen
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
                "Zugriff verweigert (E_ACCESSDENIED). SwyxIt! l\u00e4uft m\u00f6glicherweise unter einem anderen Benutzer oder mit erh\u00f6hten Rechten.", ex);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM-Fehler beim Erstellen von CLMgr: 0x{ex.HResult:X8} - {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Pr\u00fcft ob SwyxIt!.exe l\u00e4uft. Wenn nicht, wird es gestartet und minimiert.
    /// Wartet bis CLMgr-Prozess verf\u00fcgbar ist (max 30s).
    /// </summary>
    private static void EnsureSwyxItRunning()
    {
        // Pr\u00fcfe ob SwyxIt! bereits l\u00e4uft
        var existing = Process.GetProcessesByName(SwyxItExeName);
        if (existing.Length > 0)
        {
            Logging.Info($"SwyxConnector: SwyxIt! l\u00e4uft bereits (PID={existing[0].Id}).");
            return;
        }

        // SwyxIt! ist nicht gestartet \u2014 starten
        if (!File.Exists(SwyxItExePath))
        {
            Logging.Warn($"SwyxConnector: SwyxIt!.exe nicht gefunden: {SwyxItExePath}");
            Logging.Warn("SwyxConnector: Versuche trotzdem COM-Verbindung...");
            return;
        }

        Logging.Info("SwyxConnector: Starte SwyxIt!.exe...");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = SwyxItExePath,
                WindowStyle = ProcessWindowStyle.Minimized,
                UseShellExecute = true
            };
            Process.Start(psi);
            Logging.Info("SwyxConnector: SwyxIt!.exe gestartet, warte auf Bereitschaft...");

            // Warten bis CLMgr-Prozess l\u00e4uft (SwyxIt! startet CLMgr intern)
            var sw = Stopwatch.StartNew();
            bool ready = false;
            while (sw.Elapsed.TotalSeconds < MaxWaitForSwyxItSec)
            {
                Thread.Sleep(1000);
                var clmgr = Process.GetProcessesByName("CLMgr");
                if (clmgr.Length > 0)
                {
                    // CLMgr l\u00e4uft \u2014 noch 3s warten f\u00fcr COM-Registrierung
                    Logging.Info($"SwyxConnector: CLMgr erkannt (PID={clmgr[0].Id}), warte 3s auf COM...");
                    Thread.Sleep(3000);
                    ready = true;
                    break;
                }
                Logging.Info($"SwyxConnector: Warte auf CLMgr... ({(int)sw.Elapsed.TotalSeconds}s)");
            }

            if (!ready)
                Logging.Warn($"SwyxConnector: CLMgr nach {MaxWaitForSwyxItSec}s nicht gefunden. Versuche trotzdem...");

            // SwyxIt!-Fenster minimieren/verstecken
            MinimizeSwyxItWindows();
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxConnector: SwyxIt! starten fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Minimiert alle SwyxIt!-Fenster nach dem Start.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    private const int SW_SHOWMINNOACTIVE = 7;

    private static void MinimizeSwyxItWindows()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName(SwyxItExeName))
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(proc.MainWindowHandle, SW_SHOWMINNOACTIVE);
                    Logging.Info($"SwyxConnector: SwyxIt!-Fenster minimiert (PID={proc.Id}).");
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxConnector: Fenster minimieren fehlgeschlagen: {ex.Message}");
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
