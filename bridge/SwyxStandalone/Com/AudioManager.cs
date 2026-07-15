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

        // Selected device names (singular properties)
        try { handsfreePlayback = (string)(com.DispHandsfreeDevice ?? ""); } catch { }
        try { handsfreeCapture  = (string)(com.DispHandsfreeCaptureDevice ?? ""); } catch { }
        try { headsetPlayback   = (string)(com.DispHeadsetDevice ?? ""); } catch { }
        try { headsetCapture    = (string)(com.DispHeadsetCaptureDevice ?? ""); } catch { }
        try { speakerPlayback   = (string)(com.DispSpeakerDevice ?? ""); } catch { }

        // If no device is selected, try to auto-select from available collections.
        // SwyxIt! does this at startup; we replicate it here for standalone mode.
        if (string.IsNullOrEmpty(handsfreePlayback))
        {
            string autoPlayback = "";
            string autoCapture = "";
            AutoSelectAudioDevices(com, out autoPlayback, out autoCapture);
            if (!string.IsNullOrEmpty(autoPlayback))
            {
                handsfreePlayback = autoPlayback;
                handsfreeCapture = autoCapture;
                Logging.Info($"AudioManager: Auto-selected HF playback='{autoPlayback}', capture='{autoCapture}'");
            }
        }

        Logging.Info($"AudioManager: Geräte abgefragt: HF={handsfreePlayback}, HS={headsetPlayback}");

        return new
        {
            handsfree = new { playback = handsfreePlayback, capture = handsfreeCapture },
            headset   = new { playback = headsetPlayback,   capture = headsetCapture },
            speaker   = new { playback = speakerPlayback }
        };
    }

    /// <summary>
    /// Queries the Plural collection properties to enumerate all available audio devices.
    /// If devices are found, selects the first one as the default.
    /// </summary>
    private void AutoSelectAudioDevices(dynamic com, out string playback, out string capture)
    {
        playback = "";
        capture = "";

        try
        {
            // DispHandsetDevices returns a collection of available playback device names
            var playbackDevices = com.DispHandsetDevices;
            if (playbackDevices != null)
            {
                // Try to iterate the collection
                try
                {
                    var count = (int)playbackDevices.Count;
                    Logging.Info($"AudioManager: Available playback devices: {count}");
                    if (count > 0)
                    {
                        playback = (string)playbackDevices.Item(0);
                        Logging.Info($"AudioManager: Selected playback device[0]: '{playback}'");
                    }
                }
                catch
                {
                    // Maybe it's an array or comma-separated string
                    try { playback = (string)playbackDevices; } catch { }
                    if (!string.IsNullOrEmpty(playback) && playback.Contains(','))
                        playback = playback.Split(',')[0].Trim();
                }
            }
        }
        catch (Exception ex) { Logging.Warn($"AudioManager: DispHandsetDevices failed: {ex.Message}"); }

        try
        {
            // DispHandsfreeCaptureDevices returns capture device names
            var captureDevices = com.DispHandsfreeCaptureDevices;
            if (captureDevices != null)
            {
                try
                {
                    var count = (int)captureDevices.Count;
                    Logging.Info($"AudioManager: Available capture devices: {count}");
                    if (count > 0)
                    {
                        capture = (string)captureDevices.Item(0);
                        Logging.Info($"AudioManager: Selected capture device[0]: '{capture}'");
                    }
                }
                catch
                {
                    try { capture = (string)captureDevices; } catch { }
                    if (!string.IsNullOrEmpty(capture) && capture.Contains(','))
                        capture = capture.Split(',')[0].Trim();
                }
            }
        }
        catch (Exception ex) { Logging.Warn($"AudioManager: DispHandsfreeCaptureDevices failed: {ex.Message}"); }

        // If we found devices, set them as the selected device
        if (!string.IsNullOrEmpty(playback))
        {
            try
            {
                com.DispHandsfreeDevice = playback;
                Logging.Info($"AudioManager: Set DispHandsfreeDevice='{playback}'");
            }
            catch (Exception ex) { Logging.Warn($"AudioManager: Set HF playback failed: {ex.Message}"); }
        }
        if (!string.IsNullOrEmpty(capture))
        {
            try
            {
                com.DispHandsfreeCaptureDevice = capture;
                Logging.Info($"AudioManager: Set DispHandsfreeCaptureDevice='{capture}'");
            }
            catch (Exception ex) { Logging.Warn($"AudioManager: Set HF capture failed: {ex.Message}"); }
        }

        return;
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
