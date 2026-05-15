using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.RealEstate;

/// <summary>
/// Closing disclosure — structured PDF summarizing loan terms, closing
/// costs, and cash to close at real estate settlement. Meant as a
/// settlement preview; the official lender-provided CD must be used for
/// the actual transaction.
/// </summary>
public sealed class ClosingDisclosureTemplate : IDocumentTemplate
{
    public string Id => "real-estate/closing-disclosure";
    public string Name => "Closing Disclosure";
    public string Industry => "real-estate";
    public string Description => "Closing disclosure summarizing loan terms, closing costs, and cash to close.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "borrower_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "seller_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "property_address", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "closing_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "lender_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "loan_amount", Type = TemplateFieldType.Currency, Required = true },
        new() { Name = "interest_rate_percent", Type = TemplateFieldType.Decimal, Required = true },
        new() { Name = "loan_term_years", Type = TemplateFieldType.Integer, Required = true },
        new() { Name = "monthly_principal_interest", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "purchase_price", Type = TemplateFieldType.Currency, Required = true },
        new() { Name = "down_payment", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new()
        {
            Name = "closing_costs",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "description", Type = TemplateFieldType.String, Required = true },
                new() { Name = "amount", Type = TemplateFieldType.Currency, Required = true },
                new() { Name = "paid_by", Type = TemplateFieldType.String, Required = false, Description = "Borrower, Seller, or Lender." },
            },
        },
        new() { Name = "cash_to_close", Type = TemplateFieldType.Currency, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var borrower = d.String("borrower_name");
        var property = d.String("property_address");
        var closing = d.Date("closing_date");
        var lender = d.String("lender_name");
        var loanAmount = d.Decimal("loan_amount");
        var rate = d.Decimal("interest_rate_percent");
        var termYears = d.Integer("loan_term_years");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.AppendLine("# Closing Disclosure");
        sb.AppendLine();
        sb.Append("**Borrower:** ").AppendLine(borrower);
        var seller = d.String("seller_name");
        if (!string.IsNullOrWhiteSpace(seller)) sb.Append("**Seller:** ").AppendLine(seller);
        sb.AppendLine("**Property:**");
        sb.AppendLine(property);
        sb.Append("**Closing date:** ").AppendLine(TemplateFormat.FormatDate(closing));
        sb.Append("**Lender:** ").AppendLine(lender);
        sb.AppendLine();

        sb.AppendLine("## Loan Terms");
        sb.AppendLine();
        sb.AppendLine("| Item | Amount |");
        sb.AppendLine("|---|---:|");
        sb.Append("| Loan amount | ").Append(TemplateFormat.FormatMoney(loanAmount, currency)).AppendLine(" |");
        sb.Append("| Interest rate | ").Append(TemplateFormat.FormatPercent(rate)).AppendLine(" |");
        sb.Append("| Term | ").Append(termYears).AppendLine(" years |");
        if (d.Has("monthly_principal_interest"))
            sb.Append("| Monthly principal &amp; interest | ").Append(TemplateFormat.FormatMoney(d.Decimal("monthly_principal_interest"), currency)).AppendLine(" |");
        sb.AppendLine();

        sb.AppendLine("## Transaction Summary");
        sb.AppendLine();
        var price = d.Decimal("purchase_price");
        var down = d.Decimal("down_payment");
        sb.Append("| Purchase price | ").Append(TemplateFormat.FormatMoney(price, currency)).AppendLine(" |");
        if (d.Has("down_payment"))
            sb.Append("| Down payment | ").Append(TemplateFormat.FormatMoney(down, currency)).AppendLine(" |");
        sb.Append("| Loan amount | ").Append(TemplateFormat.FormatMoney(loanAmount, currency)).AppendLine(" |");
        sb.AppendLine();

        var costs = d.ObjectArray("closing_costs");
        if (costs.Count > 0)
        {
            sb.AppendLine("## Closing Costs");
            sb.AppendLine();
            sb.AppendLine("| Description | Amount | Paid by |");
            sb.AppendLine("|---|---:|---|");
            decimal total = 0m;
            foreach (var c in costs)
            {
                var desc = TemplateFormat.GetString(c, "description");
                var amount = TemplateFormat.GetDecimal(c, "amount");
                var paidBy = TemplateFormat.GetString(c, "paid_by");
                total += amount;
                sb.Append("| ").Append(TemplateFormat.EscapePipes(desc))
                  .Append(" | ").Append(TemplateFormat.FormatMoney(amount, currency))
                  .Append(" | ").Append(string.IsNullOrWhiteSpace(paidBy) ? "—" : TemplateFormat.EscapePipes(paidBy))
                  .AppendLine(" |");
            }
            sb.Append("| **Total closing costs** | **").Append(TemplateFormat.FormatMoney(total, currency)).AppendLine("** | |");
            sb.AppendLine();
        }

        if (d.Has("cash_to_close"))
        {
            sb.AppendLine("## Cash to Close");
            sb.AppendLine();
            sb.Append("**").Append(TemplateFormat.FormatMoney(d.Decimal("cash_to_close"), currency)).AppendLine("**");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This document is a settlement preview only. The lender's official Closing Disclosure (Form H-25) provided under TILA/RESPA is the authoritative record for the transaction.*");

        var fileName = $"closing-disclosure-{TemplateFormat.Slug(borrower)}-{TemplateFormat.FormatDate(closing)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"Closing Disclosure — {borrower}");
    }
}
