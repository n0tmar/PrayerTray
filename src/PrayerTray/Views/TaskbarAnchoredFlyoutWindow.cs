using System.Drawing;
using System.Windows;
using PrayerTray.Native;
using PrayerTray.Services;

namespace PrayerTray.Views;

public class TaskbarAnchoredFlyoutWindow : Window
{
    private bool _isOpen;
    private LocalizationService? _localization;

    public bool IsFlyoutOpen => _isOpen;

    public event EventHandler? FlyoutOpened;
    public event EventHandler? FlyoutClosed;

    public void BindLocalization(LocalizationService localization)
    {
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }

        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;
        ApplyFlowDirection();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(ApplyFlowDirection);

    private void ApplyFlowDirection()
    {
        if (_localization is null)
        {
            return;
        }

        FlowDirection = _localization.FlowDirection;
    }

    protected virtual bool AllowActivation => false;

    protected TaskbarAnchoredFlyoutWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20));
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = AllowActivation;

        if (!AllowActivation)
        {
            FlyoutWindowHelper.ConfigureNoActivate(this);
        }

        Loaded += (_, _) =>
        {
            FlyoutWindowHelper.ApplyFlyoutChrome(this);
            ApplyFlowDirection();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                HideFlyout();
            }
        };
    }

    public void ShowAboveSlot()
    {
        if (!TaskbarFlyoutPosition.TryGetSlotAnchor(out var anchor))
        {
            ShowAboveTaskbarFallback();
            return;
        }

        ShowAboveSlot(anchor);
    }

    public void ShowAboveSlot(Rectangle anchor)
    {
        _isOpen = true;
        Visibility = Visibility.Visible;
        Opacity = 1;

        UpdateLayout();
        TaskbarFlyoutPosition.PlaceAboveSlot(this, anchor);
        Show();
        if (AllowActivation)
        {
            Activate();
        }

        FlyoutWindowHelper.EnsureVisible(this);
        FlyoutWindowHelper.RefreshTopmost(this);
        FlyoutOpened?.Invoke(this, EventArgs.Empty);
    }

    public void ShowAboveTaskbarFallback()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }

        var anchor = new Rectangle(
            screen.WorkingArea.Right - (int)Width - 12,
            screen.Bounds.Bottom - 48,
            (int)Width,
            48);
        ShowAboveSlot(anchor);
    }

    public void HideFlyout()
    {
        if (!_isOpen && Visibility != Visibility.Visible)
        {
            return;
        }

        _isOpen = false;
        Visibility = Visibility.Hidden;
        Hide();
        FlyoutClosed?.Invoke(this, EventArgs.Empty);
    }
}
