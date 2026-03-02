using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Kontaktsuche via CLMgr COM.
///
/// CLMgr hat KEIN DispSearchPhoneBookEntries. Stattdessen:
///   1. SpeedDials: DispNumberOfSpeedDials, DispSpeedDialName(i), DispSpeedDialNumber(i)
///      → Interne Benutzer / konfigurierte Kurzwahlen
///   2. CallerEnumerator: DispClientConfig.CallerEnumerator → Anrufliste
///   3. DispResolveNumber(number) → Name-Auflösung
///   4. UserAppearances: GetUserAppearances() → Kollegen mit Presence
/// </summary>
public sealed class ContactHandler
{
    private readonly SwyxConnector _connector;

    public ContactHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method switch
    {
        "searchContacts" or "getPhonebook" or "getContacts" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "searchContacts" or "getContacts" => SearchContacts(req.Params),
                "getPhonebook"   => GetPhonebook(),
                _                => null
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Logging.Error($"ContactHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object SearchContacts(JsonElement? p)
    {
        string query = "";
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("query", out var val))
            query = val.GetString() ?? "";

        var com = _connector.GetCom();
        if (com == null) return Array.Empty<object>();

        var allContacts = new List<object>();

        // === Quelle 1: SpeedDials (interne Benutzer + Kurzwahlen) ===
        try
        {
            int numSpeedDials = (int)com.DispNumberOfSpeedDials;
            Logging.Info($"ContactHandler: {numSpeedDials} SpeedDials verfügbar.");

            for (int i = 0; i < numSpeedDials && i < 500; i++)
            {
                try
                {
                    string name = (string)(com.DispSpeedDialName(i) ?? "");
                    string number = (string)(com.DispSpeedDialNumber(i) ?? "");

                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(number))
                        continue;

                    allContacts.Add(new
                    {
                        id = $"sd_{i}",
                        name = name.Trim(),
                        number = number.Trim(),
                        email = "",
                        department = "",
                        source = "speedDial"
                    });
                }
                catch { /* SpeedDial-Index nicht verfügbar */ }
            }

            Logging.Info($"ContactHandler: {allContacts.Count} SpeedDial-Kontakte geladen.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: SpeedDials: {ex.Message}");
        }

        // === Quelle 2: UserAppearances (Kollegen mit Presence-Info) ===
        try
        {
            dynamic? appearances = com.GetUserAppearances();
            if (appearances != null)
            {
                int appCount = 0;
                try { appCount = (int)appearances.Count; } catch { }

                int added = 0;
                for (int i = 0; i < appCount && i < 500; i++)
                {
                    try
                    {
                        dynamic item = appearances.Item(i);
                        string name = "";
                        string extension = "";

                        try { name = (string)(item.UserName ?? ""); } catch { }
                        // UserAppearance hat kein Number/Extension Property — userId für Reference
                        int userId = 0;
                        try { userId = (int)item.userId; } catch { }

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            // Prüfe ob schon als SpeedDial vorhanden (Name-Match)
                            bool alreadyExists = allContacts.Any(c =>
                            {
                                var dict = c as dynamic;
                                try { return ((string)dict.name).Equals(name.Trim(), StringComparison.OrdinalIgnoreCase); }
                                catch { return false; }
                            });

                            if (!alreadyExists)
                            {
                                allContacts.Add(new
                                {
                                    id = $"user_{userId}",
                                    name = name.Trim(),
                                    number = extension,
                                    email = "",
                                    department = "",
                                    source = "appearance"
                                });
                                added++;
                            }
                        }
                    }
                    catch { }
                }

                if (added > 0)
                    Logging.Info($"ContactHandler: {added} weitere Kontakte aus UserAppearances.");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: UserAppearances: {ex.Message}");
        }

        // === Quelle 3: CallerEnumerator (Anrufhistorie als Kontakt-Quelle) ===
        try
        {
            dynamic cfg = com.DispClientConfig;
            if (cfg != null)
            {
                dynamic? callerEnum = cfg.CallerEnumerator;
                if (callerEnum != null)
                {
                    int callerAdded = 0;
                    try
                    {
                        foreach (dynamic caller in callerEnum)
                        {
                            try
                            {
                                string name = "";
                                string number = "";

                                try { name = (string)(caller.Name ?? ""); } catch { }
                                try { number = (string)(caller.Number ?? ""); } catch { }

                                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(number))
                                    continue;

                                // Duplikate vermeiden (Name oder Nummer bereits vorhanden)
                                bool dup = allContacts.Any(c =>
                                {
                                    var d = c as dynamic;
                                    try
                                    {
                                        string existingName = (string)d.name;
                                        string existingNumber = (string)d.number;
                                        if (!string.IsNullOrEmpty(number) && existingNumber == number) return true;
                                        if (!string.IsNullOrEmpty(name) && existingName.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
                                        return false;
                                    }
                                    catch { return false; }
                                });

                                if (!dup)
                                {
                                    int idx = 0;
                                    try { idx = (int)caller.Idx; } catch { }

                                    allContacts.Add(new
                                    {
                                        id = $"caller_{idx}",
                                        name = name.Trim(),
                                        number = number.Trim(),
                                        email = "",
                                        department = "",
                                        source = "callerHistory"
                                    });
                                    callerAdded++;
                                }
                            }
                            catch { }

                            if (callerAdded >= 200) break; // Limit
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Warn($"ContactHandler: CallerEnumerator iteration: {ex.Message}");
                    }

                    if (callerAdded > 0)
                        Logging.Info($"ContactHandler: {callerAdded} Kontakte aus Anrufhistorie.");
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: CallerEnumerator: {ex.Message}");
        }

        // === Filter nach Suchbegriff ===
        if (!string.IsNullOrWhiteSpace(query))
        {
            string q = query.ToLowerInvariant();
            allContacts = allContacts.Where(c =>
            {
                var d = c as dynamic;
                try
                {
                    string n = ((string)d.name).ToLowerInvariant();
                    string num = ((string)d.number).ToLowerInvariant();
                    return n.Contains(q) || num.Contains(q);
                }
                catch { return false; }
            }).ToList();
        }

        Logging.Info($"ContactHandler: Insgesamt {allContacts.Count} Kontakte" +
            (string.IsNullOrWhiteSpace(query) ? "" : $" für '{query}'") + ".");

        return allContacts.ToArray();
    }

    private object GetPhonebook()
    {
        return SearchContacts(null);
    }
}
