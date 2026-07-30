using System.Globalization;
using Avalonia.Data.Converters;

namespace Sovrant.Desktop.ViewModels;

public sealed class BoolToLockIconConverter : IValueConverter
{
    public static readonly BoolToLockIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "lock" : "unlock";

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
