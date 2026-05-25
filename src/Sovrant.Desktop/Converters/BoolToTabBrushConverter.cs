using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sovrant.Desktop.Converters;

public sealed class BoolToTabBrushConverter : IValueConverter
{
    public static readonly BoolToTabBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            var app = Application.Current;
            if (app is not null
                && app.Styles.TryGetResource("BrandPrimary", app.ActualThemeVariant, out var resource)
                && resource is IBrush brush)
                return brush;
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
