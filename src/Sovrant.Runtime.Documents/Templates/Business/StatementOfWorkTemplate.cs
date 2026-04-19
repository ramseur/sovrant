using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Business;

/// <summary>
/// Statement of Work (SOW) — Word document describing scope,
/// deliverables with acceptance criteria, milestones, pricing, and
/// sign-off. Meant to be executed alongside or as an attachment to a
/// master service agreement.
/// </summary>
public sealed class StatementOfWorkTemplate : IDocumentTemplate
{
    public string Id => "business/sow";
    public string Name => "Statement of Work";
    public string Industry => "business";
    public string Description => "Statement of Work with scope, deliverables, milestones, and acceptance criteria.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "sow_number", Type = TemplateFieldType.String, Required = true },
        new() { Name = "effective_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "client_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "vendor_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "msa_reference", Type = TemplateFieldType.String, Required = false, Description = "Reference to the governing master service agreement." },
        new() { Name = "background", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "scope", Type = TemplateFieldType.Text, Required = true },
        new()
        {
            Name = "deliverables",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "name", Type = TemplateFieldType.String, Required = true },
                new() { Name = "description", Type = TemplateFieldType.Text, Required = false },
                new() { Name = "acceptance_criteria", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new()
        {
            Name = "milestones",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "name", Type = TemplateFieldType.String, Required = true },
                new() { Name = "target_date", Type = TemplateFieldType.Date, Required = false },
                new() { Name = "payment_percent", Type = TemplateFieldType.Decimal, Required = false },
            },
        },
        new() { Name = "total_fee", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "assumptions", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "out_of_scope", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var sowNumber = d.String("sow_number");
        var effectiveDate = d.Date("effective_date");
        var client = d.String("client_name");
        var vendor = d.String("vendor_name");
        var msa = d.String("msa_reference");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.Append("# Statement of Work ").AppendLine(sowNumber);
        sb.AppendLine();
        sb.Append("**Effective date:** ").AppendLine(TemplateFormat.FormatDate(effectiveDate));
        sb.Append("**Client:** ").AppendLine(client);
        sb.Append("**Vendor:** ").AppendLine(vendor);
        if (!string.IsNullOrWhiteSpace(msa))
            sb.Append("**MSA reference:** ").AppendLine(msa);
        sb.AppendLine();

        var background = d.String("background");
        if (!string.IsNullOrWhiteSpace(background))
        {
            sb.AppendLine("## Background");
            sb.AppendLine(background);
            sb.AppendLine();
        }

        sb.AppendLine("## Scope of Work");
        sb.AppendLine(d.String("scope"));
        sb.AppendLine();

        sb.AppendLine("## Deliverables");
        sb.AppendLine();
        var delIndex = 1;
        foreach (var del in d.ObjectArray("deliverables"))
        {
            sb.Append("**").Append(delIndex).Append(". ").Append(TemplateFormat.GetString(del, "name")).AppendLine("**");
            sb.AppendLine();
            var desc = TemplateFormat.GetString(del, "description");
            if (!string.IsNullOrWhiteSpace(desc)) { sb.AppendLine(desc); sb.AppendLine(); }
            var accept = TemplateFormat.GetString(del, "acceptance_criteria");
            if (!string.IsNullOrWhiteSpace(accept))
            {
                sb.Append("*Acceptance criteria:* ").AppendLine(accept);
                sb.AppendLine();
            }
            delIndex++;
        }

        var milestones = d.ObjectArray("milestones");
        if (milestones.Count > 0)
        {
            sb.AppendLine("## Milestones");
            sb.AppendLine();
            sb.AppendLine("| Milestone | Target date | Payment |");
            sb.AppendLine("|---|---|---:|");
            foreach (var m in milestones)
            {
                var name = TemplateFormat.GetString(m, "name");
                var date = TemplateFormat.GetDate(m, "target_date");
                var pct = TemplateFormat.GetDecimal(m, "payment_percent");
                sb.Append("| ").Append(TemplateFormat.EscapePipes(name))
                  .Append(" | ").Append(date is not null ? TemplateFormat.FormatDate(date) : "—")
                  .Append(" | ").Append(pct > 0m ? TemplateFormat.FormatPercent(pct) : "—")
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        if (d.Has("total_fee"))
        {
            sb.AppendLine("## Total Fee");
            sb.Append(TemplateFormat.FormatMoney(d.Decimal("total_fee"), currency)).AppendLine();
            sb.AppendLine();
        }

        var assumptions = d.String("assumptions");
        if (!string.IsNullOrWhiteSpace(assumptions))
        {
            sb.AppendLine("## Assumptions");
            sb.AppendLine(assumptions);
            sb.AppendLine();
        }

        var oos = d.String("out_of_scope");
        if (!string.IsNullOrWhiteSpace(oos))
        {
            sb.AppendLine("## Out of Scope");
            sb.AppendLine(oos);
            sb.AppendLine();
        }

        sb.AppendLine("## Signatures");
        sb.AppendLine();
        sb.Append("**").Append(client).AppendLine("**");
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine("Name / Title: ____________________________");
        sb.AppendLine("Date: ____________________________");
        sb.AppendLine();
        sb.Append("**").Append(vendor).AppendLine("**");
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine("Name / Title: ____________________________");
        sb.AppendLine("Date: ____________________________");

        var fileName = $"sow-{TemplateFormat.Slug(sowNumber)}-{TemplateFormat.Slug(client)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"SOW {sowNumber}");
    }
}
