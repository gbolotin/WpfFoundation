using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfFoundation.Behaviors;

/// <summary>Adds a placeholder, a non-editable suffix, and a trailing icon without replacing a TextBox template.</summary>
public static class TextBoxAssist
{
    /// <summary>Text displayed while the TextBox is empty, including while it has focus.</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(TextBoxAssist), new PropertyMetadata(null, OnDecorationChanged));

    /// <summary>Text displayed after the value, excluded from Text, selection, and source bindings.</summary>
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.RegisterAttached(
        "Suffix", typeof(string), typeof(TextBoxAssist), new PropertyMetadata(null, OnDecorationChanged));

    /// <summary>Segoe Fluent Icons glyph displayed on the right side of the TextBox, after the clear button.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(string), typeof(TextBoxAssist), new PropertyMetadata(null, OnDecorationChanged));

    private static readonly DependencyProperty AttachmentProperty = DependencyProperty.RegisterAttached(
        "Attachment", typeof(Attachment), typeof(TextBoxAssist));

    public static string? GetPlaceholder(DependencyObject element) => (string?)element.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject element, string? value) => element.SetValue(PlaceholderProperty, value);
    public static string? GetSuffix(DependencyObject element) => (string?)element.GetValue(SuffixProperty);
    public static void SetSuffix(DependencyObject element, string? value) => element.SetValue(SuffixProperty, value);
    public static string? GetIcon(DependencyObject element) => (string?)element.GetValue(IconProperty);
    public static void SetIcon(DependencyObject element, string? value) => element.SetValue(IconProperty, value);

    private static void OnDecorationChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBox textBox)
        {
            return;
        }
        var attachment = (Attachment?)textBox.GetValue(AttachmentProperty);
        if (string.IsNullOrEmpty(GetPlaceholder(textBox))
            && string.IsNullOrEmpty(GetSuffix(textBox))
            && string.IsNullOrEmpty(GetIcon(textBox)))
        {
            attachment?.Dispose();
            textBox.ClearValue(AttachmentProperty);
            return;
        }
        if (attachment is null)
        {
            attachment = new Attachment(textBox);
            textBox.SetValue(AttachmentProperty, attachment);
        }
        attachment.Refresh(true);
    }

    private sealed class Attachment : IDisposable
    {
        private readonly TextBox textBox;
        private DecorationAdorner? adorner;
        private AdornerLayer? layer;
        private Control? paddingHost;
        private Grid? iconGrid;
        private ColumnDefinition? iconColumn;
        private Border? iconHost;
        private TextBlock? iconGlyph;
        private Button? trackedDeleteButton;
        private bool? iconEnabled;
        private Thickness originalPadding;
        private Thickness appliedPadding;
        private bool paddingAdjusted;
        private bool refreshing;
        private LayoutSnapshot? previous;

        public Attachment(TextBox textBox)
        {
            this.textBox = textBox;
            textBox.Loaded += OnLoaded;
            textBox.Unloaded += OnUnloaded;
            if (textBox.IsLoaded)
            {
                OnLoaded(textBox, new RoutedEventArgs());
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            originalPadding = textBox.Padding;
            textBox.TextChanged -= OnTextChanged;
            textBox.TextChanged += OnTextChanged;
            textBox.LayoutUpdated -= OnLayoutUpdated;
            textBox.LayoutUpdated += OnLayoutUpdated;
            Refresh(true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            textBox.TextChanged -= OnTextChanged;
            textBox.LayoutUpdated -= OnLayoutUpdated;
            RemoveAdorner();
            RestorePadding();
            RemoveIcon();
            paddingHost = null;
            previous = null;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs args) => Refresh(true);
        private void OnLayoutUpdated(object? sender, EventArgs args) => Refresh(false);

        public void Refresh(bool force)
        {
            if (refreshing || !textBox.IsLoaded)
            {
                return;
            }
            refreshing = true;
            try
            {
                UpdateIcon();
                if (string.IsNullOrEmpty(GetPlaceholder(textBox)) && string.IsNullOrEmpty(GetSuffix(textBox)))
                {
                    RemoveAdorner();
                    RestorePadding();
                    return;
                }
                var currentLayer = AdornerLayer.GetAdornerLayer(textBox);
                if (!ReferenceEquals(layer, currentLayer))
                {
                    RemoveAdorner();
                    layer = currentLayer;
                    previous = null;
                }
                if (layer is null)
                {
                    RestorePadding();
                    return;
                }
                if (adorner is null)
                {
                    adorner = new DecorationAdorner(textBox);
                    layer.Add(adorner);
                }
                adorner.Visibility = textBox.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                // Reserve room inside the native content host. Changing TextBox.Padding
                // itself would write the decoration width back through a two-way binding.
                var currentHost = textBox.Template?.FindName("PART_ContentHost", textBox) as Control;
                if (!ReferenceEquals(paddingHost, currentHost))
                {
                    RestorePadding();
                    paddingHost = currentHost;
                    originalPadding = paddingHost?.Padding ?? textBox.Padding;
                }
                if (paddingHost is not null && (!paddingAdjusted || paddingHost.Padding != appliedPadding))
                {
                    originalPadding = paddingHost.Padding;
                }
                string? suffix = GetSuffix(textBox);
                double extra = string.IsNullOrEmpty(suffix) ? 0 : adorner.Format(suffix, textBox.Foreground).WidthIncludingTrailingWhitespace + 4;
                var padding = textBox.FlowDirection == FlowDirection.LeftToRight
                    ? originalPadding with { Right = originalPadding.Right + extra }
                    : originalPadding with { Left = originalPadding.Left + extra };
                appliedPadding = padding;
                paddingAdjusted = extra > 0 && paddingHost is not null;
                if (paddingHost is not null && paddingHost.Padding != padding)
                {
                    paddingHost.SetCurrentValue(Control.PaddingProperty, padding);
                }
                adorner.OriginalPadding = originalPadding;
                var snapshot = new LayoutSnapshot(textBox.Template, textBox.RenderSize, paddingHost?.Padding ?? textBox.Padding,
                    adorner.ContentBounds(), adorner.EndCaret(), textBox.FontFamily, textBox.FontSize,
                    textBox.FontStyle, textBox.FontWeight, textBox.FontStretch, textBox.FlowDirection,
                    VisualTreeHelper.GetDpi(textBox).PixelsPerDip, textBox.IsEnabled, textBox.Text.Length == 0);
                if (force || previous != snapshot)
                {
                    previous = snapshot;
                    adorner.InvalidateVisual();
                }
            }
            finally
            {
                refreshing = false;
            }
        }

        private void UpdateIcon()
        {
            string? icon = GetIcon(textBox);
            if (string.IsNullOrEmpty(icon))
            {
                RemoveIcon();
                return;
            }
            textBox.ApplyTemplate();
            var contentBorder = textBox.Template?.FindName("ContentBorder", textBox) as Border;
            var deleteButton = textBox.Template?.FindName("DeleteButton", textBox) as Button;
            if (contentBorder?.Parent is not Grid grid)
            {
                RemoveIcon();
                return;
            }
            if (!ReferenceEquals(iconGrid, grid))
            {
                RemoveIcon();
                iconGrid = grid;
                iconColumn = new ColumnDefinition { Width = GridLength.Auto };
                grid.ColumnDefinitions.Add(iconColumn);
                Grid.SetColumnSpan(contentBorder, grid.ColumnDefinitions.Count);
                iconGlyph = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconGlyph.SetResourceReference(TextBlock.FontFamilyProperty, "SymbolThemeFontFamily");
                iconGlyph.SetResourceReference(TextBlock.FontSizeProperty, "TextBoxIconFontSize");
                iconHost = new Border
                {
                    MinWidth = 30,
                    Margin = new Thickness(0, 4, 4, 4),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsHitTestVisible = false,
                    Child = iconGlyph
                };
                Grid.SetColumn(iconHost, grid.ColumnDefinitions.Count - 1);
                grid.Children.Add(iconHost);
                if (deleteButton is not null)
                {
                    trackedDeleteButton = deleteButton;
                    deleteButton.CommandTarget = textBox;
                    deleteButton.Margin = new Thickness(0, 4, 0, 4);
                    if (string.IsNullOrEmpty(AutomationProperties.GetName(deleteButton)))
                    {
                        AutomationProperties.SetName(deleteButton, "Clear");
                    }
                }
            }
            if (iconGlyph is not null)
            {
                if (iconGlyph.Text != icon)
                {
                    iconGlyph.Text = icon;
                }
                if (iconEnabled != textBox.IsEnabled)
                {
                    iconEnabled = textBox.IsEnabled;
                    iconGlyph.SetResourceReference(
                        TextBlock.ForegroundProperty,
                        textBox.IsEnabled ? "TextControlButtonForeground" : "TextFillColorDisabledBrush");
                }
            }
            if (deleteButton is not null)
            {
                var visibility = CanShowClearButton(textBox) ? Visibility.Visible : Visibility.Collapsed;
                if (deleteButton.Visibility != visibility)
                {
                    deleteButton.Visibility = visibility;
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private static bool CanShowClearButton(TextBox textBox) =>
            textBox.Text.Length > 0
            && !textBox.IsReadOnly
            && !textBox.AcceptsReturn
            && textBox.TextWrapping == TextWrapping.NoWrap;

        private void RemoveIcon()
        {
            if (iconGrid is not null)
            {
                if (iconHost is not null)
                {
                    iconGrid.Children.Remove(iconHost);
                }
                if (iconColumn is not null)
                {
                    iconGrid.ColumnDefinitions.Remove(iconColumn);
                }
                if (textBox.Template?.FindName("ContentBorder", textBox) is Border contentBorder)
                {
                    Grid.SetColumnSpan(contentBorder, Math.Max(1, iconGrid.ColumnDefinitions.Count));
                }
            }
            if (trackedDeleteButton is not null)
            {
                trackedDeleteButton.ClearValue(UIElement.VisibilityProperty);
                trackedDeleteButton.ClearValue(FrameworkElement.MarginProperty);
                trackedDeleteButton.ClearValue(Button.CommandTargetProperty);
                trackedDeleteButton.ClearValue(AutomationProperties.NameProperty);
            }
            iconHost = null;
            iconGlyph = null;
            iconColumn = null;
            iconGrid = null;
            trackedDeleteButton = null;
            iconEnabled = null;
        }

        private void RestorePadding()
        {
            if (paddingAdjusted && paddingHost?.Padding == appliedPadding)
            {
                paddingHost.SetCurrentValue(Control.PaddingProperty, originalPadding);
            }
            paddingAdjusted = false;
        }

        private void RemoveAdorner()
        {
            if (adorner is not null)
            {
                layer?.Remove(adorner);
            }
            adorner = null;
            layer = null;
        }

        public void Dispose()
        {
            textBox.Loaded -= OnLoaded;
            textBox.Unloaded -= OnUnloaded;
            OnUnloaded(textBox, new RoutedEventArgs());
        }
    }

    private sealed record LayoutSnapshot(ControlTemplate? Template, Size Size, Thickness Padding, Rect Content, Rect Caret,
        FontFamily FontFamily, double FontSize, FontStyle FontStyle, FontWeight FontWeight, FontStretch FontStretch,
        FlowDirection Direction, double PixelsPerDip, bool Enabled, bool Empty);

    private sealed class DecorationAdorner : Adorner
    {
        private static readonly DependencyProperty PlaceholderBrushProperty = DependencyProperty.Register(
            "PlaceholderBrush", typeof(Brush), typeof(DecorationAdorner), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly DependencyProperty SuffixBrushProperty = DependencyProperty.Register(
            "SuffixBrush", typeof(Brush), typeof(DecorationAdorner), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly DependencyProperty DisabledBrushProperty = DependencyProperty.Register(
            "DisabledBrush", typeof(Brush), typeof(DecorationAdorner), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly TextBox textBox;
        public Thickness OriginalPadding { get; set; }

        public DecorationAdorner(TextBox textBox) : base(textBox)
        {
            this.textBox = textBox;
            IsHitTestVisible = false;
            Focusable = false;
            SetResourceReference(PlaceholderBrushProperty, "TextFillColorTertiaryBrush");
            SetResourceReference(SuffixBrushProperty, "TextFillColorSecondaryBrush");
            SetResourceReference(DisabledBrushProperty, "TextFillColorDisabledBrush");
        }

        public FormattedText Format(string text, Brush brush) => new(text, CultureInfo.CurrentCulture, textBox.FlowDirection,
            new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch), textBox.FontSize,
            brush, VisualTreeHelper.GetDpi(textBox).PixelsPerDip);

        public Rect ContentBounds()
        {
            if (textBox.Template?.FindName("PART_ContentHost", textBox) is FrameworkElement host && host.IsVisible)
            {
                return host.TransformToAncestor(textBox).TransformBounds(new Rect(host.RenderSize));
            }
            return new Rect(textBox.RenderSize);
        }

        public Rect EndCaret()
        {
            if (!textBox.IsMeasureValid || !textBox.IsArrangeValid)
            {
                return Rect.Empty;
            }
            return textBox.GetRectFromCharacterIndex(Math.Max(0, textBox.Text.Length - 1), textBox.Text.Length > 0);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var bounds = ContentBounds();
            var caret = EndCaret();
            if (bounds.IsEmpty || caret.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }
            double left = bounds.Left + OriginalPadding.Left;
            double right = bounds.Right - OriginalPadding.Right;
            if (right <= left || caret.Bottom < bounds.Top || caret.Top > bounds.Bottom)
            {
                return;
            }
            var disabled = (Brush?)GetValue(DisabledBrushProperty) ?? textBox.Foreground;
            var placeholderBrush = textBox.IsEnabled ? (Brush?)GetValue(PlaceholderBrushProperty) ?? textBox.Foreground : disabled;
            var suffixBrush = textBox.IsEnabled ? (Brush?)GetValue(SuffixBrushProperty) ?? textBox.Foreground : disabled;
            string? suffix = GetSuffix(textBox);
            var suffixText = string.IsNullOrEmpty(suffix) ? null : Format(suffix, suffixBrush);
            double suffixWidth = suffixText?.WidthIncludingTrailingWhitespace ?? 0;
            double suffixX = textBox.FlowDirection == FlowDirection.LeftToRight ? caret.Right + 4 : caret.Left - suffixWidth - 4;
            drawingContext.PushClip(new RectangleGeometry(bounds));
            if (textBox.Text.Length == 0 && GetPlaceholder(textBox) is { Length: > 0 } placeholder)
            {
                var hint = Format(placeholder, placeholderBrush);
                hint.MaxTextWidth = Math.Max(1, right - left - (suffixText is null ? 0 : suffixWidth + 4));
                hint.MaxLineCount = 1;
                hint.Trimming = TextTrimming.CharacterEllipsis;
                double hintX = textBox.FlowDirection == FlowDirection.LeftToRight ? left : right - hint.WidthIncludingTrailingWhitespace;
                drawingContext.DrawText(hint, new Point(hintX, caret.Top + (caret.Height - hint.Height) / 2));
                suffixX = textBox.FlowDirection == FlowDirection.LeftToRight
                    ? hintX + hint.WidthIncludingTrailingWhitespace + 4 : hintX - suffixWidth - 4;
            }
            if (suffixText is not null && suffixWidth <= right - left)
            {
                suffixX = Math.Clamp(suffixX, left, right - suffixWidth);
                drawingContext.DrawText(suffixText, new Point(suffixX, caret.Top + (caret.Height - suffixText.Height) / 2));
            }
            drawingContext.Pop();
        }
    }
}
