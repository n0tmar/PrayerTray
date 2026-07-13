using System.IO;

namespace PrayerTray.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsFirstInstance { get; }

    private static void TryNotifyExistingInstance()
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PrayerTray");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "taskbar-command.txt"), "SHOW_SETTINGS");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void NotifyExistingInstance() => TryNotifyExistingInstance();

    public SingleInstanceService()
    {
        _mutex = new Mutex(true, "PrayerTray_SingleInstance_Mutex", out var createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
