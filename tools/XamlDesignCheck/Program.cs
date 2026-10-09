using XamlDesignCheck;

// Usage: dotnet run --project tools/XamlDesignCheck -- [repository root]
// Checks every .xaml file under the root, except bin and obj, against the Fluent design rules.
// Exceptions are listed with a reason in xaml-design-allowlist.txt at the root.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var allowlistPath = Path.Combine(root, Allowlist.FileName);
var allowlist = File.Exists(allowlistPath)
    ? Allowlist.Parse(File.ReadAllLines(allowlistPath))
    : Allowlist.Empty;

var annotate = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
var errorCount = 0;

void Report(string path, int line, int column, string message)
{
    errorCount++;
    Console.WriteLine($"{path}({line},{column}): error XDC: {message}");
    if (annotate)
    {
        Console.WriteLine($"::error file={path},line={line},col={column}::{message}");
    }
}

foreach (var error in allowlist.Errors)
{
    Report(Allowlist.FileName, error.Line, 1, error.Message);
}

var files = Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
    .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
    .Where(file => !file.Split('/').Any(part => part is "bin" or "obj" or ".git"))
    .Order(StringComparer.Ordinal);

var fileCount = 0;
foreach (var file in files)
{
    if (allowlist.IsExcluded(file))
    {
        continue;
    }

    fileCount++;
    foreach (var finding in XamlDesignChecker.Check(File.ReadAllText(Path.Combine(root, file))))
    {
        if (!allowlist.Allows(file, finding))
        {
            Report(file, finding.Line, finding.Column, $"{finding.Rule}: {finding.Message}");
        }
    }
}

foreach (var entry in allowlist.UnusedEntries())
{
    Report(Allowlist.FileName, entry.Line, 1, $"'{entry.Text}' no longer matches anything in {entry.Path}. Remove the entry.");
}

Console.WriteLine(errorCount == 0
    ? $"XAML design check passed for {fileCount} files."
    : $"XAML design check found {errorCount} problems in {fileCount} files. See the \"UI design\" section of the common development rules.");
return errorCount == 0 ? 0 : 1;
