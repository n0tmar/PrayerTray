using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PrayerTray.Native;

public static class FlyoutWindowHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point lpPoint);

    public static void ConfigureNoActivate(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            Win32.SetWindowLong(handle, Win32.GwlExstyle,
                Win32.GetWindowLong(handle, Win32.GwlExstyle) | Win32.WsExNoactivate | Win32.WsExToolwindow);
        };
    }

    public static void ApplyFlyoutChrome(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var dark = 1;
        Win32.DwmSetWindowAttribute(handle, Win32.DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

        var backdrop = 4;
        Win32.DwmSetWindowAttribute(handle, Win32.DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    public static void EnsureVisible(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SwShowNoActivate);
        Win32.SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            Win32.SwpNomove | Win32.SwpNosize | Win32.SwpNozorder | Win32.SwpNoactivate | Win32.SwpShowwindow);
    }

    public static void RefreshTopmost(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        window.Topmost = false;
        window.Topmost = true;
        Win32.SetWindowPos(
            handle,
            Win32.HwndTopmost,
            0,
            0,
            0,
            0,
            Win32.SwpNomove | Win32.SwpNosize | Win32.SwpNoactivate | Win32.SwpShowwindow);
    }

    public static void SetScreenPosition(Window window, double x, double y)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.Left = x;
            window.Top = y;
            return;
        }

        Win32.SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(x), (int)Math.Round(y), 0, 0,
            Win32.SwpNosize | Win32.SwpNozorder | Win32.SwpNoactivate | Win32.SwpShowwindow);
        window.Left = x;
        window.Top = y;
    }

    public static bool IsMouseOverWindow(Window window)
    {
        if (!GetCursorPos(out var cursor))
        {
            return false;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !Win32.GetWindowRect(handle, out var rect))
        {
            return false;
        }

        return cursor.X >= rect.Left && cursor.X <= rect.Right &&
               cursor.Y >= rect.Top && cursor.Y <= rect.Bottom;
    }

    public static bool IsMouseOverRectangle(System.Drawing.Rectangle bounds)
    {
        if (!GetCursorPos(out var cursor))
        {
            return false;
        }

        return cursor.X >= bounds.Left && cursor.X <= bounds.Right &&
               cursor.Y >= bounds.Top && cursor.Y <= bounds.Bottom;
    }
}
