using System.IO;
using System.Windows.Interop;

namespace PrayerTray.Services;

public static class NativeMessageHwndService
{
    private static string? _filePath;

    private static string FilePath
    {
        get
        {
            if (_filePath is not null)
            {
                return _filePath;
            }

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PrayerTray");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "message-hwnd.txt");
            return _filePath;
        }
    }

    public static void Write(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            File.WriteAllText(FilePath, hwnd.ToInt64().ToString());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
