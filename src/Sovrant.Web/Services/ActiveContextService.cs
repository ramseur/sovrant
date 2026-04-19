namespace Sovrant.Web.Services;

/// <summary>
/// Shared state for the active workspace, project, model, and provider.
/// Registered as a singleton (one Blazor Server circuit shares a single instance).
/// Components subscribe to <see cref="OnChanged"/> to refresh when context changes.
/// </summary>
public sealed class ActiveContextService
{
    public string WorkspaceId { get; private set; } = string.Empty;
    public string WorkspaceName { get; private set; } = "Personal";
    public string ProjectId { get; private set; } = string.Empty;
    public string ProjectName { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;

    /// <summary>Fires whenever any context property changes.</summary>
    public event Action? OnChanged;

    /// <summary>Fires when workspaces or projects are created/deleted and lists need refreshing.</summary>
    public event Action? OnDataChanged;

    /// <summary>Fires when a session should be resumed in the chat page.</summary>
    public event Action<string>? OnSessionResume;

    /// <summary>
    /// Session id set by <see cref="ResumeSession"/> so the Chat page can pick
    /// it up even when it hasn't been mounted yet (first click from another page).
    /// Chat clears this after consuming.
    /// </summary>
    public string? PendingResumeSessionId { get; set; }

    public void SetWorkspace(string id, string name)
    {
        WorkspaceId = id;
        WorkspaceName = name;
        // Clear project when workspace changes
        ProjectId = string.Empty;
        ProjectName = string.Empty;
        OnChanged?.Invoke();
    }

    public void SetProject(string id, string name)
    {
        ProjectId = id;
        ProjectName = name;
        OnChanged?.Invoke();
    }

    public void SetModel(string model, string provider)
    {
        Model = model;
        Provider = provider;
        OnChanged?.Invoke();
    }

    /// <summary>Call after creating/deleting workspaces or projects to refresh sidebar lists.</summary>
    public void NotifyDataChanged() => OnDataChanged?.Invoke();

    /// <summary>Request the chat page to resume the given session.</summary>
    public void ResumeSession(string sessionId)
    {
        PendingResumeSessionId = sessionId;
        OnSessionResume?.Invoke(sessionId);
    }
}
