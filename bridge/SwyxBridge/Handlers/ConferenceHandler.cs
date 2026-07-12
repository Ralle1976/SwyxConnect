using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Konferenz-bezogene JSON-RPC Methoden.
///
/// COM-Methoden:
///   com.DispCreateConference(int lineNumber)          → Konferenz aus Leitung erstellen
///   com.DispJoinLineToConference(int lineNumber)      → Leitung zur Konferenz hinzufügen
///   com.DispJoinAllToConference(int lineNumber)       → Alle Leitungen zur Konferenz
///   com.DispConferenceRunning                         → Konferenz läuft (bool als int)
///   com.DispConferenceLineNumber                      → Leitungsnummer der Konferenz
///   com.DispNuberOfConferenceParticipants             → Teilnehmerzahl (COM-Tippfehler!)
/// </summary>
public sealed class ConferenceHandler
{
    private readonly SwyxConnector _connector;

    public ConferenceHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method switch
    {
        "createConference" or "joinLineToConference" or "joinAllToConference"
        or "getConferenceStatus" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "createConference"      => HandleCreateConference(req.Params),
                "joinLineToConference"  => HandleJoinLineToConference(req.Params),
                "joinAllToConference"   => HandleJoinAllToConference(req.Params),
                "getConferenceStatus"   => HandleGetConferenceStatus(),
                _ => throw new InvalidOperationException($"Unbekannte Methode: {req.Method}")
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"ConferenceHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    // ─── CREATE CONFERENCE ────────────────────────────────────────────────────

    private object HandleCreateConference(JsonElement? p)
    {
        int lineNumber = GetInt(p, "lineNumber");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.DispCreateConference(lineNumber);
            Logging.Info($"ConferenceHandler: createConference lineNumber={lineNumber}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"ConferenceHandler: DispCreateConference(lineNumber={lineNumber}): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── JOIN LINE TO CONFERENCE ──────────────────────────────────────────────

    private object HandleJoinLineToConference(JsonElement? p)
    {
        int lineNumber = GetInt(p, "lineNumber");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.DispJoinLineToConference(lineNumber);
            Logging.Info($"ConferenceHandler: joinLineToConference lineNumber={lineNumber}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"ConferenceHandler: DispJoinLineToConference(lineNumber={lineNumber}): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── JOIN ALL TO CONFERENCE ───────────────────────────────────────────────

    private object HandleJoinAllToConference(JsonElement? p)
    {
        int lineNumber = GetInt(p, "lineNumber");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.DispJoinAllToConference(lineNumber);
            Logging.Info($"ConferenceHandler: joinAllToConference lineNumber={lineNumber}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"ConferenceHandler: DispJoinAllToConference(lineNumber={lineNumber}): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── GET CONFERENCE STATUS ────────────────────────────────────────────────

    private object HandleGetConferenceStatus()
    {
        var com = _connector.GetCom();
        if (com == null)
            return new { running = false, lineNumber = 0, participants = 0, error = "COM not connected" };

        bool running = false;
        int lineNumber = 0;
        int participants = 0;

        try
        {
            running = (int)com.DispConferenceRunning != 0;
        }
        catch (Exception ex)
        {
            Logging.Warn($"ConferenceHandler: DispConferenceRunning: {ex.Message}");
        }

        try
        {
            lineNumber = (int)com.DispConferenceLineNumber;
        }
        catch (Exception ex)
        {
            Logging.Warn($"ConferenceHandler: DispConferenceLineNumber: {ex.Message}");
        }

        try
        {
            // Hinweis: COM-API hat Schreibfehler "Nuber" statt "Number"
            participants = (int)com.DispNuberOfConferenceParticipants;
        }
        catch (Exception ex)
        {
            Logging.Warn($"ConferenceHandler: DispNuberOfConferenceParticipants: {ex.Message}");
        }

        Logging.Info($"ConferenceHandler: getConferenceStatus → running={running}, lineNumber={lineNumber}, participants={participants}");

        return new { running, lineNumber, participants };
    }

    // ─── Param Helpers ────────────────────────────────────────────────────────

    private static int GetInt(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        throw new ArgumentException($"Parameter '{key}' fehlt.");
    }
}
