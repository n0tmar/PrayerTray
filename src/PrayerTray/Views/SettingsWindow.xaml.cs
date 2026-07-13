using System.Diagnostics;
using System.Drawing;
using System.Windows.Navigation;
using PrayerTray.Services;
using PrayerTray.ViewModels;

namespace PrayerTray.Views;

public partial class SettingsWindow : TaskbarAnchoredFlyoutWindow
{
    public event EventHandler? SettingsSaved;

    private readonly SettingsViewModel _viewModel;
    private const string SupportUrl = "https://www.patreon.com/n0tmar";

    protected override bool AllowActivation => true;

    public SettingsWindow(SettingsViewModel viewModel, LocalizationService localization)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        BindLocalization(localization);
    }

    public void ShowNear(Rectangle anchor)
    {
        _viewModel.LoadFromSettings();
        ShowAboveSlot(anchor);
    }

    public void ShowNearScreen()
    {
        _viewModel.LoadFromSettings();
        ShowAboveSlot();
    }

    private void OnSaveClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.TrySave())
        {
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            HideFlyout();
        }
    }

    private void OnCancelClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.LoadFromSettings();
        HideFlyout();
    }

    private void OnSupportLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        var target = string.IsNullOrWhiteSpace(e.Uri?.AbsoluteUri)
            ? SupportUrl
            : e.Uri.AbsoluteUri;

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });

        e.Handled = true;
    }
}
