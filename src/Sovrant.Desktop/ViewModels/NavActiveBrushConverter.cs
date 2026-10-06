using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Converts a SelectedNavItem string to a highlight brush for sub-panel nav buttons.
/// Binding value is SelectedNavItem; ConverterParameter is the expected page name.
/// </summary>
public sealed class NavActiveBrushConverter : IValueConverter
{
    public static readonly NavActiveBrushConverter SubNavBg = new() { ActiveResource = "SurfaceHover" };
    public static readonly NavActiveBrushConverter SubNavFg = new() { ActiveResource = "BrandPrimary" };

    /// <summary>Nav-redesign: soft brand tint instead of the plain hover-gray background.</summary>
    public static readonly NavActiveBrushConverter SubNavBgSoft = new() { ActiveResource = "BrandPrimarySoft" };

    /// <summary>Nav-redesign: brand color when active, normal text color otherwise (unlike
    /// the background variants above, text must stay visible in the inactive state too).</summary>
    public static readonly NavActiveBrushConverter SubNavFgOrText = new() { ActiveResource = "BrandPrimary", InactiveResource = "TextPrimary" };

    public string? ActiveResource { get; init; }
    public string? InactiveResource { get; init; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string current && parameter is string target && current == target)
        {
            if (ActiveResource is not null && Application.Current is { } app
                && app.TryGetResource(ActiveResource, app.ActualThemeVariant, out var res) && res is IBrush rb)
                return rb;
        }
        else if (InactiveResource is not null && Application.Current is { } app2
            && app2.TryGetResource(InactiveResource, app2.ActualThemeVariant, out var res2) && res2 is IBrush rb2)
        {
            return rb2;
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
