using System.Text.Json;
using Microsoft.Win32;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Lightweight Teams handler — nur Registry-Check für Teams-Installationen.
/// Die eigentliche Teams-Integration (Graph API, Presence-Polling) läuft
/// komplett im TypeScript/Electron-Layer via @azure/msal-node.
///
/// C#-Bridge stellt nur noch bereit:
///   teams.local.getAccounts — Erkennt installierte Teams-Versionen via Registry
///   teams.local.getStatus   — Gibt "not_managed" zurück (wird von TS verwaltet)
///   teams.local.connect     — Gibt Hinweis zurück, dass Graph API in TS läuft
///   teams.local.disconnect  — No-op
/// </summary>
public sealed class TeamsLocalHandler
{
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
                "teams.local.connect" => new { ok = false, error = "Teams integration is managed by the Electron app via Microsoft Graph API." },
                "teams.local.disconnect" => new { ok = true },
                "teams.local.getStatus" => new { connected = false, availability = "Unknown", managedBy = "electron-graph-api" },
                "teams.local.getAvailability" => new { availability = "Unknown", managedBy = "electron-graph-api" },
                "teams.local.setAvailability" => new { ok = false, error = "Use Graph API via Electron" },
                "teams.local.makeCall" => new { ok = false, error = "Use Graph API via Electron" },
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
