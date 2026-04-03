using Microsoft.Extensions.Logging;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Api.Routing;

/// <summary>
/// Intelligent multi-provider router. Pings all providers on startup, scores them
/// by latency/cost/health, routes each request to the optimal provider, and
/// falls back automatically on failure. Ported from smart_router.py.
/// </summary>
public sealed class SmartRouter : ISmartRouter
{
    private readonly IReadOnlyList<ProviderInfo> _providers;
    private readonly RouterMode _mode;
    private readonly RouterStrategy _strategy;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmartRouter> _logger;
    private bool _initialized;

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
        var available = _providers.Where(p => p.Healthy).ToList();
        if (available.Count == 0)
        {
            throw new InvalidOperationException(
                "SmartRouter: no healthy providers available. Check your API keys and provider health.");
        }
        var selected = _mode == RouterMode.Fixed
            ? available[0]
            : available.MinBy(p => p.Score(_strategy))!;

        _logRouting(_logger, selected.Provider.Name, _strategy.ToString(), null);
        return Task.FromResult(selected.Provider);
    }

    /// <inheritdoc/>
    public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerName);
        var info = _providers.FirstOrDefault(p => p.Provider.Name == providerName);
        if (info is null) return Task.CompletedTask;

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
                _ = RecheckAsync(info, TimeSpan.FromSeconds(60), CancellationToken.None);
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
            _logProviderUnhealthy(_logger, info.Provider.Name, ex);
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
}
