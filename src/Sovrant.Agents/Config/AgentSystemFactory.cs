using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Isolated;
using Sovrant.Agents.Shared;

namespace Sovrant.Agents.Config;

/// <summary>
/// Creates the <see cref="IOrchestrationSystem"/> selected by <see cref="AgentSystemConfig"/>.
/// Used as a DI factory so the rest of the system depends only on <see cref="IOrchestrationSystem"/>
/// and never on the concrete backend.
/// </summary>
public static class AgentSystemFactory
{
    /// <summary>
    /// Instantiates and returns the <see cref="IOrchestrationSystem"/> specified by
    /// <paramref name="config"/>. All dependencies are resolved from <paramref name="services"/>.
    /// </summary>
    public static IOrchestrationSystem Create(AgentSystemConfig config, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(services);

        if (config.UseIsolatedAgents)
        {
            return new ProcessBasedOrchestrationSystem(
                services.GetRequiredService<Sovrant.Runtime.Workspaces.ILiveSettings<AgentSystemConfig>>(),
                services.GetRequiredService<ILogger<ProcessBasedOrchestrationSystem>>());
        }

        return new InProcessOrchestrationSystem(
            services.GetRequiredService<OrchestrationCoordinator>(),
            services.GetRequiredService<WorkspaceContext>(),
            services.GetRequiredService<ILogger<InProcessOrchestrationSystem>>());
    }
}
