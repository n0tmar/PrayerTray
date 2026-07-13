using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using PrayerTray.Native;

namespace PrayerTray.Services;

public sealed class TaskbarAnchorService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Win32.WinEventDelegate _winEventDelegate;
    private IntPtr _winEventHook;
    private Window? _window;
    private double _widgetWidth;
    private double _widgetHeight;

    public TaskbarAnchorService()
    {
        _winEventDelegate = OnWinEvent;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += (_, _) => Reposition();
    }

    public void Attach(Window window, double widgetWidth, double widgetHeight)
    {
        _window = window;
        _widgetWidth = Math.Max(widgetWidth, 48);
        _widgetHeight = Math.Max(widgetHeight, 28);

        window.SourceInitialized -= OnSourceInitialized;
        window.SourceInitialized += OnSourceInitialized;

        if (window.IsLoaded)
        {
            ConfigureOverlayWindow();
            Reposition();
        }

        _timer.Start();
        HookTaskbarEvents();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ConfigureOverlayWindow();
        Reposition();
    }

    private static void ConfigureOverlayWindow()
    {
        // Window styles are managed through WPF (Topmost popup overlay).
    }

    private void HookTaskbarEvents()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            return;
        }

        var shellTray = TaskbarInterop.GetShellTrayHandle();
        if (shellTray == IntPtr.Zero)
        {
            return;
        }

        _winEventHook = Win32.SetWinEventHook(
            Win32.EventObjectLocationChange,
            Win32.EventObjectLocationChange,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            Win32.WineventOutofcontext);
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(Reposition);
    }

    public void Reposition()
    {
        if (_window is null || !_window.IsVisible)
        {
            return;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (_window.ActualWidth > 0)
        {
            _widgetWidth = _window.ActualWidth;
        }

        if (_window.ActualHeight > 0)
        {
            _widgetHeight = _window.ActualHeight;
        }

        _window.UpdateLayout();

        var dpiScale = GetDpiScale(_window);
        var width = (int)Math.Ceiling(_widgetWidth * dpiScale);
        var height = (int)Math.Ceiling(_widgetHeight * dpiScale);

        if (!TaskbarInterop.TryGetWidgetScreenPosition(width, height, 6, out var screenX, out var screenY))
        {
            return;
        }

        _window.Left = screenX / dpiScale;
        _window.Top = screenY / dpiScale;

        Win32.SetWindowPos(
            handle,
            Win32.HwndTopmost,
            screenX,
            screenY,
            width,
            height,
            Win32.SwpNoactivate | Win32.SwpShowwindow);
    }

    private static double GetDpiScale(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            return 1.0;
        }

        return source.CompositionTarget.TransformToDevice.M11;
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_winEventHook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }
}
