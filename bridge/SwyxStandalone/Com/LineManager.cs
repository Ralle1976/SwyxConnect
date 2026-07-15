using SwyxStandalone.Utils;

namespace SwyxStandalone.Com;

public sealed class LineManager
{
    private readonly StandaloneConnector _connector;

    // LineState int → string mapping (must match LineState enum in shared/types.ts)
    // 0=Inactive, 1=HookOffInternal, 2=HookOffExternal, 3=Ringing, 4=Dialing,
    // 5=Alerting, 6=Knocking, 7=Busy, 8=Active, 9=OnHold, 10=ConferenceActive,
    // 11=ConferenceOnHold, 12=Terminated, 13=Transferring, 14=Disabled, 15=DirectCall
    public static string MapLineState(int state) => state switch
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
        _  => $"Unknown_{state}"
    };

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
        // VERIFIED via live test: DispNumberOfLines is a Property (get), not a method.
        // DispGetNumberOfLines does NOT exist on the COM object.
        try
        {
            return (int)GetCom().DispNumberOfLines;
        }
        catch
        {
            try { return (int)GetCom().DispGetNumberOfLines(); }
            catch { return 0; }
        }
    }

    public int GetSelectedLineId()
    {
        try
        {
            return (int)GetCom().DispSelectedLineNumber;
        }
        catch
        {
            try
            {
                var line = GetCom().DispSelectedLine;
                return line != null ? (int)line.DispLineId : -1;
            }
            catch { return -1; }
        }
    }

    public string GetLineState(int lineId)
    {
        return MapLineState((int)GetLine(lineId).DispState);
    }

    public object GetAllLines()
    {
        int count = GetLineCount();
        var lines = new List<object>();
        int selectedId = -1;
        try { selectedId = GetSelectedLineId(); } catch { }

        // Headless CLMgr may report 0 or 1 lines — ensure at least 2 for functional calls.
        // RE finding: Dial stays Inactive with only 1 line; needs 2+ to go Active.
        if (count < 2)
        {
            Logging.Info($"LineManager: DispNumberOfLines={count}, setting to 2");
            try
            {
                GetCom().DispSetNumberOfLines(2);
                count = GetLineCount();
            }
            catch (Exception ex)
            {
                Logging.Warn($"LineManager: SetNumberOfLines failed: {ex.Message}");
            }
        }

        for (int i = 0; i < count; i++)
        {
            try
            {
                var line = GetLine(i);
                int stateRaw = 0;
                string callerName = "";
                string callerNumber = "";
                string peerName = "";
                string peerNumber = "";

                try { stateRaw = (int)line.DispState; } catch { }
                string state = MapLineState(stateRaw);

                try { callerName   = (string)(line.DispCallerName   ?? ""); } catch { }
                try { callerNumber = (string)(line.DispCallerNumber ?? ""); } catch { }
                // Fallback: peer properties (some COM versions expose peer instead of caller)
                try { peerName   = (string)(line.DispPeerName   ?? ""); } catch { }
                try { peerNumber = (string)(line.DispPeerNumber ?? ""); } catch { }

                // Use peer as fallback if caller is empty
                if (string.IsNullOrEmpty(callerName) && !string.IsNullOrEmpty(peerName))
                    callerName = peerName;
                if (string.IsNullOrEmpty(callerNumber) && !string.IsNullOrEmpty(peerNumber))
                    callerNumber = peerNumber;

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
                lines.Add(new { id = i, state = "Inactive", callerName = "", callerNumber = "", isSelected = false });
            }
        }

        return new { lines };
    }

    public object GetLineDetails(int lineId)
    {
        var line = GetLine(lineId);
        int stateRaw = 0;
        try { stateRaw = (int)line.DispState; } catch { }

        string callerName = "";
        string callerNumber = "";
        try { callerName   = (string)(line.DispCallerName   ?? ""); } catch { }
        try { callerNumber = (string)(line.DispCallerNumber ?? ""); } catch { }

        return new
        {
            id           = lineId,
            state        = MapLineState(stateRaw),
            callerName,
            callerNumber,
            isSelected   = (lineId == GetSelectedLineId())
        };
    }
}
