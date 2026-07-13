using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrayerTray.Converters;

public sealed class HighlightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isHighlighted = value is true;
        return new SolidColorBrush(isHighlighted
            ? System.Windows.Media.Color.FromArgb(80, 255, 255, 255)
            : Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
