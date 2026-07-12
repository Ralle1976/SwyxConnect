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
        "getPresence" or "setPresence" or "getColleaguePresence" or "getOwnPresence" or "getConnectionInfo" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "getPresence" or "getOwnPresence" => GetOwnPresence(),
                "setPresence"                     => SetOwnPresence(req.Params),
                "getColleaguePresence"            => GetColleaguePresence(),
                "getConnectionInfo"               => GetConnectionInfo(),
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
                        string statusText = "";
                        string extension = "";

                        // Bug #6 fix: try multiple property name variants
                        try { userId    = (string)(item.UserId    ?? item.DispUserId    ?? item.userId    ?? ""); } catch { }
                        try { name      = (string)(item.Name      ?? item.DispName      ?? item.UserName  ?? item.userName ?? ""); } catch { }
                        try { extension = (string)(item.Extension ?? item.DispExtension ?? item.Number   ?? item.DispNumber ?? ""); } catch { }
                        try
                        {
                            int stateInt = (int)(item.State ?? item.DispState ?? 0);
                            status = MapAppearanceStateToPresence(stateInt);
                        }
                        catch { }
                        // Bug #8 fix: read statusText
                        try { statusText = (string)(item.statusText ?? item.StatusText ?? item.AwayText ?? ""); } catch { }

                        if (!string.IsNullOrEmpty(name))
                        {
                            colleagues.Add(new
                            {
                                userId = string.IsNullOrEmpty(userId) ? $"user_{i}" : userId,
                                name,
                                status,
                                statusText,
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

    /// <summary>
    /// Returns connection info: server, user, extension, server-up status.
    /// </summary>
    private object GetConnectionInfo()
    {
        var com = _connector.GetCom();
        if (com == null) return new { connected = false };

        string server = "";
        string user = "";
        string extension = "";
        int serverUp = 0;
        int loggedIn = 0;

        try { server    = (string)(com.DispGetCurrentServer  ?? ""); } catch { }
        try { user      = (string)(com.DispGetCurrentUser    ?? ""); } catch { }
        try { serverUp  = (int)(com.DispIsServerUp            ?? 0); } catch { }
        try { loggedIn  = (int)(com.DispIsLoggedIn            ?? 0); } catch { }

        return new
        {
            connected = true,
            server,
            user,
            extension,
            serverUp = serverUp != 0,
            loggedIn = loggedIn != 0
        };
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

    /// <summary>
    /// Maps SpeedDial state int to presence string.
    /// VERIFIED via live diagnostics on 2026-07-12 against real SwyxWare server:
    ///   0 = empty/unused slot (no user assigned)
    ///   1 = Offline (user logged out)
    ///   2 = Available
    ///   3 = Busy / On the phone
    ///   4 = DND (Do Not Disturb)
    ///   5 = Away (was incorrectly mapped to "Busy" before!)
    /// </summary>
    private static string MapSpeedDialStateToPresence(int state) => state switch
    {
        0 => "Offline",       // empty slot
        1 => "Offline",       // user offline
        2 => "Available",     // available
        3 => "Busy",          // on the phone
        4 => "DND",           // do not disturb
        5 => "Away",          // away (CORRECTED — was "Busy")
        _ => "Offline"
    };

    /// <summary>
    /// Maps UserAppearance state int to presence string.
    /// Richer than SpeedDial — can represent Away/DND.
    /// </summary>
    private static string MapAppearanceStateToPresence(int state) => state switch
    {
        0 => "Offline",
        1 => "Available",
        2 => "Away",
        3 => "DND",
        4 => "Busy",
        5 => "Away",      // BeRightBack maps to Away
        6 => "Offline",
        _ => MapSpeedDialStateToPresence(state)
    };
}
