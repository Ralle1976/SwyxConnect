using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Aufnahme- und Sound-bezogene JSON-RPC Methoden.
///
/// COM-Methoden (via Leitung):
///   line = com.DispGetLine(int lineNumber)
///   line.DispStartRecording()                                → Aufnahme starten
///   line.DispStopRecording()                                 → Aufnahme stoppen
///   line.DispPlaySoundFile(string file, int flags, int repeat) → Sound auf Leitung abspielen
///   line.DispStopPlaySoundFile()                             → Sound auf Leitung stoppen
///
/// COM-Methoden (direkt via CLMgr):
///   com.DispPlaySoundFile(string file, int device, int repeat) → Sound auf Gerät abspielen
///   com.DispStopPlaySoundFile()                               → Sound stoppen
/// </summary>
public sealed class RecordingHandler
{
    private readonly SwyxConnector _connector;

    public RecordingHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method switch
    {
        "startRecording" or "stopRecording" or "playSound" or "stopSound" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "startRecording" => HandleStartRecording(req.Params),
                "stopRecording"  => HandleStopRecording(req.Params),
                "playSound"      => HandlePlaySound(req.Params),
                "stopSound"      => HandleStopSound(req.Params),
                _ => throw new InvalidOperationException($"Unbekannte Methode: {req.Method}")
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"RecordingHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    // ─── START RECORDING ─────────────────────────────────────────────────────

    private object HandleStartRecording(JsonElement? p)
    {
        int lineNumber = GetInt(p, "lineNumber");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            dynamic line = com.DispGetLine(lineNumber);
            line.DispStartRecording();
            Logging.Info($"RecordingHandler: startRecording lineNumber={lineNumber}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"RecordingHandler: DispStartRecording(lineNumber={lineNumber}): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── STOP RECORDING ──────────────────────────────────────────────────────

    private object HandleStopRecording(JsonElement? p)
    {
        int lineNumber = GetInt(p, "lineNumber");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            dynamic line = com.DispGetLine(lineNumber);
            line.DispStopRecording();
            Logging.Info($"RecordingHandler: stopRecording lineNumber={lineNumber}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Warn($"RecordingHandler: DispStopRecording(lineNumber={lineNumber}): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── PLAY SOUND ───────────────────────────────────────────────────────────

    /// <summary>
    /// Spielt eine Sound-Datei ab. Wenn lineNumber angegeben, wird sie über
    /// die Leitung abgespielt (line.DispPlaySoundFile), sonst direkt via CLMgr
    /// (com.DispPlaySoundFile mit device-Parameter).
    /// </summary>
    private object HandlePlaySound(JsonElement? p)
    {
        var file = GetString(p, "file")
            ?? throw new ArgumentException("Parameter 'file' fehlt.");
        int flags = GetIntOpt(p, "flags", 0);
        int repeat = GetIntOpt(p, "repeat", 0);
        int? lineNumber = GetIntOptNullable(p, "lineNumber");
        int device = GetIntOpt(p, "device", 0);

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        // Bevorzuge leitungsbasiertes Abspielen wenn lineNumber angegeben
        if (lineNumber.HasValue)
        {
            try
            {
                dynamic line = com.DispGetLine(lineNumber.Value);
                line.DispPlaySoundFile(file, flags, repeat);
                Logging.Info($"RecordingHandler: playSound (line) lineNumber={lineNumber.Value} file='{file}' flags={flags} repeat={repeat}");
                return new { ok = true, via = "line" };
            }
            catch (Exception ex)
            {
                Logging.Warn($"RecordingHandler: line.DispPlaySoundFile(lineNumber={lineNumber.Value}, '{file}'): {ex.Message}");
                // Fallback auf direkte COM-Methode
            }
        }

        // Direkt über CLMgr COM-Objekt
        try
        {
            com.DispPlaySoundFile(file, device, repeat);
            Logging.Info($"RecordingHandler: playSound (com) file='{file}' device={device} repeat={repeat}");
            return new { ok = true, via = "com" };
        }
        catch (Exception ex)
        {
            Logging.Warn($"RecordingHandler: com.DispPlaySoundFile('{file}', device={device}): {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── STOP SOUND ───────────────────────────────────────────────────────────

    /// <summary>
    /// Stoppt Sound-Wiedergabe. Wenn lineNumber angegeben, wird die leitungs-
    /// spezifische Methode verwendet, sonst die globale CLMgr-Methode.
    /// </summary>
    private object HandleStopSound(JsonElement? p)
    {
        int? lineNumber = GetIntOptNullable(p, "lineNumber");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        // Bevorzuge leitungsbasiertes Stoppen wenn lineNumber angegeben
        if (lineNumber.HasValue)
        {
            try
            {
                dynamic line = com.DispGetLine(lineNumber.Value);
                line.DispStopPlaySoundFile();
                Logging.Info($"RecordingHandler: stopSound (line) lineNumber={lineNumber.Value}");
                return new { ok = true, via = "line" };
            }
            catch (Exception ex)
            {
                Logging.Warn($"RecordingHandler: line.DispStopPlaySoundFile(lineNumber={lineNumber.Value}): {ex.Message}");
                // Fallback auf direkte COM-Methode
            }
        }

        // Direkt über CLMgr COM-Objekt
        try
        {
            com.DispStopPlaySoundFile();
            Logging.Info("RecordingHandler: stopSound (com) aufgerufen.");
            return new { ok = true, via = "com" };
        }
        catch (Exception ex)
        {
            Logging.Warn($"RecordingHandler: com.DispStopPlaySoundFile: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── Param Helpers ────────────────────────────────────────────────────────

    private static string? GetString(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetString();
        return null;
    }

    private static int GetInt(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        throw new ArgumentException($"Parameter '{key}' fehlt.");
    }

    private static int GetIntOpt(JsonElement? p, string key, int defaultValue)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        return defaultValue;
    }

    private static int? GetIntOptNullable(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        return null;
    }
}
