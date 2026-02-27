using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Kontakt-bezogene JSON-RPC Methoden.
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
                "getPhonebook" => GetPhonebook(),
                _ => null
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { contacts = Array.Empty<object>() });
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

        // TODO: Implementierung über PbxPhoneBookEntryDisp + UserPhoneBookEntryDisp
        Logging.Info($"ContactHandler: searchContacts(query={query}) (Stub)");
        return new { query, contacts = Array.Empty<object>() };
    }

    private object GetPhonebook()
    {
        // TODO: Implementierung über COM Phonebook-Zugriff
        Logging.Info("ContactHandler: getPhonebook (Stub)");
        return new { entries = Array.Empty<object>() };
    }
}
