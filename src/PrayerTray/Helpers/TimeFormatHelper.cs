using System.Globalization;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.Helpers;

public static class TimeFormatHelper
{
    private const char LeftToRightEmbedding = '\u202A';
    private const char PopDirectionalFormatting = '\u202C';

    public static string FormatTime(DateTime time, LocalizationService? localization = null) =>
        FormatTime(time, TimeFormats.TwelveHour, localization);

    public static string FormatTime(DateTime time, string? timeFormat, LocalizationService? localization = null)
    {
        if (TimeFormats.Normalize(timeFormat) == TimeFormats.TwentyFourHour)
        {
            var twentyFourHourText = time.ToString("HH:mm", CultureInfo.InvariantCulture).Replace(':', '\u2236');
            return StabilizeCompactText(twentyFourHourText, localization);
        }

        var hour = time.Hour % 12;
        if (hour == 0)
        {
            hour = 12;
        }

        var period = time.Hour < 12
            ? localization?.Get("TimePeriod_AM") ?? "AM"
            : localization?.Get("TimePeriod_PM") ?? "PM";

        var twelveHourText = string.Concat(
            hour.ToString(CultureInfo.InvariantCulture),
            '\u2236',
            time.Minute.ToString("00", CultureInfo.InvariantCulture),
            " ",
            period);

        return StabilizeCompactText(twelveHourText, localization);
    }

    public static string FormatCountdown(TimeSpan remaining, LocalizationService? localization = null)
    {
        var loc = localization;
        remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;

        string text;
        if (remaining.TotalSeconds < 60)
        {
            var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            text = loc is null ? $"{seconds}s" : loc.Format("Countdown_Seconds", seconds);
            return StabilizeCompactText(text, loc);
        }

        if (remaining.TotalHours < 1)
        {
            var minutes = (int)remaining.TotalMinutes;
            text = loc is null ? $"{minutes}m" : loc.Format("Countdown_Minutes", minutes);
            return StabilizeCompactText(text, loc);
        }

        var hours = (int)remaining.TotalHours;
        var mins = remaining.Minutes;
        if (mins == 0)
        {
            text = loc is null ? $"{hours}h" : loc.Format("Countdown_Hours", hours);
            return StabilizeCompactText(text, loc);
        }

        text = loc is null ? $"{hours}h {mins}m" : loc.Format("Countdown_HoursMinutes", hours, mins);
        return StabilizeCompactText(text, loc);
    }

    private static string StabilizeCompactText(string text, LocalizationService? localization) =>
        localization?.IsRightToLeft == true
            ? string.Concat(LeftToRightEmbedding, text, PopDirectionalFormatting)
            : text;
}
