using IpPbx.CLMgrLib;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Wrapper um die CLMgr Dispatch-Methoden.
/// Alle Methoden MÜSSEN auf dem STA-Thread aufgerufen werden.
/// Verwendet typisierte IClientLineDisp / ClientLineMgrClass statt dynamic.
/// </summary>
public sealed class LineManager
{
    private readonly SwyxConnector _connector;

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
            try { current = com.DispNumberOfLines; } catch { }

            if (current == 0)
            {
                Logging.Info("LineManager: DispNumberOfLines=0, initialisiere mit DispSetNumberOfLines(2)...");
                try
                {
                    com.DispSetNumberOfLines(2);
                    int afterSet = com.DispNumberOfLines;
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

    /// <summary>
    /// Gibt eine typisierte Leitung per Index zurück.
    /// Fällt zurück auf DispSelectedLine wenn DispGetLine fehlschlägt.
    /// </summary>
    private IClientLineDisp GetLine(int lineId)
    {
        var com = GetCom();
        try
        {
            var lineObj = com.DispGetLine(lineId);
            if (lineObj is IClientLineDisp line) return line;
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: DispGetLine({lineId}) fehlgeschlagen: {ex.Message}");
        }
        // Fallback: DispSelectedLine
        try
        {
            var selObj = com.DispSelectedLine;
            if (selObj is IClientLineDisp sel) return sel;
        }
        catch { }
        throw new InvalidOperationException($"Leitung {lineId} nicht verfügbar.");
    }

    // --- Leitungsanzahl setzen ---

    public void SetNumberOfLines(int count)
    {
        if (count < 1 || count > 8) count = 2;
        Logging.Info($"LineManager: SetNumberOfLines({count})");
        try
        {
            GetCom().DispSetNumberOfLines(count);
            int actual = GetLineCount();
            Logging.Info($"LineManager: Nach SetNumberOfLines → {actual} Leitungen.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: SetNumberOfLines fehlgeschlagen: {ex.Message}");
        }
    }

    // --- Anruf-Steuerung ---

    public void Dial(string number)
    {
        Logging.Info($"LineManager: Dial({number})");

        try
        {
            // Versuche zuerst DispSimpleDialEx3 mit Line-Flag 0 (= aktuelle Leitung)
            // Parameter: (Nummer, LineId, Flags, CallerInfo)
            try
            {
                GetCom().DispSimpleDialEx3(number, 0, 0, "");
                Logging.Info("LineManager: DispSimpleDialEx3 erfolgreich.");
            }
            catch
            {
                // Fallback: DispDial über ausgewählte Leitung
                var selObj = GetCom().DispSelectedLine;
                if (selObj is IClientLineDisp selectedLine)
                {
                    selectedLine.DispDial(number);
                }
                else
                {
                    var lineObj = GetCom().DispGetLine(0);
                    if (lineObj is IClientLineDisp fallbackLine)
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
            var selObj = GetCom().DispSelectedLine;
            if (selObj is IClientLineDisp selectedLine)
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
        // Versuch 2: Erste Leitung hängig auflegen
        try
        {
            var lineObj = GetCom().DispGetLine(0);
            if (lineObj is IClientLineDisp line)
            {
                line.DispHookOn();
                Logging.Info("LineManager: Hangup via Line(0).DispHookOn()");
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
        try { return GetCom().DispNumberOfLines; }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: DispNumberOfLines fehlgeschlagen: {ex.Message}");
            return 1;
        }
    }

    public int GetSelectedLineId()
    {
        try { return GetCom().DispSelectedLineNumber; }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: DispSelectedLineNumber fehlgeschlagen: {ex.Message}");
            return 0;
        }
    }

    public int GetLineState(int lineId)
    {
        try { return GetLine(lineId).DispState; }
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
                var selObj = GetCom().DispSelectedLine;
                if (selObj is IClientLineDisp sel)
                {
                    int stateInt = 0;
                    string callerName = "", callerNumber = "";
                    try { stateInt = sel.DispState; } catch { }
                    try { callerName = sel.DispPeerName ?? ""; } catch { }
                    try { callerNumber = sel.DispPeerNumber ?? ""; } catch { }

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

            try { stateInt = line.DispState; } catch { }
            try { callerName = line.DispPeerName ?? ""; } catch { }
            try { callerNumber = line.DispPeerNumber ?? ""; } catch { }

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
            try { stateInt = line.DispState; } catch { }
            try { callerName = line.DispPeerName ?? ""; } catch { }
            try { callerNumber = line.DispPeerNumber ?? ""; } catch { }
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
}
