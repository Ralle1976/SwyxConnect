namespace SwyxBridge.Standalone;

/// <summary>
/// Ersatz-Interface für IpPbx.Client.Plugin.ComSocket.IClientLineManagerProvider.
/// Das Original wraps CLMgr.exe COM — wir ersetzen es mit SIP-UA.
/// </summary>
public interface ILineManagerProvider : IDisposable
{
    void DoWithLineManager(Action<ILineManagerFacade> action);
    void DoWithLineManagerConfig(Action<ILineManagerFacade, IClientConfig> action);
    void CloseAll();
    event EventHandler<LineNotificationEventArgs> OnLineNotification;
}

/// <summary>
/// Façade über den Line Manager — abstrahiert COM-Aufrufe zu einer reinen .NET-API.
/// </summary>
public interface ILineManagerFacade
{
    bool IsLoggedIn { get; }
    bool IsServerUp { get; }
    string CurrentServer { get; }
    string CurrentUser { get; }
    int NumberOfLines { get; }
    void Dial(string number, int lineId = 0);
    void HookOff(int lineId);
    void HookOn(int lineId);
    void Hold(int lineId);
    void Activate(int lineId);
    void Transfer(int lineId, string targetNumber);
    LineInfo GetLineInfo(int lineId);
    LineInfo[] GetAllLines();
    int SelectedLineId { get; }
    void SetNumberOfLines(int count);
}

public interface IClientConfig
{
    string ServerAddress { get; }
    string Username { get; }
    string SipDomain { get; }
    int SipPort { get; }
}

public interface IEventDistributor
{
    void SetServiceProvider(IServiceProvider serviceProvider);
}

public interface ICdsRestApi
{
    string? BaseUri { get; }
    bool IsAvailable { get; }
    Task<string?> GetAsync(string endpoint);
    Task<string?> PostAsync(string endpoint, string? jsonBody = null);
}

public interface ISwyxItHubBackend
{
    void NotifyLineStateChanged(LineInfo[] lines);
    void NotifyPresenceChanged(string userId, string state);
    void NotifyIncomingCall(int lineId, string callerName, string callerNumber);
    void NotifyCallEnded(int lineId);
}

public interface IConnectionTokenStore
{
    string GenerateToken(string userId);
    bool ValidateToken(string token, out string userId);
    void RevokeToken(string token);
}

// --- Data Types ---

public sealed class LineInfo
{
    public int Id { get; init; }
    public string State { get; init; } = "Inactive";
    public string CallerName { get; init; } = "";
    public string CallerNumber { get; init; } = "";
    public bool IsSelected { get; init; }
}

public sealed class LineNotificationEventArgs : EventArgs
{
    public int Message { get; init; }
    public int Param { get; init; }
    public LineInfo? Line { get; init; }
}

public static class LineStates
{
    public const string Inactive = "Inactive";
    public const string HookOffInternal = "HookOffInternal";
    public const string HookOffExternal = "HookOffExternal";
    public const string Ringing = "Ringing";
    public const string Dialing = "Dialing";
    public const string Alerting = "Alerting";
    public const string Knocking = "Knocking";
    public const string Busy = "Busy";
    public const string Active = "Active";
    public const string OnHold = "OnHold";
    public const string ConferenceActive = "ConferenceActive";
    public const string ConferenceOnHold = "ConferenceOnHold";
    public const string Terminated = "Terminated";
    public const string Transferring = "Transferring";
    public const string Disabled = "Disabled";
    public const string DirectCall = "DirectCall";
}
