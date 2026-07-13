namespace PrayerTray.Models;

public sealed class LocationInfo
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string Source { get; set; } = "ip";
    public DateTime ResolvedAtUtc { get; set; } = DateTime.UtcNow;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(City) && !string.IsNullOrWhiteSpace(Country))
            {
                return $"{City}, {Country}";
            }

            if (!string.IsNullOrWhiteSpace(City))
            {
                return City;
            }

            if (!string.IsNullOrWhiteSpace(Country))
            {
                return Country;
            }

            if (!string.IsNullOrWhiteSpace(Region))
            {
                return Region;
            }

            return "Unknown location";
        }
    }
}
