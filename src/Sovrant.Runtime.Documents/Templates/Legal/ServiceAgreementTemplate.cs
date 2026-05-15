using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Legal;

/// <summary>
/// Master service agreement — Word document covering services, fees,
/// term, intellectual property, warranties, limitation of liability,
/// and general boilerplate. Meant as a starting draft to be reviewed
/// by counsel.
/// </summary>
public sealed class ServiceAgreementTemplate : IDocumentTemplate
{
    public string Id => "legal/service-agreement";
    public string Name => "Service Agreement";
    public string Industry => "legal";
    public string Description => "Master service agreement covering services, fees, IP, and boilerplate.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "client_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "client_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "provider_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "provider_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "effective_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "services_description", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "fees", Type = TemplateFieldType.Text, Required = true, Description = "Fee structure — hourly, fixed, retainer, etc." },
        new() { Name = "payment_terms", Type = TemplateFieldType.String, Required = false, Description = "e.g. Net 30." },
        new() { Name = "term", Type = TemplateFieldType.String, Required = false, Description = "e.g. 1 year, auto-renewing." },
        new() { Name = "termination_notice_days", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "ip_ownership", Type = TemplateFieldType.Text, Required = false, Description = "Who owns deliverables and pre-existing IP." },
        new() { Name = "confidentiality", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "governing_law", Type = TemplateFieldType.String, Required = true },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var client = d.String("client_name");
        var provider = d.String("provider_name");
        var effective = d.Date("effective_date");
        var noticeDays = d.Integer("termination_notice_days", 30);
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.AppendLine("# Services Agreement");
        sb.AppendLine();
        sb.Append("This Services Agreement (the \"Agreement\") is entered into as of ")
          .Append(TemplateFormat.FormatDate(effective))
          .Append(" (the \"Effective Date\") by and between **")
          .Append(provider).Append("** (\"Provider\") and **")
          .Append(client).AppendLine("** (\"Client\").");
        sb.AppendLine();

        var providerAddress = d.String("provider_address");
        var clientAddress = d.String("client_address");
        if (!string.IsNullOrWhiteSpace(providerAddress) || !string.IsNullOrWhiteSpace(clientAddress))
        {
            sb.AppendLine("## Parties");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(providerAddress))
            {
                sb.Append("**").Append(provider).AppendLine("**");
                sb.AppendLine(providerAddress);
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(clientAddress))
            {
                sb.Append("**").Append(client).AppendLine("**");
                sb.AppendLine(clientAddress);
                sb.AppendLine();
            }
        }

        sb.AppendLine("## 1. Services");
        sb.AppendLine();
        sb.AppendLine(d.String("services_description"));
        sb.AppendLine();

        sb.AppendLine("## 2. Fees and Payment");
        sb.AppendLine();
        sb.AppendLine(d.String("fees"));
        var paymentTerms = d.String("payment_terms");
        if (!string.IsNullOrWhiteSpace(paymentTerms))
        {
            sb.AppendLine();
            sb.Append("**Payment terms:** ").AppendLine(paymentTerms);
        }
        sb.AppendLine();

        sb.AppendLine("## 3. Term and Termination");
        sb.AppendLine();
        var term = d.String("term");
        if (!string.IsNullOrWhiteSpace(term))
            sb.Append("**Term:** ").AppendLine(term);
        sb.Append("Either party may terminate this Agreement by providing ").Append(noticeDays)
          .AppendLine(" days' prior written notice. Termination will not affect any rights or obligations accrued prior to termination.");
        sb.AppendLine();

        var ip = d.String("ip_ownership");
        if (!string.IsNullOrWhiteSpace(ip))
        {
            sb.AppendLine("## 4. Intellectual Property");
            sb.AppendLine();
            sb.AppendLine(ip);
            sb.AppendLine();
        }

        var confidentiality = d.String("confidentiality");
        if (!string.IsNullOrWhiteSpace(confidentiality))
        {
            sb.AppendLine("## 5. Confidentiality");
            sb.AppendLine();
            sb.AppendLine(confidentiality);
            sb.AppendLine();
        }

        sb.AppendLine("## 6. Warranties and Disclaimers");
        sb.AppendLine();
        sb.AppendLine("Provider warrants that the services will be performed in a professional and workmanlike manner. Except as expressly stated, the services are provided \"as is\" without warranty of any kind, express or implied.");
        sb.AppendLine();

        sb.AppendLine("## 7. Limitation of Liability");
        sb.AppendLine();
        sb.AppendLine("To the maximum extent permitted by law, neither party will be liable for indirect, incidental, special, or consequential damages. Each party's aggregate liability under this Agreement will not exceed the fees paid or payable in the twelve months preceding the claim.");
        sb.AppendLine();

        sb.AppendLine("## 8. Governing Law");
        sb.AppendLine();
        sb.Append("This Agreement is governed by the laws of **").Append(d.String("governing_law")).AppendLine("**.");
        sb.AppendLine();

        sb.AppendLine("## Signatures");
        sb.AppendLine();
        sb.Append("**").Append(provider).AppendLine("**");
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.Append("**").Append(client).AppendLine("**");
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        if (showDisclaimer)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*");
        }

        var fileName = $"services-agreement-{TemplateFormat.Slug(provider)}-{TemplateFormat.Slug(client)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Services Agreement — {provider} & {client}");
    }
}
