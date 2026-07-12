using System.Diagnostics;
using System.Runtime.InteropServices;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

public sealed class SwyxConnector : IDisposable
{
    private const string ProgId = "CLMgr.ClientLineMgr";
    private const string ClMgrExePath = @"C:\Program Files (x86)\Swyx\SwyxIt!\CLMgr.exe";
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);
    private const int MaxWaitForClMgrSec = 10;

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    private const int SW_HIDE = 0;
    private const int SW_MINIMIZE = 6;

    private static SwyxConnector? _instance;

    private dynamic? _clmgr;
    private Process? _clMgrProcess;
    private System.Timers.Timer? _swyxItKiller;
    private bool _disposed;
    private bool _startedClMgr;

    public bool IsConnected => _clmgr != null;

    public SwyxConnector()
    {
        _instance = this;
    }

    public void Connect()
    {
        if (_clmgr != null)
        {
            Logging.Warn("SwyxConnector: Bereits verbunden.");
            return;
        }

        EnsureClMgrRunning();

        Logging.Info("SwyxConnector: Verbinde mit CLMgr.ClientLineMgr...");

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
                "Zugriff verweigert (E_ACCESSDENIED). CLMgr läuft möglicherweise unter einem anderen Benutzer.", ex);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"COM-Fehler beim Erstellen von CLMgr: 0x{ex.HResult:X8} - {ex.Message}", ex);
        }
    }

    private void EnsureClMgrRunning()
    {
        var existing = Process.GetProcessesByName("CLMgr").FirstOrDefault();
        if (existing != null)
        {
            Logging.Info($"SwyxConnector: CLMgr bereits aktiv (PID={existing.Id}).");
            _startedClMgr = false;
            StartSwyxItKiller();
            return;
        }

        if (!File.Exists(ClMgrExePath))
        {
            throw new FileNotFoundException($"CLMgr.exe nicht gefunden: {ClMgrExePath}");
        }

        Logging.Info("SwyxConnector: Starte CLMgr.exe...");

        _clMgrProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ClMgrExePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        if (!_clMgrProcess.Start())
        {
            throw new InvalidOperationException("CLMgr.exe konnte nicht gestartet werden.");
        }

        _startedClMgr = true;
        Logging.Info($"SwyxConnector: CLMgr.exe gestartet (PID={_clMgrProcess.Id})");

        WaitForClMgrReady();
        StartSwyxItKiller();
    }

    private void StartSwyxItKiller()
    {
        KillAndHideSwyxIt();

        _swyxItKiller = new System.Timers.Timer(500);
        _swyxItKiller.Elapsed += (s, e) => KillAndHideSwyxIt();
        _swyxItKiller.Start();
        Logging.Info("SwyxConnector: SwyxIt!-Überwachung aktiviert.");
    }

    private void KillAndHideSwyxIt()
    {
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                if (proc.ProcessName.StartsWith("SwyxIt", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(proc.MainWindowHandle, SW_HIDE);
                        }
                        proc.Kill();
                        Logging.Info($"SwyxConnector: SwyxIt! beendet (PID={proc.Id}, Name={proc.ProcessName})");
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private void WaitForClMgrReady()
    {
        int waited = 0;
        while (waited < MaxWaitForClMgrSec)
        {
            KillAndHideSwyxIt();

            try
            {
                var comType = Type.GetTypeFromProgID(ProgId);
                if (comType != null)
                {
                    var testObj = Activator.CreateInstance(comType);
                    if (testObj != null)
                    {
                        Marshal.FinalReleaseComObject(testObj);
                        Logging.Info($"SwyxConnector: CLMgr COM bereit nach {waited}s.");
                        return;
                    }
                }
            }
            catch { }

            Thread.Sleep(1000);
            waited++;
            Logging.Info($"SwyxConnector: Warte auf CLMgr COM... ({waited}s)");
        }

        throw new TimeoutException($"CLMgr COM nicht bereit nach {MaxWaitForClMgrSec} Sekunden.");
    }

    public dynamic? GetCom() => _clmgr;

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

        _swyxItKiller?.Stop();
        _swyxItKiller?.Dispose();

        KillAndHideSwyxIt();

        Disconnect();

        if (_startedClMgr && _clMgrProcess != null && !_clMgrProcess.HasExited)
        {
            try
            {
                _clMgrProcess.Kill();
                Logging.Info("SwyxConnector: CLMgr.exe beendet.");
            }
            catch (Exception ex)
            {
                Logging.Warn($"SwyxConnector: Konnte CLMgr nicht beenden: {ex.Message}");
            }
        }

        _clMgrProcess?.Dispose();
    }
}