# Check SwyxIt! window state - uses $procId instead of $pid (reserved in PS)
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinCheck {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
'@

$results = @()
[WinCheck]::EnumWindows({
  param($hWnd, $lParam)
  $sb = New-Object System.Text.StringBuilder(256)
  [WinCheck]::GetWindowText($hWnd, $sb, 256) | Out-Null
  $title = $sb.ToString()

  $procId = [uint32]0
  [WinCheck]::GetWindowThreadProcessId($hWnd, [ref]$procId) | Out-Null

  if ($procId -gt 0) {
    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($proc -and $proc.ProcessName -match 'SwyxIt') {
      $visible = [WinCheck]::IsWindowVisible($hWnd)
      $minimized = [WinCheck]::IsIconic($hWnd)
      $rect = New-Object WinCheck+RECT
      [WinCheck]::GetWindowRect($hWnd, [ref]$rect) | Out-Null
      $script:results += [PSCustomObject]@{
        HWND = $hWnd.ToString()
        Title = if ($title) { $title } else { "(no title)" }
        PID = $procId
        Visible = $visible
        Minimized = $minimized
        Position = "$($rect.Left),$($rect.Top) ($($rect.Right - $rect.Left)x$($rect.Bottom - $rect.Top))"
      }
    }
  }
  return $true
}, [IntPtr]::Zero) | Out-Null

if ($results.Count -gt 0) {
  Write-Host "=== SwyxIt! WINDOWS ===" -ForegroundColor Cyan
  $results | Format-Table -AutoSize
} else {
  Write-Host "No SwyxIt! windows found" -ForegroundColor Yellow
}
