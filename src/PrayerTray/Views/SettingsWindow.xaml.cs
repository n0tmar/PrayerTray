using System.Diagnostics;
using System.Drawing;
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

    private void OnSupportButtonClick(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenSupportPage();
        e.Handled = true;
    }

    private static void OpenSupportPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SupportUrl,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
