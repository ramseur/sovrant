using Sovrant.Runtime.Permissions;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Permissions;

/// <summary>
/// An <see cref="IPermissionPolicy"/> that delegates to <see cref="ModeAwarePermissionPolicy"/>
/// using the live <see cref="MutableServerConfig.PermissionMode"/>, allowing the mode to be
/// changed at runtime via <c>PUT /v1/config</c>.
/// </summary>
internal sealed class MutablePermissionPolicy : IPermissionPolicy
{
    private readonly MutableServerConfig _config;

    public MutablePermissionPolicy(MutableServerConfig config) => _config = config;

    /// <inheritdoc/>
    public PolicyDecision Evaluate(string toolName, bool isDestructive) =>
        new ModeAwarePermissionPolicy(_config.PermissionMode).Evaluate(toolName, isDestructive);
}
