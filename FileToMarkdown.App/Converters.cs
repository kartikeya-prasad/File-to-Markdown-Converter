using Microsoft.UI.Xaml.Data;

namespace FileToMarkdown.App;

/// <summary>Bool → Visibility. Pass ConverterParameter="Invert" to flip.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool v && v;
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility vis && vis == Visibility.Visible;
}
