using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Healthcare;

/// <summary>
/// Discharge summary — Word document summarizing an inpatient stay:
/// admission/discharge dates, diagnoses, hospital course, medications
/// at discharge, follow-up, and discharge instructions. Contains PHI.
/// </summary>
public sealed class DischargeSummaryTemplate : IDocumentTemplate
{
    public string Id => "healthcare/discharge-summary";
    public string Name => "Discharge Summary";
    public string Industry => "healthcare";
    public string Description => "Inpatient discharge summary with hospital course, meds, and follow-up.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "patient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "mrn", Type = TemplateFieldType.String, Required = false },
        new() { Name = "admission_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "discharge_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "attending_physician", Type = TemplateFieldType.String, Required = true },
        new() { Name = "admission_diagnosis", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "discharge_diagnosis", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "hospital_course", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "procedures", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "discharge_medications", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "discharge_condition", Type = TemplateFieldType.String, Required = false, Description = "e.g. Stable, Improved." },
        new() { Name = "discharge_instructions", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "follow_up_appointments", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "pending_results", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var patient = d.String("patient_name");
        var admit = d.Date("admission_date");
        var discharge = d.Date("discharge_date");
        var attending = d.String("attending_physician");

        var sb = new StringBuilder();
        sb.AppendLine("# Discharge Summary");
        sb.AppendLine();
        sb.Append("**Patient:** ").AppendLine(patient);
        var mrn = d.String("mrn");
        if (!string.IsNullOrWhiteSpace(mrn)) sb.Append("**MRN:** ").AppendLine(mrn);
        sb.Append("**Admission date:** ").AppendLine(TemplateFormat.FormatDate(admit));
        sb.Append("**Discharge date:** ").AppendLine(TemplateFormat.FormatDate(discharge));
        sb.Append("**Attending physician:** ").AppendLine(attending);
        var condition = d.String("discharge_condition");
        if (!string.IsNullOrWhiteSpace(condition)) sb.Append("**Condition at discharge:** ").AppendLine(condition);
        sb.AppendLine();

        sb.AppendLine("## Admission Diagnosis");
        sb.AppendLine(d.String("admission_diagnosis"));
        sb.AppendLine();

        sb.AppendLine("## Discharge Diagnosis");
        sb.AppendLine(d.String("discharge_diagnosis"));
        sb.AppendLine();

        sb.AppendLine("## Hospital Course");
        sb.AppendLine(d.String("hospital_course"));
        sb.AppendLine();

        var procedures = d.StringArray("procedures");
        if (procedures.Count > 0)
        {
            sb.AppendLine("## Procedures Performed");
            foreach (var p in procedures) sb.Append("- ").AppendLine(p);
            sb.AppendLine();
        }

        var meds = d.StringArray("discharge_medications");
        if (meds.Count > 0)
        {
            sb.AppendLine("## Discharge Medications");
            foreach (var m in meds) sb.Append("- ").AppendLine(m);
            sb.AppendLine();
        }

        sb.AppendLine("## Discharge Instructions");
        sb.AppendLine(d.String("discharge_instructions"));
        sb.AppendLine();

        var followUps = d.StringArray("follow_up_appointments");
        if (followUps.Count > 0)
        {
            sb.AppendLine("## Follow-Up Appointments");
            foreach (var f in followUps) sb.Append("- ").AppendLine(f);
            sb.AppendLine();
        }

        var pending = d.String("pending_results");
        if (!string.IsNullOrWhiteSpace(pending))
        {
            sb.AppendLine("## Pending Results at Discharge");
            sb.AppendLine(pending);
            sb.AppendLine();
        }

        sb.AppendLine("## Attestation");
        sb.AppendLine();
        sb.Append(attending).AppendLine();
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This discharge summary contains Protected Health Information (PHI). AI-generated drafts must be reviewed, corrected, and signed by the responsible clinician before finalization.*");

        var fileName = $"discharge-{TemplateFormat.Slug(patient)}-{TemplateFormat.FormatDate(discharge)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Discharge Summary — {patient}");
    }
}
