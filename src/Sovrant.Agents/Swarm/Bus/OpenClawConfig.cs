using System.Text.Json.Serialization;

namespace Sovrant.Agents.Swarm.Bus;

/// <summary>Connection settings for the OpenClaw MCP gateway.</summary>
public sealed class OpenClawConfig
{
    /// <summary>MCP server name registered in the session (matches the key in mcp_connections).</summary>
    [JsonPropertyName("server_name")]
    public string ServerName { get; init; } = "openclaw";

    /// <summary>Route namespace prefix used when publishing swarm events.</summary>
    [JsonPropertyName("route_prefix")]
    public string RoutePrefix { get; init; } = "sovrant.swarm";

    /// <summary>
    /// Maximum seconds to wait for an approval response before timing out.
    /// Default 120 s (2 minutes).
    /// </summary>
    [JsonPropertyName("approval_timeout_seconds")]
    public int ApprovalTimeoutSeconds { get; init; } = 120;
}

/// <summary>Federation behaviour config nested inside <see cref="SwarmConfig"/>.</summary>
public sealed class SwarmFederationConfig
{
    /// <summary>How this swarm participates in the federation bus. Default is <see cref="SwarmFederationMode.Silo"/>.</summary>
    [JsonPropertyName("mode")]
    public SwarmFederationMode Mode { get; init; } = SwarmFederationMode.Silo;

    /// <summary>
    /// ID of the parent swarm when <see cref="Mode"/> is <see cref="SwarmFederationMode.ManagerLed"/>.
    /// Null for top-level swarms.
    /// </summary>
    [JsonPropertyName("parent_swarm_id")]
    public string? ParentSwarmId { get; init; }

    /// <summary>OpenClaw connection config. Null → use defaults.</summary>
    [JsonPropertyName("openclaw")]
    public OpenClawConfig? OpenClaw { get; init; }
}
