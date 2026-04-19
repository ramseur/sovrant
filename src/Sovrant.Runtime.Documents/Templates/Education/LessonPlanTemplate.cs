using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Education;

/// <summary>
/// Lesson plan — Word document for a single class session, covering
/// objectives, standards alignment, materials, procedure/activities
/// with timing, differentiation, assessment, and homework.
/// </summary>
public sealed class LessonPlanTemplate : IDocumentTemplate
{
    public string Id => "education/lesson-plan";
    public string Name => "Lesson Plan";
    public string Industry => "education";
    public string Description => "Single-session lesson plan with objectives, procedure, and assessment.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "teacher_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "subject", Type = TemplateFieldType.String, Required = true },
        new() { Name = "grade_level", Type = TemplateFieldType.String, Required = false },
        new() { Name = "lesson_title", Type = TemplateFieldType.String, Required = true },
        new() { Name = "lesson_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "duration_minutes", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "objectives", Type = TemplateFieldType.StringArray, Required = true, Description = "What students will be able to do after the lesson." },
        new() { Name = "standards", Type = TemplateFieldType.StringArray, Required = false, Description = "Curriculum standards addressed." },
        new() { Name = "materials", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "prior_knowledge", Type = TemplateFieldType.Text, Required = false },
        new()
        {
            Name = "procedure",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "phase", Type = TemplateFieldType.String, Required = true, Description = "e.g. Warm-up, Direct instruction, Guided practice, Independent practice, Closure." },
                new() { Name = "minutes", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "description", Type = TemplateFieldType.Text, Required = true },
            },
        },
        new() { Name = "differentiation", Type = TemplateFieldType.Text, Required = false, Description = "Accommodations for varied learners, ELLs, IEPs." },
        new() { Name = "formative_assessment", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "summative_assessment", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "homework", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "reflection", Type = TemplateFieldType.Text, Required = false, Description = "Post-lesson reflection notes." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var teacher = d.String("teacher_name");
        var subject = d.String("subject");
        var title = d.String("lesson_title");
        var lessonDate = d.Date("lesson_date");

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(title);
        sb.Append("### ").Append(subject);
        var grade = d.String("grade_level");
        if (!string.IsNullOrWhiteSpace(grade)) sb.Append(" · ").Append(grade);
        sb.AppendLine();
        sb.AppendLine();

        sb.Append("**Teacher:** ").AppendLine(teacher);
        sb.Append("**Date:** ").AppendLine(TemplateFormat.FormatDate(lessonDate));
        var duration = d.Integer("duration_minutes");
        if (duration > 0) sb.Append("**Duration:** ").Append(duration).AppendLine(" minutes");
        sb.AppendLine();

        sb.AppendLine("## Learning Objectives");
        foreach (var o in d.StringArray("objectives")) sb.Append("- ").AppendLine(o);
        sb.AppendLine();

        var standards = d.StringArray("standards");
        if (standards.Count > 0)
        {
            sb.AppendLine("## Standards Addressed");
            foreach (var s in standards) sb.Append("- ").AppendLine(s);
            sb.AppendLine();
        }

        var materials = d.StringArray("materials");
        if (materials.Count > 0)
        {
            sb.AppendLine("## Materials");
            foreach (var m in materials) sb.Append("- ").AppendLine(m);
            sb.AppendLine();
        }

        var prior = d.String("prior_knowledge");
        if (!string.IsNullOrWhiteSpace(prior))
        {
            sb.AppendLine("## Prior Knowledge Expected");
            sb.AppendLine(prior);
            sb.AppendLine();
        }

        sb.AppendLine("## Procedure");
        sb.AppendLine();
        foreach (var step in d.ObjectArray("procedure"))
        {
            var phase = TemplateFormat.GetString(step, "phase");
            var minutes = TemplateFormat.GetInteger(step, "minutes");
            var desc = TemplateFormat.GetString(step, "description");
            sb.Append("### ").Append(phase);
            if (minutes > 0) sb.Append(" (").Append(minutes).Append(" min)");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(desc);
            sb.AppendLine();
        }

        AppendSection(sb, "Differentiation", d.String("differentiation"));
        AppendSection(sb, "Formative Assessment", d.String("formative_assessment"));
        AppendSection(sb, "Summative Assessment", d.String("summative_assessment"));
        AppendSection(sb, "Homework", d.String("homework"));
        AppendSection(sb, "Post-Lesson Reflection", d.String("reflection"));

        var fileName = $"lesson-{TemplateFormat.Slug(title)}-{TemplateFormat.FormatDate(lessonDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Lesson Plan — {title}");
    }

    private static void AppendSection(StringBuilder sb, string heading, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.Append("## ").AppendLine(heading);
        sb.AppendLine(content);
        sb.AppendLine();
    }
}
