using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Legal;

/// <summary>
/// Mutual non-disclosure agreement as a .docx. Takes parties, effective
/// date, term, scope, and governing law. Ships with a plain-English AI
/// disclaimer so recipients know the draft is machine-generated and
/// should be reviewed by counsel before execution.
/// </summary>
public sealed class NdaTemplate : IDocumentTemplate
{
    public string Id => "legal/nda";
    public string Name => "Non-Disclosure Agreement";
    public string Industry => "legal";
    public string Description => "Mutual NDA template with parties, term, scope, and governing law.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "party_a", Type = TemplateFieldType.String, Required = true, Description = "First party — legal name." },
        new() { Name = "party_a_address", Type = TemplateFieldType.Text, Required = false, Description = "First party address (multiline)." },
        new() { Name = "party_b", Type = TemplateFieldType.String, Required = true, Description = "Second party — legal name." },
        new() { Name = "party_b_address", Type = TemplateFieldType.Text, Required = false, Description = "Second party address (multiline)." },
        new() { Name = "effective_date", Type = TemplateFieldType.Date, Required = true, Description = "Date the agreement takes effect." },
        new() { Name = "term_years", Type = TemplateFieldType.Integer, Required = false, Description = "Duration of confidentiality obligations, in years. Defaults to 2." },
        new() { Name = "purpose", Type = TemplateFieldType.Text, Required = true, Description = "Why the parties are sharing confidential information." },
        new() { Name = "governing_law", Type = TemplateFieldType.String, Required = true, Description = "Jurisdiction whose laws govern, e.g. \"State of Delaware\"." },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false, Description = "Whether to include the AI-generated draft disclaimer (default true)." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var partyA = d.String("party_a");
        var partyB = d.String("party_b");
        var effectiveDate = d.Date("effective_date");
        var termYears = d.Integer("term_years", 2);
        var purpose = d.String("purpose");
        var governingLaw = d.String("governing_law");
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.AppendLine("# Mutual Non-Disclosure Agreement");
        sb.AppendLine();

        sb.Append("This Mutual Non-Disclosure Agreement (the \"Agreement\") is entered into as of ")
          .Append(FormatDate(effectiveDate))
          .Append(" (the \"Effective Date\") by and between **")
          .Append(partyA).Append("** (\"Party A\") and **")
          .Append(partyB).AppendLine("** (\"Party B\"), collectively the \"Parties.\"");
        sb.AppendLine();

        var partyAAddress = d.String("party_a_address");
        var partyBAddress = d.String("party_b_address");
        if (!string.IsNullOrWhiteSpace(partyAAddress) || !string.IsNullOrWhiteSpace(partyBAddress))
        {
            sb.AppendLine("## Parties");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(partyAAddress))
            {
                sb.Append("**").Append(partyA).AppendLine("**");
                sb.AppendLine(partyAAddress);
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(partyBAddress))
            {
                sb.Append("**").Append(partyB).AppendLine("**");
                sb.AppendLine(partyBAddress);
                sb.AppendLine();
            }
        }

        sb.AppendLine("## 1. Purpose");
        sb.AppendLine();
        sb.AppendLine(purpose);
        sb.AppendLine();

        sb.AppendLine("## 2. Confidential Information");
        sb.AppendLine();
        sb.AppendLine("\"Confidential Information\" means any non-public information disclosed by one Party (the \"Disclosing Party\") to the other (the \"Receiving Party\"), whether in writing, orally, visually, or in any other form, that is marked or identified as confidential, or that would reasonably be understood to be confidential given its nature or the circumstances of disclosure.");
        sb.AppendLine();

        sb.AppendLine("## 3. Obligations");
        sb.AppendLine();
        sb.AppendLine("The Receiving Party shall:");
        sb.AppendLine();
        sb.AppendLine("- hold the Confidential Information in strict confidence;");
        sb.AppendLine("- use it solely for the Purpose described above;");
        sb.AppendLine("- protect it with at least the same degree of care used to protect its own confidential information, and never less than reasonable care; and");
        sb.AppendLine("- limit access to its employees, contractors, and advisors who have a need to know and who are bound by confidentiality obligations no less protective than those in this Agreement.");
        sb.AppendLine();

        sb.AppendLine("## 4. Exclusions");
        sb.AppendLine();
        sb.AppendLine("Confidential Information does not include information that (a) is or becomes publicly available without breach of this Agreement, (b) was known to the Receiving Party prior to disclosure without a duty of confidentiality, (c) is lawfully received from a third party free of any confidentiality obligation, or (d) is independently developed by the Receiving Party without use of the Disclosing Party's Confidential Information.");
        sb.AppendLine();

        sb.AppendLine("## 5. Term");
        sb.AppendLine();
        sb.Append("This Agreement begins on the Effective Date and continues for ").Append(termYears)
          .Append(" year").Append(termYears == 1 ? string.Empty : "s")
          .AppendLine(", unless terminated earlier by mutual written agreement. The confidentiality obligations survive termination for the same period.");
        sb.AppendLine();

        sb.AppendLine("## 6. Return or Destruction");
        sb.AppendLine();
        sb.AppendLine("On written request of the Disclosing Party, the Receiving Party shall promptly return or destroy all Confidential Information in its possession, and certify destruction in writing if requested.");
        sb.AppendLine();

        sb.AppendLine("## 7. No License; No Warranty");
        sb.AppendLine();
        sb.AppendLine("Nothing in this Agreement grants any license, ownership, or other rights in the Confidential Information. Confidential Information is provided \"as is,\" without any warranty of any kind.");
        sb.AppendLine();

        sb.AppendLine("## 8. Governing Law");
        sb.AppendLine();
        sb.Append("This Agreement is governed by the laws of **").Append(governingLaw).AppendLine("**, without regard to its conflict-of-law provisions.");
        sb.AppendLine();

        sb.AppendLine("## 9. Entire Agreement");
        sb.AppendLine();
        sb.AppendLine("This Agreement constitutes the entire understanding between the Parties regarding its subject matter and supersedes any prior agreements, written or oral, on that subject.");
        sb.AppendLine();

        sb.AppendLine("## Signatures");
        sb.AppendLine();
        sb.Append("**").Append(partyA).AppendLine("**");
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine();
        sb.AppendLine("Name / Title: ____________________________");
        sb.AppendLine();
        sb.AppendLine("Date: ____________________________");
        sb.AppendLine();
        sb.Append("**").Append(partyB).AppendLine("**");
        sb.AppendLine();
        sb.AppendLine("Signature: ____________________________");
        sb.AppendLine();
        sb.AppendLine("Name / Title: ____________________________");
        sb.AppendLine();
        sb.AppendLine("Date: ____________________________");
        sb.AppendLine();

        if (showDisclaimer)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*");
        }

        var fileName = $"nda-{Slug(partyA)}-{Slug(partyB)}-{FormatDate(effectiveDate)}.docx";
        var title = $"NDA — {partyA} & {partyB}";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, title);
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Slug(string value)
    {
        var cleaned = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c)) cleaned.Append(char.ToLowerInvariant(c));
            else if (c is '-' or '_') cleaned.Append(c);
            else if (char.IsWhiteSpace(c)) cleaned.Append('-');
        }
        var slug = cleaned.ToString().Trim('-');
        return slug.Length == 0 ? "party" : slug;
    }
}
