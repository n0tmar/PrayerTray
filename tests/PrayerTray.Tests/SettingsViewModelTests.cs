using PrayerTray.Services;
using PrayerTray.ViewModels;

namespace PrayerTray.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void SelectedLanguage_DoesNotApplyLanguageBeforeSave()
    {
        var settings = new SettingsService();
        var localization = new LocalizationService();
        localization.Initialize("en");
        using var viewModel = new SettingsViewModel(settings, localization);

        viewModel.SelectedLanguage = "ar";

        Assert.Equal("ar", viewModel.SelectedLanguage);
        Assert.Equal("en", localization.CurrentLanguage);
    }
}
