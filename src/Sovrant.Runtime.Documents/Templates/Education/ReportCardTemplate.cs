using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Education;

/// <summary>
/// Report card — Word document summarizing a student's performance
/// across subjects for a term, with letter or numeric grades,
/// attendance, conduct, and teacher comments.
/// </summary>
public sealed class ReportCardTemplate : IDocumentTemplate
{
    public string Id => "education/report-card";
    public string Name => "Report Card";
    public string Industry => "education";
    public string Description => "Student report card with per-subject grades, attendance, and comments.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "school_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "district_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "student_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "student_id", Type = TemplateFieldType.String, Required = false },
        new() { Name = "grade_level", Type = TemplateFieldType.String, Required = true },
        new() { Name = "homeroom_teacher", Type = TemplateFieldType.String, Required = false },
        new() { Name = "term", Type = TemplateFieldType.String, Required = true, Description = "e.g. Q2 2026, Semester 1." },
        new() { Name = "report_date", Type = TemplateFieldType.Date, Required = true },
        new()
        {
            Name = "subjects",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "subject", Type = TemplateFieldType.String, Required = true },
                new() { Name = "teacher", Type = TemplateFieldType.String, Required = false },
                new() { Name = "grade", Type = TemplateFieldType.String, Required = true, Description = "Letter or numeric grade." },
                new() { Name = "effort", Type = TemplateFieldType.String, Required = false, Description = "e.g. E/S/N." },
                new() { Name = "comments", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new() { Name = "days_present", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "days_absent", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "days_tardy", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "conduct", Type = TemplateFieldType.String, Required = false },
        new() { Name = "overall_comments", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "promotion_status", Type = TemplateFieldType.String, Required = false, Description = "e.g. Promoted, Retained, In progress." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var school = d.String("school_name");
        var student = d.String("student_name");
        var term = d.String("term");
        var reportDate = d.Date("report_date");

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(school);
        var district = d.String("district_name");
        if (!string.IsNullOrWhiteSpace(district)) sb.Append("### ").AppendLine(district);
        sb.AppendLine("## Report Card");
        sb.AppendLine();

        sb.Append("**Student:** ").AppendLine(student);
        var studentId = d.String("student_id");
        if (!string.IsNullOrWhiteSpace(studentId)) sb.Append("**Student ID:** ").AppendLine(studentId);
        sb.Append("**Grade:** ").AppendLine(d.String("grade_level"));
        var homeroom = d.String("homeroom_teacher");
        if (!string.IsNullOrWhiteSpace(homeroom)) sb.Append("**Homeroom:** ").AppendLine(homeroom);
        sb.Append("**Term:** ").AppendLine(term);
        sb.Append("**Report date:** ").AppendLine(TemplateFormat.FormatDate(reportDate));
        sb.AppendLine();

        sb.AppendLine("## Academic Performance");
        sb.AppendLine();
        sb.AppendLine("| Subject | Teacher | Grade | Effort | Comments |");
        sb.AppendLine("|---------|---------|:-----:|:------:|----------|");
        foreach (var s in d.ObjectArray("subjects"))
        {
            sb.Append("| ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "subject")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "teacher")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "grade")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "effort")))
              .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(s, "comments")))
              .AppendLine(" |");
        }
        sb.AppendLine();

        var present = d.Integer("days_present");
        var absent = d.Integer("days_absent");
        var tardy = d.Integer("days_tardy");
        if (present > 0 || absent > 0 || tardy > 0)
        {
            sb.AppendLine("## Attendance");
            if (present > 0) sb.Append("- Days present: ").AppendLine(present.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (absent > 0) sb.Append("- Days absent: ").AppendLine(absent.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (tardy > 0) sb.Append("- Days tardy: ").AppendLine(tardy.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        var conduct = d.String("conduct");
        if (!string.IsNullOrWhiteSpace(conduct))
        {
            sb.AppendLine("## Conduct");
            sb.AppendLine(conduct);
            sb.AppendLine();
        }

        var comments = d.String("overall_comments");
        if (!string.IsNullOrWhiteSpace(comments))
        {
            sb.AppendLine("## Overall Comments");
            sb.AppendLine(comments);
            sb.AppendLine();
        }

        var promotion = d.String("promotion_status");
        if (!string.IsNullOrWhiteSpace(promotion))
        {
            sb.Append("**Promotion status:** ").AppendLine(promotion);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Parent / guardian signature: ____________________________  Date: ____________");

        var fileName = $"report-card-{TemplateFormat.Slug(student)}-{TemplateFormat.Slug(term)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Report Card — {student} {term}");
    }
}
