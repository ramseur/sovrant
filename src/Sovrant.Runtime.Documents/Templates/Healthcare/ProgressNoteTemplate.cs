using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Healthcare;

/// <summary>
/// Progress note (SOAP format) — Word document capturing a single
/// clinical encounter as Subjective, Objective, Assessment, Plan.
/// Contains PHI and must be reviewed and signed by the responsible
/// clinician before being entered into the medical record.
/// </summary>
public sealed class ProgressNoteTemplate : IDocumentTemplate
{
    public string Id => "healthcare/progress-note";
    public string Name => "Progress Note (SOAP)";
    public string Industry => "healthcare";
    public string Description => "SOAP-format progress note for a single clinical encounter.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "patient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "mrn", Type = TemplateFieldType.String, Required = false },
        new() { Name = "date_of_birth", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "encounter_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "provider_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "provider_role", Type = TemplateFieldType.String, Required = false, Description = "e.g. MD, DO, NP, PA." },
        new() { Name = "encounter_type", Type = TemplateFieldType.String, Required = false, Description = "e.g. office visit, telehealth, follow-up." },
        new() { Name = "chief_complaint", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "subjective", Type = TemplateFieldType.Text, Required = true, Description = "History, symptoms, patient report." },
        new() { Name = "objective", Type = TemplateFieldType.Text, Required = true, Description = "Exam findings, vital signs, lab/imaging results." },
        new() { Name = "vital_signs", Type = TemplateFieldType.StringArray, Required = false, Description = "e.g. 'BP 120/80', 'HR 72', 'Temp 98.6F'." },
        new() { Name = "assessment", Type = TemplateFieldType.Text, Required = true, Description = "Clinical impression / differential." },
        new() { Name = "plan", Type = TemplateFieldType.Text, Required = true, Description = "Interventions, prescriptions, follow-up." },
        new() { Name = "medications_prescribed", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "follow_up", Type = TemplateFieldType.String, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var patient = d.String("patient_name");
        var encounter = d.Date("encounter_date");
        var provider = d.String("provider_name");

        var sb = new StringBuilder();
        sb.AppendLine("# Progress Note");
        sb.AppendLine();
        sb.Append("**Patient:** ").AppendLine(patient);
        var mrn = d.String("mrn");
        if (!string.IsNullOrWhiteSpace(mrn)) sb.Append("**MRN:** ").AppendLine(mrn);
        var dob = d.Date("date_of_birth");
        if (dob is not null) sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        sb.Append("**Encounter date:** ").AppendLine(TemplateFormat.FormatDate(encounter));
        var encounterType = d.String("encounter_type");
        if (!string.IsNullOrWhiteSpace(encounterType)) sb.Append("**Encounter type:** ").AppendLine(encounterType);
        sb.Append("**Provider:** ").Append(provider);
        var providerRole = d.String("provider_role");
        if (!string.IsNullOrWhiteSpace(providerRole)) sb.Append(", ").Append(providerRole);
        sb.AppendLine();
        sb.AppendLine();

        sb.AppendLine("## Chief Complaint");
        sb.AppendLine(d.String("chief_complaint"));
        sb.AppendLine();

        sb.AppendLine("## S — Subjective");
        sb.AppendLine(d.String("subjective"));
        sb.AppendLine();

        sb.AppendLine("## O — Objective");
        var vitals = d.StringArray("vital_signs");
        if (vitals.Count > 0)
        {
            sb.AppendLine("**Vital signs:**");
            foreach (var v in vitals) sb.Append("- ").AppendLine(v);
            sb.AppendLine();
        }
        sb.AppendLine(d.String("objective"));
        sb.AppendLine();

        sb.AppendLine("## A — Assessment");
        sb.AppendLine(d.String("assessment"));
        sb.AppendLine();

        sb.AppendLine("## P — Plan");
        sb.AppendLine(d.String("plan"));
        sb.AppendLine();

        var meds = d.StringArray("medications_prescribed");
        if (meds.Count > 0)
        {
            sb.AppendLine("### Medications Prescribed");
            foreach (var m in meds) sb.Append("- ").AppendLine(m);
            sb.AppendLine();
        }

        var followUp = d.String("follow_up");
        if (!string.IsNullOrWhiteSpace(followUp))
        {
            sb.AppendLine("### Follow-Up");
            sb.AppendLine(followUp);
            sb.AppendLine();
        }

        sb.AppendLine("## Signature");
        sb.AppendLine();
        sb.Append(provider);
        if (!string.IsNullOrWhiteSpace(providerRole)) sb.Append(", ").Append(providerRole);
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This progress note contains Protected Health Information (PHI). AI-generated drafts must be reviewed, corrected, and signed by the responsible clinician before being entered into the medical record.*");

        var fileName = $"progress-note-{TemplateFormat.Slug(patient)}-{TemplateFormat.FormatDate(encounter)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Progress Note — {patient}");
    }
}
