using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Business;

/// <summary>
/// Employment offer letter — Word document with candidate details,
/// position, compensation package (base salary, bonus, equity),
/// benefits summary, start date, and contingencies.
/// </summary>
public sealed class EmployeeOfferLetterTemplate : IDocumentTemplate
{
    public string Id => "business/employee-offer";
    public string Name => "Employment Offer Letter";
    public string Industry => "business";
    public string Description => "Employment offer letter with compensation, benefits, and start date.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "company_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "candidate_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "candidate_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "position_title", Type = TemplateFieldType.String, Required = true },
        new() { Name = "department", Type = TemplateFieldType.String, Required = false },
        new() { Name = "manager", Type = TemplateFieldType.String, Required = false },
        new() { Name = "employment_type", Type = TemplateFieldType.String, Required = false, Description = "Full-time, Part-time, Contract." },
        new() { Name = "start_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "offer_expiration", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "base_salary", Type = TemplateFieldType.Currency, Required = true },
        new() { Name = "salary_frequency", Type = TemplateFieldType.String, Required = false, Description = "annual / hourly / monthly (default annual)." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "sign_on_bonus", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "target_bonus_percent", Type = TemplateFieldType.Decimal, Required = false },
        new() { Name = "equity", Type = TemplateFieldType.Text, Required = false, Description = "Equity grant details." },
        new() { Name = "benefits_summary", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "contingencies", Type = TemplateFieldType.StringArray, Required = false, Description = "e.g. background check, reference check, work authorization." },
        new() { Name = "signer_name", Type = TemplateFieldType.String, Required = true, Description = "Person signing on behalf of the company." },
        new() { Name = "signer_title", Type = TemplateFieldType.String, Required = false },
        new() { Name = "letter_date", Type = TemplateFieldType.Date, Required = true },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var company = d.String("company_name");
        var candidate = d.String("candidate_name");
        var title = d.String("position_title");
        var startDate = d.Date("start_date");
        var offerExpiration = d.Date("offer_expiration");
        var baseSalary = d.Decimal("base_salary");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));
        var frequency = d.String("salary_frequency", "annual");
        var letterDate = d.Date("letter_date");

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(company);
        sb.AppendLine();
        sb.Append(TemplateFormat.FormatDate(letterDate)).AppendLine();
        sb.AppendLine();
        sb.Append("**").Append(candidate).AppendLine("**");
        var address = d.String("candidate_address");
        if (!string.IsNullOrWhiteSpace(address))
            sb.AppendLine(address);
        sb.AppendLine();

        sb.Append("Dear ").Append(candidate).AppendLine(",");
        sb.AppendLine();
        sb.Append("We are pleased to offer you the position of **").Append(title).Append("** at ").Append(company).AppendLine(". We are excited about the skills and experience you will bring to our team.");
        sb.AppendLine();

        sb.AppendLine("## Position Details");
        sb.AppendLine();
        sb.Append("- **Title:** ").AppendLine(title);
        var dept = d.String("department");
        if (!string.IsNullOrWhiteSpace(dept)) sb.Append("- **Department:** ").AppendLine(dept);
        var manager = d.String("manager");
        if (!string.IsNullOrWhiteSpace(manager)) sb.Append("- **Reports to:** ").AppendLine(manager);
        var empType = d.String("employment_type");
        if (!string.IsNullOrWhiteSpace(empType)) sb.Append("- **Employment type:** ").AppendLine(empType);
        sb.Append("- **Start date:** ").AppendLine(TemplateFormat.FormatDate(startDate));
        sb.AppendLine();

        sb.AppendLine("## Compensation");
        sb.AppendLine();
        sb.Append("- **Base salary:** ").Append(TemplateFormat.FormatMoney(baseSalary, currency)).Append(' ').AppendLine(frequency);
        if (d.Has("sign_on_bonus"))
            sb.Append("- **Sign-on bonus:** ").AppendLine(TemplateFormat.FormatMoney(d.Decimal("sign_on_bonus"), currency));
        if (d.Has("target_bonus_percent"))
            sb.Append("- **Target annual bonus:** ").Append(TemplateFormat.FormatPercent(d.Decimal("target_bonus_percent"))).AppendLine(" of base salary");
        var equity = d.String("equity");
        if (!string.IsNullOrWhiteSpace(equity))
            sb.Append("- **Equity:** ").AppendLine(equity);
        sb.AppendLine();

        var benefits = d.String("benefits_summary");
        if (!string.IsNullOrWhiteSpace(benefits))
        {
            sb.AppendLine("## Benefits");
            sb.AppendLine(benefits);
            sb.AppendLine();
        }

        var contingencies = d.StringArray("contingencies");
        if (contingencies.Count > 0)
        {
            sb.AppendLine("## Contingencies");
            sb.AppendLine("This offer is contingent upon:");
            foreach (var c in contingencies) sb.Append("- ").AppendLine(c);
            sb.AppendLine();
        }

        if (offerExpiration is not null)
        {
            sb.Append("This offer will remain open until **").Append(TemplateFormat.FormatDate(offerExpiration)).AppendLine("**.");
            sb.AppendLine();
        }

        sb.AppendLine("Employment with the company is at-will, meaning either you or the company may terminate the relationship at any time, with or without cause or notice, consistent with applicable law.");
        sb.AppendLine();
        sb.AppendLine("We look forward to you joining us.");
        sb.AppendLine();
        sb.AppendLine("Sincerely,");
        sb.AppendLine();
        sb.Append("**").Append(d.String("signer_name")).AppendLine("**");
        var signerTitle = d.String("signer_title");
        if (!string.IsNullOrWhiteSpace(signerTitle)) sb.AppendLine(signerTitle);
        sb.Append(company).AppendLine();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**Acceptance**");
        sb.AppendLine();
        sb.Append("I, ").Append(candidate).AppendLine(", accept the offer of employment on the terms above.");
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine();
        sb.AppendLine("Date: ____________________________");

        var fileName = $"offer-{TemplateFormat.Slug(candidate)}-{TemplateFormat.FormatDate(letterDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Offer — {candidate}");
    }
}
