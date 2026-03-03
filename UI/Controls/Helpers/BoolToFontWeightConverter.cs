using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace UI.Controls.Helpers;

public class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? FontWeight.SemiBold : FontWeight.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}