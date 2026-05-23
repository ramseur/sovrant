using System.Globalization;
using Avalonia.Data.Converters;

namespace Sovrant.Desktop.Converters;

/// <summary>Returns true when an integer is greater than zero. Used for IsVisible bindings.</summary>
public sealed class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
