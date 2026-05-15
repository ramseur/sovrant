namespace Sovrant.Tools.Todo;

/// <summary>In-session todo list state, persisted across turns for the lifetime of the service.</summary>
public sealed class TodoState
{
    private readonly List<TodoItem> _items = [];
    private readonly object _lock = new();

    public IReadOnlyList<TodoItem> Items
    {
        get { lock (_lock) { return [.. _items]; } }
    }

    public void SetItems(IEnumerable<TodoItem> items)
    {
        lock (_lock)
        {
            _items.Clear();
            _items.AddRange(items);
        }
    }
}

/// <summary>A single todo item.</summary>
public sealed record TodoItem(
    string Id,
    string Content,
    TodoStatus Status,
    TodoPriority Priority);

/// <summary>The completion status of a todo item.</summary>
public enum TodoStatus
{
    /// <summary>Not yet started.</summary>
    Pending,
    /// <summary>Currently in progress.</summary>
    InProgress,
    /// <summary>Completed.</summary>
    Completed,
}

/// <summary>The priority of a todo item.</summary>
public enum TodoPriority
{
    /// <summary>Low priority.</summary>
    Low,
    /// <summary>Normal priority.</summary>
    Medium,
    /// <summary>High priority.</summary>
    High,
}
