using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.RealEstate;

/// <summary>
/// Residential purchase agreement — Word document for a real estate
/// transaction. Covers parties, property, price, earnest money,
/// contingencies, and closing date.
/// </summary>
public sealed class PurchaseAgreementTemplate : IDocumentTemplate
{
    public string Id => "real-estate/purchase-agreement";
    public string Name => "Purchase Agreement";
    public string Industry => "real-estate";
    public string Description => "Residential purchase agreement with price, contingencies, and closing terms.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "buyer_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "seller_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "property_address", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "apn", Type = TemplateFieldType.String, Required = false, Description = "Assessor's parcel number." },
        new() { Name = "purchase_price", Type = TemplateFieldType.Currency, Required = true },
        new() { Name = "earnest_money", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "financing_contingency", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "inspection_contingency", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "appraisal_contingency", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "closing_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "possession_date", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "included_items", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "excluded_items", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "governing_state", Type = TemplateFieldType.String, Required = true },
        new() { Name = "effective_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var buyer = d.String("buyer_name");
        var seller = d.String("seller_name");
        var address = d.String("property_address");
        var price = d.Decimal("purchase_price");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));
        var closing = d.Date("closing_date");
        var effective = d.Date("effective_date");
        var state = d.String("governing_state");
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.AppendLine("# Residential Purchase Agreement");
        sb.AppendLine();
        sb.Append("This Purchase Agreement (the \"Agreement\") is entered into as of ").Append(TemplateFormat.FormatDate(effective))
          .Append(" by and between **").Append(seller).Append("** (\"Seller\") and **").Append(buyer).AppendLine("** (\"Buyer\").");
        sb.AppendLine();

        sb.AppendLine("## 1. Property");
        sb.AppendLine();
        sb.AppendLine("Seller agrees to sell and Buyer agrees to purchase the following real property:");
        sb.AppendLine();
        sb.AppendLine(address);
        var apn = d.String("apn");
        if (!string.IsNullOrWhiteSpace(apn)) { sb.AppendLine(); sb.Append("APN: ").AppendLine(apn); }
        sb.AppendLine();

        sb.AppendLine("## 2. Purchase Price and Earnest Money");
        sb.AppendLine();
        sb.Append("The total purchase price is **").Append(TemplateFormat.FormatMoney(price, currency)).AppendLine("**.");
        if (d.Has("earnest_money"))
        {
            sb.Append("Buyer shall deposit earnest money of **")
              .Append(TemplateFormat.FormatMoney(d.Decimal("earnest_money"), currency))
              .AppendLine("** within three business days after acceptance of this Agreement, to be applied against the purchase price at closing.");
        }
        sb.AppendLine();

        sb.AppendLine("## 3. Contingencies");
        sb.AppendLine();
        sb.AppendLine("This Agreement is subject to the following contingencies:");
        sb.AppendLine();
        AppendContingency(sb, "Financing", d.Has("financing_contingency") ? d.Boolean("financing_contingency") : true);
        AppendContingency(sb, "Inspection", d.Has("inspection_contingency") ? d.Boolean("inspection_contingency") : true);
        AppendContingency(sb, "Appraisal", d.Has("appraisal_contingency") ? d.Boolean("appraisal_contingency") : true);
        sb.AppendLine();

        sb.AppendLine("## 4. Closing and Possession");
        sb.AppendLine();
        sb.Append("Closing shall occur on or before **").Append(TemplateFormat.FormatDate(closing)).AppendLine("**.");
        var possession = d.Date("possession_date");
        if (possession is not null)
            sb.Append("Possession shall be delivered to Buyer on **").Append(TemplateFormat.FormatDate(possession)).AppendLine("**.");
        sb.AppendLine();

        var included = d.StringArray("included_items");
        var excluded = d.StringArray("excluded_items");
        if (included.Count > 0 || excluded.Count > 0)
        {
            sb.AppendLine("## 5. Included and Excluded Items");
            sb.AppendLine();
            if (included.Count > 0)
            {
                sb.AppendLine("**Included with the property:**");
                foreach (var i in included) sb.Append("- ").AppendLine(i);
                sb.AppendLine();
            }
            if (excluded.Count > 0)
            {
                sb.AppendLine("**Excluded from the sale:**");
                foreach (var e in excluded) sb.Append("- ").AppendLine(e);
                sb.AppendLine();
            }
        }

        sb.AppendLine("## 6. Default");
        sb.AppendLine();
        sb.AppendLine("If Buyer defaults, Seller may retain the earnest money as liquidated damages. If Seller defaults, Buyer may pursue all remedies available at law or equity, including specific performance.");
        sb.AppendLine();

        sb.AppendLine("## 7. Governing Law");
        sb.AppendLine();
        sb.Append("This Agreement is governed by the laws of the State of **").Append(state).AppendLine("**.");
        sb.AppendLine();

        sb.AppendLine("## Signatures");
        sb.AppendLine();
        sb.Append("**Buyer:** ").AppendLine(buyer);
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.Append("**Seller:** ").AppendLine(seller);
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        if (showDisclaimer)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with a qualified real estate attorney or broker before execution.*");
        }

        var fileName = $"purchase-{TemplateFormat.Slug(buyer)}-{TemplateFormat.Slug(seller)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Purchase Agreement — {buyer} / {seller}");
    }

    private static void AppendContingency(StringBuilder sb, string name, bool included)
    {
        sb.Append("- **").Append(name).Append(":** ").AppendLine(included ? "included" : "waived");
    }
}
