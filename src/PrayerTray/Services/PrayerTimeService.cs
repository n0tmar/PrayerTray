using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using PrayerTray.Models;

namespace PrayerTray.Services;

public sealed class PrayerTimeService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly PrayerName[] PrayerOrder =
    [
        PrayerName.Fajr,
        PrayerName.Dhuhr,
        PrayerName.Asr,
        PrayerName.Maghrib,
        PrayerName.Isha
    ];

    private static readonly TimeSpan CurrentPrayerGrace = TimeSpan.FromMinutes(2);

    private readonly SettingsService _settingsService;
    private PrayerSchedule? _currentSchedule;
    private PrayerSchedule? _nextSchedule;

    public event EventHandler? ScheduleUpdated;

    public PrayerSchedule? CurrentSchedule => _currentSchedule;
    public PrayerSchedule? NextSchedule => _nextSchedule;

    public PrayerTimeService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<PrayerSchedule> FetchScheduleAsync(LocationInfo location, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var schedule = await GetScheduleForDateAsync(location, today, cancellationToken);
        _currentSchedule = schedule;
        ScheduleUpdated?.Invoke(this, EventArgs.Empty);

        await PrefetchNextScheduleAsync(location, today, cancellationToken);
        return schedule;
    }

    public bool HasNextScheduleFor(DateOnly date) =>
        _nextSchedule?.Date == date.AddDays(1);

    private async Task PrefetchNextScheduleAsync(LocationInfo location, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            _nextSchedule = await GetScheduleForDateAsync(location, date.AddDays(1), cancellationToken);
            ScheduleUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _nextSchedule = null;
        }
    }

    private async Task<PrayerSchedule> GetScheduleForDateAsync(LocationInfo location, DateOnly date, CancellationToken cancellationToken)
    {
        var cachePath = GetCachePath(date, location);

        if (File.Exists(cachePath))
        {
            try
            {
                var cached = await LoadCacheAsync(cachePath, cancellationToken);
                if (cached is not null && !IsCacheStale(cached, date, location))
                {
                    return cached;
                }
            }
            catch
            {
                // Fall through to network fetch.
            }
        }

        var schedule = await FetchFromApiAsync(location, date, cancellationToken);
        await SaveCacheAsync(cachePath, schedule, cancellationToken);
        return schedule;
    }

    private static bool IsCacheStale(PrayerSchedule schedule, DateOnly date, LocationInfo location)
    {
        if (schedule.Date != date)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(location.Timezone)
            && !string.Equals(schedule.Location.Timezone, location.Timezone, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var age = DateTime.UtcNow - schedule.FetchedAtUtc;
        return date == DateOnly.FromDateTime(DateTime.Now) && age > TimeSpan.FromHours(6);
    }

    public NextPrayerInfo? GetNextPrayer() =>
        GetNextPrayer(DateTime.Now, _currentSchedule, _nextSchedule);

    internal static NextPrayerInfo? GetNextPrayer(
        DateTime now,
        PrayerSchedule? currentSchedule,
        PrayerSchedule? nextSchedule)
    {
        if (currentSchedule is null)
        {
            return null;
        }

        var nextPrayer = new[] { currentSchedule, nextSchedule }
            .Where(schedule => schedule is not null)
            .SelectMany(schedule => schedule!.Prayers)
            .Where(prayer => prayer.Time >= now)
            .OrderBy(prayer => prayer.Time)
            .FirstOrDefault();

        if (nextPrayer is null)
        {
            return null;
        }

        return new NextPrayerInfo
        {
            Name = nextPrayer.Name,
            Time = nextPrayer.Time,
            TimeUntil = nextPrayer.Time - now,
            IsNow = false
        };
    }

    public IReadOnlyList<PrayerEntry> GetTodayScheduleWithStatus()
    {
        if (_currentSchedule is null)
        {
            return Array.Empty<PrayerEntry>();
        }

        var now = DateTime.Now;
        var next = GetNextPrayer(now, _currentSchedule, _nextSchedule);
        var currentPrayer = _currentSchedule.Prayers
            .FirstOrDefault(p => now >= p.Time && now < p.Time.Add(CurrentPrayerGrace));
        return _currentSchedule.Prayers
            .Select(p => new PrayerEntry
            {
                Name = p.Name,
                Time = p.Time,
                IsNext = currentPrayer is null && next is not null && p.Name == next.Name && p.Time == next.Time,
                IsCurrent = currentPrayer is not null && p.Name == currentPrayer.Name && p.Time == currentPrayer.Time
            })
            .ToList();
    }

    private async Task<PrayerSchedule> FetchFromApiAsync(LocationInfo location, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            return await FetchFromIslamicAppAsync(location, date, cancellationToken);
        }
        catch
        {
            return await FetchFromAladhanAsync(location, date, cancellationToken);
        }
    }

    private async Task<PrayerSchedule> FetchFromIslamicAppAsync(LocationInfo location, DateOnly date, CancellationToken cancellationToken)
    {
        var method = ResolveCalculationMethod(location);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dateSegment = date == today ? "today" : date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var url = BuildIslamicAppUrl(dateSegment, location, method);

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var data))
        {
            throw new InvalidOperationException("islamic.app response did not include prayer data.");
        }

        var timings = data.GetProperty("timings");
        var timezone = data.TryGetProperty("meta", out var meta) && meta.TryGetProperty("timezone", out var tz)
            ? tz.GetString() ?? location.Timezone
            : location.Timezone;

        return BuildSchedule(date, location, timings, timezone);
    }

    private async Task<PrayerSchedule> FetchFromAladhanAsync(LocationInfo location, DateOnly date, CancellationToken cancellationToken)
    {
        var method = ResolveCalculationMethod(location);
        string url;

        if (location.Source == "manual-city")
        {
            var city = Uri.EscapeDataString(location.City);
            var country = Uri.EscapeDataString(location.Country);
            var dateSegment = date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
            url = $"https://api.aladhan.com/v1/timingsByCity/{dateSegment}?city={city}&country={country}&method={method}";
        }
        else
        {
            var dateSegment = date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
            url =
                $"https://api.aladhan.com/v1/timings/{dateSegment}?latitude={location.Latitude.ToString(CultureInfo.InvariantCulture)}&longitude={location.Longitude.ToString(CultureInfo.InvariantCulture)}&method={method}";

            if (!string.IsNullOrWhiteSpace(location.Timezone))
            {
                url += $"&timezonestring={Uri.EscapeDataString(location.Timezone)}";
            }
        }

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var timings = document.RootElement.GetProperty("data").GetProperty("timings");
        return BuildSchedule(date, location, timings, location.Timezone);
    }

    private string BuildIslamicAppUrl(string dateSegment, LocationInfo location, int method)
    {
        if (location.Source == "manual-city")
        {
            var city = Uri.EscapeDataString(location.City);
            var countryCode = LocationService.GetCountryCode(location.Country);
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                throw new InvalidOperationException($"Unsupported country for city lookup: {location.Country}");
            }

            return $"https://api.islamic.app/v1/timings/{dateSegment}?city={city}&country={countryCode}&method={method}";
        }

        var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"https://api.islamic.app/v1/timings/{dateSegment}?latitude={latitude}&longitude={longitude}&method={method}";

        if (!string.IsNullOrWhiteSpace(location.Timezone))
        {
            url += $"&timezone={Uri.EscapeDataString(location.Timezone)}";
        }

        return url;
    }

    private int ResolveCalculationMethod(LocationInfo location)
    {
        var configured = _settingsService.Settings.CalculationMethod;
        if (configured is not null)
        {
            return configured.Value;
        }

        return LocationService.GetDefaultMethodForCountry(location.Country);
    }

    private static PrayerSchedule BuildSchedule(
        DateOnly date,
        LocationInfo location,
        JsonElement timings,
        string? timezone)
    {
        var prayers = new List<PrayerEntry>();

        foreach (var prayerName in PrayerOrder)
        {
            var key = prayerName.ToString();
            if (!timings.TryGetProperty(key, out var timeElement))
            {
                continue;
            }

            var timeText = timeElement.GetString();
            if (string.IsNullOrWhiteSpace(timeText))
            {
                continue;
            }

            var cleanTime = timeText.Split(' ')[0];
            if (TimeOnly.TryParse(cleanTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
            {
                prayers.Add(new PrayerEntry
                {
                    Name = prayerName,
                    Time = date.ToDateTime(timeOnly)
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(timezone))
        {
            location = new LocationInfo
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                City = location.City,
                Region = location.Region,
                Country = location.Country,
                Timezone = timezone,
                Source = location.Source,
                ResolvedAtUtc = location.ResolvedAtUtc
            };
        }

        return new PrayerSchedule
        {
            Date = date,
            Location = location,
            Prayers = prayers,
            FetchedAtUtc = DateTime.UtcNow
        };
    }

    private string GetCachePath(DateOnly date, LocationInfo location)
    {
        var method = ResolveCalculationMethod(location);
        var key = location.Latitude != 0 || location.Longitude != 0
            ? $"{location.Latitude.ToString("F4", CultureInfo.InvariantCulture)}_{location.Longitude.ToString("F4", CultureInfo.InvariantCulture)}"
            : $"{location.City}_{location.Country}";
        key = SanitizeCachePart(key);

        var timezone = string.IsNullOrWhiteSpace(location.Timezone)
            ? "notz"
            : SanitizeCachePart(location.Timezone);

        return Path.Combine(_settingsService.GetCacheFolder(), $"{date:yyyy-MM-dd}_{key}_m{method}_{timezone}.json");
    }

    private static string SanitizeCachePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private static async Task SaveCacheAsync(string path, PrayerSchedule schedule, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(schedule, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static async Task<PrayerSchedule?> LoadCacheAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<PrayerSchedule>(json);
    }
}

