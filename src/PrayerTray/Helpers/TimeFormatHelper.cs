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
        if (TimeFormats.Normalize(timeFormat) == TimeFormats.TwentyFourHour)
        {
            return time.ToString("HH:mm", CultureInfo.InvariantCulture).Replace(':', '\u2236');
        }

        var hour = time.Hour % 12;
        if (hour == 0)
        {
            hour = 12;
        }

        var period = time.Hour < 12
            ? localization?.Get("TimePeriod_AM") ?? "AM"
            : localization?.Get("TimePeriod_PM") ?? "PM";

        return string.Concat(
            hour.ToString(CultureInfo.InvariantCulture),
            '\u2236',
            time.Minute.ToString("00", CultureInfo.InvariantCulture),
            " ",
            period);
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
