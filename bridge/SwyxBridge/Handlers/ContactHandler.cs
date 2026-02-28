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
            // FulltextSearchInContactsEx ist die korrekte typisierte Methode.
            // Parameter: searchText, bSearchInPhonebook, bSearchInPlugins, bSearchInNumbers, out resultCollection
            int hr = com.FulltextSearchInContactsEx(query, 1, 1, 1, out object resultCollection);
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
}
