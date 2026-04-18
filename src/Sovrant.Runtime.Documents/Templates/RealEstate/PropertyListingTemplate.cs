using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.RealEstate;

/// <summary>
/// Structured-PDF property listing — headline and address up top, a
/// property-details table (beds / baths / square footage / lot size /
/// year built), pricing, amenities list, and free-form description.
/// </summary>
public sealed class PropertyListingTemplate : IDocumentTemplate
{
    public string Id => "real-estate/property-listing";
    public string Name => "Property Listing";
    public string Industry => "real-estate";
    public string Description => "Property listing sheet with specs, amenities, pricing, and agent contact.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "headline", Type = TemplateFieldType.String, Required = true, Description = "Short attention-grabbing headline." },
        new() { Name = "address", Type = TemplateFieldType.Text, Required = true, Description = "Full property address (multiline ok)." },
        new() { Name = "price", Type = TemplateFieldType.Currency, Required = true, Description = "Asking price as a number." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false, Description = "ISO currency code (default USD)." },
        new() { Name = "bedrooms", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "bathrooms", Type = TemplateFieldType.Decimal, Required = false, Description = "May be fractional (e.g. 2.5)." },
        new() { Name = "square_feet", Type = TemplateFieldType.Integer, Required = false, Description = "Interior size." },
        new() { Name = "lot_size", Type = TemplateFieldType.String, Required = false, Description = "Free-form lot size description." },
        new() { Name = "year_built", Type = TemplateFieldType.Integer, Required = false },
        new() { Name = "property_type", Type = TemplateFieldType.String, Required = false, Description = "e.g. Single-family, Condo, Townhouse." },
        new() { Name = "mls_id", Type = TemplateFieldType.String, Required = false, Description = "MLS / listing identifier." },
        new() { Name = "amenities", Type = TemplateFieldType.StringArray, Required = false, Description = "Amenities or feature highlights." },
        new() { Name = "description", Type = TemplateFieldType.Text, Required = false, Description = "Free-form marketing description (supports Markdown)." },
        new() { Name = "agent_name", Type = TemplateFieldType.String, Required = false },
        new() { Name = "agent_phone", Type = TemplateFieldType.String, Required = false },
        new() { Name = "agent_email", Type = TemplateFieldType.String, Required = false },
        new() { Name = "brokerage", Type = TemplateFieldType.String, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var headline = d.String("headline");
        var address = d.String("address");
        var price = d.Decimal("price");
        var currency = NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(headline);
        sb.AppendLine();

        sb.Append("**Address:**  \n").AppendLine(address);
        sb.AppendLine();
        sb.Append("**Price:** ").AppendLine(FormatMoney(price, currency));
        sb.AppendLine();

        var propertyType = d.String("property_type");
        var mls = d.String("mls_id");
        if (!string.IsNullOrWhiteSpace(propertyType) || !string.IsNullOrWhiteSpace(mls))
        {
            if (!string.IsNullOrWhiteSpace(propertyType))
                sb.Append("**Property type:** ").AppendLine(propertyType);
            if (!string.IsNullOrWhiteSpace(mls))
                sb.Append("**MLS ID:** ").AppendLine(mls);
            sb.AppendLine();
        }

        var specs = new List<(string Label, string Value)>();
        if (d.Has("bedrooms")) specs.Add(("Bedrooms", d.Integer("bedrooms").ToString(CultureInfo.InvariantCulture)));
        if (d.Has("bathrooms")) specs.Add(("Bathrooms", FormatFraction(d.Decimal("bathrooms"))));
        if (d.Has("square_feet")) specs.Add(("Square feet", d.Integer("square_feet").ToString("N0", CultureInfo.InvariantCulture)));
        if (d.Has("lot_size")) specs.Add(("Lot size", d.String("lot_size")));
        if (d.Has("year_built")) specs.Add(("Year built", d.Integer("year_built").ToString(CultureInfo.InvariantCulture)));

        if (specs.Count > 0)
        {
            sb.AppendLine("## Property details");
            sb.AppendLine();
            sb.AppendLine("| Feature | Value |");
            sb.AppendLine("|---|---|");
            foreach (var (label, value) in specs)
                sb.Append("| ").Append(label).Append(" | ").Append(EscapePipes(value)).AppendLine(" |");
            sb.AppendLine();
        }

        var amenities = d.StringArray("amenities");
        if (amenities.Count > 0)
        {
            sb.AppendLine("## Amenities");
            foreach (var a in amenities) sb.Append("- ").AppendLine(a);
            sb.AppendLine();
        }

        var description = d.String("description");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine("## About this property");
            sb.AppendLine(description);
            sb.AppendLine();
        }

        var agentName = d.String("agent_name");
        var agentPhone = d.String("agent_phone");
        var agentEmail = d.String("agent_email");
        var brokerage = d.String("brokerage");
        if (!string.IsNullOrWhiteSpace(agentName) || !string.IsNullOrWhiteSpace(agentPhone)
            || !string.IsNullOrWhiteSpace(agentEmail) || !string.IsNullOrWhiteSpace(brokerage))
        {
            sb.AppendLine("## Contact");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(agentName))
                sb.Append("**").Append(agentName).AppendLine("**  ");
            if (!string.IsNullOrWhiteSpace(brokerage))
                sb.Append(brokerage).AppendLine("  ");
            if (!string.IsNullOrWhiteSpace(agentPhone))
                sb.Append("Phone: ").Append(agentPhone).AppendLine("  ");
            if (!string.IsNullOrWhiteSpace(agentEmail))
                sb.Append("Email: ").AppendLine(agentEmail);
        }

        var fileName = $"listing-{Slug(headline)}.pdf";
        var title = headline;
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, title);
    }

    private static string FormatMoney(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{currency} {amount:N0}");

    private static string FormatFraction(decimal value) =>
        value == Math.Floor(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string NormalizeCurrency(string input) =>
        string.IsNullOrWhiteSpace(input) ? "USD" : input.Trim().ToUpperInvariant();

    private static string EscapePipes(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

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
        return slug.Length == 0 ? "listing" : slug;
    }
}
