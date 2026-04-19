using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Education;

/// <summary>
/// Academic transcript — StructuredPdf document summarizing a
/// student's cumulative record: courses taken by term with grades and
/// credits earned, cumulative GPA, and the registrar's seal line.
/// Typically issued under FERPA protections.
/// </summary>
public sealed class TranscriptTemplate : IDocumentTemplate
{
    public string Id => "education/transcript";
    public string Name => "Academic Transcript";
    public string Industry => "education";
    public string Description => "Academic transcript with per-term coursework, credits, grades, and cumulative GPA.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "institution_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "institution_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "student_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "student_id", Type = TemplateFieldType.String, Required = false },
        new() { Name = "date_of_birth", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "program", Type = TemplateFieldType.String, Required = false, Description = "e.g. B.S. Computer Science." },
        new() { Name = "issue_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "official", Type = TemplateFieldType.Boolean, Required = false, Description = "Mark as Official (registrar seal) vs Unofficial." },
        new()
        {
            Name = "terms",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "term", Type = TemplateFieldType.String, Required = true, Description = "e.g. Fall 2025." },
                new() { Name = "courses", Type = TemplateFieldType.ObjectArray, Required = true, ItemFields = new List<TemplateField>
                {
                    new() { Name = "course_code", Type = TemplateFieldType.String, Required = true },
                    new() { Name = "course_title", Type = TemplateFieldType.String, Required = true },
                    new() { Name = "credits", Type = TemplateFieldType.Decimal, Required = true },
                    new() { Name = "grade", Type = TemplateFieldType.String, Required = true },
                    new() { Name = "grade_points", Type = TemplateFieldType.Decimal, Required = false, Description = "Quality points earned on this course's GPA scale." },
                } },
                new() { Name = "term_gpa", Type = TemplateFieldType.Decimal, Required = false },
                new() { Name = "term_credits", Type = TemplateFieldType.Decimal, Required = false },
            },
        },
        new() { Name = "cumulative_gpa", Type = TemplateFieldType.Decimal, Required = false },
        new() { Name = "cumulative_credits", Type = TemplateFieldType.Decimal, Required = false },
        new() { Name = "degree_awarded", Type = TemplateFieldType.String, Required = false },
        new() { Name = "degree_date", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "honors", Type = TemplateFieldType.String, Required = false, Description = "e.g. Cum Laude." },
        new() { Name = "registrar_name", Type = TemplateFieldType.String, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var institution = d.String("institution_name");
        var student = d.String("student_name");
        var issueDate = d.Date("issue_date");
        var official = d.Boolean("official");

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(institution);
        var addr = d.String("institution_address");
        if (!string.IsNullOrWhiteSpace(addr)) sb.AppendLine(addr);
        sb.AppendLine();
        sb.Append("## ").Append(official ? "Official" : "Unofficial").AppendLine(" Academic Transcript");
        sb.AppendLine();

        sb.AppendLine("## Student Information");
        sb.Append("**Name:** ").AppendLine(student);
        var studentId = d.String("student_id");
        if (!string.IsNullOrWhiteSpace(studentId)) sb.Append("**Student ID:** ").AppendLine(studentId);
        var dob = d.Date("date_of_birth");
        if (dob is not null) sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        var program = d.String("program");
        if (!string.IsNullOrWhiteSpace(program)) sb.Append("**Program:** ").AppendLine(program);
        sb.Append("**Issue date:** ").AppendLine(TemplateFormat.FormatDate(issueDate));
        sb.AppendLine();

        decimal totalCredits = 0m;
        decimal totalPoints = 0m;
        foreach (var termObj in d.ObjectArray("terms"))
        {
            var termName = TemplateFormat.GetString(termObj, "term");
            sb.Append("## ").AppendLine(termName);
            sb.AppendLine();
            sb.AppendLine("| Course | Title | Credits | Grade |");
            sb.AppendLine("|--------|-------|--------:|:-----:|");
            decimal termCredits = 0m;
            decimal termPoints = 0m;
            if (termObj.ValueKind == JsonValueKind.Object && termObj.TryGetProperty("courses", out var coursesEl) && coursesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in coursesEl.EnumerateArray())
                {
                    if (c.ValueKind != JsonValueKind.Object) continue;
                    var code = TemplateFormat.GetString(c, "course_code");
                    var title = TemplateFormat.GetString(c, "course_title");
                    var credits = TemplateFormat.GetDecimal(c, "credits");
                    var grade = TemplateFormat.GetString(c, "grade");
                    var points = TemplateFormat.GetDecimal(c, "grade_points");
                    termCredits += credits;
                    termPoints += points;
                    sb.Append("| ").Append(TemplateFormat.EscapePipes(code))
                      .Append(" | ").Append(TemplateFormat.EscapePipes(title))
                      .Append(" | ").Append(TemplateFormat.FormatNumber(credits))
                      .Append(" | ").Append(TemplateFormat.EscapePipes(grade))
                      .AppendLine(" |");
                }
            }
            totalCredits += termCredits;
            totalPoints += termPoints;

            var termGpa = TemplateFormat.GetDecimal(termObj, "term_gpa");
            var explicitTermCredits = TemplateFormat.GetDecimal(termObj, "term_credits");
            var shownTermCredits = explicitTermCredits > 0 ? explicitTermCredits : termCredits;
            sb.AppendLine();
            sb.Append("**Term credits:** ").Append(TemplateFormat.FormatNumber(shownTermCredits));
            if (termGpa > 0) sb.Append("  ·  **Term GPA:** ").Append(TemplateFormat.FormatNumber(termGpa));
            sb.AppendLine();
            sb.AppendLine();
        }

        sb.AppendLine("## Cumulative Summary");
        sb.AppendLine();
        var cumCredits = d.Decimal("cumulative_credits");
        var shownCumCredits = cumCredits > 0 ? cumCredits : totalCredits;
        sb.Append("- **Cumulative credits earned:** ").AppendLine(TemplateFormat.FormatNumber(shownCumCredits));
        var cumGpa = d.Decimal("cumulative_gpa");
        if (cumGpa > 0)
        {
            sb.Append("- **Cumulative GPA:** ").AppendLine(TemplateFormat.FormatNumber(cumGpa));
        }
        else if (totalCredits > 0 && totalPoints > 0)
        {
            var computedGpa = Math.Round(totalPoints / totalCredits, 2, MidpointRounding.AwayFromZero);
            sb.Append("- **Cumulative GPA (computed):** ").AppendLine(TemplateFormat.FormatNumber(computedGpa));
        }
        sb.AppendLine();

        var degree = d.String("degree_awarded");
        if (!string.IsNullOrWhiteSpace(degree))
        {
            sb.AppendLine("## Degree Awarded");
            sb.AppendLine(degree);
            var degreeDate = d.Date("degree_date");
            if (degreeDate is not null) sb.Append("**Conferred:** ").AppendLine(TemplateFormat.FormatDate(degreeDate));
            var honors = d.String("honors");
            if (!string.IsNullOrWhiteSpace(honors)) sb.Append("**Honors:** ").AppendLine(honors);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        var registrar = d.String("registrar_name");
        if (!string.IsNullOrWhiteSpace(registrar))
        {
            sb.Append("Registrar: ").AppendLine(registrar);
        }
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.AppendLine("*This transcript is released subject to the Family Educational Rights and Privacy Act (FERPA). Unofficial transcripts are not valid for admissions or employment verification.*");

        var fileName = $"transcript-{TemplateFormat.Slug(student)}-{TemplateFormat.FormatDate(issueDate)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"Transcript — {student}");
    }
}
