using System.Text.Json;
using Microsoft.Win32;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Teams-Handler mit lokaler Presence-Erkennung.
///
/// Bereitgestellte Methoden:
///   teams.local.getAccounts        — Erkennt installierte Teams-Versionen via Registry
///   teams.local.getTeamsPresence   — Aktueller Teams-Status (vom Watcher)
///   teams.local.startTeamsWatch    — Startet die Log-/Process-Überwachung
///   teams.local.stopTeamsWatch     — Stoppt die Überwachung
///   teams.local.getStatus          — Kombinierter Status
///   teams.local.connect            — Startet Watcher
///   teams.local.disconnect         — Stoppt Watcher
///   teams.local.getAvailability    — Aktueller Availability-String
/// </summary>
public sealed class TeamsLocalHandler
{
    private readonly TeamsPresenceWatcher _presenceWatcher;

    public TeamsLocalHandler(TeamsPresenceWatcher presenceWatcher)
    {
        _presenceWatcher = presenceWatcher;
    }

    public bool CanHandle(string method) => method switch
    {
        "teams.local.connect" or
        "teams.local.disconnect" or
        "teams.local.getStatus" or
        "teams.local.getAvailability" or
        "teams.local.setAvailability" or
        "teams.local.makeCall" or
        "teams.local.getAccounts" or
        "teams.local.getTeamsPresence" or
        "teams.local.startTeamsWatch" or
        "teams.local.stopTeamsWatch" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "teams.local.connect" => StartWatch(),
                "teams.local.disconnect" => StopWatch(),
                "teams.local.getStatus" => _presenceWatcher.GetStatus(),
                "teams.local.getAvailability" => new { availability = _presenceWatcher.CurrentAvailability },
                "teams.local.setAvailability" => new { ok = false, error = "Lokale Teams-Erkennung ist read-only" },
                "teams.local.makeCall" => new { ok = false, error = "Nicht unterstützt über lokale Erkennung" },
                "teams.local.getAccounts" => GetAccounts(),
                "teams.local.getTeamsPresence" => _presenceWatcher.GetStatus(),
                "teams.local.startTeamsWatch" => StartWatch(),
                "teams.local.stopTeamsWatch" => StopWatch(),
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

    private object StartWatch()
    {
        if (!_presenceWatcher.IsRunning)
            _presenceWatcher.Start();
        return new { ok = true, isRunning = _presenceWatcher.IsRunning };
    }

    private object StopWatch()
    {
        if (_presenceWatcher.IsRunning)
            _presenceWatcher.Stop();
        return new { ok = true, isRunning = false };
    }

    /// <summary>
    /// Erkennt installierte Teams-Versionen via Registry.
    /// Prüft HKCU\Software\IM Providers\{Teams|MsTeams}
    /// </summary>
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
}
