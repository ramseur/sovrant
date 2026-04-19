using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.RealEstate;

/// <summary>
/// Rental application — Word document collecting applicant identity,
/// residential history, employment, income, references, and required
/// authorizations (credit / background check, signature).
/// </summary>
public sealed class RentalApplicationTemplate : IDocumentTemplate
{
    public string Id => "real-estate/rental-application";
    public string Name => "Rental Application";
    public string Industry => "real-estate";
    public string Description => "Rental application capturing applicant info, income, history, and references.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "property_address", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "desired_move_in", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "monthly_rent", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "applicant_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "applicant_dob", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "applicant_email", Type = TemplateFieldType.String, Required = false },
        new() { Name = "applicant_phone", Type = TemplateFieldType.String, Required = false },
        new() { Name = "government_id", Type = TemplateFieldType.String, Required = false, Description = "Driver's license or similar — last 4 digits only." },
        new() { Name = "current_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "current_landlord_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "current_landlord_phone", Type = TemplateFieldType.String, Required = false },
        new() { Name = "reason_for_leaving", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "employer_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "position", Type = TemplateFieldType.String, Required = false },
        new() { Name = "employment_start", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "monthly_income", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "supervisor_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "supervisor_phone", Type = TemplateFieldType.String, Required = false },
        new() { Name = "additional_occupants", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "has_pets", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "pet_description", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "has_vehicles", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "vehicle_description", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "emergency_contact_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "emergency_contact_phone", Type = TemplateFieldType.String, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var property = d.String("property_address");
        var applicant = d.String("applicant_name");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.AppendLine("# Rental Application");
        sb.AppendLine();
        sb.AppendLine("## Property");
        sb.AppendLine(property);
        var moveIn = d.Date("desired_move_in");
        if (moveIn is not null)
            sb.Append("**Desired move-in:** ").AppendLine(TemplateFormat.FormatDate(moveIn));
        if (d.Has("monthly_rent"))
            sb.Append("**Monthly rent:** ").AppendLine(TemplateFormat.FormatMoney(d.Decimal("monthly_rent"), currency));
        sb.AppendLine();

        sb.AppendLine("## Applicant");
        sb.Append("**Name:** ").AppendLine(applicant);
        var dob = d.Date("applicant_dob");
        if (dob is not null) sb.Append("**Date of birth:** ").AppendLine(TemplateFormat.FormatDate(dob));
        var email = d.String("applicant_email");
        if (!string.IsNullOrWhiteSpace(email)) sb.Append("**Email:** ").AppendLine(email);
        var phone = d.String("applicant_phone");
        if (!string.IsNullOrWhiteSpace(phone)) sb.Append("**Phone:** ").AppendLine(phone);
        var id = d.String("government_id");
        if (!string.IsNullOrWhiteSpace(id)) sb.Append("**Gov't ID (last 4):** ").AppendLine(id);
        sb.AppendLine();

        sb.AppendLine("## Current Residence");
        var currentAddr = d.String("current_address");
        if (!string.IsNullOrWhiteSpace(currentAddr)) sb.AppendLine(currentAddr);
        var currentLandlord = d.String("current_landlord_name");
        if (!string.IsNullOrWhiteSpace(currentLandlord))
            sb.Append("**Landlord:** ").AppendLine(currentLandlord);
        var landlordPhone = d.String("current_landlord_phone");
        if (!string.IsNullOrWhiteSpace(landlordPhone))
            sb.Append("**Landlord phone:** ").AppendLine(landlordPhone);
        var leaving = d.String("reason_for_leaving");
        if (!string.IsNullOrWhiteSpace(leaving))
            sb.Append("**Reason for leaving:** ").AppendLine(leaving);
        sb.AppendLine();

        sb.AppendLine("## Employment");
        var employer = d.String("employer_name");
        if (!string.IsNullOrWhiteSpace(employer)) sb.Append("**Employer:** ").AppendLine(employer);
        var position = d.String("position");
        if (!string.IsNullOrWhiteSpace(position)) sb.Append("**Position:** ").AppendLine(position);
        var empStart = d.Date("employment_start");
        if (empStart is not null) sb.Append("**Start date:** ").AppendLine(TemplateFormat.FormatDate(empStart));
        if (d.Has("monthly_income"))
            sb.Append("**Monthly income:** ").AppendLine(TemplateFormat.FormatMoney(d.Decimal("monthly_income"), currency));
        var supervisor = d.String("supervisor_name");
        if (!string.IsNullOrWhiteSpace(supervisor)) sb.Append("**Supervisor:** ").AppendLine(supervisor);
        var supervisorPhone = d.String("supervisor_phone");
        if (!string.IsNullOrWhiteSpace(supervisorPhone)) sb.Append("**Supervisor phone:** ").AppendLine(supervisorPhone);
        sb.AppendLine();

        var occupants = d.StringArray("additional_occupants");
        if (occupants.Count > 0)
        {
            sb.AppendLine("## Additional Occupants");
            foreach (var o in occupants) sb.Append("- ").AppendLine(o);
            sb.AppendLine();
        }

        if (d.Has("has_pets") || !string.IsNullOrWhiteSpace(d.String("pet_description")))
        {
            sb.AppendLine("## Pets");
            sb.Append("**Has pets:** ").AppendLine(d.Boolean("has_pets") ? "Yes" : "No");
            var pets = d.String("pet_description");
            if (!string.IsNullOrWhiteSpace(pets)) sb.AppendLine(pets);
            sb.AppendLine();
        }

        if (d.Has("has_vehicles") || !string.IsNullOrWhiteSpace(d.String("vehicle_description")))
        {
            sb.AppendLine("## Vehicles");
            sb.Append("**Has vehicles:** ").AppendLine(d.Boolean("has_vehicles") ? "Yes" : "No");
            var veh = d.String("vehicle_description");
            if (!string.IsNullOrWhiteSpace(veh)) sb.AppendLine(veh);
            sb.AppendLine();
        }

        var ecName = d.String("emergency_contact_name");
        if (!string.IsNullOrWhiteSpace(ecName))
        {
            sb.AppendLine("## Emergency Contact");
            sb.Append("**Name:** ").AppendLine(ecName);
            var ecPhone = d.String("emergency_contact_phone");
            if (!string.IsNullOrWhiteSpace(ecPhone)) sb.Append("**Phone:** ").AppendLine(ecPhone);
            sb.AppendLine();
        }

        sb.AppendLine("## Authorization and Signature");
        sb.AppendLine();
        sb.AppendLine("I authorize the landlord or property manager to verify the information provided, including contacting current and previous landlords and employers, and to obtain a consumer credit report and background check in connection with this application. I certify that the information above is true and complete to the best of my knowledge.");
        sb.AppendLine();
        sb.Append("Applicant: ").AppendLine(applicant);
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        var fileName = $"rental-application-{TemplateFormat.Slug(applicant)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Rental Application — {applicant}");
    }
}
