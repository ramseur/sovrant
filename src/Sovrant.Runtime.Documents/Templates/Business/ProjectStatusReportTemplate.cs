using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Business;

/// <summary>
/// Weekly or monthly project status report — Word document with a
/// header block (project, reporting period, owner, health), a
/// summary, progress against milestones, risks, and next period plan.
/// </summary>
public sealed class ProjectStatusReportTemplate : IDocumentTemplate
{
    public string Id => "business/project-status";
    public string Name => "Project Status Report";
    public string Industry => "business";
    public string Description => "Weekly or monthly project status with health, milestones, risks, and next steps.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "project_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "period", Type = TemplateFieldType.String, Required = true, Description = "e.g. \"Week of 2026-04-13\" or \"April 2026\"." },
        new() { Name = "project_manager", Type = TemplateFieldType.String, Required = false },
        new() { Name = "sponsor", Type = TemplateFieldType.String, Required = false },
        new() { Name = "overall_health", Type = TemplateFieldType.String, Required = true, Description = "Green / Yellow / Red." },
        new() { Name = "summary", Type = TemplateFieldType.Text, Required = true },
        new()
        {
            Name = "milestones",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "name", Type = TemplateFieldType.String, Required = true },
                new() { Name = "status", Type = TemplateFieldType.String, Required = false, Description = "e.g. On Track, At Risk, Complete." },
                new() { Name = "target_date", Type = TemplateFieldType.Date, Required = false },
                new() { Name = "notes", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new() { Name = "accomplishments", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "planned_next_period", Type = TemplateFieldType.StringArray, Required = false },
        new()
        {
            Name = "risks",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "title", Type = TemplateFieldType.String, Required = true },
                new() { Name = "severity", Type = TemplateFieldType.String, Required = false },
                new() { Name = "mitigation", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new() { Name = "asks", Type = TemplateFieldType.Text, Required = false, Description = "What the team needs from stakeholders." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var project = d.String("project_name");
        var period = d.String("period");
        var health = d.String("overall_health");

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(project);
        sb.Append("## Status Report — ").AppendLine(period);
        sb.AppendLine();
        sb.Append("**Overall health:** ").AppendLine(health);
        var pm = d.String("project_manager");
        if (!string.IsNullOrWhiteSpace(pm))
            sb.Append("**Project manager:** ").AppendLine(pm);
        var sponsor = d.String("sponsor");
        if (!string.IsNullOrWhiteSpace(sponsor))
            sb.Append("**Sponsor:** ").AppendLine(sponsor);
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine(d.String("summary"));
        sb.AppendLine();

        var milestones = d.ObjectArray("milestones");
        if (milestones.Count > 0)
        {
            sb.AppendLine("## Milestones");
            sb.AppendLine();
            sb.AppendLine("| Milestone | Status | Target date | Notes |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var m in milestones)
            {
                var name = TemplateFormat.GetString(m, "name");
                var status = TemplateFormat.GetString(m, "status");
                var target = TemplateFormat.GetDate(m, "target_date");
                var notes = TemplateFormat.GetString(m, "notes");
                sb.Append("| ").Append(TemplateFormat.EscapePipes(name))
                  .Append(" | ").Append(string.IsNullOrWhiteSpace(status) ? "—" : TemplateFormat.EscapePipes(status))
                  .Append(" | ").Append(target is not null ? TemplateFormat.FormatDate(target) : "—")
                  .Append(" | ").Append(string.IsNullOrWhiteSpace(notes) ? "—" : TemplateFormat.EscapePipes(notes))
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        var accomplishments = d.StringArray("accomplishments");
        if (accomplishments.Count > 0)
        {
            sb.AppendLine("## Accomplishments This Period");
            foreach (var a in accomplishments) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var planned = d.StringArray("planned_next_period");
        if (planned.Count > 0)
        {
            sb.AppendLine("## Planned Next Period");
            foreach (var p in planned) sb.Append("- ").AppendLine(p);
            sb.AppendLine();
        }

        var risks = d.ObjectArray("risks");
        if (risks.Count > 0)
        {
            sb.AppendLine("## Risks & Issues");
            sb.AppendLine();
            foreach (var r in risks)
            {
                var title = TemplateFormat.GetString(r, "title");
                var severity = TemplateFormat.GetString(r, "severity");
                var mitigation = TemplateFormat.GetString(r, "mitigation");
                sb.Append("- **").Append(title).Append("**");
                if (!string.IsNullOrWhiteSpace(severity)) sb.Append(" (").Append(severity).Append(')');
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(mitigation))
                    sb.Append("  *Mitigation:* ").AppendLine(mitigation);
            }
            sb.AppendLine();
        }

        var asks = d.String("asks");
        if (!string.IsNullOrWhiteSpace(asks))
        {
            sb.AppendLine("## Asks");
            sb.AppendLine(asks);
        }

        var fileName = $"status-{TemplateFormat.Slug(project)}-{TemplateFormat.Slug(period)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"{project} — Status");
    }
}
