using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Education;

/// <summary>
/// Individualized Education Program (IEP) — Word document required
/// under the Individuals with Disabilities Education Act (IDEA) for
/// students receiving special-education services. Captures present
/// levels, annual goals, services, accommodations, and team members.
/// Contains Personally Identifiable Information (PII) about a minor
/// and must be handled under FERPA and IDEA §300.622.
/// </summary>
public sealed class IepTemplate : IDocumentTemplate
{
    public string Id => "education/iep";
    public string Name => "Individualized Education Program (IEP)";
    public string Industry => "education";
    public string Description => "IEP with present levels, annual goals, services, and accommodations.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "district_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "school_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "student_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "date_of_birth", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "student_id", Type = TemplateFieldType.String, Required = false },
        new() { Name = "grade_level", Type = TemplateFieldType.String, Required = true },
        new() { Name = "primary_disability", Type = TemplateFieldType.String, Required = true, Description = "IDEA eligibility category." },
        new() { Name = "secondary_disability", Type = TemplateFieldType.String, Required = false },
        new() { Name = "iep_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "next_review_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "case_manager", Type = TemplateFieldType.String, Required = true },
        new() { Name = "present_levels_academic", Type = TemplateFieldType.Text, Required = true, Description = "Present Levels of Academic Achievement (PLAA)." },
        new() { Name = "present_levels_functional", Type = TemplateFieldType.Text, Required = true, Description = "Present Levels of Functional Performance (PLFP)." },
        new()
        {
            Name = "annual_goals",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "area", Type = TemplateFieldType.String, Required = true, Description = "e.g. Reading, Math, Behavior, Speech." },
                new() { Name = "goal", Type = TemplateFieldType.Text, Required = true, Description = "Measurable annual goal." },
                new() { Name = "measurement", Type = TemplateFieldType.Text, Required = false, Description = "How progress is measured." },
                new() { Name = "objectives", Type = TemplateFieldType.StringArray, Required = false, Description = "Short-term objectives / benchmarks." },
            },
        },
        new()
        {
            Name = "services",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "service", Type = TemplateFieldType.String, Required = true },
                new() { Name = "provider", Type = TemplateFieldType.String, Required = false },
                new() { Name = "frequency", Type = TemplateFieldType.String, Required = false },
                new() { Name = "location", Type = TemplateFieldType.String, Required = false },
                new() { Name = "start_date", Type = TemplateFieldType.Date, Required = false },
                new() { Name = "end_date", Type = TemplateFieldType.Date, Required = false },
            },
        },
        new() { Name = "accommodations", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "modifications", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "assessment_accommodations", Type = TemplateFieldType.Text, Required = false, Description = "State and district testing accommodations." },
        new() { Name = "least_restrictive_environment", Type = TemplateFieldType.Text, Required = false, Description = "LRE statement and rationale for any removal from general education." },
        new() { Name = "transition_plan", Type = TemplateFieldType.Text, Required = false, Description = "Required for students age 16+ under IDEA." },
        new()
        {
            Name = "team_members",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "name", Type = TemplateFieldType.String, Required = true },
                new() { Name = "role", Type = TemplateFieldType.String, Required = true },
            },
        },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var district = d.String("district_name");
        var school = d.String("school_name");
        var student = d.String("student_name");
        var dob = d.Date("date_of_birth");
        var iepDate = d.Date("iep_date");

        var sb = new StringBuilder();
        sb.AppendLine("# Individualized Education Program (IEP)");
        sb.Append("### ").Append(district).Append(" · ").AppendLine(school);
        sb.AppendLine();

        sb.AppendLine("## Student Information");
        sb.Append("**Name:** ").AppendLine(student);
        sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        var studentId = d.String("student_id");
        if (!string.IsNullOrWhiteSpace(studentId)) sb.Append("**Student ID:** ").AppendLine(studentId);
        sb.Append("**Grade:** ").AppendLine(d.String("grade_level"));
        sb.Append("**Primary disability:** ").AppendLine(d.String("primary_disability"));
        var secondary = d.String("secondary_disability");
        if (!string.IsNullOrWhiteSpace(secondary)) sb.Append("**Secondary disability:** ").AppendLine(secondary);
        sb.Append("**IEP date:** ").AppendLine(TemplateFormat.FormatDate(iepDate));
        sb.Append("**Next review date:** ").AppendLine(TemplateFormat.FormatDate(d.Date("next_review_date")));
        sb.Append("**Case manager:** ").AppendLine(d.String("case_manager"));
        sb.AppendLine();

        sb.AppendLine("## Present Levels of Academic Achievement");
        sb.AppendLine(d.String("present_levels_academic"));
        sb.AppendLine();

        sb.AppendLine("## Present Levels of Functional Performance");
        sb.AppendLine(d.String("present_levels_functional"));
        sb.AppendLine();

        sb.AppendLine("## Annual Goals");
        sb.AppendLine();
        var goalNum = 1;
        foreach (var g in d.ObjectArray("annual_goals"))
        {
            var area = TemplateFormat.GetString(g, "area");
            var goal = TemplateFormat.GetString(g, "goal");
            var measurement = TemplateFormat.GetString(g, "measurement");
            sb.Append("### Goal ").Append(goalNum++).Append(" — ").AppendLine(area);
            sb.AppendLine();
            sb.AppendLine(goal);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(measurement))
            {
                sb.Append("**Measurement:** ").AppendLine(measurement);
            }
            if (g.ValueKind == JsonValueKind.Object && g.TryGetProperty("objectives", out var objs) && objs.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Short-term objectives:**");
                foreach (var o in objs.EnumerateArray())
                    if (o.ValueKind == JsonValueKind.String)
                        sb.Append("- ").AppendLine(o.GetString() ?? string.Empty);
            }
            sb.AppendLine();
        }

        var services = d.ObjectArray("services");
        if (services.Count > 0)
        {
            sb.AppendLine("## Special Education and Related Services");
            sb.AppendLine();
            sb.AppendLine("| Service | Provider | Frequency | Location | Start | End |");
            sb.AppendLine("|---------|----------|-----------|----------|-------|-----|");
            foreach (var s in services)
            {
                var start = TemplateFormat.GetDate(s, "start_date");
                var end = TemplateFormat.GetDate(s, "end_date");
                sb.Append("| ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "service")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "provider")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "frequency")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "location")))
                  .Append(" | ").Append(start is not null ? TemplateFormat.FormatDate(start) : string.Empty)
                  .Append(" | ").Append(end is not null ? TemplateFormat.FormatDate(end) : string.Empty)
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        var accommodations = d.StringArray("accommodations");
        if (accommodations.Count > 0)
        {
            sb.AppendLine("## Accommodations");
            foreach (var a in accommodations) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var modifications = d.StringArray("modifications");
        if (modifications.Count > 0)
        {
            sb.AppendLine("## Modifications");
            foreach (var m in modifications) sb.Append("- ").AppendLine(m);
            sb.AppendLine();
        }

        AppendSection(sb, "Assessment Accommodations", d.String("assessment_accommodations"));
        AppendSection(sb, "Least Restrictive Environment (LRE)", d.String("least_restrictive_environment"));
        AppendSection(sb, "Transition Plan", d.String("transition_plan"));

        sb.AppendLine("## IEP Team");
        sb.AppendLine();
        sb.AppendLine("| Name | Role | Signature | Date |");
        sb.AppendLine("|------|------|-----------|------|");
        foreach (var m in d.ObjectArray("team_members"))
        {
            sb.Append("| ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(m, "name")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(m, "role")))
              .AppendLine(" | | |");
        }
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This IEP contains Personally Identifiable Information about a minor and is subject to FERPA (20 U.S.C. §1232g) and IDEA (34 CFR §300.622). AI-generated drafts must be reviewed by the IEP team and cannot replace the collaborative development process required under IDEA.*");

        var fileName = $"iep-{TemplateFormat.Slug(student)}-{TemplateFormat.FormatDate(iepDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"IEP — {student}");
    }

    private static void AppendSection(StringBuilder sb, string heading, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.Append("## ").AppendLine(heading);
        sb.AppendLine(content);
        sb.AppendLine();
    }
}
