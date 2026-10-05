using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfFoundation.Converters;

/// <summary>
/// Keeps the left or right corners of a <see cref="CornerRadius"/> and squares the others, so two joined
/// parts of one control, such as the halves of a split button, share the theme's corner radius.
/// </summary>
internal sealed class CornerRadiusSideConverter : IValueConverter
{
    /// <summary>The side whose corners stay rounded: <see cref="HorizontalAlignment.Left"/> or <see cref="HorizontalAlignment.Right"/>.</summary>
    public HorizontalAlignment Side { get; set; } = HorizontalAlignment.Left;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CornerRadius radius)
        {
            return DependencyProperty.UnsetValue;
        }

        return Side == HorizontalAlignment.Right
            ? new CornerRadius(0, radius.TopRight, radius.BottomRight, 0)
            : new CornerRadius(radius.TopLeft, 0, 0, radius.BottomLeft);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
