using Microsoft.Extensions.DependencyInjection;
using Sovrant.Tools.Agent;
using Sovrant.Tools.Core;
using Sovrant.Tools.Extended;
using Sovrant.Tools.Mcp;
using Sovrant.Tools.PlanMode;
using Sovrant.Tools.Skills;
using Sovrant.Tools.Tasks;
using Sovrant.Tools.Todo;
using Sovrant.Tools.Worktree;

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
        services.AddSingleton<ITool, WebSearchTool>();
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

        // Skill / discovery tools
        services.AddSingleton<ITool, SkillTool>();
        services.AddSingleton<ITool, ToolSearchTool>();

        // MCP resource tools
        services.AddSingleton<ITool, ListMcpResourcesTool>();
        services.AddSingleton<ITool, ReadMcpResourceTool>();

        // Agent tool
        services.AddSingleton<ITool, AgentTool>();

        // Tool registrar
        services.AddSingleton<ToolRegistrar>();

        return services;
    }
}
