using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;
using Sovrant.Tools.Extended;

namespace Sovrant.Web.Services.Remote;

/// <summary>
/// Registers all services needed for remote Sovrant server mode.
/// The web frontend connects to a running Sovrant.Server via HTTP + SignalR
/// instead of running the runtime in-process.
/// </summary>
public static class SovrantClientServiceExtensions
{
    /// <summary>
    /// Registers remote-mode service implementations that proxy to a Sovrant server.
    /// Call this instead of <c>AddSovrantRuntime()</c> when <c>SOVRANT_RUNTIME_MODE=remote</c>.
    /// </summary>
    public static IServiceCollection AddSovrantClient(
        this IServiceCollection services,
        SovrantRemoteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Url))
            throw new InvalidOperationException(
                "SOVRANT_SERVER_URL or Sovrant:Server:Url must be set when RuntimeMode is 'remote'.");

        // Options + connection state (singletons).
        services.AddSingleton(options);
        services.AddSingleton<RemoteConnectionState>();

        // Named HttpClient with bearer token injection.
        services.AddTransient<SovrantApiDelegatingHandler>();
        services.AddHttpClient("SovrantApi", client =>
        {
            client.BaseAddress = new Uri(options.Url.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(120);
        }).AddHttpMessageHandler(sp =>
        {
            var opts = sp.GetRequiredService<SovrantRemoteOptions>();
            var state = sp.GetRequiredService<RemoteConnectionState>();
            return new SovrantApiDelegatingHandler(opts, state);
        });

        // SignalR streaming client.
        services.AddSingleton<SignalRStreamingClient>();

        // Remote service implementations.
        services.AddSingleton<IRuntimeSessionPool, RemoteRuntimeSessionPool>();
        services.AddSingleton<ISessionStore, RemoteSessionStore>();
        services.AddSingleton<IToolRegistry, RemoteToolRegistry>();
        services.AddSingleton<IArtifactStore, RemoteArtifactStore>();

        // Tool confirmation handler.
        services.AddSingleton<IToolConfirmationHandler>(sp =>
            new RemoteToolConfirmationHandler(sp.GetRequiredService<SignalRStreamingClient>()));

        // User input — not available in remote mode (server handles it).
        services.AddSingleton<IUserInputProvider, RemoteUserInputProvider>();

        return services;
    }
}

/// <summary>
/// Returns a fixed message when the engine asks for user input in remote mode.
/// </summary>
public sealed class RemoteUserInputProvider : IUserInputProvider
{
    public Task<string> AskAsync(string question, CancellationToken ct = default) =>
        Task.FromResult("[User input is not available in remote mode. Please proceed without it.]");
}
