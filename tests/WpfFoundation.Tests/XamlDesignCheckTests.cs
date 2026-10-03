using XamlDesignCheck;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class XamlDesignCheckTests
{
    private const string Namespaces = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [TestMethod]
    public void ThemeResourcesAndStepValuesPass()
    {
        var findings = Check(
            """
            <Border Background="{DynamicResource CardBackgroundFillColorDefaultBrush}"
                    BorderBrush="Transparent"
                    CornerRadius="{StaticResource OverlayCornerRadius}"
                    Padding="16"
                    Margin="0,8,24,48">
                <TextBlock Style="{StaticResource BodyStrongTextBlockStyle}"
                           FontSize="{StaticResource CaptionTextBlockFontSize}"
                           FontWeight="Normal"
                           Margin="12 4" />
            </Border>
            """);

        Assert.IsEmpty(findings);
    }

    [TestMethod]
    [DataRow("Foreground=\"#FF625A\"", "color")]
    [DataRow("Background=\"White\"", "color")]
    [DataRow("TextElement.Foreground=\"Red\"", "color")]
    [DataRow("Foreground=\"{DynamicResource {x:Static SystemColors.AccentColorBrushKey}}\"", "color")]
    [DataRow("Margin=\"0,2,0,0\"", "spacing")]
    [DataRow("Margin=\"0,-8,0,0\"", "spacing")]
    [DataRow("Padding=\"10\"", "spacing")]
    [DataRow("FontSize=\"14\"", "typography")]
    [DataRow("FontWeight=\"SemiBold\"", "typography")]
    [DataRow("FontFamily=\"Segoe Fluent Icons\"", "typography")]
    [DataRow("CornerRadius=\"6\"", "corner-radius")]
    public void LiteralDesignValuesAreReported(string attribute, string rule)
    {
        var findings = Check($"<Border {attribute} />");

        Assert.HasCount(1, findings);
        Assert.AreEqual(rule, findings[0].Rule);
        Assert.AreEqual(attribute, findings[0].Text);
        Assert.AreEqual(1, findings[0].Line);
    }

    [TestMethod]
    public void SetterValuesAreCheckedAsTheirProperty()
    {
        var findings = Check(
            """
            <Style TargetType="TextBlock">
                <Setter Property="Margin" Value="0,2" />
                <Setter Property="Foreground" Value="{DynamicResource TextFillColorPrimaryBrush}" />
                <Setter TargetName="Knob" Property="Border.CornerRadius" Value="5" />
            </Style>
            """);

        CollectionAssert.AreEqual(new[] { "Margin=\"0,2\"", "CornerRadius=\"5\"" }, findings.Select(finding => finding.Text).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 4 }, findings.Select(finding => finding.Line).ToArray());
    }

    [TestMethod]
    public void ColorResourcesAreReported()
    {
        var findings = Check("<ResourceDictionary><Color x:Key=\"Brand\">#0067D9</Color><SolidColorBrush x:Key=\"BrandBrush\" Color=\"#0067D9\" /></ResourceDictionary>");

        CollectionAssert.AreEqual(new[] { "color", "color" }, findings.Select(finding => finding.Rule).ToArray());
    }

    [TestMethod]
    public void AllowlistMatchesFileRuleAndAttribute()
    {
        var allowlist = Allowlist.Parse(
        [
            "# comment",
            "Views/Media.xaml | spacing | Padding=\"7,6\" | Compact row buttons.",
            "mockup/ | * | * | Throwaway mockups.",
        ]);
        var finding = new Finding("spacing", "Padding=\"7,6\"", "", 1, 1);

        Assert.IsEmpty(allowlist.Errors);
        Assert.IsTrue(allowlist.Allows("Views/Media.xaml", finding));
        Assert.IsFalse(allowlist.Allows("Views/Settings.xaml", finding));
        Assert.IsFalse(allowlist.Allows("Views/Media.xaml", finding with { Text = "Padding=\"10\"" }));
        Assert.IsTrue(allowlist.IsExcluded("mockup/Views/Main.xaml"));
        Assert.IsFalse(allowlist.IsExcluded("src/Views/Main.xaml"));
        Assert.IsEmpty(allowlist.UnusedEntries());
    }

    [TestMethod]
    public void AllowlistReportsEntriesWithoutReasonAndUnusedEntries()
    {
        var allowlist = Allowlist.Parse(
        [
            "Views/Media.xaml | spacing | Padding=\"7,6\"",
            "Views/Media.xaml | spacing | Padding=\"10\" | Fixed since.",
        ]);

        Assert.HasCount(1, allowlist.Errors);
        Assert.AreEqual(1, allowlist.Errors[0].Line);
        Assert.AreEqual(2, allowlist.UnusedEntries().Single().Line);
    }

    private static IReadOnlyList<Finding> Check(string xaml) =>
        XamlDesignChecker.Check(xaml.Insert(xaml.IndexOfAny([' ', '>', '/']), " " + Namespaces));
}
