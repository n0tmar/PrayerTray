namespace PrayerTray.Native;

public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right
}

public static class TaskbarGeometry
{
    private const int EstimatedClockWidth = 126;
    private const int GapPixels = 6;

    public static bool TryGetPrimaryMonitorRects(out Win32.Rect monitorRect, out Win32.Rect workAreaRect)
    {
        monitorRect = default;
        workAreaRect = default;

        var monitor = Win32.MonitorFromWindow(TaskbarInterop.GetShellTrayHandle(), 2);
        if (monitor == IntPtr.Zero)
        {
            monitor = Win32.MonitorFromWindow(IntPtr.Zero, 2);
        }

        var monitorInfo = new Win32.MonitorInfo
        {
            CbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MonitorInfo>()
        };

        if (!Win32.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        monitorRect = monitorInfo.Monitor;
        workAreaRect = monitorInfo.WorkArea;
        return true;
    }

    public static bool TryGetVisibleTaskbarBounds(out Win32.Rect bounds, out TaskbarEdge edge)
    {
        bounds = default;
        edge = TaskbarEdge.Bottom;

        if (!TryGetPrimaryMonitorRects(out var monitorRect, out var workAreaRect))
        {
            return false;
        }

        var bottomGap = monitorRect.Bottom - workAreaRect.Bottom;
        var topGap = workAreaRect.Top - monitorRect.Top;
        var leftGap = workAreaRect.Left - monitorRect.Left;
        var rightGap = monitorRect.Right - workAreaRect.Right;

        if (bottomGap >= topGap && bottomGap >= leftGap && bottomGap >= rightGap && bottomGap > 0)
        {
            edge = TaskbarEdge.Bottom;
            bounds = new Win32.Rect
            {
                Left = workAreaRect.Left,
                Top = workAreaRect.Bottom,
                Right = workAreaRect.Right,
                Bottom = monitorRect.Bottom
            };
            return true;
        }

        if (topGap >= bottomGap && topGap >= leftGap && topGap >= rightGap && topGap > 0)
        {
            edge = TaskbarEdge.Top;
            bounds = new Win32.Rect
            {
                Left = workAreaRect.Left,
                Top = monitorRect.Top,
                Right = workAreaRect.Right,
                Bottom = workAreaRect.Top
            };
            return true;
        }

        if (leftGap >= rightGap && leftGap > 0)
        {
            edge = TaskbarEdge.Left;
            bounds = new Win32.Rect
            {
                Left = monitorRect.Left,
                Top = workAreaRect.Top,
                Right = workAreaRect.Left,
                Bottom = workAreaRect.Bottom
            };
            return true;
        }

        if (rightGap > 0)
        {
            edge = TaskbarEdge.Right;
            bounds = new Win32.Rect
            {
                Left = workAreaRect.Right,
                Top = workAreaRect.Top,
                Right = monitorRect.Right,
                Bottom = workAreaRect.Bottom
            };
            return true;
        }

        return false;
    }

    public static bool TryGetWidgetScreenPosition(int widgetWidth, int widgetHeight, out int screenX, out int screenY)
    {
        screenX = 0;
        screenY = 0;

        if (!TryGetVisibleTaskbarBounds(out var taskbarBounds, out var edge))
        {
            return false;
        }

        var clockLeft = GetClockLeftScreenX(taskbarBounds, edge);

        switch (edge)
        {
            case TaskbarEdge.Bottom:
            case TaskbarEdge.Top:
                screenX = clockLeft - widgetWidth - GapPixels;
                screenY = taskbarBounds.Top + (taskbarBounds.Height - widgetHeight) / 2;
                return true;

            case TaskbarEdge.Left:
                screenX = taskbarBounds.Right - widgetWidth;
                screenY = clockLeft - widgetHeight - GapPixels;
                return true;

            case TaskbarEdge.Right:
                screenX = taskbarBounds.Left;
                screenY = clockLeft - widgetHeight - GapPixels;
                return true;

            default:
                return false;
        }
    }

    private static int GetClockLeftScreenX(Win32.Rect taskbarBounds, TaskbarEdge edge)
    {
        if (TaskbarInterop.TryGetTrayClockRect(out var clockRect))
        {
            return edge is TaskbarEdge.Bottom or TaskbarEdge.Top
                ? clockRect.Left
                : clockRect.Top;
        }

        return edge switch
        {
            TaskbarEdge.Bottom or TaskbarEdge.Top => taskbarBounds.Right - EstimatedClockWidth,
            TaskbarEdge.Left or TaskbarEdge.Right => taskbarBounds.Bottom - EstimatedClockWidth,
            _ => taskbarBounds.Right - EstimatedClockWidth
        };
    }
}
