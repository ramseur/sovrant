using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Finance;

/// <summary>
/// Structured-PDF financial statement — income statement, balance sheet,
/// and cash flow in a single document (any subset supplied is rendered,
/// the rest skipped). Each statement is a two-column table of line items
/// with a trailing total row.
/// </summary>
public sealed class FinancialStatementTemplate : IDocumentTemplate
{
    public string Id => "finance/financial-statement";
    public string Name => "Financial Statement";
    public string Industry => "finance";
    public string Description => "Combined income statement, balance sheet, and cash flow.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "entity_name", Type = TemplateFieldType.String, Required = true, Description = "Company or individual name." },
        new() { Name = "period", Type = TemplateFieldType.String, Required = true, Description = "Reporting period, e.g. \"Q1 2026\" or \"Year ended 2025-12-31\"." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "prepared_by", Type = TemplateFieldType.String, Required = false },
        new()
        {
            Name = "income_statement",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            Description = "Rows in order. Use negative amounts for expenses/deductions.",
            ItemFields = new List<TemplateField>
            {
                new() { Name = "label", Type = TemplateFieldType.String, Required = true },
                new() { Name = "amount", Type = TemplateFieldType.Decimal, Required = true },
            },
        },
        new()
        {
            Name = "balance_sheet",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "label", Type = TemplateFieldType.String, Required = true },
                new() { Name = "amount", Type = TemplateFieldType.Decimal, Required = true },
                new() { Name = "section", Type = TemplateFieldType.String, Required = false, Description = "Optional grouping: Assets / Liabilities / Equity." },
            },
        },
        new()
        {
            Name = "cash_flow",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "label", Type = TemplateFieldType.String, Required = true },
                new() { Name = "amount", Type = TemplateFieldType.Decimal, Required = true },
                new() { Name = "section", Type = TemplateFieldType.String, Required = false, Description = "Optional grouping: Operating / Investing / Financing." },
            },
        },
        new() { Name = "notes", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var entity = d.String("entity_name");
        var period = d.String("period");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(entity);
        sb.Append("## Financial Statement — ").AppendLine(period);
        sb.AppendLine();

        var preparedBy = d.String("prepared_by");
        if (!string.IsNullOrWhiteSpace(preparedBy))
            sb.Append("**Prepared by:** ").AppendLine(preparedBy).AppendLine();

        RenderFlatSection(sb, "Income Statement", d.ObjectArray("income_statement"), currency, includeTotal: true);
        RenderSectionedSection(sb, "Balance Sheet", d.ObjectArray("balance_sheet"), currency);
        RenderSectionedSection(sb, "Cash Flow", d.ObjectArray("cash_flow"), currency);

        var notes = d.String("notes");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine(notes);
        }

        var fileName = $"financial-statement-{TemplateFormat.Slug(entity)}-{TemplateFormat.Slug(period)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"{entity} — Financial Statement");
    }

    private static void RenderFlatSection(StringBuilder sb, string title, IReadOnlyList<JsonElement> rows, string currency, bool includeTotal)
    {
        if (rows.Count == 0) return;
        sb.Append("## ").AppendLine(title);
        sb.AppendLine();
        sb.AppendLine("| Line item | Amount |");
        sb.AppendLine("|---|---:|");

        decimal total = 0m;
        foreach (var row in rows)
        {
            var label = TemplateFormat.GetString(row, "label");
            var amount = TemplateFormat.GetDecimal(row, "amount");
            total += amount;
            sb.Append("| ").Append(TemplateFormat.EscapePipes(label))
              .Append(" | ").Append(TemplateFormat.FormatMoney(amount, currency))
              .AppendLine(" |");
        }

        if (includeTotal)
            sb.Append("| **Total** | **").Append(TemplateFormat.FormatMoney(total, currency)).AppendLine("** |");
        sb.AppendLine();
    }

    private static void RenderSectionedSection(StringBuilder sb, string title, IReadOnlyList<JsonElement> rows, string currency)
    {
        if (rows.Count == 0) return;
        sb.Append("## ").AppendLine(title);
        sb.AppendLine();

        var grouped = rows.GroupBy(r => TemplateFormat.GetString(r, "section"), StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var group in grouped)
        {
            if (!string.IsNullOrWhiteSpace(group.Key))
            {
                sb.Append("### ").AppendLine(group.Key);
                sb.AppendLine();
            }
            sb.AppendLine("| Line item | Amount |");
            sb.AppendLine("|---|---:|");

            decimal sectionTotal = 0m;
            foreach (var row in group)
            {
                var label = TemplateFormat.GetString(row, "label");
                var amount = TemplateFormat.GetDecimal(row, "amount");
                sectionTotal += amount;
                sb.Append("| ").Append(TemplateFormat.EscapePipes(label))
                  .Append(" | ").Append(TemplateFormat.FormatMoney(amount, currency))
                  .AppendLine(" |");
            }
            sb.Append("| **Subtotal** | **").Append(TemplateFormat.FormatMoney(sectionTotal, currency)).AppendLine("** |");
            sb.AppendLine();
        }
    }
}
