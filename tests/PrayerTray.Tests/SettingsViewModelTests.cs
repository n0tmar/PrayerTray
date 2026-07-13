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

    [Fact]
    public void PlayPrayerNotification_PreviewsSoundWhenTurnedOn()
    {
        var settings = new SettingsService();
        var localization = new LocalizationService();
        var player = new FakePrayerNotificationPlayer();
        localization.Initialize("en");
        using var viewModel = new SettingsViewModel(settings, localization, player);

        viewModel.PlayPrayerNotification = true;

        Assert.Equal(1, player.PlayCount);
    }

    [Fact]
    public void LoadFromSettings_DoesNotPreviewPrayerNotification()
    {
        var settings = new SettingsService();
        settings.Settings.PlayPrayerNotification = true;
        var localization = new LocalizationService();
        var player = new FakePrayerNotificationPlayer();
        localization.Initialize("en");

        using var viewModel = new SettingsViewModel(settings, localization, player);

        Assert.True(viewModel.PlayPrayerNotification);
        Assert.Equal(0, player.PlayCount);
    }

    private sealed class FakePrayerNotificationPlayer : IPrayerNotificationPlayer
    {
        public int PlayCount { get; private set; }

        public void Play() => PlayCount++;

        public void Dispose()
        {
        }
    }
}
