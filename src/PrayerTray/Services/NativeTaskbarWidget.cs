using System.ComponentModel;
using System.Windows.Forms;
using PrayerTray.Native;
using PrayerTray.ViewModels;

namespace PrayerTray.Services;

public sealed class NativeTaskbarWidget : NativeWindow, IDisposable
{
    private const int WmPaint = 0x000F;
    private const int WmErasebkgnd = 0x0014;
    private const int WmLbuttonUp = 0x0202;
    private const int WmRbuttonUp = 0x0205;
    private const int SwShow = 5;
    private const int SwHide = 0;
    private const int GapPixels = 6;
    private const int WidgetWidth = 88;
    private const int WidgetHeight = 48;

    private readonly TaskbarWidgetViewModel _viewModel;
    private readonly System.Windows.Threading.DispatcherTimer _repositionTimer;
    private readonly PropertyChangedEventHandler _onViewModelChanged;
    private IntPtr _shellTrayHwnd;
    private bool _embedded;
    private bool _isVisible;
    private bool _hasHandle;

    public event EventHandler? OpenFlyoutRequested;
    public event EventHandler? OpenContextMenuRequested;

    public NativeTaskbarWidget(TaskbarWidgetViewModel viewModel)
    {
        _viewModel = viewModel;
        _onViewModelChanged = (_, _) => Invalidate();
        _viewModel.PropertyChanged += _onViewModelChanged;

        _repositionTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _repositionTimer.Tick += (_, _) => Reposition();
    }

    public void ShowWidget()
    {
        if (!_hasHandle)
        {
            CreateWidgetHandle();
        }

        _isVisible = true;
        Win32.ShowWindow(Handle, SwShow);
        Reposition();
        Invalidate();
        _repositionTimer.Start();
    }

    public void HideWidget()
    {
        _isVisible = false;
        _repositionTimer.Stop();
        if (_hasHandle)
        {
            Win32.ShowWindow(Handle, SwHide);
        }
    }

    public bool IsWidgetVisible => _isVisible && _hasHandle;

    public void OnTaskbarRecreated()
    {
        _embedded = false;
        _shellTrayHwnd = IntPtr.Zero;
        Reposition();
        Invalidate();
    }

    public void Reposition()
    {
        if (!_hasHandle || !_isVisible)
        {
            return;
        }

        if (!_embedded)
        {
            TryEmbed();
        }

        if (!TaskbarInterop.TryGetWidgetShellTrayClientPosition(
                _shellTrayHwnd, WidgetWidth, WidgetHeight, GapPixels, out var x, out var y))
        {
            return;
        }

        Gdi32.MoveWindow(Handle, x, y, WidgetWidth, WidgetHeight, true);
        RaiseAboveTaskbarComposition();
        Invalidate();
    }

    private void CreateWidgetHandle()
    {
        _shellTrayHwnd = TaskbarInterop.GetShellTrayHandle();
        var cp = new CreateParams
        {
            Caption = string.Empty,
            ClassName = "Static",
            Style = Win32.WsChild | Win32.WsVisible,
            ExStyle = Win32.WsExNoactivate,
            Parent = _shellTrayHwnd,
            Width = WidgetWidth,
            Height = WidgetHeight
        };

        CreateHandle(cp);
        _hasHandle = true;
        TryEmbed();
        Reposition();
    }

    private void TryEmbed()
    {
        if (!_hasHandle)
        {
            return;
        }

        _shellTrayHwnd = TaskbarInterop.GetShellTrayHandle();
        if (_shellTrayHwnd == IntPtr.Zero)
        {
            return;
        }

        Win32.SetParent(Handle, _shellTrayHwnd);

        var style = Win32.GetWindowLong(Handle, Win32.GwlStyle);
        style &= ~Win32.WsPopup;
        style |= Win32.WsChild | Win32.WsVisible;
        Win32.SetWindowLong(Handle, Win32.GwlStyle, style);

        RaiseAboveTaskbarComposition();
        _embedded = true;
    }

    private void RaiseAboveTaskbarComposition()
    {
        Win32.SetWindowPos(
            Handle,
            Win32.HwndTop,
            0,
            0,
            0,
            0,
            Win32.SwpNomove | Win32.SwpNosize | Win32.SwpNoactivate | Win32.SwpShowwindow);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmErasebkgnd:
                m.Result = new IntPtr(1);
                return;
            case WmPaint:
                PaintWidget();
                m.Result = IntPtr.Zero;
                return;
            case WmLbuttonUp:
                OpenFlyoutRequested?.Invoke(this, EventArgs.Empty);
                return;
            case WmRbuttonUp:
                OpenContextMenuRequested?.Invoke(this, EventArgs.Empty);
                return;
        }

        base.WndProc(ref m);
    }

    private void PaintWidget()
    {
        if (!_hasHandle)
        {
            return;
        }

        var hdc = Gdi32.BeginPaint(Handle, out var ps);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var dpi = Gdi32.GetDpiForWindow(Handle);
            var emSize = 11f * dpi / 96f;

            using var graphics = System.Drawing.Graphics.FromHdc(hdc);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var color = ParseColor(_viewModel.TextColor);
            using var font = CreateTaskbarFont(emSize);
            using var brush = new System.Drawing.SolidBrush(color);
            using var format = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Far,
                LineAlignment = System.Drawing.StringAlignment.Center,
                Trimming = System.Drawing.StringTrimming.EllipsisCharacter,
                FormatFlags = System.Drawing.StringFormatFlags.NoWrap
            };

            var topRect = new System.Drawing.RectangleF(0, 2, WidgetWidth, WidgetHeight / 2f);
            var bottomRect = new System.Drawing.RectangleF(0, WidgetHeight / 2f - 2, WidgetWidth, WidgetHeight / 2f);
            graphics.DrawString(_viewModel.TopLine, font, brush, topRect, format);
            graphics.DrawString(_viewModel.BottomLine, font, brush, bottomRect, format);
        }
        finally
        {
            Gdi32.EndPaint(Handle, ref ps);
        }
    }

    private static System.Drawing.Font CreateTaskbarFont(float emSize)
    {
        try
        {
            return new System.Drawing.Font("Segoe UI Variable", emSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        }
        catch
        {
            return new System.Drawing.Font("Segoe UI", emSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        }
    }

    private void Invalidate()
    {
        if (_hasHandle)
        {
            Gdi32.InvalidateRect(Handle, IntPtr.Zero, true);
        }
    }

    private static System.Drawing.Color ParseColor(string hex)
    {
        try
        {
            return System.Drawing.ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return System.Drawing.Color.White;
        }
    }

    public bool TryGetScreenBounds(out System.Drawing.Rectangle bounds)
    {
        bounds = System.Drawing.Rectangle.Empty;
        if (!_hasHandle || !Win32.GetWindowRect(Handle, out var rect))
        {
            return false;
        }

        bounds = System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return true;
    }

    public void Dispose()
    {
        _repositionTimer.Stop();
        _viewModel.PropertyChanged -= _onViewModelChanged;
        if (_hasHandle)
        {
            DestroyHandle();
            _hasHandle = false;
        }
    }
}
