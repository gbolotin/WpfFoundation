using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfFoundation.Converters;

/// <summary>Joins WPF validation messages for a tooltip or accessible description.</summary>
public sealed class ValidationErrorsToTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.FirstOrDefault() is IEnumerable<ValidationError> errors
            ? string.Join(Environment.NewLine, errors.Select(error => error.ErrorContent?.ToString()).Where(message => !string.IsNullOrWhiteSpace(message)))
            : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Validation messages are read-only.");
}
