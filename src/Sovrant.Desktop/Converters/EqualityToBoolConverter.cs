using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sovrant.Desktop.Converters;

/// <summary>
/// Two-way equality converter used by mutually-exclusive RadioButton groups bound to a
/// single string property. Returns true when the bound value equals the parameter; on
/// ConvertBack, returns the parameter when checked so the source becomes the radio's value.
/// </summary>
public sealed class EqualityToBoolConverter : IValueConverter
{
    public static readonly EqualityToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : Avalonia.Data.BindingOperations.DoNothing;
}
