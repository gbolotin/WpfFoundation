using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfFoundation.Converters;

/// <summary>Shows the element when both bound values are the same non-null object and collapses it otherwise.</summary>
public sealed class ReferenceEqualsToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is not null && values[0] != DependencyProperty.UnsetValue && ReferenceEquals(values[0], values[1])
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
