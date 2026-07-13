using PrayerTray.Models;

namespace PrayerTray.Services;

public enum TaskbarDisplayMode
{
    /// <summary>HWND fallback child of Shell_TrayWnd (overlays XAML tray).</summary>
    Embed,

    /// <summary>Windhawk mod injects a real Windows 11 system tray frame slot.</summary>
    NativeSlot
}

public static class TaskbarDisplay
{
    public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    public static TaskbarDisplayMode ResolveMode(AppSettings settings) =>
        settings.TaskbarDisplayMode switch
        {
            "Embed" => TaskbarDisplayMode.Embed,
            "NativeSlot" => TaskbarDisplayMode.NativeSlot,
            _ => IsWindows11 ? TaskbarDisplayMode.NativeSlot : TaskbarDisplayMode.Embed
        };

    public static bool UsesNativeSlot(AppSettings settings) =>
        ResolveMode(settings) == TaskbarDisplayMode.NativeSlot;
}
