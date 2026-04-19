using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Documents.Generators;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Business;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Documents.Templates.Legal;
using Sovrant.Runtime.Documents.Templates.RealEstate;

namespace Sovrant.Runtime.Documents;

/// <summary>DI wiring for the document generation subsystem.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default set of document generators and the registry.
    /// Consumers (CLI, Desktop, Web, Server, MCP) get document generation
    /// just by calling this after <c>AddSovrantRuntime</c>.
    /// </summary>
    public static IServiceCollection AddSovrantDocuments(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDocumentGenerator, MarkdownGenerator>();
        services.AddSingleton<IDocumentGenerator, PdfSharpGenerator>();
        services.AddSingleton<IDocumentGenerator, MigraDocGenerator>();
        services.AddSingleton<IDocumentGenerator, WordDocumentGenerator>();
        services.AddSingleton<IDocumentGenerator, ExcelDocumentGenerator>();
        services.AddSingleton<IDocumentGenerator, PowerPointGenerator>();

        services.AddSingleton<IDocumentGeneratorRegistry, DocumentGeneratorRegistry>();

        // Business
        services.AddSingleton<IDocumentTemplate, MeetingNotesTemplate>();
        services.AddSingleton<IDocumentTemplate, StatementOfWorkTemplate>();
        services.AddSingleton<IDocumentTemplate, ProjectStatusReportTemplate>();
        services.AddSingleton<IDocumentTemplate, EmployeeOfferLetterTemplate>();
        services.AddSingleton<IDocumentTemplate, PerformanceReviewTemplate>();
        services.AddSingleton<IDocumentTemplate, BusinessPlanTemplate>();

        // Finance
        services.AddSingleton<IDocumentTemplate, InvoiceTemplate>();
        services.AddSingleton<IDocumentTemplate, ExpenseReportTemplate>();
        services.AddSingleton<IDocumentTemplate, FinancialStatementTemplate>();
        services.AddSingleton<IDocumentTemplate, BudgetReportTemplate>();
        services.AddSingleton<IDocumentTemplate, LoanAmortizationTemplate>();
        services.AddSingleton<IDocumentTemplate, AuditReportTemplate>();
        services.AddSingleton<IDocumentTemplate, ProposalTemplate>();

        // Legal
        services.AddSingleton<IDocumentTemplate, NdaTemplate>();

        // Real estate
        services.AddSingleton<IDocumentTemplate, PropertyListingTemplate>();

        services.AddSingleton<ITemplateRegistry, TemplateRegistry>();

        return services;
    }
}
