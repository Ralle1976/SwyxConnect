using System.Diagnostics;
using System.Net.Sockets;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Startet SwyxIt!.exe automatisch als unsichtbaren Tunnel-Provider.
///
/// Zweck: SwyxIt! baut den RemoteConnector-Tunnel auf (proprietäres Protokoll
/// auf Port 15021), der CDS lokal auf 127.0.0.1:9094 verfügbar macht.
/// Ohne SwyxIt! gibt es keinen Tunnel und damit keine Kontakte/Telefonie.
///
/// Ablauf:
///   1. Prüfe ob SwyxIt!.exe bereits läuft → wenn ja, fertig
///   2. Finde SwyxIt!.exe im Installationsverzeichnis
///   3. Starte SwyxIt!.exe mit WindowStyle=Hidden
///   4. WindowHook unterdrückt alle Fenster sofort
///   5. Warte auf Port 9094 (Tunnel aktiv) mit Timeout
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

        // Schritt 1: Bereits am Laufen?
        if (IsRunning())
        {
            result.AlreadyRunning = true;
            result.Success = true;
            result.TunnelAvailable = IsTunnelAvailable();
            Logging.Info($"SwyxItLauncher: SwyxIt! läuft bereits. Tunnel: {(result.TunnelAvailable ? "verfügbar" : "nicht verfügbar")}");
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
        Logging.Info($"SwyxItLauncher: Starte {swyxItPath} (versteckt)...");

        // Schritt 3: SwyxIt!.exe starten (Hidden)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = swyxItPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true, // nötig für WindowStyle
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
        var sw = Stopwatch.StartNew();
        int pollInterval = 500; // 500ms zwischen Checks

        while (sw.ElapsedMilliseconds < tunnelTimeoutMs)
        {
            if (IsTunnelAvailable())
            {
                result.TunnelAvailable = true;
                result.Success = true;
                result.WaitTimeMs = (int)sw.ElapsedMilliseconds;
                Logging.Info($"SwyxItLauncher: Tunnel verfügbar nach {result.WaitTimeMs}ms!");
                return result;
            }
            Thread.Sleep(pollInterval);
        }

        // Timeout — SwyxIt! läuft, aber Tunnel kam nicht hoch
        result.WaitTimeMs = (int)sw.ElapsedMilliseconds;
        result.Error = $"Tunnel nach {tunnelTimeoutMs}ms nicht verfügbar. SwyxIt! läuft (PID {result.ProcessId}), aber Port 9094 ist geschlossen. Prüfe RemoteConnector-Konfiguration.";
        Logging.Warn($"SwyxItLauncher: {result.Error}");

        // Trotzdem als "teilweise erfolgreich" markieren — SwyxIt! läuft, User kann sich manuell anmelden
        result.Success = IsRunning();
        return result;
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
