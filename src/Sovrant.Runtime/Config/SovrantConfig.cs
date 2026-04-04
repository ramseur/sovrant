using Sovrant.Api.Routing;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Config;

/// <summary>Root configuration for the Sovrant runtime.</summary>
public sealed class SovrantConfig
{
    /// <summary>The model identifier to use for LLM requests.</summary>
    public string Model { get; init; } = "gpt-4o-mini";

    /// <summary>The maximum number of output tokens per request.</summary>
    public int MaxTokens { get; init; } = 8192;

    /// <summary>Controls how the runtime handles potentially destructive tool invocations.</summary>
    public PermissionMode PermissionMode { get; init; } = PermissionMode.Default;

    /// <summary>Controls whether the router uses smart multi-provider routing or a fixed provider.</summary>
    public RouterMode RouterMode { get; init; } = RouterMode.Smart;

    /// <summary>The scoring strategy used when routing to the optimal provider.</summary>
    public RouterStrategy RouterStrategy { get; init; } = RouterStrategy.Balanced;

    /// <summary>Optional base URL override for the LLM API.</summary>
    public Uri? BaseUrl { get; init; }

    /// <summary>Optional API key override. Defaults to the <c>LLM_API_KEY</c> environment variable.</summary>
    public string? ApiKey { get; init; }

    /// <summary>MCP server configurations keyed by server name.</summary>
    public IReadOnlyDictionary<string, McpServerConfig> McpServers { get; init; } =
        new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);
}

/// <summary>Configuration for a single MCP server.</summary>
public sealed class McpServerConfig
{
    /// <summary>The command to launch the MCP server process.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Command-line arguments for the MCP server process.</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>Additional environment variables to set for the MCP server process.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
