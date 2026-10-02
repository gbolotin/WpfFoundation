using System.Globalization;
using System.Windows;
using WpfFoundation.Converters;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class ConverterTests
{
    [TestMethod]
    public void InverseBooleanTreatsOnlyFalseAsFalse()
    {
        var converter = new InverseBooleanConverter();

        Assert.AreEqual(true, converter.Convert(false, typeof(bool), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(false, converter.Convert(true, typeof(bool), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(false, converter.Convert(null!, typeof(bool), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(false, converter.Convert(DependencyProperty.UnsetValue, typeof(bool), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(true, converter.ConvertBack(false, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void InverseBooleanToVisibilityShowsOnlyForFalse()
    {
        var converter = new InverseBooleanToVisibilityConverter();

        Assert.AreEqual(Visibility.Visible, converter.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(Visibility.Collapsed, converter.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(Visibility.Collapsed, converter.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ReferenceEqualsToVisibilityRequiresTheSameAvailableObject()
    {
        var converter = new ReferenceEqualsToVisibilityConverter();
        var item = new object();

        Assert.AreEqual(Visibility.Visible, converter.Convert([item, item], typeof(Visibility), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(Visibility.Collapsed, converter.Convert([item, new object()], typeof(Visibility), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(Visibility.Collapsed, converter.Convert([null!, null!], typeof(Visibility), null!, CultureInfo.InvariantCulture), "Two missing values are not a match.");
        Assert.AreEqual(Visibility.Collapsed, converter.Convert([DependencyProperty.UnsetValue, DependencyProperty.UnsetValue], typeof(Visibility), null!, CultureInfo.InvariantCulture), "Unresolved bindings are not a match.");
        Assert.AreEqual(Visibility.Collapsed, converter.Convert([item], typeof(Visibility), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(Visibility.Collapsed, converter.Convert(["text", new string("text".ToCharArray())], typeof(Visibility), null!, CultureInfo.InvariantCulture), "Equal but distinct objects are not a match.");
    }
}
