using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Modern;

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
    public SovrantAgent Create(TeamMemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var systemPrompt = AgentPrompts.GetSystemPrompt(member.Role, member.SystemPrompt);
        var runtime = CreateRuntime(systemPrompt, member.AllowedTools);
        var logger = _loggerFactory.CreateLogger($"Sovrant.Agents.{member.Name}");
        return new SovrantAgent(member.Name, member.Role, runtime, logger);
    }

    /// <summary>Creates a <see cref="SovrantAgent"/> from raw parameters.</summary>
    public SovrantAgent Create(
        string name,
        AgentRole role,
        string? customInstructions = null,
        IReadOnlyList<string>? allowedTools = null)
    {
        var systemPrompt = AgentPrompts.GetSystemPrompt(role, customInstructions);
        var runtime = CreateRuntime(systemPrompt, allowedTools);
        var logger = _loggerFactory.CreateLogger($"Sovrant.Agents.{name}");
        return new SovrantAgent(name, role, runtime, logger);
    }

    private ConversationRuntime CreateRuntime(
        string systemPrompt,
        IReadOnlyList<string>? allowedTools)
    {
        var router = _services.GetRequiredService<Api.Routing.ISmartRouter>();
        var executor = _services.GetRequiredService<IToolExecutor>();
        var sessionStore = _services.GetRequiredService<Runtime.Session.ISessionStore>();
        var config = _services.GetRequiredService<Runtime.Config.SovrantConfig>();
        var logger = _loggerFactory.CreateLogger<ConversationRuntime>();

        IToolRegistry registry = _services.GetRequiredService<IToolRegistry>();

        // If tool restriction is requested, wrap the registry in a filter.
        if (allowedTools is { Count: > 0 })
            registry = new FilteredToolRegistry(registry, new HashSet<string>(allowedTools, StringComparer.Ordinal));

        return new ConversationRuntime(router, executor, registry, sessionStore, config, logger, systemPrompt);
    }
}
