using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.ViewModels;

public sealed class CalculationMethodOption
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class TaskbarContentModeOption
{
    public string Value { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly int[] CalculationMethodIds =
    [
        1, 2, 3, 4, 5, 7, 8, 9, 10, 13, 17, 20
    ];

    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localization;
    private readonly CitySearchService _citySearchService = new();
    private readonly DispatcherTimer _citySearchTimer;
    private CancellationTokenSource? _citySearchCts;
    private bool _suppressCitySearch;
    private bool _disposed;

    private bool _useManualLocation;
    private string _citySearchText = string.Empty;
    private CityOption? _selectedCity;
    private string _manualCity = string.Empty;
    private string _manualCountry = string.Empty;
    private string _manualTimezone = string.Empty;
    private string _manualLatitude = string.Empty;
    private string _manualLongitude = string.Empty;
    private string _selectedLanguage = "en";
    private int _selectedMethod = 3;
    private string _selectedTaskbarContentMode = TaskbarContentModes.Countdown;
    private bool _startWithWindows;
    private bool _showWidget = true;
    private bool _showNotificationIcon;
    private string _statusMessage = string.Empty;
    private string _detectedLocationText = string.Empty;
    private bool _isSearchingCities;

    public ObservableCollection<CalculationMethodOption> CalculationMethods { get; } = [];
    public ObservableCollection<TaskbarContentModeOption> TaskbarContentModeOptions { get; } = [];
    public ObservableCollection<LanguageOption> Languages { get; } = [];
    public ObservableCollection<CityOption> CitySearchResults { get; } = [];

    public bool UseManualLocation
    {
        get => _useManualLocation;
        set => SetProperty(ref _useManualLocation, value);
    }

    public string CitySearchText
    {
        get => _citySearchText;
        set
        {
            if (!SetProperty(ref _citySearchText, value))
            {
                return;
            }

            if (_suppressCitySearch)
            {
                return;
            }

            if (SelectedCity is not null
                && !string.Equals(value, SelectedCity.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                SelectedCity = null;
            }

            ScheduleCitySearch();
        }
    }

    public CityOption? SelectedCity
    {
        get => _selectedCity;
        set
        {
            if (!SetProperty(ref _selectedCity, value) || value is null)
            {
                return;
            }

            ApplySelectedCity(value);
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            var normalized = LocalizationService.NormalizeLanguageCode(value);
            if (!SetProperty(ref _selectedLanguage, normalized))
            {
                return;
            }

            _localization.SetLanguage(normalized);
        }
    }

    public bool IsSearchingCities
    {
        get => _isSearchingCities;
        private set => SetProperty(ref _isSearchingCities, value);
    }

    public int SelectedMethod
    {
        get => _selectedMethod;
        set => SetProperty(ref _selectedMethod, value);
    }

    public string SelectedTaskbarContentMode
    {
        get => _selectedTaskbarContentMode;
        set => SetProperty(ref _selectedTaskbarContentMode, TaskbarContentModes.Normalize(value));
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool ShowWidget
    {
        get => _showWidget;
        set => SetProperty(ref _showWidget, value);
    }

    public bool ShowNotificationIcon
    {
        get => _showNotificationIcon;
        set => SetProperty(ref _showNotificationIcon, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string DetectedLocationText
    {
        get => _detectedLocationText;
        private set => SetProperty(ref _detectedLocationText, value);
    }

    public string ManualLocationTitle => _localization.Get("ManualLocation");

    public string StartWithWindowsTitle => _localization.Get("StartWithWindows");

    public string ShowWidgetTitle => _localization.Get("ShowWidget");

    public string ShowNotificationIconTitle => _localization.Get("ShowNotificationIcon");

    public string TaskbarContentModeTitle => _localization.Get("TaskbarContentMode");

    private readonly EventHandler _languageChangedHandler;

    public SettingsViewModel(SettingsService settingsService, LocalizationService localization)
    {
        _settingsService = settingsService;
        _localization = localization;
        _languageChangedHandler = (_, _) => OnLanguageChanged();

        _citySearchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _citySearchTimer.Tick += async (_, _) => await RunCitySearchAsync();

        _localization.LanguageChanged += _languageChangedHandler;
        RebuildLanguageOptions();
        RebuildCalculationMethods();
        RebuildTaskbarContentModes();
        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        var settings = _settingsService.Settings;
        UseManualLocation = settings.UseManualLocation;
        ManualCity = settings.ManualCity;
        ManualCountry = settings.ManualCountry;
        ManualTimezone = settings.ManualTimezone;
        ManualLatitude = settings.ManualLatitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        ManualLongitude = settings.ManualLongitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedMethod = settings.CalculationMethod ?? 3;
        SelectedTaskbarContentMode = settings.TaskbarContentMode;
        SelectedLanguage = LocalizationService.NormalizeLanguageCode(settings.Language);
        StartWithWindows = settings.StartWithWindows;
        ShowWidget = settings.ShowWidget;
        ShowNotificationIcon = settings.ShowNotificationIcon;
        UpdateDetectedLocationText(settings.LastKnownLocation?.DisplayName);

        CitySearchResults.Clear();
        if (!string.IsNullOrWhiteSpace(settings.ManualCity))
        {
            var restored = new CityOption
            {
                Name = settings.ManualCity,
                CountryName = settings.ManualCountry,
                CountryCode = LocationService.GetCountryCode(settings.ManualCountry),
                Latitude = settings.ManualLatitude ?? 0,
                Longitude = settings.ManualLongitude ?? 0,
                Timezone = settings.ManualTimezone
            };

            _suppressCitySearch = true;
            SelectedCity = restored;
            CitySearchText = restored.DisplayName;
            _suppressCitySearch = false;
        }
        else
        {
            _suppressCitySearch = true;
            SelectedCity = null;
            CitySearchText = string.Empty;
            _suppressCitySearch = false;
        }
    }

    public void RefreshDetectedLocation()
    {
        UpdateDetectedLocationText(_settingsService.Settings.LastKnownLocation?.DisplayName);
    }

    public bool TrySave()
    {
        StatusMessage = string.Empty;

        if (UseManualLocation && SelectedCity is null)
        {
            StatusMessage = _localization.Get("ChooseCity");
            return false;
        }

        var settings = _settingsService.Settings;
        settings.UseManualLocation = UseManualLocation;
        settings.Language = LocalizationService.NormalizeLanguageCode(SelectedLanguage);

        if (SelectedCity is not null)
        {
            settings.ManualCity = SelectedCity.Name;
            settings.ManualCountry = SelectedCity.CountryName;
            settings.ManualTimezone = string.IsNullOrWhiteSpace(SelectedCity.Timezone)
                ? LocationService.GetDefaultTimezoneForCountry(SelectedCity.CountryName)
                : SelectedCity.Timezone;
            settings.ManualLatitude = SelectedCity.Latitude;
            settings.ManualLongitude = SelectedCity.Longitude;
        }
        else
        {
            settings.ManualCity = string.Empty;
            settings.ManualCountry = string.Empty;
            settings.ManualTimezone = string.Empty;
            settings.ManualLatitude = null;
            settings.ManualLongitude = null;
        }

        settings.CalculationMethod = SelectedMethod;
        settings.TaskbarContentMode = TaskbarContentModes.Normalize(SelectedTaskbarContentMode);
        settings.StartWithWindows = StartWithWindows;
        settings.ShowWidget = ShowWidget;
        settings.ShowNotificationIcon = ShowNotificationIcon;

        _settingsService.Save();
        _localization.SetLanguage(settings.Language);
        StartupService.SetEnabled(StartWithWindows);
        StatusMessage = _localization.Get("SettingsSaved");
        return true;
    }

    private void OnLanguageChanged()
    {
        RebuildLanguageOptions();
        RebuildCalculationMethods();
        RebuildTaskbarContentModes();
        UpdateDetectedLocationText(_settingsService.Settings.LastKnownLocation?.DisplayName);
        OnPropertyChanged(nameof(ManualLocationTitle));
        OnPropertyChanged(nameof(StartWithWindowsTitle));
        OnPropertyChanged(nameof(ShowWidgetTitle));
        OnPropertyChanged(nameof(ShowNotificationIconTitle));
        OnPropertyChanged(nameof(TaskbarContentModeTitle));
        if (StatusMessage == _localization.Get("SettingsSaved") || string.IsNullOrEmpty(StatusMessage))
        {
            StatusMessage = string.Empty;
        }
    }

    private void UpdateDetectedLocationText(string? locationName)
    {
        DetectedLocationText = string.IsNullOrWhiteSpace(locationName)
            ? _localization.Get("Detecting")
            : _localization.Format("Location_Format", locationName);
    }

    private void RebuildLanguageOptions()
    {
        Languages.Clear();
        foreach (var option in _localization.GetLanguageOptions())
        {
            Languages.Add(option);
        }
    }

    private void RebuildCalculationMethods()
    {
        CalculationMethods.Clear();
        foreach (var id in CalculationMethodIds)
        {
            CalculationMethods.Add(new CalculationMethodOption
            {
                Id = id,
                Name = _localization.GetCalculationMethodName(id)
            });
        }
    }

    private void RebuildTaskbarContentModes()
    {
        var selected = SelectedTaskbarContentMode;
        TaskbarContentModeOptions.Clear();
        foreach (var mode in TaskbarContentModes.All)
        {
            TaskbarContentModeOptions.Add(new TaskbarContentModeOption
            {
                Value = mode,
                Name = _localization.Get($"TaskbarContentMode_{mode}")
            });
        }

        SelectedTaskbarContentMode = selected;
    }

    private void ApplySelectedCity(CityOption city)
    {
        ManualCity = city.Name;
        ManualCountry = city.CountryName;
        ManualTimezone = city.Timezone;
        ManualLatitude = city.Latitude.ToString(CultureInfo.InvariantCulture);
        ManualLongitude = city.Longitude.ToString(CultureInfo.InvariantCulture);

        _suppressCitySearch = true;
        CitySearchText = city.DisplayName;
        _suppressCitySearch = false;
    }

    private void ScheduleCitySearch()
    {
        _citySearchTimer.Stop();

        if (CitySearchText.Trim().Length < 2)
        {
            CitySearchResults.Clear();
            IsSearchingCities = false;
            return;
        }

        _citySearchTimer.Start();
    }

    private async Task RunCitySearchAsync()
    {
        _citySearchTimer.Stop();

        var query = CitySearchText.Trim();
        if (query.Length < 2)
        {
            CitySearchResults.Clear();
            IsSearchingCities = false;
            return;
        }

        _citySearchCts?.Cancel();
        _citySearchCts?.Dispose();
        _citySearchCts = new CancellationTokenSource();
        var token = _citySearchCts.Token;

        IsSearchingCities = true;
        try
        {
            var results = await _citySearchService.SearchAsync(query, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            CitySearchResults.Clear();
            foreach (var city in results)
            {
                CitySearchResults.Add(city);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            CitySearchResults.Clear();
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsSearchingCities = false;
            }
        }
    }

    private string ManualCity
    {
        get => _manualCity;
        set => _manualCity = value;
    }

    private string ManualCountry
    {
        get => _manualCountry;
        set => _manualCountry = value;
    }

    private string ManualTimezone
    {
        get => _manualTimezone;
        set => _manualTimezone = value;
    }

    private string ManualLatitude
    {
        get => _manualLatitude;
        set => _manualLatitude = value;
    }

    private string ManualLongitude
    {
        get => _manualLongitude;
        set => _manualLongitude = value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= _languageChangedHandler;
        _citySearchTimer.Stop();
        _citySearchCts?.Cancel();
        _citySearchCts?.Dispose();
    }
}
