using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WpfFoundation.Controls;

/// <summary>
/// Single-line text that scrolls continuously when it is wider than the control, and is trimmed with an
/// ellipsis otherwise. Scrolling stops while the control is hidden or unloaded, when
/// <see cref="IsAnimationEnabled"/> is <see langword="false"/>, and when Windows animation effects are off.
/// </summary>
public sealed class MarqueeTextBlock : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MarqueeTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextChanged));

    /// <summary>The space between the end of the text and its repeat while scrolling, in device-independent pixels.</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(MarqueeTextBlock),
        new PropertyMetadata(48.0, OnAnimationPropertyChanged), IsNonNegative);

    /// <summary>The scrolling speed, in device-independent pixels per second.</summary>
    public static readonly DependencyProperty PixelsPerSecondProperty = DependencyProperty.Register(
        nameof(PixelsPerSecond), typeof(double), typeof(MarqueeTextBlock),
        new PropertyMetadata(42.0, OnAnimationPropertyChanged), IsPositive);

    /// <summary>Whether overflowing text scrolls. Windows animation effects must also be on.</summary>
    public static readonly DependencyProperty IsAnimationEnabledProperty = DependencyProperty.Register(
        nameof(IsAnimationEnabled), typeof(bool), typeof(MarqueeTextBlock),
        new PropertyMetadata(true, OnAnimationPropertyChanged));

    private readonly TextBlock primaryText = CreateTextBlock();
    private readonly TextBlock repeatedText = CreateTextBlock();
    private readonly Grid viewport;
    private readonly TranslateTransform translation = new();
    private bool updateQueued;

    public MarqueeTextBlock()
    {
        Focusable = false;
        repeatedText.Visibility = Visibility.Collapsed;

        var strip = new Canvas { RenderTransform = translation };
        strip.Children.Add(primaryText);
        strip.Children.Add(repeatedText);

        // An invisible copy gives the control the height of one line of text.
        var heightSizer = CreateTextBlock();
        heightSizer.Opacity = 0;
        heightSizer.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Text)) { Source = this });

        viewport = new Grid { ClipToBounds = true };
        viewport.Children.Add(heightSizer);
        viewport.Children.Add(strip);
        AddVisualChild(viewport);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => ScheduleUpdate();
        SizeChanged += (_, _) => ScheduleUpdate();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double PixelsPerSecond
    {
        get => (double)GetValue(PixelsPerSecondProperty);
        set => SetValue(PixelsPerSecondProperty, value);
    }

    public bool IsAnimationEnabled
    {
        get => (bool)GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    /// <summary>Whether the text is currently scrolling.</summary>
    public bool IsScrolling { get; private set; }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => index == 0 ? viewport : throw new ArgumentOutOfRangeException(nameof(index));

    protected override Size MeasureOverride(Size constraint)
    {
        viewport.Measure(constraint);
        return viewport.DesiredSize;
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        viewport.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    private static TextBlock CreateTextBlock() => new() { TextWrapping = TextWrapping.NoWrap };

    private static bool IsNonNegative(object value) => value is double number && double.IsFinite(number) && number >= 0;

    private static bool IsPositive(object value) => value is double number && double.IsFinite(number) && number > 0;

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var marquee = (MarqueeTextBlock)sender;
        string text = e.NewValue as string ?? string.Empty;
        marquee.primaryText.Text = text;
        marquee.repeatedText.Text = text;
        marquee.ScheduleUpdate();
    }

    private static void OnAnimationPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((MarqueeTextBlock)sender).ScheduleUpdate();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        ScheduleUpdate();
    }

    // The static event would otherwise keep the control alive after it leaves the visual tree.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        StopScrolling();
    }

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
        {
            ScheduleUpdate();
        }
    }

    private void ScheduleUpdate()
    {
        StopScrolling();
        if (!IsLoaded || updateQueued)
        {
            return;
        }

        // Wait for layout so the control's width is final.
        updateQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            updateQueued = false;
            Update();
        }, DispatcherPriority.Loaded);
    }

    private void Update()
    {
        StopScrolling();
        if (!IsLoaded || !IsVisible || !IsAnimationEnabled || !SystemParameters.ClientAreaAnimation || string.IsNullOrEmpty(Text))
        {
            return;
        }

        primaryText.ClearValue(WidthProperty);
        primaryText.TextTrimming = TextTrimming.None;
        primaryText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double textWidth = primaryText.DesiredSize.Width;
        if (textWidth <= ActualWidth + 0.5)
        {
            StopScrolling();
            return;
        }

        double distance = textWidth + Gap;
        Canvas.SetLeft(repeatedText, distance);
        repeatedText.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(0, -distance, new Duration(TimeSpan.FromSeconds(distance / PixelsPerSecond)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        translation.BeginAnimation(TranslateTransform.XProperty, animation);
        IsScrolling = true;
    }

    private void StopScrolling()
    {
        translation.BeginAnimation(TranslateTransform.XProperty, null);
        translation.X = 0;
        repeatedText.Visibility = Visibility.Collapsed;
        primaryText.Width = ActualWidth;
        primaryText.TextTrimming = TextTrimming.CharacterEllipsis;
        IsScrolling = false;
    }
}
