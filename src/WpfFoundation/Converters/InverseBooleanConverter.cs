using System.Globalization;
using System.Windows.Data;

namespace WpfFoundation.Converters;
/// <summary>Converts <see langword="false"/> to <see langword="true"/> and anything else to <see langword="false"/>, in both directions.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is false;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is false;
}
