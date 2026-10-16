using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Controls;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class ContentOverlayTests
{
    [TestMethod]
    public async Task OpeningShowsTheCardAndFocusesItsContentAndClosingReturnsFocus()
    {
        await UiThread.RunAsync(async () =>
        {
            var opener = new Button { Content = "Trail settings" };
            var field = new TextBox { Text = "Granite Ridge" };
            var overlay = new ContentOverlay { Header = "Trail settings", Content = field };
            // As applications compose it, the content behind the overlay is disabled while it is open.
            var covered = new StackPanel { Children = { opener } };
            covered.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(ContentOverlay.IsOpen))
            {
                Source = overlay,
                Converter = new WpfFoundation.Converters.InverseBooleanConverter()
            });
            var window = UiThread.ShowWindow(Layout(covered, overlay));
            try
            {
                window.Activate();
                opener.Focus();
                await UiThread.IdleAsync();
                Assert.AreEqual(Visibility.Collapsed, overlay.Visibility, "A closed overlay takes no space and no input.");

                overlay.IsOpen = true;
                await UiThread.IdleAsync();
                Assert.IsFalse(opener.IsEnabled);
                var card = Part(overlay, "PART_Card");
                Assert.IsTrue(card.IsVisible);
                Assert.IsTrue(Part(overlay, "PART_Smoke").IsVisible, "The smoke layer covers the content around the card.");
                Assert.AreEqual("Trail settings", AutomationProperties.GetName(card));
                Assert.IsTrue(AutomationProperties.GetIsDialog(card));
                Assert.IsTrue(Visuals.Descendants<TextBlock>(overlay).Any(text => text.Text == "Trail settings"), "The header shows as the card's title.");
                if (window.IsActive)
                {
                    Assert.IsTrue(field.IsKeyboardFocused, "Opening focuses the content before the close button.");
                }

                overlay.IsOpen = false;
                await UiThread.IdleAsync();
                Assert.AreEqual(Visibility.Collapsed, overlay.Visibility);
                if (window.IsActive)
                {
                    Assert.IsTrue(opener.IsKeyboardFocused, "Closing returns focus to the element that had it.");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task WithoutACommandTheCloseButtonTheSmokeAndEscapeEachClose()
    {
        await UiThread.RunAsync(async () =>
        {
            var model = new OverlayModel { IsOpen = true };
            var field = new TextBox();
            var overlay = new ContentOverlay { Header = "Trail settings", Content = field };
            overlay.SetBinding(ContentOverlay.IsOpenProperty, new Binding(nameof(OverlayModel.IsOpen)) { Source = model });
            var window = UiThread.ShowWindow(Layout(new Button { Content = "Behind" }, overlay));
            try
            {
                await UiThread.IdleAsync();
                var close = CloseButton(overlay);
                Assert.IsTrue(close.IsEnabled);
                Assert.AreEqual("Close", close.ToolTip);
                Click(close);
                await UiThread.IdleAsync();
                Assert.IsFalse(model.IsOpen, "Closing writes back to a bound IsOpen.");

                model.IsOpen = true;
                await UiThread.IdleAsync();
                Press(Part(overlay, "PART_Card"));
                await UiThread.IdleAsync();
                Assert.IsTrue(model.IsOpen, "A press inside the card does not close it.");
                Press(Part(overlay, "PART_Smoke"));
                await UiThread.IdleAsync();
                Assert.IsFalse(model.IsOpen, "A press on the smoke outside the card closes it.");

                model.IsOpen = true;
                await UiThread.IdleAsync();
                Assert.IsTrue(Escape(field));
                Assert.IsFalse(model.IsOpen, "Esc closes it.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ACloseCommandDecidesAndKeepsTheOverlayOpenWhileItCannotExecute()
    {
        await UiThread.RunAsync(async () =>
        {
            bool canClose = false;
            int closes = 0;
            var field = new TextBox();
            var overlay = new ContentOverlay { Header = "Trail settings", Content = field, IsOpen = true };
            var command = new RelayCommand(() =>
            {
                closes++;
                overlay.IsOpen = false;
            }, () => canClose);
            overlay.CloseCommand = command;
            var window = UiThread.ShowWindow(Layout(new Button { Content = "Behind" }, overlay));
            try
            {
                await UiThread.IdleAsync();
                var close = CloseButton(overlay);
                Assert.IsFalse(close.IsEnabled, "The close button is disabled while closing is refused.");
                Press(Part(overlay, "PART_Smoke"));
                Assert.IsTrue(Escape(field), "The open overlay takes Esc even while it refuses to close, so nothing behind it acts on it.");
                await UiThread.IdleAsync();
                Assert.AreEqual(0, closes);
                Assert.IsTrue(overlay.IsOpen);

                canClose = true;
                command.NotifyCanExecuteChanged();
                await UiThread.IdleAsync();
                Assert.IsTrue(close.IsEnabled, "The close button follows the command.");
                Click(close);
                await UiThread.IdleAsync();
                Assert.AreEqual(1, closes);
                Assert.IsFalse(overlay.IsOpen);

                overlay.IsOpen = true;
                await UiThread.IdleAsync();
                Press(Part(overlay, "PART_Smoke"));
                Assert.AreEqual(2, closes, "A press on the smoke runs the command.");
                overlay.IsOpen = true;
                await UiThread.IdleAsync();
                Assert.IsTrue(Escape(field));
                Assert.AreEqual(3, closes, "Esc runs the command.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ClosingCommitsTheFocusedTextBoxBeforeTheCommandRuns()
    {
        await UiThread.RunAsync(async () =>
        {
            var model = new OverlayModel { Note = "Granite Ridge" };
            var field = new TextBox();
            field.SetBinding(TextBox.TextProperty, new Binding(nameof(OverlayModel.Note)) { Source = model });
            string? closedWith = null;
            var overlay = new ContentOverlay { Header = "Trail settings", Content = field, IsOpen = true };
            overlay.CloseCommand = new RelayCommand(() =>
            {
                closedWith = model.Note;
                overlay.IsOpen = false;
            });
            var window = UiThread.ShowWindow(Layout(new Button { Content = "Behind" }, overlay));
            try
            {
                window.Activate();
                await UiThread.IdleAsync();
                field.Focus();
                await UiThread.IdleAsync();
                if (!window.IsActive || !field.IsKeyboardFocused)
                {
                    return;
                }

                field.Text = "Granite Ridge north";
                Assert.AreEqual("Granite Ridge", model.Note, "The binding waits for focus to leave.");
                Assert.IsTrue(Escape(field));
                Assert.AreEqual("Granite Ridge north", closedWith, "The close command sees the edit.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task EscapeHandledInsideTheCardDoesNotClose()
    {
        await UiThread.RunAsync(async () =>
        {
            var field = new TextBox();
            field.KeyDown += (_, e) => e.Handled = e.Key == Key.Escape;
            var overlay = new ContentOverlay { Header = "Trail settings", Content = field, IsOpen = true };
            var window = UiThread.ShowWindow(Layout(new Button { Content = "Behind" }, overlay));
            try
            {
                await UiThread.IdleAsync();
                Escape(field);
                await UiThread.IdleAsync();
                Assert.IsTrue(overlay.IsOpen, "An inner control that handles Esc, such as an open drop-down, keeps the overlay open.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task TabStaysInsideTheCard()
    {
        await UiThread.RunAsync(async () =>
        {
            var behind = new Button { Content = "Behind" };
            var field = new TextBox();
            var overlay = new ContentOverlay { Header = "Trail settings", Content = field, IsOpen = true };
            var window = UiThread.ShowWindow(Layout(behind, overlay));
            try
            {
                window.Activate();
                await UiThread.IdleAsync();
                field.Focus();
                await UiThread.IdleAsync();
                if (!window.IsActive)
                {
                    return;
                }

                var close = CloseButton(overlay);
                field.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Assert.IsTrue(close.IsKeyboardFocused, "Tab moves from the content to the close button.");
                close.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Assert.IsTrue(field.IsKeyboardFocused, "Tab cycles back to the content instead of leaving the card.");
                Assert.IsFalse(behind.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task AnUnloadedOverlayStopsFollowingItsCloseCommand()
    {
        await UiThread.RunAsync(async () =>
        {
            var first = new CountingCommand();
            var second = new CountingCommand();
            var overlay = new ContentOverlay { Header = "Trail settings", CloseCommand = first };
            var container = new Border { Child = overlay };
            var window = UiThread.ShowWindow(container);
            try
            {
                await UiThread.IdleAsync();
                Assert.AreEqual(1, first.Subscribers);

                container.Child = null;
                await UiThread.IdleAsync();
                Assert.AreEqual(0, first.Subscribers, "A long-lived command must not keep an unloaded overlay alive.");

                container.Child = overlay;
                await UiThread.IdleAsync();
                Assert.AreEqual(1, first.Subscribers);

                overlay.CloseCommand = second;
                Assert.AreEqual(0, first.Subscribers);
                Assert.AreEqual(1, second.Subscribers);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Grid Layout(UIElement content, ContentOverlay overlay) => new() { Children = { content, overlay } };

    private static FrameworkElement Part(ContentOverlay overlay, string name) =>
        (FrameworkElement)overlay.Template.FindName(name, overlay);

    private static Button CloseButton(ContentOverlay overlay) =>
        Visuals.Descendants<Button>(overlay).Single(button => AutomationProperties.GetName(button) == "Close");

    private static void Click(Button button) =>
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();

    private static void Press(UIElement element) =>
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });

    // Raises Esc on an element inside the card and returns whether a handler took it.
    private static bool Escape(UIElement target)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private sealed class OverlayModel : ObservableObject
    {
        private bool isOpen;
        private string note = string.Empty;

        public bool IsOpen
        {
            get => isOpen;
            set => SetProperty(ref isOpen, value);
        }

        public string Note
        {
            get => note;
            set => SetProperty(ref note, value);
        }
    }

    private sealed class CountingCommand : ICommand
    {
        private EventHandler? handlers;

        public int Subscribers => handlers?.GetInvocationList().Length ?? 0;

        public event EventHandler? CanExecuteChanged
        {
            add => handlers += value;
            remove => handlers -= value;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
