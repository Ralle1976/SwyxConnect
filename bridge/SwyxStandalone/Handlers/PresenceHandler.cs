using System.Text.Json;
using SwyxStandalone.Com;
using SwyxStandalone.JsonRpc;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Handlers;

public sealed class PresenceHandler
{
    private readonly StandaloneConnector _connector;
    private bool _appearanceNotificationsEnabled;

    private const uint CMD_PRESENCE_STATUS = 131;
    private const uint CMD_STATUS_TEXT      = 132;

    private const uint PRESENCE_AVAILABLE = 0;
    private const uint PRESENCE_AWAY      = 1;
    private const uint PRESENCE_DND       = 2;
    private const uint PRESENCE_OFFLINE   = 3;

    public PresenceHandler(StandaloneConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method switch
    {
        "getPresence" or "setPresence" or "getColleaguePresence" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "getPresence"          => GetOwnPresence(),
                "setPresence"          => SetOwnPresence(req.Params),
                "getColleaguePresence" => GetColleaguePresence(),
                _ => null
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"PresenceHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object GetOwnPresence()
    {
        var com = _connector.GetCom();
        if (com == null) return new { status = "unknown" };

        try
        {
            uint stateCode = (uint)com.DispSkinGetActionAreaState(CMD_PRESENCE_STATUS, 0);
            string status = MapStateCodeToString(stateCode);
            Logging.Info($"PresenceHandler: GetOwnPresence → {status} (code={stateCode})");
            return new { status };
        }
        catch (Exception ex)
        {
            Logging.Warn($"PresenceHandler: DispSkinGetActionAreaState fehlgeschlagen: {ex.Message}");
            return new { status = "Available" };
        }
    }

    private object SetOwnPresence(JsonElement? p)
    {
        if (p == null)
            return new { ok = false, error = "Missing parameters" };

        string statusStr = "Available";
        if (p.Value.TryGetProperty("status", out var statusProp))
            statusStr = statusProp.GetString() ?? "Available";

        uint statusCode = MapStringToStatusCode(statusStr);

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            Logging.Info($"PresenceHandler: Setze Presence auf '{statusStr}' (code={statusCode})");
            com.DispSkinPhoneCommand(CMD_PRESENCE_STATUS, statusCode);
            return new { ok = true, status = statusStr };
        }
        catch (Exception ex)
        {
            Logging.Error($"PresenceHandler: DispSkinPhoneCommand fehlgeschlagen: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object GetColleaguePresence()
    {
        var com = _connector.GetCom();
        if (com == null)
            return new { colleagues = Array.Empty<object>() };

        if (!_appearanceNotificationsEnabled)
        {
            try
            {
                com.EnableNotifyUserAppearanceChanged();
                _appearanceNotificationsEnabled = true;
                Logging.Info("PresenceHandler: UserAppearance-Benachrichtigungen aktiviert.");
            }
            catch (Exception ex)
            {
                Logging.Warn($"PresenceHandler: EnableNotifyUserAppearanceChanged fehlgeschlagen: {ex.Message}");
            }
        }

        try
        {
            dynamic? appearances = com.GetUserAppearances();
            if (appearances == null)
                return new { colleagues = Array.Empty<object>() };

            var colleagues = new List<object>();

            try
            {
                int count = 0;
                try { count = (int)appearances.Count; }
                catch { try { count = (int)appearances.DispCount; } catch { } }

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic item = appearances.Item(i);
                        string userId    = "";
                        string name      = "";
                        string status    = "unknown";
                        string extension = "";

                        try { userId = (string)(item.UserId    ?? item.DispUserId    ?? ""); } catch { }
                        try { name   = (string)(item.Name      ?? item.DispName      ?? ""); } catch { }
                        try { extension = (string)(item.Extension ?? item.DispExtension ?? ""); } catch { }
                        try
                        {
                            int stateInt = (int)(item.State ?? item.DispState ?? 0);
                            status = MapSpeedDialStateToPresence(stateInt);
                        }
                        catch { }

                        if (!string.IsNullOrEmpty(name))
                        {
                            colleagues.Add(new
                            {
                                userId = string.IsNullOrEmpty(userId) ? $"user_{i}" : userId,
                                name,
                                status,
                                extension
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Warn($"PresenceHandler: Fehler bei Appearance[{i}]: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"PresenceHandler: Fehler beim Iterieren: {ex.Message}");
                return GetPresenceViaSpeedDials(com);
            }

            Logging.Info($"PresenceHandler: {colleagues.Count} Kollegen-Präsenzen geladen.");
            return new { colleagues };
        }
        catch (Exception ex)
        {
            Logging.Warn($"PresenceHandler: GetUserAppearances fehlgeschlagen: {ex.Message}");
            return GetPresenceViaSpeedDials(com);
        }
    }

    private object GetPresenceViaSpeedDials(dynamic com)
    {
        var colleagues = new List<object>();
        try
        {
            int numSpeedDials = (int)com.DispNumberOfSpeedDials;
            for (int i = 0; i < numSpeedDials && i < 200; i++)
            {
                try
                {
                    string name   = (string)(com.DispSpeedDialName(i)   ?? "");
                    string number = (string)(com.DispSpeedDialNumber(i) ?? "");
                    int state     = (int)com.DispSpeedDialState(i);

                    if (!string.IsNullOrEmpty(name))
                    {
                        colleagues.Add(new
                        {
                            userId = $"sd_{i}",
                            name,
                            status = MapSpeedDialStateToPresence(state),
                            extension = number
                        });
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"PresenceHandler: SpeedDial-Fallback fehlgeschlagen: {ex.Message}");
        }
        return new { colleagues };
    }

    private static string MapStateCodeToString(uint code) => code switch
    {
        PRESENCE_AVAILABLE => "Available",
        PRESENCE_AWAY      => "Away",
        PRESENCE_DND       => "DND",
        PRESENCE_OFFLINE   => "Offline",
        _                  => "Available"
    };

    private static uint MapStringToStatusCode(string status) => status.ToLowerInvariant() switch
    {
        "available"        => PRESENCE_AVAILABLE,
        "away"             => PRESENCE_AWAY,
        "busy" or "dnd"    => PRESENCE_DND,
        "offline"          => PRESENCE_OFFLINE,
        _                  => PRESENCE_AVAILABLE
    };

    private static string MapSpeedDialStateToPresence(int state) => state switch
    {
        0 => "Offline",
        1 => "Offline",
        2 => "Available",
        3 => "Busy",
        4 => "Busy",
        _ => "Offline"
    };
}
