namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Types a template expects. Used both at render time (to coerce/validate
/// incoming JSON) and at discovery time so agents can see what data the
/// template needs before calling it.
/// </summary>
public enum TemplateFieldType
{
    String,
    Text,
    Integer,
    Decimal,
    Currency,
    Date,
    Boolean,
    StringArray,
    ObjectArray,
}

/// <summary>
/// One declared field on a template. The list of fields on a template
/// doubles as machine-readable documentation so the agent can ask the
/// user for missing required data instead of guessing.
/// </summary>
public sealed record TemplateField
{
    public required string Name { get; init; }
    public required TemplateFieldType Type { get; init; }
    public bool Required { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// For <see cref="TemplateFieldType.ObjectArray"/> only — describes
    /// the shape of each object so nested data can be validated.
    /// </summary>
    public IReadOnlyList<TemplateField>? ItemFields { get; init; }
}
