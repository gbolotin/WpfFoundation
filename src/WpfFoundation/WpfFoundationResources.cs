using System.IO.Packaging;

namespace WpfFoundation;

/// <summary>
/// Locates the WpfFoundation entry resource dictionary.
/// </summary>
public static class WpfFoundationResources
{
    /// <summary>
    /// The pack URI of the entry dictionary that applications merge into their resources.
    /// </summary>
    public static Uri EntryDictionaryUri { get; } = CreateEntryDictionaryUri();

    private static Uri CreateEntryDictionaryUri()
    {
        // Reading the scheme name registers the pack URI scheme, which WPF otherwise registers only once an Application exists.
        string scheme = PackUriHelper.UriSchemePack;
        return new Uri($"{scheme}://application:,,,/WpfFoundation;component/Themes/WpfFoundation.xaml", UriKind.Absolute);
    }
}
