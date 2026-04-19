using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Finance;

/// <summary>
/// Audit report (internal or external). Header block with scope and
/// period, an executive summary, then a findings table grouped by
/// severity, followed by recommendations.
/// </summary>
public sealed class AuditReportTemplate : IDocumentTemplate
{
    public string Id => "finance/audit-report";
    public string Name => "Audit Report";
    public string Industry => "finance";
    public string Description => "Internal or external audit report with findings grouped by severity.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "entity_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "audit_type", Type = TemplateFieldType.String, Required = true, Description = "e.g. Internal Controls, SOX, IT Security." },
        new() { Name = "period", Type = TemplateFieldType.String, Required = true },
        new() { Name = "auditor", Type = TemplateFieldType.String, Required = true },
        new() { Name = "report_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "scope", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "executive_summary", Type = TemplateFieldType.Text, Required = false },
        new()
        {
            Name = "findings",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "title", Type = TemplateFieldType.String, Required = true },
                new() { Name = "severity", Type = TemplateFieldType.String, Required = true, Description = "High / Medium / Low." },
                new() { Name = "description", Type = TemplateFieldType.Text, Required = true },
                new() { Name = "recommendation", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new() { Name = "overall_recommendations", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var entity = d.String("entity_name");
        var auditType = d.String("audit_type");
        var period = d.String("period");
        var auditor = d.String("auditor");
        var reportDate = d.Date("report_date");

        var sb = new StringBuilder();
        sb.Append("# Audit Report — ").AppendLine(entity);
        sb.Append("## ").Append(auditType).Append(" — ").AppendLine(period);
        sb.AppendLine();
        sb.Append("**Auditor:** ").AppendLine(auditor);
        sb.Append("**Report date:** ").AppendLine(TemplateFormat.FormatDate(reportDate));
        sb.AppendLine();

        sb.AppendLine("## Scope");
        sb.AppendLine(d.String("scope"));
        sb.AppendLine();

        var summary = d.String("executive_summary");
        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine(summary);
            sb.AppendLine();
        }

        var findings = d.ObjectArray("findings");
        var grouped = findings
            .GroupBy(f => TemplateFormat.GetString(f, "severity"), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => SeverityOrder(g.Key));

        sb.AppendLine("## Findings");
        sb.AppendLine();
        foreach (var group in grouped)
        {
            sb.Append("### Severity: ").AppendLine(string.IsNullOrWhiteSpace(group.Key) ? "Unspecified" : group.Key);
            sb.AppendLine();
            var index = 1;
            foreach (var f in group)
            {
                sb.Append("**").Append(index).Append(". ").Append(TemplateFormat.GetString(f, "title")).AppendLine("**");
                sb.AppendLine();
                sb.AppendLine(TemplateFormat.GetString(f, "description"));
                sb.AppendLine();
                var rec = TemplateFormat.GetString(f, "recommendation");
                if (!string.IsNullOrWhiteSpace(rec))
                {
                    sb.Append("*Recommendation:* ").AppendLine(rec);
                    sb.AppendLine();
                }
                index++;
            }
        }

        var overall = d.String("overall_recommendations");
        if (!string.IsNullOrWhiteSpace(overall))
        {
            sb.AppendLine("## Overall Recommendations");
            sb.AppendLine(overall);
        }

        var fileName = $"audit-{TemplateFormat.Slug(entity)}-{TemplateFormat.Slug(period)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"Audit Report — {entity}");
    }

    private static int SeverityOrder(string severity) => severity?.Trim().ToUpperInvariant() switch
    {
        "HIGH" or "CRITICAL" => 0,
        "MEDIUM" => 1,
        "LOW" => 2,
        "INFO" or "INFORMATIONAL" => 3,
        _ => 4,
    };
}
