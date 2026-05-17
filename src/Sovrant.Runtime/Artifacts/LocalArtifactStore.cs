using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// On-disk <see cref="IArtifactStore"/> implementation that writes artifacts
/// into a tenant-scoped directory tree under <c>SOVRANT_ARTIFACTS_ROOT</c>
/// (default <c>~/.sovrant/workspaces</c>).
/// </summary>
/// <remarks>
/// Workspace-level layout: <c>{root}/{workspace}/artifacts/{run}/</c>
/// Project-level layout:   <c>{root}/{workspace}/projects/{project}/artifacts/{run}/</c>
/// The routing is determined by <see cref="ArtifactScope.IsWorkspaceLevel"/>:
/// when no real project is selected the artifact lands at workspace level;
/// an explicit project routes it under the project's artifacts folder.
/// Each run directory contains a <c>_manifest.json</c> with metadata.
/// </remarks>
public sealed partial class LocalArtifactStore : IArtifactStore
{
    private readonly string _root;
    private readonly string? _accessPathPrefix;
    private readonly ILogger<LocalArtifactStore> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
    };

    public LocalArtifactStore(
        ILogger<LocalArtifactStore> logger,
        string? root = null,
        string? accessPathPrefix = null)
    {
        _logger = logger;
        _root = root
            ?? Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sovrant", "workspaces");
        _accessPathPrefix = accessPathPrefix
            ?? Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_URL_PREFIX");
    }

    /// <summary>The resolved root directory for all artifacts.</summary>
    public string Root => _root;

    /// <inheritdoc/>
    public Task<ArtifactHandle> CreateRunScopeAsync(ArtifactScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (string.IsNullOrWhiteSpace(scope.RunId))
            throw new ArgumentException("RunId is required to create a run scope.", nameof(scope));

        ValidateSegment(scope.WorkspaceId, "WorkspaceId");
        ValidateSegment(scope.ProjectId, "ProjectId");
        ValidateSegment(scope.RunId, "RunId");

        var runDir = BuildScopePath(scope);
        Directory.CreateDirectory(runDir);

        // Write initial manifest
        var manifest = new ArtifactManifest
        {
            RunId = scope.RunId,
            UserId = scope.UserId,
            WorkspaceId = scope.WorkspaceId,
            ProjectId = scope.ProjectId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        WriteManifest(runDir, manifest);

        LogScopeCreated(scope.WorkspaceId, scope.ProjectId, scope.RunId);

        return Task.FromResult(new ArtifactHandle
        {
            Scope = scope,
            ResolvedRoot = runDir,
        });
    }

    /// <inheritdoc/>
    public async Task WriteAsync(
        ArtifactHandle handle,
        string relativePath,
        Stream content,
        string? contentType = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = ResolveAndGuard(handle.ResolvedRoot, relativePath);

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct).ConfigureAwait(false);

        // Update manifest file list
        UpdateManifest(handle.ResolvedRoot, relativePath);
    }

    /// <inheritdoc/>
    public Task<Stream> ReadAsync(ArtifactHandle handle, string relativePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        var fullPath = ResolveAndGuard(handle.ResolvedRoot, relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Artifact not found: {relativePath}", fullPath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ArtifactEntry> ListAsync(
        ArtifactScope scope,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var scopePath = BuildScopePath(scope);

        if (!Directory.Exists(scopePath))
            yield break;

        await Task.CompletedTask.ConfigureAwait(false); // async enumerable requirement

        foreach (var file in Directory.EnumerateFiles(scopePath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "_manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(scopePath, file).Replace('\\', '/');
            var info = new FileInfo(file);

            // Derive runId from path if scope doesn't have one
            string? runId = scope.RunId;
            if (runId is null)
            {
                var relativeToScope = Path.GetRelativePath(scopePath, file);
                var firstSegment = relativeToScope.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                if (!string.IsNullOrEmpty(firstSegment))
                    runId = firstSegment;
            }

            yield return new ArtifactEntry
            {
                RelativePath = relativePath,
                SizeBytes = info.Length,
                LastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                RunId = runId,
            };
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(ArtifactScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var scopePath = BuildScopePath(scope);

        if (Directory.Exists(scopePath))
        {
            Directory.Delete(scopePath, recursive: true);
            LogScopeDeleted(scopePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Uri?> GetAccessUrlAsync(
        ArtifactHandle handle,
        string relativePath,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        var fullPath = ResolveAndGuard(handle.ResolvedRoot, relativePath);

        if (!File.Exists(fullPath))
            return Task.FromResult<Uri?>(null);

        if (!string.IsNullOrEmpty(_accessPathPrefix))
        {
            var scope = handle.Scope;
            var prefix = _accessPathPrefix.TrimEnd('/');
            var segs = new List<string> { prefix, Uri.EscapeDataString(scope.WorkspaceId) };

            if (scope.IsWorkspaceLevel)
            {
                segs.Add("artifacts");
            }
            else
            {
                segs.Add("projects");
                segs.Add(Uri.EscapeDataString(scope.ProjectId));
                segs.Add("artifacts");
            }

            if (!string.IsNullOrEmpty(scope.RunId))
                segs.Add(Uri.EscapeDataString(scope.RunId));

            foreach (var seg in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
                segs.Add(Uri.EscapeDataString(seg));

            var url = string.Join('/', segs);
            var kind = Uri.IsWellFormedUriString(url, UriKind.Absolute) ? UriKind.Absolute : UriKind.Relative;
            return Task.FromResult<Uri?>(new Uri(url, kind));
        }

        return Task.FromResult<Uri?>(new Uri($"file:///{fullPath.Replace('\\', '/')}"));
    }

    // ── Path helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Builds the filesystem path for a scope.
    /// Workspace-level (no real project): <c>{root}/{ws}/artifacts/{run}</c>
    /// Project-level (explicit project):  <c>{root}/{ws}/projects/{proj}/artifacts/{run}</c>
    /// </summary>
    private string BuildScopePath(ArtifactScope scope)
    {
        var wsDir = Path.Combine(_root, MakeDirSegment(scope.WorkspaceId, scope.WorkspaceName));

        string artifactsDir;
        if (scope.IsWorkspaceLevel)
        {
            // No real project selected — store at workspace level.
            artifactsDir = Path.Combine(wsDir, "artifacts");
        }
        else
        {
            // Explicit project — nest under projects/{proj}/artifacts.
            var projDir = Path.Combine(wsDir, "projects", MakeDirSegment(scope.ProjectId, scope.ProjectName));
            artifactsDir = Path.Combine(projDir, "artifacts");
        }

        return scope.RunId is not null ? Path.Combine(artifactsDir, scope.RunId) : artifactsDir;
    }

    /// <summary>Returns <c>{id}__{safeName}</c> if name is usable, otherwise bare <c>{id}</c>.</summary>
    private static string MakeDirSegment(string id, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return id;
        var safe = MakeSafeName(name);
        return string.IsNullOrEmpty(safe) ? id : $"{id}__{safe}";
    }

    /// <summary>Converts a display name into a filesystem-safe lowercase suffix (letters, digits, hyphens).</summary>
    private static string MakeSafeName(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(char.ToLowerInvariant(c));
            else if (c == ' ' || c == '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    /// <summary>
    /// Resolves a relative path under a root and guards against directory
    /// traversal attacks.
    /// </summary>
    private static string ResolveAndGuard(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path must not be empty.", nameof(relativePath));

        // Normalize separators
        var normalized = relativePath.Replace('\\', '/');

        // Reject obvious traversal patterns before even combining
        if (normalized.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException(
                $"Path traversal is not allowed: '{relativePath}'", nameof(relativePath));

        var fullPath = Path.GetFullPath(Path.Combine(root, normalized));

        // Ensure the resolved path is still under the root
        var normalizedRoot = Path.GetFullPath(root);
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Path traversal is not allowed: '{relativePath}'", nameof(relativePath));

        return fullPath;
    }

    // Windows-invalid filename characters (NTFS + FAT32): < > : " / \ | ? *
    // Plus control characters 0–31. @ is technically legal on NTFS but excluded
    // here because it appears in email addresses that may flow in as user IDs.
    private static readonly SearchValues<char> InvalidSegmentChars =
        SearchValues.Create(['<', '>', ':', '"', '/', '\\', '|', '?', '*', '@']);

    /// <summary>Validates a scope segment against path traversal and Windows-illegal filename characters.</summary>
    private static void ValidateSegment(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} must not be empty.", paramName);

        if (value.EndsWith(' ') || value.EndsWith('.'))
            throw new ArgumentException(
                $"{paramName} must not end with a space or period: '{value}'", paramName);

        if (value.Contains("..", StringComparison.Ordinal) ||
            value.IndexOfAny(InvalidSegmentChars) >= 0 ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{paramName} contains invalid characters: '{value}'", paramName);
        }
    }

    // ── Manifest helpers ────────────────────────────────────────────────

    private static void WriteManifest(string runDir, ArtifactManifest manifest)
    {
        var manifestPath = Path.Combine(runDir, "_manifest.json");
        var json = JsonSerializer.Serialize(manifest, s_jsonOptions);
        File.WriteAllText(manifestPath, json);
    }

    private void UpdateManifest(string runDir, string relativePath)
    {
        var manifestPath = Path.Combine(runDir, "_manifest.json");
        try
        {
            ArtifactManifest? manifest = null;
            if (File.Exists(manifestPath))
            {
                var json = File.ReadAllText(manifestPath);
                manifest = JsonSerializer.Deserialize<ArtifactManifest>(json);
            }

            manifest ??= new ArtifactManifest();
            var normalized = relativePath.Replace('\\', '/');
            if (!manifest.Files.Contains(normalized, StringComparer.Ordinal))
            {
                manifest.Files.Add(normalized);
                WriteManifest(runDir, manifest);
            }
        }
        catch (IOException ex)
        {
            LogManifestUpdateFailed(manifestPath, ex);
        }
        catch (JsonException ex)
        {
            LogManifestUpdateFailed(manifestPath, ex);
        }
    }

    // ── Logging ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created artifact scope: {WorkspaceId}/{ProjectId}/{RunId}")]
    private partial void LogScopeCreated(string workspaceId, string projectId, string runId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted artifact scope: {ScopePath}")]
    private partial void LogScopeDeleted(string scopePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update manifest at {ManifestPath}")]
    private partial void LogManifestUpdateFailed(string manifestPath, Exception ex);
}
