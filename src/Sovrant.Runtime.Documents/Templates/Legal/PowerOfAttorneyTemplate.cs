using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Legal;

/// <summary>
/// Power of attorney — Word document authorizing an agent (attorney-in-fact)
/// to act on behalf of a principal. Supports general / limited / durable
/// variants, enumerated powers, and a notarization block.
/// </summary>
public sealed class PowerOfAttorneyTemplate : IDocumentTemplate
{
    public string Id => "legal/power-of-attorney";
    public string Name => "Power of Attorney";
    public string Industry => "legal";
    public string Description => "Power of attorney with principal, agent, powers granted, and durability.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "principal_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "principal_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "agent_name", Type = TemplateFieldType.String, Required = true, Description = "Attorney-in-fact." },
        new() { Name = "agent_address", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "alternate_agent_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "poa_type", Type = TemplateFieldType.String, Required = true, Description = "General, Limited, or Durable." },
        new() { Name = "effective_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "expiration_date", Type = TemplateFieldType.Date, Required = false },
        new() { Name = "is_durable", Type = TemplateFieldType.Boolean, Required = false, Description = "Continues in effect on principal's incapacity." },
        new() { Name = "powers_granted", Type = TemplateFieldType.StringArray, Required = true, Description = "Enumerated powers the agent may exercise." },
        new() { Name = "limitations", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "governing_state", Type = TemplateFieldType.String, Required = true },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var principal = d.String("principal_name");
        var agent = d.String("agent_name");
        var alternate = d.String("alternate_agent_name");
        var poaType = d.String("poa_type");
        var effective = d.Date("effective_date");
        var expiration = d.Date("expiration_date");
        var durable = d.Has("is_durable") ? d.Boolean("is_durable") : poaType.Contains("Durable", StringComparison.OrdinalIgnoreCase);
        var state = d.String("governing_state");
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.Append("# ").Append(poaType).AppendLine(" Power of Attorney");
        sb.AppendLine();

        sb.Append("I, **").Append(principal).Append("**, residing in the State of ").Append(state)
          .Append(", do hereby appoint **").Append(agent).AppendLine("** as my attorney-in-fact (the \"Agent\") to act in my name, place, and stead.");
        sb.AppendLine();

        var principalAddr = d.String("principal_address");
        var agentAddr = d.String("agent_address");
        if (!string.IsNullOrWhiteSpace(principalAddr) || !string.IsNullOrWhiteSpace(agentAddr))
        {
            sb.AppendLine("## Parties");
            sb.AppendLine();
            sb.Append("**Principal:** ").AppendLine(principal);
            if (!string.IsNullOrWhiteSpace(principalAddr)) sb.AppendLine(principalAddr);
            sb.AppendLine();
            sb.Append("**Agent:** ").AppendLine(agent);
            if (!string.IsNullOrWhiteSpace(agentAddr)) sb.AppendLine(agentAddr);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(alternate))
        {
            sb.Append("If my Agent is unable or unwilling to serve, I appoint **").Append(alternate)
              .AppendLine("** as my alternate Agent, with the same powers and authority granted to my primary Agent.");
            sb.AppendLine();
        }

        sb.AppendLine("## Effective Date and Term");
        sb.AppendLine();
        sb.Append("This Power of Attorney is effective as of **").Append(TemplateFormat.FormatDate(effective)).AppendLine("**.");
        if (expiration is not null)
            sb.Append("It shall automatically terminate on **").Append(TemplateFormat.FormatDate(expiration)).AppendLine("** unless revoked earlier.");
        sb.AppendLine();

        sb.AppendLine("## Durability");
        sb.AppendLine();
        sb.AppendLine(durable
            ? "This Power of Attorney **shall not be affected** by the subsequent disability or incapacity of the Principal, and shall remain in effect until revoked by the Principal in writing."
            : "This Power of Attorney **shall terminate** upon the subsequent disability or incapacity of the Principal.");
        sb.AppendLine();

        sb.AppendLine("## Powers Granted");
        sb.AppendLine();
        sb.AppendLine("The Agent is authorized to act on my behalf with respect to the following matters:");
        sb.AppendLine();
        foreach (var power in d.StringArray("powers_granted")) sb.Append("- ").AppendLine(power);
        sb.AppendLine();

        var limitations = d.String("limitations");
        if (!string.IsNullOrWhiteSpace(limitations))
        {
            sb.AppendLine("## Limitations");
            sb.AppendLine(limitations);
            sb.AppendLine();
        }

        sb.AppendLine("## Third-Party Reliance");
        sb.AppendLine();
        sb.AppendLine("Any third party receiving a copy of this document may rely on it and is released from any liability for acting in good faith on the Agent's apparent authority until receiving actual written notice of revocation.");
        sb.AppendLine();

        sb.AppendLine("## Governing Law");
        sb.AppendLine();
        sb.Append("This Power of Attorney is governed by the laws of the State of **").Append(state).AppendLine("**.");
        sb.AppendLine();

        sb.AppendLine("## Execution");
        sb.AppendLine();
        sb.Append("Signed this ____ day of ____________, ").Append(effective?.Year.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "____").AppendLine(".");
        sb.AppendLine();
        sb.Append("**Principal:** ").AppendLine(principal);
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine();
        sb.AppendLine("## Notary Acknowledgement");
        sb.AppendLine();
        sb.Append("State of ").Append(state).AppendLine("  ____________");
        sb.AppendLine("County of ____________________________");
        sb.AppendLine();
        sb.AppendLine("On this ____ day of ____________, ________, before me personally appeared the above-named Principal, known to me (or satisfactorily proven) to be the person whose name is subscribed to the foregoing instrument, and acknowledged that they executed the same for the purposes therein contained.");
        sb.AppendLine();
        sb.AppendLine("Notary Public: ____________________________");
        sb.AppendLine("My commission expires: ____________________________");

        if (showDisclaimer)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel and comply with local notarization and witness requirements before execution.*");
        }

        var fileName = $"poa-{TemplateFormat.Slug(principal)}-{TemplateFormat.Slug(agent)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Power of Attorney — {principal}");
    }
}
