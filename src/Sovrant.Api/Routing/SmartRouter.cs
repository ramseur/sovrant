using Microsoft.Extensions.Logging;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Api.Routing;

/// <summary>
/// Intelligent multi-provider router. Pings all providers on startup, scores them
/// by latency/cost/health, routes each request to the optimal provider, and
/// falls back automatically on failure. Ported from smart_router.py.
/// </summary>
public sealed class SmartRouter : ISmartRouter, IDisposable
{
    private readonly IReadOnlyList<ProviderInfo> _providers;
    private readonly Dictionary<string, ProviderInfo> _providersByName;
    private readonly RouterMode _mode;
    private readonly RouterStrategy _strategy;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmartRouter> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly List<Task> _recheckTasks = [];
    private bool _initialized;
    private volatile string? _pinnedProviderName;

    private static readonly Action<ILogger, string, double, int, Exception?> _logProviderOk =
        LoggerMessage.Define<string, double, int>(LogLevel.Information, new EventId(1, "ProviderOk"),
            "SmartRouter: {Provider} OK ({LatencyMs:F0}ms, status={Status})");
    private static readonly Action<ILogger, string, Exception?> _logProviderUnhealthy =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "ProviderUnhealthy"),
            "SmartRouter: {Provider} unhealthy or unreachable.");
    private static readonly Action<ILogger, string, string, Exception?> _logRouting =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(3, "Routing"),
            "SmartRouter: routing to {Provider} (strategy={Strategy})");
    private static readonly Action<ILogger, string, double, Exception?> _logHighErrorRate =
        LoggerMessage.Define<string, double>(LogLevel.Warning, new EventId(4, "HighErrorRate"),
            "SmartRouter: {Provider} error rate high ({Rate:P0}), marking unhealthy.");
    private static readonly Action<ILogger, string, Exception?> _logProviderRecovered =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "ProviderRecovered"),
            "SmartRouter: {Provider} recovered, re-adding to pool.");
    private static readonly Action<ILogger, Exception?> _logAllUnhealthyFallback =
        LoggerMessage.Define(LogLevel.Warning, new EventId(6, "AllUnhealthyFallback"),
            "SmartRouter: all providers failed health check — routing to configured providers anyway. Check DNS/network if this persists.");

    /// <summary>Initializes a new instance of <see cref="SmartRouter"/>.</summary>
    /// <param name="providers">The list of providers to manage.</param>
    /// <param name="mode">The routing mode.</param>
    /// <param name="strategy">The scoring strategy.</param>
    /// <param name="httpClient">HTTP client used for health-check pings.</param>
    /// <param name="logger">The logger.</param>
    public SmartRouter(
        IReadOnlyList<ProviderInfo> providers,
        RouterMode mode,
        RouterStrategy strategy,
        HttpClient httpClient,
        ILogger<SmartRouter> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _providers = providers;
        _providersByName = providers.ToDictionary(p => p.Provider.Name, StringComparer.OrdinalIgnoreCase);
        _mode = mode;
        _strategy = strategy;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await Task.WhenAll(_providers.Select(p => PingAsync(p, ct))).ConfigureAwait(false);
        _initialized = true;
    }

    /// <inheritdoc/>
    public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        // If a provider is pinned, prefer it when healthy.
        var pinned = _pinnedProviderName;
        if (pinned is not null)
        {
            var pinnedInfo = _providers.FirstOrDefault(p =>
                string.Equals(p.Provider.Name, pinned, StringComparison.OrdinalIgnoreCase) && p.Healthy);
            if (pinnedInfo is not null)
            {
                _logRouting(_logger, pinnedInfo.Provider.Name, "pinned", null);
                return Task.FromResult(pinnedInfo.Provider);
            }
            // Pinned provider is unhealthy — fall through to normal routing.
        }

        var available = _providers.Where(p => p.Healthy).ToList();
        if (available.Count == 0)
        {
            // All providers failed the startup health check (e.g. transient DNS failure in WSL/CI).
            // Fall back to all configured providers and let the actual request surface the real error.
            if (_providers.Count == 0)
                throw new InvalidOperationException("SmartRouter: no providers configured.");
            _logAllUnhealthyFallback(_logger, null);
            available = _providers.ToList();
        }
        var selected = _mode == RouterMode.Fixed
            ? available[0]
            : available.MinBy(p => p.Score(_strategy))!;

        _logRouting(_logger, selected.Provider.Name, _strategy.ToString(), null);
        return Task.FromResult(selected.Provider);
    }

    /// <inheritdoc/>
    public Task PinProviderAsync(string? providerName, CancellationToken ct = default)
    {
        if (providerName is not null)
        {
            if (!_providersByName.ContainsKey(providerName))
                throw new InvalidOperationException(
                    $"Provider '{providerName}' is not configured. " +
                    $"Known providers: {string.Join(", ", _providers.Select(p => p.Provider.Name))}");
        }
        _pinnedProviderName = providerName;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerName);
        if (!_providersByName.TryGetValue(providerName, out var info))
            return Task.CompletedTask;

        info.RequestCount++;
        if (success)
        {
            const double alpha = 0.3;
            info.AvgLatencyMs = alpha * durationMs + (1 - alpha) * info.AvgLatencyMs;
        }
        else
        {
            info.ErrorCount++;
            if (info.RequestCount >= 3 && info.ErrorRate > 0.7)
            {
                _logHighErrorRate(_logger, providerName, info.ErrorRate, null);
                info.Healthy = false;
                var recheckTask = RecheckAsync(info, TimeSpan.FromSeconds(60), _shutdownCts.Token);
                lock (_recheckTasks) { _recheckTasks.Add(recheckTask); }
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProviderStatus> GetStatus() =>
        _providers.Select(p => new ProviderStatus(
            p.Provider.Name,
            p.Healthy,
            Math.Round(p.AvgLatencyMs, 1),
            p.CostPer1kTokens,
            p.RequestCount,
            p.ErrorCount,
            p.ErrorRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture),
            p.Healthy
                ? Math.Round(p.Score(_strategy), 3).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "N/A"
        )).ToList();

    private async Task PingAsync(ProviderInfo info, CancellationToken ct)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var baseUri = new Uri(info.Provider.BaseUrl.ToString().TrimEnd('/') + "/");
            var pingUri = new Uri(baseUri, info.PingPath.TrimStart('/'));
            using var resp = await _httpClient.GetAsync(pingUri, ct).ConfigureAwait(false);
            var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            int statusCode = (int)resp.StatusCode;
            if (statusCode is 200 or 400 or 401 or 403)
            {
                info.Healthy = true;
                info.LatencyMs = elapsedMs;
                info.AvgLatencyMs = elapsedMs;
                _logProviderOk(_logger, info.Provider.Name, elapsedMs, statusCode, null);
            }
            else
            {
                info.Healthy = false;
                _logProviderUnhealthy(_logger, info.Provider.Name, null);
            }
        }
        catch (HttpRequestException ex)
        {
            info.Healthy = false;
            // Suppress full stack trace for connection-refused — it's just a provider not running.
            var inner = ex.InnerException;
            bool isConnRefused = inner is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.ConnectionRefused };
            _logProviderUnhealthy(_logger, info.Provider.Name, isConnRefused ? null : ex);
        }
        catch (TaskCanceledException ex)
        {
            info.Healthy = false;
            _logProviderUnhealthy(_logger, info.Provider.Name, ex);
        }
        catch (UriFormatException ex)
        {
            info.Healthy = false;
            _logProviderUnhealthy(_logger, info.Provider.Name, ex);
        }
    }

    private async Task RecheckAsync(ProviderInfo info, TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
            await PingAsync(info, ct).ConfigureAwait(false);
            if (info.Healthy)
            {
                _logProviderRecovered(_logger, info.Provider.Name, null);
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _shutdownCts.Cancel();
        Task[] tasks;
        lock (_recheckTasks) { tasks = [.. _recheckTasks]; }
        Task.WhenAll(tasks).Wait(TimeSpan.FromSeconds(5));
        _shutdownCts.Dispose();
    }
}
