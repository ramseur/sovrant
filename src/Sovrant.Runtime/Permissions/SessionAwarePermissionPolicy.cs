using Sovrant.Runtime.Conversation;

namespace Sovrant.Runtime.Permissions;

/// <summary>
/// Phase 145 — permission mode per conversation for multi-user hosts (Sovrant.Web). A tool call is
/// judged by the mode of the session it runs in (<see cref="SessionContext.Current"/>), which the
/// chat sets from that user's saved mode each turn; calls outside any session use the install
/// default. Setting <see cref="Mode"/> changes the current session only, never everyone's.
/// </summary>
public sealed class SessionAwarePermissionPolicy(PermissionMode defaultMode) : IPermissionPolicy, IPermissionModeAccessor
{
    private volatile PermissionMode _default = defaultMode;

    public PermissionMode Mode
    {
        get => SessionContext.Current?.PermissionMode ?? _default;
        set
        {
            if (SessionContext.Current is { } session)
                session.PermissionMode = value;
            // Outside a session there's nobody to apply it to; the install default stays as configured.
        }
    }

    /// <summary>The install-wide default for work that isn't part of a conversation.</summary>
    public PermissionMode DefaultMode
    {
        get => _default;
        set => _default = value;
    }

    public PolicyDecision Evaluate(string toolName, bool isDestructive) =>
        new ModeAwarePermissionPolicy(Mode).Evaluate(toolName, isDestructive);
}
