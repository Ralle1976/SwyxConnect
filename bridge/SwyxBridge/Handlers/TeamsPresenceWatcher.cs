using System.Text.RegularExpressions;
using Microsoft.Win32;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Überwacht den Microsoft Teams Status über lokale Mechanismen:
///   1. Log-Datei-Parsing (Legacy/Classic Teams) — liest logs.txt
///   2. Periodischer Process-Check als Fallback
///
/// Quellen / Referenzen:
///   - pathartl/TeamsPresence (C#, CC0) — Regex-Muster für Legacy Teams
///   - AntoineGS/TeamsStatusV2 (PS, MIT) — zusätzliche Muster
///   - uncannyowly/pyTeamsStatus (Python) — Bestätigung der Muster
///
/// HINWEIS: NEW Teams (Store-App) schreibt KEINE Presence-Änderungen
/// in lokale Log-Dateien. Für NEW Teams wird der Process-Check verwendet.
/// </summary>
public sealed class TeamsPresenceWatcher
{
    // ─── Regex-Muster (bewährt aus Open-Source-Projekten) ────────────────────

    /// <summary>StatusIndicatorStateService: Added Available/Busy/Away/etc.</summary>
    private static readonly Regex StatusRegex = new(
        @"StatusIndicatorStateService: Added (\w+)",
        RegexOptions.Compiled);

    /// <summary>Setting the taskbar overlay icon - Available/Busy/etc.</summary>
    private static readonly Regex OverlayRegex = new(
        @"Setting the taskbar overlay icon - (\w+)",
        RegexOptions.Compiled);

    /// <summary>desktop_call_state_change_send, isOngoing: true/false</summary>
    private static readonly Regex ActivityRegex = new(
        @"name: desktop_call_state_change_send, isOngoing: (\w+)",
        RegexOptions.Compiled);

    // ─── Gültige Teams-Status-Werte ─────────────────────────────────────────

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "Busy", "OnThePhone", "Away", "BeRightBack",
        "DoNotDisturb", "Presenting", "Focusing", "InAMeeting", "Offline"
    };

    // ─── State ──────────────────────────────────────────────────────────────

    public string CurrentAvailability { get; private set; } = "Unknown";
    public string CurrentActivity { get; private set; } = "Unknown";
    public string DetectionSource { get; private set; } = "none";
    public bool IsRunning { get; private set; }

    private Thread? _watcherThread;
    private volatile bool _stopRequested;
    private long _lastFilePosition;

    // ─── Log-Pfade ──────────────────────────────────────────────────────────

    private static string LegacyLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Teams", "logs.txt");

    private static string NewTeamsLogDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "MSTeams_8wekyb3d8bbwe", "LocalCache", "Microsoft", "MSTeams", "Logs");

    // ─── Public API ─────────────────────────────────────────────────────────

    public void Start()
    {
        if (IsRunning) return;

        _stopRequested = false;
        IsRunning = true;

        _watcherThread = new Thread(WatchLoop)
        {
            IsBackground = true,
            Name = "TeamsPresenceWatcher"
        };
        _watcherThread.Start();

        Logging.Info("TeamsPresenceWatcher: Gestartet.");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _stopRequested = true;
        IsRunning = false;

        // Warten bis Thread beendet (max 3s)
        _watcherThread?.Join(3000);
        _watcherThread = null;

        Logging.Info("TeamsPresenceWatcher: Gestoppt.");
    }

    public object GetStatus() => new
    {
        availability = CurrentAvailability,
        activity = CurrentActivity,
        source = DetectionSource,
        isRunning = IsRunning,
        teamsInstalled = DetectInstalledTeams()
    };

    // ─── Hauptschleife ──────────────────────────────────────────────────────

    private void WatchLoop()
    {
        Logging.Info("TeamsPresenceWatcher: Watch-Loop gestartet.");

        var installed = DetectInstalledTeams();
        Logging.Info($"TeamsPresenceWatcher: Erkannt — Legacy={installed.legacyInstalled}, New={installed.newInstalled}");

        // Legacy Teams: Log-Datei überwachen
        if (installed.legacyInstalled && File.Exists(LegacyLogPath))
        {
            Logging.Info($"TeamsPresenceWatcher: Überwache Legacy-Log: {LegacyLogPath}");
            DetectionSource = "logfile";
            WatchLogFile(LegacyLogPath);
            return;
        }

        // New Teams: Versuch Log-Dateien im neuen Verzeichnis zu finden
        if (installed.newInstalled && Directory.Exists(NewTeamsLogDir))
        {
            var logFiles = Directory.GetFiles(NewTeamsLogDir, "*.log")
                .OrderByDescending(File.GetLastWriteTime)
                .ToArray();

            if (logFiles.Length > 0)
            {
                Logging.Info($"TeamsPresenceWatcher: Versuche New-Teams-Log: {logFiles[0]}");
                DetectionSource = "logfile-new";
                WatchLogFile(logFiles[0]);
                return;
            }
        }

        // Fallback: Process-Check Polling (alle 10s)
        Logging.Info("TeamsPresenceWatcher: Kein Log gefunden, verwende Process-Check Fallback.");
        DetectionSource = "process-check";
        ProcessCheckLoop();
    }

    // ─── Log-Datei-Überwachung ──────────────────────────────────────────────

    private void WatchLogFile(string logPath)
    {
        try
        {
            string dir = Path.GetDirectoryName(logPath)!;
            string fileName = Path.GetFileName(logPath);

            // Ans Ende der Datei springen (nur neue Einträge lesen)
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _lastFilePosition = fs.Length;

            using var fsw = new FileSystemWatcher(dir)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            // Initiale Rückwärtssuche: letzte 50KB lesen für aktuellen Status
            ReadInitialStatus(fs);

            while (!_stopRequested)
            {
                // Warte auf Änderung (mit Timeout für Stop-Check)
                fsw.WaitForChanged(WatcherChangeTypes.Changed, 2000);

                if (_stopRequested) break;

                // Neue Zeilen lesen
                ReadNewLines(fs);
            }
        }
        catch (Exception ex)
        {
            Logging.Error($"TeamsPresenceWatcher: Log-Watcher Fehler: {ex.Message}");

            // Fallback auf Process-Check
            if (!_stopRequested)
            {
                DetectionSource = "process-check";
                ProcessCheckLoop();
            }
        }
    }

    /// <summary>
    /// Liest die letzten ~50KB der Log-Datei um den aktuellen Status zu ermitteln.
    /// </summary>
    private void ReadInitialStatus(FileStream fs)
    {
        try
        {
            long readStart = Math.Max(0, fs.Length - 50_000);
            fs.Seek(readStart, SeekOrigin.Begin);

            using var sr = new StreamReader(fs, leaveOpen: true);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                ParseLine(line, emitEvent: false);
            }

            _lastFilePosition = fs.Position;

            if (CurrentAvailability != "Unknown")
            {
                Logging.Info($"TeamsPresenceWatcher: Initialer Status aus Log: {CurrentAvailability} / {CurrentActivity}");
                EmitPresenceChanged();
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"TeamsPresenceWatcher: Initiale Log-Analyse: {ex.Message}");
        }
    }

    /// <summary>
    /// Liest neue Zeilen seit der letzten Position.
    /// </summary>
    private void ReadNewLines(FileStream fs)
    {
        try
        {
            if (fs.Length < _lastFilePosition)
            {
                // Log wurde rotiert/geleert
                _lastFilePosition = 0;
            }

            fs.Seek(_lastFilePosition, SeekOrigin.Begin);
            using var sr = new StreamReader(fs, leaveOpen: true);

            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                ParseLine(line, emitEvent: true);
            }

            _lastFilePosition = fs.Position;
        }
        catch (Exception ex)
        {
            Logging.Warn($"TeamsPresenceWatcher: Zeilen lesen: {ex.Message}");
        }
    }

    // ─── Zeilen-Parser ──────────────────────────────────────────────────────

    private void ParseLine(string line, bool emitEvent)
    {
        bool changed = false;

        // Status-Erkennung (zwei Quellen)
        var statusMatch = StatusRegex.Match(line);
        if (statusMatch.Success)
        {
            string value = statusMatch.Groups[1].Value;
            if (value != "NewActivity" && ValidStatuses.Contains(value))
            {
                if (CurrentAvailability != value)
                {
                    CurrentAvailability = value;
                    changed = true;
                }
            }
        }

        var overlayMatch = OverlayRegex.Match(line);
        if (overlayMatch.Success)
        {
            string value = overlayMatch.Groups[1].Value;
            if (ValidStatuses.Contains(value) && CurrentAvailability != value)
            {
                CurrentAvailability = value;
                changed = true;
            }
        }

        // Aktivitäts-Erkennung (InACall / NotInACall)
        var activityMatch = ActivityRegex.Match(line);
        if (activityMatch.Success)
        {
            string newActivity = activityMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase)
                ? "InACall"
                : "NotInACall";

            if (CurrentActivity != newActivity)
            {
                CurrentActivity = newActivity;
                changed = true;
            }
        }

        if (changed && emitEvent)
        {
            EmitPresenceChanged();
        }
    }

    // ─── Process-Check Fallback ─────────────────────────────────────────────

    private void ProcessCheckLoop()
    {
        string lastProcessStatus = "Unknown";

        while (!_stopRequested)
        {
            try
            {
                bool teamsRunning = IsTeamsProcessRunning();
                string status = teamsRunning ? "Available" : "Offline";

                if (status != lastProcessStatus)
                {
                    lastProcessStatus = status;
                    CurrentAvailability = status;
                    CurrentActivity = "Unknown";
                    EmitPresenceChanged();
                    Logging.Info($"TeamsPresenceWatcher: Process-Check → {status}");
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"TeamsPresenceWatcher: Process-Check Fehler: {ex.Message}");
            }

            // 10 Sekunden warten
            for (int i = 0; i < 100 && !_stopRequested; i++)
                Thread.Sleep(100);
        }
    }

    private static bool IsTeamsProcessRunning()
    {
        try
        {
            // New Teams = ms-teams, Legacy = Teams
            var processes = System.Diagnostics.Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    string name = p.ProcessName.ToLowerInvariant();
                    if (name == "teams" || name == "ms-teams" || name == "msteams")
                        return true;
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
        return false;
    }

    // ─── Event-Emission ─────────────────────────────────────────────────────

    private void EmitPresenceChanged()
    {
        JsonRpcEmitter.EmitEvent("teamsPresenceChanged", new
        {
            availability = CurrentAvailability,
            activity = CurrentActivity,
            source = DetectionSource
        });
    }

    // ─── Teams-Erkennung ────────────────────────────────────────────────────

    private static (bool legacyInstalled, bool newInstalled) DetectInstalledTeams()
    {
        bool legacy = false;
        bool newTeams = false;

        try
        {
#pragma warning disable CA1416
            int legacyState = (int)(Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\IM Providers\Teams",
                "UpAndRunning", 0) ?? 0);
            legacy = legacyState > 0;
#pragma warning restore CA1416
        }
        catch { }

        try
        {
#pragma warning disable CA1416
            int newState = (int)(Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\IM Providers\MsTeams",
                "UpAndRunning", 0) ?? 0);
            newTeams = newState > 0;
#pragma warning restore CA1416
        }
        catch { }

        // Zusätzlich: Prüfe ob Log-Pfade existieren
        if (!legacy && File.Exists(LegacyLogPath))
            legacy = true;

        if (!newTeams && Directory.Exists(NewTeamsLogDir))
            newTeams = true;

        return (legacy, newTeams);
    }
}
