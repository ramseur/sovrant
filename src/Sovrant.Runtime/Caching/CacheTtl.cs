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
}
