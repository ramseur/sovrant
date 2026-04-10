using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sovrant.Desktop.Converters;

public sealed class BoolToStatusBrushConverter : IValueConverter
{
    public static readonly BoolToStatusBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "StatusPass" : "StatusFail";
        var app = Application.Current;
        if (app is not null
            && app.Styles.TryGetResource(key, app.ActualThemeVariant, out var resource)
            && resource is IBrush found)
        {
            return found;
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
