using System.Text.Json;
using SwyxStandalone.Com;
using SwyxStandalone.JsonRpc;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Handlers;

public sealed class AudioHandler
{
    private readonly AudioManager _audio;

    public AudioHandler(AudioManager audio)
    {
        _audio = audio;
    }

    public bool CanHandle(string method) => method switch
    {
        "getAudioDevices" or "setAudioDevice" or "getVolume" or "setVolume" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "getAudioDevices" => _audio.GetAudioDevices(),
                "setAudioDevice"  => HandleSetAudioDevice(req.Params),
                "getVolume"       => _audio.GetVolume(),
                "setVolume"       => HandleSetVolume(req.Params),
                _ => throw new InvalidOperationException($"Unbekannte Methode: {req.Method}")
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"AudioHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    private object HandleSetAudioDevice(JsonElement? p)
    {
        if (p == null || p.Value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Parameter fehlt: { deviceType, playback, capture? }");

        string deviceType = GetString(p, "deviceType")
            ?? throw new ArgumentException("Parameter 'deviceType' fehlt. Gültig: handsfree, headset, speaker");
        string playback = GetString(p, "playback")
            ?? throw new ArgumentException("Parameter 'playback' fehlt.");
        string? capture = GetString(p, "capture");

        return _audio.SetAudioDevice(deviceType, playback, capture);
    }

    private object HandleSetVolume(JsonElement? p)
    {
        if (p == null || p.Value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Parameter fehlt: { deviceType, volume }");

        string deviceType = GetString(p, "deviceType")
            ?? throw new ArgumentException("Parameter 'deviceType' fehlt. Gültig: handsfree, headset, ring");

        if (!p.Value.TryGetProperty("volume", out var volProp))
            throw new ArgumentException("Parameter 'volume' fehlt.");

        int volume = volProp.GetInt32();
        return _audio.SetVolume(deviceType, volume);
    }

    private static string? GetString(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetString();
        return null;
    }
}
