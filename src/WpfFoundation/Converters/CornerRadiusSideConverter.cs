using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfFoundation.Converters;

/// <summary>The side of a rectangle whose corners a <see cref="CornerRadiusSideConverter"/> keeps rounded.</summary>
internal enum CornerSide
{
    Left,
    Right,
    Top
}

/// <summary>
/// Keeps the corners on one side of a <see cref="CornerRadius"/> and squares the others, so joined parts of one
/// control, such as the halves of a split button or the header row of a card, share the theme's corner radius.
/// </summary>
internal sealed class CornerRadiusSideConverter : IValueConverter
{
    /// <summary>The side whose corners stay rounded.</summary>
    public CornerSide Side { get; set; } = CornerSide.Left;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CornerRadius radius)
        {
            return DependencyProperty.UnsetValue;
        }

        return Side switch
        {
            CornerSide.Right => new CornerRadius(0, radius.TopRight, radius.BottomRight, 0),
            CornerSide.Top => new CornerRadius(radius.TopLeft, radius.TopRight, 0, 0),
            _ => new CornerRadius(radius.TopLeft, 0, 0, radius.BottomLeft)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
