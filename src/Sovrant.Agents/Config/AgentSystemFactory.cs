using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Legacy;
using Sovrant.Agents.Modern;

namespace Sovrant.Agents.Config;

/// <summary>
/// Creates the <see cref="IMultiAgentSystem"/> selected by <see cref="AgentSystemConfig"/>.
/// Used as a DI factory so the rest of the system depends only on <see cref="IMultiAgentSystem"/>
/// and never on the concrete backend.
/// </summary>
public static class AgentSystemFactory
{
    /// <summary>
    /// Instantiates and returns the <see cref="IMultiAgentSystem"/> specified by
    /// <paramref name="config"/>. All dependencies are resolved from <paramref name="services"/>.
    /// </summary>
    public static IMultiAgentSystem Create(AgentSystemConfig config, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(services);

        if (config.UseLegacyAgents)
        {
            return new ProcessBasedMultiAgentSystem(
                config,
                services.GetRequiredService<ILogger<ProcessBasedMultiAgentSystem>>());
        }

        return new InProcessMultiAgentSystem(
            services.GetRequiredService<MultiAgentCoordinator>(),
            services.GetRequiredService<WorkspaceContext>(),
            services.GetRequiredService<ILogger<InProcessMultiAgentSystem>>());
    }
}
