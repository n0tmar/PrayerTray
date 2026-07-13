using System.Windows.Threading;
using PrayerTray.Helpers;
using PrayerTray.Models;
using PrayerTray.Services;

namespace PrayerTray.ViewModels;

public sealed class TaskbarWidgetViewModel : ViewModelBase, IDisposable
{
    private readonly PrayerTimeService _prayerTimeService;
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly LocalizationService _localization;
    private readonly DispatcherTimer _timer;
    private string _topLine = "...";
    private string _bottomLine = "Prayer";
    private string _textColor = "#FFFFFF";

    public string TopLine
    {
        get => _topLine;
        private set => SetProperty(ref _topLine, value);
    }

    public string BottomLine
    {
        get => _bottomLine;
        private set => SetProperty(ref _bottomLine, value);
    }

    public string TextColor
    {
        get => _textColor;
        private set => SetProperty(ref _textColor, value);
    }

    public TaskbarWidgetViewModel(
        PrayerTimeService prayerTimeService,
        SettingsService settingsService,
        ThemeService themeService,
        LocalizationService localization)
    {
        _prayerTimeService = prayerTimeService;
        _settingsService = settingsService;
        _themeService = themeService;
        _localization = localization;
        _prayerTimeService.ScheduleUpdated += (_, _) => RefreshDisplay();
        _localization.LanguageChanged += (_, _) => RefreshDisplay();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _timer.Tick += (_, _) => RefreshDisplay();
        _timer.Start();

        RefreshTheme();
        RefreshDisplay();
    }

    public void RefreshTheme()
    {
        TextColor = _themeService.TaskbarTextColor;
    }

    public void RefreshDisplay()
    {
        var next = _prayerTimeService.GetNextPrayer();
        if (next is null)
        {
            SetTimerInterval(TimeSpan.FromSeconds(30));
            TopLine = "...";
            BottomLine = _localization.Get("Prayer");
            return;
        }

        var mode = TaskbarContentModes.Normalize(_settingsService.Settings.TaskbarContentMode);
        SetTimerInterval(GetRefreshInterval(next.TimeUntil));
        TopLine = GetTopLine(mode, next);
        BottomLine = _localization.GetPrayerName(next.Name);
    }

    private string GetTopLine(string mode, NextPrayerInfo next)
    {
        return mode switch
        {
            TaskbarContentModes.Time => TimeFormatHelper.FormatTime(next.Time, _localization),
            TaskbarContentModes.Smart when next.TimeUntil > TimeSpan.FromHours(1) =>
                TimeFormatHelper.FormatTime(next.Time, _localization),
            _ => TimeFormatHelper.FormatCountdown(next.TimeUntil, _localization)
        };
    }

    private void SetTimerInterval(TimeSpan interval)
    {
        if (_timer.Interval == interval)
        {
            return;
        }

        _timer.Stop();
        _timer.Interval = interval;
        _timer.Start();
    }

    private static TimeSpan GetRefreshInterval(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.FromHours(1))
        {
            return TimeSpan.FromSeconds(1);
        }

        if (remaining <= TimeSpan.FromHours(6))
        {
            return TimeSpan.FromSeconds(15);
        }

        return TimeSpan.FromSeconds(30);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
