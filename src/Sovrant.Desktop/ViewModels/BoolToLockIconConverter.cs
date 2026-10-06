using System.Globalization;
using Avalonia.Data.Converters;
using Sovrant.Api.Ui;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Returns the <see cref="IconNames.Private"/> / <see cref="IconNames.Public"/> icon name
/// for binding to <c>SovrantIcon.IconName</c> (Phase 136); not usable on TextBlock.Text.
/// </summary>
public sealed class BoolToLockIconConverter : IValueConverter
{
    public static readonly BoolToLockIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? IconNames.Private : IconNames.Public;

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
