using WpfFoundation.Dialogs;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class FileDialogServiceTests
{
    [TestMethod]
    public void AnImplementationWithoutTheFolderOptionsFallsBackToThePlainFolderPicker()
    {
        IFileDialogService files = new PlainPickers(@"C:\Trail logs");

        Assert.AreEqual(@"C:\Trail logs", files.PickFolder("Choose a trail log folder", @"C:\Field notes"));
    }

    // Implements only the members that existed before the folder-picker options were added.
    private sealed class PlainPickers(string? folder) : IFileDialogService
    {
        public IReadOnlyList<string> PickFiles(string filter, bool allowMultiple = true) => [];

        public string? PickFolder() => folder;

        public string? PickSaveFile(string filter, string? fileName = null) => null;
    }
}
