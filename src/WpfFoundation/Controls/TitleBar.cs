using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace WpfFoundation.Controls;

/// <summary>
/// The title bar of a window that shows its own content, such as a menu, in the title bar. It shows the window's
/// <see cref="Window.Icon"/>, then the window's <see cref="Window.Title"/>, then <see cref="ContentControl.Content"/>.
/// </summary>
/// <remarks>
/// <para>
/// Place it at the top of the window's content. While the window shows the Fluent backdrop, which needs
/// <see cref="Application.ThemeMode"/> on Windows 11 without high contrast and leaves the window's Background
/// transparent, the title bar replaces the system title bar through <see cref="WindowChrome"/> and
/// <see cref="IsExtended"/> is <see langword="true"/>. Windows still draws the minimize, maximize and close buttons,
/// with Snap Layouts, at the right of the row. Dragging the row moves the window, a double-click maximizes it, and a
/// right-click or Alt+Space opens the system menu; a click on the icon opens the system menu and a double-click on it
/// closes the window. The content takes its own clicks.
/// </para>
/// <para>
/// Otherwise, or while <see cref="ExtendsIntoTitleBar"/> is <see langword="false"/>, the window keeps its system title
/// bar, and the row shows only the content, as a menu bar below it. The title bar switches between the two when
/// <see cref="ExtendsIntoTitleBar"/> or the window's Background changes, for example when high contrast turns on.
/// While extended and maximized, it sets the window's BorderThickness so the content stays on the screen.
/// </para>
/// </remarks>
[TemplatePart(Name = IconPartName, Type = typeof(FrameworkElement))]
[TemplatePart(Name = CaptionButtonsPartName, Type = typeof(FrameworkElement))]
public class TitleBar : ContentControl
{
    private const string IconPartName = "PART_Icon";
    private const string CaptionButtonsPartName = "PART_CaptionButtons";

    // Inherited, so styles of the content, such as the compact top-level menu items, can trigger on it.
    private static readonly DependencyPropertyKey IsExtendedPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        nameof(IsExtended), typeof(bool), typeof(TitleBar), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IsExtendedProperty = IsExtendedPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ExtendsIntoTitleBarProperty = DependencyProperty.Register(
        nameof(ExtendsIntoTitleBar), typeof(bool), typeof(TitleBar), new PropertyMetadata(true, OnExtendsIntoTitleBarChanged));

    // Follows the window's Background, which the Fluent theme makes opaque when the backdrop is unavailable.
    private static readonly DependencyProperty WindowBackgroundProperty = DependencyProperty.Register(
        "WindowBackground", typeof(Brush), typeof(TitleBar), new PropertyMetadata(null, OnWindowBackgroundChanged));

    private Window? window;
    private WindowChrome? chrome;
    private Thickness? restoreBorderThickness;
    private FrameworkElement? icon;
    private FrameworkElement? captionButtons;
    private UIElement? markedContent;

    static TitleBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TitleBar), new FrameworkPropertyMetadata(typeof(TitleBar)));
        FocusableProperty.OverrideMetadata(typeof(TitleBar), new FrameworkPropertyMetadata(false));
    }

    public TitleBar()
    {
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        SizeChanged += (_, _) => UpdateLayoutMetrics();
    }

    /// <summary>
    /// Whether the title bar replaces the window's system title bar. While <see langword="false"/>, the row shows only
    /// <see cref="ContentControl.Content"/> below the system title bar.
    /// </summary>
    public bool IsExtended
    {
        get => (bool)GetValue(IsExtendedProperty);
        private set => SetValue(IsExtendedPropertyKey, value);
    }

    /// <summary>Whether the element is inside a title bar that replaces the window's system title bar.</summary>
    public static bool GetIsExtended(DependencyObject element) => (bool)element.GetValue(IsExtendedProperty);

    /// <summary>
    /// Whether the title bar replaces the system title bar while the window shows the Fluent backdrop. The default is
    /// <see langword="true"/>; <see langword="false"/> keeps the system title bar, with the content as a menu bar below it.
    /// </summary>
    public bool ExtendsIntoTitleBar
    {
        get => (bool)GetValue(ExtendsIntoTitleBarProperty);
        set => SetValue(ExtendsIntoTitleBarProperty, value);
    }

    private static void OnExtendsIntoTitleBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TitleBar)d).UpdateChrome();

    // An element content inherits from the title bar, its logical parent, rather than from the template's presenter, so
    // it is marked itself to take its own clicks. A value the application set on it wins.
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (oldContent is UIElement old && markedContent == old)
        {
            old.ClearValue(WindowChrome.IsHitTestVisibleInChromeProperty);
        }

        markedContent = null;
        if (newContent is UIElement element && element.ReadLocalValue(WindowChrome.IsHitTestVisibleInChromeProperty) == DependencyProperty.UnsetValue)
        {
            WindowChrome.SetIsHitTestVisibleInChrome(element, true);
            markedContent = element;
        }
    }

    public override void OnApplyTemplate()
    {
        if (icon is not null)
        {
            icon.MouseLeftButtonDown -= OnIconPressed;
            icon.MouseRightButtonUp -= OnIconRightReleased;
        }

        base.OnApplyTemplate();
        icon = GetTemplateChild(IconPartName) as FrameworkElement;
        captionButtons = GetTemplateChild(CaptionButtonsPartName) as FrameworkElement;
        if (icon is not null)
        {
            icon.MouseLeftButtonDown += OnIconPressed;
            icon.MouseRightButtonUp += OnIconRightReleased;
        }

        UpdateLayoutMetrics();
    }

    private void Attach()
    {
        var owner = Window.GetWindow(this);
        if (ReferenceEquals(owner, window))
        {
            return;
        }

        Detach();
        window = owner;
        if (window is null)
        {
            return;
        }

        window.StateChanged += OnWindowChanged;
        window.SizeChanged += OnWindowChanged;
        window.DpiChanged += OnWindowChanged;
        SetBinding(WindowBackgroundProperty, new Binding(nameof(Window.Background)) { Source = window });
        UpdateChrome();
    }

    private void Detach()
    {
        if (window is null)
        {
            return;
        }

        window.StateChanged -= OnWindowChanged;
        window.SizeChanged -= OnWindowChanged;
        window.DpiChanged -= OnWindowChanged;
        RemoveChrome();
        window = null;
        BindingOperations.ClearBinding(this, WindowBackgroundProperty);
    }

    private static void OnWindowBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TitleBar)d).UpdateChrome();

    private void OnWindowChanged(object? sender, EventArgs e) => UpdateChrome();

    private void UpdateChrome()
    {
        if (window is null)
        {
            return;
        }

        if (ExtendsIntoTitleBar && window.Background is SolidColorBrush { Color.A: 0 })
        {
            if (chrome is null || !ReferenceEquals(WindowChrome.GetWindowChrome(window), chrome))
            {
                chrome = new WindowChrome
                {
                    GlassFrameThickness = new Thickness(-1),
                    ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
                    UseAeroCaptionButtons = true,
                    CornerRadius = default
                };
                WindowChrome.SetWindowChrome(window, chrome);
            }

            IsExtended = true;
            UpdateMaximizedInset();
        }
        else
        {
            RemoveChrome();
        }

        UpdateLayoutMetrics();
        // Windows moves the caption buttons after the window changes size or state, so measure them again after it.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateLayoutMetrics);
    }

    private void RemoveChrome()
    {
        if (window is not null && chrome is not null && ReferenceEquals(WindowChrome.GetWindowChrome(window), chrome))
        {
            window.ClearValue(WindowChrome.WindowChromeProperty);
        }

        chrome = null;
        RestoreBorderThickness();
        IsExtended = false;
    }

    // A maximized window without a system frame reaches past the screen edges by the width of its resize border.
    private void UpdateMaximizedInset()
    {
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Maximized)
        {
            restoreBorderThickness ??= window.BorderThickness;
            window.SetCurrentValue(Control.BorderThicknessProperty, SystemParameters.WindowResizeBorderThickness);
        }
        else
        {
            RestoreBorderThickness();
        }
    }

    private void RestoreBorderThickness()
    {
        if (window is not null && restoreBorderThickness is { } thickness)
        {
            window.SetCurrentValue(Control.BorderThicknessProperty, thickness);
        }

        restoreBorderThickness = null;
    }

    // Keeps the content clear of the caption buttons and makes the row above it, to the bottom of the title bar, the
    // window's caption.
    private void UpdateLayoutMetrics()
    {
        if (window is null || !IsExtended || chrome is null || !IsLoaded)
        {
            return;
        }

        var bottom = TranslatePoint(new Point(0, ActualHeight), window).Y;
        chrome.CaptionHeight = Math.Max(0, bottom - chrome.ResizeBorderThickness.Top);
        if (captionButtons is null)
        {
            return;
        }

        var right = TranslatePoint(new Point(ActualWidth, 0), window).X;
        if (CaptionButtonBounds(window) is { } bounds)
        {
            captionButtons.Width = Math.Max(0, right - bounds.Left);
            captionButtons.Height = bounds.Height;
        }
        else
        {
            captionButtons.Width = 3 * SystemParameters.WindowCaptionButtonWidth;
            captionButtons.Height = SystemParameters.WindowCaptionHeight;
        }
    }

    /// <summary>Where Windows draws the caption buttons, in device-independent units relative to the window.</summary>
    private static Rect? CaptionButtonBounds(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0 || DwmGetWindowAttribute(handle, CaptionButtonBoundsAttribute, out var bounds, Marshal.SizeOf<NativeRect>()) != 0
            || bounds.Right <= bounds.Left)
        {
            return null;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        return new Rect(
            bounds.Left / dpi.DpiScaleX,
            bounds.Top / dpi.DpiScaleY,
            (bounds.Right - bounds.Left) / dpi.DpiScaleX,
            (bounds.Bottom - bounds.Top) / dpi.DpiScaleY);
    }

    private void OnIconPressed(object sender, MouseButtonEventArgs e)
    {
        if (window is null || !IsExtended)
        {
            return;
        }

        e.Handled = true;
        if (e.ClickCount == 2)
        {
            SystemCommands.CloseWindow(window);
        }
        else
        {
            ShowSystemMenu(icon!, new Point(0, icon!.ActualHeight));
        }
    }

    private void OnIconRightReleased(object sender, MouseButtonEventArgs e)
    {
        if (window is null || !IsExtended)
        {
            return;
        }

        e.Handled = true;
        ShowSystemMenu(icon!, e.GetPosition(icon));
    }

    private void ShowSystemMenu(Visual relativeTo, Point point)
    {
        // ShowSystemMenu takes device-independent screen coordinates; PointToScreen returns device pixels.
        var screen = relativeTo.PointToScreen(point);
        var dpi = VisualTreeHelper.GetDpi(relativeTo);
        SystemCommands.ShowSystemMenu(window!, new Point(screen.X / dpi.DpiScaleX, screen.Y / dpi.DpiScaleY));
    }

    private const int CaptionButtonBoundsAttribute = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, int size);
}
