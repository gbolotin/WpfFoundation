using System.Windows;
using System.Windows.Controls;

namespace WpfFoundation.Gallery.Controls;

/// <summary>
/// Shows a read-only usage snippet with a button that copies it to the clipboard.
/// </summary>
public partial class CodeSample : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(CodeSample), new PropertyMetadata("Usage"));

    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(nameof(Code), typeof(string), typeof(CodeSample), new PropertyMetadata(string.Empty));

    public CodeSample()
    {
        InitializeComponent();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Code.Trim());
    }
}
