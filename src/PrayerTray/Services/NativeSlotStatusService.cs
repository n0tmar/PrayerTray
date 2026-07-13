using System.IO;

namespace PrayerTray.Services;

public sealed class NativeSlotStatusService
{
    internal const string StatusFileName = "taskbar-slot-status.txt";

    private readonly string _statusFilePath;

    public NativeSlotStatusService()
        : this(GetDefaultStatusFilePath())
    {
    }

    internal NativeSlotStatusService(string statusFilePath)
    {
        _statusFilePath = statusFilePath;
        var folder = Path.GetDirectoryName(_statusFilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_statusFilePath))
            {
                File.Delete(_statusFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public bool IsVisibleAndFresh(TimeSpan maxAge) =>
        IsVisibleAndFresh(DateTimeOffset.UtcNow, maxAge);

    internal bool IsVisibleAndFresh(DateTimeOffset now, TimeSpan maxAge)
    {
        try
        {
            if (!File.Exists(_statusFilePath))
            {
                return false;
            }

            var visible = false;
            foreach (var line in File.ReadLines(_statusFilePath))
            {
                if (line.Equals("VISIBLE=1", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("VISIBLE=true", StringComparison.OrdinalIgnoreCase))
                {
                    visible = true;
                    break;
                }
            }

            if (!visible)
            {
                return false;
            }

            var lastWrite = File.GetLastWriteTimeUtc(_statusFilePath);
            return now.UtcDateTime - lastWrite <= maxAge;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string GetDefaultStatusFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "PrayerTray", StatusFileName);
    }
}
