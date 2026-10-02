using System.IO;
using System.Windows;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class EntryDictionaryTests
{
    // The committed list of resource keys. Removing or renaming a key breaks applications,
    // so the test fails until this list changes together with the package's major version.
    private static readonly string KeyListPath = Path.Combine(AppContext.BaseDirectory, "ResourceKeys.txt");

    [TestMethod]
    public async Task EntryDictionaryKeysMatchTheCommittedList()
    {
        await UiThread.RunAsync(() =>
        {
            var dictionary = new ResourceDictionary { Source = WpfFoundationResources.EntryDictionaryUri };
            string[] actual = [.. AllKeys(dictionary).OfType<string>().Order(StringComparer.Ordinal)];
            string[] committed = [.. File.ReadAllLines(KeyListPath).Where(line => line.Length > 0 && !line.StartsWith('#')).Order(StringComparer.Ordinal)];

            CollectionAssert.AreEqual(committed, actual, $"Entry dictionary keys changed. Actual keys:{Environment.NewLine}{string.Join(Environment.NewLine, actual)}");
        });
    }

    [TestMethod]
    public async Task EveryKeyIsPrefixedAndResolvesInTheFluentTheme()
    {
        await UiThread.RunAsync(() =>
        {
            var dictionary = new ResourceDictionary { Source = WpfFoundationResources.EntryDictionaryUri };
            foreach (string key in AllKeys(dictionary).OfType<string>())
            {
                StringAssert.StartsWith(key, "Wf");
                Assert.IsNotNull(Application.Current.FindResource(key), key);
            }
        });
    }

    private static IEnumerable<object> AllKeys(ResourceDictionary dictionary) =>
        dictionary.Keys.Cast<object>().Concat(dictionary.MergedDictionaries.SelectMany(AllKeys));
}
