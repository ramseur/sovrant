using System.Globalization;
using System.Text.Json;
using Scriban;
using Scriban.Runtime;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Renders a Scriban template string against a <see cref="JsonElement"/> data payload.
/// Registers the same formatting helpers as <see cref="TemplateFormat"/> so DB templates
/// can call <c>format_date</c>, <c>format_money</c>, <c>format_percent</c>, etc.
/// </summary>
internal static class ScribanRenderer
{
    public static string Render(string templateText, JsonElement data)
    {
        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            var msgs = string.Join("; ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template parse error: {msgs}");
        }

        var context = new TemplateContext { StrictVariables = false };

        var globals = new ScriptObject();
        BindJsonElement(globals, data);

        globals.Import("format_date",       new Func<object?, string>(FormatDateObj));
        globals.Import("format_money",      new Func<double, string, string>(FormatMoney));
        globals.Import("format_money_whole",new Func<double, string, string>(FormatMoneyWhole));
        globals.Import("format_number",     new Func<double, string>(FormatNumber));
        globals.Import("format_percent",    new Func<double, string>(FormatPercent));
        globals.Import("escape_pipes",      new Func<object?, string>(EscapePipesObj));
        globals.Import("slug",              new Func<object?, string, string>(SlugObj));
        globals.Import("normalize_currency",new Func<object?, string>(NormalizeCurrencyObj));

        context.PushGlobal(globals);
        return template.Render(context);
    }

    // ── JSON → Scriban binding ────────────────────────────────────────────────

    private static void BindJsonElement(ScriptObject target, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in element.EnumerateObject())
            target[prop.Name] = ToScribanValue(prop.Value);
    }

    private static object? ToScribanValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String  => element.GetString() ?? string.Empty,
            JsonValueKind.Number  => element.GetDouble(),
            JsonValueKind.True    => true,
            JsonValueKind.False   => false,
            JsonValueKind.Null    => null,
            JsonValueKind.Array   => ToScribanArray(element),
            JsonValueKind.Object  => ToScribanObject(element),
            _                     => null,
        };

    private static ScriptArray ToScribanArray(JsonElement array)
    {
        var result = new ScriptArray();
        foreach (var item in array.EnumerateArray())
            result.Add(ToScribanValue(item));
        return result;
    }

    private static ScriptObject ToScribanObject(JsonElement obj)
    {
        var result = new ScriptObject();
        foreach (var prop in obj.EnumerateObject())
            result[prop.Name] = ToScribanValue(prop.Value);
        return result;
    }

    // ── Custom functions ──────────────────────────────────────────────────────

    private static string FormatDateObj(object? value)
    {
        var str = value?.ToString();
        if (string.IsNullOrWhiteSpace(str)) return string.Empty;
        return DateOnly.TryParse(str, CultureInfo.InvariantCulture, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : str;
    }

    private static string FormatMoney(double amount, string currency) =>
        TemplateFormat.FormatMoney((decimal)amount, TemplateFormat.NormalizeCurrency(currency));

    private static string FormatMoneyWhole(double amount, string currency) =>
        TemplateFormat.FormatMoneyWhole((decimal)amount, TemplateFormat.NormalizeCurrency(currency));

    private static string FormatNumber(double value) =>
        TemplateFormat.FormatNumber((decimal)value);

    private static string FormatPercent(double rate) =>
        TemplateFormat.FormatPercent((decimal)rate);

    private static string EscapePipesObj(object? value) =>
        value is null ? string.Empty : TemplateFormat.EscapePipes(value.ToString() ?? string.Empty);

    private static string SlugObj(object? value, string fallback = "item") =>
        TemplateFormat.Slug(value?.ToString() ?? string.Empty, fallback);

    private static string NormalizeCurrencyObj(object? value) =>
        TemplateFormat.NormalizeCurrency(value?.ToString() ?? string.Empty);
}
