using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt System-Informations- und Audio-bezogene JSON-RPC Methoden.
///
/// COM-Properties (CLMgr):
///   com.DispPublicAccessPrefix          → Amtskennzahl (vom Server!)
///   com.DispAreaCode                    → Ortsvorwahl
///   com.DispCountryCode                 → Landesvorwahl
///   com.DispInternationCallPrefix       → Internationales Vorwahlpräfix
///   com.DispLongDistanceCallPrefix      → Fernwahlpräfix
///   com.DispNumberOfLines               → Anzahl Leitungen
///   com.DispNumberOfExtensions          → Anzahl Durchwahlen
///   com.DispNumberOfSpeedDials          → Anzahl Kurzwahleinträge
///   com.DispIsServerUp                  → Server erreichbar (int != 0)
///   com.DispIsLoggedInAsCtiMaster       → CTI-Master eingeloggt (int != 0)
///   com.DispAudioMode                   → Audio-Modus (PubCLMgrAudioMode)
///   com.DispMicroEnabled                → Mikrofon aktiv (int)
///   com.DispSpeakerEnabled              → Lautsprecher aktiv (int)
///   com.DispOpenListeningAvailable      → Mithören verfügbar (int != 0)
///   com.DispOpenListening               → Mithören aktiv (int != 0)
///   com.IsMessagingAvailable()          → Messaging verfügbar (int != 0)
///   com.SwyxItVersionInfo               → Versionsinfo-Objekt
///
/// Audio-Device-Properties (Variant-Arrays):
///   com.DispHandsetDevices / DispHandsfreeDevices / DispHeadsetDevices
///   com.DispRingingDevices / DispOpenListeningDevices
/// </summary>
public sealed class SystemInfoHandler
{
    private readonly SwyxConnector _connector;

    public SystemInfoHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method switch
    {
        "getSystemInfo" or "getAudioDevices" or "setAudioMode"
        or "setMicro" or "setSpeaker" => true,
        _ => false
    };

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "getSystemInfo"   => HandleGetSystemInfo(),
                "getAudioDevices" => HandleGetAudioDevices(),
                "setAudioMode"    => HandleSetAudioMode(req.Params),
                "setMicro"        => HandleSetMicro(req.Params),
                "setSpeaker"      => HandleSetSpeaker(req.Params),
                _ => throw new InvalidOperationException($"Unbekannte Methode: {req.Method}")
            };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            Logging.Error($"SystemInfoHandler: {req.Method} fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }

    // ─── GET SYSTEM INFO ──────────────────────────────────────────────────────

    private object HandleGetSystemInfo()
    {
        var com = _connector.GetCom();
        if (com == null)
            return new { error = "COM not connected" };

        string? publicAccessPrefix = null;
        string? areaCode = null;
        string? countryCode = null;
        string? internationalPrefix = null;
        string? longDistancePrefix = null;
        int numberOfLines = 0;
        int numberOfExtensions = 0;
        int numberOfSpeedDials = 0;
        bool isServerUp = false;
        bool isCtiMaster = false;
        int audioMode = 0;
        bool microEnabled = false;
        bool speakerEnabled = false;
        bool openListeningAvailable = false;
        bool openListening = false;
        bool messagingAvailable = false;
        string? versionInfo = null;

        try { publicAccessPrefix = (string)com.DispPublicAccessPrefix; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispPublicAccessPrefix: {ex.Message}"); }

        try { areaCode = (string)com.DispAreaCode; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispAreaCode: {ex.Message}"); }

        try { countryCode = (string)com.DispCountryCode; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispCountryCode: {ex.Message}"); }

        try { internationalPrefix = (string)com.DispInternationCallPrefix; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispInternationCallPrefix: {ex.Message}"); }

        try { longDistancePrefix = (string)com.DispLongDistanceCallPrefix; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispLongDistanceCallPrefix: {ex.Message}"); }

        try { numberOfLines = (int)com.DispNumberOfLines; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispNumberOfLines: {ex.Message}"); }

        try { numberOfExtensions = (int)com.DispNumberOfExtensions; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispNumberOfExtensions: {ex.Message}"); }

        try { numberOfSpeedDials = (int)com.DispNumberOfSpeedDials; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispNumberOfSpeedDials: {ex.Message}"); }

        try { isServerUp = (int)com.DispIsServerUp != 0; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispIsServerUp: {ex.Message}"); }

        try { isCtiMaster = (int)com.DispIsLoggedInAsCtiMaster != 0; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispIsLoggedInAsCtiMaster: {ex.Message}"); }

        try { audioMode = (int)com.DispAudioMode; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispAudioMode: {ex.Message}"); }

        try { microEnabled = (int)com.DispMicroEnabled != 0; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispMicroEnabled: {ex.Message}"); }

        try { speakerEnabled = (int)com.DispSpeakerEnabled != 0; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispSpeakerEnabled: {ex.Message}"); }

        try { openListeningAvailable = (int)com.DispOpenListeningAvailable != 0; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispOpenListeningAvailable: {ex.Message}"); }

        try { openListening = (int)com.DispOpenListening != 0; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: DispOpenListening: {ex.Message}"); }

        try { messagingAvailable = (int)com.IsMessagingAvailable() != 0; }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: IsMessagingAvailable: {ex.Message}"); }

        try
        {
            var verObj = com.SwyxItVersionInfo;
            if (verObj != null)
                versionInfo = verObj.ToString();
        }
        catch (Exception ex) { Logging.Warn($"SystemInfoHandler: SwyxItVersionInfo: {ex.Message}"); }

        Logging.Info($"SystemInfoHandler: getSystemInfo → lines={numberOfLines}, serverUp={isServerUp}, audioMode={audioMode}");

        return new
        {
            publicAccessPrefix,
            areaCode,
            countryCode,
            internationalPrefix,
            longDistancePrefix,
            numberOfLines,
            numberOfExtensions,
            numberOfSpeedDials,
            isServerUp,
            isCtiMaster,
            audioMode,
            microEnabled,
            speakerEnabled,
            openListeningAvailable,
            openListening,
            messagingAvailable,
            versionInfo
        };
    }

    // ─── GET AUDIO DEVICES ────────────────────────────────────────────────────

    private object HandleGetAudioDevices()
    {
        var com = _connector.GetCom();
        if (com == null)
            return new { error = "COM not connected" };

        var handsetDevices = ReadVariantStringArray(com, "DispHandsetDevices");
        var handsfreeDDevices = ReadVariantStringArray(com, "DispHandsfreeDevices");
        var headsetDevices = ReadVariantStringArray(com, "DispHeadsetDevices");
        var ringingDevices = ReadVariantStringArray(com, "DispRingingDevices");
        var openListeningDevices = ReadVariantStringArray(com, "DispOpenListeningDevices");

        // Capture-Devices für jeden Typ
        var captureDevices = ReadVariantStringArray(com, "DispCaptureDevices");

        Logging.Info($"SystemInfoHandler: getAudioDevices → handset={handsetDevices.Length}, " +
            $"handsfree={handsfreeDDevices.Length}, headset={headsetDevices.Length}, " +
            $"ringing={ringingDevices.Length}, openListening={openListeningDevices.Length}");

        return new
        {
            handsetDevices,
            handsfreeDDevices,
            headsetDevices,
            ringingDevices,
            openListeningDevices,
            captureDevices
        };
    }

    /// <summary>
    /// Liest eine COM-Variant-Property als String-Array.
    /// Behandelt sowohl Array- als auch Collection-Varianten.
    /// </summary>
    private static string[] ReadVariantStringArray(dynamic com, string propertyName)
    {
        var result = new List<string>();

        try
        {
            object? raw = ((object)com).GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                com,
                null);

            if (raw == null)
                return Array.Empty<string>();

            // Array-Typ direkt
            if (raw is Array arr)
            {
                foreach (var item in arr)
                {
                    try
                    {
                        string? s = item?.ToString();
                        if (!string.IsNullOrEmpty(s))
                            result.Add(s);
                    }
                    catch { }
                }
                return result.ToArray();
            }

            // Dynamic: versuche Count + Item-Iteration
            dynamic variant = raw;
            int count = 0;
            try { count = (int)variant.Count; }
            catch
            {
                try { count = (int)variant.Length; } catch { }
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    string? s = variant[i]?.ToString();
                    if (!string.IsNullOrEmpty(s))
                        result.Add(s);
                }
                catch (Exception ex)
                {
                    Logging.Warn($"SystemInfoHandler: {propertyName}[{i}]: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"SystemInfoHandler: {propertyName}: {ex.Message}");
        }

        return result.ToArray();
    }

    // ─── SET AUDIO MODE ───────────────────────────────────────────────────────

    private object HandleSetAudioMode(JsonElement? p)
    {
        int mode = GetInt(p, "mode");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            // PubCLMgrAudioMode-Enum direkt als int setzen (dynamic übernimmt die Konvertierung)
            com.DispAudioMode = mode;
            Logging.Info($"SystemInfoHandler: setAudioMode mode={mode}");
            return new { ok = true, mode };
        }
        catch (Exception ex)
        {
            Logging.Warn($"SystemInfoHandler: DispAudioMode = {mode}: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── SET MICRO ────────────────────────────────────────────────────────────

    private object HandleSetMicro(JsonElement? p)
    {
        bool enabled = GetBool(p, "enabled");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.DispMicroEnabled = enabled ? 1 : 0;
            Logging.Info($"SystemInfoHandler: setMicro enabled={enabled}");
            return new { ok = true, enabled };
        }
        catch (Exception ex)
        {
            Logging.Warn($"SystemInfoHandler: DispMicroEnabled = {(enabled ? 1 : 0)}: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── SET SPEAKER ──────────────────────────────────────────────────────────

    private object HandleSetSpeaker(JsonElement? p)
    {
        bool enabled = GetBool(p, "enabled");

        var com = _connector.GetCom();
        if (com == null)
            return new { ok = false, error = "COM not connected" };

        try
        {
            com.DispSpeakerEnabled = enabled ? 1 : 0;
            Logging.Info($"SystemInfoHandler: setSpeaker enabled={enabled}");
            return new { ok = true, enabled };
        }
        catch (Exception ex)
        {
            Logging.Warn($"SystemInfoHandler: DispSpeakerEnabled = {(enabled ? 1 : 0)}: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    // ─── Param Helpers ────────────────────────────────────────────────────────

    private static int GetInt(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetInt32();
        throw new ArgumentException($"Parameter '{key}' fehlt.");
    }

    private static bool GetBool(JsonElement? p, string key)
    {
        if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var val))
            return val.GetBoolean();
        throw new ArgumentException($"Parameter '{key}' fehlt.");
    }
}
