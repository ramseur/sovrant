using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sovrant.Desktop.Converters;

public sealed class BoolToTabBrushConverter : IValueConverter
{
    public static readonly BoolToTabBrushConverter Instance = new();

    /// <summary>Text color counterpart: white on the active (filled) tab, normal text color otherwise.</summary>
    public static readonly BoolToTabBrushConverter Foreground = new() { ActiveResource = "White", InactiveResource = "TextPrimary" };

    public string ActiveResource { get; init; } = "BrandPrimary";
    public string? InactiveResource { get; init; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? ActiveResource : InactiveResource;
        if (key is not null)
        {
            var app = Application.Current;
            if (app is not null
                && app.TryGetResource(key, app.ActualThemeVariant, out var resource)
                && resource is IBrush brush)
                return brush;
            if (key == "White") return Brushes.White;
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
