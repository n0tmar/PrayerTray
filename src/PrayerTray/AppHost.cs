using System.IO;
using System.Windows;
using System.Windows.Forms;
using PrayerTray.Models;
using PrayerTray.Native;
using PrayerTray.Services;
using PrayerTray.ViewModels;
using PrayerTray.Views;
using WpfApplication = System.Windows.Application;

namespace PrayerTray;

public sealed class AppHost : IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly LocalizationService _localizationService = new();
    private readonly ThemeService _themeService = new();
    private readonly LocationService _locationService;
    private readonly PrayerTimeService _prayerTimeService;
    private readonly PrayerNotificationService _prayerNotificationService;
    private readonly TaskbarEmbedService _embedService = new();
    private readonly TaskbarSlotPublisher _slotPublisher;
    private readonly FullscreenDetector _fullscreenDetector = new();
    private readonly TaskbarWidgetViewModel _widgetViewModel;
    private readonly FlyoutViewModel _flyoutViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private FlyoutDismissService? _flyoutDismissService;

    private TaskbarWidgetWindow? _widgetWindow;
    private PrayerFlyoutWindow? _flyoutWindow;
    private SettingsWindow? _settingsWindow;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private uint _taskbarCreatedMessage;
    private System.Windows.Threading.DispatcherTimer? _scheduleRefreshTimer;
    private System.Windows.Threading.DispatcherTimer? _themeTimer;
    private System.Windows.Threading.DispatcherTimer? _nativeCommandTimer;
    private string? _nativeCommandFilePath;
    private NativeSlotCommand _lastNativeCommand = NativeSlotCommand.None;
    private long _lastNativeCommandTick;
    private bool _scheduleRefreshTickRunning;
    private bool _disposed;

    public AppHost()
    {
        _locationService = new LocationService(_settingsService);
        _prayerTimeService = new PrayerTimeService(_settingsService);
        _prayerNotificationService = new PrayerNotificationService(_prayerTimeService, _settingsService);
        _widgetViewModel = new TaskbarWidgetViewModel(_prayerTimeService, _settingsService, _themeService, _localizationService);
        _slotPublisher = new TaskbarSlotPublisher(_widgetViewModel);
        _flyoutViewModel = new FlyoutViewModel(_prayerTimeService, _settingsService, _localizationService);
        _settingsViewModel = new SettingsViewModel(_settingsService, _localizationService);
    }

    public async Task InitializeAsync()
    {
        _settingsService.Load();
        _localizationService.Initialize(_settingsService.Settings.Language);
        _localizationService.LanguageChanged += (_, _) =>
        {
            RebuildContextMenu();
            if (_notifyIcon is not null)
            {
                _notifyIcon.Text = _localizationService.Get("AppName");
            }
        };

        if (_settingsService.Settings.StartWithWindows && !StartupService.IsEnabled())
        {
            StartupService.SetEnabled(true);
        }

        SetupTrayIcon();
        SetupWindows();
        SetupFlyoutDismiss();
        SetupHooks();
        StartThemePolling();
        StartNativeCommandPolling();
        ClearStaleNativeCommandFile();

        await RefreshDataAsync(forceLocation: !_settingsService.Settings.UseManualLocation);
        _prayerNotificationService.Start();

        if (_settingsService.Settings.ShowWidget)
        {
            ShowWidget();
        }

        StartScheduleRefreshPolling();
    }

    private void EnsureContextMenu()
    {
        _contextMenu ??= new ContextMenuStrip();
        RebuildContextMenu();
    }

    private void RebuildContextMenu()
    {
        if (_contextMenu is null)
        {
            return;
        }

        _contextMenu.Items.Clear();
        _contextMenu.Items.Add(_localizationService.Get("Tray_TodaySchedule"), null, (_, _) => ToggleFlyout());
        _contextMenu.Items.Add(_localizationService.Get("Tray_ShowWidget"), null, (_, _) => ShowWidget());
        _contextMenu.Items.Add(_localizationService.Get("Tray_HideWidget"), null, (_, _) => HideWidget());
        _contextMenu.Items.Add(_localizationService.Get("Tray_RefreshLocation"), null, async (_, _) => await RefreshDataAsync(forceLocation: true));
        _contextMenu.Items.Add(_localizationService.Get("Tray_Settings"), null, (_, _) => OpenSettings());
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_localizationService.Get("Tray_Exit"), null, (_, _) => Shutdown());
    }

    private void SetupTrayIcon()
    {
        if (!_settingsService.Settings.ShowNotificationIcon)
        {
            return;
        }

        DisposeTrayIcon();
        EnsureContextMenu();

        _notifyIcon = new NotifyIcon
        {
            Text = _localizationService.Get("AppName"),
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = _contextMenu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
    }

    private void ApplyTrayIconVisibility()
    {
        if (_settingsService.Settings.ShowNotificationIcon)
        {
            if (_notifyIcon is null)
            {
                SetupTrayIcon();
            }

            return;
        }

        DisposeTrayIcon();
    }

    private void DisposeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;
    }

    private void SetupWindows()
    {
        _widgetWindow = new TaskbarWidgetWindow(_widgetViewModel);
        _widgetWindow.OpenFlyoutRequested += (_, _) => ToggleFlyout();
        _widgetWindow.OpenContextMenuRequested += (_, _) => ToggleSettings();

        _flyoutWindow = new PrayerFlyoutWindow(_flyoutViewModel, _localizationService);
        _flyoutWindow.SettingsRequested += (_, _) => OpenSettings();
        _flyoutWindow.RefreshLocationRequested += async (_, _) => await RefreshDataAsync(forceLocation: true);
    }

    private void SetupFlyoutDismiss()
    {
        _flyoutDismissService = new FlyoutDismissService(WpfApplication.Current.Dispatcher);
        _flyoutDismissService.DismissRequested += () => HideFlyouts(null);
        WireFlyoutDismiss(_flyoutWindow!);
    }

    private void WireFlyoutDismiss(TaskbarAnchoredFlyoutWindow window)
    {
        window.FlyoutOpened += (_, _) => _flyoutDismissService?.NotifyFlyoutOpened(window);
        window.FlyoutClosed += (_, _) => _flyoutDismissService?.NotifyFlyoutClosed(window);
    }

    private void SetupHooks()
    {
        _fullscreenDetector.FullscreenChanged += (_, _) =>
        {
            WpfApplication.Current.Dispatcher.Invoke(ApplyWidgetVisibility);
        };
    }

    private bool UsesNativeSlot() =>
        TaskbarDisplay.UsesNativeSlot(_settingsService.Settings);

    private void ApplyWidgetVisibility()
    {
        if (_widgetWindow is null)
        {
            return;
        }

        if (!_settingsService.Settings.ShowWidget || _fullscreenDetector.IsFullscreen)
        {
            _widgetWindow.Hide();
            _embedService.Detach();
            _slotPublisher.SetActive(false);
            return;
        }

        if (UsesNativeSlot())
        {
            _widgetWindow.Hide();
            _embedService.Detach();
            _slotPublisher.SetActive(true);
            return;
        }

        _slotPublisher.SetActive(false);
        ShowEmbeddedWidget();
    }

    private void ShowEmbeddedWidget()
    {
        if (_widgetWindow is null)
        {
            return;
        }

        _widgetWindow.Show();
        _widgetWindow.UpdateLayout();
        _embedService.Attach(_widgetWindow);
        _embedService.Reposition();
    }

    private void ShowWidget()
    {
        if (_widgetWindow is null)
        {
            return;
        }

        _settingsService.Settings.ShowWidget = true;
        _settingsService.Save();
        ApplyWidgetVisibility();
    }

    private void HideWidget()
    {
        if (_widgetWindow is null)
        {
            return;
        }

        _settingsService.Settings.ShowWidget = false;
        _settingsService.Save();
        ApplyWidgetVisibility();
    }

    private void ToggleFlyout()
    {
        if (_flyoutWindow is null)
        {
            return;
        }

        if (_flyoutWindow.IsFlyoutOpen)
        {
            _flyoutWindow.HideFlyout();
            return;
        }

        HideFlyouts(_flyoutWindow);

        if (_widgetWindow is { IsVisible: true })
        {
            _flyoutWindow.ShowNear(_widgetWindow);
            return;
        }

        _flyoutWindow.ShowNearScreen();
    }

    private void ToggleSettings()
    {
        var settings = EnsureSettingsWindow();

        if (settings.IsFlyoutOpen)
        {
            settings.HideFlyout();
            return;
        }

        HideFlyouts(settings);

        if (_widgetWindow is { IsVisible: true })
        {
            settings.ShowNear(new System.Drawing.Rectangle(
                (int)_widgetWindow.Left,
                (int)_widgetWindow.Top,
                (int)_widgetWindow.Width,
                (int)_widgetWindow.Height));
            return;
        }

        settings.ShowNearScreen();
    }

    private SettingsWindow EnsureSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            return _settingsWindow;
        }

        _settingsWindow = new SettingsWindow(_settingsViewModel, _localizationService);
        WireFlyoutDismiss(_settingsWindow);
        _settingsWindow.SettingsSaved += async (_, _) =>
        {
            await RefreshDataAsync(forceLocation: true);
            ApplyTrayIconVisibility();
            if (_settingsService.Settings.ShowWidget && !_fullscreenDetector.IsFullscreen)
            {
                ShowWidget();
            }
            else
            {
                HideWidget();
            }
        };
        return _settingsWindow;
    }

    private void HideFlyouts(Window? keepOpen)
    {
        if (keepOpen != _flyoutWindow && _flyoutWindow is { IsFlyoutOpen: true })
        {
            _flyoutWindow.HideFlyout();
        }

        if (keepOpen != _settingsWindow && _settingsWindow is { IsFlyoutOpen: true })
        {
            _settingsWindow.HideFlyout();
        }
    }

    private void ShowWidgetContextMenu()
    {
        EnsureContextMenu();

        if (_contextMenu is null)
        {
            return;
        }

        if (TaskbarFlyoutPosition.TryGetSlotAnchor(out var anchor))
        {
            _contextMenu.Show(anchor.Left, anchor.Bottom);
            return;
        }

        if (_widgetWindow is null)
        {
            return;
        }

        var position = _widgetWindow.PointToScreen(new System.Windows.Point(0, _widgetWindow.ActualHeight));
        _contextMenu.Show((int)position.X, (int)position.Y);
    }

    private void OpenSettings()
    {
        var settings = EnsureSettingsWindow();

        if (settings.IsFlyoutOpen)
        {
            _settingsViewModel.RefreshDetectedLocation();
            settings.Activate();
            BeginSettingsLocationRefresh();
            return;
        }

        HideFlyouts(settings);

        if (_widgetWindow is { IsVisible: true })
        {
            settings.ShowNear(new System.Drawing.Rectangle(
                (int)_widgetWindow.Left,
                (int)_widgetWindow.Top,
                (int)_widgetWindow.Width,
                (int)_widgetWindow.Height));
        }
        else
        {
            settings.ShowNearScreen();
        }

        BeginSettingsLocationRefresh();
    }

    private void BeginSettingsLocationRefresh() => _ = RefreshLocationIfAutoAsync();

    private async Task RefreshLocationIfAutoAsync()
    {
        if (_settingsService.Settings.UseManualLocation)
        {
            return;
        }

        try
        {
            await RefreshDataAsync(forceLocation: true);
        }
        catch
        {
            _settingsViewModel.RefreshDetectedLocation();
        }
    }

    public void HandleNativeSlotCommand(NativeSlotCommand command)
    {
        if (_disposed || command == NativeSlotCommand.None)
        {
            return;
        }

        var dispatcher = WpfApplication.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            ExecuteNativeSlotCommand(command);
        }
        else
        {
            dispatcher.Invoke(() => ExecuteNativeSlotCommand(command));
        }
    }

    private void ExecuteNativeSlotCommand(NativeSlotCommand command)
    {
        var now = Environment.TickCount64;
        if (command == _lastNativeCommand && now - _lastNativeCommandTick < 150)
        {
            return;
        }

        _lastNativeCommand = command;
        _lastNativeCommandTick = now;

        switch (command)
        {
            case NativeSlotCommand.ShowFlyout:
                if (_flyoutWindow is { IsFlyoutOpen: true })
                {
                    _flyoutWindow.HideFlyout();
                }
                else
                {
                    ShowFlyoutPanel();
                }
                break;
            case NativeSlotCommand.ShowSettings:
                if (EnsureSettingsWindow() is { IsFlyoutOpen: true } settingsOpen)
                {
                    settingsOpen.HideFlyout();
                }
                else
                {
                    ShowSettingsPanel();
                }
                break;
            case NativeSlotCommand.RefreshLocation:
                _ = RefreshDataAsync(forceLocation: true);
                break;
        }
    }

    private void ShowFlyoutPanel()
    {
        if (_flyoutWindow is null)
        {
            return;
        }

        HideFlyouts(_flyoutWindow);

        if (_widgetWindow is { IsVisible: true })
        {
            _flyoutWindow.ShowNear(_widgetWindow);
            return;
        }

        _flyoutWindow.ShowNearScreen();
    }

    private void ShowSettingsPanel()
    {
        var settings = EnsureSettingsWindow();
        HideFlyouts(settings);

        if (_widgetWindow is { IsVisible: true })
        {
            settings.ShowNear(new System.Drawing.Rectangle(
                (int)_widgetWindow.Left,
                (int)_widgetWindow.Top,
                (int)_widgetWindow.Width,
                (int)_widgetWindow.Height));
        }
        else
        {
            settings.ShowNearScreen();
        }

        BeginSettingsLocationRefresh();
    }

    private void ClearStaleNativeCommandFile()
    {
        if (string.IsNullOrWhiteSpace(_nativeCommandFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(_nativeCommandFilePath))
            {
                File.Delete(_nativeCommandFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void StartNativeCommandPolling()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "PrayerTray");
        Directory.CreateDirectory(folder);
        _nativeCommandFilePath = Path.Combine(folder, "taskbar-command.txt");

        _nativeCommandTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _nativeCommandTimer.Tick += (_, _) => TryConsumeNativeCommandFile();
        _nativeCommandTimer.Start();
        TryConsumeNativeCommandFile();
    }

    private void TryConsumeNativeCommandFile()
    {
        if (string.IsNullOrWhiteSpace(_nativeCommandFilePath) || !File.Exists(_nativeCommandFilePath))
        {
            return;
        }

        try
        {
            var commandText = File.ReadAllText(_nativeCommandFilePath).Trim();
            File.Delete(_nativeCommandFilePath);

            var command = commandText switch
            {
                "SHOW_FLYOUT" => NativeSlotCommand.ShowFlyout,
                "SHOW_SETTINGS" => NativeSlotCommand.ShowSettings,
                "SHOW_CONTEXT_MENU" => NativeSlotCommand.ShowSettings,
                "REFRESH_LOCATION" => NativeSlotCommand.RefreshLocation,
                _ => NativeSlotCommand.None
            };

            HandleNativeSlotCommand(command);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public async Task RefreshDataAsync(bool forceLocation = false)
    {
        try
        {
            LocationInfo location;
            if (forceLocation
                || _settingsService.Settings.UseManualLocation
                || _settingsService.Settings.LastKnownLocation is null)
            {
                location = await _locationService.ResolveLocationAsync();
            }
            else
            {
                location = _settingsService.Settings.LastKnownLocation!;
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var schedule = _prayerTimeService.CurrentSchedule;
            if (forceLocation
                || schedule is null
                || schedule.Date != today
                || !_prayerTimeService.HasNextScheduleFor(today)
                || LocationChangedSignificantly(schedule.Location, location))
            {
                await _prayerTimeService.FetchScheduleAsync(location);
            }

            _widgetViewModel.RefreshTheme();
            _widgetViewModel.RefreshDisplay();
            _flyoutViewModel.Refresh();
            _settingsViewModel.RefreshDetectedLocation();

            if (_settingsService.Settings.ShowWidget && !_fullscreenDetector.IsFullscreen)
            {
                ShowWidget();
            }
            else
            {
                HideWidget();
            }
        }
        catch (Exception ex)
        {
            _widgetViewModel.RefreshDisplay();
            if (_notifyIcon is not null)
            {
                _notifyIcon.BalloonTipTitle = _localizationService.Get("AppName");
                _notifyIcon.BalloonTipText = ex.Message;
                _notifyIcon.ShowBalloonTip(3000);
            }
        }
    }

    private void StartThemePolling()
    {
        _themeTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _themeTimer.Tick += (_, _) => _widgetViewModel.RefreshTheme();
        _themeTimer.Start();
    }

    private void StartScheduleRefreshPolling()
    {
        _scheduleRefreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _scheduleRefreshTimer.Tick += async (_, _) =>
        {
            if (_scheduleRefreshTickRunning)
            {
                return;
            }

            try
            {
                _scheduleRefreshTickRunning = true;
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_prayerTimeService.CurrentSchedule?.Date != today
                    || !_prayerTimeService.HasNextScheduleFor(today))
                {
                    await RefreshDataAsync(forceLocation: false);
                }
            }
            finally
            {
                _scheduleRefreshTickRunning = false;
            }
        };
        _scheduleRefreshTimer.Start();
    }

    public void OnTaskbarCreated()
    {
        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_settingsService.Settings.ShowWidget && !_fullscreenDetector.IsFullscreen)
            {
                ApplyWidgetVisibility();
            }
        });
    }

    public void RegisterTaskbarCreatedMessage(Action<uint> registerCallback)
    {
        _taskbarCreatedMessage = Win32.RegisterWindowMessage("TaskbarCreated");
        registerCallback(_taskbarCreatedMessage);
    }

    private static bool LocationChangedSignificantly(LocationInfo previous, LocationInfo current)
    {
        if (!string.Equals(previous.City, current.City, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previous.Country, current.Country, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (previous.Latitude == 0 && previous.Longitude == 0)
        {
            return current.Latitude != 0 || current.Longitude != 0;
        }

        const double earthRadiusKm = 6371;
        var lat1 = previous.Latitude * Math.PI / 180;
        var lat2 = current.Latitude * Math.PI / 180;
        var deltaLat = (current.Latitude - previous.Latitude) * Math.PI / 180;
        var deltaLon = (current.Longitude - previous.Longitude) * Math.PI / 180;
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2)
                * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var distanceKm = earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return distanceKm > 5;
    }

    private void Shutdown()
    {
        WpfApplication.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _scheduleRefreshTimer?.Stop();
        _themeTimer?.Stop();
        _nativeCommandTimer?.Stop();
        _widgetViewModel.Dispose();
        _settingsViewModel.Dispose();
        _prayerNotificationService.Dispose();
        _slotPublisher.Dispose();
        _embedService.Dispose();
        _fullscreenDetector.Dispose();
        _flyoutDismissService?.Dispose();
        DisposeTrayIcon();
    }
}
