namespace Sovrant.Tools;

/// <summary>
/// Phase 43 — tracks the logical current working directory for shell tools
/// (<see cref="Core.BashTool"/> in particular) so that <c>cd</c> issued in
/// one invocation persists into the next. Without this, every BashTool call
/// spawned a fresh process inheriting Sovrant's launch cwd, and <c>cd /foo</c>
/// died with the child.
///
/// Currently a process-wide singleton — there is no per-conversation session
/// plumbing through to tools yet. When multi-session support lands, this can
/// grow a keyed store; for now the "one interactive shell per process" model
/// matches how a human runs a terminal.
///
/// Thread-safe: the getter/setter are guarded by a simple lock because tool
/// executions on the same conversation are sequential but nothing in the
/// type system enforces that.
/// </summary>
public sealed class ShellSessionState
{
    private readonly object _gate = new();
    private string _currentDirectory;

    public ShellSessionState()
    {
        _currentDirectory = Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// The logical cwd for shell tools. Initialized to the process cwd at
    /// construction; updated by BashTool after each command runs.
    /// </summary>
    public string CurrentDirectory
    {
        get { lock (_gate) return _currentDirectory; }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            lock (_gate) _currentDirectory = value;
        }
    }
}
