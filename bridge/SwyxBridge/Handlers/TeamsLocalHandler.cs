using System.Text.Json;
using Microsoft.Win32;
using SwyxBridge.JsonRpc;
using SwyxBridge.Teams;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Handles Teams Local integration via the TeamsConnector library.
/// Provides JSON-RPC methods for connecting to and controlling Microsoft Teams.
/// 
/// Methods:
///   teams.local.connect         — Connect to running Teams instance
///   teams.local.disconnect      — Disconnect and cleanup
///   teams.local.getStatus       — Get current connection + presence status
///   teams.local.getAvailability — Get current availability enum value
///   teams.local.setAvailability — Set availability (New Teams 2023 only)
///   teams.local.makeCall        — Initiate an audio call
///   teams.local.getAccounts     — Detect Teams installations from registry
/// </summary>
public sealed class TeamsLocalHandler
{
    private TeamsClient? _client;

    public bool CanHandle(string method) => method switch
    {
        "teams.local.connect" or
        "teams.local.disconnect" or
        "teams.local.getStatus" or
        "teams.local.getAvailability" or
        "teams.local.setAvailability" or
        "teams.local.makeCall" or
        "teams.local.getAccounts" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "teams.local.connect" => Connect(),
                "teams.local.disconnect" => Disconnect(),
                "teams.local.getStatus" => GetStatus(),
                "teams.local.getAvailability" => GetAvailability(),
                "teams.local.setAvailability" => SetAvailability(req.Params),
                "teams.local.makeCall" => MakeCall(req.Params),
                "teams.local.getAccounts" => GetAccounts(),
                _ => null
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"TeamsLocalHandler: {req.Method} failed: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    // ─── CONNECT ─────────────────────────────────────────────────────────────

    private object Connect()
    {
        if (_client != null)
        {
            Logging.Info("TeamsLocalHandler: Already connected, reusing existing client.");
            return new { ok = true, message = "Already connected", currentUser = _client.CurrentUser };
        }

        try
        {
            _client = new TeamsClient();
            _client.CreatePresenceSubscription();

            // Wire up events to emit JSON-RPC notifications
            _client.PresenceChanged += OnPresenceChanged;
            _client.IncomingCall += OnIncomingCall;

            Logging.Info($"TeamsLocalHandler: Connected. User={_client.CurrentUser}");

            // Emit initial state
            var availability = _client.GetAvailability();
            var activity = _client.GetActivity();
            JsonRpcEmitter.EmitEvent("teamsLocalPresenceChanged", new
            {
                availability = availability.ToString(),
                availabilityCode = (int)availability,
                activity
            });

            return new
            {
                ok = true,
                connected = true,
                currentUser = _client.CurrentUser,
                availability = availability.ToString(),
                availabilityCode = (int)availability,
                activity
            };
        }
        catch (TeamsConnectorException ex)
        {
            Logging.Warn($"TeamsLocalHandler: Connect failed: {ex.Message}");
            _client = null;
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── DISCONNECT ──────────────────────────────────────────────────────────

    private object Disconnect()
    {
        if (_client == null)
            return new { ok = true, message = "Not connected" };

        _client.PresenceChanged -= OnPresenceChanged;
        _client.IncomingCall -= OnIncomingCall;
        _client.Dispose();
        _client = null;

        Logging.Info("TeamsLocalHandler: Disconnected.");
        return new { ok = true };
    }

    // ─── GET STATUS ──────────────────────────────────────────────────────────

    private object GetStatus()
    {
        if (_client == null || !_client.IsConnected)
        {
            return new
            {
                connected = false,
                availability = "Unknown",
                availabilityCode = 0,
                activity = "Unknown",
                currentUser = (string?)null
            };
        }

        var availability = _client.GetAvailability();
        var activity = _client.GetActivity();

        return new
        {
            connected = true,
            availability = availability.ToString(),
            availabilityCode = (int)availability,
            activity,
            currentUser = _client.CurrentUser
        };
    }

    // ─── GET AVAILABILITY ────────────────────────────────────────────────────

    private object GetAvailability()
    {
        if (_client == null)
            return new { availability = "Unknown", availabilityCode = 0 };

        var availability = _client.GetAvailability();
        return new
        {
            availability = availability.ToString(),
            availabilityCode = (int)availability
        };
    }

    // ─── SET AVAILABILITY ────────────────────────────────────────────────────

    private object SetAvailability(JsonElement? p)
    {
        if (_client == null)
            return new { ok = false, error = "Not connected" };

        if (p == null)
            return new { ok = false, error = "Missing parameters" };

        Availability target = Availability.Available;
        if (p.Value.TryGetProperty("availability", out var availProp))
        {
            string availStr = availProp.GetString() ?? "Available";
            if (!Enum.TryParse<Availability>(availStr, ignoreCase: true, out target))
            {
                // Try numeric value
                if (availProp.TryGetInt32(out int code))
                    target = (Availability)code;
            }
        }

        bool success = _client.SetAvailability(target);
        return new { ok = success, availability = target.ToString() };
    }

    // ─── MAKE CALL ───────────────────────────────────────────────────────────

    private object MakeCall(JsonElement? p)
    {
        if (_client == null)
            return new { ok = false, error = "Not connected" };

        if (p == null)
            return new { ok = false, error = "Missing parameters" };

        string phoneNumber = "";
        if (p.Value.TryGetProperty("phoneNumber", out var phoneProp))
            phoneNumber = phoneProp.GetString() ?? "";
        else if (p.Value.TryGetProperty("number", out var numProp))
            phoneNumber = numProp.GetString() ?? "";

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return new { ok = false, error = "phoneNumber is required" };

        bool success = _client.MakeCall(phoneNumber);
        return new { ok = success, phoneNumber };
    }

    // ─── GET ACCOUNTS ────────────────────────────────────────────────────────

    private object GetAccounts()
    {
        var accounts = new List<object>();

        try
        {
#pragma warning disable CA1416
            string? defaultApp = Registry.GetValue("HKEY_CURRENT_USER\\Software\\IM Providers", "DefaultIMApp", null) as string;

            string[] knownClients = { "Teams", "MsTeams" };
            foreach (string clientName in knownClients)
            {
                try
                {
                    int upAndRunning = (int)(Registry.GetValue(
                        $"HKEY_CURRENT_USER\\Software\\IM Providers\\{clientName}",
                        "UpAndRunning", 0) ?? 0);

                    string version = clientName == "Teams" ? "Legacy" : "New2023";
                    accounts.Add(new
                    {
                        clientName,
                        version,
                        isDefault = clientName == defaultApp,
                        isRunning = upAndRunning == 2
                    });
                }
                catch
                {
                    // Key not present — Teams version not installed
                }
            }
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logging.Warn($"TeamsLocalHandler: GetAccounts registry error: {ex.Message}");
        }

        return new { accounts };
    }

    // ─── EVENT HANDLERS ──────────────────────────────────────────────────────

    private void OnPresenceChanged(object? sender, PresenceChangedEventArgs e)
    {
        Logging.Info($"TeamsLocalHandler: Presence changed → {e.Availability} / {e.Activity}");
        JsonRpcEmitter.EmitEvent("teamsLocalPresenceChanged", new
        {
            availability = e.Availability.ToString(),
            availabilityCode = (int)e.Availability,
            activity = e.Activity
        });
    }

    private void OnIncomingCall(object? sender, IncomingCallEventArgs e)
    {
        Logging.Info($"TeamsLocalHandler: Incoming call from {e.PhoneNumber}");
        JsonRpcEmitter.EmitEvent("teamsLocalIncomingCall", new
        {
            phoneNumber = e.PhoneNumber
        });
    }
}
