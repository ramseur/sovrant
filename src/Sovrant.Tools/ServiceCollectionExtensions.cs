using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Lsp;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Prompt;
using Sovrant.Tools.Agent;
using Sovrant.Tools.Core;
using Sovrant.Tools.Extended;
using Sovrant.Tools.Lsp;
using Sovrant.Tools.Mcp;
using Sovrant.Tools.PlanMode;
using Sovrant.Tools.Skills;
using Sovrant.Tools.Tasks;
using Sovrant.Tools.Todo;
using Sovrant.Tools.Quality;
using Sovrant.Tools.Shell;
using Sovrant.Tools.Missions;
using Sovrant.Tools.Coordination;
using Sovrant.Tools.Swarm;
using Sovrant.Tools.Team;
using Sovrant.Tools.Artifacts;
using Sovrant.Tools.Documents;
using Sovrant.Tools.Worktree;
using Sovrant.Tools.Projects.Scaffolds.Node;
using Sovrant.Tools.Projects.Scaffolds.DotNet;
using Sovrant.Tools.Projects.Scaffolds.Python;
using Sovrant.Tools.Projects.Scaffolds.Go;
using Sovrant.Tools.Projects.Scaffolds.Rust;
using Sovrant.Tools.Projects.Scaffolds.Java;
using Sovrant.Tools.Projects.Scaffolds.Minimal;
using Sovrant.Tools.Projects;
using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools;

/// <summary>Extension methods for registering all Sovrant built-in tools.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all built-in tools and the <see cref="ToolRegistrar"/>.
    /// Call <see cref="ToolRegistrar.RegisterAll"/> after building the service provider
    /// to seed the <see cref="Sovrant.Runtime.Tools.IToolRegistry"/>.
    /// </summary>
    public static IServiceCollection AddSovrantTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // HTTP clients for web tools
        services.AddHttpClient("WebFetch");
        services.AddHttpClient("WebSearch");

        // User input provider (can be replaced by CLI layer)
        services.AddSingleton<IUserInputProvider, NullUserInputProvider>();

        // In-session singletons
        services.AddSingleton<TodoState>();
        services.AddSingleton<BackgroundTaskRegistry>();
        services.AddSingleton<WorktreeState>();
        services.AddSingleton<ShellSessionState>();
        services.AddSingleton<ShellEnvironment>();

        // Core tools
        services.AddSingleton<ITool, ReadFileTool>();
        services.AddSingleton<ITool, WriteFileTool>();
        services.AddSingleton<ITool, EditFileTool>();
        services.AddSingleton<ITool, BashTool>();
        services.AddSingleton<ITool, GlobTool>();
        services.AddSingleton<ITool, GrepTool>();
        services.AddSingleton<ITool, ListDirectoryTool>();
        services.AddSingleton<ITool, WebFetchTool>();

        // Extended tools
        services.AddSingleton<ITool>(sp =>
            new WebSearchTool(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetService<Sovrant.Api.Config.CredentialConfig>(),
                sp.GetService<Sovrant.Runtime.Mcp.ICredentialStore>(),
                sp.GetService<Sovrant.Api.Routing.ISmartRouter>(),
                sp.GetService<Sovrant.Runtime.Config.SovrantConfig>(),
                sp.GetService<Sovrant.Api.Config.WebSearchOptions>()));
        services.AddSingleton<ITool, NotebookEditTool>();
        services.AddSingleton<ITool, ReplTool>();
        services.AddSingleton<ITool, PowerShellTool>();
        services.AddSingleton<ITool, SleepTool>();
        services.AddSingleton<ITool, AskUserQuestionTool>();

        // Todo tool
        services.AddSingleton<ITool, TodoWriteTool>();

        // Task tools
        services.AddSingleton<ITool, TaskCreateTool>();
        services.AddSingleton<ITool, TaskGetTool>();
        services.AddSingleton<ITool, TaskListTool>();
        services.AddSingleton<ITool, TaskOutputTool>();
        services.AddSingleton<ITool, TaskStopTool>();
        services.AddSingleton<ITool, TaskUpdateTool>();

        // Plan mode tools
        services.AddSingleton<ITool, EnterPlanModeTool>();
        services.AddSingleton<ITool, ExitPlanModeTool>();

        // Worktree tools
        services.AddSingleton<ITool, EnterWorktreeTool>();
        services.AddSingleton<ITool, ExitWorktreeTool>();

        // Skill system — registry reads/writes IKnowledgeStore (Phase 112)
        services.AddSingleton<SkillRegistry>(sp => new SkillRegistry(
            sp.GetRequiredService<Sovrant.Runtime.Knowledge.IKnowledgeStore>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillRegistry>>()));
        services.AddSingleton<SkillRunner>();
        services.AddSingleton<ICapabilityCatalog>(sp => new CapabilityCatalog(
            sp.GetRequiredService<SkillRegistry>(),
            sp.GetRequiredService<Sovrant.Agents.Templates.AgentTemplateRegistry>(),
            sp.GetRequiredService<Sovrant.Tools.Templates.UserToolTemplateRegistry>()));
        services.AddSingleton<ITool, SkillTool>();
        services.AddSingleton<ITool>(sp => new SkillCreateTool(
            sp.GetRequiredService<SkillRegistry>(),
            sp.GetRequiredService<Sovrant.Runtime.Knowledge.IKnowledgeStore>()));
        services.AddSingleton<ITool, ToolSearchTool>();

        // User-authored tool templates — DB-backed (Phase 112C).
        services.AddSingleton<Sovrant.Tools.Templates.UserToolTemplateRegistry>(sp =>
            new Sovrant.Tools.Templates.UserToolTemplateRegistry(
                sp.GetRequiredService<Sovrant.Runtime.Knowledge.IKnowledgeStore>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Sovrant.Tools.Templates.UserToolTemplateRegistry>>()));

        // MCP resource tools, dynamic proxy, and OAuth
        services.AddSingleton<ITool, ListMcpResourcesTool>();
        services.AddSingleton<ITool, ReadMcpResourceTool>();
        services.AddSingleton<ITool, McpProxyTool>();
        services.AddSingleton<ITool, McpAuthTool>();

        // Agent tool
        services.AddSingleton<ITool, AgentTool>();

        // Team tools
        services.AddSingleton<ITool, TeamCreateTool>();
        services.AddSingleton<ITool, TeamDeleteTool>();
        services.AddSingleton<ITool, TeamStatusTool>();
        services.AddSingleton<ITool>(sp =>
        {
            var registry = sp.GetRequiredService<Sovrant.Agents.Teams.ITeamRegistry>();
            var factory = sp.GetRequiredService<Sovrant.Agents.Shared.SovrantAgentFactory>();

            // SovrantAgentFactory always creates in-process SovrantAgent instances,
            // so team delegation must use InProcessOrchestrationSystem regardless of
            // the global AGENT_MODE setting (which may be ProcessBased).
            var coordinator = sp.GetRequiredService<Sovrant.Agents.Shared.OrchestrationCoordinator>();
            var workspace = sp.GetRequiredService<Sovrant.Agents.Shared.WorkspaceContext>();
            var logger = sp.GetRequiredService<ILogger<Sovrant.Agents.Shared.InProcessOrchestrationSystem>>();
            var agentSystem = new Sovrant.Agents.Shared.InProcessOrchestrationSystem(coordinator, workspace, logger);

            return new TeamDelegateTool(registry, agentSystem, member => factory.Create(member));
        });

        // Team run + publish tools (Phase 52)
        services.AddSingleton<ITool, TeamRunTool>();
        services.AddSingleton<ITool, TeamPublishTool>();

        // Mission tool — lets running agents spawn and drive sub-missions
        services.AddSingleton<ITool, MissionTool>();

        // Artifact tools — agent-side producer interface (Phase 41)
        services.AddSingleton<ITool, ArtifactTool>();

        // Document generation (Phase 66) — markdown, simple PDF, structured PDF.
        services.AddSovrantDocuments();
        services.AddSingleton<ITool, DocumentGenerateTool>();
        services.AddSingleton<ITool, DocumentFromTemplateTool>();
        services.AddSingleton<ITool, DocumentListTemplatesTool>();
        services.AddSingleton<ITool, DocumentSuggestTemplateTool>();
        services.AddSingleton<ITool, DocumentPackageTool>();
        services.AddSingleton<ITool, DocumentListPackagesTool>();

        // Phase 73 — project scaffold implementations
        services.AddSingleton<IProjectTemplate, NodeCliScaffold>();
        services.AddSingleton<IProjectTemplate, NodeExpressApiScaffold>();
        services.AddSingleton<IProjectTemplate, NodeLibraryScaffold>();
        services.AddSingleton<IProjectTemplate, NodeNextJsScaffold>();
        services.AddSingleton<IProjectTemplate, NodeMonorepoScaffold>();
        services.AddSingleton<IProjectTemplate, DotNetConsoleScaffold>();
        services.AddSingleton<IProjectTemplate, DotNetWebApiScaffold>();
        services.AddSingleton<IProjectTemplate, DotNetLibraryScaffold>();
        services.AddSingleton<IProjectTemplate, DotNetWorkerScaffold>();
        services.AddSingleton<IProjectTemplate, DotNetBlazorScaffold>();
        services.AddSingleton<IProjectTemplate, PythonFastApiScaffold>();
        services.AddSingleton<IProjectTemplate, PythonScriptScaffold>();
        services.AddSingleton<IProjectTemplate, GoApiScaffold>();
        services.AddSingleton<IProjectTemplate, RustCliScaffold>();
        services.AddSingleton<IProjectTemplate, JavaMavenAppScaffold>();
        services.AddSingleton<IProjectTemplate, KotlinConsoleScaffold>();
        services.AddSingleton<IProjectTemplate, RubyScriptScaffold>();
        services.AddSingleton<IProjectTemplate, SwiftCliScaffold>();
        services.AddSingleton<IProjectTemplate, LuaScriptScaffold>();
        services.AddSingleton<IProjectTemplate, ZigCliScaffold>();
        services.AddSingleton<IProjectTemplate, CppCmakeScaffold>();

        // Phase 73 — code creation tools (Phase 108: inject IKnowledgeStore for language guidelines)
        services.AddSingleton<ITool>(sp => new CodeCreateTool(
            sp.GetRequiredService<ProjectTemplateRegistry>(),
            sp.GetRequiredService<Sovrant.Runtime.Artifacts.IArtifactStore>(),
            sp.GetService<Sovrant.Runtime.Knowledge.IKnowledgeStore>()));
        services.AddSingleton<ITool, CodeCreateMultiTool>();
        services.AddSingleton<ITool, CodeListTemplatesTool>();
        services.AddSingleton<ITool, CodeValidateTool>();

        // Quality / verification tools
        services.AddSingleton<ITool, VerifyTool>();

        // Swarm tools
        services.AddSingleton<ISwarmProgressReporter, NullSwarmProgressReporter>();
        services.AddSingleton<ITool, SwarmTool>();
        services.AddSingleton<ITool, SwarmStatusTool>();

        // Coordination tools (Phase 57)
        services.AddSingleton<ITool, CoordinationStatusTool>();

        // LSP tools — language-server entries are loaded from ILspServerStore
        // (the lsp_servers table in V019), not settings.json. Reads tolerate
        // a missing table because the DI resolve can happen before
        // InitializeRuntimeAsync runs migrations (e.g. when a test resolves
        // the tool registry to introspect routes).
        services.AddSingleton<ILspClientManager>(sp =>
        {
            var store = sp.GetRequiredService<Sovrant.Runtime.Mcp.ILspServerStore>();
            var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            IReadOnlyDictionary<string, Sovrant.Runtime.Mcp.LspServerEntry> entries;
            try
            {
                entries = store.GetAllAsync().GetAwaiter().GetResult();
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // lsp_servers table not yet created — DB hasn't been migrated.
                entries = new Dictionary<string, Sovrant.Runtime.Mcp.LspServerEntry>(StringComparer.OrdinalIgnoreCase);
            }
            var configs = entries.Select(kvp => new LspServerConfig
            {
                Language = kvp.Key,
                Command = kvp.Value.Command,
                Args = kvp.Value.Args,
                Env = kvp.Value.Env,
            });
            return new LspClientManager(configs, loggerFactory);
        });
        services.AddSingleton<ITool, LspHoverTool>();
        services.AddSingleton<ITool, LspDefinitionTool>();
        services.AddSingleton<ITool, LspReferencesTool>();
        services.AddSingleton<ITool, LspDiagnosticsTool>();
        services.AddSingleton<ITool, LspRenameTool>();

        // Tool registrar
        services.AddSingleton<ToolRegistrar>();

        return services;
    }
}
