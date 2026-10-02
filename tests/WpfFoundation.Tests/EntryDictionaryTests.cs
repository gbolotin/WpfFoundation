using System.Windows;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class EntryDictionaryTests
{
    [TestMethod]
    public void EntryDictionaryLoadsFromItsDocumentedPackUri()
    {
        StaDispatcher.Run(() =>
        {
            var dictionary = new ResourceDictionary { Source = WpfFoundationResources.EntryDictionaryUri };

            Assert.AreEqual(WpfFoundationResources.EntryDictionaryUri, dictionary.Source);
        });
    }
}
