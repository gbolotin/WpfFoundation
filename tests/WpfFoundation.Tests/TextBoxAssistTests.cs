using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using WpfFoundation.Behaviors;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class TextBoxAssistTests
{
    [TestMethod]
    public async Task PlaceholderTracksEmptyTextAndSuffixStaysOutOfTheValue()
    {
        await UiThread.RunAsync(async () =>
        {
            var input = new TextBox { Width = 208 };
            TextBoxAssist.SetPlaceholder(input, "1000");
            TextBoxAssist.SetSuffix(input, "nits");
            var window = UiThread.ShowWindow(input);
            try
            {
                await UiThread.IdleAsync();
                var template = input.Template;
                var layer = AdornerLayer.GetAdornerLayer(input)!;
                Assert.HasCount(1, layer.GetAdorners(input)!);
                var adorner = layer.GetAdorners(input)!.Single();
                Assert.AreEqual("1000nits", DrawnText(adorner));
                Assert.AreEqual(string.Empty, input.Text);
                input.Text = "67";
                await UiThread.IdleAsync();
                Assert.AreEqual("nits", DrawnText(adorner));
                input.SelectAll();
                Assert.AreEqual("67", input.SelectedText);
                Assert.AreSame(template, input.Template);
                TextBoxAssist.SetSuffix(input, "%");
                await UiThread.IdleAsync();
                Assert.AreEqual("%", DrawnText(adorner));
                input.Clear();
                await UiThread.IdleAsync();
                Assert.AreEqual("1000%", DrawnText(adorner));
                TextBoxAssist.SetPlaceholder(input, null);
                TextBoxAssist.SetSuffix(input, null);
                await UiThread.IdleAsync();
                Assert.IsTrue(layer.GetAdorners(input) is null or { Length: 0 });
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReloadAndPropertyChangesKeepOneAdornerAndPreservePaddingBinding(bool twoWay)
    {
        await UiThread.RunAsync(async () =>
        {
            var input = new TextBox { Width = 208, Text = "1234560" };
            var padding = new Border { Padding = new Thickness(12, 8, 12, 8) };
            input.SetBinding(Control.PaddingProperty, new Binding(nameof(Border.Padding)) { Source = padding, Mode = twoWay ? BindingMode.TwoWay : BindingMode.OneWay });
            TextBoxAssist.SetSuffix(input, "cm");
            var host = new ContentControl { Content = input };
            var window = UiThread.ShowWindow(host);
            try
            {
                await UiThread.IdleAsync();
                Assert.IsTrue(BindingOperations.IsDataBound(input, Control.PaddingProperty));
                Assert.AreEqual(new Thickness(12, 8, 12, 8), padding.Padding, "The decoration must not change the binding source.");
                Assert.IsGreaterThan(12d, ((ScrollViewer)input.Template.FindName("PART_ContentHost", input)).Padding.Right);
                for (int i = 0; i < 3; i++)
                {
                    host.Content = null;
                    await UiThread.IdleAsync();
                    Assert.AreEqual(padding.Padding, input.Padding);
                    host.Content = input;
                    await UiThread.IdleAsync();
                    Assert.HasCount(1, AdornerLayer.GetAdornerLayer(input)!.GetAdorners(input)!);
                }
                padding.Padding = new Thickness(16, 8, 16, 8);
                await UiThread.IdleAsync();
                Assert.AreEqual(16d, input.Padding.Left);
                Assert.IsGreaterThan(16d, ((ScrollViewer)input.Template.FindName("PART_ContentHost", input)).Padding.Right);
                TextBoxAssist.SetSuffix(input, null);
                await UiThread.IdleAsync();
                Assert.AreEqual(padding.Padding, input.Padding);
                Assert.IsTrue(BindingOperations.IsDataBound(input, Control.PaddingProperty));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task DecorationsHandleScrollFontChangesRtlAndHiddenViews()
    {
        await UiThread.RunAsync(async () =>
        {
            var input = new TextBox { Width = 208, Text = "123" };
            TextBoxAssist.SetSuffix(input, "cm");
            var window = UiThread.ShowWindow(input);
            try
            {
                foreach (ThemeMode theme in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
                {
                    window.ThemeMode = theme;
                    await UiThread.IdleAsync();
                    var adorner = AdornerLayer.GetAdornerLayer(input)!.GetAdorners(input)!.Single();
                    Assert.AreEqual("cm", DrawnText(adorner));
                    Assert.IsFalse(adorner.IsHitTestVisible || adorner.Focusable);
                    input.FontSize += 4;
                    input.Width += 16;
                    await UiThread.IdleAsync();
                    Assert.AreEqual("cm", DrawnText(adorner));
                    input.Text = new string('8', 100);
                    input.ScrollToEnd();
                    await UiThread.IdleAsync();
                    Assert.AreEqual("cm", DrawnText(adorner));
                    input.FlowDirection = FlowDirection.RightToLeft;
                    await UiThread.IdleAsync();
                    Assert.AreEqual("cm", DrawnText(adorner));
                    input.Visibility = Visibility.Collapsed;
                    await UiThread.IdleAsync();
                    Assert.IsFalse(adorner.IsVisible);
                    input.Visibility = Visibility.Visible;
                    input.FlowDirection = FlowDirection.LeftToRight;
                    input.Text = "123";
                    await UiThread.IdleAsync();
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task MissingAdornerLayerDoesNotChangePaddingOrThrow()
    {
        await UiThread.RunAsync(() =>
        {
            var input = new TextBox { Padding = new Thickness(8) };
            TextBoxAssist.SetPlaceholder(input, "Example");
            TextBoxAssist.SetSuffix(input, "cm");
            input.Measure(new Size(200, 32));
            input.Arrange(new Rect(0, 0, 200, 32));
            Assert.AreEqual(new Thickness(8), input.Padding);
            TextBoxAssist.SetPlaceholder(input, null);
            TextBoxAssist.SetSuffix(input, null);
        });
    }

    [TestMethod]
    public async Task SearchTextBoxStyleShowsSearchIconAndClearButtonWhileTextIsNonEmpty()
    {
        await UiThread.RunAsync(async () =>
        {
            var input = new TextBox
            {
                Width = 240,
                Style = (Style)Application.Current.FindResource("WfSearchTextBoxStyle")
            };
            TextBoxAssist.SetPlaceholder(input, "Filter files");
            var otherButton = new Button { Content = "Other" };
            var panel = new StackPanel { Children = { input, otherButton } };
            var window = UiThread.ShowWindow(panel);
            try
            {
                await UiThread.IdleAsync();
                Assert.AreEqual("\uE721", TextBoxAssist.GetIcon(input));
                var deleteButton = (Button)input.Template.FindName("DeleteButton", input);
                var contentBorder = (Border)input.Template.FindName("ContentBorder", input);
                var rootGrid = (Grid)contentBorder.Parent;
                Assert.HasCount(3, rootGrid.ColumnDefinitions);
                Assert.AreEqual(Visibility.Collapsed, deleteButton.Visibility);
                var iconGlyph = Visuals.Descendants<TextBlock>(rootGrid).Single(tb => tb.Text == "\uE721");
                Assert.IsTrue(iconGlyph.IsVisible);

                input.Text = "hdr10";
                otherButton.Focus();
                await UiThread.IdleAsync();
                Assert.IsFalse(input.IsKeyboardFocused);
                Assert.AreEqual(Visibility.Visible, deleteButton.Visibility);
                Assert.IsLessThan(
                    iconGlyph.TransformToAncestor(input).Transform(new Point(0, 0)).X,
                    deleteButton.TransformToAncestor(input).Transform(new Point(0, 0)).X,
                    "The clear button must appear to the left of the search icon.");

                Assert.IsTrue(deleteButton.IsEnabled);
                Assert.IsTrue(((System.Windows.Input.RoutedCommand)deleteButton.Command).CanExecute(deleteButton.CommandParameter, deleteButton.CommandTarget));
                ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(deleteButton)
                    .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
                await UiThread.IdleAsync();
                Assert.AreEqual(string.Empty, input.Text);
                Assert.AreEqual(Visibility.Collapsed, deleteButton.Visibility);

                TextBoxAssist.SetIcon(input, null);
                await UiThread.IdleAsync();
                Assert.HasCount(2, rootGrid.ColumnDefinitions);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static string DrawnText(Visual visual) => string.Concat(Glyphs(VisualTreeHelper.GetDrawing(visual)).SelectMany(glyph => glyph.GlyphRun.Characters));

    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing? drawing)
    {
        if (drawing is GlyphRunDrawing glyph)
        {
            yield return glyph;
        }
        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children.SelectMany(Glyphs))
            {
                yield return child;
            }
        }
    }
}
