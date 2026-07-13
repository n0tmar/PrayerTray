using Microsoft.Win32;

namespace PrayerTray.Services;

public sealed class ThemeService
{
    public bool IsLightTheme => ReadAppsUseLightTheme();

    public string TaskbarTextColor => IsLightTheme ? "#1A1A1A" : "#FFFFFF";

    private static bool ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 1;
        }
        catch
        {
            return false;
        }
    }
}
