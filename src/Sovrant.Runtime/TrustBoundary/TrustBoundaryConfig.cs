namespace Sovrant.Runtime.TrustBoundary;

/// <summary>Root configuration for the Sovrant Trust Boundary.</summary>
public sealed class TrustBoundaryConfig
{
    /// <summary>Master switch for the entire trust boundary.</summary>
    public bool Enabled { get; init; }

    /// <summary>Data sanitizer configuration.</summary>
    public SanitizerConfig Sanitizer { get; init; } = new();

    /// <summary>Ethical harness configuration.</summary>
    public EthicalPolicy EthicalHarness { get; init; } = new();

    /// <summary>Intent verification configuration.</summary>
    public IntentVerificationConfig IntentVerification { get; init; } = new();
}

/// <summary>Configuration for the data sanitizer stage.</summary>
public sealed class SanitizerConfig
{
    /// <summary>Whether the sanitizer is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Redaction mode: "redact" replaces with placeholders, "warn" logs but sends, "block" refuses.</summary>
    public string Mode { get; init; } = "redact";

    /// <summary>Internal domain suffixes to treat as corporate data.</summary>
    public IReadOnlyList<string> CorporateDomains { get; init; } = [];

    /// <summary>User-defined sensitive patterns.</summary>
    public IReadOnlyList<CustomPattern> CustomPatterns { get; init; } = [];

    /// <summary>Domains/patterns that should never be redacted.</summary>
    public IReadOnlyList<string> AllowList { get; init; } = [];

    /// <summary>Provider names to skip sanitization for (e.g. "ollama" — data stays local).</summary>
    public IReadOnlyList<string> ExemptProviders { get; init; } = [];

    /// <summary>Whether to log what was redacted (for audit).</summary>
    public bool LogRedactions { get; init; }
}

/// <summary>Configuration for the intent verification stage.</summary>
public sealed class IntentVerificationConfig
{
    /// <summary>Whether intent verification is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether to clarify ambiguous input before sending.</summary>
    public bool ClarifyAmbiguous { get; init; } = true;

    /// <summary>Whether to block clearly harmful intent at this layer.</summary>
    public bool BlockHarmfulIntent { get; init; } = true;
}
