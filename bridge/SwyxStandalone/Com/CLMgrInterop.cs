using System.Runtime.InteropServices;

namespace SwyxStandalone.Com;

// COM Interop interfaces for CLMgr — extracted from Interop.CLMgr.dll (decompiled 2026-07-15).
// These are the native vtable interfaces that disp-interface (dynamic) cannot reach.
// IClientLineMgr2.Init() is the critical method that triggers audio plugin loading.

// GUID: F8E552F6-4C00-11D3-80BC-00105A653379
// The real vtable API — Init/RegisterUser are here, not on the disp-interface.
[ComImport]
[Guid("F8E552F6-4C00-11D3-80BC-00105A653379")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClientLineMgr2
{
    // vtable slot 0: QueryInterface (inherited from IUnknown)
    // vtable slot 1: AddRef
    // vtable slot 2: Release
    // vtable slot 3:
    void Init(
        [In, MarshalAs(UnmanagedType.BStr)] string ServerName,
        out IClPBX ppIClPbx);

    // vtable slot 4:
    void ReInit(
        out IClPBX ppIClPbx,
        out IClUser ppIClUser);

    // vtable slot 5:
    void RegisterUser(
        [In, MarshalAs(UnmanagedType.BStr)] string PbxUserName,
        out IClUser ppIClUser,
        out uint pulUserId,
        ref uint pMaxUsers,
        [Out, MarshalAs(UnmanagedType.BStr)] out string pszPBXUser,
        out uint pNumReturned);

    // vtable slot 6:
    void RegisterSecondaryUser(
        [In, MarshalAs(UnmanagedType.BStr)] string PbxUserName,
        [In, MarshalAs(UnmanagedType.BStr)] string NtUserName,
        [In, MarshalAs(UnmanagedType.BStr)] string NtPassword,
        out IClUser ppIClUser,
        out uint pulUserId);

    // vtable slot 7:
    void ReleaseUser([In] uint ulUserId);

    // vtable slot 8:
    void GetLineManagerType(out SClLineManagerType pLineManagerType);

    // vtable slot 9:
    void RegisterMessageTarget([In] uint hWnd, [In] uint dwThreadId);

    // vtable slot 10:
    void UnRegisterMessageTarget();

    // vtable slot 11:
    void SendClientShutDownRequest();
}

// Dummy interfaces for out-params — we don't need to call methods on them,
// we just need the types to exist so the vtable layout is correct.
[ComImport]
[Guid("00000000-0000-0000-0000-000000000000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClPBX { }

[ComImport]
[Guid("00000000-0000-0000-0000-000000000000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClUser { }

// Line manager type enum
public enum SClLineManagerType
{
    SClPbxLineManager = 0,
    SClTapiLineManager = 1
}

// GUID: F8E5536B-4C00-11D3-80BC-00105A653379 — the disp-interface we already use via dynamic
// But we also need DispId(100) DispClientConfig which returns IClientConfig
// GUID: F8E554CD-4C00-11D3-80BC-00105A653379 — IClientConfig (has LoginDeviceType, audio device names)
[ComImport]
[Guid("F8E554CD-4C00-11D3-80BC-00105A653379")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IClientConfig
{
    [DispId(24)] sbyte LoginDeviceType { get; set; }
    [DispId(40)] sbyte DefaultLoginDeviceType { get; }

    // Audio device names (DispIds 92-99 from RE)
    [DispId(92)] string HandsetDevice { get; set; }
    [DispId(93)] string HandsetCaptureDevice { get; set; }
    [DispId(94)] string HeadsetDevice { get; set; }
    [DispId(95)] string HeadsetCaptureDevice { get; set; }
    [DispId(96)] string HandsfreeDevice { get; set; }
    [DispId(97)] string HandsfreeCaptureDevice { get; set; }
    [DispId(98)] string OpenListeningDevice { get; set; }
    [DispId(99)] string RingingDevice { get; set; }

    [DispId(101)] int DefaultAudioMode { get; }
}

// GUID: F8E554AC-4C00-11D3-80BC-00105A653379 — IClientLineMgrEx8 (audio vtable API)
[ComImport]
[Guid("F8E554AC-4C00-11D3-80BC-00105A653379")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClientLineMgrEx8
{
    // Slots 0-2: IUnknown
    // The exact vtable order matters. We need to get to UseWaveDevices.
    // Since we can't know all intermediate slots, we use PreserveSig and
    // careful ordering. For now, just declare the methods we need.
    // If vtable offset is wrong, we'll get a COMException and adjust.

    void SetVolume(int volume);
    void GetVolume(out int volume);
    void IncrementVolume();
    void DecrementVolume();
    void SetMicLevel(int level);
    void GetMicLevel(out int level);
    void GetAudioMode(out int piAudioMode);
    void SetAudioMode(int iAudioMode);
    void GetAvailableWaveDevicesEx(ref uint pMaxDevices, out IntPtr pSoundDevices, out uint pNumReturned);
    void UseWaveDevices(ref IntPtr pVoiceDevice, ref IntPtr pHandsFreeDevice, ref IntPtr pRingingDevice, int bConfigure, int bPnPEnable);
    void GetUsedWaveDevices(out IntPtr pVoiceDevice, out IntPtr pHandsFreeDevice, out IntPtr pRingingDevice);
    void IsAudioConfigured(out int pbConfigured, out int pbIsPnPDevice, out int pbPnPDevicePresent);
    void StartAudioPnP(int bForcePnPDevice);
}
