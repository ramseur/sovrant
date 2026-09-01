using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Returns the IconLock/IconUnlock StreamGeometry (from NavIcons.axaml) for
/// binding to a Path's Data — not text, so this is not usable on TextBlock.Text.
/// </summary>
public sealed class BoolToLockIconConverter : IValueConverter
{
    public static readonly BoolToLockIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is bool b && b ? "IconLock" : "IconUnlock";
        if (Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var res))
            return res;
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToPrivacyLabelConverter : IValueConverter
{
    public static readonly BoolToPrivacyLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "Private" : "Public";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
