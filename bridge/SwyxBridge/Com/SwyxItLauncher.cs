using System.Diagnostics;
using System.Net.Sockets;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Startet SwyxIt!.exe temporär zum Tunnel-Aufbau und killt es danach.
///
/// ERKENNTNIS: CLMgr.exe hält den RemoteConnector-Tunnel eigenständig.
/// SwyxIt!.exe wird nur benötigt, um den Tunnel initial aufzubauen.
/// Danach kann SwyxIt! beendet werden — CLMgr läuft weiter, Port 9094 bleibt offen.
///
/// Ablauf:
///   1. Prüfe ob Tunnel bereits steht (Port 9094) → wenn ja, fertig
///   2. Prüfe ob SwyxIt!.exe bereits läuft → wenn ja, nur auf Tunnel warten
///   3. Starte SwyxIt!.exe mit WindowStyle=Hidden
///   4. Warte auf Port 9094 (Tunnel aktiv) mit Timeout
///   5. Kill SwyxIt!.exe — CLMgr hält den Tunnel
///   6. Melde Erfolg/Misserfolg zurück
/// </summary>
public static class SwyxItLauncher
{
    // Standard-Installationspfade (in Prioritätsreihenfolge)
    private static readonly string[] SearchPaths =
    {
        @"C:\Program Files (x86)\Swyx\SwyxIt!\SwyxIt!.exe",
        @"C:\Program Files\Swyx\SwyxIt!\SwyxIt!.exe",
        @"C:\Program Files (x86)\SwyxIt!\SwyxIt!.exe",
        @"C:\Program Files\SwyxIt!\SwyxIt!.exe",
    };

    /// <summary>
    /// Ergebnis eines Launch-Versuchs.
    /// </summary>
    public class LaunchResult
    {
        public bool Success { get; set; }
        public bool AlreadyRunning { get; set; }
        public bool TunnelAvailable { get; set; }
        public int? ProcessId { get; set; }
        public string? SwyxItPath { get; set; }
        public string? Error { get; set; }
        public int WaitTimeMs { get; set; }
        public bool SwyxItKilled { get; set; }
    }

    /// <summary>
    /// Prüft ob SwyxIt!.exe bereits läuft.
    /// </summary>
    public static bool IsRunning()
    {
        try
        {
            return Process.GetProcessesByName("SwyxIt!").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prüft ob der CDS-Tunnel auf Port 9094 verfügbar ist.
    /// </summary>
    public static bool IsTunnelAvailable(int port = 9094)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect("127.0.0.1", port, null, null);
            bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            if (connected)
            {
                client.EndConnect(result);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Findet den Installationspfad von SwyxIt!.exe.
    /// </summary>
    public static string? FindSwyxItPath()
    {
        // Prüfe bekannte Installationspfade
        foreach (var path in SearchPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Fallback: Suche im Verzeichnis von CLMgr.exe
        try
        {
            var clmgr = Process.GetProcessesByName("CLMgr").FirstOrDefault();
            if (clmgr?.MainModule?.FileName != null)
            {
                var dir = Path.GetDirectoryName(clmgr.MainModule.FileName);
                if (dir != null)
                {
                    var swyxitPath = Path.Combine(dir, "SwyxIt!.exe");
                    if (File.Exists(swyxitPath))
                        return swyxitPath;
                }
            }
        }
        catch { /* Zugriff verweigert o.ä. */ }

        return null;
    }

    /// <summary>
    /// Startet SwyxIt!.exe als unsichtbaren Hintergrundprozess und wartet
    /// auf den RemoteConnector-Tunnel (Port 9094).
    /// 
    /// WindowHook.Start() MUSS vorher aufgerufen worden sein, damit die
    /// Fenster sofort unterdrückt werden.
    /// </summary>
    /// <param name="tunnelTimeoutMs">Maximale Wartezeit auf Tunnel in ms (Standard: 30s)</param>
    /// <returns>Launch-Ergebnis mit Status</returns>
    public static LaunchResult Launch(int tunnelTimeoutMs = 30000)
    {
        var result = new LaunchResult();

        // Schritt 0: Tunnel bereits offen? (CLMgr läuft noch von vorher)
        if (IsTunnelAvailable())
        {
            result.Success = true;
            result.TunnelAvailable = true;
            result.AlreadyRunning = IsRunning();
            Logging.Info("SwyxItLauncher: Tunnel bereits verfügbar (Port 9094 offen). SwyxIt! nicht nötig.");
            return result;
        }

        // Schritt 1: SwyxIt! bereits am Laufen? Nur auf Tunnel warten.
        if (IsRunning())
        {
            result.AlreadyRunning = true;
            Logging.Info("SwyxItLauncher: SwyxIt! läuft bereits, warte auf Tunnel...");
            result.TunnelAvailable = WaitForTunnel(tunnelTimeoutMs, out int waitMs);
            result.WaitTimeMs = waitMs;
            result.Success = result.TunnelAvailable;
            if (result.TunnelAvailable)
            {
                // SwyxIt! killen — CLMgr hält den Tunnel
                KillSwyxIt();
                result.SwyxItKilled = true;
                Logging.Info($"SwyxItLauncher: Tunnel offen nach {waitMs}ms. SwyxIt! gekillt — CLMgr hält Tunnel.");
            }
            else
            {
                result.Error = $"Tunnel nach {tunnelTimeoutMs}ms nicht verfügbar. SwyxIt! läuft, aber Port 9094 ist geschlossen.";
                Logging.Warn($"SwyxItLauncher: {result.Error}");
            }
            return result;
        }

        // Schritt 2: Pfad finden
        var swyxItPath = FindSwyxItPath();
        if (swyxItPath == null)
        {
            result.Error = "SwyxIt!.exe nicht gefunden. Bitte installieren oder Pfad in Einstellungen angeben.";
            Logging.Error($"SwyxItLauncher: {result.Error}");
            return result;
        }
        result.SwyxItPath = swyxItPath;
        Logging.Info($"SwyxItLauncher: Starte {swyxItPath} (temporär für Tunnel-Aufbau)...");

        // Schritt 3: SwyxIt!.exe starten (Hidden)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = swyxItPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true,
            };

            var proc = Process.Start(psi);
            if (proc == null)
            {
                result.Error = "Process.Start hat null zurückgegeben.";
                Logging.Error($"SwyxItLauncher: {result.Error}");
                return result;
            }

            result.ProcessId = proc.Id;
            Logging.Info($"SwyxItLauncher: SwyxIt! gestartet (PID {proc.Id}). Warte auf Tunnel...");
        }
        catch (Exception ex)
        {
            result.Error = $"Start fehlgeschlagen: {ex.Message}";
            Logging.Error($"SwyxItLauncher: {result.Error}");
            return result;
        }

        // Schritt 4: Auf Tunnel warten (Port 9094)
        result.TunnelAvailable = WaitForTunnel(tunnelTimeoutMs, out int waited);
        result.WaitTimeMs = waited;

        if (result.TunnelAvailable)
        {
            result.Success = true;
            // Schritt 5: SwyxIt! killen — CLMgr hält den Tunnel eigenständig
            KillSwyxIt();
            result.SwyxItKilled = true;
            Logging.Info($"SwyxItLauncher: Tunnel offen nach {waited}ms. SwyxIt! gekillt — CLMgr hält Tunnel.");
        }
        else
        {
            // Timeout — SwyxIt! läuft, aber kein Tunnel. Nicht killen, User muss sich anmelden.
            result.Error = $"Tunnel nach {tunnelTimeoutMs}ms nicht verfügbar. SwyxIt! läuft (PID {result.ProcessId}), aber Port 9094 ist geschlossen. Prüfe RemoteConnector-Konfiguration.";
            Logging.Warn($"SwyxItLauncher: {result.Error}");
            result.Success = IsRunning();
        }

        return result;
    }

    /// <summary>
    /// Wartet auf Port 9094 (Tunnel) mit Polling.
    /// </summary>
    private static bool WaitForTunnel(int timeoutMs, out int elapsedMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int pollInterval = 500;

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (IsTunnelAvailable())
            {
                elapsedMs = (int)sw.ElapsedMilliseconds;
                return true;
            }
            Thread.Sleep(pollInterval);
        }

        elapsedMs = (int)sw.ElapsedMilliseconds;
        return false;
    }

    /// <summary>
    /// Killt SwyxIt!.exe sofort (Force-Kill, kein graceful close).
    /// CLMgr.exe bleibt am Leben und hält den Tunnel.
    /// </summary>
    private static void KillSwyxIt()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("SwyxIt!"))
            {
                Logging.Info($"SwyxItLauncher: Kill SwyxIt! (PID {proc.Id}) — CLMgr bleibt.");
                proc.Kill();
                proc.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxItLauncher: Kill fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Beendet SwyxIt!.exe sauber.
    /// </summary>
    public static void Stop()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("SwyxIt!"))
            {
                Logging.Info($"SwyxItLauncher: Beende SwyxIt! (PID {proc.Id})...");
                proc.CloseMainWindow();
                if (!proc.WaitForExit(5000))
                {
                    proc.Kill();
                    Logging.Warn($"SwyxItLauncher: SwyxIt! (PID {proc.Id}) wurde gekillt.");
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxItLauncher: Stop fehlgeschlagen: {ex.Message}");
        }
    }
}
