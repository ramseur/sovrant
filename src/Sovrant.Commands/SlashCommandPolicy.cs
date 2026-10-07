using Sovrant.Runtime.Auth;

namespace Sovrant.Commands;

/// <summary>Decides whether the current person may run a slash command. Null = allowed.</summary>
public interface ISlashCommandPolicy
{
    string? GetBlockReason(string commandName);
}

/// <summary>
/// On a shared server (Web), commands that change things for everyone or touch the server's disk are
/// admin-only: <c>/eval</c> runs commands from eval suites, <c>/memory</c> edits the server's memory
/// files, <c>/artifacts</c> can export to any path on the server, and <c>/swarm</c>, <c>/websearch</c>
/// and <c>/provider</c> change settings for everyone. Desktop and the CLI register no policy.
/// </summary>
public sealed class SharedServerCommandPolicy(IPrincipalAccessor principal) : ISlashCommandPolicy
{
    public static readonly IReadOnlySet<string> AdminOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "eval", "memory", "artifacts", "swarm", "websearch", "provider",
    };

    public string? GetBlockReason(string commandName) =>
        AdminOnly.Contains(commandName) && !principal.IsAdmin
            ? $"/{commandName} is for admins on a shared server — it changes things for everyone or uses the server's own files."
            : null;
}
