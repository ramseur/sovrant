using Microsoft.Extensions.Configuration;
using Sovrant.Api.Routing;

namespace Sovrant.Api.Config;

/// <summary>
/// Read-once snapshot of the user's router-mode and -strategy preferences.
/// Resolves the precedence chain at startup (env var → IConfiguration → default)
/// so consumers don't repeat the lookup, and keeps both values mutable so the
/// Settings UI can hot-swap them at runtime without rebuilding DI.
/// </summary>
public sealed class RouterOptions
{
    /// <summary>
    /// The active routing mode. Mutable so the Settings UI can hot-swap the value
    /// at runtime; <see cref="SmartRouter"/> consults this singleton on every
    /// route decision rather than capturing it at construction.
    /// </summary>
    public RouterMode Mode { get; set; } = RouterMode.Smart;

    /// <summary>
    /// The active scoring strategy. Mutable for the same reason as <see cref="Mode"/>.
    /// </summary>
    public RouterStrategy Strategy { get; set; } = RouterStrategy.Balanced;

    /// <summary>
    /// Resolves the routing options from environment variables and configuration.
    /// Precedence (highest first): <c>ROUTER_MODE</c> / <c>ROUTER_STRATEGY</c> env vars,
    /// then <c>Router:Mode</c> / <c>Router:Strategy</c> config keys, then defaults.
    /// </summary>
    public static RouterOptions Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var mode = Enum.TryParse<RouterMode>(
            Environment.GetEnvironmentVariable("ROUTER_MODE") ?? configuration["Router:Mode"], true,
            out var rm) ? rm : RouterMode.Smart;

        var strategy = Enum.TryParse<RouterStrategy>(
            Environment.GetEnvironmentVariable("ROUTER_STRATEGY") ?? configuration["Router:Strategy"], true,
            out var rs) ? rs : RouterStrategy.Balanced;

        return new RouterOptions { Mode = mode, Strategy = strategy };
    }
}
