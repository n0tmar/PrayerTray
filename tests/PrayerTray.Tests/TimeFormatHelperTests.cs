using PrayerTray.Helpers;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.Tests;

public sealed class TimeFormatHelperTests
{
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
}
