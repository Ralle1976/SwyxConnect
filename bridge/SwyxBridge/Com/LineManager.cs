using System.Diagnostics;
using System.Runtime.InteropServices;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Wrapper um die CLMgr Dispatch-Methoden.
/// Alle Methoden MÜSSEN auf dem STA-Thread aufgerufen werden.
/// </summary>
public sealed class LineManager
{
    private readonly SwyxConnector _connector;

    // Win32 API zum Unterdrücken des SwyxIt!-Fensters
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    private const int SW_HIDE = 0;
    private const int SW_MINIMIZE = 6;
    private const int SW_SHOWMINNOACTIVE = 7;

    public LineManager(SwyxConnector connector)
    {
        _connector = connector;
        InitializeLines();
    }

    /// <summary>
    /// Initialisiert Leitungen beim Start. Im Headless-Modus (kein SwyxIt!-GUI)
    /// gibt DispNumberOfLines oft 0 zurück. DispSetNumberOfLines konfiguriert
    /// die Anzahl der verfügbaren Leitungen.
    /// </summary>
    private void InitializeLines()
    {
        try
        {
            var com = _connector.GetCom();
            if (com == null) return;

            int current = 0;
            try { current = (int)com.DispNumberOfLines; } catch { }

            if (current == 0)
            {
                Logging.Info("LineManager: DispNumberOfLines=0, initialisiere mit DispSetNumberOfLines(2)...");
                try
                {
                    com.DispSetNumberOfLines(2);
                    int afterSet = (int)com.DispNumberOfLines;
                    Logging.Info($"LineManager: Nach DispSetNumberOfLines → {afterSet} Leitungen.");
                }
                catch (Exception ex)
                {
                    Logging.Warn($"LineManager: DispSetNumberOfLines fehlgeschlagen: {ex.Message}");
                }
            }
            else
            {
                Logging.Info($"LineManager: {current} Leitungen verfügbar.");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: InitializeLines fehlgeschlagen: {ex.Message}");
        }
    }

    private dynamic GetCom() =>
        _connector.GetCom() ?? throw new InvalidOperationException("COM nicht verbunden.");

    private dynamic GetLine(int lineId)
    {
        var com = GetCom();
        try
        {
            var line = com.DispGetLine(lineId);
            if (line != null) return line;
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: DispGetLine({lineId}) fehlgeschlagen: {ex.Message}");
        }
        // Fallback: DispSelectedLine
        try
        {
            var sel = com.DispSelectedLine;
            if (sel != null) return sel;
        }
        catch { }
        throw new InvalidOperationException($"Leitung {lineId} nicht verfügbar.");
    }

    // --- Anruf-Steuerung ---

    public void Dial(string number)
    {
        Logging.Info($"LineManager: Dial({number})");

        // Merke aktuelles Vordergrund-Fenster (unser Electron-Fenster)
        var ourWindow = GetForegroundWindow();

        try
        {
            // Versuche zuerst DispSimpleDialEx3 mit Line-Flag 0 (= aktuelle Leitung)
            // Parameter: (Nummer, LineId, Flags, CallerInfo)
            // Manche Swyx-Versionen unterstützen dies ohne UI-Popup
            try
            {
                GetCom().DispSimpleDialEx3(number, 0, 0, "");
                Logging.Info("LineManager: DispSimpleDialEx3 erfolgreich.");
            }
            catch
            {
                // Fallback: DispDial über ausgewählte Leitung
                var selectedLine = GetCom().DispSelectedLine;
                if (selectedLine != null)
                {
                    selectedLine.DispDial(number);
                }
                else
                {
                    var fallbackLine = GetCom().DispGetLine(0);
                    if (fallbackLine != null)
                    {
                        fallbackLine.DispDial(number);
                    }
                    else
                    {
                        Logging.Warn("LineManager: Keine Leitung zum Wählen gefunden.");
                        return;
                    }
                }
            }

            // CRITICAL: SwyxIt!-Fenster sofort unterdrücken
            SuppressSwyxWindow(ourWindow);
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: Fehler beim Wählen: {ex.Message}");
        }
    }

    public void Hangup()
    {
        Logging.Info("LineManager: Hangup()");
        // Versuch 1: DispSelectedLine.DispHookOn()
        try
        {
            var selectedLine = GetCom().DispSelectedLine;
            if (selectedLine != null)
            {
                selectedLine.DispHookOn();
                Logging.Info("LineManager: Hangup via DispSelectedLine.DispHookOn()");
                return;
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: DispSelectedLine.DispHookOn() fehlgeschlagen: {ex.Message}");
        }
        // Versuch 2: GetCom().DispHookOn() direkt
        try
        {
            GetCom().DispHookOn();
            Logging.Info("LineManager: Hangup via GetCom().DispHookOn()");
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: Fehler beim Auflegen: {ex.Message}");
        }
    }

    public void HookOff(int lineId)
    {
        Logging.Info($"LineManager: HookOff(line={lineId})");
        GetLine(lineId).DispHookOff();
    }

    public void HookOn(int lineId)
    {
        Logging.Info($"LineManager: HookOn(line={lineId})");
        GetLine(lineId).DispHookOn();
    }

    public void Hold(int lineId)
    {
        Logging.Info($"LineManager: Hold(line={lineId})");
        GetLine(lineId).DispHold();
    }

    public void Activate(int lineId)
    {
        Logging.Info($"LineManager: Activate(line={lineId})");
        GetLine(lineId).DispActivate();
    }

    public void Transfer(int lineId, string targetNumber)
    {
        Logging.Info($"LineManager: Transfer(line={lineId}, target={targetNumber})");
        GetLine(lineId).DispForwardCall(targetNumber);
    }

    // --- Abfragen ---

    public int GetLineCount()
    {
        try { return (int)GetCom().DispNumberOfLines; }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: DispNumberOfLines fehlgeschlagen: {ex.Message}");
            return 1;
        }
    }

    public int GetSelectedLineId()
    {
        try { return (int)GetCom().DispSelectedLineNumber; }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: DispSelectedLineNumber fehlgeschlagen: {ex.Message}");
            return 0;
        }
    }

    public int GetLineState(int lineId)
    {
        try { return (int)GetLine(lineId).DispState; }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: GetLineState({lineId}) fehlgeschlagen: {ex.Message}");
            return 0; // Inactive
        }
    }

    /// <summary>
    /// Mappt den COM DispState-Integer auf den TypeScript LineState-Enum-String.
    /// </summary>
    private static string MapLineState(int stateInt) => stateInt switch
    {
        0  => "Inactive",
        1  => "HookOffInternal",
        2  => "HookOffExternal",
        3  => "Ringing",
        4  => "Dialing",
        5  => "Alerting",
        6  => "Knocking",
        7  => "Busy",
        8  => "Active",
        9  => "OnHold",
        10 => "ConferenceActive",
        11 => "ConferenceOnHold",
        12 => "Terminated",
        13 => "Transferring",
        14 => "Disabled",
        15 => "DirectCall",
        _  => "Inactive"
    };

    public object GetAllLines()
    {
        int count = GetLineCount();
        var lines = new List<object>();
        int selectedId = GetSelectedLineId();

        // Headless-Fallback: Wenn DispNumberOfLines noch 0, nochmal initialisieren
        if (count == 0)
        {
            try
            {
                GetCom().DispSetNumberOfLines(2);
                count = GetLineCount();
                Logging.Info($"LineManager: Re-init → {count} Leitungen.");
            }
            catch { }
        }

        // Normaler Pfad: alle Leitungen per Index abfragen
        for (int i = 0; i < count; i++)
        {
            lines.Add(ReadLine(i, i == selectedId));
        }

        // Letzter Fallback: wenn count immer noch 0, DispSelectedLine direkt
        if (lines.Count == 0)
        {
            try
            {
                var sel = GetCom().DispSelectedLine;
                if (sel != null)
                {
                    int stateInt = 0;
                    string callerName = "", callerNumber = "";
                    try { stateInt = (int)sel.DispState; } catch { }
                    try { callerName = (string)(sel.DispCallerName ?? ""); } catch { }
                    if (string.IsNullOrEmpty(callerName))
                        try { callerName = (string)(sel.DispPeerName ?? ""); } catch { }
                    try { callerNumber = (string)(sel.DispCallerNumber ?? ""); } catch { }
                    if (string.IsNullOrEmpty(callerNumber))
                        try { callerNumber = (string)(sel.DispPeerNumber ?? ""); } catch { }

                    lines.Add(new
                    {
                        id = Math.Max(selectedId, 0),
                        state = MapLineState(stateInt),
                        callerName,
                        callerNumber,
                        isSelected = true
                    });
                    Logging.Info($"LineManager: Fallback DispSelectedLine → state={MapLineState(stateInt)}");
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"LineManager: DispSelectedLine Fallback fehlgeschlagen: {ex.Message}");
            }
        }

        Logging.Info($"LineManager: GetAllLines → {lines.Count} Leitungen, selected={selectedId}");
        return new { lines };
    }

    /// <summary>
    /// Liest eine einzelne Leitung per Index. Fängt Fehler ab und gibt Inactive zurück.
    /// </summary>
    private object ReadLine(int lineId, bool isSelected)
    {
        try
        {
            var line = GetLine(lineId);
            int stateInt = 0;
            string callerName = "";
            string callerNumber = "";

            try { stateInt = (int)line.DispState; } catch { }
            try { callerName = (string)(line.DispCallerName ?? ""); } catch { }
            if (string.IsNullOrEmpty(callerName))
                try { callerName = (string)(line.DispPeerName ?? ""); } catch { }
            try { callerNumber = (string)(line.DispCallerNumber ?? ""); } catch { }
            if (string.IsNullOrEmpty(callerNumber))
                try { callerNumber = (string)(line.DispPeerNumber ?? ""); } catch { }

            return new
            {
                id = lineId,
                state = MapLineState(stateInt),
                callerName,
                callerNumber,
                isSelected
            };
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: Line {lineId} fehlgeschlagen: {ex.Message}");
            return new { id = lineId, state = "Inactive", callerName = "", callerNumber = "", isSelected = false };
        }
    }

    public object GetLineDetails(int lineId)
    {
        string callerName   = "";
        string callerNumber = "";
        int stateInt = 0;
        try
        {
            var line = GetLine(lineId);
            try { stateInt = (int)line.DispState; } catch { }
            try { callerName = (string)(line.DispCallerName ?? ""); } catch { }
            if (string.IsNullOrEmpty(callerName))
                try { callerName = (string)(line.DispPeerName ?? ""); } catch { }
            try { callerNumber = (string)(line.DispCallerNumber ?? ""); } catch { }
            if (string.IsNullOrEmpty(callerNumber))
                try { callerNumber = (string)(line.DispPeerNumber ?? ""); } catch { }
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: GetLineDetails({lineId}) fehlgeschlagen: {ex.Message}");
        }
        return new
        {
            id           = lineId,
            state        = MapLineState(stateInt),
            callerName,
            callerNumber,
            isSelected   = true
        };
    }

    // --- Window-Suppression ---

    /// <summary>
    /// Unterdrückt das SwyxIt!-Fenster nach einem Dial-Vorgang.
    /// Findet SwyxIt! über den Prozessnamen und minimiert/versteckt es.
    /// </summary>
    private void SuppressSwyxWindow(IntPtr previousForeground)
    {
        try
        {
            // Kurz warten damit SwyxIt! Zeit hat in den Vordergrund zu kommen
            Thread.Sleep(150);

            // Prüfe ob sich das Vordergrund-Fenster geändert hat
            var currentForeground = GetForegroundWindow();
            if (currentForeground == previousForeground || currentForeground == IntPtr.Zero)
                return; // Nichts passiert, SwyxIt! hat sich nicht nach vorne gedrängt

            // Prüfe ob das neue Vordergrund-Fenster zu SwyxIt! gehört
            GetWindowThreadProcessId(currentForeground, out uint processId);
            try
            {
                var proc = Process.GetProcessById((int)processId);
                var procName = proc.ProcessName.ToLowerInvariant();
                if (procName.Contains("swyxit") || procName.Contains("swyx") || procName.Contains("clmgr"))
                {
                    ShowWindow(currentForeground, SW_SHOWMINNOACTIVE);
                    Logging.Info($"LineManager: SwyxIt!-Fenster unterdrückt (PID={processId}, Process={proc.ProcessName})");

                    // Unser Fenster wieder nach vorne bringen
                    if (previousForeground != IntPtr.Zero)
                        SetForegroundWindow(previousForeground);
                }
            }
            catch { /* Prozess bereits beendet oder Zugriff verweigert */ }

            // Zusätzlich: Alle SwyxIt!-Fenster durchgehen und minimieren
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var name = proc.ProcessName.ToLowerInvariant();
                    if ((name.Contains("swyxit") || name == "swyxitc") && proc.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(proc.MainWindowHandle, SW_SHOWMINNOACTIVE);
                        Logging.Info($"LineManager: SwyxIt!-Prozess minimiert: {proc.ProcessName} (PID={proc.Id})");
                    }
                }
                catch { /* Ignore */ }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: SuppressSwyxWindow fehlgeschlagen: {ex.Message}");
        }
    }
}
