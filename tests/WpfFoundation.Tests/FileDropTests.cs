using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Behaviors;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class FileDropTests
{
    [TestMethod]
    public async Task TheCommandDecidesAcceptanceAndRunsOnlyOnDrop()
    {
        await UiThread.RunAsync(() =>
        {
            var executed = new List<string[]>();
            var command = new RelayCommand<string[]>(paths => executed.Add(paths!), paths => paths!.All(path => path.EndsWith(".txt", StringComparison.Ordinal)));
            var target = new Border();
            FileDrop.SetCommand(target, command);
            var accepted = new DataObject(DataFormats.FileDrop, new[] { @"C:\Notes\a.txt", @"C:\Notes\b.txt" });
            var rejected = new DataObject(DataFormats.FileDrop, new[] { @"C:\Notes\a.png" });

            Assert.AreEqual(DragDropEffects.Copy, RaiseDrag(target, accepted, UIElement.PreviewDragEnterEvent));
            Assert.AreEqual(DragDropEffects.Copy, RaiseDrag(target, accepted, UIElement.PreviewDragOverEvent));
            Assert.IsEmpty(executed, "Dragging over never runs the command.");
            Assert.AreEqual(DragDropEffects.None, RaiseDrag(target, rejected, UIElement.PreviewDragOverEvent));
            Assert.AreEqual(DragDropEffects.None, RaiseDrag(target, rejected, UIElement.PreviewDropEvent));
            Assert.IsEmpty(executed, "A rejected drop does not run the command.");

            Assert.AreEqual(DragDropEffects.Copy, RaiseDrag(target, accepted, UIElement.PreviewDropEvent));
            Assert.HasCount(1, executed);
            CollectionAssert.AreEqual(new[] { @"C:\Notes\a.txt", @"C:\Notes\b.txt" }, executed[0]);
        });
    }

    [TestMethod]
    public async Task DragsWithoutFilesOrCopyAreRejected()
    {
        await UiThread.RunAsync(() =>
        {
            var command = new RelayCommand<string[]>(_ => Assert.Fail("Must not run."));
            var target = new Border();
            FileDrop.SetCommand(target, command);

            Assert.AreEqual(DragDropEffects.None, RaiseDrag(target, new DataObject(DataFormats.Text, "text"), UIElement.PreviewDropEvent));
            Assert.AreEqual(DragDropEffects.None, RaiseDrag(target, new DataObject(DataFormats.FileDrop, Array.Empty<string>()), UIElement.PreviewDropEvent));
            Assert.AreEqual(DragDropEffects.None, RaiseDrag(target, new DataObject(DataFormats.FileDrop, new[] { @"C:\a.txt" }), UIElement.PreviewDropEvent, DragDropEffects.Move));
        });
    }

    [TestMethod]
    public async Task ClearingTheCommandStopsHandlingDrags()
    {
        await UiThread.RunAsync(() =>
        {
            var target = new Border();
            FileDrop.SetCommand(target, new RelayCommand<string[]>(_ => Assert.Fail("Must not run.")));
            FileDrop.SetCommand(target, null);

            var args = CreateArgs(target, new DataObject(DataFormats.FileDrop, new[] { @"C:\a.txt" }), UIElement.PreviewDropEvent, DragDropEffects.Copy);
            target.RaiseEvent(args);
            Assert.IsFalse(args.Handled);
        });
    }

    private static DragDropEffects RaiseDrag(UIElement target, IDataObject data, RoutedEvent routedEvent, DragDropEffects allowed = DragDropEffects.Copy)
    {
        var args = CreateArgs(target, data, routedEvent, allowed);
        target.RaiseEvent(args);
        Assert.IsTrue(args.Handled);
        return args.Effects;
    }

    // WPF constructs drag arguments internally for native drops; raise the same routed events in-process.
    private static DragEventArgs CreateArgs(UIElement target, IDataObject data, RoutedEvent routedEvent, DragDropEffects allowed)
    {
        var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [data, DragDropKeyStates.None, allowed, target, new Point()], null)!;
        args.RoutedEvent = routedEvent;
        return args;
    }
}
