namespace Sovrant.Runtime.Caching;

/// <summary>Standard TTL values for cacheable resources.</summary>
public static class CacheTtl
{
    /// <summary>Tool registry — changes only on server restart.</summary>
    public static readonly TimeSpan Tools = TimeSpan.FromHours(1);

    /// <summary>Skill registry — rarely changes in practice.</summary>
    public static readonly TimeSpan Skills = TimeSpan.FromHours(1);

    /// <summary>Agent templates — rarely changes in practice.</summary>
    public static readonly TimeSpan Templates = TimeSpan.FromHours(1);

    /// <summary>Server config — cached until mutation.</summary>
    public static readonly TimeSpan Config = TimeSpan.FromMinutes(10);

    /// <summary>Provider health/status — short-lived, refreshes frequently.</summary>
    public static readonly TimeSpan Status = TimeSpan.FromSeconds(10);

    // Knowledge store TTLs — content changes at human cadence (edits, not per-request).
    public static readonly TimeSpan KnowledgeSkills           = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan KnowledgeAgents           = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan KnowledgeDocumentTemplates = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan KnowledgeTools            = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan KnowledgeDocuments        = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan KnowledgeGuidelines       = TimeSpan.FromSeconds(30);

    public static TimeSpan ForKnowledgeKind(string kind) => kind switch
    {
        "skills"             => KnowledgeSkills,
        "agents"             => KnowledgeAgents,
        "document-templates" => KnowledgeDocumentTemplates,
        "tools"              => KnowledgeTools,
        "documents"          => KnowledgeDocuments,
        "guidelines"         => KnowledgeGuidelines,
        _                    => KnowledgeSkills,
    };
}
