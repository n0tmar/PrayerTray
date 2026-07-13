using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PrayerTray.Native;

namespace PrayerTray.Services;

public static class TaskbarAutomation
{
    private static readonly TimeSpan AutomationTimeout = TimeSpan.FromMilliseconds(750);

    private static readonly string[] ClockAutomationIds =
    [
        "ClockButton",
        "DateTimeButton",
        "NotificationCenterButton"
    ];

    private static AutomationElement? _taskbarFrameElement;
    private static AutomationElement? _clockElement;

    public static bool TryGetTaskbarFrameRect(IntPtr taskbarHwnd, out Rect bounds)
    {
        bounds = Rect.Empty;
        if (taskbarHwnd == IntPtr.Zero)
        {
            return false;
        }

        if (TryGetElementRect(taskbarHwnd, ref _taskbarFrameElement, "TaskbarFrame", out bounds))
        {
            return true;
        }

        if (!Win32.GetWindowRect(taskbarHwnd, out var rect))
        {
            return false;
        }

        bounds = ToWpfRect(rect);
        return true;
    }

    public static bool TryGetClockRect(IntPtr taskbarHwnd, out Rect bounds)
    {
        bounds = Rect.Empty;
        if (taskbarHwnd == IntPtr.Zero)
        {
            return false;
        }

        foreach (var automationId in ClockAutomationIds)
        {
            if (automationId == "NotificationCenterButton")
            {
                continue;
            }

            AutomationElement? elementCache = null;
            if (TryGetElementRect(taskbarHwnd, ref elementCache, automationId, out bounds))
            {
                _clockElement = elementCache;
                return true;
            }
        }

        if (TryFindClockButtonByName(taskbarHwnd, out bounds))
        {
            return true;
        }

        if (TryFindRightmostTrayTextBounds(taskbarHwnd, out bounds))
        {
            return true;
        }

        if (TaskbarInterop.TryGetTrayClockRect(out var clockRect))
        {
            bounds = ToWpfRect(clockRect);
            return true;
        }

        return false;
    }

    public static void ResetCache()
    {
        _taskbarFrameElement = null;
        _clockElement = null;
    }

    private static bool TryGetElementRect(
        IntPtr taskbarHwnd,
        ref AutomationElement? cache,
        string automationId,
        out Rect bounds)
    {
        bounds = Rect.Empty;

        try
        {
            if (cache is null)
            {
                if (!TryRunWithTimeout(() =>
                {
                    var root = AutomationElement.FromHandle(taskbarHwnd);
                    return root.FindFirst(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
                }, out var found))
                {
                    return false;
                }

                cache = found;
            }

            var element = cache;
            if (element is null)
            {
                return false;
            }

            if (!TryRunWithTimeout(() => element.Current.BoundingRectangle, out var foundBounds))
            {
                cache = null;
                return false;
            }

            bounds = foundBounds;
            if (bounds == Rect.Empty)
            {
                cache = null;
                return false;
            }

            return true;
        }
        catch (ElementNotAvailableException)
        {
            cache = null;
            return false;
        }
        catch (COMException)
        {
            cache = null;
            return false;
        }
    }

    private static bool TryFindClockButtonByName(IntPtr taskbarHwnd, out Rect bounds)
    {
        bounds = Rect.Empty;

        try
        {
            if (!TryRunWithTimeout(() =>
            {
                var root = AutomationElement.FromHandle(taskbarHwnd);
                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "ClockButton"),
                    new PropertyCondition(AutomationElement.NameProperty, "Clock"),
                    new PropertyCondition(AutomationElement.NameProperty, "Date and time"));

                return root.FindFirst(TreeScope.Descendants, condition);
            }, out var match))
            {
                return false;
            }

            if (match is null)
            {
                return false;
            }

            if (!TryRunWithTimeout(() => match.Current.BoundingRectangle, out var foundBounds))
            {
                return false;
            }

            bounds = foundBounds;
            return bounds != Rect.Empty;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFindRightmostTrayTextBounds(IntPtr taskbarHwnd, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (!TaskbarInterop.TryGetTrayClockRect(out var estimatedClock))
        {
            return false;
        }

        try
        {
            if (!TryRunWithTimeout(() =>
            {
                var root = AutomationElement.FromHandle(taskbarHwnd);
                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

                return root.FindAll(TreeScope.Descendants, condition);
            }, out var matches))
            {
                return false;
            }

            Rect? best = null;
            var bestRight = double.MinValue;
            var trayLeft = estimatedClock.Left - 260;

            for (var i = 0; i < matches.Count; i++)
            {
                var element = matches[i];
                Rect rect;
                try
                {
                    rect = element.Current.BoundingRectangle;
                }
                catch (ElementNotAvailableException)
                {
                    continue;
                }

                if (rect == Rect.Empty || rect.Width < 40 || rect.Width > 180 || rect.Height < 14 || rect.Height > 60)
                {
                    continue;
                }

                if (rect.Left < trayLeft || rect.Right < estimatedClock.Left - 20)
                {
                    continue;
                }

                if (rect.Right <= bestRight)
                {
                    continue;
                }

                bestRight = rect.Right;
                best = rect;
            }

            if (best is null)
            {
                return false;
            }

            bounds = best.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Rect ToWpfRect(Win32.Rect rect) =>
        new(rect.Left, rect.Top, rect.Width, rect.Height);

    private static bool TryRunWithTimeout<T>(Func<T> action, out T result)
    {
        result = default!;
        try
        {
            var task = Task.Run(action);
            if (!task.Wait(AutomationTimeout))
            {
                return false;
            }

            result = task.GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
