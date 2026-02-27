using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Wrapper um die CLMgr Dispatch-Methoden.
/// Alle Methoden MÜSSEN auf dem STA-Thread aufgerufen werden.
/// </summary>
public sealed class LineManager
{
    private readonly SwyxConnector _connector;

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
        GetCom().DispSimpleDial(number);
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
        // Blind Transfer: DispForwardCall leitet den Anruf weiter
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

    /// <summary>
    /// Gibt Details aller Leitungen als serialisierbares Objekt zurück.
    /// </summary>
    public object GetAllLines()
    {
        int count = GetLineCount();
        var lines = new List<object>();

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
                    callerName = (string)(line.DispCallerName ?? "");
                    callerNumber = (string)(line.DispCallerNumber ?? "");
                }
                catch
                {
                    // Nicht alle Properties sind in jedem Zustand verfügbar
                }

                lines.Add(new
                {
                    id = i,
                    state,
                    callerName,
                    callerNumber,
                    isSelected = (i == GetSelectedLineId())
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
            id = lineId,
            state = (int)line.DispState,
            callerName = (string)(line.DispCallerName ?? ""),
            callerNumber = (string)(line.DispCallerNumber ?? ""),
            isSelected = (lineId == GetSelectedLineId())
        };
    }
}
