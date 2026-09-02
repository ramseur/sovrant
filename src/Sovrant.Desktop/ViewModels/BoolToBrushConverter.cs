using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 69 — converts a boolean (rail-group selected?) into a brush so the
/// active rail icon can render as filled brand vs. transparent.
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public IBrush TrueBrush { get; init; } = Brushes.Transparent;
    public IBrush FalseBrush { get; init; } = Brushes.Transparent;
    public string? TrueResource { get; init; }
    public string? FalseResource { get; init; }

    public static readonly BoolToBrushConverter BrandOrTransparent = new()
    {
        TrueResource = "BrandPrimary",
        FalseBrush = Brushes.Transparent,
    };

    public static readonly BoolToBrushConverter WhiteOrSecondary = new()
    {
        TrueBrush = Brushes.White,
        FalseResource = "TextSecondary",
    };

    /// <summary>Nav-redesign active state: soft brand tint background instead of a solid fill.</summary>
    public static readonly BoolToBrushConverter BrandTintOrTransparent = new()
    {
        TrueResource = "BrandPrimarySoft",
        FalseBrush = Brushes.Transparent,
    };

    /// <summary>Nav-redesign active state: brand-colored icon/text instead of white-on-solid.</summary>
    public static readonly BoolToBrushConverter BrandOrSecondary = new()
    {
        TrueResource = "BrandPrimary",
        FalseResource = "TextSecondary",
    };

    public static readonly BoolToBrushConverter ErrorOrToolBorder = new()
    {
        TrueResource = "StatusFail",
        FalseResource = "ToolUseBorder",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (flag)
        {
            if (TrueResource is not null && Application.Current is { } app
                && app.TryGetResource(TrueResource, app.ActualThemeVariant, out var res) && res is IBrush rb)
                return rb;
            return TrueBrush;
        }
        if (FalseResource is not null && Application.Current is { } app2
            && app2.TryGetResource(FalseResource, app2.ActualThemeVariant, out var res2) && res2 is IBrush rb2)
            return rb2;
        return FalseBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
