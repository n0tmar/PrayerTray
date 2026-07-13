using PrayerTray.Helpers;

namespace PrayerTray.Tests;

public sealed class TimeFormatHelperTests
{
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
