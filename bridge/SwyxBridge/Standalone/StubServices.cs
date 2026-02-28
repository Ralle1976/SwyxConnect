using SwyxBridge.Utils;

namespace SwyxBridge.Standalone;

public sealed class StubEventDistributor : IEventDistributor
{
    public void SetServiceProvider(IServiceProvider serviceProvider) =>
        Logging.Info("EventDistributor: ServiceProvider gesetzt.");
}

public sealed class StubCdsRestApi : ICdsRestApi
{
    private readonly SipClientConfig _config;
    private HttpClient? _httpClient;
    public string? BaseUri { get; private set; }
    public bool IsAvailable => !string.IsNullOrEmpty(BaseUri);

    public StubCdsRestApi(SipClientConfig config)
    {
        _config = config;
        if (!string.IsNullOrEmpty(config.ServerAddress))
            BaseUri = $"https://{config.ServerAddress}:9443";
        Logging.Info($"CdsRestApi: BaseUri={BaseUri ?? "(nicht verfügbar)"}");
    }

    public async Task<string?> GetAsync(string endpoint)
    {
        if (!IsAvailable) return null;
        try
        {
            var client = GetHttpClient();
            var resp = await client.GetAsync($"{BaseUri}/{endpoint.TrimStart('/')}");
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
        }
        catch (Exception ex) { Logging.Warn($"CdsRestApi GET {endpoint}: {ex.Message}"); return null; }
    }

    public async Task<string?> PostAsync(string endpoint, string? jsonBody = null)
    {
        if (!IsAvailable) return null;
        try
        {
            var client = GetHttpClient();
            var content = jsonBody != null ? new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json") : null;
            var resp = await client.PostAsync($"{BaseUri}/{endpoint.TrimStart('/')}", content);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
        }
        catch (Exception ex) { Logging.Warn($"CdsRestApi POST {endpoint}: {ex.Message}"); return null; }
    }

    private HttpClient GetHttpClient()
    {
        if (_httpClient != null) return _httpClient;
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        return _httpClient;
    }
}

public sealed class StubConnectionTokenStore : IConnectionTokenStore
{
    private readonly Dictionary<string, string> _tokens = new();
    private readonly object _lock = new();

    public string GenerateToken(string userId)
    {
        var token = Guid.NewGuid().ToString("N");
        lock (_lock) { _tokens[token] = userId; }
        return token;
    }

    public bool ValidateToken(string token, out string userId)
    {
        lock (_lock) { return _tokens.TryGetValue(token, out userId!); }
    }

    public void RevokeToken(string token)
    {
        lock (_lock) { _tokens.Remove(token); }
    }
}

public sealed class StubSwyxItHubBackend : ISwyxItHubBackend
{
    public void NotifyLineStateChanged(LineInfo[] lines) => Logging.Debug($"HubBackend: LineStateChanged ({lines.Length})");
    public void NotifyPresenceChanged(string userId, string state) => Logging.Debug($"HubBackend: Presence {userId}={state}");
    public void NotifyIncomingCall(int lineId, string callerName, string callerNumber) => Logging.Debug($"HubBackend: IncomingCall {lineId}");
    public void NotifyCallEnded(int lineId) => Logging.Debug($"HubBackend: CallEnded {lineId}");
}
