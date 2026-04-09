using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sovrant.Runtime.Config;
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

    /// <summary>
    /// Registers the MCP server with stdio transport, bridging all <see cref="IToolRegistry"/>
    /// tools and session/config resources to the MCP protocol.
    /// </summary>
    public static IServiceCollection AddSovrantMcpServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation { Name = "sovrant", Version = "1.0.0" };
        })
        .WithStdioServerTransport()
        .WithListToolsHandler((context, ct) => new ValueTask<ListToolsResult>(ListToolsAsync(context.Services!, ct)))
        .WithCallToolHandler(async (context, ct) => await CallToolAsync(context.Services!, context.Params!, ct).ConfigureAwait(false))
        .WithListResourcesHandler((_, _) => new ValueTask<ListResourcesResult>(ListResourcesSync()))
        .WithReadResourceHandler(async (context, ct) => await ReadResourceAsync(context.Services!, context.Params!, ct).ConfigureAwait(false));

        return services;
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

        try
        {
            var input = ConvertArguments(arguments);
            var result = await handler(input, ct).ConfigureAwait(false);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = result }],
            };
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResult(ex);
        }
        catch (IOException ex)
        {
            return ErrorResult(ex);
        }
        catch (JsonException ex)
        {
            return ErrorResult(ex);
        }
    }

    // ── Resources ────────────────────────────────────────────────────────────

    private static ListResourcesResult ListResourcesSync()
    {
        var resources = new List<Resource>
        {
            new()
            {
                Uri = "sovrant://sessions",
                Name = "Session list",
                Description = "All session IDs that have at least one entry.",
                MimeType = "application/json",
            },
            new()
            {
                Uri = "sovrant://config",
                Name = "Server configuration",
                Description = "Current Sovrant runtime configuration.",
                MimeType = "application/json",
            },
        };

        return new ListResourcesResult { Resources = resources };
    }

    private static async Task<ReadResourceResult> ReadResourceAsync(
        IServiceProvider services,
        ReadResourceRequestParams requestParams,
        CancellationToken ct)
    {
        var uri = requestParams.Uri ?? string.Empty;

        if (string.Equals(uri, "sovrant://sessions", StringComparison.Ordinal))
        {
            var store = services.GetRequiredService<ISessionStore>();
            var sessions = await store.ListAsync(ownerUserId: null, ct).ConfigureAwait(false);
            var json = JsonSerializer.Serialize(sessions, s_jsonOptions);
            return new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = uri, MimeType = "application/json", Text = json }],
            };
        }

        if (string.Equals(uri, "sovrant://config", StringComparison.Ordinal))
        {
            var config = services.GetRequiredService<SovrantConfig>();
            var json = JsonSerializer.Serialize(config, s_jsonOptions);
            return new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = uri, MimeType = "application/json", Text = json }],
            };
        }

        // sovrant://sessions/{id}
        const string sessionsPrefix = "sovrant://sessions/";
        if (uri.StartsWith(sessionsPrefix, StringComparison.Ordinal))
        {
            var sessionId = Uri.UnescapeDataString(uri[sessionsPrefix.Length..]);
            var store = services.GetRequiredService<ISessionStore>();
            var entries = await store.LoadAsync(sessionId, ownerUserId: null, ct).ConfigureAwait(false);
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
