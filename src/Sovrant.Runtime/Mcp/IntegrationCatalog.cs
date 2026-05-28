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
    string? SettingsSection = null,
    string? GroupName = null,
    string? TabLabel = null,
    bool RequiresOAuth = false,
    Uri? OAuthAuthorizationUrl = null,
    Uri? OAuthTokenUrl = null,
    IReadOnlyList<string>? OAuthScopes = null)
{
    public bool NeedsApiKey => ApiKeyLabel is not null;
    public bool NeedsEndpoint => EndpointLabel is not null;
    public bool HasOAuthMetadata => OAuthAuthorizationUrl is not null && OAuthTokenUrl is not null;
}

public static class IntegrationCatalog
{
    public static IReadOnlyList<CatalogEntry> All { get; } =
    [
        // Automation (HTTP MCP)
        new("composio", "Composio", "Automation", "🔗",
            "Connect 250+ apps — GitHub, Slack, Gmail, and more — as native agent tools.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            ApiKeyLabel: "API Key", ApiKeyHeader: "x-api-key",
            EndpointTemplate: "https://mcp.composio.dev/composio/{API_KEY}"),

        new("n8n", "n8n", "Automation", "⚙️",
            "Expose your n8n workflows as MCP tools — agents call them as first-class actions.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            EndpointLabel: "n8n MCP endpoint",
            ApiKeyLabel: "API Key", ApiKeyHeader: "X-N8N-API-Key"),

        new("zapier", "Zapier", "Automation", "🔌",
            "Run any Zapier action from an agent via the Zapier MCP bridge. Browser-based OAuth is the recommended auth flow (Phase 101); enter your MCP endpoint URL from the Zapier dashboard.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            EndpointLabel: "Zapier MCP endpoint (from Zapier dashboard)",
            ApiKeyLabel: "API Key (optional)", ApiKeyHeader: "Authorization",
            RequiresOAuth: true),

        new("make", "Make", "Automation", "🔄",
            "Trigger Make (Integromat) scenarios as agent tools via MCP.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            EndpointLabel: "Make MCP endpoint",
            ApiKeyLabel: "API Key", ApiKeyHeader: "Authorization"),

        // Platform (stdio MCP)
        new("github", "GitHub", "Platform", "🐙",
            "Search repos, read files, manage issues and PRs via the official GitHub MCP server. Uses the legacy npx package; GitHub's canonical server is now Docker-based (ghcr.io/github/github-mcp-server).",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Personal Access Token", ApiKeyEnvVar: "GITHUB_PERSONAL_ACCESS_TOKEN",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-github"]),

        new("slack", "Slack", "Platform", "💬",
            "Read channels, send messages, and search Slack from agents.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Bot Token", ApiKeyEnvVar: "SLACK_BOT_TOKEN",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-slack"]),

        new("notion", "Notion", "Platform", "📝",
            "Search, read, and update Notion pages and databases from agents.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Integration Token", ApiKeyEnvVar: "NOTION_API_KEY",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@notionhq/notion-mcp-server"]),

        new("linear", "Linear", "Platform", "📐",
            "Create, update, and query Linear issues and projects from agents. Uses Linear's official remote MCP — browser OAuth required on first connect.",
            IntegrationKind.McpHttp, IntegrationTier.Platform,
            EndpointLabel: "Linear MCP endpoint",
            EndpointTemplate: "https://mcp.linear.app/mcp",
            RequiresOAuth: true,
            OAuthAuthorizationUrl: new Uri("https://linear.app/oauth/authorize"),
            OAuthTokenUrl: new Uri("https://api.linear.app/oauth/token"),
            OAuthScopes: ["read", "write"]),

        new("stripe", "Stripe", "Platform", "💳",
            "Query customers, payments, and subscriptions via the Stripe MCP server.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Secret Key", ApiKeyEnvVar: "STRIPE_SECRET_KEY",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@stripe/mcp", "--tools=all"]),

        new("postgres", "PostgreSQL", "Platform", "🐘",
            "Query and manage a PostgreSQL database directly from agents.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            EndpointLabel: "Connection string (postgresql://user:pass@host/db)",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-postgres", "{ENDPOINT}"]),

        new("supabase", "Supabase", "Platform", "🗄️",
            "Query, manage, and deploy Supabase projects directly from agents. Use a personal access token for CLI/headless access; browser OAuth is the default interactive flow.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Access Token (for headless use)",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@supabase/mcp-server-supabase", "--access-token", "{API_KEY}"]),

        new("snowflake", "Snowflake", "Platform", "❄️",
            "Run SQL queries and search Snowflake data warehouses directly from agents. Requires SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_ACCOUNT, SNOWFLAKE_WAREHOUSE, SNOWFLAKE_DATABASE, and SNOWFLAKE_SCHEMA environment variables.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Password", ApiKeyEnvVar: "SNOWFLAKE_PASSWORD",
            EndpointLabel: "Account identifier (e.g. myorg-myaccount) → sets SNOWFLAKE_ACCOUNT",
            DefaultCommand: "npx", DefaultArgs: ["-y", "snowflake-mcp"]),

        // Search
        new("brave-search", "Brave Search", "Search", "🔍",
            "Web and local search powered by the Brave Search API.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "API Key", ApiKeyEnvVar: "BRAVE_API_KEY",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@modelcontextprotocol/server-brave-search"]),

        new("exa", "Exa", "Search", "🔎",
            "AI-powered web search and content retrieval for agent research tasks.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "API Key", ApiKeyEnvVar: "EXA_API_KEY",
            DefaultCommand: "npx", DefaultArgs: ["-y", "exa-mcp-server"]),

        new("tavily", "Tavily", "Search", "🌐",
            "Real-time web search optimised for AI agents and RAG pipelines.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "API Key", ApiKeyEnvVar: "TAVILY_API_KEY",
            DefaultCommand: "npx", DefaultArgs: ["-y", "tavily-mcp"]),

        // DXP / CMS / Data
        new("sitecore-community", "Sitecore", "DXP", "🟩",
            "Community MCP server by Anton Tishchenko. Exposes Sitecore content, items, and layout via GraphQL + ItemService. Install via npx.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Authorization Header (optional)", ApiKeyEnvVar: "AUTORIZATION_HEADER",
            EndpointLabel: "GraphQL endpoint (e.g. https://your-site/api/graph/edge)",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@antonytm/mcp-sitecore-server"],
            GroupName: "Sitecore", TabLabel: "Community"),

        new("sitecore-marketer", "Sitecore", "DXP", "🟩",
            "Official Sitecore.AI Marketer MCP. Remote HTTPS endpoint — browser-based OAuth 2.0 via Sitecore Identity. No API key needed; auth is handled interactively on first connect.",
            IntegrationKind.McpHttp, IntegrationTier.Platform,
            EndpointLabel: "Marketer MCP endpoint (from Sitecore Cloud Portal)",
            GroupName: "Sitecore", TabLabel: "Commercial",
            RequiresOAuth: true),

        new("aem", "Adobe Experience Manager", "DXP", "🔴",
            "Read and write AEM content, pages, assets, and Cloud Manager resources via the official Adobe MCP server.",
            IntegrationKind.McpHttp, IntegrationTier.Platform,
            EndpointLabel: "AEM MCP endpoint (e.g. https://mcp.adobeaemcloud.com/adobe/mcp/content)",
            RequiresOAuth: true,
            OAuthAuthorizationUrl: new Uri("https://ims-na1.adobelogin.com/ims/authorize/v2"),
            OAuthTokenUrl: new Uri("https://ims-na1.adobelogin.com/ims/token/v3"),
            OAuthScopes: ["openid", "AdobeID", "read_organizations"]),

        new("optimizely-experimentation", "Optimizely", "DXP", "🔵",
            "Official Optimizely Experimentation MCP. Remote HTTPS endpoint — browser-based OAuth via Optimizely Identity. Query, create, and update experiments, flags, and audiences.",
            IntegrationKind.McpHttp, IntegrationTier.Platform,
            EndpointTemplate: "https://exp.mcp.opal.optimizely.com/mcp",
            GroupName: "Optimizely", TabLabel: "Experimentation",
            RequiresOAuth: true,
            OAuthAuthorizationUrl: new Uri("https://app.optimizely.com/oauth2/authorize"),
            OAuthTokenUrl: new Uri("https://app.optimizely.com/oauth2/token"),
            OAuthScopes: ["project.readonly"]),

        new("optimizely-cms", "Optimizely", "DXP", "🔵",
            "Community MCP server for Optimizely CMS (Graph API + Content Management API). Requires manual install from GitHub (github.com/first3things/optimizely-cms-mcp) — not available via npx. Additional env vars needed: GRAPH_ENDPOINT, CMA_BASE_URL, CMA_TOKEN_ENDPOINT, CMA_CLIENT_ID, CMA_CLIENT_SECRET.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Graph Single Key", ApiKeyEnvVar: "GRAPH_SINGLE_KEY",
            EndpointLabel: "CMA base URL (e.g. https://api.cms.optimizely.com/preview3)",
            GroupName: "Optimizely", TabLabel: "CMS"),

    ];
}
