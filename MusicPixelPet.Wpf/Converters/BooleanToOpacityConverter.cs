using System.Globalization;
using System.Windows.Data;

namespace MusicPixelPet.Wpf.Converters;

public sealed class BooleanToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? 1.0 : 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double opacity && opacity >= 0.5;
    }
}
