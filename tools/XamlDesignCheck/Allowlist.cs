namespace XamlDesignCheck;

/// <summary>
/// Justified exceptions to the design check, one per line:
/// <c>path | rule | attribute as written | reason</c>.
/// A path ending in <c>/</c> covers a folder; <c>*</c> matches any rule or any attribute.
/// Every entry needs a reason, and an entry that no longer matches anything is an error.
/// </summary>
public sealed class Allowlist
{
    public const string FileName = "xaml-design-allowlist.txt";

    public static readonly Allowlist Empty = new([], []);

    private readonly List<Entry> entries;
    private readonly HashSet<Entry> used = [];

    private Allowlist(List<Entry> entries, List<Error> errors)
    {
        this.entries = entries;
        Errors = errors;
    }

    public sealed record Entry(int Line, string Path, string Rule, string Text, string Reason);

    public sealed record Error(int Line, string Message);

    public IReadOnlyList<Error> Errors { get; }

    public static Allowlist Parse(IEnumerable<string> lines)
    {
        var entries = new List<Entry>();
        var errors = new List<Error>();
        var number = 0;
        foreach (var line in lines)
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length < 4 || parts.Any(part => part.Length == 0))
            {
                errors.Add(new Error(number, "Write each entry as 'path | rule | attribute as written | reason'. The reason is required."));
                continue;
            }

            entries.Add(new Entry(number, parts[0].Replace('\\', '/'), parts[1], parts[2], parts[3]));
        }

        return new Allowlist(entries, errors);
    }

    /// <summary>True when an entry with any rule and any attribute covers the whole file or its folder.</summary>
    public bool IsExcluded(string path) =>
        Use(entries.FirstOrDefault(entry => entry.Rule == "*" && entry.Text == "*" && Covers(entry, path)));

    public bool Allows(string path, Finding finding) =>
        Use(entries.FirstOrDefault(entry => Covers(entry, path)
            && (entry.Rule == "*" || entry.Rule == finding.Rule)
            && (entry.Text == "*" || entry.Text == finding.Text)));

    public IEnumerable<Entry> UnusedEntries() => entries.Where(entry => !used.Contains(entry));

    private static bool Covers(Entry entry, string path) =>
        entry.Path.EndsWith('/') ? path.StartsWith(entry.Path, StringComparison.Ordinal) : path == entry.Path;

    private bool Use(Entry? entry)
    {
        if (entry is null)
        {
            return false;
        }

        used.Add(entry);
        return true;
    }
}
