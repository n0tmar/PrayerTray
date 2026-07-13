namespace PrayerTray.Models;

public enum PrayerName
{
    Fajr,
    Dhuhr,
    Asr,
    Maghrib,
    Isha
}

public sealed class PrayerEntry
{
    public PrayerName Name { get; init; }
    public DateTime Time { get; init; }
    public bool IsNext { get; set; }
    public bool IsCurrent { get; set; }

    public string DisplayName => Name.ToString();
}

public sealed class PrayerSchedule
{
    public DateOnly Date { get; init; }
    public LocationInfo Location { get; init; } = new();
    public IReadOnlyList<PrayerEntry> Prayers { get; init; } = Array.Empty<PrayerEntry>();
    public DateTime FetchedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class NextPrayerInfo
{
    public PrayerName Name { get; init; }
    public DateTime Time { get; init; }
    public TimeSpan TimeUntil { get; init; }
    public bool IsNow { get; init; }
}
