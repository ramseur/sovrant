using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Finance;

/// <summary>
/// Business proposal — structured PDF with an executive summary, scope,
/// a pricing table, timeline milestones, and an acceptance signature
/// block. Used for service engagements, consulting work, or vendor bids.
/// </summary>
public sealed class ProposalTemplate : IDocumentTemplate
{
    public string Id => "finance/proposal";
    public string Name => "Business Proposal";
    public string Industry => "finance";
    public string Description => "Business proposal with summary, pricing table, timeline, and acceptance block.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "proposal_title", Type = TemplateFieldType.String, Required = true },
        new() { Name = "prepared_for", Type = TemplateFieldType.String, Required = true, Description = "Client name." },
        new() { Name = "prepared_by", Type = TemplateFieldType.String, Required = true, Description = "Vendor / consultant name." },
        new() { Name = "proposal_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "valid_until", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "executive_summary", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "scope", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "deliverables", Type = TemplateFieldType.StringArray, Required = false },
        new()
        {
            Name = "pricing",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "item", Type = TemplateFieldType.String, Required = true },
                new() { Name = "quantity", Type = TemplateFieldType.Decimal, Required = false },
                new() { Name = "unit_price", Type = TemplateFieldType.Decimal, Required = true },
            },
        },
        new()
        {
            Name = "timeline",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "milestone", Type = TemplateFieldType.String, Required = true },
                new() { Name = "target_date", Type = TemplateFieldType.Date, Required = false },
            },
        },
        new() { Name = "terms", Type = TemplateFieldType.Text, Required = false, Description = "Terms and payment conditions." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var title = d.String("proposal_title");
        var preparedFor = d.String("prepared_for");
        var preparedBy = d.String("prepared_by");
        var proposalDate = d.Date("proposal_date");
        var validUntil = d.Date("valid_until");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(title);
        sb.AppendLine();
        sb.Append("**Prepared for:** ").AppendLine(preparedFor);
        sb.Append("**Prepared by:** ").AppendLine(preparedBy);
        sb.Append("**Date:** ").AppendLine(TemplateFormat.FormatDate(proposalDate));
        if (validUntil is not null)
            sb.Append("**Valid until:** ").AppendLine(TemplateFormat.FormatDate(validUntil));
        sb.AppendLine();

        sb.AppendLine("## Executive Summary");
        sb.AppendLine(d.String("executive_summary"));
        sb.AppendLine();

        sb.AppendLine("## Scope");
        sb.AppendLine(d.String("scope"));
        sb.AppendLine();

        var deliverables = d.StringArray("deliverables");
        if (deliverables.Count > 0)
        {
            sb.AppendLine("## Deliverables");
            foreach (var item in deliverables) sb.Append("- ").AppendLine(item);
            sb.AppendLine();
        }

        sb.AppendLine("## Pricing");
        sb.AppendLine();
        sb.AppendLine("| Item | Qty | Unit price | Subtotal |");
        sb.AppendLine("|---|---:|---:|---:|");

        decimal total = 0m;
        foreach (var item in d.ObjectArray("pricing"))
        {
            var itemName = TemplateFormat.GetString(item, "item");
            var qty = TemplateFormat.GetDecimal(item, "quantity");
            if (qty == 0m) qty = 1m;
            var unitPrice = TemplateFormat.GetDecimal(item, "unit_price");
            var subtotal = qty * unitPrice;
            total += subtotal;

            sb.Append("| ").Append(TemplateFormat.EscapePipes(itemName))
              .Append(" | ").Append(TemplateFormat.FormatNumber(qty))
              .Append(" | ").Append(TemplateFormat.FormatMoney(unitPrice, currency))
              .Append(" | ").Append(TemplateFormat.FormatMoney(subtotal, currency))
              .AppendLine(" |");
        }
        sb.Append("| **Total** | | | **").Append(TemplateFormat.FormatMoney(total, currency)).AppendLine("** |");
        sb.AppendLine();

        var timeline = d.ObjectArray("timeline");
        if (timeline.Count > 0)
        {
            sb.AppendLine("## Timeline");
            sb.AppendLine();
            foreach (var step in timeline)
            {
                var milestone = TemplateFormat.GetString(step, "milestone");
                var target = TemplateFormat.GetDate(step, "target_date");
                sb.Append("- ").Append(milestone);
                if (target is not null) sb.Append(" — ").Append(TemplateFormat.FormatDate(target));
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        var terms = d.String("terms");
        if (!string.IsNullOrWhiteSpace(terms))
        {
            sb.AppendLine("## Terms");
            sb.AppendLine(terms);
            sb.AppendLine();
        }

        sb.AppendLine("## Acceptance");
        sb.AppendLine();
        sb.Append("Accepted by ").Append(preparedFor).AppendLine(":");
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine();
        sb.AppendLine("Name / Title: ____________________________");
        sb.AppendLine();
        sb.AppendLine("Date: ____________________________");

        var fileName = $"proposal-{TemplateFormat.Slug(preparedFor)}-{TemplateFormat.Slug(title)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, title);
    }
}
