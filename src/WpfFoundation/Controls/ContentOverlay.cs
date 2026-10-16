using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace WpfFoundation.Controls;

/// <summary>
/// A card shown over a window's content, such as a settings frame, with a smoke layer over everything around it. The card
/// shows <see cref="HeaderedContentControl.Header"/> and a close button above <see cref="ContentControl.Content"/>.
/// </summary>
/// <remarks>
/// <para>
/// The close button, a click on the smoke layer outside the card and Esc each close the overlay. They run
/// <see cref="CloseCommand"/> when one is set, which can refuse while it cannot execute; otherwise they set
/// <see cref="IsOpen"/> to <see langword="false"/>. The close button is disabled while closing is refused. Any
/// <see cref="ApplicationCommands.Close"/> raised inside the card closes the overlay the same way.
/// </para>
/// <para>
/// Opening moves keyboard focus into the content and keeps Tab inside the card; closing returns focus to the element
/// that had it. The overlay does not disable what it covers: bind that content's IsEnabled to the inverse of
/// <see cref="IsOpen"/>, so keyboard shortcuts, access keys and screen readers cannot reach it either.
/// </para>
/// </remarks>
[TemplatePart(Name = SmokePartName, Type = typeof(UIElement))]
[TemplatePart(Name = CardPartName, Type = typeof(FrameworkElement))]
public class ContentOverlay : HeaderedContentControl
{
    private const string SmokePartName = "PART_Smoke";
    private const string CardPartName = "PART_Card";

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(ContentOverlay),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand), typeof(ICommand), typeof(ContentOverlay), new PropertyMetadata(null, OnCloseCommandChanged));

    public static readonly DependencyProperty CardMaxWidthProperty = DependencyProperty.Register(
        nameof(CardMaxWidth), typeof(double), typeof(ContentOverlay), new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty CardMaxHeightProperty = DependencyProperty.Register(
        nameof(CardMaxHeight), typeof(double), typeof(ContentOverlay), new PropertyMetadata(double.PositiveInfinity));

    private UIElement? smoke;
    private FrameworkElement? card;
    private ICommand? subscribed;
    private IInputElement? restoreFocus;

    static ContentOverlay()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ContentOverlay), new FrameworkPropertyMetadata(typeof(ContentOverlay)));
        FocusableProperty.OverrideMetadata(typeof(ContentOverlay), new FrameworkPropertyMetadata(false));
        CommandManager.RegisterClassCommandBinding(typeof(ContentOverlay), new CommandBinding(ApplicationCommands.Close, OnClose, OnCanClose));
    }

    public ContentOverlay()
    {
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
    }

    /// <summary>Whether the overlay is shown. It is collapsed while <see langword="false"/>.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Runs when the user closes the overlay, instead of setting <see cref="IsOpen"/> to <see langword="false"/>. The
    /// command decides, for example after saving, and the overlay stays open while the command cannot execute.
    /// </summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>The largest width of the card. The card fills the overlay inside <see cref="Control.Padding"/> up to it.</summary>
    public double CardMaxWidth
    {
        get => (double)GetValue(CardMaxWidthProperty);
        set => SetValue(CardMaxWidthProperty, value);
    }

    /// <summary>The largest height of the card. The card fills the overlay inside <see cref="Control.Padding"/> up to it.</summary>
    public double CardMaxHeight
    {
        get => (double)GetValue(CardMaxHeightProperty);
        set => SetValue(CardMaxHeightProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (smoke is not null)
        {
            smoke.MouseLeftButtonDown -= OnSmokePressed;
        }

        base.OnApplyTemplate();
        smoke = GetTemplateChild(SmokePartName) as UIElement;
        card = GetTemplateChild(CardPartName) as FrameworkElement;
        if (smoke is not null)
        {
            smoke.MouseLeftButtonDown += OnSmokePressed;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Controls inside the card handle Esc first, for example to close an open drop-down.
        if (!e.Handled && e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            RequestClose();
        }
    }

    private static void OnClose(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        ((ContentOverlay)sender).RequestClose();
    }

    private static void OnCanClose(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ((ContentOverlay)sender).CanClose();
        e.Handled = true;
    }

    private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((ContentOverlay)sender).OpenChanged((bool)e.NewValue);

    private static void OnCloseCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (ContentOverlay)sender;
        if (overlay.IsLoaded)
        {
            overlay.Subscribe();
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanClose() => IsOpen && (CloseCommand?.CanExecute(null) ?? true);

    private void RequestClose()
    {
        if (!CanClose())
        {
            return;
        }

        if (CloseCommand is { } command)
        {
            command.Execute(null);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void OnSmokePressed(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        RequestClose();
    }

    // Focus moves once layout has shown or hidden the card, and once bindings have re-enabled the content behind it.
    private void OpenChanged(bool open)
    {
        if (open)
        {
            if (Keyboard.FocusedElement is { } focused && !Contains(focused))
            {
                restoreFocus = focused;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => MoveFocusIntoCard());
        }
        else
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => RestoreFocus());
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void MoveFocusIntoCard()
    {
        if (IsOpen && card is { IsVisible: true } && !card.IsKeyboardFocusWithin)
        {
            card.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }

    private void RestoreFocus()
    {
        if (IsOpen)
        {
            return;
        }

        var target = restoreFocus;
        restoreFocus = null;
        if (target is { IsEnabled: true } && (target is not UIElement element || element.IsVisible))
        {
            target.Focus();
        }
    }

    private bool Contains(IInputElement element)
    {
        for (DependencyObject? current = element as DependencyObject; current is not null; current = Parent(current))
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? Parent(DependencyObject element) =>
        element is Visual or Visual3D
            ? VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element)
            : LogicalTreeHelper.GetParent(element);

    // Follows the close command only while loaded, so a long-lived command never keeps a closed window alive.
    private void Subscribe()
    {
        Unsubscribe();
        if (CloseCommand is { } command)
        {
            subscribed = command;
            command.CanExecuteChanged += OnCloseCommandCanExecuteChanged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribed is not null)
        {
            subscribed.CanExecuteChanged -= OnCloseCommandCanExecuteChanged;
            subscribed = null;
        }
    }

    private void OnCloseCommandCanExecuteChanged(object? sender, EventArgs e) => CommandManager.InvalidateRequerySuggested();
}
