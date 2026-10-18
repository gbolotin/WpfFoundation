using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class ValidationOutlineTests
{
    [TestMethod]
    public async Task OutlineUsesNativeFluentTemplateAndRestoresHelpAfterCorrection()
    {
        await UiThread.RunAsync(async () =>
        {
            var style = new Style(typeof(TextBox), (Style)Application.Current.FindResource("WfValidationOutlineTextBoxStyle"));
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Field help"));
            var input = new TextBox { Width = 128, Style = style };
            var number = new NumberSource();
            input.SetBinding(TextBox.TextProperty, new Binding(nameof(NumberSource.Number))
            {
                Source = number,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true
            });
            input.SetBinding(FrameworkElement.TagProperty, new Binding(nameof(NumberSource.Number)) { Source = number });
            var window = UiThread.ShowWindow(input);
            try
            {
                foreach (ThemeMode theme in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
                {
                    window.ThemeMode = theme;
                    await UiThread.IdleAsync();
                    var template = input.Template;
                    Assert.AreEqual(default(Thickness), input.Margin, "The outline needs no marker space.");
                    input.Focus();
                    input.Text = "invalid number";
                    await UiThread.IdleAsync();
                    Assert.IsTrue(Validation.GetHasError(input));
                    Assert.AreSame(input.FindResource("DefaultTextBoxInvalidationStyle"), Validation.GetErrorTemplate(input));
                    Assert.AreSame(template, input.Template, "The control template and clear button stay native.");
                    var layer = AdornerLayer.GetAdornerLayer(input)!;
                    var adorner = layer.GetAdorners(input)!.Single();
                    var border = Visuals.Descendants<Border>(adorner).Single();
                    Assert.AreEqual(input.FindResource("SystemFillColorCriticalBrush"), border.BorderBrush);
                    Assert.AreEqual(input.FindResource("ControlCornerRadius"), border.CornerRadius);
                    Assert.IsFalse(Visuals.Descendants<TextBlock>(adorner).Any(text => text.Text == "\uE7BA"));
                    Assert.IsFalse(string.IsNullOrEmpty(UIElementAutomationPeer.CreatePeerForElement(input).GetItemStatus()));
                    Assert.AreEqual(UIElementAutomationPeer.CreatePeerForElement(input).GetItemStatus(), input.ToolTip);

                    var expression = input.GetBindingExpression(FrameworkElement.TagProperty)!;
                    Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Another error", null));
                    await UiThread.IdleAsync();
                    StringAssert.Contains((string)input.ToolTip, "Another error");
                    Validation.ClearInvalid(expression);
                    input.Text = "42";
                    await UiThread.IdleAsync();
                    Assert.IsFalse(Validation.GetHasError(input));
                    Assert.AreEqual(42, number.Number);
                    Assert.AreEqual("Field help", input.ToolTip);
                    Assert.AreEqual(string.Empty, UIElementAutomationPeer.CreatePeerForElement(input).GetItemStatus());
                    Assert.IsTrue(layer.GetAdorners(input) is null or { Length: 0 });
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ComboBoxUsesTheOutlineAndReportsItsError()
    {
        await UiThread.RunAsync(async () =>
        {
            var input = new ComboBox { Style = (Style)Application.Current.FindResource("WfValidationOutlineComboBoxStyle") };
            input.SetBinding(FrameworkElement.TagProperty, new Binding(nameof(NumberSource.Number)) { Source = new NumberSource() });
            var window = UiThread.ShowWindow(input);
            try
            {
                var expression = input.GetBindingExpression(FrameworkElement.TagProperty)!;
                Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Choose an available value.", null));
                await UiThread.IdleAsync();
                Assert.AreSame(input.FindResource("DefaultTextBoxInvalidationStyle"), Validation.GetErrorTemplate(input));
                Assert.AreEqual("Choose an available value.", input.ToolTip);
                Assert.AreEqual("Choose an available value.", UIElementAutomationPeer.CreatePeerForElement(input).GetItemStatus());
                Validation.ClearInvalid(expression);
                await UiThread.IdleAsync();
                Assert.IsFalse(Validation.GetHasError(input));
                Assert.IsNull(input.ToolTip);
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
