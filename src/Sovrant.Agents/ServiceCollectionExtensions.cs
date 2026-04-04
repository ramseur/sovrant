using Microsoft.Extensions.DependencyInjection;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Config;
using Sovrant.Agents.Modern;

namespace Sovrant.Agents;

/// <summary>
/// Extension methods for registering the multi-agent system with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMultiAgentSystem"/> and its supporting services.
    /// The active backend is determined by <paramref name="config"/> (or by reading
    /// <c>AGENT_MODE</c> from the environment when <paramref name="config"/> is <see langword="null"/>).
    /// <para>
    /// Usage: <c>services.AddMultiAgentSystem()</c> — picks up <c>AGENT_MODE</c> automatically.
    /// </para>
    /// </summary>
    public static IServiceCollection AddMultiAgentSystem(
        this IServiceCollection services,
        AgentSystemConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var resolvedConfig = config ?? AgentSystemConfig.FromEnvironment();
        services.AddSingleton(resolvedConfig);

        // Always register modern-backend singletons; the factory only constructs them
        // when UseLegacyAgents == false.
        services.AddSingleton<MultiAgentCoordinator>();
        services.AddSingleton<WorkspaceContext>();

        // The factory resolves the correct backend at first resolution.
        services.AddSingleton<IMultiAgentSystem>(sp =>
            AgentSystemFactory.Create(resolvedConfig, sp));

        return services;
    }
}
