using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Nav-redesign: rail buttons left-align their icon+label when expanded and
/// center the icon-only content when collapsed. Button.Padding stays
/// symmetric ("10,0") in both states so centering isn't skewed.
/// </summary>
public sealed class BoolToAlignmentConverter : IValueConverter
{
    public static readonly BoolToAlignmentConverter CenterWhenTrue = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? HorizontalAlignment.Center : HorizontalAlignment.Left;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
