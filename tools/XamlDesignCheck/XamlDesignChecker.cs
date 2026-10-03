using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace XamlDesignCheck;

/// <summary>A literal design value in XAML that bypasses the Fluent theme resources.</summary>
/// <param name="Rule">The rule name: color, spacing, typography or corner-radius.</param>
/// <param name="Text">The attribute as written, for example <c>Margin="0,2,0,0"</c>. Allowlist entries match it.</param>
public sealed record Finding(string Rule, string Text, string Message, int Line, int Column);

/// <summary>
/// Checks XAML against the WPF Gallery Design Guidance and the .NET 10 Fluent theme:
/// colors come from theme brushes, margins and padding sit on the spacing steps,
/// text uses the type ramp's styles and keys, and corner radii use the Fluent radius keys.
/// </summary>
public static class XamlDesignChecker
{
    // WPF Gallery Design Guidance, Spacing page: 4 compact, 8 between controls, 12 control and header,
    // 16 card padding, 24 between content sections, 32 page padding, 48 between titled page sections.
    public static readonly IReadOnlyList<double> SpacingSteps = [0, 4, 8, 12, 16, 24, 32, 48];

    private static readonly HashSet<string> SpacingProperties = ["Margin", "Padding"];

    private static readonly HashSet<string> ColorProperties =
    [
        "Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color",
        "CaretBrush", "SelectionBrush", "SelectionTextBrush", "OpacityMask",
    ];

    public static IReadOnlyList<Finding> Check(string xaml)
    {
        var document = XDocument.Parse(xaml, LoadOptions.SetLineInfo);
        var findings = new List<Finding>();

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName == "Color" && !element.HasElements && element.Value.Trim().Length > 0)
            {
                findings.Add(At(element, "color", $"<Color>{element.Value.Trim()}</Color>",
                    "defines a new color. Use the Fluent theme brushes from the WPF Gallery Colors page instead."));
            }

            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                var property = PropertyName(element, attribute);
                if (property is null)
                {
                    continue;
                }

                var text = element.Name.LocalName == "Setter"
                    ? $"{property}=\"{attribute.Value}\""
                    : $"{attribute.Name.LocalName}=\"{attribute.Value}\"";
                var message = Check(property, attribute.Value.Trim());
                if (message is not null)
                {
                    findings.Add(At(attribute, message.Value.Rule, text, message.Value.Message));
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// The property an attribute sets: the attribute itself (with any owner prefix removed),
    /// or for a Setter's Value, the property the Setter names. Null for attributes that set nothing checked here.
    /// </summary>
    private static string? PropertyName(XElement element, XAttribute attribute)
    {
        if (element.Name.LocalName == "Setter")
        {
            return attribute.Name.LocalName == "Value"
                ? (string?)element.Attribute("Property") is { } setterProperty ? LocalProperty(setterProperty) : null
                : attribute.Name.LocalName == "Property" ? null : LocalProperty(attribute.Name.LocalName);
        }

        return attribute.Name.Namespace == XNamespace.None || attribute.Name.Namespace == element.Name.Namespace
            ? LocalProperty(attribute.Name.LocalName)
            : null;
    }

    private static string LocalProperty(string name) => name[(name.LastIndexOf('.') + 1)..];

    private static (string Rule, string Message)? Check(string property, string value)
    {
        if (value.Contains("SystemColors.", StringComparison.Ordinal))
        {
            return ("color", "uses SystemColors, which skip the Fluent palette. Use a Fluent theme brush such as AccentTextFillColorPrimaryBrush.");
        }

        if (value.StartsWith('{'))
        {
            return null;
        }

        if (ColorProperties.Contains(property) || property.EndsWith("Brush", StringComparison.Ordinal))
        {
            return value == "Transparent"
                ? null
                : ("color", "is a literal color. Use a Fluent theme brush as a DynamicResource, so it follows Light, Dark and high contrast.");
        }

        if (SpacingProperties.Contains(property))
        {
            return IsOnSpacingSteps(value)
                ? null
                : ("spacing", $"is off the spacing steps ({string.Join(", ", SpacingSteps)}). Use the nearest step that fits.");
        }

        return property switch
        {
            "FontSize" => ("typography", "is a literal font size. Use a type ramp style (CaptionTextBlockStyle, BodyTextBlockStyle, SubtitleTextBlockStyle, ...) or a Fluent key such as BodyTextBlockFontSize or DefaultIconFontSize."),
            "FontWeight" when value != "Normal" => ("typography", "sets a literal weight. Use BodyStrongTextBlockStyle or another type ramp style."),
            "FontFamily" => ("typography", "is a literal font. Text uses the theme font; icons use SymbolThemeFontFamily."),
            "CornerRadius" when value != "0" => ("corner-radius", "is a literal radius. Use ControlCornerRadius for in-page elements or OverlayCornerRadius for windows, cards and dialogs."),
            _ => null,
        };
    }

    private static bool IsOnSpacingSteps(string value)
    {
        foreach (var part in value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                || !SpacingSteps.Contains(number))
            {
                return false;
            }
        }

        return true;
    }

    private static Finding At(XObject node, string rule, string text, string message)
    {
        var info = (IXmlLineInfo)node;
        return new Finding(rule, text, $"{text} {message}", info.LineNumber, info.LinePosition);
    }
}
