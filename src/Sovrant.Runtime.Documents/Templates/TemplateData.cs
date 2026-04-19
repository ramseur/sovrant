using System.Globalization;
using System.Text.Json;
using Sovrant.Api.Errors;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Read-only facade over a <see cref="JsonElement"/> that templates use
/// during <see cref="IDocumentTemplate.Render"/>. Handles validation
/// (required fields, type coercion) in one place so individual templates
/// stay focused on layout.
/// </summary>
public sealed class TemplateData
{
    private readonly JsonElement _root;

    private TemplateData(JsonElement root) { _root = root; }

    /// <summary>
    /// Validates <paramref name="data"/> against <paramref name="fields"/>
    /// and returns a facade to read values from. Throws
    /// <see cref="TemplateValidationException"/> on any schema mismatch.
    /// </summary>
    public static TemplateData Validate(JsonElement data, IReadOnlyList<TemplateField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (data.ValueKind != JsonValueKind.Object)
            throw new TemplateValidationException("Template data must be a JSON object.");

        var errors = new List<string>();
        foreach (var field in fields)
        {
            if (!data.TryGetProperty(field.Name, out var value))
            {
                if (field.Required) errors.Add($"Missing required field '{field.Name}'.");
                continue;
            }

            if (value.ValueKind == JsonValueKind.Null)
            {
                if (field.Required) errors.Add($"Field '{field.Name}' is null but required.");
                continue;
            }

            ValidateType(field, value, errors);
        }

        if (errors.Count > 0)
            throw new TemplateValidationException(string.Join(" ", errors));

        return new TemplateData(data);
    }

    public string String(string name, string fallback = "") =>
        _root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback
            : fallback;

    public decimal Decimal(string name, decimal fallback = 0m)
    {
        if (!_root.TryGetProperty(name, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String => decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                ? d : fallback,
            _ => fallback,
        };
    }

    public int Integer(string name, int fallback = 0)
    {
        if (!_root.TryGetProperty(name, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(v.GetString(), out var n) => n,
            _ => fallback,
        };
    }

    public DateOnly? Date(string name)
    {
        if (!_root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        return DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public bool Boolean(string name, bool fallback = false) =>
        _root.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : fallback;

    public IReadOnlyList<string> StringArray(string name)
    {
        if (!_root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var list = new List<string>();
        foreach (var el in v.EnumerateArray())
            if (el.ValueKind == JsonValueKind.String)
                list.Add(el.GetString() ?? string.Empty);
        return list;
    }

    public IReadOnlyList<JsonElement> ObjectArray(string name)
    {
        if (!_root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return Array.Empty<JsonElement>();
        return [.. v.EnumerateArray()];
    }

    public bool Has(string name) =>
        _root.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null;

    private static void ValidateType(TemplateField field, JsonElement value, List<string> errors)
    {
        switch (field.Type)
        {
            case TemplateFieldType.String:
            case TemplateFieldType.Text:
                if (value.ValueKind != JsonValueKind.String)
                    errors.Add($"Field '{field.Name}' must be a string.");
                break;
            case TemplateFieldType.Integer:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
                    errors.Add($"Field '{field.Name}' must be an integer.");
                break;
            case TemplateFieldType.Decimal:
            case TemplateFieldType.Currency:
                if (value.ValueKind != JsonValueKind.Number)
                    errors.Add($"Field '{field.Name}' must be a number.");
                break;
            case TemplateFieldType.Date:
                if (value.ValueKind != JsonValueKind.String ||
                    !DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, out _))
                    errors.Add($"Field '{field.Name}' must be an ISO date string (YYYY-MM-DD).");
                break;
            case TemplateFieldType.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    errors.Add($"Field '{field.Name}' must be a boolean.");
                break;
            case TemplateFieldType.StringArray:
                if (value.ValueKind != JsonValueKind.Array)
                    errors.Add($"Field '{field.Name}' must be an array of strings.");
                break;
            case TemplateFieldType.ObjectArray:
                if (value.ValueKind != JsonValueKind.Array)
                    errors.Add($"Field '{field.Name}' must be an array of objects.");
                break;
        }
    }
}

/// <summary>Thrown when template data fails schema validation.</summary>
public sealed class TemplateValidationException : SovrantException
{
    public TemplateValidationException(string message) : base(message) { }
    public TemplateValidationException() { }
    public TemplateValidationException(string message, Exception innerException) : base(message, innerException) { }
}
