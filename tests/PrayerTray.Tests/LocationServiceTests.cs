using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.Tests;

public sealed class LocationServiceTests
{
    [Fact]
    public void ResolveManualTimezone_UsesExplicitTimezoneFirst()
    {
        var settings = new AppSettings
        {
            ManualCountry = "Egypt",
            ManualTimezone = "Asia/Riyadh",
            LastKnownLocation = new LocationInfo
            {
                Country = "Egypt",
                Timezone = "Africa/Cairo"
            }
        };

        var timezone = LocationService.ResolveManualTimezone(settings);

        Assert.Equal("Asia/Riyadh", timezone);
    }

    [Fact]
    public void ResolveManualTimezone_ReusesLastKnownTimezoneForSameCountry()
    {
        var settings = new AppSettings
        {
            ManualCountry = "Saudi Arabia",
            LastKnownLocation = new LocationInfo
            {
                Country = "Saudi Arabia",
                Timezone = "Asia/Riyadh"
            }
        };

        var timezone = LocationService.ResolveManualTimezone(settings);

        Assert.Equal("Asia/Riyadh", timezone);
    }

    [Fact]
    public void ResolveManualTimezone_FallsBackToCountryDefault()
    {
        var settings = new AppSettings
        {
            ManualCountry = "Qatar",
            LastKnownLocation = new LocationInfo
            {
                Country = "Saudi Arabia",
                Timezone = "Asia/Riyadh"
            }
        };

        var timezone = LocationService.ResolveManualTimezone(settings);

        Assert.Equal("Asia/Qatar", timezone);
    }
}
