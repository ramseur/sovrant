using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.RealEstate;

/// <summary>
/// Property inspection report — structured PDF capturing inspector
/// observations room-by-room or system-by-system, with a severity
/// rating per finding and a summary of recommended repairs.
/// </summary>
public sealed class PropertyInspectionTemplate : IDocumentTemplate
{
    public string Id => "real-estate/property-inspection";
    public string Name => "Property Inspection Report";
    public string Industry => "real-estate";
    public string Description => "Property inspection report with findings per system and severity.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "property_address", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "client_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "inspector_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "inspector_license", Type = TemplateFieldType.String, Required = false },
        new() { Name = "inspection_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "weather", Type = TemplateFieldType.String, Required = false },
        new() { Name = "overall_condition", Type = TemplateFieldType.String, Required = false, Description = "e.g. Good, Fair, Poor." },
        new()
        {
            Name = "sections",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            Description = "One entry per system or area (roof, HVAC, electrical, kitchen, etc.).",
            ItemFields = new List<TemplateField>
            {
                new() { Name = "name", Type = TemplateFieldType.String, Required = true },
                new() { Name = "condition", Type = TemplateFieldType.String, Required = false },
                new()
                {
                    Name = "findings",
                    Type = TemplateFieldType.ObjectArray,
                    Required = false,
                    ItemFields = new List<TemplateField>
                    {
                        new() { Name = "description", Type = TemplateFieldType.Text, Required = true },
                        new() { Name = "severity", Type = TemplateFieldType.String, Required = false, Description = "e.g. Safety, Major, Minor, Maintenance." },
                        new() { Name = "recommendation", Type = TemplateFieldType.Text, Required = false },
                    },
                },
            },
        },
        new() { Name = "summary_recommendations", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var address = d.String("property_address");
        var client = d.String("client_name");
        var inspector = d.String("inspector_name");
        var inspectionDate = d.Date("inspection_date");

        var sb = new StringBuilder();
        sb.AppendLine("# Property Inspection Report");
        sb.AppendLine();
        sb.AppendLine("**Property:**");
        sb.AppendLine(address);
        sb.AppendLine();
        sb.Append("**Client:** ").AppendLine(client);
        sb.Append("**Inspector:** ").AppendLine(inspector);
        var license = d.String("inspector_license");
        if (!string.IsNullOrWhiteSpace(license)) sb.Append("**License #:** ").AppendLine(license);
        sb.Append("**Date:** ").AppendLine(TemplateFormat.FormatDate(inspectionDate));
        var weather = d.String("weather");
        if (!string.IsNullOrWhiteSpace(weather)) sb.Append("**Weather:** ").AppendLine(weather);
        var overall = d.String("overall_condition");
        if (!string.IsNullOrWhiteSpace(overall)) sb.Append("**Overall condition:** ").AppendLine(overall);
        sb.AppendLine();

        foreach (var section in d.ObjectArray("sections"))
        {
            var name = TemplateFormat.GetString(section, "name");
            var condition = TemplateFormat.GetString(section, "condition");
            sb.Append("## ").AppendLine(name);
            if (!string.IsNullOrWhiteSpace(condition))
                sb.Append("*Condition:* ").AppendLine(condition);
            sb.AppendLine();

            if (section.ValueKind == JsonValueKind.Object && section.TryGetProperty("findings", out var findings) && findings.ValueKind == JsonValueKind.Array)
            {
                var any = false;
                foreach (var finding in findings.EnumerateArray())
                {
                    any = true;
                    var desc = TemplateFormat.GetString(finding, "description");
                    var severity = TemplateFormat.GetString(finding, "severity");
                    var rec = TemplateFormat.GetString(finding, "recommendation");
                    sb.Append("- ");
                    if (!string.IsNullOrWhiteSpace(severity)) sb.Append("**[").Append(severity).Append("]** ");
                    sb.AppendLine(desc);
                    if (!string.IsNullOrWhiteSpace(rec))
                        sb.Append("  *Recommendation:* ").AppendLine(rec);
                }
                if (!any) sb.AppendLine("No items noted.");
            }
            sb.AppendLine();
        }

        var recommendations = d.String("summary_recommendations");
        if (!string.IsNullOrWhiteSpace(recommendations))
        {
            sb.AppendLine("## Summary and Recommendations");
            sb.AppendLine(recommendations);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This report reflects the inspector's observations on the date of inspection and is not a warranty or guarantee of the property's condition.*");

        var fileName = $"inspection-{TemplateFormat.Slug(client)}-{TemplateFormat.FormatDate(inspectionDate)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"Inspection — {address.Split('\n')[0]}");
    }
}
