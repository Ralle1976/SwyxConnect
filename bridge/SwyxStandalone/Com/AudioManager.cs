using SwyxStandalone.Utils;

namespace SwyxStandalone.Com;

public sealed class AudioManager
{
    private readonly StandaloneConnector _connector;

    public AudioManager(StandaloneConnector connector)
    {
        _connector = connector;
    }

    private dynamic GetCom() =>
        _connector.GetCom() ?? throw new InvalidOperationException("COM nicht verbunden.");

    public object GetAudioDevices()
    {
        var com = GetCom();
        string handsfreePlayback = "";
        string handsfreeCapture = "";
        string headsetPlayback = "";
        string headsetCapture = "";
        string speakerPlayback = "";

        try { handsfreePlayback = (string)(com.DispHandsfreeDevice ?? ""); } catch { }
        try { handsfreeCapture  = (string)(com.DispHandsfreeCaptureDevice ?? ""); } catch { }
        try { headsetPlayback   = (string)(com.DispHeadsetDevice ?? ""); } catch { }
        try { headsetCapture    = (string)(com.DispHeadsetCaptureDevice ?? ""); } catch { }
        try { speakerPlayback   = (string)(com.DispSpeakerDevice ?? ""); } catch { }

        Logging.Info($"AudioManager: Geräte abgefragt: HF={handsfreePlayback}, HS={headsetPlayback}");

        return new
        {
            handsfree = new { playback = handsfreePlayback, capture = handsfreeCapture },
            headset   = new { playback = headsetPlayback,   capture = headsetCapture },
            speaker   = new { playback = speakerPlayback }
        };
    }

    public object SetAudioDevice(string deviceType, string playback, string? capture)
    {
        var com = GetCom();

        Logging.Info($"AudioManager: SetDevice type={deviceType} playback={playback} capture={capture}");

        switch (deviceType.ToLowerInvariant())
        {
            case "handsfree":
                if (!string.IsNullOrEmpty(playback))
                    com.DispHandsfreeDevice = playback;
                if (!string.IsNullOrEmpty(capture))
                    com.DispHandsfreeCaptureDevice = capture;
                break;

            case "headset":
                if (!string.IsNullOrEmpty(playback))
                    com.DispHeadsetDevice = playback;
                if (!string.IsNullOrEmpty(capture))
                    com.DispHeadsetCaptureDevice = capture;
                break;

            case "speaker":
                if (!string.IsNullOrEmpty(playback))
                    com.DispSpeakerDevice = playback;
                break;

            default:
                throw new ArgumentException($"Unbekannter Gerätetyp: '{deviceType}'. Gültig: handsfree, headset, speaker");
        }

        return new { ok = true, deviceType, playback, capture };
    }

    public object GetVolume()
    {
        var com = GetCom();
        int handsfreeVolume = 50;
        int headsetVolume = 50;
        int ringVolume = 50;

        try { handsfreeVolume = (int)com.DispHandsfreeVolume; } catch { }
        try { headsetVolume   = (int)com.DispHeadsetVolume;   } catch { }
        try { ringVolume      = (int)com.DispRingVolume;       } catch { }

        return new { handsfree = handsfreeVolume, headset = headsetVolume, ring = ringVolume };
    }

    public object SetVolume(string deviceType, int volume)
    {
        if (volume < 0 || volume > 100)
            throw new ArgumentOutOfRangeException(nameof(volume), "Lautstärke muss zwischen 0 und 100 liegen.");

        var com = GetCom();
        Logging.Info($"AudioManager: SetVolume type={deviceType} volume={volume}");

        switch (deviceType.ToLowerInvariant())
        {
            case "handsfree":
                com.DispHandsfreeVolume = volume;
                break;
            case "headset":
                com.DispHeadsetVolume = volume;
                break;
            case "ring":
                com.DispRingVolume = volume;
                break;
            default:
                throw new ArgumentException($"Unbekannter Gerätetyp: '{deviceType}'. Gültig: handsfree, headset, ring");
        }

        return new { ok = true, deviceType, volume };
    }
}
