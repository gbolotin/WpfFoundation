using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using WpfFoundation.Dialogs;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class DialogServiceTests
{
    [TestMethod]
    public async Task AMessageShowsInTheSharedWindowOverTheActiveWindowAndRestoresFocus()
    {
        await UiThread.RunAsync(async () =>
        {
            var field = new TextBox();
            var owner = UiThread.ShowWindow(field);
            try
            {
                owner.Activate();
                field.Focus();
                await UiThread.IdleAsync();
                var service = new DialogService(Application.Current);
                DialogWindow? shown = null;

                bool accepted = await ShowAndInteractAsync(service, new MessageDialogViewModel("Sync finished", "12 field notes were copied."), window =>
                {
                    shown = window;
                    Assert.AreEqual("Sync finished", window.Title);
                    Assert.AreSame(owner, window.Owner);
                    Assert.IsFalse(window.ShowInTaskbar);
                    Assert.AreEqual(ResizeMode.NoResize, window.ResizeMode);
                    Assert.AreEqual(SizeToContent.WidthAndHeight, window.SizeToContent);
                    Assert.IsTrue(Visuals.Descendants<TextBlock>(window).Any(text => text.Text == "12 field notes were copied."), "The built-in template shows the message.");
                    var close = FooterButton(window, "Close");
                    Assert.IsTrue(close.IsDefault);
                    Assert.AreEqual(Application.Current.FindResource("WfPrimaryButtonStyle"), close.Style);
                    Click(close);
                });

                Assert.IsTrue(accepted);
                Assert.IsNotNull(shown);
                Assert.IsFalse(shown.IsVisible);
                if (owner.IsActive)
                {
                    Assert.IsTrue(field.IsKeyboardFocused, "Focus returns to the element that had it.");
                }
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [TestMethod]
    public async Task ADestructiveConfirmationDefaultsToTheSafeChoice()
    {
        await UiThread.RunAsync(async () =>
        {
            var service = new DialogService(Application.Current);

            bool deleted = await ShowAndInteractAsync(service, new ConfirmationDialogViewModel("Delete note?", "The note cannot be recovered.", "Delete", "Keep", isDestructive: true), window =>
            {
                var delete = FooterButton(window, "Delete");
                var keep = FooterButton(window, "Keep");
                Assert.IsFalse(delete.IsDefault);
                Assert.IsTrue(keep.IsDefault, "Enter keeps the note.");
                Assert.IsTrue(keep.IsCancel);
                Assert.AreNotEqual(Application.Current.FindResource("WfPrimaryButtonStyle"), delete.Style, "A destructive action is not styled as the primary choice.");
                Click(keep);
            });
            Assert.IsFalse(deleted);

            deleted = await ShowAndInteractAsync(service, new ConfirmationDialogViewModel("Delete note?", "The note cannot be recovered.", "Delete", "Keep", isDestructive: true), window => Click(FooterButton(window, "Delete")));
            Assert.IsTrue(deleted);
        });
    }

    [TestMethod]
    public async Task EscapeAndClosingTheWindowCancel()
    {
        await UiThread.RunAsync(async () =>
        {
            var service = new DialogService(Application.Current);

            bool escaped = await ShowAndInteractAsync(service, new MessageDialogViewModel("Notice", "Escape closes this."), window =>
                window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent }));
            Assert.IsFalse(escaped);

            var confirmation = new ConfirmationDialogViewModel("Archive note?", "Archived notes stay searchable.", "Archive", "Cancel");
            bool closed = await ShowAndInteractAsync(service, confirmation, window => window.Close());
            Assert.IsFalse(closed);
            Assert.IsTrue(confirmation.IsCompleted);
        });
    }

    [TestMethod]
    public async Task AReviewIsResizableAndReturnsTheApproval()
    {
        await UiThread.RunAsync(async () =>
        {
            var service = new DialogService(Application.Current);
            var review = new ReviewDialogViewModel("Review import", string.Join(Environment.NewLine, Enumerable.Range(1, 200).Select(i => $"Note {i}")), "Import notes");

            bool approved = await ShowAndInteractAsync(service, review, window =>
            {
                Assert.AreEqual(ResizeMode.CanResize, window.ResizeMode);
                Assert.AreEqual(SizeToContent.Manual, window.SizeToContent);
                Assert.IsTrue(window.ActualHeight <= window.Height + 1, "Long text scrolls instead of growing the window.");
                Assert.IsTrue(FooterButton(window, "Cancel").IsCancel);
                Click(FooterButton(window, "Import notes"));
            });

            Assert.IsTrue(approved);
        });
    }

    [TestMethod]
    public async Task AFormIsAcceptedOnlyWhenValidAndCancellingCommitsNothing()
    {
        await UiThread.RunAsync(async () =>
        {
            var key = new DataTemplateKey(typeof(RenameForm));
            Application.Current.Resources[key] = (DataTemplate)XamlReader.Parse(
                """
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
                </DataTemplate>
                """);
            try
            {
                var service = new DialogService(Application.Current);
                var note = new Note("Fog over the quarry");

                var form = new RenameForm(note) { Name = "" };
                bool renamed = await ShowAndInteractAsync(service, form, window =>
                {
                    Click(FooterButton(window, "Rename"));
                    Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                    {
                        Assert.IsTrue(window.IsVisible, "An invalid form stays open.");
                        Assert.IsTrue(form.HasErrors);
                        form.Name = "Fog at dawn";
                        Click(FooterButton(window, "Rename"));
                    }, DispatcherPriority.ApplicationIdle);
                });
                Assert.IsTrue(renamed);
                Assert.AreEqual("Fog at dawn", note.Title);

                var cancelled = new RenameForm(note) { Name = "Discarded title" };
                bool kept = await ShowAndInteractAsync(service, cancelled, window => Click(FooterButton(window, "Cancel")));
                Assert.IsFalse(kept);
                Assert.AreEqual("Fog at dawn", note.Title, "Cancelling does not commit the edit.");
            }
            finally
            {
                Application.Current.Resources.Remove(key);
            }
        });
    }

    [TestMethod]
    public async Task AMissingTemplateFailsBeforeAnythingIsShown()
    {
        await UiThread.RunAsync(() =>
        {
            var service = new DialogService(Application.Current);
            var form = new RenameForm(new Note("Untitled"));

            var error = Assert.ThrowsExactly<InvalidOperationException>(() => service.ShowAsync(form));
            StringAssert.Contains(error.Message, nameof(RenameForm));
            Assert.IsFalse(Application.Current.Windows.OfType<DialogWindow>().Any());
        });
    }

    [TestMethod]
    public async Task ACompletedDialogCannotBeShownAgain()
    {
        await UiThread.RunAsync(() =>
        {
            var service = new DialogService(Application.Current);
            var message = new MessageDialogViewModel("Notice", "Shown once.");
            message.Cancel();

            Assert.ThrowsExactly<InvalidOperationException>(() => service.ShowAsync(message));
        });
    }

    private static async Task<bool> ShowAndInteractAsync(IDialogService service, DialogViewModel dialog, Action<DialogWindow> interact)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        // Never let a failed interaction leave a modal window open.
        var safety = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Normal, (_, _) =>
        {
            foreach (var open in Application.Current.Windows.OfType<DialogWindow>().ToArray())
            {
                open.Close();
            }
        }, dispatcher);
        Exception? failure = null;
        _ = dispatcher.InvokeAsync(() =>
        {
            try
            {
                interact(Application.Current.Windows.OfType<DialogWindow>().Single());
            }
            catch (Exception exception)
            {
                failure = exception;
                Application.Current.Windows.OfType<DialogWindow>().Single().Close();
            }
        }, DispatcherPriority.ApplicationIdle);
        try
        {
            bool result = await service.ShowAsync(dialog);
            if (failure is not null)
            {
                throw new AssertFailedException(failure.Message, failure);
            }

            return result;
        }
        finally
        {
            safety.Stop();
        }
    }

    private static Button FooterButton(Window window, string label) =>
        Visuals.Descendants<Button>(window).Single(button => Equals(button.Content, label));

    private static void Click(Button button) =>
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();

    internal sealed class Note(string title)
    {
        public string Title { get; set; } = title;
    }

    internal sealed class RenameForm : DialogViewModel
    {
        private readonly Note note;
        private string name;

        public RenameForm(Note note)
            : base("Rename note")
        {
            this.note = note;
            name = note.Title;
            Buttons =
            [
                new DialogButton("Rename", AcceptCommand) { IsDefault = true, IsPrimary = true },
                new DialogButton("Cancel", CancelCommand) { IsCancel = true }
            ];
        }

        [Required]
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value, validate: true);
        }

        public override IReadOnlyList<DialogButton> Buttons { get; }

        protected override void OnAccepted() => note.Title = Name;
    }
}
