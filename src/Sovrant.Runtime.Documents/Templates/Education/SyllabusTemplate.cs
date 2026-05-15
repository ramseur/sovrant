using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Education;

/// <summary>
/// Course syllabus — Word document describing a course's objectives,
/// schedule, grading policy, required materials, and contact
/// information. Intended for distribution to students at the start of
/// a term.
/// </summary>
public sealed class SyllabusTemplate : IDocumentTemplate
{
    public string Id => "education/syllabus";
    public string Name => "Course Syllabus";
    public string Industry => "education";
    public string Description => "Course syllabus with objectives, schedule, grading, and policies.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "institution_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "course_code", Type = TemplateFieldType.String, Required = true },
        new() { Name = "course_title", Type = TemplateFieldType.String, Required = true },
        new() { Name = "term", Type = TemplateFieldType.String, Required = true, Description = "e.g. Fall 2026." },
        new() { Name = "credits", Type = TemplateFieldType.String, Required = false },
        new() { Name = "instructor_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "instructor_email", Type = TemplateFieldType.String, Required = false },
        new() { Name = "office_location", Type = TemplateFieldType.String, Required = false },
        new() { Name = "office_hours", Type = TemplateFieldType.String, Required = false },
        new() { Name = "meeting_schedule", Type = TemplateFieldType.String, Required = false, Description = "e.g. MWF 10:00-10:50, Room 201." },
        new() { Name = "course_description", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "learning_objectives", Type = TemplateFieldType.StringArray, Required = true },
        new() { Name = "required_materials", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "prerequisites", Type = TemplateFieldType.Text, Required = false },
        new()
        {
            Name = "grading_breakdown",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "category", Type = TemplateFieldType.String, Required = true },
                new() { Name = "weight_percent", Type = TemplateFieldType.Decimal, Required = true },
                new() { Name = "description", Type = TemplateFieldType.String, Required = false },
            },
        },
        new()
        {
            Name = "schedule",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "week", Type = TemplateFieldType.String, Required = true },
                new() { Name = "topic", Type = TemplateFieldType.String, Required = true },
                new() { Name = "readings", Type = TemplateFieldType.String, Required = false },
                new() { Name = "assignments", Type = TemplateFieldType.String, Required = false },
            },
        },
        new() { Name = "attendance_policy", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "academic_integrity_policy", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "accessibility_statement", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var institution = d.String("institution_name");
        var courseCode = d.String("course_code");
        var courseTitle = d.String("course_title");
        var term = d.String("term");
        var instructor = d.String("instructor_name");

        var sb = new StringBuilder();
        sb.Append("# ").Append(courseCode).Append(" — ").AppendLine(courseTitle);
        sb.Append("## ").Append(institution).Append(" · ").AppendLine(term);
        var credits = d.String("credits");
        if (!string.IsNullOrWhiteSpace(credits)) sb.Append("**Credits:** ").AppendLine(credits);
        sb.AppendLine();

        sb.AppendLine("## Instructor");
        sb.AppendLine(instructor);
        var email = d.String("instructor_email");
        if (!string.IsNullOrWhiteSpace(email)) sb.Append("Email: ").AppendLine(email);
        var office = d.String("office_location");
        if (!string.IsNullOrWhiteSpace(office)) sb.Append("Office: ").AppendLine(office);
        var hours = d.String("office_hours");
        if (!string.IsNullOrWhiteSpace(hours)) sb.Append("Office hours: ").AppendLine(hours);
        var meeting = d.String("meeting_schedule");
        if (!string.IsNullOrWhiteSpace(meeting)) sb.Append("Meetings: ").AppendLine(meeting);
        sb.AppendLine();

        sb.AppendLine("## Course Description");
        sb.AppendLine(d.String("course_description"));
        sb.AppendLine();

        var prereqs = d.String("prerequisites");
        if (!string.IsNullOrWhiteSpace(prereqs))
        {
            sb.AppendLine("## Prerequisites");
            sb.AppendLine(prereqs);
            sb.AppendLine();
        }

        sb.AppendLine("## Learning Objectives");
        sb.AppendLine("By the end of this course, students will be able to:");
        foreach (var obj in d.StringArray("learning_objectives")) sb.Append("- ").AppendLine(obj);
        sb.AppendLine();

        var materials = d.StringArray("required_materials");
        if (materials.Count > 0)
        {
            sb.AppendLine("## Required Materials");
            foreach (var m in materials) sb.Append("- ").AppendLine(m);
            sb.AppendLine();
        }

        sb.AppendLine("## Grading");
        sb.AppendLine();
        sb.AppendLine("| Category | Weight | Description |");
        sb.AppendLine("|----------|-------:|-------------|");
        decimal totalWeight = 0m;
        foreach (var g in d.ObjectArray("grading_breakdown"))
        {
            var cat = TemplateFormat.GetString(g, "category");
            var weight = TemplateFormat.GetDecimal(g, "weight_percent");
            var desc = TemplateFormat.GetString(g, "description");
            totalWeight += weight;
            sb.Append("| ").Append(TemplateFormat.EscapePipes(cat))
              .Append(" | ").Append(TemplateFormat.FormatPercent(weight))
              .Append(" | ").Append(TemplateFormat.EscapePipes(desc))
              .AppendLine(" |");
        }
        sb.Append("| **Total** | **").Append(TemplateFormat.FormatPercent(totalWeight)).AppendLine("** | |");
        sb.AppendLine();

        var schedule = d.ObjectArray("schedule");
        if (schedule.Count > 0)
        {
            sb.AppendLine("## Schedule");
            sb.AppendLine();
            sb.AppendLine("| Week | Topic | Readings | Assignments |");
            sb.AppendLine("|------|-------|----------|-------------|");
            foreach (var w in schedule)
            {
                sb.Append("| ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(w, "week")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(w, "topic")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(w, "readings")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(w, "assignments")))
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        AppendSection(sb, "Attendance Policy", d.String("attendance_policy"));
        AppendSection(sb, "Academic Integrity", d.String("academic_integrity_policy"));
        AppendSection(sb, "Accessibility", d.String("accessibility_statement"));

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This syllabus may be updated during the term. Students will be notified of any changes.*");

        var fileName = $"syllabus-{TemplateFormat.Slug(courseCode)}-{TemplateFormat.Slug(term)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Syllabus — {courseCode} {term}");
    }

    private static void AppendSection(StringBuilder sb, string heading, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.Append("## ").AppendLine(heading);
        sb.AppendLine(content);
        sb.AppendLine();
    }
}
