namespace Sovrant.Runtime.TrustBoundary;

/// <summary>Severity level for flagged content.</summary>
public enum EthicalSeverity
{
    /// <summary>Informational — logged but not surfaced to user.</summary>
    Info,
    /// <summary>Warning — surfaced to user but content is allowed.</summary>
    Warning,
    /// <summary>Critical — content is blocked.</summary>
    Critical,
}

/// <summary>Result of an ethical harness evaluation.</summary>
public abstract record EthicalVerdict
{
    private EthicalVerdict() { }

    /// <summary>Content is allowed to proceed.</summary>
    public sealed record Allow() : EthicalVerdict;

    /// <summary>Content is blocked and must not reach the provider or user.</summary>
    public sealed record Block(string Reason, string Category) : EthicalVerdict;

    /// <summary>Content is flagged but allowed — logged and optionally warned.</summary>
    public sealed record Flag(string Reason, string Category, EthicalSeverity Severity) : EthicalVerdict;
}
