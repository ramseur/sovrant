using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Healthcare;

/// <summary>
/// Superbill — Word document itemizing services rendered with ICD-10
/// diagnosis codes and CPT procedure codes, provider information, and
/// fees. Used by patients to submit out-of-network claims and by
/// billers to post charges. Contains PHI.
/// </summary>
public sealed class SuperbillTemplate : IDocumentTemplate
{
    public string Id => "healthcare/superbill";
    public string Name => "Superbill";
    public string Industry => "healthcare";
    public string Description => "Itemized superbill with ICD-10 and CPT codes for insurance submission.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "practice_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "practice_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "practice_phone", Type = TemplateFieldType.String, Required = false },
        new() { Name = "practice_tax_id", Type = TemplateFieldType.String, Required = false, Description = "EIN / TIN." },
        new() { Name = "practice_npi", Type = TemplateFieldType.String, Required = false, Description = "National Provider Identifier." },
        new() { Name = "provider_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "provider_npi", Type = TemplateFieldType.String, Required = false },
        new() { Name = "patient_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "patient_dob", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "patient_id", Type = TemplateFieldType.String, Required = false, Description = "Insurance member ID or chart number." },
        new() { Name = "date_of_service", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "place_of_service", Type = TemplateFieldType.String, Required = false, Description = "e.g. 11 Office, 22 Outpatient Hospital." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new()
        {
            Name = "diagnoses",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "code", Type = TemplateFieldType.String, Required = true, Description = "ICD-10 code." },
                new() { Name = "description", Type = TemplateFieldType.String, Required = false },
            },
        },
        new()
        {
            Name = "charges",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "cpt_code", Type = TemplateFieldType.String, Required = true },
                new() { Name = "description", Type = TemplateFieldType.String, Required = false },
                new() { Name = "modifier", Type = TemplateFieldType.String, Required = false },
                new() { Name = "units", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "fee", Type = TemplateFieldType.Currency, Required = true },
                new() { Name = "diagnosis_pointer", Type = TemplateFieldType.String, Required = false, Description = "Which diagnosis line(s) this charge relates to, e.g. 1,2." },
            },
        },
        new() { Name = "amount_paid", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "notes", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var practice = d.String("practice_name");
        var provider = d.String("provider_name");
        var patient = d.String("patient_name");
        var dob = d.Date("patient_dob");
        var dos = d.Date("date_of_service");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency"));

        var sb = new StringBuilder();
        sb.AppendLine("# Superbill");
        sb.AppendLine();

        sb.Append("**").Append(practice).AppendLine("**");
        var practiceAddr = d.String("practice_address");
        if (!string.IsNullOrWhiteSpace(practiceAddr)) sb.AppendLine(practiceAddr);
        var phone = d.String("practice_phone");
        if (!string.IsNullOrWhiteSpace(phone)) sb.Append("Phone: ").AppendLine(phone);
        var tax = d.String("practice_tax_id");
        if (!string.IsNullOrWhiteSpace(tax)) sb.Append("Tax ID: ").AppendLine(tax);
        var npi = d.String("practice_npi");
        if (!string.IsNullOrWhiteSpace(npi)) sb.Append("Practice NPI: ").AppendLine(npi);
        sb.AppendLine();

        sb.AppendLine("## Provider");
        sb.AppendLine(provider);
        var providerNpi = d.String("provider_npi");
        if (!string.IsNullOrWhiteSpace(providerNpi)) sb.Append("NPI: ").AppendLine(providerNpi);
        sb.AppendLine();

        sb.AppendLine("## Patient");
        sb.Append("**Name:** ").AppendLine(patient);
        sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        var patientId = d.String("patient_id");
        if (!string.IsNullOrWhiteSpace(patientId)) sb.Append("**Member / Chart ID:** ").AppendLine(patientId);
        sb.Append("**Date of service:** ").AppendLine(TemplateFormat.FormatDate(dos));
        var pos = d.String("place_of_service");
        if (!string.IsNullOrWhiteSpace(pos)) sb.Append("**Place of service:** ").AppendLine(pos);
        sb.AppendLine();

        sb.AppendLine("## Diagnoses (ICD-10)");
        sb.AppendLine();
        sb.AppendLine("| # | Code | Description |");
        sb.AppendLine("|---|------|-------------|");
        var dxNum = 1;
        foreach (var dx in d.ObjectArray("diagnoses"))
        {
            var code = TemplateFormat.GetString(dx, "code");
            var desc = TemplateFormat.GetString(dx, "description");
            sb.Append("| ").Append(dxNum++).Append(" | ")
              .Append(TemplateFormat.EscapePipes(code)).Append(" | ")
              .Append(TemplateFormat.EscapePipes(desc)).AppendLine(" |");
        }
        sb.AppendLine();

        sb.AppendLine("## Charges");
        sb.AppendLine();
        sb.AppendLine("| CPT | Modifier | Description | Dx | Units | Fee |");
        sb.AppendLine("|-----|----------|-------------|----|-------|-----|");
        decimal total = 0m;
        foreach (var c in d.ObjectArray("charges"))
        {
            var cpt = TemplateFormat.GetString(c, "cpt_code");
            var modifier = TemplateFormat.GetString(c, "modifier");
            var desc = TemplateFormat.GetString(c, "description");
            var pointer = TemplateFormat.GetString(c, "diagnosis_pointer");
            var unitsRaw = TemplateFormat.GetInteger(c, "units");
            var units = unitsRaw > 0 ? unitsRaw : 1;
            var fee = TemplateFormat.GetDecimal(c, "fee");
            total += fee * units;

            sb.Append("| ").Append(TemplateFormat.EscapePipes(cpt))
              .Append(" | ").Append(TemplateFormat.EscapePipes(modifier))
              .Append(" | ").Append(TemplateFormat.EscapePipes(desc))
              .Append(" | ").Append(TemplateFormat.EscapePipes(pointer))
              .Append(" | ").Append(units)
              .Append(" | ").Append(TemplateFormat.FormatMoney(fee, currency))
              .AppendLine(" |");
        }
        sb.AppendLine();
        sb.Append("**Total charges:** ").AppendLine(TemplateFormat.FormatMoney(total, currency));
        if (d.Has("amount_paid"))
        {
            var paid = d.Decimal("amount_paid");
            sb.Append("**Amount paid by patient:** ").AppendLine(TemplateFormat.FormatMoney(paid, currency));
            sb.Append("**Balance:** ").AppendLine(TemplateFormat.FormatMoney(total - paid, currency));
        }
        sb.AppendLine();

        var notes = d.String("notes");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine(notes);
            sb.AppendLine();
        }

        sb.AppendLine("## Provider Certification");
        sb.AppendLine();
        sb.AppendLine("I certify that the services listed above were medically necessary and were personally rendered by me or under my direct supervision.");
        sb.AppendLine();
        sb.Append(provider).AppendLine();
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This superbill contains Protected Health Information (PHI). Verify all codes, modifiers, and fees against the medical record and current coding guidelines before submitting to a payer. AI-generated drafts are not a substitute for certified coder review.*");

        var fileName = $"superbill-{TemplateFormat.Slug(patient)}-{TemplateFormat.FormatDate(dos)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Superbill — {patient}");
    }
}
