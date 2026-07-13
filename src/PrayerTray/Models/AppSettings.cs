namespace PrayerTray.Models;

public sealed class AppSettings
{
    public bool UseManualLocation { get; set; }
    public string ManualCity { get; set; } = string.Empty;
    public string ManualCountry { get; set; } = string.Empty;
    public string ManualTimezone { get; set; } = string.Empty;
    public double? ManualLatitude { get; set; }
    public double? ManualLongitude { get; set; }
    public int? CalculationMethod { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public bool ShowWidget { get; set; } = true;
    public bool ShowNotificationIcon { get; set; }
    public bool PlayPrayerNotification { get; set; }

    /// <summary>Embed = HWND fallback overlay; NativeSlot = Windhawk mod (Win11).</summary>
    public string TaskbarDisplayMode { get; set; } = "NativeSlot";
    public string TaskbarContentMode { get; set; } = TaskbarContentModes.Countdown;
    public string TimeFormat { get; set; } = TimeFormats.TwelveHour;

    public string Language { get; set; } = "en";

    public LocationInfo? LastKnownLocation { get; set; }
}
