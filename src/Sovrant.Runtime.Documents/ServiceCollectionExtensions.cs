using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Documents.Generators;

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

        return services;
    }
}
