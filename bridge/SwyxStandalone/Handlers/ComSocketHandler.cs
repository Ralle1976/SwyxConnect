using System.Text.Json;
using SwyxStandalone.Com;
using SwyxStandalone.JsonRpc;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Handlers;

/// <summary>
/// JSON-RPC handler that exposes ComSocket (SignalR) methods to the Electron frontend.
/// All methods route through the ComSocketClient to the SwyxItHub running in CLMgr.
///
/// This provides richer data than the COM API:
///   - GetPhoneBookEntries: ALL colleagues with live presence
///   - SearchContacts: full-text contact search
///   - GetCallJournal: detailed call history
///   - Real-time push events instead of polling
/// </summary>
public sealed class ComSocketHandler
{
    private readonly ComSocketClient _client;

    public ComSocketHandler(ComSocketClient client)
    {
        _client = client;
    }

    public bool CanHandle(string method) => method switch
    {
        "cs.getPhoneBook" or "cs.searchContacts" or "cs.getCallJournal"
        or "cs.getSpeedDials" or "cs.getVoiceMessages" or "cs.getForwardingConfig"
        or "cs.getAudioModes" or "cs.getAudioVolumes" or "cs.getUserGroups"
        or "cs.getVersionInfo" or "cs.getStatus" or "cs.reconnect"
        => true,
        _ => false
    };

    public async void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "cs.getStatus"          => GetStatus(),
                "cs.reconnect"          => await Reconnect(),
                "cs.getPhoneBook"       => await _client.GetPhoneBookEntriesAsync(),
                "cs.searchContacts"     => await _client.SearchContactsAsync(GetString(req.Params, "query") ?? ""),
                "cs.getCallJournal"     => await _client.GetCallJournalAsync(GetOptionalInt(req.Params, "part", 0)),
                "cs.getSpeedDials"      => await _client.GetSpeedDialsAsync(),
                "cs.getVoiceMessages"   => await _client.GetVoiceMessagesAsync(),
                "cs.getForwardingConfig"=> await _client.GetForwardingConfigAsync(),
                "cs.getAudioModes"      => await _client.GetAudioModesAsync(),
                "cs.getAudioVolumes"    => await _client.GetAudioVolumesAsync(),
                "cs.getUserGroups"      => await _client.GetUserGroupsAsync(),
                "cs.getVersionInfo"     => await _client.GetVersionInfoAsync(),
                _ => null
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"ComSocketHandler: {req.Method} failed: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object GetStatus()
    {
        return new
        {
            connected = _client.IsConnected,
            port = 0 // filled by Program.cs
        };
    }

    private async Task<object> Reconnect()
    {
        var port = await ComSocketClient.DiscoverPortAsync();
        if (port == 0) return new { ok = false, error = "ComSocket port not found" };

        var success = await _client.ConnectAsync(port);
        return new { ok = success, port };
    }

    private static string? GetString(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetString();
        return null;
    }

    private static int GetOptionalInt(JsonElement? p, string key, int defaultValue)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        return defaultValue;
    }
}
