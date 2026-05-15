using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Legal;

/// <summary>
/// Demand letter — Word document stating facts, the legal basis for the
/// demand, the specific remedy sought, a deadline for response, and the
/// consequences of non-compliance. Ships with an AI-generated disclaimer.
/// </summary>
public sealed class DemandLetterTemplate : IDocumentTemplate
{
    public string Id => "legal/demand-letter";
    public string Name => "Demand Letter";
    public string Industry => "legal";
    public string Description => "Demand letter stating facts, remedy sought, deadline, and consequences.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "sender_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "sender_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "recipient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "recipient_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "letter_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "subject", Type = TemplateFieldType.String, Required = true },
        new() { Name = "facts", Type = TemplateFieldType.Text, Required = true, Description = "Statement of relevant facts." },
        new() { Name = "legal_basis", Type = TemplateFieldType.Text, Required = false, Description = "Legal grounds for the demand." },
        new() { Name = "demand", Type = TemplateFieldType.Text, Required = true, Description = "Specific remedy being requested." },
        new() { Name = "amount_due", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "response_deadline", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "consequences", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "signer_title", Type = TemplateFieldType.String, Required = false },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var sender = d.String("sender_name");
        var recipient = d.String("recipient_name");
        var letterDate = d.Date("letter_date");
        var deadline = d.Date("response_deadline");
        var subject = d.String("subject");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(sender);
        var senderAddr = d.String("sender_address");
        if (!string.IsNullOrWhiteSpace(senderAddr)) sb.AppendLine(senderAddr);
        sb.AppendLine();
        sb.AppendLine(TemplateFormat.FormatDate(letterDate));
        sb.AppendLine();
        sb.Append("**").Append(recipient).AppendLine("**");
        var recipientAddr = d.String("recipient_address");
        if (!string.IsNullOrWhiteSpace(recipientAddr)) sb.AppendLine(recipientAddr);
        sb.AppendLine();
        sb.Append("**Re: ").Append(subject).AppendLine("**");
        sb.AppendLine();
        sb.Append("Dear ").Append(recipient).AppendLine(",");
        sb.AppendLine();

        sb.AppendLine("## Statement of Facts");
        sb.AppendLine(d.String("facts"));
        sb.AppendLine();

        var legalBasis = d.String("legal_basis");
        if (!string.IsNullOrWhiteSpace(legalBasis))
        {
            sb.AppendLine("## Legal Basis");
            sb.AppendLine(legalBasis);
            sb.AppendLine();
        }

        sb.AppendLine("## Demand");
        sb.AppendLine(d.String("demand"));
        if (d.Has("amount_due"))
        {
            sb.AppendLine();
            sb.Append("Amount due: **").Append(TemplateFormat.FormatMoney(d.Decimal("amount_due"), currency)).AppendLine("**.");
        }
        sb.AppendLine();

        sb.Append("You must respond to this letter in writing no later than **")
          .Append(TemplateFormat.FormatDate(deadline))
          .AppendLine("**.");
        sb.AppendLine();

        var consequences = d.String("consequences");
        if (!string.IsNullOrWhiteSpace(consequences))
        {
            sb.AppendLine("## Consequences of Non-Compliance");
            sb.AppendLine(consequences);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Failure to respond by the deadline above may result in additional legal action without further notice.");
            sb.AppendLine();
        }

        sb.AppendLine("This letter is sent without prejudice to any rights or remedies available, all of which are expressly reserved.");
        sb.AppendLine();
        sb.AppendLine("Sincerely,");
        sb.AppendLine();
        sb.Append("**").Append(sender).AppendLine("**");
        var signerTitle = d.String("signer_title");
        if (!string.IsNullOrWhiteSpace(signerTitle)) sb.AppendLine(signerTitle);

        if (showDisclaimer)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before sending.*");
        }

        var fileName = $"demand-{TemplateFormat.Slug(sender)}-to-{TemplateFormat.Slug(recipient)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Demand — {sender} to {recipient}");
    }
}
