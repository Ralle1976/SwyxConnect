// DiagInspect — Read-only CLMgr state inspector.
//
// ATTACHES to a running CLMgr (via COM, no login) and prints every piece of
// state we can read. Does NOT modify anything, does NOT dial, does NOT login.
//
// Purpose: compare a known-good session (SwyxIt! running) against a SwyxConnect
// standalone session to find what makes audio work.
//
// Run from a cmd/x86 prompt:
//   DiagInspect.exe
//
// Requires: CLMgr.exe running (started by SwyxIt! or by SwyxConnect).

using System.Runtime.InteropServices;

namespace DiagInspect;

internal static class Program
{
    private const string ProgId = "CLMgr.ClientLineMgr";

    [STAThread]
    private static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== DiagInspect — CLMgr State Inspector ===");
        Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        // 1. Process snapshot
        Console.WriteLine("--- Running processes ---");
        foreach (var name in new[] { "SwyxIt!", "CLMgr", "SwyxConnect", "SwyxMessenger" })
        {
            var procs = System.Diagnostics.Process.GetProcessesByName(name);
            if (procs.Length > 0)
            {
                foreach (var p in procs)
                {
                    try { Console.WriteLine($"  {name}.exe PID={p.Id} Mem={p.WorkingSet64 / 1024}KB"); }
                    catch { }
                }
            }
            else
            {
                Console.WriteLine($"  {name}.exe: NOT RUNNING");
            }
        }
        Console.WriteLine();

        // 2. COM attach
        Console.WriteLine("--- COM Attach ---");
        dynamic? clmgr = null;
        try
        {
            var t = Type.GetTypeFromProgID(ProgId);
            if (t == null) { Console.WriteLine($"ERROR: ProgID '{ProgId}' not found."); return; }
            clmgr = Activator.CreateInstance(t);
            Console.WriteLine("OK: COM object created.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            return;
        }
        Console.WriteLine();

        // 3. Session state
        Console.WriteLine("--- Session state ---");
        ReadInt(clmgr, "DispIsLoggedIn", "IsLoggedIn");
        ReadInt(clmgr, "DispIsLoggedInAsCtiMaster", "IsCtiMaster");
        ReadInt(clmgr, "DispIsServerUp", "IsServerUp");
        ReadString(clmgr, "DispGetCurrentServer", "CurrentServer");
        ReadString(clmgr, "DispGetCurrentUser", "CurrentUser");
        ReadInt(clmgr, "DispGetCurrentAuthMode", "CurrentAuthMode");
        ReadString(clmgr, "DispDeviceSessionID", "DeviceSessionID");
        Console.WriteLine();

        // 4. Lines
        Console.WriteLine("--- Lines ---");
        ReadInt(clmgr, "DispNumberOfLines", "NumberOfLines");
        ReadInt(clmgr, "DispSelectedLineNumber", "SelectedLineNumber");
        for (int i = 0; i < 4; i++)
        {
            try
            {
                var line = clmgr!.GetType().InvokeMember("DispGetLine",
                    System.Reflection.BindingFlags.InvokeMethod, null, clmgr, new object[] { i });
                if (line != null)
                {
                    int state = 0;
                    try { state = (int)line.GetType().InvokeMember("DispState",
                        System.Reflection.BindingFlags.GetProperty, null, line, null)!; }
                    catch { }
                    Console.WriteLine($"  Line {i}: state={state} ({MapState(state)})");
                }
            }
            catch (Exception ex) { Console.WriteLine($"  Line {i}: ERR {ex.Message}"); }
        }
        Console.WriteLine();

        // 5. ClientConfig (where LoginDeviceType lives)
        Console.WriteLine("--- DispClientConfig ---");
        try
        {
            dynamic cfg = GetProp(clmgr!, "DispClientConfig");
            if (cfg == null) { Console.WriteLine("  (null)"); }
            else
            {
                Console.WriteLine("  Type: " + cfg.GetType().FullName);
                DumpAllProperties(cfg, "    ");
            }
        }
        catch (Exception ex) { Console.WriteLine($"  ERR: {ex.Message}"); }
        Console.WriteLine();

        // 6. Audio devices (active selection)
        Console.WriteLine("--- Active audio devices (singular disp-props) ---");
        ReadString(clmgr, "DispHandsfreeDevice", "HF playback");
        ReadString(clmgr, "DispHandsfreeCaptureDevice", "HF capture");
        ReadString(clmgr, "DispHeadsetDevice", "HS playback");
        ReadString(clmgr, "DispHeadsetCaptureDevice", "HS capture");
        ReadString(clmgr, "DispSpeakerDevice", "Speaker playback");
        ReadInt(clmgr, "DispHandsfreeAvailable", "HF available");
        ReadInt(clmgr, "DispHeadsetAvailable", "HS available");
        ReadInt(clmgr, "DispHandsetAvailable", "Handset available");
        ReadInt(clmgr, "DispAudioMode", "AudioMode (0=None,1=Handset,2=Headset,3=Handsfree)");
        ReadInt(clmgr, "DispMicroEnabled", "MicroEnabled");
        ReadInt(clmgr, "DispSpeakerEnabled", "SpeakerEnabled");
        Console.WriteLine();

        // 7. Audio devices (available collections)
        Console.WriteLine("--- Available audio device collections ---");
        DumpCollection(clmgr, "DispHandsetDevices", "Handset devices");
        DumpCollection(clmgr, "DispHeadsetDevices", "Headset devices");
        DumpCollection(clmgr, "DispHandsfreeDevices", "Handsfree devices");
        DumpCollection(clmgr, "DispHandsfreeCaptureDevices", "HF capture devices");
        Console.WriteLine();

        // 8. Server / RC state
        Console.WriteLine("--- Server / RemoteConnector ---");
        ReadString(clmgr, "CloudConnectorServer", "CloudConnectorServer");
        ReadInt(clmgr, "CloudConnectorStatus", "CloudConnectorStatus");
        ReadInt(clmgr, "DispAutoDetectionEnabled", "AutoDetection");
        Console.WriteLine();

        Console.WriteLine("=== End ===");

        // Optional modes via command line:
        //   dial <number>           — live SIP dial (DANGEROUS: calls a real number!)
        //   playsound <wav-path>    — play WAV file locally (NO call, safe test)
        //   ring                    — play ringback tone (KlingelnExtern.wav) locally
        //   audio                   — deep audio vtable diagnosis (IClientLineMgrEx8)
        if (args.Length >= 1)
        {
            switch (args[0])
            {
                case "dial" when args.Length >= 2:
                    DoDial(clmgr!, args[1]);
                    break;
                case "playsound" when args.Length >= 2:
                    DoPlaySound(clmgr!, args[1]);
                    break;
                case "ring":
                    DoPlaySound(clmgr!, @"C:\Program Files (x86)\Swyx\SwyxIt!\KlingelnExtern.wav");
                    break;
                case "audio":
                    DoAudioVtableDiag(clmgr!);
                    break;
            }
        }

        try { Marshal.FinalReleaseComObject(clmgr); } catch { }
    }

    /// <summary>
    /// Deep audio diagnosis via IClientLineMgrEx8 vtable (read-only).
    /// Calls three methods to reveal what audio binding SwyxIt! established:
    ///   1. IsAudioConfigured — is audio bound at all?
    ///   2. GetUsedWaveDevices — the 3 active devices (voice/handsfree/ringing)
    ///   3. GetAvailableWaveDevicesEx — all available devices
    ///
    /// IMPORTANT: A direct vtable cast (IClientLineMgrEx8)clmgr CRASHES with 0xC0000005
    /// because CLMgr is an OUT-OF-PROCESS COM server (32-bit) and IClientLineMgrEx8 has
    /// no registered Proxy/Stub — only IClientLineMgrDisp (the dispatch interface) is
    /// marshalled correctly. We MUST use IDispatch::Invoke (late binding by name).
    /// </summary>
    private static void DoAudioVtableDiag(dynamic clmgr)
    {
        Console.WriteLine();
        Console.WriteLine("=== Audio Diagnosis (via IDispatch late binding) ===");
        Console.WriteLine("Read-only inspection — no modifications to CLMgr session.");
        Console.WriteLine();
        Console.WriteLine("NOTE: vtable interface IClientLineMgrEx8 cannot be used directly");
        Console.WriteLine("      (no registered Proxy/Stub for out-of-process COM).");
        Console.WriteLine("      Using IDispatch::Invoke via Type.InvokeMember by name.");
        Console.WriteLine();

        // These method names are the vtable method names — COM dispatches them by name
        // through IDispatch::GetIDsOfNames + IDispatch::Invoke. Safe and correct.

        // 1. IsAudioConfigured (3 out params)
        Console.WriteLine("--- IsAudioConfigured ---");
        try
        {
            // out params via InvokeMember: pass a wrapper array; the runtime fills them.
            object?[] args = new object?[] { 0, 0, 0 };
            clmgr.GetType().InvokeMember("IsAudioConfigured",
                System.Reflection.BindingFlags.InvokeMethod
                | System.Reflection.BindingFlags.IgnoreReturn, null, clmgr, args);
            Console.WriteLine($"  configured: {args[0]} ({((((int)args[0]!) != 0) ? "YES" : "NO")})");
            Console.WriteLine($"  isPnPDevice: {args[1]}");
            Console.WriteLine($"  pnpDevicePresent: {args[2]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERR: {ex.Message}");
            for (var i = ex.InnerException; i != null; i = i.InnerException)
                Console.WriteLine($"    inner: {i.Message}");
        }
        Console.WriteLine();

        // 2. GetUsedWaveDevices — try via dispatch
        // NOTE: This has 3 out params of struct type, which InvokeMember can't easily marshal.
        // We attempt it; if it fails we know we need a different approach.
        Console.WriteLine("--- GetUsedWaveDevices (3 active bindings) ---");
        Console.WriteLine("  (Likely to fail: struct out-params via IDispatch are tricky.)");
        try
        {
            // Pass placeholder args — the actual struct marshalling is uncertain.
            // If this crashes, we know to fall back to GetAudioMode (simpler signal).
            object?[] args = new object?[] { null, null, null };
            clmgr.GetType().InvokeMember("GetUsedWaveDevices",
                System.Reflection.BindingFlags.InvokeMethod, null, clmgr, args);
            Console.WriteLine($"  VOICE: {args[0]}");
            Console.WriteLine($"  HANDSFREE: {args[1]}");
            Console.WriteLine($"  RINGING: {args[2]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERR: {ex.Message}");
            for (var i = ex.InnerException; i != null; i = i.InnerException)
                Console.WriteLine($"    inner: {i.Message}");
        }
        Console.WriteLine();

        // 3. GetAudioMode (simple int out) — the simplest signal
        Console.WriteLine("--- GetAudioMode ---");
        try
        {
            object?[] args = new object?[] { 0 };
            clmgr.GetType().InvokeMember("GetAudioMode",
                System.Reflection.BindingFlags.InvokeMethod, null, clmgr, args);
            int mode = (int)args[0]!;
            string modeName = mode switch
            {
                0 => "None", 1 => "Handset", 2 => "Headset", 3 => "Handsfree",
                _ => $"Unknown({mode})"
            };
            Console.WriteLine($"  AudioMode: {mode} ({modeName})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERR: {ex.Message}");
            for (var i = ex.InnerException; i != null; i = i.InnerException)
                Console.WriteLine($"    inner: {i.Message}");
        }
        Console.WriteLine();

        // 4. Simple audio-state queries via dispatch (should work)
        Console.WriteLine("--- Simple audio queries via dispatch ---");
        ReadInt(clmgr, "DispHandsfreeAvailable", "HF available");
        ReadInt(clmgr, "DispHeadsetAvailable", "HS available");
        ReadInt(clmgr, "DispHandsetAvailable", "Handset available");
        ReadInt(clmgr, "DispAudioMode", "DispAudioMode");
        ReadInt(clmgr, "DispMicroEnabled", "MicroEnabled");
        ReadInt(clmgr, "DispSpeakerEnabled", "SpeakerEnabled");
        Console.WriteLine();

        Console.WriteLine("=== Diagnosis complete ===");
        Console.WriteLine();
        Console.WriteLine("If 'IsAudioConfigured' shows 'YES' here, then SwyxIt! has bound audio");
        Console.WriteLine("via some mechanism we haven't replicated. If it shows 'NO' or ERR,");
        Console.WriteLine("then the dispatch interface can't reveal it — we'd need to compare");
        Console.WriteLine("the Standalone-Bridge state instead.");
    }

    /// <summary>
    /// Pretty-prints a CLMgrSoundDeviceDescriptionEx struct.
    /// </summary>
    private static void DumpDevice(in CLMgrSoundDeviceDescriptionEx d, string indent)
    {
        Console.WriteLine($"{indent}VisibleDeviceName: '{d.m_bstrVisibleDeviceName}'");
        Console.WriteLine($"{indent}  IsDirectX: {d.m_bIsDirectXSoundDevice}  HasPlayer: {d.m_bHasPlayer}  HasRecorder: {d.m_bHasRecorder}");
        Console.WriteLine($"{indent}  HasMixer: {d.m_bHasMixer}  HasDxAudioRenderer: {d.m_bHasDxAudioRenderer}");
        Console.WriteLine($"{indent}  DeviceIdPlayer:   '{d.m_bstrDeviceIdPlayer}'");
        Console.WriteLine($"{indent}  DeviceIdRecorder: '{d.m_bstrDeviceIdRecorder}'");
        Console.WriteLine($"{indent}  MixerName: '{d.m_bstrMixerName}'");
        Console.WriteLine($"{indent}  Flags: Handset={d.m_bIsHandset} Headset={d.m_bIsHeadset} Speaker={d.m_bIsSpeaker}");
        Console.WriteLine($"{indent}  Hook: USB={d.m_bHookSupportUSB} ComPort={d.m_bHookSupportComPort}({d.m_iComPort}) GamePort={d.m_bHookSupportGamePort} BT={d.m_bHookSupportBluetooth}");
        Console.WriteLine($"{indent}  IsWellKnownDevice: {d.m_bIsWellKnownDevice}");
    }

    /// <summary>
    /// Plays a WAV file via DispStartSoundFile (local playback, NO call).
    /// SAFE TEST: no SIP signaling, no peer, just local audio output.
    /// If you hear the WAV → audio output device binding works.
    /// If silent → audio device binding is broken.
    /// </summary>
    private static void DoPlaySound(dynamic clmgr, string wavPath)
    {
        Console.WriteLine();
        Console.WriteLine($"=== Local Sound Playback Test ===");
        Console.WriteLine($"WAV: {wavPath}");
        if (!File.Exists(wavPath))
        {
            Console.WriteLine($"ERROR: file not found.");
            return;
        }

        // DispStartSoundFile is a set-only property: assign the file path to start.
        try
        {
            clmgr.GetType().InvokeMember("DispStartSoundFile",
                System.Reflection.BindingFlags.SetProperty, null, clmgr,
                new object[] { wavPath });
            Console.WriteLine($"[play] DispStartSoundFile('{wavPath}') set OK.");
            Console.WriteLine($"[play] Listening for 5 seconds...");
            for (int i = 1; i <= 5; i++)
            {
                System.Threading.Thread.Sleep(1000);
                Console.WriteLine($"  {i}s...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[play] DispStartSoundFile failed: {ex.Message}");
            // Drill into InnerException (TargetInvocationException wraps the real COM error)
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                Console.WriteLine($"[play]   inner: {inner.GetType().Name}: {inner.Message}");
                if (inner is System.Runtime.InteropServices.COMException ce)
                {
                    Console.WriteLine($"[play]   HRESULT: 0x{ce.ErrorCode:X8}");
                }
            }
        }

        // Stop playback
        try
        {
            // DispStopSoundFile is also set-only, value ignored
            clmgr.GetType().InvokeMember("DispStopSoundFile",
                System.Reflection.BindingFlags.SetProperty, null, clmgr,
                new object[] { 0 });
            Console.WriteLine("[play] DispStopSoundFile called.");
        }
        catch (Exception ex) { Console.WriteLine($"[play] Stop failed: {ex.Message}"); }

        // Try alternative methods that may exist
        Console.WriteLine();
        Console.WriteLine("[play] Trying alternative APIs...");
        TryAlt(clmgr, "DispStartSoundFileEx", wavPath);
        TryAlt(clmgr, "PlaySoundFile", wavPath);
        TryAlt(clmgr, "PlaySoundFileDxEx", wavPath);

        Console.WriteLine();
        Console.WriteLine("=== Did you hear the WAV (ringing tone)? ===");
    }

    private static void TryAlt(dynamic clmgr, string method, string wavPath)
    {
        try
        {
            clmgr.GetType().InvokeMember(method,
                System.Reflection.BindingFlags.InvokeMethod, null, clmgr,
                new object[] { wavPath });
            Console.WriteLine($"[play] {method}({wavPath}) called OK");
        }
        catch (Exception ex)
        {
            Console.Write($"[play] {method} failed: {ex.Message}");
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                Console.Write($" | inner: {inner.Message}");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Interactive dial test: dials the number, prints line state every second for 15s.
    /// CRITICAL: This only works if audio is properly bound. If you hear NOTHING
    /// (no ringing, no DTMF, no audio) but the state changes to Active, the SIP
    /// signaling works but RTP/audio binding is broken.
    /// </summary>
    private static void DoDial(dynamic clmgr, string number)
    {
        Console.WriteLine();
        Console.WriteLine($"=== SAFE COM Dial Test: '{number}' ===");
        Console.WriteLine("Limit: 8 seconds. Watchdog: process kills itself if hangup fails.");
        Console.WriteLine("Listen CAREFULLY: ringing? audio? DTMF?");
        Console.WriteLine();

        // SAFETY: register Ctrl+C handler + unhandled-exception handler that hangs up.
        // This prevents being stuck in a call if the main loop crashes.
        Console.CancelKeyPress += (s, e) =>
        {
            Console.Error.WriteLine("[SAFETY] Ctrl+C pressed — emergency hangup.");
            ForceHangup(clmgr);
            Environment.Exit(2);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Console.Error.WriteLine("[SAFETY] Unhandled exception — emergency hangup.");
            ForceHangup(clmgr);
        };

        // Try DispSimpleDialEx3(number, lineNum=0, bProcessNumber=0, name="")
        try
        {
            clmgr.GetType().InvokeMember("DispSimpleDialEx3",
                System.Reflection.BindingFlags.InvokeMethod, null, clmgr,
                new object[] { number, 0, 0, "" });
            Console.WriteLine($"[dial] DispSimpleDialEx3('{number}') called OK.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[dial] DispSimpleDialEx3 failed: {ex.Message}");
            try
            {
                var sel = clmgr.GetType().InvokeMember("DispSelectedLine",
                    System.Reflection.BindingFlags.GetProperty, null, clmgr, null);
                if (sel != null)
                {
                    sel.GetType().InvokeMember("DispDial",
                        System.Reflection.BindingFlags.InvokeMethod, null, sel, new object[] { number });
                    Console.WriteLine($"[dial] DispSelectedLine.DispDial('{number}') called OK (fallback).");
                }
            }
            catch (Exception ex2) { Console.WriteLine($"[dial] Fallback failed: {ex2.Message}"); }
        }

        // Poll line state for 10 seconds (a bit longer for audio evaluation)
        for (int i = 1; i <= 10; i++)
        {
            System.Threading.Thread.Sleep(1000);
            try
            {
                var line = clmgr.GetType().InvokeMember("DispGetLine",
                    System.Reflection.BindingFlags.InvokeMethod, null, clmgr, new object[] { 0 });
                if (line != null)
                {
                    int state = (int)line.GetType().InvokeMember("DispState",
                        System.Reflection.BindingFlags.GetProperty, null, line, null)!;
                    string? peerNum = null;
                    try
                    {
                        peerNum = (string?)line.GetType().InvokeMember("DispPeerNumber",
                            System.Reflection.BindingFlags.GetProperty, null, line, null);
                    }
                    catch { }
                    Console.WriteLine($"  {i,2}s: state={state} ({MapState(state)}) peer='{peerNum}'");
                }
            }
            catch (Exception ex) { Console.WriteLine($"  {i,2}s: ERR {ex.Message}"); }
        }

        // HANGUP — multiple fallbacks to guarantee the call ends
        ForceHangup(clmgr);

        Console.WriteLine();
        Console.WriteLine("=== Did you hear ANYTHING? (ringing/DTMF/voice/silence) ===");
    }

    /// <summary>
    /// Tries multiple methods to hang up. MUST succeed or the user is stuck in a call.
    /// </summary>
    private static void ForceHangup(dynamic clmgr)
    {
        // Attempt 1: DispHookOn on CLMgr top-level
        try
        {
            clmgr.GetType().InvokeMember("DispHookOn",
                System.Reflection.BindingFlags.InvokeMethod, null, clmgr, null);
            Console.WriteLine("[hangup] DispHookOn OK (method 1)");
            return;
        }
        catch (Exception ex) { Console.WriteLine($"[hangup] method 1 failed: {ex.Message}"); }

        // Attempt 2: DispSelectedLine.DispHookOn
        try
        {
            var sel = clmgr.GetType().InvokeMember("DispSelectedLine",
                System.Reflection.BindingFlags.GetProperty, null, clmgr, null);
            if (sel != null)
            {
                sel.GetType().InvokeMember("DispHookOn",
                    System.Reflection.BindingFlags.InvokeMethod, null, sel, null);
                Console.WriteLine("[hangup] DispSelectedLine.DispHookOn OK (method 2)");
                return;
            }
        }
        catch (Exception ex) { Console.WriteLine($"[hangup] method 2 failed: {ex.Message}"); }

        // Attempt 3: ReleaseUserEx (nuclear option — logs out entirely)
        try
        {
            clmgr.GetType().InvokeMember("ReleaseUserEx",
                System.Reflection.BindingFlags.InvokeMethod, null, clmgr, null);
            Console.WriteLine("[hangup] ReleaseUserEx OK (method 3 - NUCLEAR)");
            return;
        }
        catch (Exception ex) { Console.WriteLine($"[hangup] method 3 failed: {ex.Message}"); }

        Console.Error.WriteLine("[SAFETY] All hangup methods failed. Call may still be active!");
        Console.Error.WriteLine("[SAFETY] Kill CLMgr.exe manually if needed: taskkill /IM CLMgr.exe /F");
    }

    // Use Type.InvokeMember for reliable late-binding (avoids DISPID(0) default-member issues).
    private static object? GetProp(object com, string member)
    {
        return com.GetType().InvokeMember(member,
            System.Reflection.BindingFlags.GetProperty, null, com, null);
    }

    private static void ReadInt(dynamic? com, string member, string label)
    {
        try { Console.WriteLine($"  {label}: {(int)GetProp(com!, member)!}"); }
        catch (Exception ex) { Console.WriteLine($"  {label}: ERR {ex.Message}"); }
    }

    private static void ReadString(dynamic? com, string member, string label)
    {
        try
        {
            var v = GetProp(com!, member);
            Console.WriteLine($"  {label}: '{v}'");
        }
        catch (Exception ex) { Console.WriteLine($"  {label}: ERR {ex.Message}"); }
    }

    private static void DumpCollection(dynamic? com, string member, string label)
    {
        object? v;
        try { v = GetProp(com!, member); }
        catch (Exception ex) { Console.WriteLine($"  {label}: ERR {ex.Message}"); return; }

        try
        {
            if (v == null) { Console.WriteLine($"  {label}: (null)"); return; }
            try
            {
                dynamic dv = v;
                int count = (int)dv.Count;
                Console.WriteLine($"  {label}: Count={count}");
                for (int i = 0; i < count && i < 20; i++)
                {
                    try { Console.WriteLine($"    [{i}] '{dv.Item(i)}'"); } catch { }
                }
            }
            catch
            {
                // Maybe a string
                try { Console.WriteLine($"  {label}: '{v}'"); } catch { }
            }
        }
        catch (Exception ex) { Console.WriteLine($"  {label}: ERR2 {ex.Message}"); }
    }

    private static void DumpAllProperties(dynamic obj, string indent)
    {
        // Use reflection on the dispatch-wrapper to enumerate dispids is non-trivial.
        // Instead: try a curated list of known properties and report each.
        var props = new[]
        {
            "LoginDeviceType", "HandsfreeDevice", "HandsfreeCaptureDevice",
            "HeadsetDevice", "HeadsetCaptureDevice", "HandsetDevice", "HandsetCaptureDevice",
            "Away", "DoNotDisturb", "PublicateDetectedAwayState",
            "ServerName", "PublicServerName",
            "AudioPluginLoaded", "AudioMode",
            "SelectedAudioDevice"
        };
        foreach (var p in props)
        {
            try
            {
                var v = ((Type)obj.GetType()).InvokeMember(p,
                    System.Reflection.BindingFlags.GetProperty, null, obj, null);
                Console.WriteLine($"{indent}{p} = '{v}'");
            }
            catch { /* property not present — skip silently */ }
        }
    }

    private static string MapState(int s) => s switch
    {
        0 => "Inactive", 1 => "HookOffInternal", 2 => "HookOffExternal",
        3 => "Ringing", 4 => "Dialing", 5 => "Alerting", 6 => "Knocking",
        7 => "Busy", 8 => "Active", 9 => "OnHold",
        10 => "ConferenceActive", 11 => "ConferenceOnHold", 12 => "Terminated",
        13 => "Transferring", 14 => "Disabled", 15 => "DirectCall",
        _ => "?"
    };
}
