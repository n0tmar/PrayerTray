using System.Text;

namespace PrayerTray.Native;

public static class TaskbarInterop
{
    private const string ShellTrayClass = "Shell_TrayWnd";
    private const string TrayNotifyClass = "TrayNotifyWnd";
    private const string TrayClockClass = "TrayClockWClass";
    private const string ClockButtonClass = "ClockButton";
    private const string CompositionBridgeClass = "Windows.UI.Composition.DesktopWindowContentBridge";
    private const int Win11TrailingIconWidth = 26;
    private const int Win11ClockWidth = 60;
    private const int Win11PositionFineTuneRight = 12;

    private static int GetClockLeftScreenX(Win32.Rect notifyRect)
    {
        var clock = GetTrayClockHandle();
        if (clock != IntPtr.Zero && Win32.GetWindowRect(clock, out var clockRect))
        {
            return clockRect.Left;
        }

        return notifyRect.Right - Win11TrailingIconWidth - Win11ClockWidth;
    }

    public static IntPtr GetShellTrayHandle() =>
        Win32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, ShellTrayClass, null);

    public static IntPtr GetTrayNotifyHandle()
    {
        var shellTray = GetShellTrayHandle();
        return shellTray == IntPtr.Zero
            ? IntPtr.Zero
            : Win32.FindWindowEx(shellTray, IntPtr.Zero, TrayNotifyClass, null);
    }

    public static IntPtr GetCompositionBridgeHandle()
    {
        var shellTray = GetShellTrayHandle();
        if (shellTray == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr bridge = IntPtr.Zero;
        Win32.EnumChildWindows(shellTray, (hwnd, _) =>
        {
            var className = new StringBuilder(256);
            Win32.GetClassName(hwnd, className, className.Capacity);
            if (className.ToString().Contains(CompositionBridgeClass, StringComparison.Ordinal))
            {
                bridge = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return bridge;
    }

    public static IntPtr GetTrayClockHandle(IntPtr excludeHwnd = default)
    {
        var trayNotify = GetTrayNotifyHandle();
        if (trayNotify == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var clock = Win32.FindWindowEx(trayNotify, IntPtr.Zero, TrayClockClass, null);
        if (clock != IntPtr.Zero && clock != excludeHwnd)
        {
            return clock;
        }

        clock = Win32.FindWindowEx(trayNotify, IntPtr.Zero, ClockButtonClass, null);
        if (clock != IntPtr.Zero && clock != excludeHwnd)
        {
            return clock;
        }

        return FindRightmostClockLikeChild(trayNotify, excludeHwnd);
    }

    public static bool TryGetTrayClockRect(out Win32.Rect rect, IntPtr excludeHwnd = default)
    {
        rect = default;
        var clockHandle = GetTrayClockHandle(excludeHwnd);
        if (clockHandle != IntPtr.Zero)
        {
            return Win32.GetWindowRect(clockHandle, out rect);
        }

        var trayNotify = GetTrayNotifyHandle();
        if (trayNotify != IntPtr.Zero && Win32.GetWindowRect(trayNotify, out var notifyRect))
        {
            var clockLeft = GetClockLeftScreenX(notifyRect);
            rect = new Win32.Rect
            {
                Left = clockLeft,
                Top = notifyRect.Top,
                Right = clockLeft + Win11ClockWidth,
                Bottom = notifyRect.Bottom
            };
            return true;
        }

        var shellTray = GetShellTrayHandle();
        if (shellTray == IntPtr.Zero || !Win32.GetWindowRect(shellTray, out var shellRect))
        {
            return false;
        }

        var shellClockLeft = shellRect.Right - Win11TrailingIconWidth - Win11ClockWidth;
        rect = new Win32.Rect
        {
            Left = shellClockLeft,
            Top = shellRect.Top,
            Right = shellClockLeft + Win11ClockWidth,
            Bottom = shellRect.Bottom
        };
        return true;
    }

    public static bool TryGetWidgetShellTrayClientPosition(
        IntPtr shellTrayHwnd,
        int widgetWidth,
        int widgetHeight,
        int gapPixels,
        out int clientX,
        out int clientY)
    {
        clientX = 0;
        clientY = 0;

        if (shellTrayHwnd == IntPtr.Zero)
        {
            return false;
        }

        var trayNotify = GetTrayNotifyHandle();
        if (trayNotify == IntPtr.Zero || !Win32.GetWindowRect(trayNotify, out var notifyRect))
        {
            if (!Win32.GetWindowRect(shellTrayHwnd, out var shellRect))
            {
                return false;
            }

            notifyRect = shellRect;
        }

        var screenLeft = GetClockLeftScreenX(notifyRect) - gapPixels - widgetWidth + Win11PositionFineTuneRight;
        var screenTop = notifyRect.Top + (notifyRect.Height - widgetHeight) / 2;
        var point = new Win32.Point { X = screenLeft, Y = screenTop };

        if (!Win32.ScreenToClient(shellTrayHwnd, ref point))
        {
            return false;
        }

        clientX = point.X;
        clientY = point.Y;
        return true;
    }

    public static bool TryGetWidgetScreenPosition(
        int widgetWidth,
        int widgetHeight,
        int gapPixels,
        out int screenX,
        out int screenY)
    {
        screenX = 0;
        screenY = 0;

        var shellTray = GetShellTrayHandle();
        if (!TryGetWidgetShellTrayClientPosition(shellTray, widgetWidth, widgetHeight, gapPixels, out var clientX, out var clientY))
        {
            return false;
        }

        var point = new Win32.Point { X = clientX, Y = clientY };
        if (!Win32.ClientToScreen(shellTray, ref point))
        {
            return false;
        }

        screenX = point.X;
        screenY = point.Y;
        return true;
    }

    private static IntPtr FindRightmostClockLikeChild(IntPtr trayNotify, IntPtr excludeHwnd)
    {
        IntPtr bestHandle = IntPtr.Zero;
        var bestRight = int.MinValue;

        Win32.EnumChildWindows(trayNotify, (hwnd, _) =>
        {
            if (hwnd == excludeHwnd || !Win32.IsWindowVisible(hwnd))
            {
                return true;
            }

            if (!Win32.GetWindowRect(hwnd, out var rect))
            {
                return true;
            }

            if (rect.Width is < 80 or > 220 || rect.Height is < 16 or > 80)
            {
                return true;
            }

            if (rect.Right <= bestRight)
            {
                return true;
            }

            bestRight = rect.Right;
            bestHandle = hwnd;
            return true;
        }, IntPtr.Zero);

        return bestHandle;
    }
}
