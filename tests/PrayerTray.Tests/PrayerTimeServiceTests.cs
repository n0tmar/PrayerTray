using System.Globalization;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.Tests;

public sealed class PrayerTimeServiceTests
{
    [Fact]
    public void GetNextPrayer_KeepsUpcomingPrayerWhenInsideTwoMinuteWindow()
    {
        var date = new DateOnly(2026, 7, 13);
        var schedule = Schedule(
            date,
            (PrayerName.Fajr, "04:20"),
            (PrayerName.Dhuhr, "12:00"),
            (PrayerName.Asr, "15:25"),
            (PrayerName.Maghrib, "19:08"),
            (PrayerName.Isha, "20:38"));
        var now = date.ToDateTime(TimeOnly.Parse("11:58:30", CultureInfo.InvariantCulture));

        var next = PrayerTimeService.GetNextPrayer(now, schedule, nextSchedule: null);

        Assert.NotNull(next);
        Assert.Equal(PrayerName.Dhuhr, next.Name);
        Assert.Equal(TimeSpan.FromSeconds(90), next.TimeUntil);
        Assert.False(next.IsNow);
    }

    [Fact]
    public void GetNextPrayer_UsesTomorrowFajrAfterIsha()
    {
        var today = new DateOnly(2026, 7, 13);
        var tomorrow = today.AddDays(1);
        var currentSchedule = Schedule(
            today,
            (PrayerName.Fajr, "04:20"),
            (PrayerName.Dhuhr, "12:00"),
            (PrayerName.Asr, "15:25"),
            (PrayerName.Maghrib, "19:08"),
            (PrayerName.Isha, "20:38"));
        var nextSchedule = Schedule(
            tomorrow,
            (PrayerName.Fajr, "04:21"),
            (PrayerName.Dhuhr, "12:00"),
            (PrayerName.Asr, "15:25"),
            (PrayerName.Maghrib, "19:08"),
            (PrayerName.Isha, "20:38"));
        var now = today.ToDateTime(TimeOnly.Parse("22:30", CultureInfo.InvariantCulture));

        var next = PrayerTimeService.GetNextPrayer(now, currentSchedule, nextSchedule);

        Assert.NotNull(next);
        Assert.Equal(PrayerName.Fajr, next.Name);
        Assert.Equal(tomorrow.ToDateTime(TimeOnly.Parse("04:21", CultureInfo.InvariantCulture)), next.Time);
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
