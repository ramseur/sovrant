using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Construction;

/// <summary>
/// Punch list — Word document enumerating incomplete or deficient
/// items observed during final walkthrough, grouped by location or
/// trade, each with assignment, due date, and status.
/// </summary>
public sealed class PunchListTemplate : IDocumentTemplate
{
    public string Id => "construction/punch-list";
    public string Name => "Punch List";
    public string Industry => "construction";
    public string Description => "Punch list of incomplete or deficient items from final walkthrough.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "project_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "project_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "walkthrough_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "prepared_by", Type = TemplateFieldType.String, Required = true },
        new() { Name = "owner_representative", Type = TemplateFieldType.String, Required = false },
        new() { Name = "contractor", Type = TemplateFieldType.String, Required = false },
        new() { Name = "architect", Type = TemplateFieldType.String, Required = false },
        new()
        {
            Name = "items",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "location", Type = TemplateFieldType.String, Required = true },
                new() { Name = "trade", Type = TemplateFieldType.String, Required = false },
                new() { Name = "description", Type = TemplateFieldType.Text, Required = true },
                new() { Name = "assigned_to", Type = TemplateFieldType.String, Required = false },
                new() { Name = "due_date", Type = TemplateFieldType.Date, Required = false },
                new() { Name = "status", Type = TemplateFieldType.String, Required = false, Description = "e.g. Open, In progress, Complete, Verified." },
            },
        },
        new() { Name = "acceptance_note", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var project = d.String("project_name");
        var walkDate = d.Date("walkthrough_date");
        var preparedBy = d.String("prepared_by");

        var sb = new StringBuilder();
        sb.AppendLine("# Punch List");
        sb.AppendLine();
        sb.Append("**Project:** ").AppendLine(project);
        var addr = d.String("project_address");
        if (!string.IsNullOrWhiteSpace(addr))
        {
            sb.AppendLine("**Location:**");
            sb.AppendLine(addr);
        }
        sb.Append("**Walkthrough date:** ").AppendLine(TemplateFormat.FormatDate(walkDate));
        sb.Append("**Prepared by:** ").AppendLine(preparedBy);
        var ownerRep = d.String("owner_representative");
        if (!string.IsNullOrWhiteSpace(ownerRep)) sb.Append("**Owner representative:** ").AppendLine(ownerRep);
        var contractor = d.String("contractor");
        if (!string.IsNullOrWhiteSpace(contractor)) sb.Append("**Contractor:** ").AppendLine(contractor);
        var architect = d.String("architect");
        if (!string.IsNullOrWhiteSpace(architect)) sb.Append("**Architect:** ").AppendLine(architect);
        sb.AppendLine();

        sb.AppendLine("## Items");
        sb.AppendLine();
        sb.AppendLine("| # | Location | Trade | Description | Assigned to | Due | Status |");
        sb.AppendLine("|---|----------|-------|-------------|-------------|-----|--------|");
        var n = 1;
        foreach (var item in d.ObjectArray("items"))
        {
            var due = TemplateFormat.GetDate(item, "due_date");
            sb.Append("| ").Append(n++)
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(item, "location")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(item, "trade")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(item, "description")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(item, "assigned_to")))
              .Append(" | ").Append(due is not null ? TemplateFormat.FormatDate(due) : string.Empty)
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(item, "status")))
              .AppendLine(" |");
        }
        sb.AppendLine();

        var accept = d.String("acceptance_note");
        if (!string.IsNullOrWhiteSpace(accept))
        {
            sb.AppendLine("## Acceptance");
            sb.AppendLine(accept);
            sb.AppendLine();
        }

        sb.AppendLine("Owner representative: ____________________________  Date: ____________");
        sb.AppendLine("Contractor: ____________________________  Date: ____________");

        var fileName = $"punch-list-{TemplateFormat.Slug(project)}-{TemplateFormat.FormatDate(walkDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Punch List — {project}");
    }
}
