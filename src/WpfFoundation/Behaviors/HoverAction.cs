using System.Windows;

namespace WpfFoundation.Behaviors;

/// <summary>
/// Whether an item hover button (<c>WfItemHoverButtonStyle</c>) applies to its item. The style shows the button while
/// the pointer is over the item only when this is true, so an application binds it to the item's own condition.
/// </summary>
public static class HoverAction
{
    /// <summary>Whether the action applies to the item. True by default.</summary>
    public static readonly DependencyProperty IsAvailableProperty = DependencyProperty.RegisterAttached(
        "IsAvailable", typeof(bool), typeof(HoverAction), new PropertyMetadata(true));

    public static bool GetIsAvailable(DependencyObject element) => (bool)element.GetValue(IsAvailableProperty);
    public static void SetIsAvailable(DependencyObject element, bool value) => element.SetValue(IsAvailableProperty, value);
}
