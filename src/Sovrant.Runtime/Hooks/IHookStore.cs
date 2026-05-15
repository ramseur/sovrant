namespace Sovrant.Runtime.Hooks;

/// <summary>
/// Persistence for <see cref="HookConfig"/> entries. Replaces the legacy
/// <c>.sovrant/hooks.json</c> loader; all reads/writes go through SQLite so
/// the Web/Desktop UI can edit hooks at runtime.
/// </summary>
public interface IHookStore
{
    /// <summary>Returns every enabled hook, in deterministic order by hook_id.</summary>
    Task<IReadOnlyList<HookConfig>> GetEnabledAsync(CancellationToken ct = default);

    /// <summary>Returns every hook (enabled and disabled) for management UIs.</summary>
    Task<IReadOnlyList<HookConfig>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Inserts or updates a hook keyed on <see cref="HookConfig.Id"/>.</summary>
    Task UpsertAsync(HookConfig hook, CancellationToken ct = default);

    /// <summary>Deletes the hook with the given id. No-op if absent.</summary>
    Task DeleteAsync(string hookId, CancellationToken ct = default);
}
