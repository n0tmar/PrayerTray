using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PrayerTray.ViewModels;

namespace PrayerTray.Views;

public partial class TaskbarWidgetWindow : Window
{
    public event EventHandler? OpenFlyoutRequested;
    public event EventHandler? OpenContextMenuRequested;

    public double ContentWidth { get; private set; } = 52;
    public double ContentHeight { get; private set; } = 30;

    public TaskbarWidgetWindow(TaskbarWidgetViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case 0x003D: // WM_GETOBJECT
            case 0x0046: // WM_WINDOWPOSCHANGING
            case 0x0083: // WM_NCCALCSIZE
                handled = true;
                return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TaskbarWidgetViewModel vm)
        {
            ApplyTextColor(vm.TextColor);
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(TaskbarWidgetViewModel.TextColor))
                {
                    ApplyTextColor(vm.TextColor);
                }
            };
        }

        UpdateContentMetrics();
    }

    public void SetWidgetPlacement(double left, double top, double width, double height)
    {
        Width = Math.Max(width + left + 8, width);
        Height = Math.Max(height + top + 8, height);
        Canvas.SetLeft(WidgetHost, left);
        Canvas.SetTop(WidgetHost, top);
        WidgetHost.Width = width;
        WidgetHost.Height = height;
        ContentWidth = width;
        ContentHeight = height;
    }

    public void UpdateContentMetrics()
    {
        TopLineText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        BottomLineText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));

        var topWidth = TopLineText.DesiredSize.Width;
        var bottomWidth = BottomLineText.DesiredSize.Width;
        var measuredWidth = Math.Max(topWidth, bottomWidth) + 4;
        var measuredHeight = Math.Max(TopLineText.DesiredSize.Height + BottomLineText.DesiredSize.Height, 28);

        ContentWidth = Math.Max(measuredWidth, 52);
        ContentHeight = Math.Max(measuredHeight, 30);

    }

    private void ApplyTextColor(string colorHex)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
        TopLineText.Foreground = brush;
        BottomLineText.Foreground = brush;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        OpenFlyoutRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        OpenContextMenuRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
