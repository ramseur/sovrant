using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Providers;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Phase 145 Part B — on a shared Web server each member picks one of the admin-configured provider
/// profiles, and that pick must apply to their conversations only. This router wraps the install's
/// default router: when the current conversation (<see cref="SessionContext"/>) has a
/// <see cref="SessionConfig.ProviderProfileId"/>, the request goes to that shared profile's base URL
/// with its key; otherwise it goes to the default router unchanged. Nothing global is rewritten, so
/// one member switching models never changes anyone else's.
/// </summary>
public sealed class SessionProviderRouter : ISmartRouter, IAsyncDisposable
{
    public const string HttpClientName = "SovrantSessionProvider";

    private readonly ISmartRouter _inner;
    private readonly IProviderProfileStore _profiles;
    private readonly ICredentialStore _credentials;
    private readonly IScopedProviderFactory _factory;
    private readonly IHttpClientFactory _http;
    private readonly ConcurrentDictionary<string, (string Stamp, ISmartRouter Router)> _cache = new(StringComparer.Ordinal);

    public SessionProviderRouter(ISmartRouter inner, IProviderProfileStore profiles, ICredentialStore credentials,
        IScopedProviderFactory factory, IHttpClientFactory http)
    {
        _inner = inner;
        _profiles = profiles;
        _credentials = credentials;
        _factory = factory;
        _http = http;
    }

    public Task InitializeAsync(CancellationToken ct = default) => _inner.InitializeAsync(ct);

    public async Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) =>
        await SessionRouterAsync(ct).ConfigureAwait(false) is { } scoped
            ? await scoped.RouteAsync(req, ct).ConfigureAwait(false)
            : await _inner.RouteAsync(req, ct).ConfigureAwait(false);

    public async Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
        await SessionRouterAsync(ct).ConfigureAwait(false) is { } scoped
            ? await scoped.RouteWithIntentAsync(req, ct).ConfigureAwait(false)
            : await _inner.RouteWithIntentAsync(req, ct).ConfigureAwait(false);

    public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) =>
        _inner.RecordResultAsync(providerName, success, durationMs, ct);

    public IReadOnlyList<ProviderStatus> GetStatus() => _inner.GetStatus();

    public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => _inner.PinProviderAsync(providerName, ct);

    public bool IntentRoutingEnabled
    {
        get => _inner.IntentRoutingEnabled;
        set => _inner.IntentRoutingEnabled = value;
    }

    /// <summary>
    /// The router for the current conversation's profile, or null to use the default. Rebuilt when the
    /// admin changes the profile's address or key (the cache is keyed on both).
    /// </summary>
    private async Task<ISmartRouter?> SessionRouterAsync(CancellationToken ct)
    {
        var profileId = SessionContext.Current?.ProviderProfileId;
        if (string.IsNullOrEmpty(profileId))
            return null;
        var profile = await _profiles.GetAsync(profileId, ct).ConfigureAwait(false);
        if (profile is null || !Uri.TryCreate(EnsureTrailingSlash(profile.BaseUrl), UriKind.Absolute, out var baseUri))
            return null; // deleted or unusable: fall back to the install default
        var key = string.IsNullOrEmpty(profile.CredentialId)
            ? string.Empty
            : await _credentials.RetrieveAsync(profile.CredentialId, ct).ConfigureAwait(false) ?? string.Empty;

        var stamp = baseUri + "|" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        if (_cache.TryGetValue(profileId, out var cached) && cached.Stamp == stamp)
            return cached.Router;

        var client = _http.CreateClient(HttpClientName);
        client.BaseAddress = baseUri;
        var router = _factory.Create(client, key);
        _cache[profileId] = (stamp, router);
        return router;
    }

    private static string EnsureTrailingSlash(string? url) =>
        string.IsNullOrWhiteSpace(url) ? string.Empty : url.EndsWith('/') ? url : url + "/";

    public async ValueTask DisposeAsync()
    {
        if (_inner is IAsyncDisposable d)
            await d.DisposeAsync().ConfigureAwait(false);
    }
}

public static class SessionProviderRoutingExtensions
{
    /// <summary>
    /// Wraps the registered <see cref="ISmartRouter"/> in a <see cref="SessionProviderRouter"/> so each
    /// conversation can use its own shared provider profile. Call after the runtime is registered.
    /// </summary>
    public static IServiceCollection AddSessionProviderRouting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var existing = services.LastOrDefault(d => d.ServiceType == typeof(ISmartRouter))
            ?? throw new InvalidOperationException("Register the runtime (ISmartRouter) before AddSessionProviderRouting.");
        services.Remove(existing);
        services.AddHttpClient(SessionProviderRouter.HttpClientName);
        services.AddSingleton<ISmartRouter>(sp =>
        {
            var inner = existing.ImplementationInstance as ISmartRouter
                ?? existing.ImplementationFactory?.Invoke(sp) as ISmartRouter
                ?? (ISmartRouter)ActivatorUtilities.CreateInstance(sp, existing.ImplementationType!);
            return new SessionProviderRouter(inner,
                sp.GetRequiredService<IProviderProfileStore>(),
                sp.GetRequiredService<ICredentialStore>(),
                sp.GetRequiredService<IScopedProviderFactory>(),
                sp.GetRequiredService<IHttpClientFactory>());
        });
        return services;
    }
}
