using PrayerTray.Localization;
using PrayerTray.Services;

namespace PrayerTray.Tests;

public sealed class LocalizationCatalogTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedLanguageNames =
        new Dictionary<string, string>
        {
            ["en"] = "English",
            ["ar"] = "العربية",
            ["fr"] = "Français",
            ["ur"] = "اردو",
            ["tr"] = "Türkçe",
            ["id"] = "Bahasa Indonesia"
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
    public void LanguageDisplayNames_UseNativeLabels()
    {
        Assert.Equal(LocalizationCatalog.SupportedLanguages.Order(), LocalizationCatalog.LanguageDisplayNames.Keys.Order());

        foreach (var (code, expected) in ExpectedLanguageNames)
        {
            Assert.Equal(expected, LocalizationCatalog.LanguageDisplayNames[code]);
        }
    }

    [Fact]
    public void LanguageOptions_DoNotDependOnCurrentLanguage()
    {
        var localization = new LocalizationService();
        var expected = ExpectedLanguageNames.Select(pair => (Code: pair.Key, DisplayName: pair.Value)).ToArray();

        localization.Initialize("en");
        var englishOptions = localization.GetLanguageOptions()
            .Select(option => (option.Code, option.DisplayName))
            .ToArray();

        localization.SetLanguage("ar");
        var arabicOptions = localization.GetLanguageOptions()
            .Select(option => (option.Code, option.DisplayName))
            .ToArray();

        Assert.Equal(expected, englishOptions);
        Assert.Equal(expected, arabicOptions);
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
