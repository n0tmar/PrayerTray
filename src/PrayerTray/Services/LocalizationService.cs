using System.Globalization;
using System.Windows;
using PrayerTray.Localization;
using PrayerTray.Models;

namespace PrayerTray.Services;

public sealed class LocalizationService
{
    private ResourceDictionary? _stringsDictionary;

    public event EventHandler? LanguageChanged;

    public string CurrentLanguage { get; private set; } = "en";

    public bool IsRightToLeft => CurrentLanguage is "ar" or "ur";

    public System.Windows.FlowDirection FlowDirection =>
        IsRightToLeft ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;

    public CultureInfo Culture => CurrentLanguage switch
    {
        "ar" => CultureInfo.GetCultureInfo("ar-SA"),
        "fr" => CultureInfo.GetCultureInfo("fr-FR"),
        "ur" => CultureInfo.GetCultureInfo("ur-PK"),
        "tr" => CultureInfo.GetCultureInfo("tr-TR"),
        "id" => CultureInfo.GetCultureInfo("id-ID"),
        _ => CultureInfo.GetCultureInfo("en-US")
    };

    public void Initialize(string? languageCode)
    {
        SetLanguage(NormalizeLanguage(languageCode));
    }

    public void SetLanguage(string languageCode)
    {
        var normalized = NormalizeLanguage(languageCode);
        if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase)
            && _stringsDictionary is not null)
        {
            return;
        }

        CurrentLanguage = normalized;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
        ApplyStringResources();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        if (LocalizationCatalog.All.TryGetValue(CurrentLanguage, out var table)
            && table is not null
            && table.TryGetValue(key, out var value))
        {
            return value;
        }

        if (LocalizationCatalog.All.TryGetValue("en", out var fallbackTable)
            && fallbackTable is not null
            && fallbackTable.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    public string Format(string key, params object[] args) => string.Format(Culture, Get(key), args);

    public string GetPrayerName(PrayerName prayer) => Get($"Prayer_{prayer}");

    public string GetCalculationMethodName(int methodId) => Get($"CalcMethod_{methodId}");

    public IReadOnlyList<LanguageOption> GetLanguageOptions() =>
        LocalizationCatalog.SupportedLanguages
            .Select(code => new LanguageOption
            {
                Code = code,
                DisplayName = Get($"Lang_{code}")
            })
            .ToList();

    public static string NormalizeLanguageCode(string? languageCode) => NormalizeLanguage(languageCode);

    private void ApplyStringResources()
    {
        _stringsDictionary ??= new ResourceDictionary();

        if (!System.Windows.Application.Current.Resources.MergedDictionaries.Contains(_stringsDictionary))
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(_stringsDictionary);
        }

        if (!LocalizationCatalog.All.TryGetValue(CurrentLanguage, out var table))
        {
            return;
        }

        var staleKeys = _stringsDictionary.Keys
            .Cast<string>()
            .Where(key => !table.ContainsKey(key))
            .ToList();

        foreach (var key in staleKeys)
        {
            _stringsDictionary.Remove(key);
        }

        foreach (var entry in table)
        {
            _stringsDictionary[entry.Key] = entry.Value;
        }
    }

    private static string NormalizeLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "en";
        }

        var code = languageCode.Trim().ToLowerInvariant();
        return LocalizationCatalog.SupportedLanguages.Contains(code, StringComparer.OrdinalIgnoreCase)
            ? code
            : "en";
    }
}
