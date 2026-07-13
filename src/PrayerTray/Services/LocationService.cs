using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PrayerTray.Models;

namespace PrayerTray.Services;

public sealed class LocationService
{
    private const double ClusterDistanceKm = 80;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly SettingsService _settingsService;

    public LocationService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<LocationInfo> ResolveLocationAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Settings;

        if (settings.UseManualLocation)
        {
            return ResolveManualLocation(settings);
        }

        try
        {
            var location = await FetchIpLocationAsync(cancellationToken);

            ApplyCountryDefaults(settings, location);
            settings.LastKnownLocation = location;
            _settingsService.Save();
            return location;
        }
        catch
        {
            if (settings.LastKnownLocation is not null)
            {
                return settings.LastKnownLocation;
            }

            throw;
        }
    }

    private static void ApplyCountryDefaults(AppSettings settings, LocationInfo location)
    {
        var defaultMethod = GetDefaultMethodForCountry(location.Country);
        if (settings.CalculationMethod is null || settings.CalculationMethod == 1 && defaultMethod != 1)
        {
            settings.CalculationMethod = defaultMethod;
        }
    }

    private static LocationInfo ResolveManualLocation(AppSettings settings)
    {
        var timezone = ResolveManualTimezone(settings);

        if (settings.ManualLatitude is double lat && settings.ManualLongitude is double lon)
        {
            return new LocationInfo
            {
                Latitude = lat,
                Longitude = lon,
                City = settings.ManualCity,
                Country = settings.ManualCountry,
                Timezone = timezone,
                Source = "manual",
                ResolvedAtUtc = DateTime.UtcNow
            };
        }

        if (!string.IsNullOrWhiteSpace(settings.ManualCity) && !string.IsNullOrWhiteSpace(settings.ManualCountry))
        {
            return new LocationInfo
            {
                City = settings.ManualCity.Trim(),
                Country = settings.ManualCountry.Trim(),
                Timezone = timezone,
                Source = "manual-city",
                ResolvedAtUtc = DateTime.UtcNow
            };
        }

        throw new InvalidOperationException("Manual location is enabled but no coordinates or city/country were provided.");
    }

    internal static string ResolveManualTimezone(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ManualTimezone))
        {
            return settings.ManualTimezone;
        }

        if (settings.LastKnownLocation is { } lastKnown
            && !string.IsNullOrWhiteSpace(lastKnown.Timezone)
            && string.Equals(
                NormalizeCountry(lastKnown.Country),
                NormalizeCountry(settings.ManualCountry),
                StringComparison.Ordinal))
        {
            return lastKnown.Timezone;
        }

        return GetDefaultTimezoneForCountry(settings.ManualCountry);
    }

    private static async Task<LocationInfo> FetchIpLocationAsync(CancellationToken cancellationToken)
    {
        var ipApiTask = TryFetchIpApiAsync(cancellationToken);
        var ipInfoTask = TryFetchIpInfoAsync(cancellationToken);
        var ipWhoTask = TryFetchIpWhoAsync(cancellationToken);
        var freeIpApiTask = TryFetchFreeIpApiAsync(cancellationToken);
        var geoJsTask = TryFetchGeoJsAsync(cancellationToken);

        await Task.WhenAll(ipApiTask, ipInfoTask, ipWhoTask, freeIpApiTask, geoJsTask);

        var candidates = new List<IpLocationCandidate>();
        foreach (var candidate in new[]
                 {
                     ipApiTask.Result,
                     ipInfoTask.Result,
                     ipWhoTask.Result,
                     freeIpApiTask.Result,
                     geoJsTask.Result
                 })
        {
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("All IP geolocation providers failed.");
        }

        var winner = SelectBestCandidate(candidates);
        var reverseGeocoded = await TryReverseGeocodeAsync(winner.Latitude, winner.Longitude, cancellationToken);

        return new LocationInfo
        {
            Latitude = winner.Latitude,
            Longitude = winner.Longitude,
            City = FirstNonEmpty(reverseGeocoded?.City ?? string.Empty, winner.City),
            Region = FirstNonEmpty(reverseGeocoded?.Region ?? string.Empty, winner.Region),
            Country = FirstNonEmpty(reverseGeocoded?.Country ?? string.Empty, winner.Country),
            Timezone = FirstNonEmpty(reverseGeocoded?.Timezone ?? string.Empty, winner.Timezone),
            Source = "ip",
            ResolvedAtUtc = DateTime.UtcNow
        };
    }

    private static IpLocationCandidate SelectBestCandidate(IReadOnlyList<IpLocationCandidate> candidates)
    {
        var clusters = new List<List<IpLocationCandidate>>();

        foreach (var candidate in candidates)
        {
            var matched = false;
            foreach (var cluster in clusters)
            {
                if (DistanceKm(candidate, cluster[0]) <= ClusterDistanceKm)
                {
                    cluster.Add(candidate);
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                clusters.Add([candidate]);
            }
        }

        var bestCluster = clusters
            .OrderByDescending(cluster => cluster.Count)
            .ThenBy(ClusterTightnessKm)
            .ThenByDescending(cluster => cluster.Sum(candidate => candidate.Weight))
            .First();

        return BuildClusterCandidate(bestCluster);
    }

    private static IpLocationCandidate BuildClusterCandidate(IReadOnlyList<IpLocationCandidate> cluster)
    {
        var latitude = cluster.Average(candidate => candidate.Latitude);
        var longitude = cluster.Average(candidate => candidate.Longitude);
        var bestNamed = cluster.OrderByDescending(candidate => candidate.Weight).First();

        return new IpLocationCandidate
        {
            Provider = string.Join('+', cluster.Select(candidate => candidate.Provider)),
            Weight = cluster.Sum(candidate => candidate.Weight),
            Latitude = latitude,
            Longitude = longitude,
            City = string.Empty,
            Region = bestNamed.Region,
            Country = bestNamed.Country,
            Timezone = bestNamed.Timezone
        };
    }

    private static double ClusterTightnessKm(IReadOnlyList<IpLocationCandidate> cluster)
    {
        if (cluster.Count <= 1)
        {
            return 0;
        }

        var maxDistance = 0d;
        for (var i = 0; i < cluster.Count; i++)
        {
            for (var j = i + 1; j < cluster.Count; j++)
            {
                maxDistance = Math.Max(maxDistance, DistanceKm(cluster[i], cluster[j]));
            }
        }

        return maxDistance;
    }

    private static async Task<IpLocationCandidate?> TryFetchIpApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(
                "http://ip-api.com/json/?fields=status,message,lat,lon,city,regionName,country,timezone",
                cancellationToken);

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.GetProperty("status").GetString() != "success")
            {
                return null;
            }

            return new IpLocationCandidate
            {
                Provider = "ip-api",
                Weight = 2,
                Latitude = root.GetProperty("lat").GetDouble(),
                Longitude = root.GetProperty("lon").GetDouble(),
                City = ReadString(root, "city"),
                Region = ReadString(root, "regionName"),
                Country = ReadString(root, "country"),
                Timezone = ReadString(root, "timezone")
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IpLocationCandidate?> TryFetchIpInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync("https://ipinfo.io/json", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!TryParseLoc(root, out var latitude, out var longitude))
            {
                return null;
            }

            return new IpLocationCandidate
            {
                Provider = "ipinfo",
                Weight = 3,
                Latitude = latitude,
                Longitude = longitude,
                City = ReadString(root, "city"),
                Region = ReadString(root, "region"),
                Country = ReadCountryName(ReadString(root, "country")),
                Timezone = ReadString(root, "timezone")
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IpLocationCandidate?> TryFetchIpWhoAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync("https://ipwho.is/", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                return null;
            }

            return new IpLocationCandidate
            {
                Provider = "ipwho",
                Weight = 1,
                Latitude = root.GetProperty("latitude").GetDouble(),
                Longitude = root.GetProperty("longitude").GetDouble(),
                City = ReadString(root, "city"),
                Region = ReadString(root, "region"),
                Country = ReadString(root, "country"),
                Timezone = ReadString(root, "timezone", "id")
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IpLocationCandidate?> TryFetchFreeIpApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync("https://free.freeipapi.com/api/json", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            return new IpLocationCandidate
            {
                Provider = "freeipapi",
                Weight = 1,
                Latitude = root.GetProperty("latitude").GetDouble(),
                Longitude = root.GetProperty("longitude").GetDouble(),
                City = ReadString(root, "cityName"),
                Region = ReadString(root, "regionName"),
                Country = ReadString(root, "countryName"),
                Timezone = ReadString(root, "timeZone")
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IpLocationCandidate?> TryFetchGeoJsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync("https://get.geojs.io/v1/ip/geo.json", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!double.TryParse(ReadString(root, "latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                || !double.TryParse(ReadString(root, "longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                return null;
            }

            return new IpLocationCandidate
            {
                Provider = "geojs",
                Weight = 3,
                Latitude = latitude,
                Longitude = longitude,
                City = ReadString(root, "city"),
                Region = ReadString(root, "region"),
                Country = ReadString(root, "country"),
                Timezone = string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ReverseGeocodeResult?> TryReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}&localityLanguage=en";

            using var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            return new ReverseGeocodeResult
            {
                City = FirstNonEmpty(ReadString(root, "locality"), ReadString(root, "city")),
                Region = ReadString(root, "principalSubdivision"),
                Country = ReadString(root, "countryName"),
                Timezone = ReadString(root, "ianaTimeId")
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseLoc(JsonElement root, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        if (!root.TryGetProperty("loc", out var locElement))
        {
            return false;
        }

        var loc = locElement.GetString();
        if (string.IsNullOrWhiteSpace(loc))
        {
            return false;
        }

        var parts = loc.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
               && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
               && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
    }

    private static string ReadCountryName(string countryCode) =>
        countryCode.ToUpperInvariant() switch
        {
            "SA" => "Saudi Arabia",
            "AE" => "United Arab Emirates",
            "EG" => "Egypt",
            "US" => "United States",
            "GB" => "United Kingdom",
            "PK" => "Pakistan",
            "IN" => "India",
            "TR" => "Turkey",
            "ID" => "Indonesia",
            "MY" => "Malaysia",
            _ => countryCode
        };

    public static string GetCountryCode(string country)
    {
        return NormalizeCountry(country) switch
        {
            "SAUDI ARABIA" => "SA",
            "UNITED ARAB EMIRATES" or "UAE" => "AE",
            "EGYPT" => "EG",
            "UNITED STATES" or "USA" => "US",
            "UNITED KINGDOM" => "GB",
            "PAKISTAN" => "PK",
            "INDIA" => "IN",
            "TURKEY" or "TURKIYE" => "TR",
            "INDONESIA" => "ID",
            "MALAYSIA" => "MY",
            "KUWAIT" => "KW",
            "QATAR" => "QA",
            "CANADA" => "CA",
            _ => string.Empty
        };
    }

    private static string ReadString(JsonElement element, string propertyName, string? nestedProperty = null)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        if (nestedProperty is not null && value.ValueKind == JsonValueKind.Object)
        {
            return value.TryGetProperty(nestedProperty, out var nested)
                ? nested.GetString() ?? string.Empty
                : string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static double DistanceKm(IpLocationCandidate left, IpLocationCandidate right)
    {
        const double earthRadiusKm = 6371;
        var lat1 = DegreesToRadians(left.Latitude);
        var lat2 = DegreesToRadians(right.Latitude);
        var deltaLat = DegreesToRadians(right.Latitude - left.Latitude);
        var deltaLon = DegreesToRadians(right.Longitude - left.Longitude);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2)
                * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    public static int GetDefaultMethodForCountry(string country)
    {
        return NormalizeCountry(country) switch
        {
            "UNITED STATES" or "USA" or "CANADA" => 2,
            "SAUDI ARABIA" => 4,
            "EGYPT" => 5,
            "IRAN" => 7,
            "KUWAIT" => 9,
            "QATAR" => 10,
            "UNITED ARAB EMIRATES" or "UAE" => 8,
            "TURKEY" or "TURKIYE" => 13,
            "INDONESIA" => 20,
            "MALAYSIA" => 17,
            "PAKISTAN" or "BANGLADESH" or "INDIA" => 1,
            _ => 3
        };
    }

    public static string GetDefaultTimezoneForCountry(string country)
    {
        return NormalizeCountry(country) switch
        {
            "SAUDI ARABIA" => "Asia/Riyadh",
            "UNITED ARAB EMIRATES" or "UAE" => "Asia/Dubai",
            "EGYPT" => "Africa/Cairo",
            "KUWAIT" => "Asia/Kuwait",
            "QATAR" => "Asia/Qatar",
            "TURKEY" or "TURKIYE" => "Europe/Istanbul",
            "INDONESIA" => "Asia/Jakarta",
            "MALAYSIA" => "Asia/Kuala_Lumpur",
            "PAKISTAN" => "Asia/Karachi",
            "INDIA" => "Asia/Kolkata",
            _ => string.Empty
        };
    }

    private static string NormalizeCountry(string country)
    {
        var decomposed = country.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    private sealed class IpLocationCandidate
    {
        public string Provider { get; init; } = string.Empty;
        public int Weight { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string City { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string Timezone { get; init; } = string.Empty;
    }

    private sealed class ReverseGeocodeResult
    {
        public string City { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string Timezone { get; init; } = string.Empty;
    }
}
