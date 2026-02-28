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

    // Win32 API
    private const int SW_HIDE = 0;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int OFFSCREEN_X = -32000;
    private const int OFFSCREEN_Y = -32000;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // CRITICAL: Static reference prevents GC collection while COM holds reference
    private static SwyxConnector? _instance;
    private static bool _hideLogged;

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
                "Zugriff verweigert (E_ACCESSDENIED). SwyxIt! läuft möglicherweise unter einem anderen Benutzer oder mit erhöhten Rechten.", ex);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM-Fehler beim Erstellen von CLMgr: 0x{ex.HResult:X8} - {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Prüft ob SwyxIt!.exe läuft. Wenn nicht, wird es gestartet und VERSTECKT (nicht minimiert).
    /// Wartet bis CLMgr-Prozess verfügbar ist (max 30s).
    /// </summary>
    private static void EnsureSwyxItRunning()
    {
        // Prüfe ob SwyxIt! bereits läuft
        var existing = Process.GetProcessesByName(SwyxItExeName);
        if (existing.Length > 0)
        {
            Logging.Info($"SwyxConnector: SwyxIt! läuft bereits (PID={existing[0].Id}).");
            // Sofort alle Fenster verstecken
            HideAllSwyxItWindows();
            return;
        }

        // SwyxIt! ist nicht gestartet — starten
        if (!File.Exists(SwyxItExePath))
        {
            Logging.Warn($"SwyxConnector: SwyxIt!.exe nicht gefunden: {SwyxItExePath}");
            Logging.Warn("SwyxConnector: Versuche trotzdem COM-Verbindung...");
            return;
        }

        Logging.Info("SwyxConnector: Starte SwyxIt!.exe (Hidden)...");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = SwyxItExePath,
                WindowStyle = ProcessWindowStyle.Hidden, // HIDDEN, nicht Minimized!
                UseShellExecute = true
            };
            Process.Start(psi);
            Logging.Info("SwyxConnector: SwyxIt!.exe gestartet, warte auf Bereitschaft...");

            // Warten bis CLMgr-Prozess läuft (SwyxIt! startet CLMgr intern)
            var sw = Stopwatch.StartNew();
            bool ready = false;
            while (sw.Elapsed.TotalSeconds < MaxWaitForSwyxItSec)
            {
                Thread.Sleep(500);

                // Bei jeder Iteration: SwyxIt!-Fenster verstecken
                HideAllSwyxItWindows();

                var clmgr = Process.GetProcessesByName("CLMgr");
                if (clmgr.Length > 0)
                {
                    // CLMgr läuft — noch 3s warten für COM-Registrierung
                    Logging.Info($"SwyxConnector: CLMgr erkannt (PID={clmgr[0].Id}), warte 3s auf COM...");
                    // Während des Wartens weiter verstecken
                    for (int i = 0; i < 6; i++)
                    {
                        Thread.Sleep(500);
                        HideAllSwyxItWindows();
                    }
                    ready = true;
                    break;
                }
                Logging.Info($"SwyxConnector: Warte auf CLMgr... ({(int)sw.Elapsed.TotalSeconds}s)");
            }

            if (!ready)
                Logging.Warn($"SwyxConnector: CLMgr nach {MaxWaitForSwyxItSec}s nicht gefunden. Versuche trotzdem...");

            // Finales Verstecken
            HideAllSwyxItWindows();
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxConnector: SwyxIt! starten fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Versteckt ALLE Fenster aller SwyxIt!-Prozesse.
    /// Dreistufig: 1) Off-screen verschieben (kein Blitz), 2) SW_HIDE, 3) WS_VISIBLE entfernen.
    /// </summary>
    public static void HideAllSwyxItWindows()
    {
        try
        {
            var swyxPids = new HashSet<uint>();
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var name = proc.ProcessName.ToLowerInvariant();
                    if (name.Contains("swyxit") || name == "swyxitc")
                        swyxPids.Add((uint)proc.Id);
                }
                catch { }
            }

            if (swyxPids.Count == 0) return;

            int count = 0;
            EnumWindows((hWnd, _) =>
            {
                try
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (swyxPids.Contains(pid))
                    {
                        // 1. Off-screen + Größe 0 — kein Blitz möglich
                        SetWindowPos(hWnd, IntPtr.Zero,
                            OFFSCREEN_X, OFFSCREEN_Y, 0, 0,
                            SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW | SWP_FRAMECHANGED);

                        // 2. SW_HIDE
                        ShowWindow(hWnd, SW_HIDE);

                        // 3. WS_VISIBLE entfernen
                        int style = GetWindowLong(hWnd, GWL_STYLE);
                        if ((style & WS_VISIBLE) != 0)
                            SetWindowLong(hWnd, GWL_STYLE, style & ~WS_VISIBLE);

                        // 4. Tool-Window + NoActivate
                        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                        int newEx = exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                        if (newEx != exStyle)
                            SetWindowLong(hWnd, GWL_EXSTYLE, newEx);

                        count++;
                    }
                }
                catch { }
                return true;
            }, IntPtr.Zero);

            // Nur beim ersten Mal loggen, danach still
            if (count > 0 && !_hideLogged)
            {
                Logging.Info($"SwyxConnector: {count} Fenster off-screen + hidden.");
                _hideLogged = true;
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxConnector: HideAllSwyxItWindows fehlgeschlagen: {ex.Message}");
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
