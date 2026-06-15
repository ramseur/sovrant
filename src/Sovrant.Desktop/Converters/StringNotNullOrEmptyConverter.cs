using System.Globalization;
using Avalonia.Data.Converters;

namespace Sovrant.Desktop.Converters;

/// <summary>Returns true when a string is non-null and non-empty (used for conditional visibility).</summary>
public sealed class StringNotNullOrEmptyConverter : IValueConverter
{
    public static readonly StringNotNullOrEmptyConverter Instance = new();
    private StringNotNullOrEmptyConverter() { }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
