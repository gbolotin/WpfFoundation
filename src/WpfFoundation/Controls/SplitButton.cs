using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfFoundation.Controls;

/// <summary>
/// A button with a primary part that runs <see cref="Command"/> and a chevron part that opens
/// <see cref="DropDownMenu"/>, like the Start button in Visual Studio. The primary part shows an optional
/// <see cref="Icon"/> (a Segoe Fluent Icons glyph or any element) before <see cref="ContentControl.Content"/>,
/// and is disabled while the command cannot run; the menu stays available. The primary part is the only tab
/// stop: Enter or Space runs the command, and Alt+Down or F4 opens the menu.
/// </summary>
[TemplatePart(Name = PrimaryButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = DropDownButtonPartName, Type = typeof(ButtonBase))]
public class SplitButton : ContentControl
{
    private const string PrimaryButtonPartName = "PART_PrimaryButton";
    private const string DropDownButtonPartName = "PART_DropDownButton";

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(SplitButton), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(SplitButton), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandTargetProperty = DependencyProperty.Register(
        nameof(CommandTarget), typeof(IInputElement), typeof(SplitButton), new PropertyMetadata(null));

    /// <summary>The icon before the content: a Segoe Fluent Icons glyph string, or any element.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(SplitButton), new PropertyMetadata(null));

    /// <summary>The brush of a glyph icon, for example <c>SystemFillColorSuccessBrush</c> for a green Start icon.</summary>
    public static readonly DependencyProperty IconForegroundProperty = DependencyProperty.Register(
        nameof(IconForeground), typeof(Brush), typeof(SplitButton), new PropertyMetadata(null));

    /// <summary>The menu the chevron part opens under the button.</summary>
    public static readonly DependencyProperty DropDownMenuProperty = DependencyProperty.Register(
        nameof(DropDownMenu), typeof(ContextMenu), typeof(SplitButton), new PropertyMetadata(null, OnDropDownMenuChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(SplitButton),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged, CoerceIsDropDownOpen));

    /// <summary>Raised when the primary part is clicked, just before <see cref="Command"/> runs.</summary>
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
        nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SplitButton));

    private ButtonBase? primaryButton;
    private ButtonBase? dropDownButton;

    static SplitButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
    }

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Brush? IconForeground
    {
        get => (Brush?)GetValue(IconForegroundProperty);
        set => SetValue(IconForegroundProperty, value);
    }

    public ContextMenu? DropDownMenu
    {
        get => (ContextMenu?)GetValue(DropDownMenuProperty);
        set => SetValue(DropDownMenuProperty, value);
    }

    /// <summary>Whether <see cref="DropDownMenu"/> is open. It stays <see langword="false"/> without a menu.</summary>
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (primaryButton is not null)
        {
            primaryButton.Click -= OnPrimaryButtonClick;
        }

        if (dropDownButton is not null)
        {
            dropDownButton.Click -= OnDropDownButtonClick;
        }

        base.OnApplyTemplate();

        primaryButton = GetTemplateChild(PrimaryButtonPartName) as ButtonBase;
        dropDownButton = GetTemplateChild(DropDownButtonPartName) as ButtonBase;

        if (primaryButton is not null)
        {
            primaryButton.Click += OnPrimaryButtonClick;
        }

        if (dropDownButton is not null)
        {
            dropDownButton.Click += OnDropDownButtonClick;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool altDown = e.Key == Key.System && e.SystemKey == Key.Down;
        if (!e.Handled && (altDown || e.Key == Key.F4) && DropDownMenu is not null)
        {
            SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
            e.Handled = true;
        }
    }

    private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
    {
        // The part's Click would otherwise bubble on as a ButtonBase.Click from inside this control.
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
    }

    private void OnDropDownButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    private static void OnDropDownMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var splitButton = (SplitButton)d;
        if (e.OldValue is ContextMenu oldMenu)
        {
            oldMenu.Opened -= splitButton.OnMenuOpened;
            oldMenu.Closed -= splitButton.OnMenuClosed;
            oldMenu.IsOpen = false;
            if (BindingOperations.GetBinding(oldMenu, DataContextProperty)?.Source == splitButton)
            {
                BindingOperations.ClearBinding(oldMenu, DataContextProperty);
            }
        }

        if (e.NewValue is ContextMenu newMenu)
        {
            newMenu.Opened += splitButton.OnMenuOpened;
            newMenu.Closed += splitButton.OnMenuClosed;

            // The menu is not in the visual tree, so it shares the button's data context explicitly
            // unless it has its own.
            if (newMenu.ReadLocalValue(DataContextProperty) == DependencyProperty.UnsetValue)
            {
                newMenu.SetBinding(DataContextProperty, new Binding(nameof(DataContext)) { Source = splitButton });
            }
        }

        splitButton.CoerceValue(IsDropDownOpenProperty);
    }

    private static object CoerceIsDropDownOpen(DependencyObject d, object value) =>
        ((SplitButton)d).DropDownMenu is null ? false : value;

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var splitButton = (SplitButton)d;
        if (splitButton.DropDownMenu is not { } menu)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            menu.PlacementTarget = splitButton;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
        else
        {
            menu.IsOpen = false;
        }
    }

    private void OnMenuOpened(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, true);

    private void OnMenuClosed(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, false);
}
