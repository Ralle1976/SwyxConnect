using System.Diagnostics;
using System.Runtime.InteropServices;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Dreistufige Unterdrückung aller SwyxIt!-Fenster:
///
/// 1. PROAKTIV: Beim Start alle existierenden Swyx-Fenster off-screen verschieben
/// 2. REAKTIV: SetWinEventHook fängt neue Fenster ab und versteckt sie sofort
/// 3. TIMER: Fallback-Timer (500ms) räumt entkommene Fenster auf
///
/// Fenster werden auf Position (-32000,-32000) mit Größe 0×0 verschoben,
/// WS_VISIBLE entfernt, WS_EX_TOOLWINDOW gesetzt (kein Taskbar-Eintrag).
/// Modale Dialoge (Fehlermeldungen, JavaScript-Errors) werden per WM_CLOSE geschlossen.
/// </summary>
public sealed class WindowHook : IDisposable
{
    // Win32 Constants
    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;
    private const int SW_HIDE = 0;
    private const uint WM_CLOSE = 0x0010;

    // Off-screen position
    private const int OFFSCREEN_X = -32000;
    private const int OFFSCREEN_Y = -32000;

    // Swyx process name patterns (lowercase)
    private static readonly string[] SwyxProcessPatterns =
        { "swyxit", "clmgr", "skinphone", "ippbxsrv", "callroutingmgr", "imclient" };

    // Dialog titles that indicate error/warning dialogs to close
    private static readonly string[] DialogKillPatterns =
        { "error", "fehler", "javascript", "script", "warnung", "warning", "swyxit" };

    private readonly HashSet<int> _swyxPids = new();
    private readonly List<IntPtr> _hooks = new();
    private System.Windows.Forms.Timer? _fallbackTimer;
    private WinEventDelegate? _hookDelegate; // prevent GC
    private bool _disposed;
    private int _nukCount;
    private int _dialogsKilled;

    // P/Invoke delegates and methods
    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Startet die Fensterunterdrückung.
    /// MUSS auf dem STA-Thread aufgerufen werden (für WinForms Timer).
    /// </summary>
    public void Start()
    {
        Logging.Info("WindowHook: Starte SwyxIt!-Fensterunterdrückung...");

        // Step 1: Scan all Swyx PIDs
        RefreshSwyxPids();

        // Step 2: Exile all existing Swyx windows immediately
        ExileAllSwyxWindows();

        // Step 3: Install WinEvent hooks for new windows
        _hookDelegate = OnWinEvent; // prevent GC

        var hookCreate = SetWinEventHook(
            EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW,
            IntPtr.Zero, _hookDelegate, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (hookCreate != IntPtr.Zero) _hooks.Add(hookCreate);

        var hookForeground = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _hookDelegate, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (hookForeground != IntPtr.Zero) _hooks.Add(hookForeground);

        Logging.Info($"WindowHook: {_hooks.Count} WinEvent-Hooks installiert.");

        // Step 4: Fallback timer (500ms) catches anything that slipped through
        _fallbackTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _fallbackTimer.Tick += (_, _) =>
        {
            RefreshSwyxPids();
            ExileAllSwyxWindows();
        };
        _fallbackTimer.Start();

        Logging.Info($"WindowHook: Aktiv. {_swyxPids.Count} Swyx-Prozesse überwacht, {_nukCount} Fenster unterdrückt.");
    }

    /// <summary>Findet alle laufenden Swyx-Prozesse und speichert deren PIDs.</summary>
    private void RefreshSwyxPids()
    {
        _swyxPids.Clear();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var name = proc.ProcessName.ToLowerInvariant();
                foreach (var pattern in SwyxProcessPatterns)
                {
                    if (name.Contains(pattern))
                    {
                        _swyxPids.Add(proc.Id);
                        break;
                    }
                }
            }
            catch { /* Process may have exited */ }
        }
    }

    /// <summary>Verschiebt ALLE Fenster aller Swyx-Prozesse off-screen.</summary>
    private void ExileAllSwyxWindows()
    {
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (_swyxPids.Contains((int)pid))
            {
                NukeWindow(hwnd);
            }
            return true; // continue enumeration
        }, IntPtr.Zero);
    }

    /// <summary>WinEvent callback — reagiert auf neue/sichtbare Fenster.</summary>
    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero || idObject != 0) return;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (!_swyxPids.Contains((int)pid)) return;

        // Check if this is a modal dialog (error/warning)
        if (IsDialogToKill(hwnd))
        {
            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            _dialogsKilled++;
            Logging.Debug($"WindowHook: Dialog geschlossen (PID {pid}), gesamt: {_dialogsKilled}");
        }

        NukeWindow(hwnd);
    }

    /// <summary>
    /// Eliminiert ein einzelnes Fenster: off-screen + unsichtbar + kein Taskbar-Eintrag.
    /// </summary>
    private void NukeWindow(IntPtr hwnd)
    {
        try
        {
            // Move off-screen with zero size
            SetWindowPos(hwnd, IntPtr.Zero, OFFSCREEN_X, OFFSCREEN_Y, 0, 0,
                SWP_NOACTIVATE | SWP_NOZORDER);

            // Hide window
            ShowWindow(hwnd, SW_HIDE);

            // Remove WS_VISIBLE, add WS_EX_TOOLWINDOW + WS_EX_NOACTIVATE
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_VISIBLE);

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            _nukCount++;
        }
        catch { /* Window may have been destroyed */ }
    }

    /// <summary>Prüft ob ein Fenster ein modaler Error/Warning-Dialog ist.</summary>
    private static bool IsDialogToKill(IntPtr hwnd)
    {
        // Check Win32 class name (#32770 = dialog)
        var className = new char[256];
        int classLen = GetClassName(hwnd, className, className.Length);
        var cls = new string(className, 0, classLen);
        if (cls != "#32770") return false;

        // Check title for error/warning patterns
        var titleBuf = new char[512];
        int titleLen = GetWindowText(hwnd, titleBuf, titleBuf.Length);
        var title = new string(titleBuf, 0, titleLen).ToLowerInvariant();

        foreach (var pattern in DialogKillPatterns)
        {
            if (title.Contains(pattern)) return true;
        }

        return false;
    }

    /// <summary>Statistik: Wie viele Fenster wurden unterdrückt.</summary>
    public (int WindowsNuked, int DialogsKilled, int SwyxPids) GetStats()
        => (_nukCount, _dialogsKilled, _swyxPids.Count);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _fallbackTimer?.Stop();
        _fallbackTimer?.Dispose();

        foreach (var hook in _hooks)
            UnhookWinEvent(hook);
        _hooks.Clear();

        Logging.Info($"WindowHook: Gestoppt. {_nukCount} Fenster unterdrückt, {_dialogsKilled} Dialoge geschlossen.");
    }
}
