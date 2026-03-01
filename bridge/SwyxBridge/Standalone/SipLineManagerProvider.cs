using SwyxBridge.Utils;

namespace SwyxBridge.Standalone;

public sealed class SipLineManagerProvider : ILineManagerProvider
{
    private readonly object _syncRoot = new();
    private readonly SipLineManagerFacade _facade;
    private bool _disposed;
    public event EventHandler<LineNotificationEventArgs>? OnLineNotification;

    public SipLineManagerProvider(SipClientConfig config)
    {
        _facade = new SipLineManagerFacade(config, RaiseNotification);
        Logging.Info("SipLineManagerProvider: Initialisiert (Stub-Modus).");
    }

    public void DoWithLineManager(Action<ILineManagerFacade> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_syncRoot) { action(_facade); }
    }

    public void DoWithLineManagerConfig(Action<ILineManagerFacade, IClientConfig> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_syncRoot) { action(_facade, _facade.Config); }
    }

    public void CloseAll()
    {
        lock (_syncRoot) { _facade.Shutdown(); }
        Logging.Info("SipLineManagerProvider: CloseAll aufgerufen.");
    }

    private void RaiseNotification(LineNotificationEventArgs args) =>
        OnLineNotification?.Invoke(this, args);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseAll();
    }
}

public sealed class SipLineManagerFacade : ILineManagerFacade
{
    private readonly SipClientConfig _config;
    private readonly Action<LineNotificationEventArgs> _notify;
    private readonly LineState[] _lines;
    private int _selectedLineId;
    public SipClientConfig Config => _config;

    public SipLineManagerFacade(SipClientConfig config, Action<LineNotificationEventArgs> notify)
    {
        _config = config;
        _notify = notify;
        _lines = new LineState[Math.Max(config.NumberOfLines, 2)];
        for (int i = 0; i < _lines.Length; i++)
            _lines[i] = new LineState { Id = i };
    }

    public bool IsLoggedIn => true;
    public bool IsServerUp => !string.IsNullOrEmpty(_config.ServerAddress);
    public string CurrentServer => _config.ServerAddress;
    public string CurrentUser => _config.Username;
    public int NumberOfLines => _lines.Length;
    public int SelectedLineId => _selectedLineId;

    public void Dial(string number, int lineId = 0)
    {
        var line = GetLineState(lineId);
        line.State = LineStates.Dialing;
        line.PeerNumber = number;
        line.PeerName = "";
        _selectedLineId = lineId;
        Logging.Info($"SipLineManager: Dial({number}) auf Leitung {lineId}");
        NotifyLineChanged(lineId);
    }

    public void HookOff(int lineId)
    {
        var line = GetLineState(lineId);
        if (line.State == LineStates.Ringing)
        {
            line.State = LineStates.Active;
            Logging.Info($"SipLineManager: HookOff({lineId}) → Active (Anruf angenommen)");
        }
        else
        {
            line.State = LineStates.HookOffInternal;
            Logging.Info($"SipLineManager: HookOff({lineId}) → HookOffInternal");
        }
        _selectedLineId = lineId;
        NotifyLineChanged(lineId);
    }

    public void HookOn(int lineId)
    {
        var line = GetLineState(lineId);
        line.State = LineStates.Inactive;
        line.PeerName = "";
        line.PeerNumber = "";
        Logging.Info($"SipLineManager: HookOn({lineId}) → Inactive");
        NotifyLineChanged(lineId);
    }

    public void Hold(int lineId)
    {
        GetLineState(lineId).State = LineStates.OnHold;
        Logging.Info($"SipLineManager: Hold({lineId})");
        NotifyLineChanged(lineId);
    }

    public void Activate(int lineId)
    {
        var line = GetLineState(lineId);
        if (line.State == LineStates.OnHold) line.State = LineStates.Active;
        _selectedLineId = lineId;
        NotifyLineChanged(lineId);
    }

    public void Transfer(int lineId, string targetNumber)
    {
        var line = GetLineState(lineId);
        line.State = LineStates.Transferring;
        NotifyLineChanged(lineId);
        line.State = LineStates.Inactive;
        line.PeerName = "";
        line.PeerNumber = "";
        NotifyLineChanged(lineId);
    }

    public LineInfo GetLineInfo(int lineId)
    {
        var line = GetLineState(lineId);
        return new LineInfo
        {
            Id = line.Id, State = line.State,
            CallerName = line.PeerName, CallerNumber = line.PeerNumber,
            IsSelected = lineId == _selectedLineId
        };
    }

    public LineInfo[] GetAllLines()
    {
        var result = new LineInfo[_lines.Length];
        for (int i = 0; i < _lines.Length; i++)
            result[i] = GetLineInfo(i);
        return result;
    }

    public void SetNumberOfLines(int count) =>
        Logging.Info($"SipLineManager: SetNumberOfLines({count}) — In-Memory: {_lines.Length}");

    internal void Shutdown()
    {
        for (int i = 0; i < _lines.Length; i++)
        {
            _lines[i].State = LineStates.Inactive;
            _lines[i].PeerName = "";
            _lines[i].PeerNumber = "";
        }
        Logging.Info("SipLineManager: Shutdown — alle Leitungen auf Inactive.");
    }

    internal void SimulateIncomingCall(int lineId, string callerName, string callerNumber)
    {
        var line = GetLineState(lineId);
        line.State = LineStates.Ringing;
        line.PeerName = callerName;
        line.PeerNumber = callerNumber;
        NotifyLineChanged(lineId);
    }

    private LineState GetLineState(int lineId)
    {
        if (lineId < 0 || lineId >= _lines.Length)
            throw new ArgumentOutOfRangeException(nameof(lineId));
        return _lines[lineId];
    }

    private void NotifyLineChanged(int lineId)
    {
        var info = GetLineInfo(lineId);
        _notify(new LineNotificationEventArgs { Message = 0, Param = lineId, Line = info });
    }

    private sealed class LineState
    {
        public int Id;
        public string State = LineStates.Inactive;
        public string PeerName = "";
        public string PeerNumber = "";
    }
}

public sealed class SipClientConfig : IClientConfig
{
    public string ServerAddress { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string SipDomain { get; set; } = "";
    public int SipPort { get; set; } = 5060;
    public int NumberOfLines { get; set; } = 2;
    public int KestrelPort { get; set; } = 0;
    public string PublicServer { get; set; } = "";
    public int PublicSipPort { get; set; } = 15021;
    public string PublicAuthServer { get; set; } = "";
    public int PublicAuthPort { get; set; } = 8021;
}
