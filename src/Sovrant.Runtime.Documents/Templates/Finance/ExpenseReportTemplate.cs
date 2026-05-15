using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Finance;

/// <summary>
/// Excel expense report: employee + period metadata as a preamble
/// block, a header row over the line items, and trailing rows for
/// subtotal / tax / total. Emits JSON in the shape the Excel generator
/// expects (<c>preamble / headers / rows</c>).
/// </summary>
public sealed class ExpenseReportTemplate : IDocumentTemplate
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Id => "finance/expense-report";
    public string Name => "Expense Report";
    public string Industry => "finance";
    public string Description => "Employee expense report with line items, reimbursement totals, and optional tax.";
    public DocumentFormat DefaultFormat => DocumentFormat.Excel;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "employee_name", Type = TemplateFieldType.String, Required = true, Description = "Employee submitting the report." },
        new() { Name = "employee_id", Type = TemplateFieldType.String, Required = false, Description = "Optional employee / staff ID." },
        new() { Name = "department", Type = TemplateFieldType.String, Required = false, Description = "Employee's department or team." },
        new() { Name = "period_start", Type = TemplateFieldType.Date, Required = true, Description = "First ISO date covered by the report." },
        new() { Name = "period_end", Type = TemplateFieldType.Date, Required = true, Description = "Last ISO date covered by the report." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false, Description = "ISO currency code (default USD)." },
        new() { Name = "purpose", Type = TemplateFieldType.Text, Required = false, Description = "Business purpose of the expenses." },
        new()
        {
            Name = "expenses",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            Description = "Individual expense line items.",
            ItemFields = new List<TemplateField>
            {
                new() { Name = "date", Type = TemplateFieldType.Date, Required = true },
                new() { Name = "category", Type = TemplateFieldType.String, Required = true, Description = "e.g. Travel, Meals, Lodging." },
                new() { Name = "description", Type = TemplateFieldType.String, Required = true },
                new() { Name = "amount", Type = TemplateFieldType.Decimal, Required = true },
                new() { Name = "receipt", Type = TemplateFieldType.String, Required = false, Description = "Receipt reference or URL." },
            },
        },
        new() { Name = "tax_rate", Type = TemplateFieldType.Decimal, Required = false, Description = "Tax percentage applied to the subtotal." },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var currency = NormalizeCurrency(d.String("currency", "USD"));
        var employee = d.String("employee_name");
        var employeeId = d.String("employee_id");
        var department = d.String("department");
        var periodStart = d.Date("period_start");
        var periodEnd = d.Date("period_end");
        var purpose = d.String("purpose");

        var preamble = new List<object[]>
        {
            new object[] { $"Expense Report — {employee}", string.Empty, string.Empty, string.Empty, string.Empty },
            new object[] { "Employee", employee, string.Empty, string.Empty, string.Empty },
        };
        if (!string.IsNullOrWhiteSpace(employeeId))
            preamble.Add(new object[] { "Employee ID", employeeId, string.Empty, string.Empty, string.Empty });
        if (!string.IsNullOrWhiteSpace(department))
            preamble.Add(new object[] { "Department", department, string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { "Period", $"{FormatDate(periodStart)} — {FormatDate(periodEnd)}", string.Empty, string.Empty, string.Empty });
        if (!string.IsNullOrWhiteSpace(purpose))
            preamble.Add(new object[] { "Purpose", purpose, string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { "Currency", currency, string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty });

        var headers = new[] { "Date", "Category", "Description", "Amount", "Receipt" };

        decimal subtotal = 0m;
        var rows = new List<object[]>();
        foreach (var item in d.ObjectArray("expenses"))
        {
            var itemDate = GetDate(item, "date");
            var category = GetString(item, "category");
            var description = GetString(item, "description");
            var amount = GetDecimal(item, "amount");
            var receipt = GetString(item, "receipt");
            subtotal += amount;

            rows.Add(new object[]
            {
                FormatDate(itemDate),
                category,
                description,
                amount.ToString("0.00", CultureInfo.InvariantCulture),
                receipt,
            });
        }

        var taxRate = d.Decimal("tax_rate");
        var tax = Math.Round(subtotal * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;

        rows.Add(new object[] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty });
        rows.Add(new object[] { string.Empty, string.Empty, "Subtotal", subtotal.ToString("0.00", CultureInfo.InvariantCulture), string.Empty });
        if (taxRate > 0m)
            rows.Add(new object[] { string.Empty, string.Empty, $"Tax ({FormatPercent(taxRate)})", tax.ToString("0.00", CultureInfo.InvariantCulture), string.Empty });
        rows.Add(new object[] { string.Empty, string.Empty, "Total", total.ToString("0.00", CultureInfo.InvariantCulture), string.Empty });

        var body = JsonSerializer.Serialize(new
        {
            preamble,
            headers,
            rows,
        }, s_jsonOptions);

        var fileName = $"expense-report-{Slug(employee)}-{FormatDate(periodEnd)}.xlsx";
        var title = $"Expense Report — {employee}";
        return new TemplateRenderResult(DocumentFormat.Excel, body, fileName, title);
    }

    private static string GetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static decimal GetDecimal(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return 0m;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => 0m,
        };
    }

    private static DateOnly? GetDate(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        return DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatPercent(decimal rate) =>
        rate == Math.Floor(rate)
            ? rate.ToString("0", CultureInfo.InvariantCulture) + "%"
            : rate.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string NormalizeCurrency(string input) =>
        string.IsNullOrWhiteSpace(input) ? "USD" : input.Trim().ToUpperInvariant();

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
        return slug.Length == 0 ? "employee" : slug;
    }
}
