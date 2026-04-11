using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sovrant.Desktop.Converters;

/// <summary>Converts a bool (healthy/unhealthy) to a status brush.</summary>
public sealed class BoolToStatusBrushConverter : IValueConverter
{
    public static readonly BoolToStatusBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "StatusPass" : "StatusFail";
        return LookupBrush(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    internal static IBrush LookupBrush(string key)
    {
        var app = Application.Current;
        if (app is null) return Brushes.Gray;

        // Try Application.Resources first (where ThemeDictionaries live).
        if (app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) && res is IBrush b1)
            return b1;

        // Fallback to Styles (for non-theme-variant resources).
        if (app.Styles.TryGetResource(key, app.ActualThemeVariant, out var res2) && res2 is IBrush b2)
            return b2;

        return Brushes.Gray;
    }
}

/// <summary>
/// Converts a status string (PASS, WARN, FAIL, SKIP) to a colored brush.
/// </summary>
public sealed class StatusTextToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value as string ?? string.Empty;
        var key = status switch
        {
            "PASS" or "Connected" => "StatusPass",
            "WARN" => "StatusWarn",
            "FAIL" => "StatusFail",
            "Not Connected" => "StatusFail",
            _ => (string?)null,
        };

        if (key is not null)
            return BoolToStatusBrushConverter.LookupBrush(key);

        // SKIP or unknown
        return new SolidColorBrush(Color.Parse("#666666"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
