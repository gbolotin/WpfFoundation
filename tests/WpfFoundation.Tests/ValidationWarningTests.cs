using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class ValidationWarningTests
{
    [TestMethod]
    public async Task WarningTracksAllErrorsWithoutReplacingTheControlTemplate()
    {
        await UiThread.RunAsync(async () =>
        {
            var input = new TextBox { Width = 128, Margin = new Thickness(0, 0, 24, 0) };
            Validation.SetErrorTemplate(input, (ControlTemplate)Application.Current.FindResource("WfValidationWarningTemplate"));
            var number = new NumberSource();
            input.SetBinding(TextBox.TextProperty, new Binding(nameof(NumberSource.Number))
            {
                Source = number,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true
            });
            input.SetBinding(FrameworkElement.TagProperty, new Binding(nameof(NumberSource.Number)) { Source = number });
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24) };
            panel.Children.Add(input);
            var neighbour = new Button { Content = "Reset" };
            panel.Children.Add(neighbour);
            var scroll = new ScrollViewer { Content = panel };
            var window = UiThread.ShowWindow(scroll);
            try
            {
                foreach (ThemeMode theme in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
                {
                    window.ThemeMode = theme;
                    input.Text = "bad number";
                    await UiThread.IdleAsync();
                    var layer = AdornerLayer.GetAdornerLayer(input)!;
                    Assert.HasCount(1, layer.GetAdorners(input)!);
                    var marker = Visuals.Descendants<TextBlock>(layer).Single(text => text.Text == "\uE7BA");
                    Assert.IsFalse(string.IsNullOrEmpty(AutomationProperties.GetName(marker)));
                    Assert.IsNotNull(marker.Foreground);
                    Assert.AreEqual(AutomationProperties.GetName(marker), marker.ToolTip);
                    var inputBounds = input.TransformToAncestor(window).TransformBounds(new Rect(input.RenderSize));
                    var markerBounds = marker.TransformToAncestor(window).TransformBounds(new Rect(marker.RenderSize));
                    var neighbourBounds = neighbour.TransformToAncestor(window).TransformBounds(new Rect(neighbour.RenderSize));
                    Assert.IsTrue(markerBounds.Left >= inputBounds.Right);
                    Assert.IsTrue(markerBounds.Right <= neighbourBounds.Left);
                    Assert.AreEqual(inputBounds.Top + inputBounds.Height / 2, markerBounds.Top + markerBounds.Height / 2, 1);

                    var expression = input.GetBindingExpression(FrameworkElement.TagProperty)!;
                    Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Second error", null));
                    await UiThread.IdleAsync();
                    Assert.HasCount(1, layer.GetAdorners(input)!);
                    Assert.HasCount(1, Visuals.Descendants<TextBlock>(layer).Where(text => text.Text == "\uE7BA").ToArray());
                    StringAssert.Contains(AutomationProperties.GetName(marker), "Second error");
                    Validation.ClearInvalid(expression);
                    await UiThread.IdleAsync();
                    Assert.IsFalse(AutomationProperties.GetName(marker).Contains("Second error", StringComparison.Ordinal));

                    window.Width += 32;
                    scroll.ScrollToRightEnd();
                    await UiThread.IdleAsync();
                    Assert.HasCount(1, layer.GetAdorners(input)!);
                    window.Content = null;
                    await UiThread.IdleAsync();
                    window.Content = scroll;
                    await UiThread.IdleAsync();
                    // Native conversion bindings can refresh their target on reattachment.
                    // A new invalid edit must create exactly one warning in the new host.
                    input.Text = "bad number after reload";
                    await UiThread.IdleAsync();
                    layer = AdornerLayer.GetAdornerLayer(input)!;
                    Assert.HasCount(1, layer.GetAdorners(input)!);
                    input.Text = "42";
                    await UiThread.IdleAsync();
                    Assert.IsFalse(Validation.GetHasError(input));
                    Assert.IsTrue(layer.GetAdorners(input) is null or { Length: 0 });
                    Assert.AreEqual(42, number.Number);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task WarningCanAdornAFrameworkElementWithoutAnInputTemplate()
    {
        await UiThread.RunAsync(async () =>
        {
            var element = new Border { Width = 160, Height = 32, Margin = new Thickness(24) };
            Validation.SetErrorTemplate(element, (ControlTemplate)Application.Current.FindResource("WfValidationWarningTemplate"));
            element.SetBinding(FrameworkElement.TagProperty, new Binding(nameof(NumberSource.Number)) { Source = new NumberSource() });
            var window = UiThread.ShowWindow(element);
            try
            {
                var expression = element.GetBindingExpression(FrameworkElement.TagProperty)!;
                Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Example field error", null));
                await UiThread.IdleAsync();
                var layer = AdornerLayer.GetAdornerLayer(element)!;
                Assert.HasCount(1, layer.GetAdorners(element)!);
                var marker = Visuals.Descendants<TextBlock>(layer).Single(text => text.Text == "\uE7BA");
                Assert.AreEqual("Example field error", AutomationProperties.GetName(marker));
                Validation.ClearInvalid(expression);
                await UiThread.IdleAsync();
                Assert.IsTrue(layer.GetAdorners(element) is null or { Length: 0 });
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class NumberSource
    {
        public int Number { get; set; } = 50;
    }
}
