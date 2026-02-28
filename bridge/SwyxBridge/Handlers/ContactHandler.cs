using System.Text.Json;
using IpPbx.CLMgrLib;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;
using SwyxBridge.Standalone;

namespace SwyxBridge.Handlers;

/// <summary>
/// Kontaktsuche via CLMgr COM Phonebook-Interface.
/// 
/// Strategie (in dieser Reihenfolge):
///   1. Typed Interface Cast → IClientLineMgrDisp.FulltextSearchInContactsEx
///   2. Fallback: PbxPhoneBookEnumerator + UserPhoneBookEnumerator via DispClientConfig
///   3. Fallback: SpeedDials (immer verfügbar wenn eingeloggt)
/// </summary>
public sealed class ContactHandler
{
    private readonly SwyxConnector _connector;

    // Cache für PbxPhoneBookEnumerator (single-use COM enumerator!)
    private static List<object>? _cachedPhonebookContacts;
    private static DateTime _cacheTimestamp = DateTime.MinValue;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    // CDS phonebook client for standalone mode (set via SetCdsClient when COM is unavailable)
    private static CdsPhonebookClient? _cdsClient;
    private static readonly object _cdsClientLock = new object();


    public ContactHandler(SwyxConnector connector)
    {
        _connector = connector;
    }
    /// <summary>Set the CDS phonebook client for fallback when COM is not available.</summary>
    public static void SetCdsClient(CdsPhonebookClient client)
    {
        lock (_cdsClientLock)
        {
            var old = _cdsClient;
            _cdsClient = client;
            old?.Dispose();
        }
    }


    public bool CanHandle(string method) => method switch
    {
        "searchContacts" or "getPhonebook" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "searchContacts" => SearchContacts(req.Params),
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
        if (com == null)
        {
            // COM not available — try CDS fallback
            return SearchViasCds(query);
        }

        // Non-empty query: use FulltextSearchInContactsEx (creates new collection each time)
        if (!string.IsNullOrEmpty(query))
        {
            // Strategy 1: Typed interface cast → FulltextSearchInContactsEx
            try
            {
                var results = SearchViaTypedInterface(com, query);
                if (results.Length > 0) return results;
            }
            catch (Exception ex)
            {
                Logging.Warn($"ContactHandler: Typed FulltextSearchInContactsEx fehlgeschlagen: {ex.Message}");
            }

            // Fallback: filter cached contacts by query string
            var cached = GetOrLoadCachedContacts(com);
            if (cached != null && cached.Count > 0)
            {
                return FilterCachedContacts(cached, query);
            }

            // Last resort: SpeedDials
            return GetContactsViaSpeedDials(com);
        }

        // Empty query: return all contacts from cache
        var allCached = GetOrLoadCachedContacts(com);
        if (allCached != null && allCached.Count > 0)
        {
            Logging.Info($"ContactHandler: {allCached.Count} Kontakte aus Cache zurückgegeben.");
            return allCached.ToArray();
        }

        // Fallback: SpeedDials
        var speedDials = GetContactsViaSpeedDials(com);
        if (speedDials.Length > 0) return speedDials;

        // Final fallback: CDS (if available)
        return SearchViasCds(query);
    }

    /// <summary>
    /// CDS fallback: search or retrieve all contacts via CdsPhonebookClient.
    /// Used when COM is unavailable (standalone mode).
    /// </summary>
    private object SearchViasCds(string query)
    {
        CdsPhonebookClient? client;
        lock (_cdsClientLock) { client = _cdsClient; }

        if (client == null)
        {
            Logging.Info("ContactHandler: CDS-Fallback nicht verfügbar (kein CdsClient).");
            return Array.Empty<object>();
        }

        try
        {
            CdsPhonebookEntry[] entries;
            if (string.IsNullOrEmpty(query))
                entries = client.GetAllEntries();
            else
                entries = client.SearchEntries(query);

            if (entries.Length == 0)
            {
                Logging.Info($"ContactHandler: CDS-Fallback: 0 Einträge für '{query}'.");
                return Array.Empty<object>();
            }

            Logging.Info($"ContactHandler: CDS-Fallback: {entries.Length} Einträge für '{query}'.");

            return entries.Select(e => (object)new
            {
                id = e.EntryId.ToString(),
                name = e.Name ?? "",
                number = e.Number ?? "",
                email = e.Email ?? "",
                department = e.Description ?? ""
            }).ToArray();
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: CDS-Fallback fehlgeschlagen: {ex.Message}");
            return Array.Empty<object>();
        }
    }

    private object GetPhonebook()
    {
        return SearchContacts(null); // Leere Suche = alle Einträge
    }

    /// <summary>
    /// Returns cached contacts or loads them from PbxPhoneBookEnumerator (single-use!).
    /// Cache auto-refreshes after CacheLifetime (5 min).
    /// </summary>
    private List<object>? GetOrLoadCachedContacts(dynamic com)
    {
        // Return cache if still valid
        if (_cachedPhonebookContacts != null && _cachedPhonebookContacts.Count > 0
            && (DateTime.UtcNow - _cacheTimestamp) < CacheLifetime)
        {
            return _cachedPhonebookContacts;
        }

        // Load fresh from COM enumerators
        try
        {
            var freshContacts = new List<object>();
            var cfgObj = com.DispClientConfig;
            if (cfgObj == null)
            {
                Logging.Warn("ContactHandler: Cache-Load: DispClientConfig ist null.");
                return _cachedPhonebookContacts; // return stale cache if available
            }

            dynamic cfg = cfgObj;

            // PBX Phonebook (Firmentelefonbuch)
            try
            {
                var pbxEnum = cfg.PbxPhoneBookEnumerator;
                if (pbxEnum != null)
                {
                    int pbxCount = ParsePhoneBookEnumerator(pbxEnum, freshContacts, "", "pbx");
                    Logging.Info($"ContactHandler: Cache-Load PbxPhoneBook: {pbxCount} Eintr\u00e4ge.");
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"ContactHandler: Cache-Load PbxPhoneBookEnumerator: {ex.Message}");
            }

            // User Phonebook (Persönliches Telefonbuch)
            try
            {
                var userEnum = cfg.UserPhoneBookEnumerator;
                if (userEnum != null)
                {
                    int userCount = ParsePhoneBookEnumerator(userEnum, freshContacts, "", "user");
                    Logging.Info($"ContactHandler: Cache-Load UserPhoneBook: {userCount} Eintr\u00e4ge.");
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"ContactHandler: Cache-Load UserPhoneBookEnumerator: {ex.Message}");
            }

            if (freshContacts.Count > 0)
            {
                _cachedPhonebookContacts = freshContacts;
                _cacheTimestamp = DateTime.UtcNow;
                Logging.Info($"ContactHandler: Cache aktualisiert mit {freshContacts.Count} Kontakten.");
            }
            else if (_cachedPhonebookContacts != null)
            {
                // Enumerator exhausted, keep stale cache
                Logging.Info("ContactHandler: Enumerator leer, verwende bestehenden Cache.");
            }

            return _cachedPhonebookContacts;
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: Cache-Load fehlgeschlagen: {ex.Message}");
            return _cachedPhonebookContacts; // return stale cache
        }
    }

    /// <summary>
    /// Filters cached contacts by query string (name, number, email).
    /// </summary>
    private object[] FilterCachedContacts(List<object> cached, string query)
    {
        string queryLower = query.ToLowerInvariant();
        var results = new List<object>();

        foreach (var item in cached)
        {
            try
            {
                // Anonymous types use reflection
                var type = item.GetType();
                string name = (type.GetProperty("name")?.GetValue(item) as string) ?? "";
                string number = (type.GetProperty("number")?.GetValue(item) as string) ?? "";
                string email = (type.GetProperty("email")?.GetValue(item) as string) ?? "";
                string department = (type.GetProperty("department")?.GetValue(item) as string) ?? "";

                bool match = name.ToLowerInvariant().Contains(queryLower)
                    || number.Contains(queryLower)
                    || email.ToLowerInvariant().Contains(queryLower)
                    || department.ToLowerInvariant().Contains(queryLower);

                if (match) results.Add(item);
            }
            catch { /* skip item */ }
        }

        Logging.Info($"ContactHandler: FilterCachedContacts('{query}'): {results.Count} von {cached.Count} Treffern.");
        return results.ToArray();
    }

    /// <summary>
    /// Force-refresh des Kontakt-Cache (z.B. bei speedDialNotification Event).
    /// </summary>
    public static void InvalidateCache()
    {
        _cachedPhonebookContacts = null;
        _cacheTimestamp = DateTime.MinValue;
        Logging.Info("ContactHandler: Cache invalidiert.");
    }

    /// <summary>
    /// Strategie 1: Cast __ComObject to typed IClientLineMgrDisp interface.
    /// FulltextSearchInContactsEx(string, int, int, int, out object) → int
    /// Parameters: searchText, bSearchInPhonebook(1=yes), bSearchInPlugins(1=yes), bSearchInNumbers(1=yes)
    /// </summary>
    private object[] SearchViaTypedInterface(dynamic com, string query)
    {
        // QueryInterface cast: __ComObject → IClientLineMgrDisp
        var typed = (IClientLineMgrDisp)com;
        object resultCollection;
        int hr = typed.FulltextSearchInContactsEx(query, 1, 1, 1, out resultCollection);
        Logging.Info($"ContactHandler: FulltextSearchInContactsEx returned hr={hr}");

        if (resultCollection == null)
        {
            Logging.Warn("ContactHandler: FulltextSearchInContactsEx returned null collection.");
            return Array.Empty<object>();
        }

        return ParseSearchResultCollection(resultCollection, query, "FulltextSearchInContactsEx");
    }

    /// <summary>
    /// Strategie 2: PbxPhoneBookEnumerator + UserPhoneBookEnumerator aus DispClientConfig.
    /// IClientConfig bietet: get_PbxPhoneBookEnumerator() → object, get_UserPhoneBookEnumerator() → object
    /// </summary>
    private object[] GetContactsViaPhoneBookEnumerators(dynamic com, string query)
    {
        var cfgObj = com.DispClientConfig;
        if (cfgObj == null)
        {
            Logging.Warn("ContactHandler: DispClientConfig ist null.");
            return Array.Empty<object>();
        }

        var contacts = new List<object>();
        string queryLower = (query ?? "").ToLowerInvariant();

        // PBX Phonebook (Firmentelefonbuch)
        try
        {
            dynamic cfg = cfgObj;
            var pbxEnum = cfg.PbxPhoneBookEnumerator;
            if (pbxEnum != null)
            {
                int pbxCount = ParsePhoneBookEnumerator(pbxEnum, contacts, queryLower, "pbx");
                Logging.Info($"ContactHandler: PbxPhoneBook: {pbxCount} Einträge.");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: PbxPhoneBookEnumerator: {ex.Message}");
        }

        // User Phonebook (Persönliches Telefonbuch)
        try
        {
            dynamic cfg = cfgObj;
            var userEnum = cfg.UserPhoneBookEnumerator;
            if (userEnum != null)
            {
                int userCount = ParsePhoneBookEnumerator(userEnum, contacts, queryLower, "user");
                Logging.Info($"ContactHandler: UserPhoneBook: {userCount} Einträge.");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: UserPhoneBookEnumerator: {ex.Message}");
        }

        Logging.Info($"ContactHandler: {contacts.Count} Kontakte via PhoneBookEnumerators.");
        return contacts.ToArray();
    }

    /// <summary>
    /// Parst eine COM PhoneBook-Collection. Erwartet Objekte mit Name/Number/Email Properties.
    /// </summary>
    private int ParsePhoneBookEnumerator(dynamic enumerator, List<object> contacts, string queryLower, string prefix)
    {
        int count = 0;
        try
        {
            // COM Enumerator — try foreach via IEnumerable
            foreach (dynamic entry in enumerator)
            {
                try
                {
                    string name = SafeString(() => (string)(entry.Name ?? ""));
                    string number = SafeString(() => (string)(entry.Number ?? ""));
                    string email = SafeString(() => (string)(entry.EMail ?? ""));

                    // Skip empty entries
                    if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(number)) continue;

                    // Filter by query if non-empty
                    if (!string.IsNullOrEmpty(queryLower))
                    {
                        bool match = (name?.ToLowerInvariant().Contains(queryLower) == true)
                            || (number?.Contains(queryLower) == true)
                            || (email?.ToLowerInvariant().Contains(queryLower) == true);
                        if (!match) continue;
                    }

                    contacts.Add(new
                    {
                        id = $"{prefix}_{count}",
                        name,
                        number,
                        email,
                        department = ""
                    });
                    count++;
                }
                catch { /* Skip individual entry errors */ }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: ParsePhoneBookEnumerator({prefix}): {ex.Message}");
            // Try index-based access as fallback
            try
            {
                int total = (int)enumerator.Count;
                for (int i = 0; i < total && i < 500; i++)
                {
                    try
                    {
                        dynamic entry = enumerator.Item(i);
                        if (entry == null) continue;

                        string name = SafeString(() => (string)(entry.Name ?? ""));
                        string number = SafeString(() => (string)(entry.Number ?? ""));

                        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(number)) continue;

                        if (!string.IsNullOrEmpty(queryLower))
                        {
                            if (!(name?.ToLowerInvariant().Contains(queryLower) == true)
                                && !(number?.Contains(queryLower) == true))
                                continue;
                        }

                        contacts.Add(new
                        {
                            id = $"{prefix}_{count}",
                            name,
                            number,
                            email = "",
                            department = ""
                        });
                        count++;
                    }
                    catch { }
                }
            }
            catch { }
        }
        return count;
    }

    /// <summary>
    /// Parst eine FulltextSearchInContactsEx Result-Collection.
    /// Items sind INameNumberSearchResult: Name, Number, Description, EntityId, SiteId, UserStatus
    /// Collection ist INameNumberSearchResultCollection: Count, Item(object index)
    /// </summary>
    private object[] ParseSearchResultCollection(object resultCollection, string query, string source)
    {
        dynamic collection = resultCollection;

        int count = 0;
        try { count = (int)collection.Count; } catch { }

        Logging.Info($"ContactHandler: {count} Kontakte für '{query}' gefunden ({source}).");

        var result = new List<object>();
        for (int i = 1; i <= count; i++)
        {
            try
            {
                dynamic entry = collection.Item(i);
                if (entry == null) continue;

                // INameNumberSearchResult properties: Name, Number, Description, EntityId, SiteId, UserStatus
                result.Add(new
                {
                    id         = SafeString(() => ((int)entry.EntityId).ToString()),
                    name       = SafeString(() => (string)(entry.Name ?? "")),
                    number     = SafeString(() => (string)(entry.Number ?? "")),
                    email      = "",
                    department = SafeString(() => (string)(entry.Description ?? "")),
                });
            }
            catch (Exception ex)
            {
                Logging.Warn($"ContactHandler: Eintrag {i} fehlgeschlagen: {ex.Message}");
            }
        }
        return result.ToArray();
    }

    private static string SafeString(Func<string> f) { try { return f(); } catch { return ""; } }

    /// <summary>
    /// Fallback 3: Liest interne Kontakte über SpeedDials (immer verfügbar wenn eingeloggt).
    /// DispSpeedDialName/Number/State liefern Kurzwahlen die typischerweise
    /// die internen Benutzer abbilden.
    /// </summary>
    private object[] GetContactsViaSpeedDials(dynamic com)
    {
        var contacts = new List<object>();
        try
        {
            int numSpeedDials = (int)com.DispNumberOfSpeedDials;
            Logging.Info($"ContactHandler: Fallback via SpeedDials ({numSpeedDials} Einträge).");

            for (int i = 0; i < numSpeedDials && i < 500; i++)
            {
                try
                {
                    string name   = (string)(((dynamic)com).DispSpeedDialName(i) ?? "");
                    string number = (string)(((dynamic)com).DispSpeedDialNumber(i) ?? "");

                    if (!string.IsNullOrEmpty(name))
                    {
                        contacts.Add(new
                        {
                            id         = $"sd_{i}",
                            name,
                            number,
                            email      = "",
                            department = ""
                        });
                    }
                }
                catch { /* SpeedDial-Index nicht verfügbar */ }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: SpeedDial-Fallback: {ex.Message}");
        }

        Logging.Info($"ContactHandler: {contacts.Count} Kontakte via SpeedDials.");
        return contacts.ToArray();
    }
}
