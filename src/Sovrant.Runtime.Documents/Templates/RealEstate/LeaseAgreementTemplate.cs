using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.RealEstate;

/// <summary>
/// Residential lease agreement — Word document for a tenant-landlord
/// relationship. Covers parties, premises, term, rent, deposit, and
/// house rules.
/// </summary>
public sealed class LeaseAgreementTemplate : IDocumentTemplate
{
    public string Id => "real-estate/lease-agreement";
    public string Name => "Lease Agreement";
    public string Industry => "real-estate";
    public string Description => "Residential lease agreement with rent, deposit, term, and rules.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "landlord_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "tenant_names", Type = TemplateFieldType.StringArray, Required = true },
        new() { Name = "premises_address", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "lease_start", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "lease_end", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "monthly_rent", Type = TemplateFieldType.Currency, Required = true },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "rent_due_day", Type = TemplateFieldType.Integer, Required = false, Description = "Day of month rent is due (default 1)." },
        new() { Name = "security_deposit", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "late_fee", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "utilities_included", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "utilities_tenant_pays", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "pets_allowed", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "smoking_allowed", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "house_rules", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "governing_state", Type = TemplateFieldType.String, Required = true },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var landlord = d.String("landlord_name");
        var tenants = d.StringArray("tenant_names");
        var premises = d.String("premises_address");
        var start = d.Date("lease_start");
        var end = d.Date("lease_end");
        var rent = d.Decimal("monthly_rent");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));
        var dueDay = d.Integer("rent_due_day", 1);
        var state = d.String("governing_state");
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.AppendLine("# Residential Lease Agreement");
        sb.AppendLine();
        sb.Append("This Lease Agreement is made between **").Append(landlord).AppendLine("** (\"Landlord\") and the tenant(s) listed below (\"Tenant\").");
        sb.AppendLine();

        sb.AppendLine("## 1. Tenants");
        foreach (var t in tenants) sb.Append("- ").AppendLine(t);
        sb.AppendLine();

        sb.AppendLine("## 2. Premises");
        sb.AppendLine();
        sb.AppendLine("Landlord leases to Tenant the premises located at:");
        sb.AppendLine();
        sb.AppendLine(premises);
        sb.AppendLine();

        sb.AppendLine("## 3. Term");
        sb.AppendLine();
        sb.Append("The lease term begins on **").Append(TemplateFormat.FormatDate(start))
          .Append("** and ends on **").Append(TemplateFormat.FormatDate(end)).AppendLine("**.");
        sb.AppendLine();

        sb.AppendLine("## 4. Rent");
        sb.AppendLine();
        sb.Append("Tenant shall pay Landlord monthly rent of **").Append(TemplateFormat.FormatMoney(rent, currency))
          .Append("**, due on the ").Append(dueDay).AppendLine(Ordinal(dueDay) + " day of each month.");
        if (d.Has("late_fee"))
        {
            sb.Append("A late fee of **").Append(TemplateFormat.FormatMoney(d.Decimal("late_fee"), currency))
              .AppendLine("** shall apply to payments more than five days late.");
        }
        sb.AppendLine();

        if (d.Has("security_deposit"))
        {
            sb.AppendLine("## 5. Security Deposit");
            sb.AppendLine();
            sb.Append("Tenant shall pay a security deposit of **").Append(TemplateFormat.FormatMoney(d.Decimal("security_deposit"), currency))
              .AppendLine("**, to be held and returned in accordance with applicable law.");
            sb.AppendLine();
        }

        var included = d.StringArray("utilities_included");
        var tenantPays = d.StringArray("utilities_tenant_pays");
        if (included.Count > 0 || tenantPays.Count > 0)
        {
            sb.AppendLine("## 6. Utilities");
            sb.AppendLine();
            if (included.Count > 0)
            {
                sb.AppendLine("**Included in rent:**");
                foreach (var u in included) sb.Append("- ").AppendLine(u);
                sb.AppendLine();
            }
            if (tenantPays.Count > 0)
            {
                sb.AppendLine("**Paid by Tenant:**");
                foreach (var u in tenantPays) sb.Append("- ").AppendLine(u);
                sb.AppendLine();
            }
        }

        sb.AppendLine("## 7. Occupancy and Use");
        sb.AppendLine();
        sb.AppendLine("The premises shall be used solely as a private residence. Tenant shall not engage in any illegal activity on the premises.");
        sb.AppendLine();
        sb.Append("- Pets: ").AppendLine(d.Has("pets_allowed") ? (d.Boolean("pets_allowed") ? "permitted" : "not permitted") : "by written consent of Landlord");
        sb.Append("- Smoking: ").AppendLine(d.Has("smoking_allowed") ? (d.Boolean("smoking_allowed") ? "permitted" : "not permitted") : "not permitted");
        sb.AppendLine();

        var rules = d.StringArray("house_rules");
        if (rules.Count > 0)
        {
            sb.AppendLine("## 8. House Rules");
            foreach (var r in rules) sb.Append("- ").AppendLine(r);
            sb.AppendLine();
        }

        sb.AppendLine("## 9. Maintenance and Repairs");
        sb.AppendLine();
        sb.AppendLine("Tenant shall keep the premises clean and in good condition and shall promptly notify Landlord of needed repairs. Landlord shall maintain the structural and major mechanical systems of the premises.");
        sb.AppendLine();

        sb.AppendLine("## 10. Governing Law");
        sb.AppendLine();
        sb.Append("This Agreement is governed by the laws of the State of **").Append(state).AppendLine("**.");
        sb.AppendLine();

        sb.AppendLine("## Signatures");
        sb.AppendLine();
        sb.Append("**Landlord:** ").AppendLine(landlord);
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        foreach (var t in tenants)
        {
            sb.Append("**Tenant:** ").AppendLine(t);
            sb.AppendLine("Signature: ____________________________  Date: ____________");
            sb.AppendLine();
        }

        if (showDisclaimer)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review for compliance with local landlord-tenant law before execution.*");
        }

        var fileName = $"lease-{TemplateFormat.Slug(landlord)}-{TemplateFormat.FormatDate(start)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Lease — {landlord}");
    }

    private static string Ordinal(int day) => (day % 100) switch
    {
        11 or 12 or 13 => "th",
        _ => (day % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        },
    };
}
