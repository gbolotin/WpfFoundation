using System.IO;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class SourceTextTests
{
    /// <summary>
    /// Segoe Fluent Icons glyphs are private-use characters, which look empty in diffs and editors. Source files
    /// write them as escapes instead: "\uE711" in C# and "&amp;#xE711;" in XAML.
    /// </summary>
    [TestMethod]
    public void IconGlyphsAreWrittenAsEscapes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "WpfFoundation.sln")))
        {
            root = root.Parent;
        }

        Assert.IsNotNull(root, "The repository root was not found above the test output.");
        string[] offenders =
        [
            .. new[] { "src", "tests", "tools" }
                .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root.FullName, folder), "*.*", SearchOption.AllDirectories))
                .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
                .Where(entry => entry.line.Any(c => c is >= '\uE000' and <= '\uF8FF'))
                .Select(entry => $"{Path.GetRelativePath(root.FullName, entry.file)}({entry.number})")
        ];

        Assert.IsEmpty(offenders, $"Write these glyphs as escapes: {string.Join(", ", offenders)}");
    }
}
