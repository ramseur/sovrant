using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Construction;

/// <summary>
/// Daily construction log — Word document recording site conditions,
/// crews and trades on site, equipment, materials received, work
/// performed, visitors, incidents, and delays for a single day.
/// </summary>
public sealed class DailyLogTemplate : IDocumentTemplate
{
    public string Id => "construction/daily-log";
    public string Name => "Daily Construction Log";
    public string Industry => "construction";
    public string Description => "Daily site log covering weather, crews, equipment, work performed, and incidents.";
    public DocumentFormat DefaultFormat => DocumentFormat.Word;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "project_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "project_number", Type = TemplateFieldType.String, Required = false },
        new() { Name = "log_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "prepared_by", Type = TemplateFieldType.String, Required = true },
        new() { Name = "superintendent", Type = TemplateFieldType.String, Required = false },
        new() { Name = "weather_am", Type = TemplateFieldType.String, Required = false },
        new() { Name = "weather_pm", Type = TemplateFieldType.String, Required = false },
        new() { Name = "temperature_high_f", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "temperature_low_f", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "site_conditions", Type = TemplateFieldType.Text, Required = false },
        new()
        {
            Name = "crews",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "trade", Type = TemplateFieldType.String, Required = true },
                new() { Name = "company", Type = TemplateFieldType.String, Required = false },
                new() { Name = "workers", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "hours", Type = TemplateFieldType.Decimal, Required = false },
            },
        },
        new() { Name = "equipment_on_site", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "materials_received", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "work_performed", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "visitors", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "inspections", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "incidents", Type = TemplateFieldType.Text, Required = false, Description = "Safety incidents, near misses, or injuries." },
        new() { Name = "delays", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "notes", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var project = d.String("project_name");
        var logDate = d.Date("log_date");
        var preparedBy = d.String("prepared_by");

        var sb = new StringBuilder();
        sb.AppendLine("# Daily Construction Log");
        sb.AppendLine();
        sb.Append("**Project:** ").AppendLine(project);
        var number = d.String("project_number");
        if (!string.IsNullOrWhiteSpace(number)) sb.Append("**Project no.:** ").AppendLine(number);
        sb.Append("**Date:** ").AppendLine(TemplateFormat.FormatDate(logDate));
        sb.Append("**Prepared by:** ").AppendLine(preparedBy);
        var super = d.String("superintendent");
        if (!string.IsNullOrWhiteSpace(super)) sb.Append("**Superintendent:** ").AppendLine(super);
        sb.AppendLine();

        sb.AppendLine("## Weather and Site Conditions");
        var wAm = d.String("weather_am");
        var wPm = d.String("weather_pm");
        if (!string.IsNullOrWhiteSpace(wAm)) sb.Append("- AM: ").AppendLine(wAm);
        if (!string.IsNullOrWhiteSpace(wPm)) sb.Append("- PM: ").AppendLine(wPm);
        var tempHi = d.Integer("temperature_high_f");
        var tempLo = d.Integer("temperature_low_f");
        if (tempHi != 0 || tempLo != 0)
        {
            sb.Append("- Temperature: ").Append(tempLo).Append("°F – ").Append(tempHi).AppendLine("°F");
        }
        var conditions = d.String("site_conditions");
        if (!string.IsNullOrWhiteSpace(conditions))
        {
            sb.AppendLine();
            sb.AppendLine(conditions);
        }
        sb.AppendLine();

        var crews = d.ObjectArray("crews");
        if (crews.Count > 0)
        {
            sb.AppendLine("## Crews on Site");
            sb.AppendLine();
            sb.AppendLine("| Trade | Company | Workers | Hours |");
            sb.AppendLine("|-------|---------|--------:|------:|");
            foreach (var c in crews)
            {
                var workers = TemplateFormat.GetInteger(c, "workers");
                var hours = TemplateFormat.GetDecimal(c, "hours");
                sb.Append("| ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(c, "trade")))
                  .Append(" | ").Append(TemplateFormat.EscapePipes(TemplateFormat.GetString(c, "company")))
                  .Append(" | ").Append(workers > 0 ? workers.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty)
                  .Append(" | ").Append(hours > 0 ? TemplateFormat.FormatNumber(hours) : string.Empty)
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        AppendList(sb, "Equipment on Site", d.StringArray("equipment_on_site"));
        AppendList(sb, "Materials Received", d.StringArray("materials_received"));

        sb.AppendLine("## Work Performed");
        sb.AppendLine(d.String("work_performed"));
        sb.AppendLine();

        AppendList(sb, "Visitors", d.StringArray("visitors"));
        AppendList(sb, "Inspections", d.StringArray("inspections"));

        AppendText(sb, "Incidents / Near Misses", d.String("incidents"));
        AppendText(sb, "Delays", d.String("delays"));
        AppendText(sb, "Additional Notes", d.String("notes"));

        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append("Signed: ").AppendLine(preparedBy);
        sb.AppendLine("Signature: ____________________________  Date: ____________");

        var fileName = $"daily-log-{TemplateFormat.Slug(project)}-{TemplateFormat.FormatDate(logDate)}.docx";
        return new TemplateRenderResult(DocumentFormat.Word, sb.ToString(), fileName, $"Daily Log — {project} {TemplateFormat.FormatDate(logDate)}");
    }

    private static void AppendList(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        sb.Append("## ").AppendLine(heading);
        foreach (var i in items) sb.Append("- ").AppendLine(i);
        sb.AppendLine();
    }

    private static void AppendText(StringBuilder sb, string heading, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.Append("## ").AppendLine(heading);
        sb.AppendLine(content);
        sb.AppendLine();
    }
}
