using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Präsenz-bezogene JSON-RPC Methoden.
/// 
/// WICHTIG: DispSkinPhoneCommand(131, n) ist nur ein UI-Befehl und ändert NICHT
/// den serverseitigen Presence-Status. Stattdessen verwenden wir DispClientConfig:
///
///   cfg.Away (int)          → 0=nicht abwesend, 1=abwesend
///   cfg.AwayText (string)   → Abwesenheitstext
///   cfg.DoNotDisturb (int)  → 0=aus, 1=ein
///   cfg.SetRichPresenceStatus(presenceState, flags, expiry) → Erweiterte Presence
///   cfg.GetOwnUserPresenceInfo(out state, out away, out text, out dnd, out apptText)
///   cfg.PublicateDetectedAwayState(int) → Publiziert Away-Status an Server
///   cfg.ReloadPresenceData() → Lädt Presence-Daten vom Server neu
///
/// Strategie: Wir versuchen ALLE bekannten Methoden und loggen die Ergebnisse,
/// damit wir sehen was tatsächlich funktioniert.
/// </summary>
public sealed class PresenceHandler
{
    private readonly SwyxConnector _connector;
    private bool _appearanceNotificationsEnabled;

    public PresenceHandler(SwyxConnector connector)
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
                "getPresence" => GetOwnPresence(),
                "setPresence" => SetOwnPresence(req.Params),
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

    // ─── GET OWN PRESENCE ────────────────────────────────────────────────────

    /// <summary>
    /// Liest den eigenen Presence-Status über DispClientConfig.GetOwnUserPresenceInfo.
    /// Fallback: DispClientConfig.Away / DoNotDisturb Properties.
    /// </summary>
    private object GetOwnPresence()
    {
        var com = _connector.GetCom();
        if (com == null) return new { status = "unknown", detail = "COM not connected" };

        try
        {
            dynamic cfg = com.DispClientConfig;

            // Primär: Direkte Properties lesen
            int away = 0;
            int dnd = 0;
            string awayText = "";

            try { away = (int)cfg.Away; } catch (Exception ex) { Logging.Warn($"PresenceHandler: cfg.Away lesen: {ex.Message}"); }
            try { dnd = (int)cfg.DoNotDisturb; } catch (Exception ex) { Logging.Warn($"PresenceHandler: cfg.DoNotDisturb lesen: {ex.Message}"); }
            try { awayText = (string)(cfg.AwayText ?? ""); } catch { }

            // Sekundär: GetOwnUserPresenceInfo für detaillierte Info
            int presenceState = 0, awayState = 0, dndState = 0;
            string presenceText = "", appointmentText = "";

            try
            {
                cfg.GetOwnUserPresenceInfo(
                    ref presenceState, ref awayState, ref presenceText,
                    ref dndState, ref appointmentText);

                Logging.Info($"PresenceHandler: GetOwnUserPresenceInfo → presenceState={presenceState}, " +
                    $"awayState={awayState}, dndState={dndState}, text='{presenceText}', appt='{appointmentText}'");
            }
            catch (Exception ex)
            {
                Logging.Warn($"PresenceHandler: GetOwnUserPresenceInfo: {ex.Message}");
            }

            // Status bestimmen (Priorität: DND > Away > Available)
            string status;
            if (dnd != 0 || dndState != 0)
                status = "DND";
            else if (away != 0 || awayState != 0)
                status = "Away";
            else
                status = "Available";

            Logging.Info($"PresenceHandler: GetOwnPresence → {status} (away={away}, dnd={dnd}, presenceState={presenceState})");

            return new
            {
                status,
                away,
                dnd,
                awayText,
                presenceState,
                awayState,
                dndState,
                appointmentText
            };
        }
        catch (Exception ex)
        {
            Logging.Error($"PresenceHandler: GetOwnPresence DispClientConfig: {ex.Message}");

            // Letzter Fallback: DispSkinGetActionAreaState
            try
            {
                uint stateCode = (uint)com.DispSkinGetActionAreaState(131u, 0u);
                string status = stateCode switch
                {
                    0 => "Available",
                    1 => "Away",
                    2 => "DND",
                    3 => "Offline",
                    _ => "Available"
                };
                return new { status, fallback = "SkinActionArea" };
            }
            catch
            {
                return new { status = "Available", fallback = "default" };
            }
        }
    }

    // ─── SET OWN PRESENCE ────────────────────────────────────────────────────

    /// <summary>
    /// Setzt den eigenen Presence-Status über DispClientConfig.
    /// Versucht mehrere Methoden und loggt die Ergebnisse.
    /// </summary>
    private object SetOwnPresence(JsonElement? p)
    {
        if (p == null)
            return new { ok = false, error = "Missing parameters" };

        string statusStr = "Available";
        if (p.Value.TryGetProperty("status", out var statusProp))
            statusStr = statusProp.GetString() ?? "Available";

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        Logging.Info($"PresenceHandler: === SETZE PRESENCE AUF '{statusStr}' ===");

        try
        {
            dynamic cfg = com.DispClientConfig;

            // Aktuellen Zustand vorher loggen
            try
            {
                int prevAway = (int)cfg.Away;
                int prevDnd = (int)cfg.DoNotDisturb;
                Logging.Info($"PresenceHandler: VORHER → Away={prevAway}, DND={prevDnd}");
            }
            catch (Exception ex)
            {
                Logging.Warn($"PresenceHandler: Status vorher lesen: {ex.Message}");
            }

            var results = new List<string>();
            bool success = false;

            switch (statusStr.ToLowerInvariant())
            {
                case "available":
                    success = SetAvailable(cfg, results);
                    break;

                case "away":
                    success = SetAway(cfg, results);
                    break;

                case "busy":
                case "dnd":
                    success = SetDnd(cfg, results);
                    break;

                case "offline":
                    // Offline = Away + DND oder Abmelden — wir setzen erstmal Away
                    success = SetAway(cfg, results);
                    break;

                default:
                    success = SetAvailable(cfg, results);
                    break;
            }

            // Nachher prüfen
            try
            {
                int afterAway = (int)cfg.Away;
                int afterDnd = (int)cfg.DoNotDisturb;
                Logging.Info($"PresenceHandler: NACHHER → Away={afterAway}, DND={afterDnd}");
                results.Add($"After: Away={afterAway}, DND={afterDnd}");
            }
            catch (Exception ex)
            {
                Logging.Warn($"PresenceHandler: Status nachher lesen: {ex.Message}");
            }

            Logging.Info($"PresenceHandler: === PRESENCE '{statusStr}' GESETZT (success={success}) ===");

            return new
            {
                ok = success,
                status = statusStr,
                results = results.ToArray()
            };
        }
        catch (Exception ex)
        {
            Logging.Error($"PresenceHandler: SetOwnPresence Gesamtfehler: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    /// <summary>Setzt Status auf Available (Away=0, DND=0).</summary>
    private bool SetAvailable(dynamic cfg, List<string> results)
    {
        bool anySuccess = false;

        // Methode 1: Direkte Properties
        try
        {
            cfg.Away = 0;
            results.Add("cfg.Away=0 ✓");
            anySuccess = true;
        }
        catch (Exception ex) { results.Add($"cfg.Away=0 ✗ {ex.Message}"); }

        try
        {
            cfg.DoNotDisturb = 0;
            results.Add("cfg.DoNotDisturb=0 ✓");
            anySuccess = true;
        }
        catch (Exception ex) { results.Add($"cfg.DoNotDisturb=0 ✗ {ex.Message}"); }

        try
        {
            cfg.AwayText = "";
            results.Add("cfg.AwayText='' ✓");
        }
        catch (Exception ex) { results.Add($"cfg.AwayText ✗ {ex.Message}"); }

        // Methode 2: SetRichPresenceStatus(0=Available, 0, MinDate)
        try
        {
            cfg.SetRichPresenceStatus(0, 0, DateTime.MinValue);
            results.Add("SetRichPresenceStatus(0,0,MinDate) ✓");
            anySuccess = true;
        }
        catch (Exception ex) { results.Add($"SetRichPresenceStatus ✗ {ex.Message}"); }

        // Methode 3: PublicateDetectedAwayState(0)
        try
        {
            cfg.PublicateDetectedAwayState(0);
            results.Add("PublicateDetectedAwayState(0) ✓");
        }
        catch (Exception ex) { results.Add($"PublicateDetectedAwayState ✗ {ex.Message}"); }

        // Methode 4: ReloadPresenceData
        try
        {
            cfg.ReloadPresenceData();
            results.Add("ReloadPresenceData ✓");
        }
        catch (Exception ex) { results.Add($"ReloadPresenceData ✗ {ex.Message}"); }

        return anySuccess;
    }

    /// <summary>Setzt Status auf Away.</summary>
    private bool SetAway(dynamic cfg, List<string> results)
    {
        bool anySuccess = false;

        // DND zuerst ausschalten
        try
        {
            cfg.DoNotDisturb = 0;
            results.Add("cfg.DoNotDisturb=0 ✓");
        }
        catch (Exception ex) { results.Add($"cfg.DoNotDisturb=0 ✗ {ex.Message}"); }

        // Away einschalten
        try
        {
            cfg.Away = 1;
            results.Add("cfg.Away=1 ✓");
            anySuccess = true;
        }
        catch (Exception ex) { results.Add($"cfg.Away=1 ✗ {ex.Message}"); }

        try
        {
            cfg.AwayText = "Abwesend";
            results.Add("cfg.AwayText='Abwesend' ✓");
        }
        catch (Exception ex) { results.Add($"cfg.AwayText ✗ {ex.Message}"); }

        // SetRichPresenceStatus(1=Away, 0, +1h)
        try
        {
            cfg.SetRichPresenceStatus(1, 0, DateTime.Now.AddHours(1));
            results.Add("SetRichPresenceStatus(1,0,+1h) ✓");
            anySuccess = true;
        }
        catch (Exception ex) { results.Add($"SetRichPresenceStatus ✗ {ex.Message}"); }

        // PublicateDetectedAwayState(1)
        try
        {
            cfg.PublicateDetectedAwayState(1);
            results.Add("PublicateDetectedAwayState(1) ✓");
        }
        catch (Exception ex) { results.Add($"PublicateDetectedAwayState ✗ {ex.Message}"); }

        return anySuccess;
    }

    /// <summary>Setzt Status auf DND (Nicht stören).</summary>
    private bool SetDnd(dynamic cfg, List<string> results)
    {
        bool anySuccess = false;

        // DND einschalten
        try
        {
            cfg.DoNotDisturb = 1;
            results.Add("cfg.DoNotDisturb=1 ✓");
            anySuccess = true;
        }
        catch (Exception ex) { results.Add($"cfg.DoNotDisturb=1 ✗ {ex.Message}"); }

        // SetRichPresenceStatus(2=DND, 0, +8h)
        try
        {
            cfg.SetRichPresenceStatus(2, 0, DateTime.Now.AddHours(8));
            results.Add("SetRichPresenceStatus(2,0,+8h) ✓");
            anySuccess = true;
        }
        catch (Exception ex) { results.Add($"SetRichPresenceStatus ✗ {ex.Message}"); }

        // Away auch setzen für Clients die nur Away kennen
        try
        {
            cfg.Away = 1;
            cfg.AwayText = "Nicht stören";
            results.Add("cfg.Away=1 + AwayText='Nicht stören' ✓");
        }
        catch (Exception ex) { results.Add($"Away/AwayText ✗ {ex.Message}"); }

        return anySuccess;
    }

    // ─── COLLEAGUE PRESENCE ──────────────────────────────────────────────────

    /// <summary>
    /// Liest die Kollegen-Präsenzen über GetUserAppearances().
    /// Aktiviert beim ersten Aufruf die Appearance-Benachrichtigungen.
    /// </summary>
    private object GetColleaguePresence()
    {
        var com = _connector.GetCom();
        if (com == null)
            return new { colleagues = Array.Empty<object>() };

        // Beim ersten Aufruf: Benachrichtigungen aktivieren
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
                Logging.Warn($"PresenceHandler: EnableNotifyUserAppearanceChanged: {ex.Message}");
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
                catch
                {
                    try { count = (int)appearances.DispCount; } catch { }
                }

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic item = appearances.Item(i);
                        string userId = "";
                        string name = "";
                        string status = "unknown";
                        string extension = "";

                        try { userId = (string)(item.UserId ?? item.DispUserId ?? ""); } catch { }
                        try { name = (string)(item.Name ?? item.DispName ?? ""); } catch { }
                        try
                        {
                            int stateInt = (int)(item.State ?? item.DispState ?? 0);
                            status = MapSpeedDialStateToPresence(stateInt);
                        }
                        catch { }
                        try { extension = (string)(item.Extension ?? item.DispExtension ?? ""); } catch { }

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
                        Logging.Warn($"PresenceHandler: Appearance[{i}]: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"PresenceHandler: Iterieren der Appearances: {ex.Message}");
                return GetPresenceViaSpeedDials(com);
            }

            Logging.Info($"PresenceHandler: {colleagues.Count} Kollegen-Präsenzen geladen.");
            return new { colleagues };
        }
        catch (Exception ex)
        {
            Logging.Warn($"PresenceHandler: GetUserAppearances: {ex.Message}");
            return GetPresenceViaSpeedDials(com);
        }
    }

    /// <summary>
    /// Fallback: Liest Kollegeninfo über SpeedDials (immer verfügbar).
    /// </summary>
    private object GetPresenceViaSpeedDials(dynamic com)
    {
        var colleagues = new List<object>();

        try
        {
            int numSpeedDials = (int)com.DispNumberOfSpeedDials;
            Logging.Info($"PresenceHandler: Fallback via SpeedDials ({numSpeedDials} Einträge).");

            for (int i = 0; i < numSpeedDials && i < 200; i++)
            {
                try
                {
                    string name = (string)(com.DispSpeedDialName(i) ?? "");
                    string number = (string)(com.DispSpeedDialNumber(i) ?? "");
                    int state = (int)com.DispSpeedDialState(i);

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
                catch { /* SpeedDial-Index nicht verfügbar */ }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"PresenceHandler: SpeedDial-Fallback: {ex.Message}");
        }

        return new { colleagues };
    }

    // ─── Mapping ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Mappt SpeedDial-States (PubCLMgrSpeedDialStates) auf Presence-Strings.
    /// 0=Unknown, 1=LoggedOut, 2=LoggedIn, 3=Busy, 4=GroupCallNotification
    /// </summary>
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
