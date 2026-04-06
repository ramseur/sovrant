using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Memory;

/// <summary>
/// File-based implementation of <see cref="IMemoryStore"/>.
/// <list type="bullet">
///   <item>Session summaries → <c>~/.sovrant/sessions/{project-slug}/{timestamp}-summary.json</c></item>
///   <item>Learned patterns → <c>.sovrant/learned/{id}.json</c></item>
///   <item>Instincts → <c>~/.sovrant/instincts/{id}.json</c></item>
/// </list>
/// Phase 31 (SQLite) replaces this with a database-backed implementation.
/// </summary>
public sealed partial class FileMemoryStore : IMemoryStore, IDisposable
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _summariesBaseDir;
    private readonly string _learnedBaseDir;
    private readonly string _instinctsDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<FileMemoryStore> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved session summary for {SessionId} to {Path}")]
    private static partial void LogSummarySaved(ILogger logger, string sessionId, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved learned pattern {Id} to {Path}")]
    private static partial void LogPatternSaved(ILogger logger, string id, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved instinct {Id} to {Path}")]
    private static partial void LogInstinctSaved(ILogger logger, string id, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pruned {Count} instincts below threshold {Threshold}")]
    private static partial void LogInstinctsPruned(ILogger logger, int count, double threshold);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read memory file {Path}")]
    private static partial void LogReadFailed(ILogger logger, string path, Exception ex);

    public FileMemoryStore(ILogger<FileMemoryStore> logger)
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sovrant", "summaries"),
            Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "learned"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sovrant", "instincts"),
            logger)
    { }

    internal FileMemoryStore(string summariesBaseDir, string learnedBaseDir, string instinctsDir, ILogger<FileMemoryStore> logger)
    {
        _summariesBaseDir = summariesBaseDir;
        _learnedBaseDir = learnedBaseDir;
        _instinctsDir = instinctsDir;
        _logger = logger;
    }

    // ── Session Summaries ───────────────────────────────────────────────

    public async Task SaveSummaryAsync(SessionSummary summary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var projectSlug = SlugifyProject(summary.Project);
        var dir = Path.Combine(_summariesBaseDir, projectSlug);
        Directory.CreateDirectory(dir);

        var timestamp = summary.EndedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(dir, $"{timestamp}-summary.json");

        var json = JsonSerializer.Serialize(summary, s_json);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        LogSummarySaved(_logger, summary.SessionId, path);
    }

    public async Task<IReadOnlyList<SessionSummary>> LoadSummariesAsync(string project, int maxCount = 5, CancellationToken ct = default)
    {
        var projectSlug = SlugifyProject(project);
        var dir = Path.Combine(_summariesBaseDir, projectSlug);
        if (!Directory.Exists(dir))
            return [];

        var files = Directory.GetFiles(dir, "*-summary.json")
            .OrderDescending()
            .Take(maxCount);

        var summaries = new List<SessionSummary>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var summary = await TryReadJsonAsync<SessionSummary>(file, ct).ConfigureAwait(false);
            if (summary is not null)
                summaries.Add(summary);
        }

        return summaries;
    }

    /// <summary>Workspace-scoped overload — file store ignores workspace, delegates to unscoped version.</summary>
    public Task<IReadOnlyList<SessionSummary>> LoadSummariesAsync(string project, string workspaceId, int maxCount = 5, CancellationToken ct = default) =>
        LoadSummariesAsync(project, maxCount, ct);

    // ─��� Learned Patterns ────────────���───────────────────────────────────

    public async Task SavePatternAsync(LearnedPattern pattern, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var dir = GetLearnedDir(pattern.Project);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{pattern.Id}.json");
        var json = JsonSerializer.Serialize(pattern, s_json);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        LogPatternSaved(_logger, pattern.Id, path);
    }

    public async Task<IReadOnlyList<LearnedPattern>> LoadPatternsAsync(string project, CancellationToken ct = default)
    {
        var dir = GetLearnedDir(project);
        if (!Directory.Exists(dir))
            return [];

        var patterns = new List<LearnedPattern>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            var pattern = await TryReadJsonAsync<LearnedPattern>(file, ct).ConfigureAwait(false);
            if (pattern is not null)
                patterns.Add(pattern);
        }

        return patterns
            .OrderByDescending(p => p.Confidence)
            .ToList();
    }

    /// <summary>Workspace-scoped overload — file store ignores workspace, delegates to unscoped version.</summary>
    public Task<IReadOnlyList<LearnedPattern>> LoadPatternsAsync(string project, string workspaceId, CancellationToken ct = default) =>
        LoadPatternsAsync(project, ct);

    public Task RemovePatternAsync(string id, string project, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var dir = GetLearnedDir(project);
        var path = Path.Combine(dir, $"{id}.json");
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetLearnedDir(string project)
    {
        // Per-project learned patterns use a project-specific subdirectory
        var projectSlug = SlugifyProject(project);
        return Path.Combine(_learnedBaseDir, projectSlug);
    }

    // ���─ Instincts ───────────���───────────────────────────────────────────

    public async Task SaveInstinctAsync(Instinct instinct, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instinct);

        Directory.CreateDirectory(_instinctsDir);
        var path = Path.Combine(_instinctsDir, $"{instinct.Id}.json");
        var json = JsonSerializer.Serialize(instinct, s_json);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        LogInstinctSaved(_logger, instinct.Id, path);
    }

    public async Task<IReadOnlyList<Instinct>> LoadInstinctsAsync(double minConfidence = Instinct.PruneThreshold, CancellationToken ct = default)
    {
        if (!Directory.Exists(_instinctsDir))
            return [];

        var instincts = new List<Instinct>();
        foreach (var file in Directory.GetFiles(_instinctsDir, "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            var instinct = await TryReadJsonAsync<Instinct>(file, ct).ConfigureAwait(false);
            if (instinct is not null && instinct.Confidence >= minConfidence)
                instincts.Add(instinct);
        }

        return instincts
            .OrderByDescending(i => i.Confidence)
            .ToList();
    }

    public async Task ReinforceInstinctAsync(string id, string evidence, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = Path.Combine(_instinctsDir, $"{id}.json");
            var instinct = await TryReadJsonAsync<Instinct>(path, ct).ConfigureAwait(false);
            if (instinct is null) return;

            var updated = instinct with
            {
                Confidence = Math.Min(1.0, instinct.Confidence + Instinct.ReinforcementDelta),
                Evidence = [.. instinct.Evidence, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}: {evidence}"],
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await SaveInstinctAsync(updated, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CorrectInstinctAsync(string id, string evidence, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = Path.Combine(_instinctsDir, $"{id}.json");
            var instinct = await TryReadJsonAsync<Instinct>(path, ct).ConfigureAwait(false);
            if (instinct is null) return;

            var newConfidence = instinct.Confidence - Instinct.CorrectionDelta;
            if (newConfidence < Instinct.PruneThreshold)
            {
                // Prune: confidence dropped below threshold
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            var updated = instinct with
            {
                Confidence = newConfidence,
                Evidence = [.. instinct.Evidence, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}: CORRECTION — {evidence}"],
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await SaveInstinctAsync(updated, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task RemoveInstinctAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = Path.Combine(_instinctsDir, $"{id}.json");
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<int> PruneInstinctsAsync(double threshold = Instinct.PruneThreshold, CancellationToken ct = default)
    {
        if (!Directory.Exists(_instinctsDir))
            return 0;

        var pruned = 0;
        foreach (var file in Directory.GetFiles(_instinctsDir, "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            var instinct = await TryReadJsonAsync<Instinct>(file, ct).ConfigureAwait(false);
            if (instinct is not null && instinct.Confidence < threshold)
            {
                File.Delete(file);
                pruned++;
            }
        }

        LogInstinctsPruned(_logger, pruned, threshold);
        return pruned;
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<T?> TryReadJsonAsync<T>(string path, CancellationToken ct) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, s_json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            LogReadFailed(_logger, path, ex);
            return null;
        }
    }

    internal static string SlugifyProject(string project)
    {
        if (string.IsNullOrWhiteSpace(project))
            return "default";

        var sb = new StringBuilder(project.Length);
        foreach (var c in project)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
            else if (c is '/' or '\\' or ' ' or '-' or '_')
                sb.Append('-');
        }

        // Collapse consecutive dashes and trim
        var slug = sb.ToString();
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Trim('-');
    }
}
