using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfFoundation.Converters;

/// <summary>Shows the element when the value is <see langword="false"/> and collapses it otherwise.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
