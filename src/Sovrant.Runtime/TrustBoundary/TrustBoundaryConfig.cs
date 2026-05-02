using System.Text.Json;
using System.Text.RegularExpressions;
using Sovrant.Runtime.Workspaces;

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

    /// <summary>
    /// Builds a fresh config layered as env &gt; <see cref="IWorkspaceSettingsStore"/> &gt;
    /// <paramref name="snapshot"/> &gt; defaults. <paramref name="snapshot"/> is the
    /// <c>settings.json</c>-derived <see cref="TrustBoundaryConfig"/> from
    /// <see cref="Config.SovrantConfig.TrustBoundary"/>; pass a fresh instance for the
    /// fresh-install case. The <see cref="EthicalHarness"/> branch is currently
    /// snapshot-only (no DB keys are defined yet).
    /// </summary>
    public static TrustBoundaryConfig Resolve(
        TrustBoundaryConfig snapshot, IWorkspaceSettingsStore? settings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var enabled = WorkspaceSettingsResolver.ResolveBool(
            settings, WorkspaceSettingsKeys.TrustBoundaryEnabled,
            "SOVRANT_TRUSTBOUNDARY_ENABLED", fallback: snapshot.Enabled);

        var sanitizer = ResolveSanitizer(snapshot.Sanitizer, settings);
        var intent = ResolveIntent(snapshot.IntentVerification, settings);

        return new TrustBoundaryConfig
        {
            Enabled = enabled,
            Sanitizer = sanitizer,
            EthicalHarness = snapshot.EthicalHarness,
            IntentVerification = intent,
        };
    }

    /// <summary>
    /// Persists trust-boundary fields to the global <see cref="IWorkspaceSettingsStore"/> row.
    /// Lists are JSON-encoded. Validation runs first; on failure, nothing is written.
    /// </summary>
    public async Task SaveToStoreAsync(IWorkspaceSettingsStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        Validate();

        var ws = WorkspaceSettingsKeys.GlobalWorkspaceId;

        await store.SetAsync(ws, WorkspaceSettingsKeys.TrustBoundaryEnabled,
            Enabled ? "true" : "false", ct).ConfigureAwait(false);

        await store.SetAsync(ws, WorkspaceSettingsKeys.SanitizerEnabled,
            Sanitizer.Enabled ? "true" : "false", ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.SanitizerMode,
            Sanitizer.Mode, ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.SanitizerCorporateDomains,
            JsonSerializer.Serialize(Sanitizer.CorporateDomains), ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.SanitizerCustomPatterns,
            JsonSerializer.Serialize(Sanitizer.CustomPatterns), ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.SanitizerAllowList,
            JsonSerializer.Serialize(Sanitizer.AllowList), ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.SanitizerExemptProviders,
            JsonSerializer.Serialize(Sanitizer.ExemptProviders), ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.SanitizerLogRedactions,
            Sanitizer.LogRedactions ? "true" : "false", ct).ConfigureAwait(false);

        await store.SetAsync(ws, WorkspaceSettingsKeys.IntentVerificationEnabled,
            IntentVerification.Enabled ? "true" : "false", ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.IntentClarifyAmbiguous,
            IntentVerification.ClarifyAmbiguous ? "true" : "false", ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.IntentBlockHarmful,
            IntentVerification.BlockHarmfulIntent ? "true" : "false", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if the config has fields that the
    /// runtime cannot honour: unrecognised sanitizer mode or non-compiling custom
    /// regexes. Settings UIs call this on save so a bad value never reaches the DB.
    /// </summary>
    public void Validate()
    {
        if (!IsValidSanitizerMode(Sanitizer.Mode))
            throw new ArgumentException(
                $"Sanitizer.Mode must be 'redact', 'warn', or 'block'; got '{Sanitizer.Mode}'.",
                nameof(Sanitizer));

        foreach (var pattern in Sanitizer.CustomPatterns)
        {
            try
            {
                _ = new Regex(pattern.RegexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(
                    $"Custom pattern '{pattern.Name}' has an invalid regex: {ex.Message}",
                    nameof(Sanitizer), ex);
            }
        }
    }

    private static bool IsValidSanitizerMode(string mode) =>
        mode is "redact" or "warn" or "block";

    private static SanitizerConfig ResolveSanitizer(SanitizerConfig snapshot, IWorkspaceSettingsStore? settings)
    {
        var customPatterns = ResolveCustomPatterns(
            settings, WorkspaceSettingsKeys.SanitizerCustomPatterns,
            "SOVRANT_TRUSTBOUNDARY_CUSTOM_PATTERNS", snapshot.CustomPatterns);

        return new SanitizerConfig
        {
            Enabled = WorkspaceSettingsResolver.ResolveBool(
                settings, WorkspaceSettingsKeys.SanitizerEnabled,
                "SOVRANT_TRUSTBOUNDARY_SANITIZER_ENABLED", fallback: snapshot.Enabled),
            Mode = WorkspaceSettingsResolver.ResolveString(
                settings, WorkspaceSettingsKeys.SanitizerMode,
                "SOVRANT_TRUSTBOUNDARY_SANITIZER_MODE", fallback: snapshot.Mode)
                ?? snapshot.Mode,
            CorporateDomains = WorkspaceSettingsResolver.ResolveStringList(
                settings, WorkspaceSettingsKeys.SanitizerCorporateDomains,
                "SOVRANT_TRUSTBOUNDARY_CORPORATE_DOMAINS", fallback: snapshot.CorporateDomains),
            CustomPatterns = customPatterns,
            AllowList = WorkspaceSettingsResolver.ResolveStringList(
                settings, WorkspaceSettingsKeys.SanitizerAllowList,
                "SOVRANT_TRUSTBOUNDARY_ALLOW_LIST", fallback: snapshot.AllowList),
            ExemptProviders = WorkspaceSettingsResolver.ResolveStringList(
                settings, WorkspaceSettingsKeys.SanitizerExemptProviders,
                "SOVRANT_TRUSTBOUNDARY_EXEMPT_PROVIDERS", fallback: snapshot.ExemptProviders),
            LogRedactions = WorkspaceSettingsResolver.ResolveBool(
                settings, WorkspaceSettingsKeys.SanitizerLogRedactions,
                "SOVRANT_TRUSTBOUNDARY_LOG_REDACTIONS", fallback: snapshot.LogRedactions),
        };
    }

    private static IntentVerificationConfig ResolveIntent(IntentVerificationConfig snapshot, IWorkspaceSettingsStore? settings)
    {
        return new IntentVerificationConfig
        {
            Enabled = WorkspaceSettingsResolver.ResolveBool(
                settings, WorkspaceSettingsKeys.IntentVerificationEnabled,
                "SOVRANT_TRUSTBOUNDARY_INTENT_ENABLED", fallback: snapshot.Enabled),
            ClarifyAmbiguous = WorkspaceSettingsResolver.ResolveBool(
                settings, WorkspaceSettingsKeys.IntentClarifyAmbiguous,
                "SOVRANT_TRUSTBOUNDARY_INTENT_CLARIFY", fallback: snapshot.ClarifyAmbiguous),
            BlockHarmfulIntent = WorkspaceSettingsResolver.ResolveBool(
                settings, WorkspaceSettingsKeys.IntentBlockHarmful,
                "SOVRANT_TRUSTBOUNDARY_INTENT_BLOCK_HARMFUL", fallback: snapshot.BlockHarmfulIntent),
        };
    }

    private static IReadOnlyList<CustomPattern> ResolveCustomPatterns(
        IWorkspaceSettingsStore? settings, string key, string envVar,
        IReadOnlyList<CustomPattern> fallback)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envVar);
        if (TryParseCustomPatterns(fromEnv, out var envParsed)) return envParsed;

        if (settings is not null)
        {
            string? fromDb = null;
            try
            {
                fromDb = settings.GetGlobalAsync(key).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or System.Data.Common.DbException
                or IOException
                or UnauthorizedAccessException)
            {
                // Store unavailable — fall through to snapshot.
            }
            if (TryParseCustomPatterns(fromDb, out var dbParsed)) return dbParsed;
        }

        return fallback;
    }

    private static bool TryParseCustomPatterns(string? raw, out IReadOnlyList<CustomPattern> result)
    {
        result = [];
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<List<CustomPattern>>(raw);
            if (parsed is null) return false;
            result = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
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
