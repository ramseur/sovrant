using System.Globalization;
using Avalonia.Data.Converters;

namespace Sovrant.Desktop.Converters;

/// <summary>Returns true when an integer is greater than zero. Use <see cref="Inverse"/> for the negated form.</summary>
public sealed class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();
    /// <summary>Returns true when the integer is zero (empty-state visibility).</summary>
    public static readonly IntToBoolConverter Inverse = new(invert: true);

    private readonly bool _invert;
    private IntToBoolConverter(bool invert = false) => _invert = invert;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is int i && i > 0;
        return _invert ? !result : result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
