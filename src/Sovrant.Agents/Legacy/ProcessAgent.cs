using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Legacy;

/// <summary>
/// An <see cref="IAgent"/> backed by a child process. Tasks are written as JSON to the
/// process stdin; structured results (including tool-use messages) are streamed from stdout.
/// Tool-use parsing follows the same message format as the original OpenClaude agent protocol.
/// </summary>
public sealed class ProcessAgent : IAgent
{
    private readonly ProcessStartInfo _startInfo;
    private readonly ILogger<ProcessAgent> _logger;

    /// <param name="name">Unique agent name used for routing.</param>
    /// <param name="startInfo">
    /// Start info for the agent process. <c>RedirectStandardInput</c>,
    /// <c>RedirectStandardOutput</c>, and <c>RedirectStandardError</c> are set automatically.
    /// </param>
    /// <param name="logger">Logger for process lifecycle and parsing events.</param>
    public ProcessAgent(string name, ProcessStartInfo startInfo, ILogger<ProcessAgent> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(logger);

        Name = name;
        _startInfo = startInfo;
        _logger = logger;

        // Ensure the process communicates via redirected stdio, not an attached console.
        _startInfo.RedirectStandardInput = true;
        _startInfo.RedirectStandardOutput = true;
        _startInfo.RedirectStandardError = true;
        _startInfo.UseShellExecute = false;
        _startInfo.CreateNoWindow = true;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// TODO (Phase 19): Spawn process from <c>_startInfo</c>; write the task as JSON to stdin;
    /// stream stdout line-by-line; parse structured tool-use blocks from each line;
    /// propagate <paramref name="ct"/> to kill the process on cancellation.
    /// </remarks>
    public Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        throw new NotImplementedException(
            "ProcessAgent execution is not yet implemented. See Phase 19 roadmap.");
    }
}
