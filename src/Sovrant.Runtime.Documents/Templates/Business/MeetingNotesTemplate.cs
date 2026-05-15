using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Business;

/// <summary>
/// Word-formatted meeting notes — title + date, attendees, agenda,
/// discussion, decisions, and action items. Emits Markdown that the
/// Word generator turns into a styled .docx.
/// </summary>
public sealed class MeetingNotesTemplate : IDocumentTemplate
{
    public string Id => "business/meeting-notes";
    public string Name => "Meeting Notes";
    public string Industry => "business";
    public string Description => "Structured meeting notes with attendees, agenda, decisions, and action items.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "title", Type = TemplateFieldType.String, Required = true, Description = "Meeting title." },
        new() { Name = "meeting_date", Type = TemplateFieldType.Date, Required = true, Description = "ISO date the meeting took place." },
        new() { Name = "location", Type = TemplateFieldType.String, Required = false, Description = "Physical room or video link." },
        new() { Name = "attendees", Type = TemplateFieldType.StringArray, Required = true, Description = "List of attendee names." },
        new() { Name = "absent", Type = TemplateFieldType.StringArray, Required = false, Description = "List of invited people who did not attend." },
        new() { Name = "agenda", Type = TemplateFieldType.StringArray, Required = false, Description = "Ordered agenda topics." },
        new() { Name = "discussion", Type = TemplateFieldType.Text, Required = false, Description = "Free-form discussion summary (supports Markdown)." },
        new() { Name = "decisions", Type = TemplateFieldType.StringArray, Required = false, Description = "Decisions made during the meeting." },
        new()
        {
            Name = "action_items",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            Description = "Action items with owner and optional due date.",
            ItemFields = new List<TemplateField>
            {
                new() { Name = "task", Type = TemplateFieldType.String, Required = true },
                new() { Name = "owner", Type = TemplateFieldType.String, Required = false },
                new() { Name = "due_date", Type = TemplateFieldType.Date, Required = false },
            },
        },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var title = d.String("title");
        var meetingDate = d.Date("meeting_date");
        var location = d.String("location");

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(title);
        sb.AppendLine();
        sb.Append("**Date:** ").Append(FormatDate(meetingDate));
        if (!string.IsNullOrWhiteSpace(location))
            sb.Append("  \n**Location:** ").Append(location);
        sb.AppendLine();
        sb.AppendLine();

        var attendees = d.StringArray("attendees");
        if (attendees.Count > 0)
        {
            sb.AppendLine("## Attendees");
            foreach (var a in attendees) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var absent = d.StringArray("absent");
        if (absent.Count > 0)
        {
            sb.AppendLine("## Absent");
            foreach (var a in absent) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var agenda = d.StringArray("agenda");
        if (agenda.Count > 0)
        {
            sb.AppendLine("## Agenda");
            for (var i = 0; i < agenda.Count; i++)
                sb.Append(i + 1).Append(". ").AppendLine(agenda[i]);
            sb.AppendLine();
        }

        var discussion = d.String("discussion");
        if (!string.IsNullOrWhiteSpace(discussion))
        {
            sb.AppendLine("## Discussion");
            sb.AppendLine(discussion);
            sb.AppendLine();
        }

        var decisions = d.StringArray("decisions");
        if (decisions.Count > 0)
        {
            sb.AppendLine("## Decisions");
            foreach (var dec in decisions) sb.Append("- ").AppendLine(dec);
            sb.AppendLine();
        }

        var actions = d.ObjectArray("action_items");
        if (actions.Count > 0)
        {
            sb.AppendLine("## Action Items");
            foreach (var item in actions)
            {
                var task = GetString(item, "task");
                var owner = GetString(item, "owner");
                var due = GetDate(item, "due_date");

                sb.Append("- ").Append(task);
                if (!string.IsNullOrWhiteSpace(owner)) sb.Append(" — **").Append(owner).Append("**");
                if (due is not null) sb.Append(" (due ").Append(FormatDate(due)).Append(')');
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        var fileName = $"meeting-notes-{FormatDate(meetingDate)}-{Slug(title)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, title);
    }

    private static string GetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static DateOnly? GetDate(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        return DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var d) ? d : null;
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
        return slug.Length == 0 ? "meeting" : slug;
    }
}
