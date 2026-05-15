using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Legal;

/// <summary>
/// Terms of Service — Word document for a website or online product,
/// covering acceptable use, accounts, intellectual property, disclaimers,
/// limitation of liability, termination, and governing law.
/// </summary>
public sealed class TermsOfServiceTemplate : IDocumentTemplate
{
    public string Id => "legal/terms-of-service";
    public string Name => "Terms of Service";
    public string Industry => "legal";
    public string Description => "Website / product terms of service covering accounts, IP, liability, and termination.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "company_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "service_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "website_url", Type = TemplateFieldType.String, Required = false },
        new() { Name = "effective_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "contact_email", Type = TemplateFieldType.String, Required = false },
        new() { Name = "service_description", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "account_requirements", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "acceptable_use", Type = TemplateFieldType.StringArray, Required = false, Description = "Prohibited behaviors." },
        new() { Name = "payment_terms", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "refund_policy", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "ip_notice", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "user_content_terms", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "third_party_services", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "termination_terms", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "governing_law", Type = TemplateFieldType.String, Required = true },
        new() { Name = "dispute_resolution", Type = TemplateFieldType.Text, Required = false, Description = "Arbitration clause, venue, etc." },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var company = d.String("company_name");
        var service = d.String("service_name");
        var website = d.String("website_url");
        var effective = d.Date("effective_date");
        var governingLaw = d.String("governing_law");
        var contactEmail = d.String("contact_email");
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.Append("# Terms of Service — ").AppendLine(service);
        sb.AppendLine();
        sb.Append("**Effective date:** ").AppendLine(TemplateFormat.FormatDate(effective));
        sb.AppendLine();

        sb.Append("These Terms of Service (the \"Terms\") govern your access to and use of ").Append(service)
          .Append(" (the \"Service\"), provided by ").Append(company).AppendLine(" (\"we,\" \"us,\" or \"our\"). By accessing or using the Service, you agree to be bound by these Terms.");
        sb.AppendLine();

        sb.AppendLine("## 1. The Service");
        sb.AppendLine();
        sb.AppendLine(d.String("service_description"));
        if (!string.IsNullOrWhiteSpace(website))
        {
            sb.AppendLine();
            sb.Append("Website: ").AppendLine(website);
        }
        sb.AppendLine();

        var accountReq = d.String("account_requirements");
        if (!string.IsNullOrWhiteSpace(accountReq))
        {
            sb.AppendLine("## 2. Accounts");
            sb.AppendLine();
            sb.AppendLine(accountReq);
            sb.AppendLine();
        }

        var acceptable = d.StringArray("acceptable_use");
        if (acceptable.Count > 0)
        {
            sb.AppendLine("## 3. Acceptable Use");
            sb.AppendLine();
            sb.AppendLine("You agree not to:");
            foreach (var p in acceptable) sb.Append("- ").AppendLine(p);
            sb.AppendLine();
        }

        var payment = d.String("payment_terms");
        if (!string.IsNullOrWhiteSpace(payment))
        {
            sb.AppendLine("## 4. Fees and Payment");
            sb.AppendLine();
            sb.AppendLine(payment);
            sb.AppendLine();
        }

        var refunds = d.String("refund_policy");
        if (!string.IsNullOrWhiteSpace(refunds))
        {
            sb.AppendLine("## 5. Refunds");
            sb.AppendLine();
            sb.AppendLine(refunds);
            sb.AppendLine();
        }

        sb.AppendLine("## 6. Intellectual Property");
        sb.AppendLine();
        var ip = d.String("ip_notice");
        sb.AppendLine(string.IsNullOrWhiteSpace(ip)
            ? $"The Service and all content, features, and functionality are the exclusive property of {company} and its licensors, protected by copyright, trademark, and other applicable intellectual property laws."
            : ip);
        sb.AppendLine();

        var userContent = d.String("user_content_terms");
        if (!string.IsNullOrWhiteSpace(userContent))
        {
            sb.AppendLine("## 7. User Content");
            sb.AppendLine();
            sb.AppendLine(userContent);
            sb.AppendLine();
        }

        var thirdParty = d.String("third_party_services");
        if (!string.IsNullOrWhiteSpace(thirdParty))
        {
            sb.AppendLine("## 8. Third-Party Services");
            sb.AppendLine();
            sb.AppendLine(thirdParty);
            sb.AppendLine();
        }

        sb.AppendLine("## 9. Disclaimers");
        sb.AppendLine();
        sb.AppendLine("The Service is provided \"as is\" and \"as available\" without warranties of any kind, either express or implied, including but not limited to implied warranties of merchantability, fitness for a particular purpose, and non-infringement.");
        sb.AppendLine();

        sb.AppendLine("## 10. Limitation of Liability");
        sb.AppendLine();
        sb.Append("To the maximum extent permitted by law, ").Append(company)
          .AppendLine(" and its affiliates will not be liable for any indirect, incidental, special, consequential, or punitive damages arising out of or related to your use of the Service.");
        sb.AppendLine();

        sb.AppendLine("## 11. Termination");
        sb.AppendLine();
        var termination = d.String("termination_terms");
        sb.AppendLine(string.IsNullOrWhiteSpace(termination)
            ? "We may suspend or terminate your access to the Service at any time for any reason, including breach of these Terms. You may stop using the Service at any time."
            : termination);
        sb.AppendLine();

        sb.AppendLine("## 12. Changes to These Terms");
        sb.AppendLine();
        sb.AppendLine("We may update these Terms from time to time. Material changes will be announced with reasonable notice. Continued use of the Service after an update constitutes acceptance of the revised Terms.");
        sb.AppendLine();

        var dispute = d.String("dispute_resolution");
        if (!string.IsNullOrWhiteSpace(dispute))
        {
            sb.AppendLine("## 13. Dispute Resolution");
            sb.AppendLine();
            sb.AppendLine(dispute);
            sb.AppendLine();
        }

        sb.AppendLine("## 14. Governing Law");
        sb.AppendLine();
        sb.Append("These Terms are governed by the laws of **").Append(governingLaw).AppendLine("**, without regard to its conflict-of-law provisions.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            sb.AppendLine("## 15. Contact");
            sb.AppendLine();
            sb.Append("Questions about these Terms may be directed to ").Append(contactEmail).AppendLine(".");
        }

        if (showDisclaimer)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before publishing.*");
        }

        var fileName = $"terms-of-service-{TemplateFormat.Slug(service)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Terms of Service — {service}");
    }
}
