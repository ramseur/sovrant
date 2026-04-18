using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Business;

/// <summary>
/// Structured-PDF invoice: header with invoice number / dates, seller +
/// buyer blocks, a line-item table with quantity × unit price = subtotal,
/// tax row, and grand total. Emits Markdown with a pipe table that the
/// MigraDoc generator turns into a real table.
/// </summary>
public sealed class InvoiceTemplate : IDocumentTemplate
{
    public string Id => "business/invoice";
    public string Name => "Invoice";
    public string Industry => "business";
    public string Description => "Professional invoice with line items, tax, and totals.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "invoice_number", Type = TemplateFieldType.String, Required = true, Description = "Unique invoice identifier, e.g. INV-2026-001." },
        new() { Name = "issue_date", Type = TemplateFieldType.Date, Required = true, Description = "ISO date the invoice was issued." },
        new() { Name = "due_date", Type = TemplateFieldType.Date, Required = false, Description = "ISO date payment is due." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false, Description = "ISO currency code (default USD)." },
        new() { Name = "seller", Type = TemplateFieldType.Text, Required = true, Description = "Seller block — business name and address (multiline)." },
        new() { Name = "buyer", Type = TemplateFieldType.Text, Required = true, Description = "Buyer block — billed-to name and address (multiline)." },
        new()
        {
            Name = "line_items",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            Description = "One row per billable item.",
            ItemFields = new List<TemplateField>
            {
                new() { Name = "description", Type = TemplateFieldType.String, Required = true },
                new() { Name = "quantity", Type = TemplateFieldType.Decimal, Required = true },
                new() { Name = "unit_price", Type = TemplateFieldType.Decimal, Required = true },
            },
        },
        new() { Name = "tax_rate", Type = TemplateFieldType.Decimal, Required = false, Description = "Tax rate as a percentage, e.g. 8.25 for 8.25%." },
        new() { Name = "payment_terms", Type = TemplateFieldType.Text, Required = false, Description = "Payment instructions or terms." },
        new() { Name = "notes", Type = TemplateFieldType.Text, Required = false, Description = "Additional notes shown below totals." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var currency = NormalizeCurrency(d.String("currency", "USD"));
        var invoiceNumber = d.String("invoice_number");
        var issueDate = d.Date("issue_date");
        var dueDate = d.Date("due_date");

        var sb = new StringBuilder();
        sb.Append("# Invoice ").AppendLine(invoiceNumber);
        sb.AppendLine();

        sb.Append("**Issue date:** ").Append(FormatDate(issueDate));
        if (dueDate is not null) sb.Append("  \n**Due date:** ").Append(FormatDate(dueDate));
        sb.AppendLine();
        sb.AppendLine();

        sb.AppendLine("## From");
        sb.AppendLine(d.String("seller"));
        sb.AppendLine();

        sb.AppendLine("## Bill to");
        sb.AppendLine(d.String("buyer"));
        sb.AppendLine();

        sb.AppendLine("## Line items");
        sb.AppendLine();
        sb.AppendLine("| Description | Qty | Unit price | Subtotal |");
        sb.AppendLine("|---|---:|---:|---:|");

        decimal grossTotal = 0m;
        foreach (var item in d.ObjectArray("line_items"))
        {
            var description = GetString(item, "description");
            var qty = GetDecimal(item, "quantity");
            var unitPrice = GetDecimal(item, "unit_price");
            var lineTotal = qty * unitPrice;
            grossTotal += lineTotal;

            sb.Append("| ").Append(EscapePipes(description))
              .Append(" | ").Append(FormatQuantity(qty))
              .Append(" | ").Append(FormatMoney(unitPrice, currency))
              .Append(" | ").Append(FormatMoney(lineTotal, currency))
              .AppendLine(" |");
        }
        sb.AppendLine();

        var taxRate = d.Decimal("tax_rate");
        var taxAmount = Math.Round(grossTotal * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
        var total = grossTotal + taxAmount;

        sb.AppendLine("## Totals");
        sb.AppendLine();
        sb.AppendLine("| | |");
        sb.AppendLine("|---|---:|");
        sb.Append("| Subtotal | ").Append(FormatMoney(grossTotal, currency)).AppendLine(" |");
        if (taxRate > 0m)
            sb.Append("| Tax (").Append(FormatPercent(taxRate)).Append(") | ").Append(FormatMoney(taxAmount, currency)).AppendLine(" |");
        sb.Append("| **Total** | **").Append(FormatMoney(total, currency)).AppendLine("** |");
        sb.AppendLine();

        var paymentTerms = d.String("payment_terms");
        if (!string.IsNullOrWhiteSpace(paymentTerms))
        {
            sb.AppendLine("## Payment terms");
            sb.AppendLine(paymentTerms);
            sb.AppendLine();
        }

        var notes = d.String("notes");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine(notes);
        }

        var fileName = $"invoice-{Slug(invoiceNumber)}.pdf";
        var title = $"Invoice {invoiceNumber}";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, title);
    }

    private static string GetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static decimal GetDecimal(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return 0m;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => 0m,
        };
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatMoney(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{currency} {amount:N2}");

    private static string FormatQuantity(decimal qty) =>
        qty == Math.Floor(qty)
            ? qty.ToString("0", CultureInfo.InvariantCulture)
            : qty.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatPercent(decimal rate) =>
        rate == Math.Floor(rate)
            ? rate.ToString("0", CultureInfo.InvariantCulture) + "%"
            : rate.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string NormalizeCurrency(string input) =>
        string.IsNullOrWhiteSpace(input) ? "USD" : input.Trim().ToUpperInvariant();

    private static string EscapePipes(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Slug(string value)
    {
        var cleaned = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c)) cleaned.Append(char.ToLowerInvariant(c));
            else if (c is '-' or '_' or '.') cleaned.Append(c);
            else if (char.IsWhiteSpace(c)) cleaned.Append('-');
        }
        var slug = cleaned.ToString().Trim('-');
        return slug.Length == 0 ? "invoice" : slug;
    }
}
