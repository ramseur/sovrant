using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Agents.Shared;

/// <summary>
/// Shared, thread-safe scratch space for agents collaborating on a task. Holds named
/// variables, the current working directory, and any state that needs to be visible
/// across agent boundaries within a single orchestration run.
/// </summary>
public sealed partial class WorkspaceContext
{
    private readonly ConcurrentDictionary<string, string> _state =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The working directory for the current orchestration run.
    /// Defaults to <see cref="Directory.GetCurrentDirectory()"/> at construction time.
    /// </summary>
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();

    // ── Scoped artifact support (Phase 53) ─────────────────────────────

    /// <summary>
    /// The artifact store used for writing run-scoped artifacts.
    /// When set, <see cref="GetOrCreateArtifactsDirectory"/> routes through
    /// the store instead of the legacy flat layout.
    /// </summary>
    public IArtifactStore? ArtifactStore { get; init; }

    /// <summary>
    /// The artifact scope for the current run. Used together with
    /// <see cref="ArtifactStore"/> to resolve the scoped artifact directory.
    /// </summary>
    public ArtifactScope? ArtifactScope { get; init; }

    /// <summary>
    /// The current run's artifact handle, set after
    /// <see cref="IArtifactStore.CreateRunScopeAsync"/> is called.
    /// </summary>
    public ArtifactHandle? ArtifactHandle { get; set; }

    // ── Legacy artifact support (deprecated) ───────────────────────────

    /// <summary>
    /// Root directory for agent-generated artifacts. Defaults to <c>artifacts/</c>
    /// under the <see cref="WorkingDirectory"/>.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated.</b> Use <see cref="ArtifactStore"/> and <see cref="ArtifactScope"/>
    /// instead. This property is retained for backward compatibility during the
    /// transition period.
    /// </remarks>
    [Obsolete("Use ArtifactStore + ArtifactScope instead. Will be removed in a future release.")]
    public string ArtifactsRoot => Path.Combine(WorkingDirectory, "artifacts");

    /// <summary>
    /// Creates (if needed) and returns an artifacts subdirectory for the given prompt,
    /// using a filesystem-safe slug derived from the first ~60 chars of the prompt.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated.</b> When <see cref="ArtifactStore"/> and <see cref="ArtifactScope"/>
    /// are set, this method delegates to the scoped store. Otherwise it falls back to
    /// the legacy flat layout under <c>{cwd}/artifacts/</c>.
    /// </remarks>
    [Obsolete("Use GetArtifactsDirectoryAsync instead. Will be removed in a future release.")]
    public async Task<string> GetOrCreateArtifactsDirectoryAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        // If scoped store is available, use it
        if (ArtifactStore is not null && ArtifactScope is not null)
        {
            // Ensure we have a handle
            if (ArtifactHandle is null)
            {
                ArtifactHandle = await ArtifactStore
                    .CreateRunScopeAsync(ArtifactScope, ct)
                    .ConfigureAwait(false);
            }

            return ArtifactHandle.ResolvedRoot;
        }

        // Legacy fallback — always resolve to ~/.sovrant/workspaces, never CWD-relative.
        var slug = Slugify(prompt);
        var fallbackRoot = Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sovrant", "workspaces");
        var dir = Path.Combine(fallbackRoot, slug);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Returns the resolved artifact directory for the current run scope.
    /// If no scoped store is configured, falls back to the legacy artifacts root.
    /// </summary>
    public async Task<string> GetArtifactsDirectoryAsync(CancellationToken ct = default)
    {
        if (ArtifactHandle is not null)
            return ArtifactHandle.ResolvedRoot;

        if (ArtifactStore is not null && ArtifactScope is not null)
        {
            ArtifactHandle = await ArtifactStore
                .CreateRunScopeAsync(ArtifactScope, ct)
                .ConfigureAwait(false);
            return ArtifactHandle.ResolvedRoot;
        }

        // Legacy fallback — always resolve to ~/.sovrant/workspaces, never CWD-relative.
        var dir = Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sovrant", "workspaces");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Converts a prompt into a filesystem-safe folder name.
    /// Strips filler words, keeps up to 40 characters, lowercase, hyphen-separated.
    /// Safe on both Windows and Linux.
    /// </summary>
    internal static string Slugify(string text)
    {
#pragma warning disable CA1308 // Folder names should be lowercase for readability
        var lower = text.ToLowerInvariant();
#pragma warning restore CA1308

        // Replace non-alphanumeric with spaces for word splitting
        var cleaned = NonAlphanumericRegex().Replace(lower, " ");

        // Split into words and remove common filler
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !s_stopWords.Contains(w))
            .ToList();

        // Rejoin with hyphens
        var slug = string.Join('-', words);

        // Cap at 40 characters, trim at last word boundary
        if (slug.Length > 40)
        {
            slug = slug[..40];
            var lastHyphen = slug.LastIndexOf('-');
            if (lastHyphen > 10)
                slug = slug[..lastHyphen];
        }

        return string.IsNullOrEmpty(slug) ? "output" : slug;
    }

    private static readonly HashSet<string> s_stopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "in", "on", "of", "for", "to", "and", "or", "but",
        "is", "are", "was", "were", "be", "been", "being", "by", "at", "from",
        "with", "as", "into", "that", "this", "it", "its", "do", "does",
        "create", "write", "make", "generate", "build", "produce",
        "please", "can", "you", "me", "my", "i", "we", "our",
        "using", "about", "how", "what", "which", "where", "when",
        "md", "form", "format", "file", "document",
    };

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    /// <summary>Gets a named workspace variable, or <see langword="null"/> if not set.</summary>
    public string? Get(string key) =>
        _state.TryGetValue(key, out var value) ? value : null;

    /// <summary>Sets a named workspace variable.</summary>
    public void Set(string key, string value) =>
        _state[key] = value;

    /// <summary>Removes a named workspace variable. Returns <see langword="true"/> if it existed.</summary>
    public bool Remove(string key) =>
        _state.TryRemove(key, out _);

    /// <summary>Returns a point-in-time snapshot of all current workspace variables.</summary>
    public IReadOnlyDictionary<string, string> Snapshot() =>
        new Dictionary<string, string>(_state, StringComparer.Ordinal);
}
