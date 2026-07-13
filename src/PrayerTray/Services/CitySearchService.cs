using System.Net.Http;
using System.Text.Json;
using PrayerTray.Models;

namespace PrayerTray.Services;

public sealed class CitySearchService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    public async Task<IReadOnlyList<CityOption>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 2)
        {
            return Array.Empty<CityOption>();
        }

        var url = $"https://api.islamic.app/v1/cities?q={Uri.EscapeDataString(trimmed)}";
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("results", out var results))
        {
            return Array.Empty<CityOption>();
        }

        var cities = new List<CityOption>();
        foreach (var item in results.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameElement))
            {
                continue;
            }

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var countryCode = item.TryGetProperty("country_code", out var codeElement)
                ? codeElement.GetString() ?? string.Empty
                : string.Empty;

            cities.Add(new CityOption
            {
                Name = name,
                CountryCode = countryCode,
                CountryName = CountryNames.FromCode(countryCode),
                Latitude = item.TryGetProperty("latitude", out var latElement) ? latElement.GetDouble() : 0,
                Longitude = item.TryGetProperty("longitude", out var lonElement) ? lonElement.GetDouble() : 0,
                Timezone = item.TryGetProperty("timezone", out var tzElement)
                    ? tzElement.GetString() ?? string.Empty
                    : string.Empty
            });
        }

        return cities
            .OrderByDescending(city => city.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(city => city.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            .ThenBy(city => city.Name, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }
}
