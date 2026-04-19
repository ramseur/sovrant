using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Finance;

/// <summary>
/// Budget report comparing planned vs. actual spend per category, with
/// variance amount and percent. Renders as a structured PDF with a
/// summary line above a category-level table.
/// </summary>
public sealed class BudgetReportTemplate : IDocumentTemplate
{
    public string Id => "finance/budget-report";
    public string Name => "Budget Report";
    public string Industry => "finance";
    public string Description => "Budget vs. actuals report with per-category variance.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "owner", Type = TemplateFieldType.String, Required = true, Description = "Department, team, or project name." },
        new() { Name = "period", Type = TemplateFieldType.String, Required = true, Description = "Reporting period, e.g. \"April 2026\"." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new()
        {
            Name = "categories",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "category", Type = TemplateFieldType.String, Required = true },
                new() { Name = "planned", Type = TemplateFieldType.Decimal, Required = true },
                new() { Name = "actual", Type = TemplateFieldType.Decimal, Required = true },
            },
        },
        new() { Name = "narrative", Type = TemplateFieldType.Text, Required = false, Description = "Narrative explanation of variances." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var owner = d.String("owner");
        var period = d.String("period");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.Append("# Budget Report — ").AppendLine(owner);
        sb.Append("## Period: ").AppendLine(period);
        sb.AppendLine();

        sb.AppendLine("| Category | Planned | Actual | Variance | Variance % |");
        sb.AppendLine("|---|---:|---:|---:|---:|");

        decimal totalPlanned = 0m;
        decimal totalActual = 0m;
        foreach (var item in d.ObjectArray("categories"))
        {
            var category = TemplateFormat.GetString(item, "category");
            var planned = TemplateFormat.GetDecimal(item, "planned");
            var actual = TemplateFormat.GetDecimal(item, "actual");
            var variance = actual - planned;
            var variancePct = planned == 0m ? 0m : Math.Round((variance / planned) * 100m, 1, MidpointRounding.AwayFromZero);
            totalPlanned += planned;
            totalActual += actual;

            sb.Append("| ").Append(TemplateFormat.EscapePipes(category))
              .Append(" | ").Append(TemplateFormat.FormatMoney(planned, currency))
              .Append(" | ").Append(TemplateFormat.FormatMoney(actual, currency))
              .Append(" | ").Append(TemplateFormat.FormatMoney(variance, currency))
              .Append(" | ").Append(TemplateFormat.FormatPercent(variancePct))
              .AppendLine(" |");
        }

        var totalVariance = totalActual - totalPlanned;
        var totalVariancePct = totalPlanned == 0m ? 0m
            : Math.Round((totalVariance / totalPlanned) * 100m, 1, MidpointRounding.AwayFromZero);
        sb.Append("| **Total** | **").Append(TemplateFormat.FormatMoney(totalPlanned, currency))
          .Append("** | **").Append(TemplateFormat.FormatMoney(totalActual, currency))
          .Append("** | **").Append(TemplateFormat.FormatMoney(totalVariance, currency))
          .Append("** | **").Append(TemplateFormat.FormatPercent(totalVariancePct))
          .AppendLine("** |");
        sb.AppendLine();

        var narrative = d.String("narrative");
        if (!string.IsNullOrWhiteSpace(narrative))
        {
            sb.AppendLine("## Narrative");
            sb.AppendLine(narrative);
        }

        var fileName = $"budget-{TemplateFormat.Slug(owner)}-{TemplateFormat.Slug(period)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"Budget Report — {owner}");
    }
}
