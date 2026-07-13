using System.Windows;

namespace PrayerTray.Controls;

public partial class SettingsContentRow : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingsContentRow));

    public static readonly DependencyProperty RowContentProperty =
        DependencyProperty.Register(nameof(RowContent), typeof(object), typeof(SettingsContentRow));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? RowContent
    {
        get => GetValue(RowContentProperty);
        set => SetValue(RowContentProperty, value);
    }

    public SettingsContentRow() => InitializeComponent();
}
