using System.Diagnostics;
using System.Runtime.InteropServices;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Com;

/// <summary>
/// Suppresses the classic SwyxIt! client so it doesn't pop up over our UI.
///
/// RE findings (2026-07-13):
///   - SwyxIt! is a GUI skin only; CLMgr owns the entire telephony stack (SIP/RTP/Tunnel)
///   - SwyxIt! auto-starts via Windows Startup shortcut with /M (minimized to tray)
///   - On call events, SwyxIt! un-minimizes itself via HandleCallPopup
///   - CLMgr routes line events to the "CTI slave" (SwyxIt!) which triggers the popup
///   - We don't need SwyxIt! at all — our bridge talks to CLMgr directly
///
/// This suppressor kills the classic SwyxIt! process (NOT the Modern SwyxIt!.UI.exe)
/// on a 2-second timer and renames the Startup shortcut to prevent re-launch on reboot.
/// </summary>
public sealed class SwyxItSuppressor : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;

    // Only kill the classic SwyxIt! — leave SwyxIt!.UI.exe (Modern app) alone
    private const string ClassicProcessName = "SwyxIt!";

    private readonly System.Timers.Timer _timer;
    private bool _disposed;

    // Path to the Windows Startup folder (all users)
    private static readonly string StartupFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

    private static readonly string ShortcutPath =
        Path.Combine(StartupFolder, "SwyxIt!.lnk");

    private static readonly string DisabledShortcutPath =
        Path.Combine(StartupFolder, "SwyxIt!.lnk.disabled");

    public SwyxItSuppressor()
    {
        _timer = new System.Timers.Timer(2000) { AutoReset = true };
        _timer.Elapsed += (s, e) => SuppressClassicSwyxIt();
    }

    public void Start()
    {
        DisableStartupShortcut();

        // Only kill SwyxIt if it's ALREADY running. Do NOT start it ourselves.
        // In standalone mode (EnablePowerDialMode=1), CLMgr initializes audio devices itself.
        // Starting SwyxIt would interfere with the RC tunnel login.
        var swyxIt = Process.GetProcessesByName(ClassicProcessName).FirstOrDefault();
        if (swyxIt != null)
        {
            Logging.Info($"SwyxItSuppressor: SwyxIt! running (PID={swyxIt.Id}). Killing it now.");
            SuppressClassicSwyxIt();
            _timer.Start();
            Logging.Info("SwyxItSuppressor: Active.");
        }
        else
        {
            Logging.Info("SwyxItSuppressor: SwyxIt! not running. Standalone mode — not starting it.");
        }
    }

    public void Stop()
    {
        _timer.Stop();
        Logging.Info("SwyxItSuppressor: Stopped.");
    }

    /// <summary>
    /// Kills any running classic SwyxIt! process and hides its window first
    /// to avoid a brief flash of the window.
    /// </summary>
    private static void SuppressClassicSwyxIt()
    {
        try
        {
            var procs = Process.GetProcessesByName(ClassicProcessName);
            foreach (var proc in procs)
            {
                try
                {
                    // Hide window first to avoid visual flash
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(proc.MainWindowHandle, SW_HIDE);
                    }
                    proc.Kill();
                    Logging.Info($"SwyxItSuppressor: Classic SwyxIt! killed (PID={proc.Id}).");
                }
                catch (Exception ex)
                {
                    Logging.Warn($"SwyxItSuppressor: Failed to kill PID={proc.Id}: {ex.Message}");
                }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxItSuppressor: Error during suppression: {ex.Message}");
        }
    }

    /// <summary>
    /// Renames the Startup shortcut from "SwyxIt!.lnk" to "SwyxIt!.lnk.disabled"
    /// so SwyxIt! doesn't auto-launch on next reboot.
    /// Restores it on disposal so the app doesn't permanently alter the system
    /// when uninstalled or stopped.
    /// </summary>
    private static void DisableStartupShortcut()
    {
        try
        {
            if (File.Exists(ShortcutPath))
            {
                // Check if already disabled by a previous run
                if (File.Exists(DisabledShortcutPath))
                {
                    // Both exist — delete the active one (keep the .disabled backup)
                    File.Delete(ShortcutPath);
                    Logging.Info("SwyxItSuppressor: Startup shortcut already disabled.");
                    return;
                }

                File.Move(ShortcutPath, DisabledShortcutPath);
                Logging.Info($"SwyxItSuppressor: Startup shortcut renamed to .disabled.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            Logging.Warn("SwyxItSuppressor: No admin rights to rename Startup shortcut. Process-killer still active.");
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxItSuppressor: Could not disable Startup shortcut: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores the Startup shortcut (renames .disabled back to .lnk).
    /// Called when the bridge shuts down so SwyxIt! can launch normally
    /// if the user starts their PC without our app.
    /// </summary>
    private static void RestoreStartupShortcut()
    {
        try
        {
            if (File.Exists(DisabledShortcutPath) && !File.Exists(ShortcutPath))
            {
                File.Move(DisabledShortcutPath, ShortcutPath);
                Logging.Info("SwyxItSuppressor: Startup shortcut restored.");
            }
        }
        catch (Exception ex)
        {
            Logging.Warn($"SwyxItSuppressor: Could not restore Startup shortcut: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer?.Stop();
        _timer?.Dispose();

        // Restore the shortcut so SwyxIt! can launch on next reboot
        // (if user doesn't start our app)
        RestoreStartupShortcut();

        Logging.Info("SwyxItSuppressor: Disposed, Startup shortcut restored.");
    }
}
