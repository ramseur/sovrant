using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Shared;

/// <summary>
/// Creates configured <see cref="SovrantAgent"/> instances from <see cref="TeamMemberInfo"/>
/// or raw parameters. Resolves transient <see cref="IConversationRuntime"/> from DI with
/// role-specific system prompts and optional tool filtering.
/// </summary>
public sealed class SovrantAgentFactory
{
    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;

    public SovrantAgentFactory(IServiceProvider services, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _services = services;
        _loggerFactory = loggerFactory;
    }

    /// <summary>Creates a <see cref="SovrantAgent"/> from a team member definition.</summary>
    public SovrantAgent Create(TeamMemberInfo member, IToolExecutor? executorOverride = null)
    {
        ArgumentNullException.ThrowIfNull(member);

        var systemPrompt = AgentPrompts.GetSystemPrompt(member.Role, member.SystemPrompt);
        var runtime = CreateRuntime(systemPrompt, member.AllowedTools, member.Model, executorOverride);
        var logger = _loggerFactory.CreateLogger($"Sovrant.Agents.{member.Name}");
        return new SovrantAgent(member.Name, member.Role, runtime, logger);
    }

    /// <summary>Creates a <see cref="SovrantAgent"/> from an <see cref="AgentTemplate"/>.</summary>
    /// <param name="template">The template providing system prompt and tool restrictions.</param>
    /// <param name="customInstructions">Additional instructions appended to the template's prompt.</param>
    /// <param name="modelOverride">Explicit model override. Resolved via ModelLevels if null.</param>
    /// <param name="executorOverride">Optional tool executor override (e.g. for swarm file-lock enforcement).</param>
    public SovrantAgent Create(
        AgentTemplate template,
        string? customInstructions = null,
        string? modelOverride = null,
        IToolExecutor? executorOverride = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        var config = _services.GetRequiredService<Runtime.Config.SovrantConfig>();
        var resolvedModel = ResolveModel(modelOverride, template.RecommendedLevel, config);

        var prompt = string.IsNullOrWhiteSpace(customInstructions)
            ? template.SystemPrompt
            : template.SystemPrompt + "\n\n" + customInstructions;

        var tools = template.AllowedTools.Count > 0 ? template.AllowedTools : null;
        var runtime = CreateRuntime(prompt, tools, resolvedModel == config.Model ? null : resolvedModel, executorOverride);
        var logger = _loggerFactory.CreateLogger($"Sovrant.Agents.{template.Name}");
        return new SovrantAgent(template.Name, template.Role, runtime, logger);
    }

    /// <summary>Creates a <see cref="SovrantAgent"/> from raw parameters.</summary>
    public SovrantAgent Create(
        string name,
        AgentRole role,
        string? customInstructions = null,
        IReadOnlyList<string>? allowedTools = null,
        IToolExecutor? executorOverride = null)
    {
        var systemPrompt = AgentPrompts.GetSystemPrompt(role, customInstructions);
        var runtime = CreateRuntime(systemPrompt, allowedTools, modelOverride: null, executorOverride);
        var logger = _loggerFactory.CreateLogger($"Sovrant.Agents.{name}");
        return new SovrantAgent(name, role, runtime, logger);
    }

    /// <summary>Returns the default <see cref="IToolExecutor"/> from DI (for wrapping in decorators).</summary>
    public IToolExecutor GetDefaultExecutor() =>
        _services.GetRequiredService<IToolExecutor>();

    /// <summary>
    /// Resolves the model to use for an agent.
    /// Priority: explicit <paramref name="userOverride"/> → <c>ModelLevels[level]</c> → session default.
    /// </summary>
    internal static string ResolveModel(
        string? userOverride,
        RecommendedLevel level,
        Runtime.Config.SovrantConfig config)
    {
        if (!string.IsNullOrWhiteSpace(userOverride)) return userOverride;
        if (config.ModelLevels.TryGetValue(level.ToString(), out var levelModel)) return levelModel;
        return config.Model;
    }

    private ConversationRuntime CreateRuntime(
        string systemPrompt,
        IReadOnlyList<string>? allowedTools,
        string? modelOverride,
        IToolExecutor? executorOverride = null)
    {
        var router = _services.GetRequiredService<Api.Routing.ISmartRouter>();
        var executor = executorOverride ?? _services.GetRequiredService<IToolExecutor>();
        var sessionStore = _services.GetRequiredService<Runtime.Session.ISessionStore>();
        var config = _services.GetRequiredService<Runtime.Config.SovrantConfig>();
        var logger = _loggerFactory.CreateLogger<ConversationRuntime>();

        // Apply model override if different from the default.
        if (!string.IsNullOrWhiteSpace(modelOverride) && modelOverride != config.Model)
            config = config.WithModel(modelOverride);

        IToolRegistry registry = _services.GetRequiredService<IToolRegistry>();

        // If tool restriction is requested, wrap the registry in a filter.
        if (allowedTools is { Count: > 0 })
            registry = new FilteredToolRegistry(registry, new HashSet<string>(allowedTools, StringComparer.Ordinal));

        var hookRunner = _services.GetService<IHookRunner>();
        var settings = _services.GetService<Runtime.Workspaces.IWorkspaceSettingsStore>();
        return new ConversationRuntime(router, executor, registry, sessionStore, config, logger, hookRunner, memoryInjector: null, systemPromptOverride: systemPrompt, settings: settings);
    }
}
