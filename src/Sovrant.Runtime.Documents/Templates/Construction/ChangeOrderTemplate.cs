using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Construction;

/// <summary>
/// Construction change order — Word document modifying the contract
/// scope, cost, or schedule of an existing project. Requires signed
/// acceptance from owner and contractor before work proceeds.
/// </summary>
public sealed class ChangeOrderTemplate : IDocumentTemplate
{
    public string Id => "construction/change-order";
    public string Name => "Change Order";
    public string Industry => "construction";
    public string Description => "Construction change order adjusting scope, cost, and schedule.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "project_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "project_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "owner_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "contractor_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "original_contract_number", Type = TemplateFieldType.String, Required = false },
        new() { Name = "original_contract_date", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "change_order_number", Type = TemplateFieldType.String, Required = true },
        new() { Name = "change_order_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "reason", Type = TemplateFieldType.Text, Required = true, Description = "Why the change is needed." },
        new() { Name = "description_of_change", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "original_contract_sum", Type = TemplateFieldType.Currency, Required = true },
        new() { Name = "net_change_prior_orders", Type = TemplateFieldType.Currency, Required = false, Description = "Sum of approved change orders to date." },
        new() { Name = "this_change_amount", Type = TemplateFieldType.Currency, Required = true, Description = "Positive for additions, negative for deductions." },
        new() { Name = "original_completion_date", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "days_added", Type = TemplateFieldType.Integer, Required = false, Description = "Calendar days added to completion." },
        new() { Name = "new_completion_date", Type = TemplateFieldType.Date, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var project = d.String("project_name");
        var owner = d.String("owner_name");
        var contractor = d.String("contractor_name");
        var coNumber = d.String("change_order_number");
        var coDate = d.Date("change_order_date");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency"));

        var original = d.Decimal("original_contract_sum");
        var priorNet = d.Decimal("net_change_prior_orders");
        var thisChange = d.Decimal("this_change_amount");
        var priorContract = original + priorNet;
        var newContract = priorContract + thisChange;

        var sb = new StringBuilder();
        sb.AppendLine("# Change Order");
        sb.AppendLine();
        sb.Append("**Change order no.:** ").AppendLine(coNumber);
        sb.Append("**Date:** ").AppendLine(TemplateFormat.FormatDate(coDate));
        sb.Append("**Project:** ").AppendLine(project);
        var addr = d.String("project_address");
        if (!string.IsNullOrWhiteSpace(addr))
        {
            sb.AppendLine("**Location:**");
            sb.AppendLine(addr);
        }
        sb.Append("**Owner:** ").AppendLine(owner);
        sb.Append("**Contractor:** ").AppendLine(contractor);
        var contractNumber = d.String("original_contract_number");
        if (!string.IsNullOrWhiteSpace(contractNumber)) sb.Append("**Original contract no.:** ").AppendLine(contractNumber);
        var contractDate = d.Date("original_contract_date");
        if (contractDate is not null) sb.Append("**Original contract date:** ").AppendLine(TemplateFormat.FormatDate(contractDate));
        sb.AppendLine();

        sb.AppendLine("## Reason for Change");
        sb.AppendLine(d.String("reason"));
        sb.AppendLine();

        sb.AppendLine("## Description of Change");
        sb.AppendLine(d.String("description_of_change"));
        sb.AppendLine();

        sb.AppendLine("## Cost Impact");
        sb.AppendLine();
        sb.AppendLine("| Item | Amount |");
        sb.AppendLine("|------|-------:|");
        sb.Append("| Original contract sum | ").Append(TemplateFormat.FormatMoney(original, currency)).AppendLine(" |");
        sb.Append("| Net change by prior change orders | ").Append(TemplateFormat.FormatMoney(priorNet, currency)).AppendLine(" |");
        sb.Append("| Contract sum prior to this change | ").Append(TemplateFormat.FormatMoney(priorContract, currency)).AppendLine(" |");
        sb.Append("| This change order | ").Append(TemplateFormat.FormatMoney(thisChange, currency)).AppendLine(" |");
        sb.Append("| **New contract sum** | **").Append(TemplateFormat.FormatMoney(newContract, currency)).AppendLine("** |");
        sb.AppendLine();

        var origCompletion = d.Date("original_completion_date");
        var newCompletion = d.Date("new_completion_date");
        var daysAdded = d.Integer("days_added");
        if (origCompletion is not null || newCompletion is not null || daysAdded != 0)
        {
            sb.AppendLine("## Schedule Impact");
            if (origCompletion is not null) sb.Append("- Original completion date: ").AppendLine(TemplateFormat.FormatDate(origCompletion));
            if (daysAdded != 0)
            {
                sb.Append("- Days ").Append(daysAdded >= 0 ? "added" : "reduced").Append(": ")
                  .AppendLine(Math.Abs(daysAdded).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (newCompletion is not null) sb.Append("- New completion date: ").AppendLine(TemplateFormat.FormatDate(newCompletion));
            sb.AppendLine();
        }

        sb.AppendLine("## Approval");
        sb.AppendLine();
        sb.AppendLine("By signing below, the parties agree that this change order is incorporated into the referenced contract and supersedes any conflicting prior agreements on the same subject matter. Work authorized by this change order shall not commence until all required signatures are obtained.");
        sb.AppendLine();
        sb.Append("**Owner:** ").AppendLine(owner);
        sb.AppendLine("Signature: ____________________________  Date: ____________");
        sb.AppendLine();
        sb.Append("**Contractor:** ").AppendLine(contractor);
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        var fileName = $"change-order-{TemplateFormat.Slug(project)}-{TemplateFormat.Slug(coNumber)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Change Order {coNumber} — {project}");
    }
}
