namespace PrayerTray.Models;

public static class TaskbarContentModes
{
    public const string Time = "time";
    public const string Countdown = "countdown";
    public const string Smart = "smart";

    public static IReadOnlyList<string> All { get; } = [Time, Countdown, Smart];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Countdown;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return All.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : Countdown;
    }
}
