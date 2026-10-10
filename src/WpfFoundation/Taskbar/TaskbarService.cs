using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace WpfFoundation.Taskbar;

/// <summary>
/// Drives the <see cref="TaskbarItemInfo"/> of the application's main window, creating it on first use. Overlay badges
/// are Segoe Fluent Icons glyphs in the Fluent theme's status colors, redrawn when the theme or the window's DPI changes.
/// </summary>
public sealed class TaskbarService(Application application) : ITaskbarService
{
    private const double OverlaySize = 16;

    // Follows the overlay's Fluent brush on the window, so a theme or high contrast change redraws the badge.
    private static readonly DependencyProperty OverlayBrushProperty = DependencyProperty.RegisterAttached(
        "OverlayBrush", typeof(Brush), typeof(TaskbarService), new PropertyMetadata(null, OnOverlayBrushChanged));

    private static readonly DependencyProperty ServiceProperty = DependencyProperty.RegisterAttached(
        "Service", typeof(TaskbarService), typeof(TaskbarService));

    private Window? window;
    private TaskbarOverlay overlay;
    private volatile bool isWindowActive;

    /// <inheritdoc />
    public event EventHandler? WindowActivated;

    /// <inheritdoc />
    public bool IsWindowActive =>
        application.Dispatcher.CheckAccess() ? IsInFront(application.MainWindow) : isWindowActive;

    /// <inheritdoc />
    public void SetProgress(double? value, TaskbarProgressState state = TaskbarProgressState.Normal)
    {
        if (value is { } fraction && !(fraction >= 0 && fraction <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Progress is a fraction from 0 to 1.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown progress state.");
        }

        Run(info =>
        {
            if (value is { } shown)
            {
                info.ProgressValue = shown;
            }
            else if (state == TaskbarProgressState.Error && info.ProgressState is TaskbarItemProgressState.None or TaskbarItemProgressState.Indeterminate)
            {
                // A failure before any percentage fills the bar, so the red state is visible.
                info.ProgressValue = 1;
            }

            info.ProgressState = (value, state) switch
            {
                (null, TaskbarProgressState.Normal) => TaskbarItemProgressState.Indeterminate,
                (_, TaskbarProgressState.Normal) => TaskbarItemProgressState.Normal,
                (_, TaskbarProgressState.Paused) => TaskbarItemProgressState.Paused,
                _ => TaskbarItemProgressState.Error
            };
        });
    }

    /// <inheritdoc />
    public void ClearProgress() => Run(info =>
    {
        info.ProgressState = TaskbarItemProgressState.None;
        info.ProgressValue = 0;
    });

    /// <inheritdoc />
    public void ShowOverlay(TaskbarOverlay overlay, string? description = null)
    {
        if (!Enum.IsDefined(overlay))
        {
            throw new ArgumentOutOfRangeException(nameof(overlay), overlay, "Unknown overlay.");
        }

        Run(info =>
        {
            this.overlay = overlay;
            info.Description = overlay == TaskbarOverlay.None ? string.Empty : description ?? string.Empty;
            if (overlay == TaskbarOverlay.None)
            {
                window!.ClearValue(OverlayBrushProperty);
                info.Overlay = null;
            }
            else
            {
                // Setting the reference renders the badge through OnOverlayBrushChanged.
                window!.SetResourceReference(OverlayBrushProperty, BrushKey(overlay));
                RenderOverlay();
            }
        });
    }

    /// <inheritdoc />
    public void FlashUntilActivated() => Run(_ =>
    {
        nint handle = new WindowInteropHelper(window!).Handle;
        if (IsInFront(window) || handle == 0)
        {
            return;
        }

        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = handle,
            Flags = FlashTray | FlashUntilForeground,
            Count = uint.MaxValue
        };
        FlashWindowEx(ref info);
    });

    private void Run(Action<TaskbarItemInfo> change)
    {
        if (application.Dispatcher.CheckAccess())
        {
            Apply(change);
        }
        else
        {
            application.Dispatcher.InvokeAsync(() => Apply(change));
        }
    }

    private void Apply(Action<TaskbarItemInfo> change)
    {
        if (Attach() is { } info)
        {
            change(info);
        }
    }

    private TaskbarItemInfo? Attach()
    {
        var main = application.MainWindow;
        if (main is null)
        {
            Detach();
            return null;
        }

        if (main != window)
        {
            Detach();
            window = main;
            window.SetValue(ServiceProperty, this);
            window.Activated += OnActivated;
            window.Deactivated += OnDeactivated;
            window.StateChanged += OnStateChanged;
            isWindowActive = IsInFront(window);
            window.DpiChanged += OnDpiChanged;
            window.Closed += OnClosed;
        }

        return window.TaskbarItemInfo ??= new TaskbarItemInfo();
    }

    private void Detach()
    {
        if (window is null)
        {
            return;
        }

        window.Activated -= OnActivated;
        window.Deactivated -= OnDeactivated;
        window.StateChanged -= OnStateChanged;
        isWindowActive = false;
        window.DpiChanged -= OnDpiChanged;
        window.Closed -= OnClosed;
        window.ClearValue(OverlayBrushProperty);
        window.ClearValue(ServiceProperty);
        window = null;
        overlay = TaskbarOverlay.None;
    }

    private void OnClosed(object? sender, EventArgs e) => Detach();

    private void OnDeactivated(object? sender, EventArgs e) => isWindowActive = false;

    private void OnStateChanged(object? sender, EventArgs e) => isWindowActive = IsInFront(window);

    // A minimized window can stay the active window, for example when nothing else could take the focus.
    private static bool IsInFront(Window? window) => window is { IsActive: true, WindowState: not WindowState.Minimized };

    private void OnActivated(object? sender, EventArgs e)
    {
        isWindowActive = IsInFront(window);
        if (window?.TaskbarItemInfo is { } info)
        {
            if (overlay != TaskbarOverlay.None)
            {
                ShowOverlay(TaskbarOverlay.None);
            }

            if (info.ProgressState == TaskbarItemProgressState.Error)
            {
                ClearProgress();
            }
        }

        WindowActivated?.Invoke(this, EventArgs.Empty);
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e) => RenderOverlay();

    private static void OnOverlayBrushChanged(DependencyObject target, DependencyPropertyChangedEventArgs e) =>
        (target.GetValue(ServiceProperty) as TaskbarService)?.RenderOverlay();

    private void RenderOverlay()
    {
        if (window?.TaskbarItemInfo is not { } info || overlay == TaskbarOverlay.None || window.GetValue(OverlayBrushProperty) is not Brush brush)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var font = window.TryFindResource("SymbolThemeFontFamily") as FontFamily ?? new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        var glyph = new FormattedText(
            Glyph(overlay),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            OverlaySize,
            brush,
            dpi.PixelsPerDip);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawText(glyph, new Point((OverlaySize - glyph.Width) / 2, (OverlaySize - glyph.Height) / 2));
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(OverlaySize * dpi.DpiScaleX),
            (int)Math.Ceiling(OverlaySize * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        info.Overlay = bitmap;
    }

    private static string Glyph(TaskbarOverlay overlay) => overlay switch
    {
        TaskbarOverlay.Success => "\uEC61", // CompletedSolid
        TaskbarOverlay.Warning => "\uE814", // IncidentTriangle
        _ => "\uEB90" // StatusErrorFull
    };

    private static string BrushKey(TaskbarOverlay overlay) => overlay switch
    {
        TaskbarOverlay.Success => "SystemFillColorSuccessBrush",
        TaskbarOverlay.Warning => "SystemFillColorCautionBrush",
        _ => "SystemFillColorCriticalBrush"
    };

    private const uint FlashTray = 0x2;
    private const uint FlashUntilForeground = 0xC;

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public nint Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);
}
