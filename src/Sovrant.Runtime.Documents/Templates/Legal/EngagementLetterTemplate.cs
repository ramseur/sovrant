using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Legal;

/// <summary>
/// Professional services engagement letter — Word document describing
/// scope, fees, term, and conflicts, typically used by law firms,
/// accountants, and consultants. Includes AI-generated disclaimer.
/// </summary>
public sealed class EngagementLetterTemplate : IDocumentTemplate
{
    public string Id => "legal/engagement-letter";
    public string Name => "Engagement Letter";
    public string Industry => "legal";
    public string Description => "Professional services engagement letter with scope, fees, and conflicts.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "firm_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "firm_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "client_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "client_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "letter_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "matter_description", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "scope_of_services", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "fee_structure", Type = TemplateFieldType.Text, Required = true, Description = "Hourly rates, flat fee, retainer, etc." },
        new() { Name = "retainer_amount", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "billing_cycle", Type = TemplateFieldType.String, Required = false, Description = "e.g. monthly." },
        new() { Name = "conflicts_statement", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "signer_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "signer_title", Type = TemplateFieldType.String, Required = false },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var firm = d.String("firm_name");
        var client = d.String("client_name");
        var letterDate = d.Date("letter_date");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(firm);
        var firmAddress = d.String("firm_address");
        if (!string.IsNullOrWhiteSpace(firmAddress)) { sb.AppendLine(firmAddress); }
        sb.AppendLine();
        sb.Append(TemplateFormat.FormatDate(letterDate)).AppendLine();
        sb.AppendLine();
        sb.Append("**").Append(client).AppendLine("**");
        var clientAddress = d.String("client_address");
        if (!string.IsNullOrWhiteSpace(clientAddress)) sb.AppendLine(clientAddress);
        sb.AppendLine();

        sb.Append("Dear ").Append(client).AppendLine(",");
        sb.AppendLine();
        sb.Append("Thank you for engaging ").Append(firm).AppendLine(". This letter confirms the terms under which we will provide services to you on the matter described below.");
        sb.AppendLine();

        sb.AppendLine("## Matter");
        sb.AppendLine(d.String("matter_description"));
        sb.AppendLine();

        sb.AppendLine("## Scope of Services");
        sb.AppendLine(d.String("scope_of_services"));
        sb.AppendLine();

        sb.AppendLine("## Fees and Billing");
        sb.AppendLine(d.String("fee_structure"));
        if (d.Has("retainer_amount"))
        {
            sb.AppendLine();
            sb.Append("An initial retainer of **").Append(TemplateFormat.FormatMoney(d.Decimal("retainer_amount"), currency))
              .AppendLine("** is due upon execution of this engagement letter.");
        }
        var billing = d.String("billing_cycle");
        if (!string.IsNullOrWhiteSpace(billing))
        {
            sb.AppendLine();
            sb.Append("Invoices will be issued ").Append(billing).AppendLine(" and are due upon receipt unless otherwise agreed.");
        }
        sb.AppendLine();

        var conflicts = d.String("conflicts_statement");
        if (!string.IsNullOrWhiteSpace(conflicts))
        {
            sb.AppendLine("## Conflicts of Interest");
            sb.AppendLine(conflicts);
            sb.AppendLine();
        }

        sb.AppendLine("## Termination");
        sb.AppendLine("Either party may terminate this engagement at any time by written notice, subject to applicable professional responsibility rules. You remain responsible for fees and expenses incurred through the date of termination.");
        sb.AppendLine();

        sb.AppendLine("If the above accurately reflects our understanding, please sign and return a copy of this letter.");
        sb.AppendLine();
        sb.AppendLine("Sincerely,");
        sb.AppendLine();
        sb.Append("**").Append(d.String("signer_name")).AppendLine("**");
        var signerTitle = d.String("signer_title");
        if (!string.IsNullOrWhiteSpace(signerTitle)) sb.AppendLine(signerTitle);
        sb.AppendLine(firm);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**Agreed and accepted:**");
        sb.AppendLine();
        sb.Append("Client: ").AppendLine(client);
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        if (showDisclaimer)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*");
        }

        var fileName = $"engagement-{TemplateFormat.Slug(firm)}-{TemplateFormat.Slug(client)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Engagement — {firm} & {client}");
    }
}
