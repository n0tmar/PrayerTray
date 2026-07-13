namespace PrayerTray.Models;

public sealed class CityOption
{
    public string Name { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string CountryName { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Timezone { get; init; } = string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(CountryName)
            ? Name
            : $"{Name}, {CountryName}";
}
