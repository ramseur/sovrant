namespace Sovrant.Runtime.Hooks;

/// <summary>No-op hook runner used when no hooks are configured or in unit tests.</summary>
internal sealed class NullHookRunner : IHookRunner
{
    public static readonly NullHookRunner Instance = new();

    public Task<bool> RunAsync(HookEvent hookEvent, HookContext context, CancellationToken ct = default)
        => Task.FromResult(true);
}
