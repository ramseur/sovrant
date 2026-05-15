using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Healthcare;

/// <summary>
/// HIPAA authorization — Word document for a patient to authorize the
/// release or exchange of Protected Health Information between a
/// covered entity and a named recipient. Includes expiration date and
/// right-to-revoke language required by 45 CFR §164.508.
/// </summary>
public sealed class HipaaAuthorizationTemplate : IDocumentTemplate
{
    public string Id => "healthcare/hipaa-authorization";
    public string Name => "HIPAA Authorization";
    public string Industry => "healthcare";
    public string Description => "HIPAA authorization for release of Protected Health Information.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "patient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "date_of_birth", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "mrn", Type = TemplateFieldType.String, Required = false },
        new() { Name = "releasing_entity", Type = TemplateFieldType.String, Required = true, Description = "Covered entity releasing the information." },
        new() { Name = "releasing_entity_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "receiving_party", Type = TemplateFieldType.String, Required = true, Description = "Person or organization receiving the information." },
        new() { Name = "receiving_party_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "information_to_release", Type = TemplateFieldType.StringArray, Required = true, Description = "Specific categories of PHI to be released." },
        new() { Name = "date_range_start", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "date_range_end", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "purpose_of_release", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "authorization_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "expiration_date", Type = TemplateFieldType.Date, Required = true, Description = "Required by 45 CFR §164.508(c)(1)(v)." },
        new() { Name = "includes_sensitive_categories", Type = TemplateFieldType.StringArray, Required = false, Description = "e.g. mental health, substance use, HIV — often require explicit opt-in." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var patient = d.String("patient_name");
        var dob = d.Date("date_of_birth");
        var releasing = d.String("releasing_entity");
        var receiving = d.String("receiving_party");
        var authDate = d.Date("authorization_date");
        var expiration = d.Date("expiration_date");

        var sb = new StringBuilder();
        sb.AppendLine("# Authorization for Release of Protected Health Information");
        sb.AppendLine();
        sb.AppendLine("This authorization is executed in accordance with the Health Insurance Portability and Accountability Act of 1996 (HIPAA) and its implementing regulations at 45 CFR Part 164.");
        sb.AppendLine();

        sb.AppendLine("## Patient");
        sb.Append("**Name:** ").AppendLine(patient);
        sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        var mrn = d.String("mrn");
        if (!string.IsNullOrWhiteSpace(mrn)) sb.Append("**MRN:** ").AppendLine(mrn);
        sb.AppendLine();

        sb.AppendLine("## Releasing Entity");
        sb.AppendLine(releasing);
        var releasingAddr = d.String("releasing_entity_address");
        if (!string.IsNullOrWhiteSpace(releasingAddr)) sb.AppendLine(releasingAddr);
        sb.AppendLine();

        sb.AppendLine("## Receiving Party");
        sb.AppendLine(receiving);
        var receivingAddr = d.String("receiving_party_address");
        if (!string.IsNullOrWhiteSpace(receivingAddr)) sb.AppendLine(receivingAddr);
        sb.AppendLine();

        sb.AppendLine("## Information Authorized for Release");
        foreach (var item in d.StringArray("information_to_release")) sb.Append("- ").AppendLine(item);
        var start = d.Date("date_range_start");
        var end = d.Date("date_range_end");
        if (start is not null || end is not null)
        {
            sb.AppendLine();
            sb.Append("**Covering services from** ").Append(start is not null ? TemplateFormat.FormatDate(start) : "earliest record")
              .Append(" **to** ").Append(end is not null ? TemplateFormat.FormatDate(end) : "present").AppendLine(".");
        }
        sb.AppendLine();

        var sensitive = d.StringArray("includes_sensitive_categories");
        if (sensitive.Count > 0)
        {
            sb.AppendLine("## Sensitive Categories (Explicit Opt-In)");
            sb.AppendLine("I specifically authorize the release of information in these categories, which may be subject to additional federal or state protections:");
            foreach (var s in sensitive) sb.Append("- ").AppendLine(s);
            sb.AppendLine();
        }

        sb.AppendLine("## Purpose");
        sb.AppendLine(d.String("purpose_of_release"));
        sb.AppendLine();

        sb.AppendLine("## Expiration");
        sb.Append("This authorization expires on **").Append(TemplateFormat.FormatDate(expiration)).AppendLine("**, or upon revocation by the patient, whichever is earlier.");
        sb.AppendLine();

        sb.AppendLine("## Right to Revoke");
        sb.AppendLine("I understand that I may revoke this authorization in writing at any time, except to the extent that the releasing entity has already acted in reliance on it. Revocation must be submitted in writing to the releasing entity at the address above.");
        sb.AppendLine();

        sb.AppendLine("## Redisclosure Notice");
        sb.AppendLine("I understand that information disclosed pursuant to this authorization may be redisclosed by the recipient and no longer protected by federal privacy regulations. Conditions on further disclosure, if any, depend on the recipient's own obligations under applicable law.");
        sb.AppendLine();

        sb.AppendLine("## Conditioning of Services");
        sb.AppendLine("I understand that the releasing entity generally may not condition treatment, payment, or eligibility for benefits on my signing this authorization.");
        sb.AppendLine();

        sb.AppendLine("## Authorization");
        sb.AppendLine();
        sb.Append("**Date signed:** ").AppendLine(TemplateFormat.FormatDate(authDate));
        sb.AppendLine();
        sb.Append("Patient / personal representative: ").AppendLine(patient);
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine("If signed by a personal representative, relationship to patient: ____________________________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. Review for compliance with HIPAA and any applicable state laws before use.*");

        var fileName = $"hipaa-auth-{TemplateFormat.Slug(patient)}-{TemplateFormat.FormatDate(authDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"HIPAA Authorization — {patient}");
    }
}
