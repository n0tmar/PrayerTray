using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PrayerTray.Native;
using PrayerTray.Views;
using WpfApplication = System.Windows.Application;

namespace PrayerTray.Services;

public sealed class FlyoutDismissService : IDisposable
{
    private const int OpenGracePeriodMs = 300;
    private const int SlotPaddingPixels = 4;

    private readonly Dispatcher _dispatcher;
    private readonly HashSet<TaskbarAnchoredFlyoutWindow> _openFlyouts = [];
    private IntPtr _hookHandle = IntPtr.Zero;
    private Win32.LowLevelMouseProc? _hookProc;
    private long _suppressDismissUntilTick;
    private bool _disposed;

    public event Action? DismissRequested;

    public FlyoutDismissService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void NotifyFlyoutOpened(TaskbarAnchoredFlyoutWindow flyout)
    {
        _openFlyouts.Add(flyout);
        _suppressDismissUntilTick = Environment.TickCount64 + OpenGracePeriodMs;
        EnsureHookInstalled();
    }

    public void NotifyFlyoutClosed(TaskbarAnchoredFlyoutWindow flyout)
    {
        _openFlyouts.Remove(flyout);
        if (_openFlyouts.Count == 0)
        {
            RemoveHook();
        }
    }

    private void EnsureHookInstalled()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookProc = MouseHookCallback;
        _hookHandle = Win32.SetWindowsHookEx(
            Win32.WhMouseLl,
            _hookProc,
            Win32.GetModuleHandle(null),
            0);
    }

    private void RemoveHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        Win32.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc = null;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsMouseDownMessage(wParam) && _openFlyouts.Count > 0)
        {
            var info = Marshal.PtrToStructure<Win32.MsLlHookStruct>(lParam);
            if (!ShouldIgnoreDismiss(info.Pt.X, info.Pt.Y))
            {
                _dispatcher.BeginInvoke(() =>
                {
                    if (_openFlyouts.Count > 0)
                    {
                        DismissRequested?.Invoke();
                    }
                });
            }
        }

        return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool ShouldIgnoreDismiss(int x, int y)
    {
        if (Environment.TickCount64 < _suppressDismissUntilTick)
        {
            return true;
        }

        if (IsPointOverFlyoutUi(x, y))
        {
            return true;
        }

        if (IsPointOverTaskbarSlot(x, y))
        {
            return true;
        }

        return false;
    }

    private static bool IsMouseDownMessage(IntPtr wParam) =>
        wParam == (IntPtr)Win32.WmLbuttonDown
        || wParam == (IntPtr)Win32.WmRbuttonDown
        || wParam == (IntPtr)Win32.WmMbuttonDown;

    private bool IsPointOverFlyoutUi(int x, int y)
    {
        var hasOpenFlyout = false;

        foreach (Window window in WpfApplication.Current.Windows)
        {
            if (window is TaskbarAnchoredFlyoutWindow flyout && flyout.IsFlyoutOpen)
            {
                hasOpenFlyout = true;
                if (HasOpenDropdown(flyout))
                {
                    return true;
                }

                if (IsPointInsideWindow(window, x, y))
                {
                    return true;
                }
            }
        }

        if (!hasOpenFlyout)
        {
            return false;
        }

        foreach (Window window in WpfApplication.Current.Windows)
        {
            if (window is TaskbarAnchoredFlyoutWindow || !window.IsVisible)
            {
                continue;
            }

            if (window is TaskbarWidgetWindow)
            {
                continue;
            }

            if (IsPointInsideWindow(window, x, y))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOpenDropdown(DependencyObject root)
    {
        try
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.ComboBox { IsDropDownOpen: true })
                {
                    return true;
                }

                if (HasOpenDropdown(child))
                {
                    return true;
                }
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return false;
    }

    private static bool IsPointInsideWindow(Window window, int x, int y)
    {
        if (window.ActualWidth <= 0 && window.Width <= 0)
        {
            return false;
        }

        if (window.ActualHeight <= 0 && window.Height <= 0)
        {
            return false;
        }

        var topLeft = window.PointToScreen(new System.Windows.Point(0, 0));
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        var bounds = new Rectangle(
            (int)Math.Floor(topLeft.X),
            (int)Math.Floor(topLeft.Y),
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height));

        return bounds.Contains(x, y);
    }

    private static bool IsPointOverTaskbarSlot(int x, int y)
    {
        if (!TaskbarFlyoutPosition.TryGetSlotAnchor(out var anchor))
        {
            return false;
        }

        anchor.Inflate(SlotPaddingPixels, SlotPaddingPixels);
        return anchor.Contains(x, y);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _openFlyouts.Clear();
        RemoveHook();
    }
}
