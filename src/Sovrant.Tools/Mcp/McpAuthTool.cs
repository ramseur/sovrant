using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Tools.Mcp;

/// <summary>
/// Initiates an OAuth 2.0 Authorization Code + PKCE flow for a named MCP server.
/// Returns an authorization URL that the user must visit to grant access.
/// After the user completes the OAuth flow, Sovrant automatically reconnects the
/// MCP server with the acquired token.
/// </summary>
public sealed class McpAuthTool : ITool
{
    private static readonly ToolDefinition s_definition = new("McpAuth", CreateSchema())
    {
        Description =
            "Initiates OAuth authentication for a named MCP server that requires authorization. " +
            "Returns a URL the user must visit to grant access. " +
            "Use this when an MCP tool call fails with an authentication error, or when the user " +
            "asks to connect/authorize an MCP server.",
    };

    private readonly McpOAuthService _oauthService;

    public McpAuthTool(McpOAuthService oauthService) => _oauthService = oauthService;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var serverName = input.TryGetProperty("server", out var sv)
            ? sv.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(serverName))
            return "The 'server' parameter is required. Provide the name of the MCP server to authorize.";

        try
        {
            var url = await _oauthService.GenerateAuthorizationUrlAsync(serverName, ct)
                .ConfigureAwait(false);

            return $"""
                Please visit the following URL to authorize access to the '{serverName}' MCP server:

                {url}

                After you complete the authorization, the connection will be established automatically.
                You do not need to do anything else — just open the URL and approve access.
                """;
        }
        catch (KeyNotFoundException)
        {
            return $"MCP server '{serverName}' is not configured. " +
                   "Check that the server name matches an entry in your Sovrant MCP server configuration.";
        }
        catch (InvalidOperationException ex)
        {
            return $"Cannot initiate OAuth for '{serverName}': {ex.Message}";
        }
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "server": {
                    "type": "string",
                    "description": "Name of the MCP server to authorize (must match a key in the MCP server configuration)."
                }
            },
            "required": ["server"]
        }
        """).RootElement;
}
