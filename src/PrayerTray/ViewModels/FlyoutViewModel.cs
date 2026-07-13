using PrayerTray.Helpers;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.ViewModels;

public sealed class FlyoutPrayerItemViewModel : ViewModelBase
{
    public PrayerName Name { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public bool IsNext { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsHighlighted => IsNext || IsCurrent;
}

public sealed class FlyoutViewModel : ViewModelBase
{
    private readonly PrayerTimeService _prayerTimeService;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localization;
    private string _locationText = string.Empty;
    private IReadOnlyList<FlyoutPrayerItemViewModel> _prayers = Array.Empty<FlyoutPrayerItemViewModel>();

    public string LocationText
    {
        get => _locationText;
        private set => SetProperty(ref _locationText, value);
    }

    public IReadOnlyList<FlyoutPrayerItemViewModel> Prayers
    {
        get => _prayers;
        private set => SetProperty(ref _prayers, value);
    }

    public FlyoutViewModel(
        PrayerTimeService prayerTimeService,
        SettingsService settingsService,
        LocalizationService localization)
    {
        _prayerTimeService = prayerTimeService;
        _settingsService = settingsService;
        _localization = localization;
        _prayerTimeService.ScheduleUpdated += (_, _) => Refresh();
        _localization.LanguageChanged += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        var schedule = _prayerTimeService.CurrentSchedule;
        var locationName = _settingsService.Settings.LastKnownLocation?.DisplayName
            ?? schedule?.Location.DisplayName;

        LocationText = string.IsNullOrWhiteSpace(locationName)
            ? _localization.Get("UnknownLocation")
            : locationName;

        Prayers = _prayerTimeService.GetTodayScheduleWithStatus()
            .Select(p => new FlyoutPrayerItemViewModel
            {
                Name = p.Name,
                DisplayName = _localization.GetPrayerName(p.Name),
                TimeText = TimeFormatHelper.FormatTime(p.Time, _settingsService.Settings.TimeFormat, _localization),
                IsNext = p.IsNext,
                IsCurrent = p.IsCurrent
            })
            .ToList();
    }
}
