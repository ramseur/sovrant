using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Construction;

/// <summary>
/// Safety incident report — Word document documenting a workplace
/// incident, near miss, or injury on a construction site. Contains
/// witness statements, immediate causes, corrective actions, and
/// signatures. Retain per OSHA 29 CFR 1904 recordkeeping requirements.
/// </summary>
public sealed class SafetyReportTemplate : IDocumentTemplate
{
    public string Id => "construction/safety-report";
    public string Name => "Safety Incident Report";
    public string Industry => "construction";
    public string Description => "Safety incident report with narrative, causes, corrective actions, and signatures.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "project_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "project_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "report_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "incident_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "incident_time", Type = TemplateFieldType.String, Required = false, Description = "e.g. 14:30." },
        new() { Name = "incident_type", Type = TemplateFieldType.String, Required = true, Description = "e.g. Injury, Near miss, Property damage, Environmental." },
        new() { Name = "severity", Type = TemplateFieldType.String, Required = false, Description = "e.g. First aid, Recordable, Lost time, Fatality." },
        new() { Name = "reported_by", Type = TemplateFieldType.String, Required = true },
        new() { Name = "reporter_role", Type = TemplateFieldType.String, Required = false },
        new() { Name = "location_on_site", Type = TemplateFieldType.String, Required = true },
        new() { Name = "persons_involved", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "witnesses", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "narrative", Type = TemplateFieldType.Text, Required = true, Description = "What happened, in chronological order." },
        new() { Name = "injuries", Type = TemplateFieldType.Text, Required = false, Description = "Description of any injuries and body parts affected." },
        new() { Name = "medical_treatment", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "immediate_causes", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "root_causes", Type = TemplateFieldType.StringArray, Required = false },
        new()
        {
            Name = "corrective_actions",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "action", Type = TemplateFieldType.Text, Required = true },
                new() { Name = "responsible", Type = TemplateFieldType.String, Required = false },
                new() { Name = "due_date", Type = TemplateFieldType.Date, Required = false },
            },
        },
        new() { Name = "osha_recordable", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "supervisor_name", Type = TemplateFieldType.String, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var project = d.String("project_name");
        var reportDate = d.Date("report_date");
        var incidentDate = d.Date("incident_date");
        var reporter = d.String("reported_by");

        var sb = new StringBuilder();
        sb.AppendLine("# Safety Incident Report");
        sb.AppendLine();
        sb.Append("**Project:** ").AppendLine(project);
        var addr = d.String("project_address");
        if (!string.IsNullOrWhiteSpace(addr))
        {
            sb.AppendLine("**Location:**");
            sb.AppendLine(addr);
        }
        sb.Append("**Report date:** ").AppendLine(TemplateFormat.FormatDate(reportDate));
        sb.AppendLine();

        sb.AppendLine("## Incident Summary");
        sb.Append("**Date of incident:** ").AppendLine(TemplateFormat.FormatDate(incidentDate));
        var time = d.String("incident_time");
        if (!string.IsNullOrWhiteSpace(time)) sb.Append("**Time:** ").AppendLine(time);
        sb.Append("**Type:** ").AppendLine(d.String("incident_type"));
        var severity = d.String("severity");
        if (!string.IsNullOrWhiteSpace(severity)) sb.Append("**Severity:** ").AppendLine(severity);
        sb.Append("**Location on site:** ").AppendLine(d.String("location_on_site"));
        sb.Append("**Reported by:** ").Append(reporter);
        var role = d.String("reporter_role");
        if (!string.IsNullOrWhiteSpace(role)) sb.Append(" (").Append(role).Append(')');
        sb.AppendLine();
        if (d.Has("osha_recordable"))
        {
            sb.Append("**OSHA recordable:** ").AppendLine(d.Boolean("osha_recordable") ? "Yes" : "No");
        }
        sb.AppendLine();

        var persons = d.StringArray("persons_involved");
        if (persons.Count > 0)
        {
            sb.AppendLine("## Persons Involved");
            foreach (var p in persons) sb.Append("- ").AppendLine(p);
            sb.AppendLine();
        }

        var witnesses = d.StringArray("witnesses");
        if (witnesses.Count > 0)
        {
            sb.AppendLine("## Witnesses");
            foreach (var w in witnesses) sb.Append("- ").AppendLine(w);
            sb.AppendLine();
        }

        sb.AppendLine("## Narrative");
        sb.AppendLine(d.String("narrative"));
        sb.AppendLine();

        AppendText(sb, "Injuries Sustained", d.String("injuries"));
        AppendText(sb, "Medical Treatment Provided", d.String("medical_treatment"));
        AppendList(sb, "Immediate Causes", d.StringArray("immediate_causes"));
        AppendList(sb, "Root Causes", d.StringArray("root_causes"));

        var actions = d.ObjectArray("corrective_actions");
        if (actions.Count > 0)
        {
            sb.AppendLine("## Corrective Actions");
            sb.AppendLine();
            sb.AppendLine("| # | Action | Responsible | Due |");
            sb.AppendLine("|---|--------|-------------|-----|");
            var n = 1;
            foreach (var a in actions)
            {
                var due = TemplateFormat.GetDate(a, "due_date");
                sb.Append("| ").Append(n++)
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(a, "action")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(a, "responsible")))
                  .Append(" | ").Append(due is not null ? TemplateFormat.FormatDate(due) : string.Empty)
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Signatures");
        sb.AppendLine();
        sb.Append("Reporter: ").AppendLine(reporter);
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        var supervisor = d.String("supervisor_name");
        if (!string.IsNullOrWhiteSpace(supervisor))
        {
            sb.Append("Supervisor: ").AppendLine(supervisor);
            sb.AppendLine("Signature: ____________________________  Date: ____________");
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Retain this record consistent with OSHA 29 CFR Part 1904 requirements and any applicable state OSHA rules. Do not distribute outside the investigation team without appropriate authorization.*");

        var fileName = $"safety-report-{TemplateFormat.Slug(project)}-{TemplateFormat.FormatDate(incidentDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Safety Report — {project} {TemplateFormat.FormatDate(incidentDate)}");
    }

    private static void AppendList(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        sb.Append("## ").AppendLine(heading);
        foreach (var i in items) sb.Append("- ").AppendLine(i);
        sb.AppendLine();
    }

    private static void AppendText(StringBuilder sb, string heading, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.Append("## ").AppendLine(heading);
        sb.AppendLine(content);
        sb.AppendLine();
    }
}
