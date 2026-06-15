using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// JSON roundtrip for <see cref="TemplateField"/> collections stored in the
/// <c>fields_json</c> column of <c>knowledge_pages</c>. Uses camelCase names
/// and string enum values so the JSON is readable and stable across refactors.
/// </summary>
internal static class TemplateFieldSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy      = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition    = JsonIgnoreCondition.WhenWritingNull,
        Converters                = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(IReadOnlyList<TemplateField> fields) =>
        JsonSerializer.Serialize(ToDto(fields), s_options);

    public static IReadOnlyList<TemplateField> Deserialize(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<FieldDto>>(json, s_options) ?? [];
        return dtos.Select(FromDto).ToList();
    }

    // ── DTO ──────────────────────────────────────────────────────────────────

    private sealed class FieldDto
    {
        public string Name { get; set; } = string.Empty;
        public TemplateFieldType Type { get; set; }
        public bool Required { get; set; }
        public string? Description { get; set; }
        public List<FieldDto>? ItemFields { get; set; }
    }

    private static List<FieldDto> ToDto(IReadOnlyList<TemplateField> fields) =>
        fields.Select(f => new FieldDto
        {
            Name        = f.Name,
            Type        = f.Type,
            Required    = f.Required,
            Description = f.Description,
            ItemFields  = f.ItemFields is { Count: > 0 } ? ToDto(f.ItemFields) : null,
        }).ToList();

    private static TemplateField FromDto(FieldDto d) => new()
    {
        Name        = d.Name,
        Type        = d.Type,
        Required    = d.Required,
        Description = d.Description,
        ItemFields  = d.ItemFields is { Count: > 0 }
                      ? d.ItemFields.Select(FromDto).ToList()
                      : null,
    };
}
