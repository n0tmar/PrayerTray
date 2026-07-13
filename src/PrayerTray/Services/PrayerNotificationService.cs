using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using PrayerTray.Models;

namespace PrayerTray.Services;

public sealed class PrayerNotificationService : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private readonly PrayerTimeService _prayerTimeService;
    private readonly SettingsService _settingsService;
    private readonly PrayerNotificationTracker _tracker = new();
    private readonly IPrayerNotificationPlayer _player;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public PrayerNotificationService(PrayerTimeService prayerTimeService, SettingsService settingsService)
        : this(prayerTimeService, settingsService, new AudioPrayerNotificationPlayer(), Dispatcher.CurrentDispatcher)
    {
    }

    internal PrayerNotificationService(
        PrayerTimeService prayerTimeService,
        SettingsService settingsService,
        IPrayerNotificationPlayer player,
        Dispatcher dispatcher)
    {
        _prayerTimeService = prayerTimeService;
        _settingsService = settingsService;
        _player = player;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = CheckInterval
        };
        _timer.Tick += (_, _) => Check(DateTime.Now);
    }

    public void Start()
    {
        _timer.Start();
        Check(DateTime.Now);
    }

    internal void Check(DateTime now)
    {
        var duePrayer = _tracker.TryMarkDuePrayer(
            _prayerTimeService.CurrentSchedule,
            now,
            _settingsService.Settings.PlayPrayerNotification);

        if (duePrayer is not null)
        {
            _player.Play();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _player.Dispose();
    }
}

internal sealed class PrayerNotificationTracker
{
    internal static readonly TimeSpan DueWindow = TimeSpan.FromMinutes(2);

    private readonly HashSet<string> _playedKeys = [];
    private DateOnly? _activeDate;

    public PrayerEntry? TryMarkDuePrayer(PrayerSchedule? schedule, DateTime now, bool enabled)
    {
        if (!enabled || schedule is null)
        {
            return null;
        }

        if (_activeDate != schedule.Date)
        {
            _activeDate = schedule.Date;
            _playedKeys.Clear();
        }

        foreach (var prayer in schedule.Prayers.OrderBy(prayer => prayer.Time))
        {
            var elapsed = now - prayer.Time;
            if (elapsed < TimeSpan.Zero || elapsed >= DueWindow)
            {
                continue;
            }

            var key = $"{schedule.Date:yyyyMMdd}:{prayer.Name}";
            if (_playedKeys.Add(key))
            {
                return prayer;
            }
        }

        return null;
    }
}

internal interface IPrayerNotificationPlayer : IDisposable
{
    void Play();
}

internal sealed class AudioPrayerNotificationPlayer : IPrayerNotificationPlayer
{
    private const double NotificationVolume = 0.12;

    private readonly List<MediaPlayer> _activePlayers = [];
    private readonly string _soundPath;

    public AudioPrayerNotificationPlayer()
        : this(Path.Combine(AppContext.BaseDirectory, "Resources", "audio", "allah akbar.mp3"))
    {
    }

    internal AudioPrayerNotificationPlayer(string soundPath)
    {
        _soundPath = soundPath;
    }

    public void Play()
    {
        if (!File.Exists(_soundPath))
        {
            return;
        }

        var player = new MediaPlayer
        {
            Volume = NotificationVolume
        };

        EventHandler? endedHandler = null;
        EventHandler<ExceptionEventArgs>? failedHandler = null;
        endedHandler = (_, _) => Cleanup(player, endedHandler, failedHandler);
        failedHandler = (_, _) => Cleanup(player, endedHandler, failedHandler);

        try
        {
            _activePlayers.Add(player);
            player.MediaEnded += endedHandler;
            player.MediaFailed += failedHandler;
            player.Open(new Uri(_soundPath, UriKind.Absolute));
            player.Play();
        }
        catch
        {
            Cleanup(player, endedHandler, failedHandler);
        }
    }

    public void Dispose()
    {
        foreach (var player in _activePlayers.ToArray())
        {
            player.Close();
        }

        _activePlayers.Clear();
    }

    private void Cleanup(
        MediaPlayer player,
        EventHandler? endedHandler,
        EventHandler<ExceptionEventArgs>? failedHandler)
    {
        if (endedHandler is not null)
        {
            player.MediaEnded -= endedHandler;
        }

        if (failedHandler is not null)
        {
            player.MediaFailed -= failedHandler;
        }

        player.Close();
        _activePlayers.Remove(player);
    }
}
