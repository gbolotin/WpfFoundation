using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Controls;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class SplitButtonTests
{
    [TestMethod]
    public async Task ThePrimaryPartRunsTheCommandWithoutOpeningTheMenu()
    {
        await UiThread.RunAsync(async () =>
        {
            object? ranWith = null;
            int clicks = 0;
            var splitButton = new SplitButton
            {
                Content = "Start",
                Icon = "",
                Command = new RelayCommand<object?>(parameter => ranWith = parameter),
                CommandParameter = "Fast",
                DropDownMenu = Menu("Fast", "Thorough")
            };
            splitButton.Click += (_, _) => clicks++;
            var window = UiThread.ShowWindow(splitButton);
            try
            {
                await UiThread.IdleAsync();
                Invoke(Part(splitButton, "PART_PrimaryButton"));
                await UiThread.IdleAsync();

                Assert.AreEqual("Fast", ranWith);
                Assert.AreEqual(1, clicks);
                Assert.IsFalse(splitButton.IsDropDownOpen);
                Assert.AreEqual("Start", UIElementAutomationPeer.CreatePeerForElement(Part(splitButton, "PART_PrimaryButton")).GetName());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task OnlyThePrimaryPartIsDisabledWhileTheCommandCannotRun()
    {
        await UiThread.RunAsync(async () =>
        {
            var splitButton = new SplitButton
            {
                Content = "Start",
                Command = new RelayCommand(() => { }, () => false),
                DropDownMenu = Menu("Fast")
            };
            var window = UiThread.ShowWindow(splitButton);
            try
            {
                await UiThread.IdleAsync();
                Assert.IsFalse(Part(splitButton, "PART_PrimaryButton").IsEnabled);
                Assert.IsTrue(Part(splitButton, "PART_DropDownButton").IsEnabled, "Another item can still be picked from the menu.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task TheChevronOpensTheMenuUnderTheButtonAndClosingItResetsTheState()
    {
        await UiThread.RunAsync(async () =>
        {
            var menu = Menu("Fast", "Thorough");
            var splitButton = new SplitButton { Content = "Start", DataContext = "Profiles", DropDownMenu = menu };
            var window = UiThread.ShowWindow(splitButton);
            try
            {
                await UiThread.IdleAsync();
                Assert.AreEqual("Profiles", menu.DataContext, "The menu shares the button's data context.");

                Invoke(Part(splitButton, "PART_DropDownButton"));
                await UiThread.IdleAsync();
                Assert.IsTrue(splitButton.IsDropDownOpen);
                Assert.IsTrue(menu.IsOpen);
                Assert.AreSame(splitButton, menu.PlacementTarget);

                menu.IsOpen = false;
                await UiThread.IdleAsync();
                Assert.IsFalse(splitButton.IsDropDownOpen, "Picking an item or clicking away closes the menu.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task F4OnThePrimaryPartTogglesTheMenu()
    {
        await UiThread.RunAsync(async () =>
        {
            var menu = Menu("Fast");
            var splitButton = new SplitButton { Content = "Start", DropDownMenu = menu };
            var window = UiThread.ShowWindow(splitButton);
            try
            {
                await UiThread.IdleAsync();
                var primary = Part(splitButton, "PART_PrimaryButton");
                Assert.IsTrue(primary.Focusable && primary.IsTabStop, "The primary part is the tab stop.");
                Assert.IsFalse(Part(splitButton, "PART_DropDownButton").IsTabStop, "The chevron is not a second tab stop.");

                var keyDown = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(primary), 0, Key.F4)
                {
                    RoutedEvent = Keyboard.KeyDownEvent
                };
                primary.RaiseEvent(keyDown);
                await UiThread.IdleAsync();
                Assert.IsTrue(keyDown.Handled);
                Assert.IsTrue(menu.IsOpen);
            }
            finally
            {
                menu.IsOpen = false;
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task WithoutAMenuItIsAPlainRoundedButton()
    {
        await UiThread.RunAsync(async () =>
        {
            var splitButton = new SplitButton { Content = "Start" };
            var window = UiThread.ShowWindow(splitButton);
            try
            {
                await UiThread.IdleAsync();
                var radius = (CornerRadius)Application.Current.FindResource("ControlCornerRadius");
                Assert.AreEqual(Visibility.Collapsed, Part(splitButton, "PART_DropDownButton").Visibility);
                Assert.AreEqual(radius, Part(splitButton, "PART_PrimaryButton").GetValue(Border.CornerRadiusProperty));

                splitButton.IsDropDownOpen = true;
                Assert.IsFalse(splitButton.IsDropDownOpen, "There is nothing to open.");

                splitButton.DropDownMenu = Menu("Fast");
                await UiThread.IdleAsync();
                Assert.AreEqual(Visibility.Visible, Part(splitButton, "PART_DropDownButton").Visibility);
                Assert.AreEqual(
                    new CornerRadius(radius.TopLeft, 0, 0, radius.BottomLeft),
                    Part(splitButton, "PART_PrimaryButton").GetValue(Border.CornerRadiusProperty),
                    "The two parts join in the middle.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ContextMenu Menu(params string[] headers)
    {
        var menu = new ContextMenu();
        foreach (string header in headers)
        {
            menu.Items.Add(new MenuItem { Header = header, IsCheckable = true });
        }

        return menu;
    }

    private static Button Part(SplitButton splitButton, string name) =>
        (Button)splitButton.Template.FindName(name, splitButton);

    private static void Invoke(Button button) =>
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();
}
