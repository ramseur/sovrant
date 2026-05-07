using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;
using Sovrant.Tools.Extended;

namespace Sovrant.Client.Remote;

/// <summary>
/// Registers all services needed for remote Sovrant server mode.
/// Call <see cref="AddSovrantClient"/> instead of <c>AddSovrantRuntime()</c> when connecting
/// to a shared <c>Sovrant.Server</c> instance.
/// </summary>
public static class SovrantClientServiceExtensions
{
    /// <summary>
    /// Registers remote-mode service implementations that proxy to a Sovrant server.
    /// </summary>
    public static IServiceCollection AddSovrantClient(
        this IServiceCollection services,
        SovrantRemoteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Url))
            throw new InvalidOperationException(
                "Server URL must be set when RuntimeMode is 'remote'.");

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

        // Identity adapter — lets existing login/register UI work against the server.
        services.AddSingleton<IIdentityService, RemoteIdentityService>();

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
