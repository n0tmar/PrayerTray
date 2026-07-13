using System.Globalization;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.Helpers;

public static class TimeFormatHelper
{
    public static string FormatTime(DateTime time, LocalizationService? localization = null) =>
        FormatTime(time, TimeFormats.TwelveHour, localization);

    public static string FormatTime(DateTime time, string? timeFormat, LocalizationService? localization = null)
    {
        var culture = localization?.Culture ?? CultureInfo.CurrentCulture;
        var pattern = TimeFormats.Normalize(timeFormat) == TimeFormats.TwentyFourHour
            ? "HH:mm"
            : "h:mm tt";
        var formatted = time.ToString(pattern, culture);
        return formatted.Replace(':', '\u2236');
    }

    public static string FormatCountdown(TimeSpan remaining, LocalizationService? localization = null)
    {
        var loc = localization;
        remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;

        if (remaining.TotalSeconds < 60)
        {
            var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            return loc is null ? $"{seconds}s" : loc.Format("Countdown_Seconds", seconds);
        }

        if (remaining.TotalHours < 1)
        {
            var minutes = (int)remaining.TotalMinutes;
            return loc is null ? $"{minutes}m" : loc.Format("Countdown_Minutes", minutes);
        }

        var hours = (int)remaining.TotalHours;
        var mins = remaining.Minutes;
        if (mins == 0)
        {
            return loc is null ? $"{hours}h" : loc.Format("Countdown_Hours", hours);
        }

        return loc is null ? $"{hours}h {mins}m" : loc.Format("Countdown_HoursMinutes", hours, mins);
    }
}
