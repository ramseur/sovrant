using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Session;

/// <summary>
/// Persists session entries as JSONL files under <c>~/.sovrant/sessions/</c>.
/// Rotates the active file when it exceeds 256 KB, keeping at most 3 rotations.
/// </summary>
public sealed partial class JsonlSessionStore : ISessionStore, IDisposable
{
    private const long RotationThresholdBytes = 256 * 1024;
    private const int MaxRotations = 3;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly string _sessionsDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<JsonlSessionStore> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rotating session file {Path}")]
    private static partial void LogRotating(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete old rotation {Path}")]
    private static partial void LogRotationDeleteFailed(ILogger logger, string path, Exception ex);

    public JsonlSessionStore(ILogger<JsonlSessionStore> logger) : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sovrant", "sessions"),
        logger)
    { }

    internal JsonlSessionStore(string sessionsDir, ILogger<JsonlSessionStore> logger)
    {
        _sessionsDir = sessionsDir;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_sessionsDir);
            var path = GetSessionPath(sessionId);

            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (info.Length >= RotationThresholdBytes)
                    RotateFile(path);
            }

            var line = JsonSerializer.Serialize(entry, s_jsonOptions) + Environment.NewLine;
            await File.AppendAllTextAsync(path, line, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        var path = GetSessionPath(sessionId);
        if (!File.Exists(path))
            return [];

        var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
        var entries = new List<SessionEntry>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = JsonSerializer.Deserialize<SessionEntry>(line, s_jsonOptions);
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_sessionsDir))
            return Task.FromResult<IReadOnlyList<string>>([]);

        var ids = Directory.EnumerateFiles(_sessionsDir, "*.jsonl")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(name => !name.Contains('.', StringComparison.Ordinal)) // exclude rotations like "id.1"
            .Order(StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(ids);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = GetSessionPath(sessionId);
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        // Also delete rotations (e.g. id.1.jsonl, id.2.jsonl)
        var dir = Path.GetDirectoryName(path)!;
        foreach (var rotation in Directory.EnumerateFiles(dir, $"{sessionId}.*.jsonl"))
        {
            try { File.Delete(rotation); } catch (IOException) { }
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!Directory.Exists(_sessionsDir))
            return Task.FromResult(0);

        var files = Directory.GetFiles(_sessionsDir, "*.jsonl");
        foreach (var file in files)
        {
            try { File.Delete(file); } catch (IOException) { }
        }

        return Task.FromResult(files.Length);
    }

    private string GetSessionPath(string sessionId)
    {
        var path = Path.Combine(_sessionsDir, $"{sessionId}.jsonl");
        var fullPath = Path.GetFullPath(path);
        var fullDir = Path.GetFullPath(_sessionsDir);
        if (!fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Session ID '{sessionId}' resolves outside the sessions directory.", nameof(sessionId));
        return path;
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();

    private void RotateFile(string path)
    {
        LogRotating(_logger, path);

        var baseName = Path.GetFileNameWithoutExtension(path);
        var dir = Path.GetDirectoryName(path)!;

        // Delete the oldest rotation beyond the limit
        var oldest = Path.Combine(dir, $"{baseName}.{MaxRotations}.jsonl");
        if (File.Exists(oldest))
        {
            try { File.Delete(oldest); }
            catch (IOException ex) { LogRotationDeleteFailed(_logger, oldest, ex); }
        }

        // Shift existing rotations up by one
        for (var i = MaxRotations - 1; i >= 1; i--)
        {
            var src = Path.Combine(dir, $"{baseName}.{i}.jsonl");
            var dst = Path.Combine(dir, $"{baseName}.{i + 1}.jsonl");
            if (File.Exists(src))
                File.Move(src, dst, overwrite: true);
        }

        // Rename current file to .1
        File.Move(path, Path.Combine(dir, $"{baseName}.1.jsonl"), overwrite: true);
    }
}
