using System.Diagnostics;
using System.Runtime.InteropServices;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Strategie gegen SwyxIt!-Fenster-Blitz:
///
/// 1. PROAKTIV: Alle SwyxIt!-Fenster werden permanent off-screen verschoben
///    (Position -32000,-32000, Größe 0×0). Selbst wenn Windows WS_VISIBLE
///    setzt, befindet sich das Fenster auf keinem Monitor → kein Blitz.
///
/// 2. REAKTIV: SetWinEventHook fängt EVENT_OBJECT_SHOW / EVENT_SYSTEM_FOREGROUND
///    ab und versteckt + verschiebt Fenster sofort im Callback.
///
/// 3. FALLBACK: Timer-Polling alle 500ms als letzte Sicherheitsstufe.
///
/// CRITICAL: Muss auf dem STA-Thread mit aktiver Message Pump laufen.
/// </summary>
public sealed class WindowHook : IDisposable
{
    // Win32 Event-Konstanten
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private const int SW_HIDE = 0;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    // SetWindowPos Flags
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private const uint SWP_FRAMECHANGED = 0x0020;

    // Off-screen Position — weit außerhalb jedes Monitors
    private const int OFFSCREEN_X = -32000;
    private const int OFFSCREEN_Y = -32000;

    // Win32 P/Invoke
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    // Static instance to prevent GC
    private static WindowHook? _instance;

    // CRITICAL: Delegate + EnumWindowsProc MÜSSEN als Feld gehalten werden
    private readonly WinEventDelegate _hookDelegate;

    private IntPtr _hookShow = IntPtr.Zero;
    private IntPtr _hookForeground = IntPtr.Zero;
    private IntPtr _hookCreate = IntPtr.Zero;
    private readonly HashSet<uint> _swyxPids = new();
    private readonly HashSet<IntPtr> _exiledWindows = new();  // Bereits off-screen verschobene Fenster
    private bool _disposed;
    private int _hideCount;

    private WindowHook()
    {
        _hookDelegate = WinEventProc;
    }

    /// <summary>
    /// Installiert Event-Hooks und verschiebt alle existierenden SwyxIt!-Fenster off-screen.
    /// MUSS auf dem STA-Thread aufgerufen werden.
    /// </summary>
    public static WindowHook Install()
    {
        _instance?.Dispose();

        var hook = new WindowHook();
        _instance = hook;

        hook.RefreshSwyxPids();

        // SOFORT alle existierenden Fenster off-screen verschieben
        hook.ExileAllSwyxWindows();

        // Hook für EVENT_OBJECT_CREATE — fängt Fenster ab sobald sie ERSTELLT werden
        // (bevor sie sichtbar sind)
        hook._hookCreate = SetWinEventHook(
            EVENT_OBJECT_CREATE, EVENT_OBJECT_CREATE,
            IntPtr.Zero, hook._hookDelegate,
            0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        // Hook für EVENT_OBJECT_SHOW — fängt Fenster ab wenn sie sichtbar gemacht werden
        hook._hookShow = SetWinEventHook(
            EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW,
            IntPtr.Zero, hook._hookDelegate,
            0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        // Hook für EVENT_SYSTEM_FOREGROUND — fängt Fenster ab die in den Vordergrund kommen
        hook._hookForeground = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, hook._hookDelegate,
            0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        bool allOk = hook._hookCreate != IntPtr.Zero
                  && hook._hookShow != IntPtr.Zero
                  && hook._hookForeground != IntPtr.Zero;

        if (!allOk)
            Logging.Warn("WindowHook: Nicht alle Hooks installiert!");
        else
            Logging.Info($"WindowHook: 3 Event-Hooks installiert (PIDs: {string.Join(", ", hook._swyxPids)}). "
                       + $"{hook._exiledWindows.Count} Fenster off-screen verschoben.");

        return hook;
    }

    /// <summary>
    /// Aktualisiert SwyxIt!-PIDs und verschiebt neue Fenster off-screen.
    /// Wird vom Timer periodisch aufgerufen.
    /// </summary>
    public void RefreshSwyxPids()
    {
        _swyxPids.Clear();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var name = proc.ProcessName.ToLowerInvariant();
                if (name.Contains("swyxit") || name == "swyxitc")
                    _swyxPids.Add((uint)proc.Id);
            }
            catch { }
        }
    }

    /// <summary>
    /// Findet ALLE SwyxIt!-Fenster per EnumWindows und verschiebt sie off-screen.
    /// Dreistufig: 1) Off-screen + Größe 0, 2) SW_HIDE, 3) WS_VISIBLE entfernen.
    /// </summary>
    public void ExileAllSwyxWindows()
    {
        if (_swyxPids.Count == 0) return;

        int count = 0;
        EnumWindows((hWnd, _) =>
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (_swyxPids.Contains(pid))
                {
                    NukeWindow(hWnd);
                    count++;
                }
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        if (count > 0)
            Logging.Info($"WindowHook: {count} Fenster exile'd (off-screen + hidden).");
    }

    /// <summary>
    /// Macht ein einzelnes Fenster komplett unsichtbar:
    /// 1. Off-screen verschieben (-32000, -32000) mit Größe 0×0
    /// 2. SW_HIDE
    /// 3. WS_VISIBLE entfernen
    /// 4. WS_EX_TOOLWINDOW + WS_EX_NOACTIVATE setzen (verhindert Taskbar-Eintrag + Fokus)
    /// </summary>
    private void NukeWindow(IntPtr hWnd)
    {
        try
        {
            // Schritt 1: Off-screen + Größe 0 — auch wenn WS_VISIBLE kurz gesetzt wird,
            // ist das Fenster auf keinem Monitor sichtbar
            SetWindowPos(hWnd, IntPtr.Zero,
                OFFSCREEN_X, OFFSCREEN_Y, 0, 0,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW | SWP_FRAMECHANGED);

            // Schritt 2: SW_HIDE
            ShowWindow(hWnd, SW_HIDE);

            // Schritt 3: WS_VISIBLE Flag entfernen
            int style = GetWindowLong(hWnd, GWL_STYLE);
            if ((style & WS_VISIBLE) != 0)
                SetWindowLong(hWnd, GWL_STYLE, style & ~WS_VISIBLE);

            // Schritt 4: Als Tool-Window markieren (kein Taskbar-Eintrag, nicht aktivierbar)
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            int newExStyle = exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            if (newExStyle != exStyle)
                SetWindowLong(hWnd, GWL_EXSTYLE, newExStyle);

            _exiledWindows.Add(hWnd);
        }
        catch { }
    }

    /// <summary>
    /// Event-Callback. Wird bei SHOW, FOREGROUND und CREATE aufgerufen.
    /// Reagiert sofort — verschiebt SwyxIt!-Fenster off-screen + hidden.
    /// </summary>
    private void WinEventProc(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        if (_swyxPids.Count == 0) return;
        // Nur Top-Level-Fenster
        if (idObject != 0) return;

        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (!_swyxPids.Contains(pid)) return;

            // Fenster gehört zu SwyxIt! — sofort eliminieren
            NukeWindow(hwnd);
            _hideCount++;

            if (_hideCount <= 10 || _hideCount % 100 == 0)
            {
                Logging.Info($"WindowHook: Fenster geNuked (event=0x{eventType:X4}, total={_hideCount}).");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"WindowHook: WinEventProc Fehler: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hookCreate != IntPtr.Zero)
        {
            UnhookWinEvent(_hookCreate);
            _hookCreate = IntPtr.Zero;
        }
        if (_hookShow != IntPtr.Zero)
        {
            UnhookWinEvent(_hookShow);
            _hookShow = IntPtr.Zero;
        }
        if (_hookForeground != IntPtr.Zero)
        {
            UnhookWinEvent(_hookForeground);
            _hookForeground = IntPtr.Zero;
        }

        Logging.Info($"WindowHook: Hooks entfernt. {_hideCount}× Fenster versteckt, {_exiledWindows.Count} off-screen.");
        _instance = null;
    }
}
