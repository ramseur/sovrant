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

    public string? ActiveResource { get; init; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string current && parameter is string target && current == target)
        {
            if (ActiveResource is not null && Application.Current is { } app
                && app.TryGetResource(ActiveResource, app.ActualThemeVariant, out var res) && res is IBrush rb)
                return rb;
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
