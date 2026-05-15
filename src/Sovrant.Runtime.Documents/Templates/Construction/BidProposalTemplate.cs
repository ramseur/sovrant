using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Construction;

/// <summary>
/// Construction bid proposal — StructuredPdf document submitted by a
/// contractor in response to a solicitation, detailing scope,
/// itemized costs, schedule, exclusions, and bid validity.
/// </summary>
public sealed class BidProposalTemplate : IDocumentTemplate
{
    public string Id => "construction/bid-proposal";
    public string Name => "Bid Proposal";
    public string Industry => "construction";
    public string Description => "Construction bid proposal with itemized costs, schedule, and exclusions.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "contractor_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "contractor_license", Type = TemplateFieldType.String, Required = false },
        new() { Name = "contractor_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "client_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "project_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "project_address", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "bid_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "bid_valid_until", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "proposed_start_date", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "proposed_completion_date", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "scope_summary", Type = TemplateFieldType.Text, Required = true },
        new()
        {
            Name = "line_items",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "description", Type = TemplateFieldType.String, Required = true },
                new() { Name = "quantity", Type = TemplateFieldType.Decimal, Required = false },
                new() { Name = "unit", Type = TemplateFieldType.String, Required = false },
                new() { Name = "unit_price", Type = TemplateFieldType.Currency, Required = false },
                new() { Name = "amount", Type = TemplateFieldType.Currency, Required = true },
            },
        },
        new() { Name = "overhead_and_profit_percent", Type = TemplateFieldType.Decimal, Required = false },
        new() { Name = "contingency_percent", Type = TemplateFieldType.Decimal, Required = false },
        new() { Name = "exclusions", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "assumptions", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "payment_terms", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "warranty", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var contractor = d.String("contractor_name");
        var client = d.String("client_name");
        var project = d.String("project_name");
        var bidDate = d.Date("bid_date");
        var validUntil = d.Date("bid_valid_until");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency"));

        var sb = new StringBuilder();
        sb.AppendLine("# Bid Proposal");
        sb.AppendLine();
        sb.Append("**").Append(contractor).AppendLine("**");
        var license = d.String("contractor_license");
        if (!string.IsNullOrWhiteSpace(license)) sb.Append("License: ").AppendLine(license);
        var addr = d.String("contractor_address");
        if (!string.IsNullOrWhiteSpace(addr)) sb.AppendLine(addr);
        sb.AppendLine();

        sb.Append("**Prepared for:** ").AppendLine(client);
        sb.Append("**Project:** ").AppendLine(project);
        sb.AppendLine("**Location:**");
        sb.AppendLine(d.String("project_address"));
        sb.Append("**Bid date:** ").AppendLine(TemplateFormat.FormatDate(bidDate));
        sb.Append("**Bid valid until:** ").AppendLine(TemplateFormat.FormatDate(validUntil));
        var start = d.Date("proposed_start_date");
        if (start is not null) sb.Append("**Proposed start:** ").AppendLine(TemplateFormat.FormatDate(start));
        var complete = d.Date("proposed_completion_date");
        if (complete is not null) sb.Append("**Proposed completion:** ").AppendLine(TemplateFormat.FormatDate(complete));
        sb.AppendLine();

        sb.AppendLine("## Scope of Work");
        sb.AppendLine(d.String("scope_summary"));
        sb.AppendLine();

        sb.AppendLine("## Itemized Bid");
        sb.AppendLine();
        sb.AppendLine("| # | Description | Qty | Unit | Unit price | Amount |");
        sb.AppendLine("|---|-------------|----:|:-----|-----------:|-------:|");
        decimal subtotal = 0m;
        var n = 1;
        foreach (var item in d.ObjectArray("line_items"))
        {
            var desc = TemplateFormat.GetString(item, "description");
            var qty = TemplateFormat.GetDecimal(item, "quantity");
            var unit = TemplateFormat.GetString(item, "unit");
            var unitPrice = TemplateFormat.GetDecimal(item, "unit_price");
            var amount = TemplateFormat.GetDecimal(item, "amount");
            subtotal += amount;
            sb.Append("| ").Append(n++)
              .Append(" | ").Append(TemplateFormat.EscapePipes(desc))
              .Append(" | ").Append(qty > 0 ? TemplateFormat.FormatNumber(qty) : string.Empty)
              .Append(" | ").Append(TemplateFormat.EscapePipes(unit))
              .Append(" | ").Append(unitPrice > 0 ? TemplateFormat.FormatMoney(unitPrice, currency) : string.Empty)
              .Append(" | ").Append(TemplateFormat.FormatMoney(amount, currency))
              .AppendLine(" |");
        }
        sb.AppendLine();
        sb.Append("**Subtotal:** ").AppendLine(TemplateFormat.FormatMoney(subtotal, currency));

        var ohp = d.Decimal("overhead_and_profit_percent");
        var ohpAmount = 0m;
        if (ohp > 0)
        {
            ohpAmount = Math.Round(subtotal * ohp / 100m, 2, MidpointRounding.AwayFromZero);
            sb.Append("**Overhead & profit (").Append(TemplateFormat.FormatPercent(ohp)).Append("):** ")
              .AppendLine(TemplateFormat.FormatMoney(ohpAmount, currency));
        }
        var contingency = d.Decimal("contingency_percent");
        var contingencyAmount = 0m;
        if (contingency > 0)
        {
            contingencyAmount = Math.Round(subtotal * contingency / 100m, 2, MidpointRounding.AwayFromZero);
            sb.Append("**Contingency (").Append(TemplateFormat.FormatPercent(contingency)).Append("):** ")
              .AppendLine(TemplateFormat.FormatMoney(contingencyAmount, currency));
        }
        var grandTotal = subtotal + ohpAmount + contingencyAmount;
        sb.Append("**Total bid price:** ").AppendLine(TemplateFormat.FormatMoney(grandTotal, currency));
        sb.AppendLine();

        var exclusions = d.StringArray("exclusions");
        if (exclusions.Count > 0)
        {
            sb.AppendLine("## Exclusions");
            foreach (var e in exclusions) sb.Append("- ").AppendLine(e);
            sb.AppendLine();
        }

        var assumptions = d.StringArray("assumptions");
        if (assumptions.Count > 0)
        {
            sb.AppendLine("## Assumptions");
            foreach (var a in assumptions) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var payment = d.String("payment_terms");
        if (!string.IsNullOrWhiteSpace(payment))
        {
            sb.AppendLine("## Payment Terms");
            sb.AppendLine(payment);
            sb.AppendLine();
        }

        var warranty = d.String("warranty");
        if (!string.IsNullOrWhiteSpace(warranty))
        {
            sb.AppendLine("## Warranty");
            sb.AppendLine(warranty);
            sb.AppendLine();
        }

        sb.AppendLine("## Acceptance");
        sb.AppendLine();
        sb.AppendLine("Signature of authorized representative: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This bid is based on the information available at the date of submission. Field conditions, permitting delays, or owner-directed changes may affect cost and schedule and will be addressed via change order.*");

        var fileName = $"bid-{TemplateFormat.Slug(project)}-{TemplateFormat.FormatDate(bidDate)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"Bid — {project}");
    }
}
