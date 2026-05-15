using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Healthcare;

/// <summary>
/// Patient intake form — Word document collecting demographics,
/// insurance, medical history, current medications, allergies, and
/// reason for visit. Contains Protected Health Information (PHI) and
/// should be routed through the Trust Boundary's PHI-handling policy.
/// </summary>
public sealed class PatientIntakeTemplate : IDocumentTemplate
{
    public string Id => "healthcare/patient-intake";
    public string Name => "Patient Intake Form";
    public string Industry => "healthcare";
    public string Description => "Patient intake form with demographics, insurance, medical history, and medications.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "practice_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "intake_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "patient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "date_of_birth", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "sex", Type = TemplateFieldType.String, Required = false },
        new() { Name = "pronouns", Type = TemplateFieldType.String, Required = false },
        new() { Name = "address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "phone", Type = TemplateFieldType.String, Required = false },
        new() { Name = "email", Type = TemplateFieldType.String, Required = false },
        new() { Name = "emergency_contact", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "insurance_carrier", Type = TemplateFieldType.String, Required = false },
        new() { Name = "insurance_id", Type = TemplateFieldType.String, Required = false },
        new() { Name = "primary_care_provider", Type = TemplateFieldType.String, Required = false },
        new() { Name = "reason_for_visit", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "current_medications", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "allergies", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "medical_history", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "surgical_history", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "family_history", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "social_history", Type = TemplateFieldType.Text, Required = false, Description = "Tobacco, alcohol, substance use, exercise." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var practice = d.String("practice_name");
        var intakeDate = d.Date("intake_date");
        var patient = d.String("patient_name");
        var dob = d.Date("date_of_birth");

        var sb = new StringBuilder();
        sb.Append("# ").Append(practice).AppendLine();
        sb.AppendLine("## Patient Intake Form");
        sb.Append("**Date:** ").AppendLine(TemplateFormat.FormatDate(intakeDate));
        sb.AppendLine();

        sb.AppendLine("## Patient Information");
        sb.Append("**Name:** ").AppendLine(patient);
        sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        AppendIfPresent(sb, "Sex", d.String("sex"));
        AppendIfPresent(sb, "Pronouns", d.String("pronouns"));
        var address = d.String("address");
        if (!string.IsNullOrWhiteSpace(address)) { sb.AppendLine("**Address:**"); sb.AppendLine(address); }
        AppendIfPresent(sb, "Phone", d.String("phone"));
        AppendIfPresent(sb, "Email", d.String("email"));
        var emergency = d.String("emergency_contact");
        if (!string.IsNullOrWhiteSpace(emergency)) { sb.AppendLine("**Emergency contact:**"); sb.AppendLine(emergency); }
        sb.AppendLine();

        sb.AppendLine("## Insurance");
        AppendIfPresent(sb, "Carrier", d.String("insurance_carrier"));
        AppendIfPresent(sb, "Member ID", d.String("insurance_id"));
        AppendIfPresent(sb, "Primary care provider", d.String("primary_care_provider"));
        sb.AppendLine();

        sb.AppendLine("## Reason for Visit");
        sb.AppendLine(d.String("reason_for_visit"));
        sb.AppendLine();

        AppendListSection(sb, "Current Medications", d.StringArray("current_medications"), "None reported.");
        AppendListSection(sb, "Allergies", d.StringArray("allergies"), "No known allergies.");
        AppendListSection(sb, "Medical History", d.StringArray("medical_history"));
        AppendListSection(sb, "Surgical History", d.StringArray("surgical_history"));

        AppendTextSection(sb, "Family History", d.String("family_history"));
        AppendTextSection(sb, "Social History", d.String("social_history"));

        sb.AppendLine("## Acknowledgement");
        sb.AppendLine();
        sb.AppendLine("I attest that the information provided is accurate to the best of my knowledge. I understand it will be used to direct my care and will be handled under the practice's privacy policy and applicable law.");
        sb.AppendLine();
        sb.AppendLine("Patient signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This document contains Protected Health Information (PHI). Handle under HIPAA and the practice's privacy and retention policies. AI-generated intake drafts should be reviewed by clinical staff before being added to the patient record.*");

        var fileName = $"intake-{TemplateFormat.Slug(patient)}-{TemplateFormat.FormatDate(intakeDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Intake — {patient}");
    }

    private static void AppendIfPresent(StringBuilder sb, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.Append("**").Append(label).Append(":** ").AppendLine(value);
    }

    private static void AppendListSection(StringBuilder sb, string heading, IReadOnlyList<string> items, string? fallback = null)
    {
        sb.Append("## ").AppendLine(heading);
        if (items.Count == 0)
        {
            if (fallback is not null) sb.AppendLine(fallback);
        }
        else
        {
            foreach (var item in items) sb.Append("- ").AppendLine(item);
        }
        sb.AppendLine();
    }

    private static void AppendTextSection(StringBuilder sb, string heading, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.Append("## ").AppendLine(heading);
        sb.AppendLine(content);
        sb.AppendLine();
    }
}
