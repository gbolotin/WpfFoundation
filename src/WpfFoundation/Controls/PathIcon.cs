using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfFoundation.Controls;

/// <summary>
/// A vector icon: draws a <see cref="Geometry"/> designed on a 16 by 16 grid, such as the library's <c>WfIcon*</c>
/// resources, scaled to <see cref="Size"/>. The icon is filled with <see cref="Foreground"/>, which inherits like
/// text does, so an icon inside a button follows the button's hover, pressed, disabled and high contrast colors.
/// </summary>
public sealed class PathIcon : FrameworkElement
{
    /// <summary>The size of the grid that icon geometries are drawn on, in device-independent pixels.</summary>
    public const double GridSize = 16;

    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(PathIcon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The same property as <see cref="TextElement.ForegroundProperty"/>, so the icon inherits the text color around it.</summary>
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(PathIcon),
        new FrameworkPropertyMetadata(
            TextElement.ForegroundProperty.DefaultMetadata.DefaultValue,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The width and height of the icon, in device-independent pixels.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(PathIcon),
        new FrameworkPropertyMetadata(GridSize, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender),
        IsPositive);

    static PathIcon()
    {
        // Centered, so a stretched layout slot doesn't push the icon into a corner.
        HorizontalAlignmentProperty.OverrideMetadata(typeof(PathIcon), new FrameworkPropertyMetadata(HorizontalAlignment.Center));
        VerticalAlignmentProperty.OverrideMetadata(typeof(PathIcon), new FrameworkPropertyMetadata(VerticalAlignment.Center));
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Data is null)
        {
            return;
        }

        double scale = Size / GridSize;
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(Foreground, null, Data);
        drawingContext.Pop();
    }

    private static bool IsPositive(object value) => value is double size && size > 0 && !double.IsPositiveInfinity(size);
}
