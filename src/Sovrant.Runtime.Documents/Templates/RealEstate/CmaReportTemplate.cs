using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.RealEstate;

/// <summary>
/// Comparative Market Analysis — structured PDF that compares a subject
/// property against recent comparable sales and generates a suggested
/// listing price range.
/// </summary>
public sealed class CmaReportTemplate : IDocumentTemplate
{
    public string Id => "real-estate/cma-report";
    public string Name => "Comparative Market Analysis";
    public string Industry => "real-estate";
    public string Description => "CMA comparing a subject property to recent comparable sales with price guidance.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "prepared_for", Type = TemplateFieldType.String, Required = true },
        new() { Name = "prepared_by", Type = TemplateFieldType.String, Required = true },
        new() { Name = "report_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "subject_address", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "subject_bedrooms", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "subject_bathrooms", Type = TemplateFieldType.Decimal, Required = false },
        new() { Name = "subject_square_feet", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "subject_year_built", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new()
        {
            Name = "comparables",
            Type = TemplateFieldType.ObjectArray,
            Required = true,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "address", Type = TemplateFieldType.String, Required = true },
                new() { Name = "sold_price", Type = TemplateFieldType.Currency, Required = true },
                new() { Name = "sold_date", Type = TemplateFieldType.Date, Required = false },
                new() { Name = "bedrooms", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "bathrooms", Type = TemplateFieldType.Decimal, Required = false },
                new() { Name = "square_feet", Type = TemplateFieldType.Integer, Required = false },
                new() { Name = "notes", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new() { Name = "suggested_price_low", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "suggested_price_high", Type = TemplateFieldType.Currency, Required = false },
        new() { Name = "market_commentary", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var client = d.String("prepared_for");
        var preparer = d.String("prepared_by");
        var reportDate = d.Date("report_date");
        var subject = d.String("subject_address");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.AppendLine("# Comparative Market Analysis");
        sb.AppendLine();
        sb.Append("**Prepared for:** ").AppendLine(client);
        sb.Append("**Prepared by:** ").AppendLine(preparer);
        sb.Append("**Report date:** ").AppendLine(TemplateFormat.FormatDate(reportDate));
        sb.AppendLine();

        sb.AppendLine("## Subject Property");
        sb.AppendLine();
        sb.AppendLine(subject);
        sb.AppendLine();
        var beds = d.Integer("subject_bedrooms");
        var baths = d.Decimal("subject_bathrooms");
        var sqft = d.Integer("subject_square_feet");
        var year = d.Integer("subject_year_built");
        sb.AppendLine("| Bedrooms | Bathrooms | Square feet | Year built |");
        sb.AppendLine("|---:|---:|---:|---:|");
        sb.Append("| ").Append(beds > 0 ? beds.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—")
          .Append(" | ").Append(baths > 0m ? TemplateFormat.FormatNumber(baths) : "—")
          .Append(" | ").Append(sqft > 0 ? sqft.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "—")
          .Append(" | ").Append(year > 0 ? year.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—")
          .AppendLine(" |");
        sb.AppendLine();

        var comps = d.ObjectArray("comparables");
        sb.AppendLine("## Comparable Sales");
        sb.AppendLine();
        sb.AppendLine("| # | Address | Sold | Price | Beds | Baths | Sq ft | $/sqft |");
        sb.AppendLine("|---:|---|---|---:|---:|---:|---:|---:|");
        var idx = 1;
        decimal priceSum = 0m;
        int priceCount = 0;
        decimal ppsfSum = 0m;
        int ppsfCount = 0;
        foreach (var c in comps)
        {
            var addr = TemplateFormat.GetString(c, "address");
            var soldPrice = TemplateFormat.GetDecimal(c, "sold_price");
            var soldDate = TemplateFormat.GetDate(c, "sold_date");
            var cBeds = TemplateFormat.GetInteger(c, "bedrooms");
            var cBaths = TemplateFormat.GetDecimal(c, "bathrooms");
            var cSqft = TemplateFormat.GetInteger(c, "square_feet");
            var ppsf = cSqft > 0 ? soldPrice / cSqft : 0m;
            if (soldPrice > 0m) { priceSum += soldPrice; priceCount++; }
            if (ppsf > 0m) { ppsfSum += ppsf; ppsfCount++; }

            sb.Append("| ").Append(idx++)
              .Append(" | ").Append(TemplateFormat.EscapePipes(addr))
              .Append(" | ").Append(soldDate is not null ? TemplateFormat.FormatDate(soldDate) : "—")
              .Append(" | ").Append(TemplateFormat.FormatMoneyWhole(soldPrice, currency))
              .Append(" | ").Append(cBeds > 0 ? cBeds.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—")
              .Append(" | ").Append(cBaths > 0m ? TemplateFormat.FormatNumber(cBaths) : "—")
              .Append(" | ").Append(cSqft > 0 ? cSqft.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "—")
              .Append(" | ").Append(ppsf > 0m ? TemplateFormat.FormatMoneyWhole(ppsf, currency) : "—")
              .AppendLine(" |");
        }
        sb.AppendLine();

        if (priceCount > 0)
        {
            sb.Append("**Average sold price:** ").Append(TemplateFormat.FormatMoneyWhole(priceSum / priceCount, currency)).AppendLine();
            if (ppsfCount > 0)
                sb.Append("**Average $/sqft:** ").Append(TemplateFormat.FormatMoneyWhole(ppsfSum / ppsfCount, currency)).AppendLine();
            sb.AppendLine();
        }

        if (d.Has("suggested_price_low") || d.Has("suggested_price_high"))
        {
            sb.AppendLine("## Suggested Listing Price Range");
            sb.AppendLine();
            var low = d.Decimal("suggested_price_low");
            var high = d.Decimal("suggested_price_high");
            sb.Append("**").Append(TemplateFormat.FormatMoneyWhole(low, currency))
              .Append("** – **").Append(TemplateFormat.FormatMoneyWhole(high, currency)).AppendLine("**");
            sb.AppendLine();
        }

        var commentary = d.String("market_commentary");
        if (!string.IsNullOrWhiteSpace(commentary))
        {
            sb.AppendLine("## Market Commentary");
            sb.AppendLine(commentary);
            sb.AppendLine();
        }

        var fileName = $"cma-{TemplateFormat.Slug(client)}-{TemplateFormat.FormatDate(reportDate)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"CMA — {client}");
    }
}
