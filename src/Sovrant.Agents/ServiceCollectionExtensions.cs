using Microsoft.Extensions.DependencyInjection;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Config;
using Sovrant.Agents.Coordination;
using Sovrant.Agents.Orchestration;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Agents.Templates;

namespace Sovrant.Agents;

/// <summary>
/// Extension methods for registering the orchestration system with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOrchestrationSystem"/> and its supporting services.
    /// The active backend is determined by <paramref name="config"/> (or by reading
    /// <c>AGENT_MODE</c> from the environment when <paramref name="config"/> is <see langword="null"/>).
    /// <para>
    /// Usage: <c>services.AddOrchestrationSystem()</c> — picks up <c>AGENT_MODE</c> automatically.
    /// </para>
    /// </summary>
    public static IServiceCollection AddOrchestrationSystem(
        this IServiceCollection services,
        AgentSystemConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // When an explicit config is supplied (tests, programmatic callers) honour it
        // and wrap it in a static live-settings handle so consumers that depend on
        // ILiveSettings<AgentSystemConfig> still resolve. Otherwise build a hot-reloadable
        // wrapper that re-resolves env > workspace_settings > defaults on Reload.
        if (config is not null)
        {
            services.AddSingleton(config);
            services.AddSingleton<Sovrant.Runtime.Workspaces.ILiveSettings<AgentSystemConfig>>(
                _ => Sovrant.Runtime.Workspaces.LiveSettings.Static(config));
        }
        else
        {
            services.AddSingleton<Sovrant.Runtime.Workspaces.LiveSettings<AgentSystemConfig>>(sp =>
            {
                var live = new Sovrant.Runtime.Workspaces.LiveSettings<AgentSystemConfig>(
                    () => AgentSystemConfig.Resolve(
                        sp.GetService<Sovrant.Runtime.Workspaces.IWorkspaceSettingsStore>()));
                sp.GetRequiredService<Sovrant.Runtime.Workspaces.LiveSettingsRegistry>()
                    .Register(live);
                return live;
            });
            services.AddSingleton<Sovrant.Runtime.Workspaces.ILiveSettings<AgentSystemConfig>>(
                sp => sp.GetRequiredService<Sovrant.Runtime.Workspaces.LiveSettings<AgentSystemConfig>>());
            services.AddSingleton(sp =>
                sp.GetRequiredService<Sovrant.Runtime.Workspaces.ILiveSettings<AgentSystemConfig>>().Current);
        }

        // Always register shared-backend singletons; the factory only constructs them
        // when UseIsolatedAgents == false.
        services.AddSingleton<OrchestrationCoordinator>();
        services.AddSingleton<WorkspaceContext>();

        // The factory resolves the correct backend at first resolution.
        services.AddSingleton<IOrchestrationSystem>(sp =>
            AgentSystemFactory.Create(sp.GetRequiredService<AgentSystemConfig>(), sp));

        // Template registry and agent factory
        services.AddSingleton<AgentTemplateRegistry>();
        services.AddSingleton<ITeamRegistry>(sp =>
            new SqliteTeamRegistry(sp.GetRequiredService<Sovrant.Runtime.Storage.ISqliteConnectionFactory>()));
        services.AddSingleton<SovrantAgentFactory>();
        services.AddSingleton<AdHocAgentRunner>();

        // Swarm orchestrator infrastructure
        services.AddSingleton(_ => SwarmConfigLoader.Load());
        services.AddSingleton<Sovrant.Agents.Shared.FileLockManager>();
        services.AddSingleton<Sovrant.Agents.Shared.IFileLockManager>(sp => sp.GetRequiredService<Sovrant.Agents.Shared.FileLockManager>());
        services.AddSingleton<SwarmStateTracker>();
        services.AddSingleton<ISwarmStateTracker>(sp => sp.GetRequiredService<SwarmStateTracker>());
        services.AddSingleton<SwarmSession>();
        services.AddSingleton<ISwarmDecomposer, LlmSwarmDecomposer>();
        services.AddSingleton<SwarmOrchestrator>();
        services.AddSingleton<ISwarmOrchestrator>(sp => sp.GetRequiredService<SwarmOrchestrator>());
        services.AddSingleton<SwarmQualityGate>();
        services.AddSingleton<ISwarmQualityGate>(sp => sp.GetRequiredService<SwarmQualityGate>());

        // Autonomous-driver layer (Phase 67) — registered alongside the LLM
        // driver from Sovrant.Runtime so DriverRegistry resolves either by name.
        services.AddSingleton<Sovrant.Runtime.Missions.IAutonomousDriver, SwarmAutonomousDriver>();

        // Unified agent orchestrator (Phase 52)
        services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

        // PM Coordinator (Phase 57) — inter-group coordination
        services.AddSingleton<IPMCoordinator, PMCoordinator>();

        return services;
    }
}
