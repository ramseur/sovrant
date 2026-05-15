using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Legal;

/// <summary>
/// Corporate meeting minutes — Word document capturing the formal
/// record of a board or shareholder meeting: attendees, quorum,
/// resolutions, votes, and adjournment.
/// </summary>
public sealed class CorporateMinutesTemplate : IDocumentTemplate
{
    public string Id => "legal/corporate-minutes";
    public string Name => "Corporate Meeting Minutes";
    public string Industry => "legal";
    public string Description => "Corporate meeting minutes with attendees, resolutions, and votes.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "company_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "meeting_type", Type = TemplateFieldType.String, Required = true, Description = "e.g. Board of Directors, Shareholders, Annual." },
        new() { Name = "meeting_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "meeting_time", Type = TemplateFieldType.String, Required = false },
        new() { Name = "location", Type = TemplateFieldType.String, Required = false },
        new() { Name = "chair", Type = TemplateFieldType.String, Required = true },
        new() { Name = "secretary", Type = TemplateFieldType.String, Required = false },
        new() { Name = "attendees_present", Type = TemplateFieldType.StringArray, Required = true },
        new() { Name = "attendees_absent", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "quorum_established", Type = TemplateFieldType.Boolean, Required = false },
        new() { Name = "agenda", Type = TemplateFieldType.StringArray, Required = false },
        new()
        {
            Name = "resolutions",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "title", Type = TemplateFieldType.String, Required = true },
                new() { Name = "text", Type = TemplateFieldType.Text, Required = true },
                new() { Name = "moved_by", Type = TemplateFieldType.String, Required = false },
                new() { Name = "seconded_by", Type = TemplateFieldType.String, Required = false },
                new() { Name = "votes_for", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "votes_against", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "votes_abstain", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "outcome", Type = TemplateFieldType.String, Required = false, Description = "e.g. Passed, Failed, Tabled." },
            },
        },
        new() { Name = "discussion_notes", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "adjournment_time", Type = TemplateFieldType.String, Required = false },
        new() { Name = "include_ai_disclaimer", Type = TemplateFieldType.Boolean, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var company = d.String("company_name");
        var meetingType = d.String("meeting_type");
        var meetingDate = d.Date("meeting_date");
        var chair = d.String("chair");
        var showDisclaimer = d.Has("include_ai_disclaimer") ? d.Boolean("include_ai_disclaimer", true) : true;

        var sb = new StringBuilder();
        sb.Append("# ").Append(company).AppendLine();
        sb.Append("## Minutes of the ").Append(meetingType).Append(" Meeting — ").AppendLine(TemplateFormat.FormatDate(meetingDate));
        sb.AppendLine();

        var time = d.String("meeting_time");
        if (!string.IsNullOrWhiteSpace(time)) sb.Append("**Time:** ").AppendLine(time);
        var location = d.String("location");
        if (!string.IsNullOrWhiteSpace(location)) sb.Append("**Location:** ").AppendLine(location);
        sb.Append("**Chair:** ").AppendLine(chair);
        var secretary = d.String("secretary");
        if (!string.IsNullOrWhiteSpace(secretary)) sb.Append("**Secretary:** ").AppendLine(secretary);
        sb.AppendLine();

        sb.AppendLine("## Attendance");
        sb.AppendLine();
        sb.AppendLine("**Present:**");
        foreach (var a in d.StringArray("attendees_present")) sb.Append("- ").AppendLine(a);
        var absent = d.StringArray("attendees_absent");
        if (absent.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Absent:**");
            foreach (var a in absent) sb.Append("- ").AppendLine(a);
        }
        if (d.Has("quorum_established"))
        {
            sb.AppendLine();
            sb.Append("**Quorum established:** ").AppendLine(d.Boolean("quorum_established") ? "Yes" : "No");
        }
        sb.AppendLine();

        var agenda = d.StringArray("agenda");
        if (agenda.Count > 0)
        {
            sb.AppendLine("## Agenda");
            var i = 1;
            foreach (var item in agenda)
            {
                sb.Append(i++).Append(". ").AppendLine(item);
            }
            sb.AppendLine();
        }

        var resolutions = d.ObjectArray("resolutions");
        if (resolutions.Count > 0)
        {
            sb.AppendLine("## Resolutions");
            sb.AppendLine();
            var n = 1;
            foreach (var r in resolutions)
            {
                var title = TemplateFormat.GetString(r, "title");
                var text = TemplateFormat.GetString(r, "text");
                var moved = TemplateFormat.GetString(r, "moved_by");
                var seconded = TemplateFormat.GetString(r, "seconded_by");
                var votesFor = TemplateFormat.GetInteger(r, "votes_for");
                var votesAgainst = TemplateFormat.GetInteger(r, "votes_against");
                var votesAbstain = TemplateFormat.GetInteger(r, "votes_abstain");
                var outcome = TemplateFormat.GetString(r, "outcome");

                sb.Append("**Resolution ").Append(n++).Append(": ").Append(title).AppendLine("**");
                sb.AppendLine();
                sb.AppendLine(text);
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(moved)) sb.Append("Moved by: ").AppendLine(moved);
                if (!string.IsNullOrWhiteSpace(seconded)) sb.Append("Seconded by: ").AppendLine(seconded);
                if (votesFor + votesAgainst + votesAbstain > 0)
                {
                    sb.Append("Vote: ").Append(votesFor).Append(" for, ").Append(votesAgainst)
                      .Append(" against, ").Append(votesAbstain).AppendLine(" abstain");
                }
                if (!string.IsNullOrWhiteSpace(outcome)) sb.Append("Outcome: **").Append(outcome).AppendLine("**");
                sb.AppendLine();
            }
        }

        var notes = d.String("discussion_notes");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.AppendLine("## Discussion Notes");
            sb.AppendLine(notes);
            sb.AppendLine();
        }

        var adjournment = d.String("adjournment_time");
        sb.Append("The meeting was adjourned");
        if (!string.IsNullOrWhiteSpace(adjournment)) sb.Append(" at ").Append(adjournment);
        sb.AppendLine(".");
        sb.AppendLine();
        sb.AppendLine("Respectfully submitted,");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(secretary)) sb.AppendLine(secretary);
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        if (showDisclaimer)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before finalizing.*");
        }

        var fileName = $"minutes-{TemplateFormat.Slug(company)}-{TemplateFormat.FormatDate(meetingDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"{company} — Minutes {TemplateFormat.FormatDate(meetingDate)}");
    }
}
