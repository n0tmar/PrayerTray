namespace PrayerTray.Services;

public static class CountryNames
{
    private static readonly Dictionary<string, string> ByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SA"] = "Saudi Arabia",
        ["AE"] = "United Arab Emirates",
        ["EG"] = "Egypt",
        ["US"] = "United States",
        ["GB"] = "United Kingdom",
        ["PK"] = "Pakistan",
        ["IN"] = "India",
        ["TR"] = "Turkey",
        ["ID"] = "Indonesia",
        ["MY"] = "Malaysia",
        ["KW"] = "Kuwait",
        ["QA"] = "Qatar",
        ["CA"] = "Canada",
        ["JO"] = "Jordan",
        ["IQ"] = "Iraq",
        ["SY"] = "Syria",
        ["LB"] = "Lebanon",
        ["YE"] = "Yemen",
        ["OM"] = "Oman",
        ["BH"] = "Bahrain",
        ["MA"] = "Morocco",
        ["DZ"] = "Algeria",
        ["TN"] = "Tunisia",
        ["LY"] = "Libya",
        ["SD"] = "Sudan",
        ["NG"] = "Nigeria",
        ["DE"] = "Germany",
        ["FR"] = "France",
        ["ES"] = "Spain",
        ["IT"] = "Italy",
        ["NL"] = "Netherlands",
        ["BE"] = "Belgium",
        ["SE"] = "Sweden",
        ["NO"] = "Norway",
        ["DK"] = "Denmark",
        ["AU"] = "Australia",
        ["NZ"] = "New Zealand",
        ["SG"] = "Singapore",
        ["BD"] = "Bangladesh",
        ["AF"] = "Afghanistan",
        ["IR"] = "Iran",
    };

    public static string FromCode(string countryCode) =>
        ByCode.TryGetValue(countryCode.Trim(), out var name) ? name : countryCode;
}
