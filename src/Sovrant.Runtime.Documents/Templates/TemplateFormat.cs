using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Formatting + JSON-reading helpers shared across built-in templates.
/// Keeps each template file focused on content rather than re-declaring
/// currency/date/slug boilerplate.
/// </summary>
public static class TemplateFormat
{
    public static string GetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    public static decimal GetDecimal(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return 0m;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => 0m,
        };
    }

    public static int GetInteger(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(v.GetString(), out var n) => n,
            _ => 0,
        };
    }

    public static DateOnly? GetDate(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        return DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string FormatMoney(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{currency} {amount:N2}");

    public static string FormatMoneyWhole(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{currency} {amount:N0}");

    public static string FormatNumber(decimal value) =>
        value == Math.Floor(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string FormatPercent(decimal rate) =>
        rate == Math.Floor(rate)
            ? rate.ToString("0", CultureInfo.InvariantCulture) + "%"
            : rate.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static string NormalizeCurrency(string input) =>
        string.IsNullOrWhiteSpace(input) ? "USD" : input.Trim().ToUpperInvariant();

    public static string EscapePipes(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    public static string Slug(string value, string fallback = "item")
    {
        ArgumentNullException.ThrowIfNull(value);
        var cleaned = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c)) cleaned.Append(char.ToLowerInvariant(c));
            else if (c is '-' or '_' or '.') cleaned.Append(c);
            else if (char.IsWhiteSpace(c)) cleaned.Append('-');
        }
        var slug = cleaned.ToString().Trim('-');
        return slug.Length == 0 ? fallback : slug;
    }
}
