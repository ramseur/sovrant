using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// Renders a JSON <c>{"headers":[...], "rows":[[...]]}</c> body into a
/// single-sheet .xlsx via ClosedXML. Headers get a bold row style; rows
/// are written cell-by-cell with ClosedXML's native type inference so
/// numbers land as numbers and ISO dates become date cells.
/// </summary>
/// <remarks>
/// If the body is not valid JSON, we fall back to interpreting it as
/// CSV (comma-separated). This keeps the tool usable when an agent
/// pastes a simple CSV instead of a JSON structure.
/// </remarks>
public sealed class ExcelDocumentGenerator : DocumentGeneratorBase
{
    public ExcelDocumentGenerator(IArtifactStore artifactStore) : base(artifactStore) { }

    public override DocumentFormat Format => DocumentFormat.Excel;

    protected override string ContentType =>
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    protected override Task<byte[]> RenderAsync(DocumentRequest request, CancellationToken ct)
    {
        var (headers, rows) = ParseBody(request.Body);

        using var workbook = new XLWorkbook();
        var sheetName = SanitizeSheetName(request.Title ?? "Sheet1");
        var sheet = workbook.Worksheets.Add(sheetName);

        var row = 1;

        if (headers.Count > 0)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            row++;
        }

        foreach (var dataRow in rows)
        {
            ct.ThrowIfCancellationRequested();
            for (var i = 0; i < dataRow.Count; i++)
            {
                SetCellValue(sheet.Cell(row, i + 1), dataRow[i]);
            }
            row++;
        }

        if (headers.Count > 0 || rows.Count > 0)
            sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    private static void SetCellValue(IXLCell cell, string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            cell.Value = string.Empty;
            return;
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
        {
            cell.Value = number;
            return;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            cell.Value = date;
            cell.Style.DateFormat.Format = "yyyy-mm-dd";
            return;
        }

        cell.Value = raw;
    }

    private static (List<string> Headers, List<List<string>> Rows) ParseBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return ([], []);

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var headers = new List<string>();
                if (root.TryGetProperty("headers", out var h) && h.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in h.EnumerateArray())
                        headers.Add(el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : el.ToString());
                }

                var rows = new List<List<string>>();
                if (root.TryGetProperty("rows", out var r) && r.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in r.EnumerateArray())
                    {
                        var cells = new List<string>();
                        if (row.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cell in row.EnumerateArray())
                                cells.Add(CellText(cell));
                        }
                        rows.Add(cells);
                    }
                }

                return (headers, rows);
            }
            catch (JsonException)
            {
                // Fall through to CSV handling.
            }
        }

        return ParseCsv(body);
    }

    private static string CellText(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? string.Empty,
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        _ => el.GetRawText(),
    };

    private static (List<string> Headers, List<List<string>> Rows) ParseCsv(string body)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return ([], []);

        var headers = SplitCsvLine(lines[0]);
        var rows = new List<List<string>>();
        for (var i = 1; i < lines.Length; i++)
            rows.Add(SplitCsvLine(lines[i]));
        return (headers, rows);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var buf = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                cells.Add(buf.ToString());
                buf.Clear();
            }
            else
            {
                buf.Append(ch);
            }
        }
        cells.Add(buf.ToString());
        return cells;
    }

    private static string SanitizeSheetName(string name)
    {
        const int maxLen = 31;
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        foreach (var ch in invalid)
            name = name.Replace(ch, '_');
        if (name.Length > maxLen)
            name = name[..maxLen];
        return string.IsNullOrWhiteSpace(name) ? "Sheet1" : name;
    }
}
