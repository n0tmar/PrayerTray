using PrayerTray.Localization;

namespace PrayerTray.Tests;

public sealed class LocalizationCatalogTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedLanguageNames =
        new Dictionary<string, string>
        {
            ["Lang_en"] = "English",
            ["Lang_ar"] = "العربية",
            ["Lang_fr"] = "Français",
            ["Lang_ur"] = "اردو",
            ["Lang_tr"] = "Türkçe",
            ["Lang_id"] = "Bahasa Indonesia"
        };

    private static readonly string[] MojibakeMarkers =
    [
        "Ã",
        "Â",
        "Ø",
        "Ù",
        "Û",
        "Å",
        "Ä",
        "â€",
        "�"
    ];

    [Fact]
    public void EveryLanguageCatalog_UsesSameKeysAsEnglish()
    {
        var englishKeys = LocalizationCatalog.All["en"].Keys.Order().ToArray();

        foreach (var (language, table) in LocalizationCatalog.All)
        {
            Assert.Equal(englishKeys, table.Keys.Order().ToArray());
            Assert.Contains(language, LocalizationCatalog.SupportedLanguages);
        }
    }

    [Fact]
    public void LanguageNames_StayNativeInEveryCatalog()
    {
        foreach (var table in LocalizationCatalog.All.Values)
        {
            foreach (var (key, expected) in ExpectedLanguageNames)
            {
                Assert.Equal(expected, table[key]);
            }
        }
    }

    [Fact]
    public void LocalizedStrings_DoNotContainMojibakeMarkers()
    {
        var failures = LocalizationCatalog.All
            .SelectMany(language => language.Value.Select(entry => new
            {
                Language = language.Key,
                entry.Key,
                entry.Value
            }))
            .Where(entry => MojibakeMarkers.Any(marker => entry.Value.Contains(marker, StringComparison.Ordinal)))
            .Select(entry => $"{entry.Language}:{entry.Key}={entry.Value}")
            .ToArray();

        Assert.Empty(failures);
    }
}
