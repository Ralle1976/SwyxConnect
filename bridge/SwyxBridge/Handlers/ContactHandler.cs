using System.Text.Json;
using IpPbx.CLMgrLib;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Kontaktsuche via CLMgr COM Phonebook-Interface.
/// Verwendet FulltextSearchInContactsEx (typisiert) statt DispSearchPhoneBookEntries (nicht vorhanden).
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
        if (com == null) return Array.Empty<object>();

        try
        {
            // FulltextSearchInContactsEx has an 'out' parameter which doesn't work with dynamic dispatch.
            // Use Reflection/InvokeMember to call it properly on the __ComObject.
            var comObj = (object)com;
            object resultCollection;
            
            // Try 1: InvokeMember with short params (COM VARIANT_BOOL = short, -1=true, 0=false)
            try
            {
                var args = new object[] { query, (short)-1, (short)-1, (short)-1, null! };
                var paramMods = new System.Reflection.ParameterModifier[] { new(5) };
                paramMods[0][4] = true; // out param at index 4
                comObj.GetType().InvokeMember("FulltextSearchInContactsEx",
                    System.Reflection.BindingFlags.InvokeMethod, null, comObj, args, paramMods, null, null);
                resultCollection = args[4];
            }
            catch (Exception ex1)
            {
                Logging.Warn($"ContactHandler: InvokeMember(short) fehlgeschlagen: {ex1.InnerException?.Message ?? ex1.Message}");
                // Try 2: InvokeMember with int params (some COM servers accept int for VARIANT_BOOL)
                try
                {
                    var args2 = new object[] { query, 1, 1, 1, null! };
                    var paramMods2 = new System.Reflection.ParameterModifier[] { new(5) };
                    paramMods2[0][4] = true;
                    comObj.GetType().InvokeMember("FulltextSearchInContactsEx",
                        System.Reflection.BindingFlags.InvokeMethod, null, comObj, args2, paramMods2, null, null);
                    resultCollection = args2[4];
                }
                catch (Exception ex2)
                {
                    Logging.Warn($"ContactHandler: InvokeMember(int) fehlgeschlagen: {ex2.InnerException?.Message ?? ex2.Message}");
                    // Try 3: Try using SpeedDial phonebook as fallback
                    return GetContactsViaSpeedDials(com);
                }
            }
            if (resultCollection == null) return Array.Empty<object>();

            // Das Ergebnis ist eine COM-Collection — dynamic für den Zugriff auf Items,
            // da der konkrete Typ der Elemente nicht dokumentiert ist.
            dynamic collection = resultCollection;

            int count = 0;
            try { count = (int)collection.Count; } catch { }

            Logging.Info($"ContactHandler: {count} Kontakte für '{query}' gefunden (FulltextSearchInContactsEx).");

            var result = new List<object>();
            for (int i = 1; i <= count; i++)
            {
                try
                {
                    dynamic entry = collection.Item(i);
                    if (entry == null) continue;

                    result.Add(new
                    {
                        id         = SafeString(() => (string)(entry.DispEntryId    ?? i.ToString())),
                        name       = SafeString(() => (string)(entry.DispDisplayName ?? "")),
                        number     = SafeString(() => (string)(entry.DispPhoneNumber ?? "")),
                        email      = SafeString(() => (string)(entry.DispEMail       ?? "")),
                        department = SafeString(() => (string)(entry.DispDepartment  ?? "")),
                    });
                }
                catch (Exception ex)
                {
                    Logging.Warn($"ContactHandler: Eintrag {i} fehlgeschlagen: {ex.Message}");
                }
            }
            return result.ToArray();
        }
        catch (Exception ex)
        {
            Logging.Warn($"ContactHandler: COM-Suche fehlgeschlagen: {ex.Message}");
            return Array.Empty<object>();
        }
    }

    private object GetPhonebook()
    {
        return SearchContacts(null); // Leere Suche = alle Einträge
    }

    private static string SafeString(Func<string> f) { try { return f(); } catch { return ""; } }

    /// <summary>
    /// Fallback: Liest interne Kontakte über SpeedDials (immer verfügbar).
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
