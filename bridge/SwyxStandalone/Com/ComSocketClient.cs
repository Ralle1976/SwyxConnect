using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Com;

/// <summary>
/// Connects to the Swyx ComSocket SignalR hub (SwyxItHub) running inside CLMgr.exe.
///
/// RE FINDINGS (2026-07-12):
///   - CLMgr loads IpPbx.Client.Plugin.ComSocket.dll as a Kestrel HTTP server
///   - Port is dynamic (discovered via netstat or GetApiPort)
///   - Auth: TCP port → PID → ProcessCache → ProcessName must be in whitelist
///   - "SwyxMessenger" is whitelisted → our bridge runs as SwyxMessenger.exe
///   - Hub path: /swyxIt (SwyxItHub with 100+ methods)
///
/// This client provides:
///   - GetPhoneBookEntries: ALL colleagues with live presence (curState)
///   - GetCallJournal: full call history with details
///   - SearchContacts: contact search
///   - GetLineInfos/GetLineDetails: real-time line state
///   - EnableEventNotifications: push events for line state changes
/// </summary>
public sealed class ComSocketClient : IDisposable
{
    private HubConnection? _connection;
    private bool _disposed;
    private bool _connecting;

    // Event port is dynamic — discovered via netstat
    private int _port;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Auto-discovers the ComSocket port by scanning CLMgr's listening ports.
    /// The Kestrel server typically binds to a high port (e.g. 12042).
    /// </summary>
    public static async Task<int> DiscoverPortAsync()
    {
        // Find CLMgr process
        var clmgrProcs = System.Diagnostics.Process.GetProcessesByName("CLMgr");
        if (clmgrProcs.Length == 0)
        {
            Logging.Warn("ComSocketClient: CLMgr not running, cannot discover port.");
            return 0;
        }

        var pid = clmgrProcs[0].Id;

        // Use netstat to find listening ports for this PID
        // The ComSocket Kestrel server responds with "Server: Kestrel" header
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        // Scan common dynamic port range for Kestrel
        var testPorts = new[] { 12042, 12043, 12044, 12045, 12046, 12047, 12048, 12049, 12050,
                                13000, 13001, 13002, 13003, 13004, 13005,
                                8080, 8081, 8082, 5000, 5001 };

        foreach (var port in testPorts)
        {
            try
            {
                // ComSocket returns 403 for unauthenticated requests, but the Server header reveals it
                var resp = await httpClient.GetAsync($"http://localhost:{port}/");
                if (resp.Headers.Server.ToString().Contains("Kestrel"))
                {
                    Logging.Info($"ComSocketClient: Found Kestrel server on port {port}");
                    return port;
                }
            }
            catch
            {
                // Port not active or connection refused — skip
            }
        }

        // Fallback: use netstat to find all TCP ports for CLMgr PID
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return 0;

            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("LISTENING")) continue;
                if (!line.Contains(pid.ToString())) continue;

                // Parse port from "  TCP    0.0.0.0:PORT    ..."
                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var localAddr = parts[1];
                var colonIdx = localAddr.LastIndexOf(':');
                if (colonIdx < 0 || colonIdx >= localAddr.Length - 1) continue;

                if (int.TryParse(localAddr.Substring(colonIdx + 1), out var port))
                {
                    // Test if this port is a Kestrel server
                    try
                    {
                        var resp = await httpClient.GetAsync($"http://localhost:{port}/");
                        if (resp.Headers.Server.ToString().Contains("Kestrel"))
                        {
                            Logging.Info($"ComSocketClient: Found Kestrel via netstat on port {port}");
                            return port;
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ComSocketClient: Port discovery via netstat failed: {ex.Message}");
        }

        Logging.Warn("ComSocketClient: Could not discover ComSocket port.");
        return 0;
    }

    /// <summary>
    /// Connects to the SwyxItHub SignalR endpoint.
    /// Must be called from within the SwyxMessenger.exe process (auth requires process name match).
    /// </summary>
    public async Task<bool> ConnectAsync(int port)
    {
        if (_connection?.State == HubConnectionState.Connected) return true;
        if (_connecting) return false;
        _connecting = true;
        _port = port;

        try
        {
            Logging.Info($"ComSocketClient: Connecting to ws://localhost:{port}/swyxIt ...");

            _connection = new HubConnectionBuilder()
                .WithUrl($"http://localhost:{port}/swyxIt")
                .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            // Register event handlers for push notifications
            _connection.On<JsonElement>("NotifyLineStateChanged", data =>
            {
                Logging.Info($"ComSocketClient: Event NotifyLineStateChanged received");
                LineStateChanged?.Invoke(data);
            });

            _connection.On<JsonElement>("NotifyLineDetailsChanged", data =>
            {
                LineDetailsChanged?.Invoke(data);
            });

            _connection.On<JsonElement>("NotifyUserDataChanged", data =>
            {
                UserDataChanged?.Invoke(data);
            });

            _connection.On<JsonElement>("SwyxServerConnectionStateChanged", data =>
            {
                ServerConnectionStateChanged?.Invoke(data);
            });

            _connection.On<JsonElement>("NotifyNotificationCallsChanged", data =>
            {
                NotificationCallsChanged?.Invoke(data);
            });

            _connection.Closed += ex =>
            {
                Logging.Warn($"ComSocketClient: Connection closed: {ex?.Message}");
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                Logging.Info($"ComSocketClient: Reconnected, id={connectionId}");
                return Task.CompletedTask;
            };

            await _connection.StartAsync();
            Logging.Info($"ComSocketClient: Connected to SwyxItHub successfully.");
            _connecting = false;

            // Enable push event notifications
            try
            {
                await _connection.InvokeAsync("EnableEventNotifications");
                Logging.Info("ComSocketClient: Event notifications enabled.");
            }
            catch (Exception ex)
            {
                Logging.Warn($"ComSocketClient: Could not enable event notifications: {ex.Message}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logging.Error($"ComSocketClient: Connection failed: {ex.Message}");
            _connecting = false;
            return false;
        }
    }

    // ─── Events ────────────────────────────────────────────────────────────

    public event Action<JsonElement>? LineStateChanged;
    public event Action<JsonElement>? LineDetailsChanged;
    public event Action<JsonElement>? UserDataChanged;
    public event Action<JsonElement>? ServerConnectionStateChanged;
    public event Action<JsonElement>? NotificationCallsChanged;

    // ─── Hub Method Wrappers ───────────────────────────────────────────────

    /// <summary>
    /// Gets all phonebook entries with live presence state.
    /// Returns ALL colleagues (not just speed dials) with curState indicating presence.
    /// curState: 0=Offline, 3=Away, 4=Busy
    /// </summary>
    public async Task<JsonElement> GetPhoneBookEntriesAsync(int startIndex = 0, int count = 500)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetPhoneBookEntries", startIndex, count, 0, 0);
    }

    /// <summary>
    /// Searches contacts by name or number.
    /// </summary>
    public async Task<JsonElement> SearchContactsAsync(string query)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("SearchContactsLikeInDialString", query);
    }

    /// <summary>
    /// Gets the full call journal with details.
    /// part: 0=All, 1=Missed, 2=Outgoing, etc.
    /// </summary>
    public async Task<JsonElement> GetCallJournalAsync(int part = 0)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetCallJournal", part);
    }

    /// <summary>
    /// Gets line infos (array of all lines with state).
    /// </summary>
    public async Task<JsonElement> GetLineInfosAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetLineInfos");
    }

    /// <summary>
    /// Gets detailed line information for all lines.
    /// </summary>
    public async Task<JsonElement> GetLineDetailsAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetLineDetails");
    }

    /// <summary>
    /// Gets the logged-in user info (userName, presenceState, siteId, userId).
    /// </summary>
    public async Task<JsonElement> GetLoggedInUserAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetLoggedInUser");
    }

    /// <summary>
    /// Gets speed dials (80 slots).
    /// </summary>
    public async Task<JsonElement> GetSpeedDialsAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetSpeedDials");
    }

    /// <summary>
    /// Gets voicemail messages.
    /// </summary>
    public async Task<JsonElement> GetVoiceMessagesAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetVoiceMessages");
    }

    /// <summary>
    /// Gets forwarding configuration.
    /// </summary>
    public async Task<JsonElement> GetForwardingConfigAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetForwardingConfig");
    }

    /// <summary>
    /// Gets audio modes (available devices).
    /// </summary>
    public async Task<JsonElement> GetAudioModesAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetAudioModes");
    }

    /// <summary>
    /// Gets audio volumes.
    /// </summary>
    public async Task<JsonElement> GetAudioVolumesAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetAudioVolumes");
    }

    /// <summary>
    /// Gets user groups (team memberships).
    /// </summary>
    public async Task<JsonElement> GetUserGroupsAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetUserGroups");
    }

    /// <summary>
    /// Gets version info (SwyxIt, ComSocket versions).
    /// </summary>
    public async Task<JsonElement> GetVersionInfoAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<JsonElement>("GetVersionInfo");
    }

    /// <summary>
    /// Dials a number on the specified line.
    /// </summary>
    public async Task DialAsync(int lineIndex, string dialString)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("Dial", lineIndex, dialString);
    }

    /// <summary>
    /// Hook off (pick up) on a line.
    /// </summary>
    public async Task HookOffAsync(int lineIndex)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("HookOff", lineIndex);
    }

    /// <summary>
    /// Hook on (hang up) on a line.
    /// </summary>
    public async Task HookOnAsync(int lineIndex)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("HookOn", lineIndex);
    }

    /// <summary>
    /// Hold a call on a line.
    /// </summary>
    public async Task HoldCallAsync(int lineIndex)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("HoldCall", lineIndex);
    }

    /// <summary>
    /// Activate (resume) a held call.
    /// </summary>
    public async Task ActivateCallAsync(int lineIndex)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("ActivateCall", lineIndex);
    }

    /// <summary>
    /// Transfer call to another line.
    /// </summary>
    public async Task TransferToLineAsync(int fromLine, int toLine)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("TransferToLine", fromLine, toLine);
    }

    /// <summary>
    /// Transfer call to a number.
    /// </summary>
    public async Task TransferToNumberAsync(int fromLine, string number)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("TransferToNumber", fromLine, number);
    }

    /// <summary>
    /// Start call recording on a line.
    /// </summary>
    public async Task StartRecordingAsync(int lineIndex)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("StartCallRecording", lineIndex);
    }

    /// <summary>
    /// Stop call recording on a line.
    /// </summary>
    public async Task StopRecordingAsync(int lineIndex)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("StopCallRecording", lineIndex);
    }

    /// <summary>
    /// Convert a phone number (e.g. add trunk prefix).
    /// </summary>
    public async Task<string> ConvertNumberAsync(string number, int numberStyle = 0)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<string>("ConvertNumber", number, numberStyle);
    }

    private void EnsureConnected()
    {
        if (_connection?.State != HubConnectionState.Connected)
            throw new InvalidOperationException("ComSocket not connected. Call ConnectAsync first.");
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Logging.Warn($"ComSocketClient: Disconnect error: {ex.Message}");
            }
            _connection = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisconnectAsync().Wait(TimeSpan.FromSeconds(3));
    }
}
