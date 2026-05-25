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
    string? TabLabel = null)
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
            ApiKeyLabel: "API Key", ApiKeyHeader: "X-N8N-API-Key"),

        new("zapier", "Zapier", "Automation", "🔌",
            "Run any Zapier action from an agent via the Zapier MCP bridge.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            ApiKeyLabel: "API Key", ApiKeyHeader: "Authorization",
            EndpointTemplate: "https://actions.zapier.com/mcp/{API_KEY}/sse"),

        new("make", "Make", "Automation", "🔄",
            "Trigger Make (Integromat) scenarios as agent tools via MCP.",
            IntegrationKind.McpHttp, IntegrationTier.Automation,
            EndpointLabel: "Make MCP endpoint",
            ApiKeyLabel: "API Key", ApiKeyHeader: "Authorization"),

        // Platform (stdio MCP)
        new("github", "GitHub", "Platform", "🐙",
            "Search repos, read files, manage issues and PRs via the official GitHub MCP server.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Personal Access Token", ApiKeyEnvVar: "GITHUB_TOKEN",
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
            "Create, update, and query Linear issues and projects from agents.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "API Key", ApiKeyEnvVar: "LINEAR_API_KEY",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@linear/mcp-server"]),

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
            "Query, manage, and deploy Supabase projects directly from agents.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Access Token",
            DefaultCommand: "npx", DefaultArgs: ["-y", "@supabase/mcp-server-supabase", "--access-token", "{API_KEY}"]),

        new("snowflake", "Snowflake", "Platform", "❄️",
            "Run SQL queries and search Snowflake data warehouses directly from agents.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Password", ApiKeyEnvVar: "SNOWFLAKE_PASSWORD",
            EndpointLabel: "Account identifier (e.g. myorg-myaccount)",
            DefaultCommand: "npx", DefaultArgs: ["-y", "snowflake-mcp-server"]),

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
            GroupName: "Sitecore", TabLabel: "Commercial"),

        new("aem", "Adobe Experience Manager", "DXP", "🔴",
            "Read and write AEM content, pages, assets, and Cloud Manager resources via the official Adobe MCP server.",
            IntegrationKind.McpHttp, IntegrationTier.Platform,
            ApiKeyLabel: "Adobe OAuth Token", ApiKeyHeader: "Authorization",
            EndpointLabel: "AEM MCP endpoint (e.g. https://mcp.adobeaemcloud.com/adobe/mcp/content)"),

        new("optimizely", "Optimizely CMS", "DXP", "🔵",
            "Query and manage Optimizely CMS content via the Graph API and Content Management API.",
            IntegrationKind.McpStdio, IntegrationTier.Platform,
            ApiKeyLabel: "Graph Single Key", ApiKeyEnvVar: "GRAPH_SINGLE_KEY",
            EndpointLabel: "CMA base URL (e.g. https://api.cms.optimizely.com/preview3)",
            DefaultCommand: "npx", DefaultArgs: ["-y", "optimizely-cms-mcp"]),


    ];
}
