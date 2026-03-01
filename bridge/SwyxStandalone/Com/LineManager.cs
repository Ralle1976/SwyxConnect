using SwyxStandalone.Utils;

namespace SwyxStandalone.Com;

public sealed class LineManager
{
    private readonly StandaloneConnector _connector;

    public LineManager(StandaloneConnector connector)
    {
        _connector = connector;
    }

    private dynamic GetCom() =>
        _connector.GetCom() ?? throw new InvalidOperationException("COM nicht verbunden.");

    private dynamic GetLine(int lineId) =>
        GetCom().DispGetLine(lineId);

    public void SetNumberOfLines(int count)
    {
        Logging.Info($"LineManager: SetNumberOfLines({count})");
        GetCom().DispSetNumberOfLines(count);
    }

    public void Dial(string number)
    {
        Logging.Info($"LineManager: Dial({number})");
        try
        {
            GetCom().DispSimpleDialEx3(number, 0, 0, "");
            Logging.Info("LineManager: DispSimpleDialEx3 erfolgreich.");
        }
        catch
        {
            var selectedLine = GetCom().DispSelectedLine;
            if (selectedLine != null)
            {
                selectedLine.DispDial(number);
            }
            else
            {
                var fallback = GetCom().DispGetLine(0);
                if (fallback != null)
                    fallback.DispDial(number);
                else
                    Logging.Warn("LineManager: Keine Leitung zum Wählen.");
            }
        }
    }

    public void Hangup()
    {
        Logging.Info("LineManager: Hangup()");
        try
        {
            var selected = GetCom().DispSelectedLine;
            if (selected != null)
                selected.DispHookOn();
            else
                GetCom().DispGetLine(0)?.DispHookOn();
        }
        catch (Exception ex)
        {
            Logging.Warn($"LineManager: Hangup fehlgeschlagen: {ex.Message}");
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
                catch { }

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
            id           = lineId,
            state        = (int)line.DispState,
            callerName   = (string)(line.DispCallerName   ?? ""),
            callerNumber = (string)(line.DispCallerNumber ?? ""),
            isSelected   = (lineId == GetSelectedLineId())
        };
    }
}
