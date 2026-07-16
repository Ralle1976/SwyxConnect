using System.Runtime.InteropServices;

namespace DiagInspect;

// COM Interop definitions — VERIFIED against decompiled Interop.CLMgr.dll (2026-07-16).
// Used by DiagInspect to inspect CLMgr audio state via native vtable calls.

/// <summary>
/// Complete CLMgrSoundDeviceDescriptionEx struct (22 fields, Pack=4).
/// Source: decompiled Interop.CLMgr.dll, line 15042-15096.
/// NOTE: the production bridge/SwyxStandalone/Com/CLMgrInterop.cs has only 20 fields
/// (missing m_bHookSupportBluetooth and m_bIsWellKnownDevice) — that is a marshalling
/// bug that must be fixed before UseWaveDevices can be called safely in the bridge.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CLMgrSoundDeviceDescriptionEx
{
    [MarshalAs(UnmanagedType.BStr)] public string m_bstrVisibleDeviceName;
    public int m_bIsDirectXSoundDevice;
    public int m_bHasPlayer;
    public int m_bHasRecorder;
    public int m_bHasMixer;
    public int m_bHasDxAudioRenderer;
    [MarshalAs(UnmanagedType.BStr)] public string m_bstrDeviceIdPlayer;
    [MarshalAs(UnmanagedType.BStr)] public string m_bstrDeviceIdRecorder;
    [MarshalAs(UnmanagedType.BStr)] public string m_bstrMixerName;
    [MarshalAs(UnmanagedType.BStr)] public string m_bstrDxAudioRenderer;
    [MarshalAs(UnmanagedType.BStr)] public string m_bstrManufacturerId;
    [MarshalAs(UnmanagedType.BStr)] public string m_bstrsProductId;
    public uint m_dwManufacturerId;
    public uint m_dwProductId;
    public int m_bIsHandset;
    public int m_bIsHeadset;
    public int m_bIsSpeaker;
    public int m_bHookSupportUSB;
    public int m_bHookSupportComPort;
    public int m_iComPort;
    public int m_bHookSupportGamePort;
    public int m_bHookSupportBluetooth;
    public int m_bIsWellKnownDevice;
}

/// <summary>
/// IClientLineMgrEx8 — native vtable interface for audio device management.
/// Source: decompiled Interop.CLMgr.dll, line 2014-2041.
/// GUID: F8E554AC-4C00-11D3-80BC-00105A653379
///
/// CRITICAL: method order must match the vtable layout exactly. The compiler
/// preserves declaration order for InterfaceIsIUnknown interfaces, which is
/// what COM expects. Do NOT reorder.
/// </summary>
[ComImport]
[Guid("F8E554AC-4C00-11D3-80BC-00105A653379")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClientLineMgrEx8
{
    // Slot 3: IUnknown::QueryInterface, AddRef, Release (auto-generated)
    // Slots 4-10: IClientLineMgrEx8 methods

    void SetVolume([In] int iVolume);
    void GetVolume(out int piVolume);
    void IncrementVolume();
    void DecrementVolume();
    void SetMicLevel([In] int iVolume);
    void GetMicLevel(out int piVolume);
    void GetAudioMode(out int piAudioMode);

    // pMaxDevices is [in,out]: caller sets buffer size, callee fills + returns actual count.
    // pSoundDevices is a single-element buffer; for multiple devices CLMgr writes into
    // unmanaged memory beyond. We pass maxDevices=1 for safety and read just one.
    void GetAvailableWaveDevicesEx(
        [In][Out] ref uint pMaxDevices,
        out CLMgrSoundDeviceDescriptionEx pSoundDevices,
        out uint pNumReturned);

    void UseWaveDevices(
        [In] ref CLMgrSoundDeviceDescriptionEx pVoiceDevice,
        [In] ref CLMgrSoundDeviceDescriptionEx pHandsFreeDevice,
        [In] ref CLMgrSoundDeviceDescriptionEx pRingingDevice,
        [In] int bConfigure,
        [In] int bPnPEnable);

    void GetUsedWaveDevices(
        out CLMgrSoundDeviceDescriptionEx pVoiceDevice,
        out CLMgrSoundDeviceDescriptionEx pHandsFreeDevice,
        out CLMgrSoundDeviceDescriptionEx pRingingDevice);

    void IsAudioConfigured(
        out int pbConfigured,
        out int pbIsPnPDevice,
        out int pbPnPDevicePresent);

    void StartAudioPnP([In] int bForcePnPDevice);
    void SetAudioMode([In] int iAudioMode);
}
