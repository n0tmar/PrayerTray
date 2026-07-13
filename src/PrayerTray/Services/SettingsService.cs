using System.IO;
using System.Text.Json;
using PrayerTray.Models;

namespace PrayerTray.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "PrayerTray");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            _settings = new AppSettings();
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            _settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public string GetCacheFolder()
    {
        var folder = Path.Combine(Path.GetDirectoryName(_settingsPath)!, "cache");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
