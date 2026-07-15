using System.Runtime.InteropServices;

namespace SwyxStandalone.Com;

// COM Interop interfaces verified against decompiled Interop.CLMgr.dll (2026-07-15).

// Dummy interfaces for out-params
[ComImport]
[Guid("00000000-0000-0000-0000-000000000000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClPBX { }

[ComImport]
[Guid("00000000-0000-0000-0000-000000000000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClUser { }

// GUID: F8E552F6-4C00-11D3-80BC-00105A653379
[ComImport]
[Guid("F8E552F6-4C00-11D3-80BC-00105A653379")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClientLineMgr2
{
    void Init([In, MarshalAs(UnmanagedType.BStr)] string ServerName, out IClPBX ppIClPbx);
    void ReInit(out IClPBX ppIClPbx, out IClUser ppIClUser);
    void RegisterUser([In, MarshalAs(UnmanagedType.BStr)] string PbxUserName, out IClUser ppIClUser, out uint pulUserId, ref uint pMaxUsers, [Out, MarshalAs(UnmanagedType.BStr)] out string pszPBXUser, out uint pNumReturned);
    void RegisterSecondaryUser([In, MarshalAs(UnmanagedType.BStr)] string PbxUserName, [In, MarshalAs(UnmanagedType.BStr)] string NtUserName, [In, MarshalAs(UnmanagedType.BStr)] string NtPassword, out IClUser ppIClUser, out uint pulUserId);
    void ReleaseUser([In] uint ulUserId);
    void GetLineManagerType(out int pLineManagerType);
    void RegisterMessageTarget([In] uint hWnd, [In] uint dwThreadId);
    void UnRegisterMessageTarget();
    void SendClientShutDownRequest();
}

/// <summary>
/// Sound device description for IClientLineMgrEx8.UseWaveDevices().
/// Must match the native struct layout exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
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
}

/// <summary>
/// GUID: F8E554AC-4C00-11D3-80BC-00105A653379
/// The audio vtable API. Method order verified from Interop.CLMgr.dll.
/// </summary>
[ComImport]
[Guid("F8E554AC-4C00-11D3-80BC-00105A653379")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClientLineMgrEx8
{
    void SetVolume([In] int iVolume);
    void GetVolume(out int piVolume);
    void IncrementVolume();
    void DecrementVolume();
    void SetMicLevel([In] int iVolume);
    void GetMicLevel(out int piVolume);
    void GetAudioMode(out int piAudioMode);

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
