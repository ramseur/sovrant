using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Healthcare;

/// <summary>
/// Referral letter — Word document from a referring provider to a
/// specialist or consulting provider. Includes reason for referral,
/// clinical summary, relevant history, and urgency. Contains PHI.
/// </summary>
public sealed class ReferralLetterTemplate : IDocumentTemplate
{
    public string Id => "healthcare/referral-letter";
    public string Name => "Referral Letter";
    public string Industry => "healthcare";
    public string Description => "Provider-to-provider referral letter with clinical summary and urgency.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "letter_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "referring_provider_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "referring_provider_role", Type = TemplateFieldType.String, Required = false },
        new() { Name = "referring_practice", Type = TemplateFieldType.String, Required = false },
        new() { Name = "referring_contact", Type = TemplateFieldType.Text, Required = false, Description = "Address, phone, fax, email." },
        new() { Name = "receiving_provider_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "receiving_provider_specialty", Type = TemplateFieldType.String, Required = false },
        new() { Name = "receiving_practice", Type = TemplateFieldType.String, Required = false },
        new() { Name = "patient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "patient_dob", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "mrn", Type = TemplateFieldType.String, Required = false },
        new() { Name = "insurance", Type = TemplateFieldType.String, Required = false },
        new() { Name = "reason_for_referral", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "urgency", Type = TemplateFieldType.String, Required = false, Description = "e.g. Routine, Urgent, Stat." },
        new() { Name = "relevant_diagnoses", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "clinical_summary", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "current_medications", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "allergies", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "prior_workup", Type = TemplateFieldType.Text, Required = false, Description = "Labs, imaging, prior consultations." },
        new() { Name = "requested_services", Type = TemplateFieldType.Text, Required = false, Description = "What you are asking the receiving provider to do." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var letterDate = d.Date("letter_date");
        var referring = d.String("referring_provider_name");
        var referringRole = d.String("referring_provider_role");
        var receiving = d.String("receiving_provider_name");
        var receivingSpecialty = d.String("receiving_provider_specialty");
        var patient = d.String("patient_name");
        var dob = d.Date("patient_dob");

        var sb = new StringBuilder();

        var referringPractice = d.String("referring_practice");
        if (!string.IsNullOrWhiteSpace(referringPractice))
        {
            sb.Append("**").Append(referringPractice).AppendLine("**");
        }
        var referringContact = d.String("referring_contact");
        if (!string.IsNullOrWhiteSpace(referringContact)) sb.AppendLine(referringContact);
        sb.AppendLine();

        sb.Append(TemplateFormat.FormatDate(letterDate)).AppendLine();
        sb.AppendLine();

        sb.Append(receiving);
        if (!string.IsNullOrWhiteSpace(receivingSpecialty)) sb.Append(", ").Append(receivingSpecialty);
        sb.AppendLine();
        var receivingPractice = d.String("receiving_practice");
        if (!string.IsNullOrWhiteSpace(receivingPractice)) sb.AppendLine(receivingPractice);
        sb.AppendLine();

        sb.Append("Dear ").Append(receiving).AppendLine(",");
        sb.AppendLine();

        var urgency = d.String("urgency");
        sb.Append("I am referring **").Append(patient).Append("** (DOB ").Append(TemplateFormat.FormatDate(dob)).Append(')');
        if (!string.IsNullOrWhiteSpace(urgency)) sb.Append(" for a **").Append(urgency).Append("** evaluation");
        sb.AppendLine(". Reason for referral:");
        sb.AppendLine();
        sb.AppendLine(d.String("reason_for_referral"));
        sb.AppendLine();

        sb.AppendLine("## Patient Information");
        sb.Append("**Name:** ").AppendLine(patient);
        sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        var mrn = d.String("mrn");
        if (!string.IsNullOrWhiteSpace(mrn)) sb.Append("**MRN:** ").AppendLine(mrn);
        var insurance = d.String("insurance");
        if (!string.IsNullOrWhiteSpace(insurance)) sb.Append("**Insurance:** ").AppendLine(insurance);
        sb.AppendLine();

        var diagnoses = d.StringArray("relevant_diagnoses");
        if (diagnoses.Count > 0)
        {
            sb.AppendLine("## Relevant Diagnoses");
            foreach (var dx in diagnoses) sb.Append("- ").AppendLine(dx);
            sb.AppendLine();
        }

        sb.AppendLine("## Clinical Summary");
        sb.AppendLine(d.String("clinical_summary"));
        sb.AppendLine();

        var meds = d.StringArray("current_medications");
        if (meds.Count > 0)
        {
            sb.AppendLine("## Current Medications");
            foreach (var m in meds) sb.Append("- ").AppendLine(m);
            sb.AppendLine();
        }

        var allergies = d.StringArray("allergies");
        if (allergies.Count > 0)
        {
            sb.AppendLine("## Allergies");
            foreach (var a in allergies) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var prior = d.String("prior_workup");
        if (!string.IsNullOrWhiteSpace(prior))
        {
            sb.AppendLine("## Prior Workup");
            sb.AppendLine(prior);
            sb.AppendLine();
        }

        var requested = d.String("requested_services");
        if (!string.IsNullOrWhiteSpace(requested))
        {
            sb.AppendLine("## Requested Services");
            sb.AppendLine(requested);
            sb.AppendLine();
        }

        sb.AppendLine("Thank you for seeing this patient. Please forward your consultation report to my office at your earliest convenience. Do not hesitate to contact me with any questions.");
        sb.AppendLine();
        sb.AppendLine("Sincerely,");
        sb.AppendLine();
        sb.Append(referring);
        if (!string.IsNullOrWhiteSpace(referringRole)) sb.Append(", ").Append(referringRole);
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This referral letter contains Protected Health Information (PHI). Transmit only through HIPAA-compliant channels. AI-generated drafts must be reviewed and signed by the referring clinician before being sent.*");

        var fileName = $"referral-{TemplateFormat.Slug(patient)}-{TemplateFormat.FormatDate(letterDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Referral — {patient}");
    }
}
