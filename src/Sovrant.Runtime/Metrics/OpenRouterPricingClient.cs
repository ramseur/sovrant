using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Fetches model pricing from OpenRouter's <c>GET /api/v1/models</c> endpoint.
/// Two-tier cache: in-memory (default 6h TTL) and disk-backed fallback (default 7d TTL).
/// When the network is unavailable, degrades gracefully to the disk cache.
/// </summary>
public sealed partial class OpenRouterPricingClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly Uri _endpoint;
    private readonly string _diskCachePath;
    private readonly TimeSpan _memoryCacheTtl;
    private readonly TimeSpan _diskCacheTtl;

    private PricingSnapshot? _memoryCache;
    private DateTimeOffset _memoryCacheExpiry;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Using cached pricing from {Timestamp} — network unavailable")]
    private static partial void LogUsingDiskCache(ILogger logger, DateTimeOffset timestamp);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disk cache expired ({AgeDays:F1} days old) — pricing data may be stale")]
    private static partial void LogDiskCacheExpired(ILogger logger, double ageDays);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No pricing data available — cost estimates will return null")]
    private static partial void LogNoPricingData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch pricing from {Endpoint}")]
    private static partial void LogFetchFailed(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read disk cache from {Path}")]
    private static partial void LogDiskReadFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write disk cache to {Path}")]
    private static partial void LogDiskWriteFailed(ILogger logger, Exception ex, string path);

    public OpenRouterPricingClient(
        HttpClient http,
        ILogger<OpenRouterPricingClient> logger,
        string? endpoint = null,
        string? diskCachePath = null,
        TimeSpan? memoryCacheTtl = null,
        TimeSpan? diskCacheTtl = null)
    {
        _http = http;
        _logger = logger;
        _endpoint = new Uri(endpoint ?? "https://openrouter.ai/api/v1/models");
        _diskCachePath = diskCachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "cache", "openrouter-models.json");
        _memoryCacheTtl = memoryCacheTtl ?? TimeSpan.FromHours(6);
        _diskCacheTtl = diskCacheTtl ?? TimeSpan.FromDays(7);
    }

    /// <summary>
    /// Gets the current pricing snapshot, refreshing from the network if the memory cache has expired.
    /// Falls back to disk cache, then returns <see langword="null"/> if no data is available.
    /// </summary>
    public async Task<PricingSnapshot?> GetSnapshotAsync(CancellationToken ct = default)
    {
        // Fast path: memory cache is fresh
        if (_memoryCache is not null && DateTimeOffset.UtcNow < _memoryCacheExpiry)
            return _memoryCache;

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock
            if (_memoryCache is not null && DateTimeOffset.UtcNow < _memoryCacheExpiry)
                return _memoryCache;

            // Try to fetch from network
            var snapshot = await FetchFromNetworkAsync(ct).ConfigureAwait(false);
            if (snapshot is not null)
            {
                _memoryCache = snapshot;
                _memoryCacheExpiry = DateTimeOffset.UtcNow + _memoryCacheTtl;
                _ = Task.Run(() => WriteDiskCacheAsync(snapshot), CancellationToken.None);
                return snapshot;
            }

            // Fall back to disk cache
            snapshot = await ReadDiskCacheAsync(ct).ConfigureAwait(false);
            if (snapshot is not null)
            {
                var age = DateTimeOffset.UtcNow - snapshot.FetchedAt;
                if (age <= _diskCacheTtl)
                {
                    LogUsingDiskCache(_logger, snapshot.FetchedAt);
                    _memoryCache = snapshot;
                    _memoryCacheExpiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30);
                    return snapshot;
                }

                LogDiskCacheExpired(_logger, age.TotalDays);
                _memoryCache = snapshot;
                _memoryCacheExpiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5);
                return snapshot;
            }

            LogNoPricingData(_logger);
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Returns the in-memory cached snapshot if available, or <c>null</c> if not yet loaded.
    /// Non-blocking — use this from sync callers to avoid <c>.GetAwaiter().GetResult()</c>.
    /// </summary>
    public PricingSnapshot? CachedSnapshot => _memoryCache;

    /// <summary>The source label for the last successfully loaded snapshot.</summary>
    public string LastSource => _memoryCache?.Source ?? "none";

    public void Dispose() => _refreshLock.Dispose();

    private async Task<PricingSnapshot?> FetchFromNetworkAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(_endpoint, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var wrapper = await JsonSerializer.DeserializeAsync<ModelsResponse>(stream, s_jsonOptions, ct).ConfigureAwait(false);
            if (wrapper?.Data is null || wrapper.Data.Count == 0)
                return null;

            var models = new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in wrapper.Data)
            {
                if (entry.Pricing is not null && !string.IsNullOrWhiteSpace(entry.Id))
                    models[entry.Id] = entry.Pricing;
            }

            return new PricingSnapshot
            {
                FetchedAt = DateTimeOffset.UtcNow,
                Source = "openrouter-live",
                Models = models
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogFetchFailed(_logger, ex, _endpoint.AbsoluteUri);
            return null;
        }
    }

    private async Task<PricingSnapshot?> ReadDiskCacheAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_diskCachePath))
                return null;

            var stream = File.OpenRead(_diskCachePath);
            await using (stream.ConfigureAwait(false))
            {
                var snapshot = await JsonSerializer.DeserializeAsync<PricingSnapshot>(
                    stream, s_jsonOptions, ct).ConfigureAwait(false);
                return snapshot is not null ? snapshot with { Source = "openrouter-disk-cache" } : null;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            LogDiskReadFailed(_logger, ex, _diskCachePath);
            return null;
        }
    }

    private async Task WriteDiskCacheAsync(PricingSnapshot snapshot)
    {
        try
        {
            var dir = Path.GetDirectoryName(_diskCachePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            var stream = File.Create(_diskCachePath);
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(
                    stream, snapshot, s_jsonOptions).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException)
        {
            LogDiskWriteFailed(_logger, ex, _diskCachePath);
        }
    }

    private sealed record ModelsResponse
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<ModelPricingEntry>? Data { get; init; }
    }
}
