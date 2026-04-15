using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;

namespace Sovrant.McpServer;

/// <summary>DI registration for Sovrant's MCP server mode (stdio transport).</summary>
public static class McpServerSetup
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static readonly Action<ILogger, string, Exception?> s_logToolCall =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "McpToolCall"), "MCP tool call: {ToolName}");

    private static readonly Action<ILogger, string, Exception?> s_logToolCompleted =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "McpToolCompleted"), "MCP tool completed: {ToolName}");

    private static readonly Action<ILogger, string, Exception?> s_logToolCancelled =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, "McpToolCancelled"), "MCP tool cancelled: {ToolName}");

    private static readonly Action<ILogger, string, Exception?> s_logToolFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "McpToolFailed"), "MCP tool failed: {ToolName}");

    private static readonly Action<ILogger, string, Exception?> s_logLoggingLevel =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "McpLoggingLevel"), "MCP logging level set to: {Level}");

    /// <summary>Tracks resource subscriptions per URI (thread-safe, shared across MCP sessions).</summary>
    private static readonly ConcurrentDictionary<string, int> s_resourceSubscriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers the MCP server with stdio transport, bridging all <see cref="IToolRegistry"/>
    /// tools and session/config resources to the MCP protocol.
    /// </summary>
    public static IServiceCollection AddSovrantMcpServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMcpServer(ConfigureMcpServerOptions)
            .WithStdioServerTransport()
            .AddSovrantMcpHandlers();

        return services;
    }

    /// <summary>
    /// Configures common MCP server options (server info + capabilities).
    /// Called by both stdio and HTTP registration paths.
    /// </summary>
    public static void ConfigureMcpServerOptions(ModelContextProtocol.Server.McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ServerInfo = new Implementation { Name = "sovrant", Version = "1.0.0" };
        options.Capabilities = new ServerCapabilities
        {
            Prompts = new PromptsCapability(),
            Resources = new ResourcesCapability { Subscribe = true },
            Logging = new LoggingCapability(),
        };
    }

    /// <summary>
    /// Registers all Sovrant MCP protocol handlers on a builder. Shared between
    /// stdio and HTTP transport paths.
    /// </summary>
    public static IMcpServerBuilder AddSovrantMcpHandlers(this IMcpServerBuilder builder)
    {
        return builder
            // Tools
            .WithListToolsHandler((context, ct) => new ValueTask<ListToolsResult>(ListToolsAsync(context.Services!, ct)))
            .WithCallToolHandler(async (context, ct) => await CallToolAsync(context.Services!, context.Params!, ct).ConfigureAwait(false))
            // Resources
            .WithListResourcesHandler((_, _) => new ValueTask<ListResourcesResult>(ListResourcesSync()))
            .WithReadResourceHandler(async (context, ct) => await ReadResourceAsync(context.Services!, context.Params!, ct).ConfigureAwait(false))
            .WithListResourceTemplatesHandler((_, _) => new ValueTask<ListResourceTemplatesResult>(ListResourceTemplatesSync()))
            // Prompts
            .WithListPromptsHandler((_, _) => new ValueTask<ListPromptsResult>(ListPromptsSync()))
            .WithGetPromptHandler((context, _) => new ValueTask<GetPromptResult>(GetPromptSync(context.Params!)))
            // Completions
            .WithCompleteHandler(async (context, ct) => await CompleteAsync(context.Services!, context.Params!, ct).ConfigureAwait(false))
            // Logging
            .WithSetLoggingLevelHandler((context, _) => new ValueTask<EmptyResult>(SetLoggingLevel(context.Services!, context.Params!)))
            // Resource subscriptions
            .WithSubscribeToResourcesHandler((context, _) => new ValueTask<EmptyResult>(SubscribeToResource(context.Params!)))
            .WithUnsubscribeFromResourcesHandler((context, _) => new ValueTask<EmptyResult>(UnsubscribeFromResource(context.Params!)));
    }

    // ── Tools ────────────────────────────────────────────────────────────────

    private static ListToolsResult ListToolsAsync(
        IServiceProvider services,
        CancellationToken ct)
    {
        _ = ct;
        var registry = services.GetRequiredService<IToolRegistry>();
        var allowList = ToolFilter.GetAllowList();

        var tools = new List<Tool>();

        foreach (var def in registry.GetDefinitions())
        {
            if (!ToolFilter.IsAllowed(def.Name, allowList))
                continue;

            tools.Add(new Tool
            {
                Name = def.Name,
                Description = def.Description ?? def.Name,
                InputSchema = def.InputSchema,
            });
        }

        // Always include the chat tool for full agentic turns.
        tools.Add(ChatToolHandler.Definition);

        return new ListToolsResult { Tools = tools };
    }

    private static async Task<CallToolResult> CallToolAsync(
        IServiceProvider services,
        CallToolRequestParams requestParams,
        CancellationToken ct)
    {
        var toolName = requestParams.Name ?? string.Empty;
        var arguments = requestParams.Arguments;

        if (string.Equals(toolName, ChatToolHandler.ToolName, StringComparison.Ordinal))
        {
            return await ChatToolHandler.ExecuteAsync(services, arguments, ct)
                .ConfigureAwait(false);
        }

        var registry = services.GetRequiredService<IToolRegistry>();

        if (!registry.TryGetHandler(toolName, out var handler) || handler is null)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"Unknown tool: {toolName}" }],
            };
        }

        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("Sovrant.McpServer");

        try
        {
            if (logger is not null) s_logToolCall(logger, toolName, null);
            var input = ConvertArguments(arguments);
            var result = await handler(input, ct).ConfigureAwait(false);
            if (logger is not null) s_logToolCompleted(logger, toolName, null);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = result }],
            };
        }
        catch (OperationCanceledException)
        {
            if (logger is not null) s_logToolCancelled(logger, toolName, null);
            return ErrorResult(new InvalidOperationException("Tool execution was cancelled."));
        }
#pragma warning disable CA1031 // Catch-all is intentional: MCP tools must never crash the server process
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (logger is not null) s_logToolFailed(logger, toolName, ex);
            return ErrorResult(ex);
        }
    }

    // ── Resources ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the MCP owner identity. MCP runs over stdio (single-user), so we
    /// use the same SOVRANT_USER_ID / OS-username convention as Desktop and Web.
    /// </summary>
    private static string GetOwnerUserId() =>
        Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

    private static ListResourcesResult ListResourcesSync()
    {
        var resources = new List<Resource>
        {
            new()
            {
                Uri = "sovrant://sessions",
                Name = "Session list",
                Description = "Session IDs owned by the current user.",
                MimeType = "application/json",
            },
            // Phase G (9.17): sovrant://config removed — exposed internal configuration
            // (model selection, provider URLs, permission modes) to any MCP client.
        };

        return new ListResourcesResult { Resources = resources };
    }

    private static async Task<ReadResourceResult> ReadResourceAsync(
        IServiceProvider services,
        ReadResourceRequestParams requestParams,
        CancellationToken ct)
    {
        var uri = requestParams.Uri ?? string.Empty;
        var ownerUserId = GetOwnerUserId();

        if (string.Equals(uri, "sovrant://sessions", StringComparison.Ordinal))
        {
            var store = services.GetRequiredService<ISessionStore>();
            var sessions = await store.ListAsync(ownerUserId, ct).ConfigureAwait(false);
            var json = JsonSerializer.Serialize(sessions, s_jsonOptions);
            return new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = uri, MimeType = "application/json", Text = json }],
            };
        }

        // Phase G (9.17): sovrant://config resource removed — was exposing internal
        // configuration (model, provider URLs, permission modes) to any MCP client.

        // sovrant://sessions/{id}
        const string sessionsPrefix = "sovrant://sessions/";
        if (uri.StartsWith(sessionsPrefix, StringComparison.Ordinal))
        {
            var sessionId = Uri.UnescapeDataString(uri[sessionsPrefix.Length..]);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return new ReadResourceResult
                {
                    Contents = [new TextResourceContents { Uri = uri, MimeType = "text/plain", Text = "Invalid session ID." }],
                };
            }

            var store = services.GetRequiredService<ISessionStore>();
            var entries = await store.LoadAsync(sessionId, ownerUserId, ct).ConfigureAwait(false);
            var json = JsonSerializer.Serialize(entries, s_jsonOptions);
            return new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = uri, MimeType = "application/json", Text = json }],
            };
        }

        return new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = uri, MimeType = "text/plain", Text = $"Unknown resource: {uri}" }],
        };
    }

    // ── Resource Templates ──────────────────────────────────────────────────

    private static ListResourceTemplatesResult ListResourceTemplatesSync()
    {
        var templates = new List<ResourceTemplate>
        {
            new()
            {
                UriTemplate = "sovrant://sessions/{session_id}",
                Name = "Session history",
                Description = "Conversation history for a specific session.",
                MimeType = "application/json",
            },
        };

        return new ListResourceTemplatesResult { ResourceTemplates = templates };
    }

    // ── Prompts ──────────────────────────────────────────────────────────────

    private static ListPromptsResult ListPromptsSync()
    {
        var prompts = new List<Prompt>
        {
            new()
            {
                Name = "code-review",
                Description = "Review code for issues, best practices, and improvements.",
                Arguments =
                [
                    new PromptArgument { Name = "code", Description = "The code to review.", Required = true },
                    new PromptArgument { Name = "language", Description = "Programming language (e.g. csharp, python).", Required = false },
                ],
            },
            new()
            {
                Name = "explain",
                Description = "Explain a concept, code snippet, or error message.",
                Arguments =
                [
                    new PromptArgument { Name = "topic", Description = "What to explain.", Required = true },
                ],
            },
            new()
            {
                Name = "summarize-session",
                Description = "Summarize the conversation history from a session.",
                Arguments =
                [
                    new PromptArgument { Name = "session_id", Description = "The session ID to summarize.", Required = true },
                ],
            },
            new()
            {
                Name = "refactor",
                Description = "Suggest refactoring improvements for the given code.",
                Arguments =
                [
                    new PromptArgument { Name = "code", Description = "The code to refactor.", Required = true },
                    new PromptArgument { Name = "goal", Description = "Refactoring goal (e.g. readability, performance).", Required = false },
                ],
            },
        };

        return new ListPromptsResult { Prompts = prompts };
    }

    private static GetPromptResult GetPromptSync(GetPromptRequestParams requestParams)
    {
        var name = requestParams.Name ?? string.Empty;
        var rawArgs = requestParams.Arguments;

        // Convert IDictionary<string, JsonElement> → simple string dict for convenience.
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rawArgs is not null)
        {
            foreach (var kvp in rawArgs)
            {
                args[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.String
                    ? kvp.Value.GetString() ?? string.Empty
                    : kvp.Value.GetRawText();
            }
        }

        return name switch
        {
            "code-review" => BuildCodeReviewPrompt(args),
            "explain" => BuildExplainPrompt(args),
            "summarize-session" => BuildSummarizeSessionPrompt(args),
            "refactor" => BuildRefactorPrompt(args),
            _ => new GetPromptResult
            {
                Description = $"Unknown prompt: {name}",
                Messages = [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = $"Unknown prompt: {name}" } }],
            },
        };
    }

    private static GetPromptResult BuildCodeReviewPrompt(Dictionary<string, string> args)
    {
        args.TryGetValue("code", out var code);
        args.TryGetValue("language", out var language);
        var langHint = string.IsNullOrEmpty(language) ? "" : $" (Language: {language})";

        return new GetPromptResult
        {
            Description = "Code review prompt",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock
                    {
                        Text = $"Please review the following code{langHint} for potential issues, " +
                               $"best practices violations, security concerns, and suggest improvements:\n\n```\n{code ?? "(no code provided)"}\n```",
                    },
                },
            ],
        };
    }

    private static GetPromptResult BuildExplainPrompt(Dictionary<string, string> args)
    {
        args.TryGetValue("topic", out var topic);
        return new GetPromptResult
        {
            Description = "Explanation prompt",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock
                    {
                        Text = $"Please explain the following clearly and concisely:\n\n{topic ?? "(no topic provided)"}",
                    },
                },
            ],
        };
    }

    private static GetPromptResult BuildSummarizeSessionPrompt(Dictionary<string, string> args)
    {
        args.TryGetValue("session_id", out var sessionId);
        return new GetPromptResult
        {
            Description = "Session summary prompt",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock
                    {
                        Text = $"Please read the session history for session '{sessionId ?? "unknown"}' " +
                               "using the sovrant://sessions/{session_id} resource and provide a concise summary " +
                               "of what was discussed and any decisions made.",
                    },
                },
            ],
        };
    }

    private static GetPromptResult BuildRefactorPrompt(Dictionary<string, string> args)
    {
        args.TryGetValue("code", out var code);
        args.TryGetValue("goal", out var goal);
        var goalHint = string.IsNullOrEmpty(goal) ? "" : $" with a focus on {goal}";

        return new GetPromptResult
        {
            Description = "Refactoring prompt",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock
                    {
                        Text = $"Please suggest refactoring improvements{goalHint} for the following code:\n\n```\n{code ?? "(no code provided)"}\n```",
                    },
                },
            ],
        };
    }

    // ── Completions ──────────────────────────────────────────────────────────

    private static async Task<CompleteResult> CompleteAsync(
        IServiceProvider services,
        CompleteRequestParams requestParams,
        CancellationToken ct)
    {
        var argument = requestParams.Argument;
        var argName = argument.Name;
        var argValue = argument.Value ?? string.Empty;

        // Resource URI completions — suggest session IDs.
        if (requestParams.Ref is ResourceTemplateReference)
        {
            var store = services.GetService<ISessionStore>();
            if (store is not null)
            {
                var ownerUserId = GetOwnerUserId();
                var sessions = await store.ListAsync(ownerUserId, ct).ConfigureAwait(false);
                var matching = sessions
                    .Where(s => s.StartsWith(argValue, StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToList();
                return new CompleteResult
                {
                    Completion = new Completion { Values = matching, HasMore = sessions.Count > 20 },
                };
            }
        }

        // Prompt argument completions.
        if (string.Equals(argName, "language", StringComparison.OrdinalIgnoreCase))
        {
            var languages = new[] { "csharp", "python", "javascript", "typescript", "go", "rust", "java", "cpp", "sql", "bash" };
            var matching = languages
                .Where(l => l.StartsWith(argValue, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return new CompleteResult
            {
                Completion = new Completion { Values = matching },
            };
        }

        if (string.Equals(argName, "goal", StringComparison.OrdinalIgnoreCase))
        {
            var goals = new[] { "readability", "performance", "maintainability", "testability", "simplicity", "DRY", "SOLID" };
            var matching = goals
                .Where(g => g.StartsWith(argValue, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return new CompleteResult
            {
                Completion = new Completion { Values = matching },
            };
        }

        // No completions available.
        return new CompleteResult
        {
            Completion = new Completion { Values = [] },
        };
    }

    // ── Logging ──────────────────────────────────────────────────────────────

    private static EmptyResult SetLoggingLevel(
        IServiceProvider services,
        SetLevelRequestParams requestParams)
    {
        var level = requestParams.Level;
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("Sovrant.McpServer");
        if (logger is not null)
            s_logLoggingLevel(logger, level.ToString(), null);

        // The SDK's McpServer automatically updates its LoggingLevel property;
        // we just log the change for observability.
        return new EmptyResult();
    }

    // ── Resource Subscriptions ───────────────────────────────────────────────

    private static EmptyResult SubscribeToResource(SubscribeRequestParams requestParams)
    {
        var uri = requestParams.Uri ?? string.Empty;
        s_resourceSubscriptions.AddOrUpdate(uri, 1, (_, count) => count + 1);
        return new EmptyResult();
    }

    private static EmptyResult UnsubscribeFromResource(UnsubscribeRequestParams requestParams)
    {
        var uri = requestParams.Uri ?? string.Empty;
        s_resourceSubscriptions.AddOrUpdate(uri, 0, (_, count) => Math.Max(0, count - 1));
        return new EmptyResult();
    }

    /// <summary>Returns the current subscription count for a resource URI (for testing).</summary>
    internal static int GetSubscriptionCount(string uri) =>
        s_resourceSubscriptions.TryGetValue(uri, out var count) ? count : 0;

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static JsonElement ConvertArguments(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return JsonDocument.Parse("{}").RootElement.Clone();

        var json = JsonSerializer.Serialize(arguments, s_jsonOptions);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static CallToolResult ErrorResult(Exception ex)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = ex.Message }],
        };
    }
}
