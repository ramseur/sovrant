namespace Sovrant.Runtime.Mcp;

public enum IntegrationKind { McpHttp, McpStdio, LlmProvider }
public enum IntegrationTier { Automation, Platform, LlmProvider }

public sealed record CatalogEntry(
    string Id,
    string Name,
    string Category,
    string Icon,
    string Description,
    IntegrationKind Kind,
    IntegrationTier Tier,
    string? ApiKeyLabel = null,
    string? ApiKeyEnvVar = null,
    string? ApiKeyHeader = null,
    string? EndpointLabel = null,
    string? DefaultCommand = null,
    IReadOnlyList<string>? DefaultArgs = null,
    string? EndpointTemplate = null,
    string? SettingsSection = null)
{
    public bool NeedsApiKey => ApiKeyLabel is not null;
    public bool NeedsEndpoint => EndpointLabel is not null;
}

public static class IntegrationCatalog
{
    public static IReadOnlyList<CatalogEntry> All { get; } =
    [
        // Automation (HTTP MCP)
        new("composio", "Composio", "Automation", "🔗",
            "Connect 250+ apps — GitHub, Slack, Gmail, and more — as native agent tools.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            ApiKeyLabel: "API Key", ApiKeyHeader: "Authorization",
            EndpointTemplate: "https://mcp.composio.dev/composio/{API_KEY}"),

        new("n8n", "n8n", "Automation", "⚙️",
            "Expose your n8n workflows as MCP tools — agents call them as first-class actions.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            EndpointLabel: "n8n MCP endpoint",
            ApiKeyLabel: "API Key (optional)", ApiKeyHeader: "X-N8N-API-Key"),

        new("zapier", "Zapier", "Automation", "⚡",
            "Run any Zapier action from an agent via the Zapier MCP bridge.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            ApiKeyLabel: "API Key", ApiKeyHeader: "Authorization",
            EndpointTemplate: "https://actions.zapier.com/mcp/{API_KEY}/sse"),

        new("make", "Make", "Automation", "🔄",
            "Trigger Make (Integromat) scenarios as agent tools via MCP.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            EndpointLabel: "Make MCP endpoint",
            ApiKeyLabel: "API Key (optional)", ApiKeyHeader: "Authorization"),

        // Platform (stdio MCP)
        new("github", "GitHub", "Platform", "🐙",
            "Search repos, read files, manage issues and PRs via the official GitHub MCP server.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Personal Access Token", ApiKeyEnvVar: "GITHUB_TOKEN",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-github"]),

        new("filesystem", "Filesystem", "Platform", "📁",
            "Give agents read/write access to a local directory via MCP.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            EndpointLabel: "Directory path (e.g. /home/user/projects)",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-filesystem", "{ENDPOINT}"]),

        new("brave-search", "Brave Search", "Platform", "🔍",
            "Web and local search powered by the Brave Search API.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Brave API Key", ApiKeyEnvVar: "BRAVE_API_KEY",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-brave-search"]),

        new("postgres", "PostgreSQL", "Platform", "🐘",
            "Query and manage a PostgreSQL database directly from agents.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            EndpointLabel: "Connection string (postgresql://user:pass@host/db)",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-postgres", "{ENDPOINT}"]),

        // LLM Providers (navigate to Settings)
        new("openai", "OpenAI", "LLM Provider", "🤖",
            "GPT-4o, o3, and other OpenAI models.",
            IntegrationKind.LlmProvider, IntegrationTier.LlmProvider,
            SettingsSection: "Models"),

        new("anthropic", "Anthropic", "LLM Provider", "🧠",
            "Claude Opus, Sonnet, and Haiku.",
            IntegrationKind.LlmProvider, IntegrationTier.LlmProvider,
            SettingsSection: "Models"),

        new("openrouter", "OpenRouter", "LLM Provider", "🔀",
            "Route to 100+ models from one API key.",
            IntegrationKind.LlmProvider, IntegrationTier.LlmProvider,
            SettingsSection: "Models"),

        new("ollama", "Ollama", "LLM Provider", "🦙",
            "Run local models (Llama, Mistral, Gemma…) via Ollama.",
            IntegrationKind.LlmProvider, IntegrationTier.LlmProvider,
            SettingsSection: "Models"),
    ];
}
