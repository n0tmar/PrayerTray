using PrayerTray.Helpers;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.Tests;

public sealed class TimeFormatHelperTests
{
    [Fact]
    public void TimeFormats_ListTwelveHourBeforeTwentyFourHour()
    {
        Assert.Equal(
            [TimeFormats.TwelveHour, TimeFormats.TwentyFourHour],
            TimeFormats.All);
    }

    [Fact]
    public void AppSettings_DefaultsToTwelveHourClock()
    {
        var settings = new AppSettings();

        Assert.Equal(TimeFormats.TwelveHour, settings.TimeFormat);
    }

    [Fact]
    public void FormatTime_UsesTwentyFourHourClockWhenSelected()
    {
        var text = TimeFormatHelper.FormatTime(
            new DateTime(2026, 7, 13, 17, 4, 0),
            TimeFormats.TwentyFourHour);

        Assert.Equal("17∶04", text);
    }

    [Fact]
    public void FormatTime_UsesTwelveHourClockWhenSelected()
    {
        var localization = new LocalizationService();
        localization.Initialize("en");

        var text = TimeFormatHelper.FormatTime(
            new DateTime(2026, 7, 13, 17, 4, 0),
            TimeFormats.TwelveHour,
            localization);

        Assert.Equal("5∶04 PM", text);
    }

    [Fact]
    public void FormatTime_FallsBackToTwelveHourClock()
    {
        var localization = new LocalizationService();
        localization.Initialize("en");

        var text = TimeFormatHelper.FormatTime(
            new DateTime(2026, 7, 13, 5, 4, 0),
            "bad-value",
            localization);

        Assert.Equal("5∶04 AM", text);
    }

    [Theory]
    [InlineData("en", "5∶04 PM")]
    [InlineData("ar", "\u202A5∶04 \u0645\u202C")]
    [InlineData("fr", "5∶04 PM")]
    [InlineData("ur", "\u202A5∶04 \u0628.\u0638\u202C")]
    [InlineData("tr", "5∶04 ÖS")]
    [InlineData("id", "5∶04 PM")]
    public void FormatTime_UsesLocalizedTwelveHourPeriod(string languageCode, string expected)
    {
        var localization = new LocalizationService();
        localization.Initialize(languageCode);

        var text = TimeFormatHelper.FormatTime(
            new DateTime(2026, 7, 13, 17, 4, 0),
            TimeFormats.TwelveHour,
            localization);

        Assert.Equal(expected, text);
    }

    [Fact]
    public void FormatTime_UsesBidiSafeTextForRightToLeftTwentyFourHourClock()
    {
        var localization = new LocalizationService();
        localization.Initialize("ar");

        var text = TimeFormatHelper.FormatTime(
            new DateTime(2026, 7, 13, 17, 4, 0),
            TimeFormats.TwentyFourHour,
            localization);

        Assert.Equal("\u202A17∶04\u202C", text);
    }

    [Fact]
    public void FormatCountdown_ClampsNegativeTimeToZeroSeconds()
    {
        var text = TimeFormatHelper.FormatCountdown(TimeSpan.FromSeconds(-5));

        Assert.Equal("0s", text);
    }

    [Theory]
    [InlineData(45, "45s")]
    [InlineData(15 * 60, "15m")]
    [InlineData(2 * 60 * 60, "2h")]
    [InlineData((2 * 60 * 60) + (5 * 60), "2h 5m")]
    public void FormatCountdown_UsesCompactClockText(int seconds, string expected)
    {
        var text = TimeFormatHelper.FormatCountdown(TimeSpan.FromSeconds(seconds));

        Assert.Equal(expected, text);
    }

    [Fact]
    public void FormatCountdown_UsesBidiSafeTextForRightToLeftLanguages()
    {
        var localization = new LocalizationService();
        localization.Initialize("ar");

        var text = TimeFormatHelper.FormatCountdown(TimeSpan.FromMinutes(15), localization);

        Assert.Equal("\u202A15 \u062F\u202C", text);
    }
}
