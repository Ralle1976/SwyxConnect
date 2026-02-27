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
    }

    private dynamic GetCom() =>
        _connector.GetCom() ?? throw new InvalidOperationException("COM nicht verbunden.");

    private dynamic GetLine(int lineId) =>
        GetCom().DispGetLine(lineId);

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
        try
        {
            var selectedLine = GetCom().DispSelectedLine;
            if (selectedLine != null)
            {
                selectedLine.DispHookOn();
            }
            else
            {
                // Fallback: Erste Leitung auflegen
                var fallbackLine = GetCom().DispGetLine(0);
                if (fallbackLine != null)
                {
                    fallbackLine.DispHookOn();
                }
            }
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
        return (int)GetCom().DispGetNumberOfLines();
    }

    public int GetSelectedLineId()
    {
        var line = GetCom().DispSelectedLine;
        return line != null ? (int)line.DispLineId : -1;
    }

    public int GetLineState(int lineId)
    {
        return (int)GetLine(lineId).DispState;
    }

    public object GetAllLines()
    {
        int count = GetLineCount();
        var lines = new List<object>();
        int selectedId = -1;
        try { selectedId = GetSelectedLineId(); } catch { }

        for (int i = 0; i < count; i++)
        {
            try
            {
                var line = GetLine(i);
                int state = (int)line.DispState;
                string callerName = "";
                string callerNumber = "";

                try
                {
                    callerName   = (string)(line.DispCallerName   ?? "");
                    callerNumber = (string)(line.DispCallerNumber ?? "");
                }
                catch { /* nicht alle Properties sind in jedem State verfügbar */ }

                lines.Add(new
                {
                    id = i,
                    state,
                    callerName,
                    callerNumber,
                    isSelected = (i == selectedId)
                });
            }
            catch (Exception ex)
            {
                Logging.Warn($"LineManager: Fehler bei Line {i}: {ex.Message}");
                lines.Add(new { id = i, state = 0, callerName = "", callerNumber = "", isSelected = false });
            }
        }

        return new { lines };
    }

    public object GetLineDetails(int lineId)
    {
        var line = GetLine(lineId);
        return new
        {
            id         = lineId,
            state      = (int)line.DispState,
            callerName = (string)(line.DispCallerName   ?? ""),
            callerNumber = (string)(line.DispCallerNumber ?? ""),
            isSelected = (lineId == GetSelectedLineId())
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
