using Microsoft.AspNetCore.SignalR;
using SwyxBridge.Utils;

namespace SwyxBridge.Standalone;

public sealed class SwyxConnectHub : Hub
{
    private readonly ILineManagerProvider _lineManagerProvider;
    private readonly ICdsRestApi _cdsRestApi;
    private readonly ISwyxItHubBackend _hubBackend;

    public SwyxConnectHub(ILineManagerProvider lmp, ICdsRestApi cds, ISwyxItHubBackend hub)
    { _lineManagerProvider = lmp; _cdsRestApi = cds; _hubBackend = hub; }

    public override Task OnConnectedAsync()
    { Logging.Info($"SwyxConnectHub: Client verbunden — {Context.ConnectionId}"); return base.OnConnectedAsync(); }

    public override Task OnDisconnectedAsync(Exception? ex)
    { Logging.Info($"SwyxConnectHub: Client getrennt — {Context.ConnectionId}"); return base.OnDisconnectedAsync(ex); }

    public object Dial(string number, int lineId = 0) { _lineManagerProvider.DoWithLineManager(lm => lm.Dial(number, lineId)); return new { ok = true }; }
    public object HookOff(int lineId) { _lineManagerProvider.DoWithLineManager(lm => lm.HookOff(lineId)); return new { ok = true }; }
    public object HookOn(int lineId) { _lineManagerProvider.DoWithLineManager(lm => lm.HookOn(lineId)); return new { ok = true }; }
    public object Hold(int lineId) { _lineManagerProvider.DoWithLineManager(lm => lm.Hold(lineId)); return new { ok = true }; }
    public object Activate(int lineId) { _lineManagerProvider.DoWithLineManager(lm => lm.Activate(lineId)); return new { ok = true }; }
    public object Transfer(int lineId, string target) { _lineManagerProvider.DoWithLineManager(lm => lm.Transfer(lineId, target)); return new { ok = true }; }

    public object GetLineInfos()
    {
        LineInfo[] lines = Array.Empty<LineInfo>();
        _lineManagerProvider.DoWithLineManager(lm => lines = lm.GetAllLines());
        return new { lines };
    }

    public object GetLoggedInUser()
    {
        string user = "", server = ""; bool loggedIn = false;
        _lineManagerProvider.DoWithLineManager(lm => { user = lm.CurrentUser; server = lm.CurrentServer; loggedIn = lm.IsLoggedIn; });
        return new { user, server, loggedIn };
    }

    public object GetPresenceState() => new { state = "Available", text = "" };
    public object SetPresenceState(string state, string? text = null) => new { ok = true, state, text = text ?? "" };
    public object EnableEventNotifications(bool enable) => new { ok = true, enabled = enable };
}

public sealed class ComSocketCompatHub : Hub
{
    private readonly int _apiPort;
    public ComSocketCompatHub(StandaloneKestrelHost host) { _apiPort = host.ActualPort; }
    public object GetApiPort() => new { port = _apiPort };
}
