using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Healthcare;

/// <summary>
/// Care plan — Word document capturing problems/diagnoses, goals,
/// interventions, responsible team members, and follow-up. Contains
/// PHI; must be reviewed and signed by a licensed clinician before use.
/// </summary>
public sealed class CarePlanTemplate : IDocumentTemplate
{
    public string Id => "healthcare/care-plan";
    public string Name => "Care Plan";
    public string Industry => "healthcare";
    public string Description => "Care plan with problems, goals, interventions, and responsible team members.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "patient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "mrn", Type = TemplateFieldType.String, Required = false, Description = "Medical record number." },
        new() { Name = "plan_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "author_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "author_role", Type = TemplateFieldType.String, Required = false },
        new() { Name = "care_setting", Type = TemplateFieldType.String, Required = false, Description = "e.g. inpatient, outpatient, home health." },
        new() { Name = "diagnoses", Type = TemplateFieldType.StringArray, Required = true },
        new()
        {
            Name = "problems",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "problem", Type = TemplateFieldType.String, Required = true },
                new() { Name = "goal", Type = TemplateFieldType.Text, Required = false },
                new() { Name = "interventions", Type = TemplateFieldType.StringArray, Required = false },
                new() { Name = "responsible", Type = TemplateFieldType.String, Required = false },
                new() { Name = "target_date", Type = TemplateFieldType.Date, Required = false },
            },
        },
        new() { Name = "patient_preferences", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "follow_up", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var patient = d.String("patient_name");
        var planDate = d.Date("plan_date");
        var author = d.String("author_name");

        var sb = new StringBuilder();
        sb.AppendLine("# Care Plan");
        sb.AppendLine();
        sb.Append("**Patient:** ").AppendLine(patient);
        var mrn = d.String("mrn");
        if (!string.IsNullOrWhiteSpace(mrn)) sb.Append("**MRN:** ").AppendLine(mrn);
        sb.Append("**Plan date:** ").AppendLine(TemplateFormat.FormatDate(planDate));
        sb.Append("**Author:** ").AppendLine(author);
        var authorRole = d.String("author_role");
        if (!string.IsNullOrWhiteSpace(authorRole)) sb.Append("**Role:** ").AppendLine(authorRole);
        var setting = d.String("care_setting");
        if (!string.IsNullOrWhiteSpace(setting)) sb.Append("**Care setting:** ").AppendLine(setting);
        sb.AppendLine();

        sb.AppendLine("## Diagnoses");
        foreach (var dx in d.StringArray("diagnoses")) sb.Append("- ").AppendLine(dx);
        sb.AppendLine();

        sb.AppendLine("## Problems, Goals, and Interventions");
        sb.AppendLine();
        var n = 1;
        foreach (var p in d.ObjectArray("problems"))
        {
            var problem = TemplateFormat.GetString(p, "problem");
            var goal = TemplateFormat.GetString(p, "goal");
            var responsible = TemplateFormat.GetString(p, "responsible");
            var target = TemplateFormat.GetDate(p, "target_date");

            sb.Append("### ").Append(n++).Append(". ").AppendLine(problem);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(goal)) { sb.Append("**Goal:** ").AppendLine(goal); }
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("interventions", out var intv) && intv.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Interventions:**");
                foreach (var i in intv.EnumerateArray())
                    if (i.ValueKind == JsonValueKind.String)
                        sb.Append("- ").AppendLine(i.GetString() ?? string.Empty);
            }
            if (!string.IsNullOrWhiteSpace(responsible))
                sb.Append("**Responsible:** ").AppendLine(responsible);
            if (target is not null)
                sb.Append("**Target date:** ").AppendLine(TemplateFormat.FormatDate(target));
            sb.AppendLine();
        }

        var preferences = d.String("patient_preferences");
        if (!string.IsNullOrWhiteSpace(preferences))
        {
            sb.AppendLine("## Patient Preferences");
            sb.AppendLine(preferences);
            sb.AppendLine();
        }

        var followUp = d.String("follow_up");
        if (!string.IsNullOrWhiteSpace(followUp))
        {
            sb.AppendLine("## Follow-Up");
            sb.AppendLine(followUp);
            sb.AppendLine();
        }

        sb.AppendLine("## Sign-Off");
        sb.AppendLine();
        sb.Append(author).AppendLine();
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This care plan contains Protected Health Information (PHI) and must be reviewed and signed by a licensed clinician before being entered into the medical record. AI-generated drafts are not a substitute for clinical judgment.*");

        var fileName = $"care-plan-{TemplateFormat.Slug(patient)}-{TemplateFormat.FormatDate(planDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Care Plan — {patient}");
    }
}
