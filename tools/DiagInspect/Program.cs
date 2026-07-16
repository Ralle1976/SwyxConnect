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
            }
        }

        try { Marshal.FinalReleaseComObject(clmgr); } catch { }
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
            // Try alternative: DispPlayToRtp (would need an active call though)
            Console.WriteLine("[play] (Note: DispPlayToRtp requires an active call.)");
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

        Console.WriteLine();
        Console.WriteLine("=== Did you hear the WAV (ringing tone)? ===");
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
        Console.WriteLine($"=== COM Dial Test: '{number}' ===");
        Console.WriteLine("Listen CAREFULLY: do you hear ringing? Audio? DTMF?");
        Console.WriteLine();

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
            // Fallback: DispSelectedLine.DispDial
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

        // Poll line state for 15 seconds
        for (int i = 1; i <= 15; i++)
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

        // Hangup
        try
        {
            clmgr.GetType().InvokeMember("DispHookOn",
                System.Reflection.BindingFlags.InvokeMethod, null, clmgr, null);
            Console.WriteLine("[dial] Hangup (DispHookOn) called.");
        }
        catch { }

        Console.WriteLine();
        Console.WriteLine("=== Did you hear ANYTHING? (ringing/DTMF/voice/silence) ===");
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
