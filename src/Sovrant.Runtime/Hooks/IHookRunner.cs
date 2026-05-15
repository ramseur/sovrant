namespace Sovrant.Runtime.Hooks;

/// <summary>Fires registered hooks at agent lifecycle events.</summary>
public interface IHookRunner
{
    /// <summary>
    /// Fires all matching hooks for <paramref name="hookEvent"/>.
    /// <para>
    /// For <see cref="HookEvent.PreToolUse"/> in the <c>strict</c> profile, returns
    /// <c>false</c> if any hook exits with a non-zero code — the tool is then aborted.
    /// All other events always return <c>true</c>.
    /// </para>
    /// </summary>
    Task<bool> RunAsync(HookEvent hookEvent, HookContext context, CancellationToken ct = default);
}
