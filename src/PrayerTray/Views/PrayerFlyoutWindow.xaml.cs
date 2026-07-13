using System.Drawing;
using System.Windows;
using PrayerTray.Services;
using PrayerTray.ViewModels;

namespace PrayerTray.Views;

public partial class PrayerFlyoutWindow : TaskbarAnchoredFlyoutWindow
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? RefreshLocationRequested;

    public PrayerFlyoutWindow(FlyoutViewModel viewModel, LocalizationService localization)
    {
        InitializeComponent();
        DataContext = viewModel;
        BindLocalization(localization);
    }

    public void ShowNear(Window anchor)
    {
        RefreshBinding();
        ShowAboveSlot(new Rectangle(
            (int)anchor.Left,
            (int)anchor.Top,
            (int)anchor.Width,
            (int)anchor.Height));
    }

    public void ShowNear(Rectangle anchor)
    {
        RefreshBinding();
        ShowAboveSlot(anchor);
    }

    public void ShowNearScreen() => ShowAboveSlot();

    private void RefreshBinding()
    {
        if (DataContext is FlyoutViewModel vm)
        {
            vm.Refresh();
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
        HideFlyout();
    }

    private void OnRefreshLocationClick(object sender, RoutedEventArgs e)
    {
        RefreshLocationRequested?.Invoke(this, EventArgs.Empty);
    }
}
