using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SwyxBridge.Utils;

namespace SwyxBridge.Com;

/// <summary>
/// Dreistufige Fenster-Eliminierung für SwyxIt!:
///
/// 1. PROAKTIV: Alle SwyxIt!-Fenster permanent off-screen (-32000,-32000, 0×0).
/// 2. REAKTIV: SetWinEventHook (CREATE/SHOW/FOREGROUND) → sofort NukeWindow().
/// 3. DIALOG-KILLER: Modale Dialoge (z.B. "JavaScript error") per WM_CLOSE schließen.
/// 4. FALLBACK: Timer 500ms für alles was den Hooks entgeht.
///
/// Trackt nicht nur SwyxIt!.exe sondern auch CLMgr, IpPbxSrv und andere Child-Prozesse.
/// </summary>
public sealed class WindowHook : IDisposable
{
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
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int OFFSCREEN_X = -32000;
    private const int OFFSCREEN_Y = -32000;
    private const int WM_CLOSE = 0x0010;

    // Dialog-Fenster Klassename in Win32
    private const string DIALOG_CLASS = "#32770";

    // ── P/Invoke ────────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // ── Static + Instance ───────────────────────────────────────────────────────

    private static WindowHook? _instance;

    // CRITICAL: Delegate MUSS als Feld gehalten werden (GC-Schutz)
    private readonly WinEventDelegate _hookDelegate;

    private IntPtr _hookShow = IntPtr.Zero;
    private IntPtr _hookForeground = IntPtr.Zero;
    private IntPtr _hookCreate = IntPtr.Zero;
    private readonly HashSet<uint> _swyxPids = new();
    private readonly HashSet<IntPtr> _exiledWindows = new();
    private bool _disposed;
    private int _hideCount;
    private int _dialogsKilled;

    // Prozessnamen die zu SwyxIt! gehören (alles lowercase)
    private static readonly string[] SwyxProcessPatterns = new[]
    {
        "swyxit",     // SwyxIt!.exe, SwyxIt!C.exe
        "swyxitc",
        "clmgr",      // Client Line Manager
        "ippbxsrv",   // IP PBX Server Client
        "skinphone",  // SkinPhone UI
        "swyx",       // Catch-all für Swyx-Prozesse
    };

    private WindowHook()
    {
        _hookDelegate = WinEventProc;
    }

    // ── Install ─────────────────────────────────────────────────────────────────

    public static WindowHook Install()
    {
        _instance?.Dispose();

        var hook = new WindowHook();
        _instance = hook;

        hook.RefreshSwyxPids();
        hook.ExileAllSwyxWindows();

        hook._hookCreate = SetWinEventHook(
            EVENT_OBJECT_CREATE, EVENT_OBJECT_CREATE,
            IntPtr.Zero, hook._hookDelegate, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        hook._hookShow = SetWinEventHook(
            EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW,
            IntPtr.Zero, hook._hookDelegate, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        hook._hookForeground = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, hook._hookDelegate, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        bool allOk = hook._hookCreate != IntPtr.Zero
                  && hook._hookShow != IntPtr.Zero
                  && hook._hookForeground != IntPtr.Zero;

        if (!allOk)
            Logging.Warn("WindowHook: Nicht alle Hooks installiert!");
        else
            Logging.Info($"WindowHook: 3 Hooks aktiv, {hook._swyxPids.Count} PIDs, "
                       + $"{hook._exiledWindows.Count} Fenster off-screen.");

        return hook;
    }

    // ── PID Management ──────────────────────────────────────────────────────────

    public void RefreshSwyxPids()
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
                        _swyxPids.Add((uint)proc.Id);
                        break;
                    }
                }
            }
            catch { }
        }
    }

    // ── Exile All ───────────────────────────────────────────────────────────────

    public void ExileAllSwyxWindows()
    {
        if (_swyxPids.Count == 0) return;

        int nuked = 0;
        int dialogsClosed = 0;

        EnumWindows((hWnd, _) =>
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (!_swyxPids.Contains(pid)) return true;

                // Dialog erkennen und schließen
                if (IsDialogWindow(hWnd))
                {
                    KillDialog(hWnd);
                    dialogsClosed++;
                }
                else
                {
                    NukeWindow(hWnd);
                    nuked++;
                }
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        if (nuked > 0 || dialogsClosed > 0)
        {
            if (_hideCount == 0) // Nur beim ersten Mal loggen
                Logging.Info($"WindowHook: {nuked} Fenster exile'd, {dialogsClosed} Dialoge geschlossen.");
        }
    }

    // ── Nuke Window (off-screen + hidden) ───────────────────────────────────────

    private void NukeWindow(IntPtr hWnd)
    {
        try
        {
            // 1. Off-screen + Größe 0
            SetWindowPos(hWnd, IntPtr.Zero,
                OFFSCREEN_X, OFFSCREEN_Y, 0, 0,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW | SWP_FRAMECHANGED);

            // 2. SW_HIDE
            ShowWindow(hWnd, SW_HIDE);

            // 3. WS_VISIBLE entfernen
            int style = GetWindowLong(hWnd, GWL_STYLE);
            if ((style & WS_VISIBLE) != 0)
                SetWindowLong(hWnd, GWL_STYLE, style & ~WS_VISIBLE);

            // 4. Tool-Window + NoActivate
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            int newEx = exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            if (newEx != exStyle)
                SetWindowLong(hWnd, GWL_EXSTYLE, newEx);

            _exiledWindows.Add(hWnd);
        }
        catch { }
    }

    // ── Dialog Detection + Kill ─────────────────────────────────────────────────

    /// <summary>
    /// Prüft ob ein Fenster ein Dialog ist (Klasse "#32770" oder Titel enthält
    /// typische Error-Signalwörter).
    /// </summary>
    private static bool IsDialogWindow(IntPtr hWnd)
    {
        try
        {
            // Klasse prüfen
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, 256);
            string cls = className.ToString();

            if (cls == DIALOG_CLASS) return true;

            // Titel prüfen auf Error-Dialoge
            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, 256);
            string titleStr = title.ToString().ToLowerInvariant();

            if (titleStr.Contains("error") || titleStr.Contains("fehler")
                || titleStr.Contains("javascript") || titleStr.Contains("script")
                || titleStr.Contains("warnung") || titleStr.Contains("warning"))
                return true;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Schließt einen Dialog per WM_CLOSE. Funktioniert auch bei modalen Dialogen.
    /// Falls WM_CLOSE nicht wirkt: auch off-screen verschieben + verstecken.
    /// </summary>
    private void KillDialog(IntPtr hWnd)
    {
        try
        {
            // Erst off-screen verschieben (sofort unsichtbar)
            SetWindowPos(hWnd, IntPtr.Zero,
                OFFSCREEN_X, OFFSCREEN_Y, 0, 0,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW);

            ShowWindow(hWnd, SW_HIDE);

            // Dann WM_CLOSE senden um den Dialog zu schließen
            // PostMessage ist non-blocking (anders als SendMessage)
            PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            _dialogsKilled++;

            if (_dialogsKilled <= 20)
            {
                var title = new StringBuilder(256);
                GetWindowText(hWnd, title, 256);
                Logging.Info($"WindowHook: Dialog geschlossen: \"{title}\" (total={_dialogsKilled}).");
            }
        }
        catch { }
    }

    // ── Event Callback ──────────────────────────────────────────────────────────

    private void WinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        if (_swyxPids.Count == 0) return;
        if (idObject != 0) return; // Nur Top-Level

        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (!_swyxPids.Contains(pid)) return;

            // Dialog? → Sofort schließen
            if (IsDialogWindow(hwnd))
            {
                KillDialog(hwnd);
            }
            else
            {
                NukeWindow(hwnd);
            }

            _hideCount++;

            if (_hideCount <= 5 || _hideCount % 200 == 0)
                Logging.Info($"WindowHook: event=0x{eventType:X4}, hide={_hideCount}, dialogs={_dialogsKilled}.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"WindowHook: WinEventProc: {ex.Message}");
        }
    }

    // ── Dispose ─────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hookCreate != IntPtr.Zero) { UnhookWinEvent(_hookCreate); _hookCreate = IntPtr.Zero; }
        if (_hookShow != IntPtr.Zero) { UnhookWinEvent(_hookShow); _hookShow = IntPtr.Zero; }
        if (_hookForeground != IntPtr.Zero) { UnhookWinEvent(_hookForeground); _hookForeground = IntPtr.Zero; }

        Logging.Info($"WindowHook: Fertig. {_hideCount}× versteckt, {_dialogsKilled}× Dialoge geschlossen.");
        _instance = null;
    }
}
