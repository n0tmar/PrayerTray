using System.Globalization;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.Tests;

public sealed class PrayerNotificationTrackerTests
{
    [Fact]
    public void TryMarkDuePrayer_DoesNothingWhenDisabled()
    {
        var date = new DateOnly(2026, 7, 13);
        var schedule = Schedule(date, (PrayerName.Dhuhr, "12:00"));
        var tracker = new PrayerNotificationTracker();

        var prayer = tracker.TryMarkDuePrayer(
            schedule,
            date.ToDateTime(TimeOnly.Parse("12:00", CultureInfo.InvariantCulture)),
            enabled: false);

        Assert.Null(prayer);
    }

    [Fact]
    public void TryMarkDuePrayer_MarksPrayerOnlyOnce()
    {
        var date = new DateOnly(2026, 7, 13);
        var schedule = Schedule(date, (PrayerName.Dhuhr, "12:00"));
        var tracker = new PrayerNotificationTracker();
        var firstTick = date.ToDateTime(TimeOnly.Parse("12:00:05", CultureInfo.InvariantCulture));
        var secondTick = date.ToDateTime(TimeOnly.Parse("12:00:20", CultureInfo.InvariantCulture));

        var first = tracker.TryMarkDuePrayer(schedule, firstTick, enabled: true);
        var second = tracker.TryMarkDuePrayer(schedule, secondTick, enabled: true);

        Assert.NotNull(first);
        Assert.Equal(PrayerName.Dhuhr, first.Name);
        Assert.Null(second);
    }

    [Fact]
    public void TryMarkDuePrayer_AllowsNextPrayer()
    {
        var date = new DateOnly(2026, 7, 13);
        var schedule = Schedule(
            date,
            (PrayerName.Dhuhr, "12:00"),
            (PrayerName.Asr, "15:25"));
        var tracker = new PrayerNotificationTracker();

        var dhuhr = tracker.TryMarkDuePrayer(
            schedule,
            date.ToDateTime(TimeOnly.Parse("12:00:10", CultureInfo.InvariantCulture)),
            enabled: true);
        var asr = tracker.TryMarkDuePrayer(
            schedule,
            date.ToDateTime(TimeOnly.Parse("15:25:10", CultureInfo.InvariantCulture)),
            enabled: true);

        Assert.Equal(PrayerName.Dhuhr, dhuhr?.Name);
        Assert.Equal(PrayerName.Asr, asr?.Name);
    }

    [Fact]
    public void TryMarkDuePrayer_IgnoresOldPrayerOutsideDueWindow()
    {
        var date = new DateOnly(2026, 7, 13);
        var schedule = Schedule(date, (PrayerName.Dhuhr, "12:00"));
        var tracker = new PrayerNotificationTracker();
        var lateTick = date.ToDateTime(TimeOnly.Parse("12:02:01", CultureInfo.InvariantCulture));

        var prayer = tracker.TryMarkDuePrayer(schedule, lateTick, enabled: true);

        Assert.Null(prayer);
    }

    private static PrayerSchedule Schedule(DateOnly date, params (PrayerName Name, string Time)[] prayers)
    {
        return new PrayerSchedule
        {
            Date = date,
            Prayers = prayers
                .Select(prayer => new PrayerEntry
                {
                    Name = prayer.Name,
                    Time = date.ToDateTime(TimeOnly.Parse(prayer.Time, CultureInfo.InvariantCulture))
                })
                .ToArray()
        };
    }
}
