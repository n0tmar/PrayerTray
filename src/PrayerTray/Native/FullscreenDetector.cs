namespace PrayerTray.Native;

public sealed class FullscreenDetector : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private bool _isFullscreen;

    public event EventHandler<bool>? FullscreenChanged;

    public bool IsFullscreen => _isFullscreen;

    public FullscreenDetector()
    {
        _timer = new System.Threading.Timer(CheckFullscreen, null, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
    }

    private void CheckFullscreen(object? state)
    {
        var fullscreen = IsForegroundWindowFullscreen();
        if (fullscreen == _isFullscreen)
        {
            return;
        }

        _isFullscreen = fullscreen;
        FullscreenChanged?.Invoke(this, _isFullscreen);
    }

    private static bool IsForegroundWindowFullscreen()
    {
        var foreground = Win32.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        if (!Win32.GetWindowRect(foreground, out var windowRect))
        {
            return false;
        }

        var monitor = Win32.MonitorFromWindow(foreground, 2);
        var monitorInfo = new Win32.MonitorInfo { CbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MonitorInfo>() };
        if (!Win32.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var monitorRect = monitorInfo.Monitor;
        const int tolerance = 4;

        var coversLeft = Math.Abs(windowRect.Left - monitorRect.Left) <= tolerance;
        var coversTop = Math.Abs(windowRect.Top - monitorRect.Top) <= tolerance;
        var coversRight = Math.Abs(windowRect.Right - monitorRect.Right) <= tolerance;
        var coversBottom = Math.Abs(windowRect.Bottom - monitorRect.Bottom) <= tolerance;

        if (!coversLeft || !coversTop || !coversRight || !coversBottom)
        {
            return false;
        }

        // Maximized apps stop above the taskbar; only true fullscreen covers the bottom edge.
        return coversBottom;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
