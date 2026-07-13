namespace PrayerTray.Models;

public static class TimeFormats
{
    public const string TwentyFourHour = "24h";
    public const string TwelveHour = "12h";

    public static IReadOnlyList<string> All { get; } = [TwentyFourHour, TwelveHour];

    public static string Normalize(string? value) =>
        string.Equals(value, TwentyFourHour, StringComparison.OrdinalIgnoreCase)
            ? TwentyFourHour
            : TwelveHour;
}
