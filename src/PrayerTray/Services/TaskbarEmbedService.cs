using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using PrayerTray.Native;
using PrayerTray.Views;

namespace PrayerTray.Services;

public sealed class TaskbarEmbedService : IDisposable
{
    private const uint SwpAsyncwindowpos = 0x4000;
    private const int GapPixels = 4;

    private readonly DispatcherTimer _timer;
    private readonly Win32.WinEventDelegate _winEventDelegate;
    private IntPtr _winEventHook;
    private TaskbarWidgetWindow? _window;
    private bool _embedded;
    private bool _repositionQueued;

    public TaskbarEmbedService()
    {
        _winEventDelegate = OnWinEvent;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += (_, _) => Reposition();
    }

    public void Attach(TaskbarWidgetWindow window)
    {
        if (ReferenceEquals(_window, window) && _timer.IsEnabled)
        {
            Reposition();
            return;
        }

        if (_window is not null && !ReferenceEquals(_window, window))
        {
            _window.SourceInitialized -= OnSourceInitialized;
        }

        _window = window;
        window.SourceInitialized -= OnSourceInitialized;
        window.SourceInitialized += OnSourceInitialized;

        if (window.IsLoaded)
        {
            TryEmbed();
            Reposition();
        }

        _timer.Start();
        HookTaskbarEvents();
    }

    public void Detach()
    {
        _timer.Stop();
        _repositionQueued = false;

        if (_window is not null)
        {
            _window.SourceInitialized -= OnSourceInitialized;
        }

        if (_winEventHook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }

        _window = null;
        _embedded = false;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        TryEmbed();
        Reposition();
    }

    private void HookTaskbarEvents()
    {
        if (_winEventHook != IntPtr.Zero)
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
        if (_window is null || !_timer.IsEnabled || _repositionQueued)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _repositionQueued = true;
        dispatcher.BeginInvoke(() =>
        {
            try
            {
                TaskbarAutomation.ResetCache();
                Reposition();
            }
            finally
            {
                _repositionQueued = false;
            }
        }, DispatcherPriority.Background);
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

        if (!_embedded)
        {
            TryEmbed();
        }

        var taskbar = TaskbarInterop.GetShellTrayHandle();
        if (taskbar == IntPtr.Zero)
        {
            return;
        }

        if (!TryGetTaskbarRects(taskbar, out var frameRect, out var clockRect))
        {
            return;
        }

        _window.UpdateContentMetrics();
        _window.UpdateLayout();

        var widgetWidth = _window.ContentWidth;
        var widgetHeight = _window.ContentHeight;
        var widgetLeft = clockRect.Left - frameRect.Left - GapPixels - widgetWidth;
        var widgetTop = (frameRect.Height - widgetHeight) / 2;

        Win32.Rect notifyRect = default;
        var notify = TaskbarInterop.GetTrayNotifyHandle();
        if (notify != IntPtr.Zero && Win32.GetWindowRect(notify, out notifyRect))
        {
            var proposedScreenWidgetLeft = frameRect.Left + widgetLeft;
            var proposedScreenWidgetRight = proposedScreenWidgetLeft + widgetWidth;
            var overlapsNotify = proposedScreenWidgetLeft < notifyRect.Right && proposedScreenWidgetRight > notifyRect.Left;
            if (overlapsNotify)
            {
                widgetLeft = notifyRect.Left - frameRect.Left - GapPixels - widgetWidth;
            }
        }

        widgetLeft = Math.Max(0, widgetLeft);
        widgetTop = Math.Max(0, widgetTop);

        var containerPos = new Win32.Point { X = (int)frameRect.Left, Y = (int)frameRect.Top };
        Win32.ScreenToClient(taskbar, ref containerPos);

        Win32.SetWindowPos(
            handle,
            IntPtr.Zero,
            containerPos.X,
            containerPos.Y,
            (int)frameRect.Width,
            (int)frameRect.Height,
            Win32.SwpNoactivate | SwpAsyncwindowpos | Win32.SwpShowwindow);

        _window.SetWidgetPlacement(widgetLeft, widgetTop, widgetWidth, widgetHeight);

        Win32.SetWindowRgnRect(
            handle,
            (int)Math.Round(widgetLeft),
            (int)Math.Round(widgetTop),
            (int)Math.Round(widgetLeft + widgetWidth),
            (int)Math.Round(widgetTop + widgetHeight));
    }

    private void TryEmbed()
    {
        if (_window is null)
        {
            return;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var taskbar = TaskbarInterop.GetShellTrayHandle();
        if (taskbar == IntPtr.Zero)
        {
            return;
        }

        var style = Win32.GetWindowLong(handle, Win32.GwlStyle);
        style &= ~Win32.WsPopup;
        style |= Win32.WsChild | Win32.WsVisible | Win32.WsClipsiblings;
        Win32.SetWindowLong(handle, Win32.GwlStyle, style);

        Win32.SetParent(handle, taskbar);
        _embedded = true;
    }

    private static bool TryGetTaskbarRects(IntPtr taskbar, out Rect frameRect, out Rect clockRect)
    {
        frameRect = Rect.Empty;
        clockRect = Rect.Empty;

        if (Win32.GetWindowRect(taskbar, out var shellRect)
            && TaskbarInterop.TryGetTrayClockRect(out var nativeClockRect))
        {
            frameRect = ToWpfRect(shellRect);
            clockRect = ToWpfRect(nativeClockRect);
            return true;
        }

        return TaskbarAutomation.TryGetTaskbarFrameRect(taskbar, out frameRect)
               && TaskbarAutomation.TryGetClockRect(taskbar, out clockRect);
    }

    private static Rect ToWpfRect(Win32.Rect rect) =>
        new(rect.Left, rect.Top, rect.Width, rect.Height);

    public void OnTaskbarRecreated()
    {
        _embedded = false;
        TaskbarAutomation.ResetCache();
        TryEmbed();
        Reposition();
    }

    public void Dispose()
    {
        Detach();
    }
}
