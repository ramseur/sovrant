using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Business;

/// <summary>
/// Business plan — structured PDF with executive summary, company
/// overview, market analysis, products/services, marketing &amp; sales
/// strategy, operations, management team, and financial projections.
/// </summary>
public sealed class BusinessPlanTemplate : IDocumentTemplate
{
    public string Id => "business/business-plan";
    public string Name => "Business Plan";
    public string Industry => "business";
    public string Description => "Business plan covering strategy, market, operations, team, and financial projections.";
    public DocumentFormat DefaultFormat => DocumentFormat.StructuredPdf;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "company_name", Type = TemplateFieldType.String, Required = true },
        new() { Name = "plan_date", Type = TemplateFieldType.Date, Required = true },
        new() { Name = "prepared_by", Type = TemplateFieldType.String, Required = false },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
        new() { Name = "executive_summary", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "mission", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "vision", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "company_overview", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "products_services", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "market_analysis", Type = TemplateFieldType.Text, Required = true },
        new() { Name = "target_customers", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "competitors", Type = TemplateFieldType.StringArray, Required = false },
        new() { Name = "marketing_strategy", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "operations_plan", Type = TemplateFieldType.Text, Required = false },
        new()
        {
            Name = "management_team",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "name", Type = TemplateFieldType.String, Required = true },
                new() { Name = "role", Type = TemplateFieldType.String, Required = false },
                new() { Name = "background", Type = TemplateFieldType.Text, Required = false },
            },
        },
        new()
        {
            Name = "financial_projections",
            Type = TemplateFieldType.ObjectArray,
            Required = false,
            ItemFields = new List<TemplateField>
            {
                new() { Name = "year", Type = TemplateFieldType.String, Required = true },
                new() { Name = "revenue", Type = TemplateFieldType.Currency, Required = false },
                new() { Name = "expenses", Type = TemplateFieldType.Currency, Required = false },
                new() { Name = "net_income", Type = TemplateFieldType.Currency, Required = false },
            },
        },
        new() { Name = "funding_request", Type = TemplateFieldType.Text, Required = false },
        new() { Name = "appendix", Type = TemplateFieldType.Text, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var company = d.String("company_name");
        var planDate = d.Date("plan_date");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        var sb = new StringBuilder();
        sb.Append("# ").Append(company).AppendLine(" — Business Plan");
        sb.Append("**Date:** ").AppendLine(TemplateFormat.FormatDate(planDate));
        var preparedBy = d.String("prepared_by");
        if (!string.IsNullOrWhiteSpace(preparedBy))
            sb.Append("**Prepared by:** ").AppendLine(preparedBy);
        sb.AppendLine();

        sb.AppendLine("## Executive Summary");
        sb.AppendLine(d.String("executive_summary"));
        sb.AppendLine();

        var mission = d.String("mission");
        if (!string.IsNullOrWhiteSpace(mission))
        {
            sb.AppendLine("## Mission");
            sb.AppendLine(mission);
            sb.AppendLine();
        }

        var vision = d.String("vision");
        if (!string.IsNullOrWhiteSpace(vision))
        {
            sb.AppendLine("## Vision");
            sb.AppendLine(vision);
            sb.AppendLine();
        }

        sb.AppendLine("## Company Overview");
        sb.AppendLine(d.String("company_overview"));
        sb.AppendLine();

        sb.AppendLine("## Products and Services");
        sb.AppendLine(d.String("products_services"));
        sb.AppendLine();

        sb.AppendLine("## Market Analysis");
        sb.AppendLine(d.String("market_analysis"));
        sb.AppendLine();

        var customers = d.String("target_customers");
        if (!string.IsNullOrWhiteSpace(customers))
        {
            sb.AppendLine("### Target Customers");
            sb.AppendLine(customers);
            sb.AppendLine();
        }

        var competitors = d.StringArray("competitors");
        if (competitors.Count > 0)
        {
            sb.AppendLine("### Competitive Landscape");
            foreach (var c in competitors) sb.Append("- ").AppendLine(c);
            sb.AppendLine();
        }

        var marketing = d.String("marketing_strategy");
        if (!string.IsNullOrWhiteSpace(marketing))
        {
            sb.AppendLine("## Marketing and Sales Strategy");
            sb.AppendLine(marketing);
            sb.AppendLine();
        }

        var operations = d.String("operations_plan");
        if (!string.IsNullOrWhiteSpace(operations))
        {
            sb.AppendLine("## Operations Plan");
            sb.AppendLine(operations);
            sb.AppendLine();
        }

        var team = d.ObjectArray("management_team");
        if (team.Count > 0)
        {
            sb.AppendLine("## Management Team");
            sb.AppendLine();
            foreach (var member in team)
            {
                var name = TemplateFormat.GetString(member, "name");
                var role = TemplateFormat.GetString(member, "role");
                var background = TemplateFormat.GetString(member, "background");
                sb.Append("- **").Append(name).Append("**");
                if (!string.IsNullOrWhiteSpace(role)) sb.Append(" — ").Append(role);
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(background))
                    sb.Append("  ").AppendLine(background);
            }
            sb.AppendLine();
        }

        var projections = d.ObjectArray("financial_projections");
        if (projections.Count > 0)
        {
            sb.AppendLine("## Financial Projections");
            sb.AppendLine();
            sb.AppendLine("| Year | Revenue | Expenses | Net Income |");
            sb.AppendLine("|---|---:|---:|---:|");
            foreach (var p in projections)
            {
                var year = TemplateFormat.GetString(p, "year");
                var revenue = TemplateFormat.GetDecimal(p, "revenue");
                var expenses = TemplateFormat.GetDecimal(p, "expenses");
                var net = TemplateFormat.GetDecimal(p, "net_income");
                sb.Append("| ").Append(TemplateFormat.EscapePipes(year))
                  .Append(" | ").Append(TemplateFormat.FormatMoney(revenue, currency))
                  .Append(" | ").Append(TemplateFormat.FormatMoney(expenses, currency))
                  .Append(" | ").Append(TemplateFormat.FormatMoney(net, currency))
                  .AppendLine(" |");
            }
            sb.AppendLine();
        }

        var funding = d.String("funding_request");
        if (!string.IsNullOrWhiteSpace(funding))
        {
            sb.AppendLine("## Funding Request");
            sb.AppendLine(funding);
            sb.AppendLine();
        }

        var appendix = d.String("appendix");
        if (!string.IsNullOrWhiteSpace(appendix))
        {
            sb.AppendLine("## Appendix");
            sb.AppendLine(appendix);
            sb.AppendLine();
        }

        var fileName = $"business-plan-{TemplateFormat.Slug(company)}-{TemplateFormat.FormatDate(planDate)}.pdf";
        return new TemplateRenderResult(DocumentFormat.StructuredPdf, sb.ToString(), fileName, $"{company} — Business Plan");
    }
}
