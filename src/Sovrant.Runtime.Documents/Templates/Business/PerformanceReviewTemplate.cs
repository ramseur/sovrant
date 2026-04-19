using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Business;

/// <summary>
/// Annual or quarterly performance review — Word document with
/// employee details, competency ratings, accomplishments, areas for
/// development, goals for the next period, and a sign-off block for
/// both reviewer and employee.
/// </summary>
public sealed class PerformanceReviewTemplate : IDocumentTemplate
{
    public string Id => "business/performance-review";
    public string Name => "Performance Review";
    public string Industry => "business";
    public string Description => "Performance review with competency ratings, accomplishments, and goals.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "employee_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "employee_title", Type = TemplateFieldType.String, Required = false },
        new() { Name = "department", Type = TemplateFieldType.String, Required = false },
        new() { Name = "reviewer_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "reviewer_title", Type = TemplateFieldType.String, Required = false },
        new() { Name = "review_period", Type = TemplateFieldType.String, Required = true, Description = "e.g. \"FY2025\" or \"Q1 2026\"." },
        new() { Name = "review_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "overall_rating", Type = TemplateFieldType.String, Required = false, Description = "e.g. Exceeds, Meets, Needs Improvement." },
        new() { Name = "summary", Type = TemplateFieldType.Text, Required = true },
        new()
        {
            Name = "competencies",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "name", Type = TemplateFieldType.String, Required = true },
                new() { Name = "rating", Type = TemplateFieldType.String, Required = false },
                new() { Name = "comments", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new() { Name = "accomplishments", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "areas_for_development", Type = TemplateFieldType.StringArray, Required = false },
        new()
        {
            Name = "goals_next_period",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "goal", Type = TemplateFieldType.String, Required = true },
                new() { Name = "target_date", Type = TemplateFieldType.Date, Required = false },
                new() { Name = "success_criteria", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new() { Name = "employee_comments", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var employee = d.String("employee_name");
        var reviewer = d.String("reviewer_name");
        var period = d.String("review_period");
        var reviewDate = d.Date("review_date");

        var sb = new StringBuilder();
        sb.Append("# Performance Review — ").AppendLine(employee);
        sb.Append("## ").AppendLine(period);
        sb.AppendLine();

        sb.Append("**Employee:** ").AppendLine(employee);
        var empTitle = d.String("employee_title");
        if (!string.IsNullOrWhiteSpace(empTitle)) sb.Append("**Title:** ").AppendLine(empTitle);
        var dept = d.String("department");
        if (!string.IsNullOrWhiteSpace(dept)) sb.Append("**Department:** ").AppendLine(dept);
        sb.Append("**Reviewer:** ").AppendLine(reviewer);
        var revTitle = d.String("reviewer_title");
        if (!string.IsNullOrWhiteSpace(revTitle)) sb.Append("**Reviewer title:** ").AppendLine(revTitle);
        sb.Append("**Review date:** ").AppendLine(TemplateFormat.FormatDate(reviewDate));
        var overall = d.String("overall_rating");
        if (!string.IsNullOrWhiteSpace(overall)) sb.Append("**Overall rating:** ").AppendLine(overall);
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine(d.String("summary"));
        sb.AppendLine();

        var competencies = d.ObjectArray("competencies");
        if (competencies.Count > 0)
        {
            sb.AppendLine("## Competency Ratings");
            sb.AppendLine();
            sb.AppendLine("| Competency | Rating | Comments |");
            sb.AppendLine("|---|---|---|");
            foreach (var c in competencies)
            {
                var name = TemplateFormat.GetString(c, "name");
                var rating = TemplateFormat.GetString(c, "rating");
                var comments = TemplateFormat.GetString(c, "comments");
                sb.Append("| ").Append(TemplateFormat.EscapePipes(name))
                  .Append(" | ").Append(string.IsNullOrWhiteSpace(rating) ? "—" : TemplateFormat.EscapePipes(rating))
                  .Append(" | ").Append(string.IsNullOrWhiteSpace(comments) ? "—" : TemplateFormat.EscapePipes(comments))
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        var accomplishments = d.StringArray("accomplishments");
        if (accomplishments.Count > 0)
        {
            sb.AppendLine("## Key Accomplishments");
            foreach (var a in accomplishments) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var dev = d.StringArray("areas_for_development");
        if (dev.Count > 0)
        {
            sb.AppendLine("## Areas for Development");
            foreach (var a in dev) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var goals = d.ObjectArray("goals_next_period");
        if (goals.Count > 0)
        {
            sb.AppendLine("## Goals for Next Period");
            sb.AppendLine();
            foreach (var g in goals)
            {
                var goal = TemplateFormat.GetString(g, "goal");
                var target = TemplateFormat.GetDate(g, "target_date");
                var criteria = TemplateFormat.GetString(g, "success_criteria");
                sb.Append("- **").Append(goal).Append("**");
                if (target is not null) sb.Append(" (target: ").Append(TemplateFormat.FormatDate(target)).Append(')');
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(criteria))
                    sb.Append("  *Success criteria:* ").AppendLine(criteria);
            }
            sb.AppendLine();
        }

        var employeeComments = d.String("employee_comments");
        if (!string.IsNullOrWhiteSpace(employeeComments))
        {
            sb.AppendLine("## Employee Comments");
            sb.AppendLine(employeeComments);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Acknowledgement");
        sb.AppendLine();
        sb.AppendLine("By signing below, the employee acknowledges that this review was discussed with them. Signature does not necessarily indicate agreement with the contents.");
        sb.AppendLine();
        sb.Append("**Reviewer:** ").AppendLine(reviewer);
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.Append("**Employee:** ").AppendLine(employee);
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        var fileName = $"review-{TemplateFormat.Slug(employee)}-{TemplateFormat.Slug(period)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Review — {employee} ({period})");
    }
}
