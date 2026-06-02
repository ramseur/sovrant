using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Documents.Generators;
using Sovrant.Runtime.Documents.Packages;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Documents.Trust;
using Sovrant.Runtime.Knowledge;

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

        // Phase 112D: 42 built-in Markdown templates are now DB-backed (kind=document-templates).
        // Only the two Excel-format templates remain as C# — they emit structured JSON bodies
        // (preamble / headers / rows) that cannot be expressed as simple Scriban Markdown.
        services.AddSingleton<IDocumentTemplate, ExpenseReportTemplate>();
        services.AddSingleton<IDocumentTemplate, LoanAmortizationTemplate>();

        // TemplateRegistry merges the C# Excel templates above with DB-backed templates
        // loaded from IKnowledgeStore (kind=document-templates).
        services.AddSingleton<ITemplateRegistry>(sp =>
            new TemplateRegistry(
                sp.GetServices<IDocumentTemplate>(),
                sp.GetRequiredService<IKnowledgeStore>(),
                sp.GetRequiredService<ILogger<TemplateRegistry>>()));

        // User-authored document templates — DB-backed (Phase 112C). Coexists with the
        // code-defined IDocumentTemplate registry above; the Knowledge UI renders both.
        services.AddSingleton<UserDocumentTemplateRegistry>(sp =>
            new UserDocumentTemplateRegistry(
                sp.GetRequiredService<Sovrant.Runtime.Knowledge.IKnowledgeStore>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UserDocumentTemplateRegistry>>()));

        // Document packages — bundles of templates rendered against shared data.
        foreach (var pkg in BuiltInDocumentPackages.All)
            services.AddSingleton(pkg);
        services.AddSingleton<IDocumentPackageRegistry>(sp =>
            new DocumentPackageRegistry(sp.GetServices<DocumentPackage>()));

        // Default trust gate — refuses healthcare/* templates without
        // explicit PHI consent. Apps wanting different policy register
        // their own IDocumentTrustGate before calling AddSovrantDocuments
        // (TryAddSingleton honors existing registration).
        services.TryAddSingleton<IDocumentTrustGate, HealthcarePhiTrustGate>();

        return services;
    }
}
