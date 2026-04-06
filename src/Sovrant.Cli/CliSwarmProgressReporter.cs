using System.Globalization;
using Sovrant.Agents.Swarm;
using Sovrant.Tools.Swarm;
using Spectre.Console;

namespace Sovrant.Cli;

/// <summary>
/// Renders swarm progress events to the console in real time so the user
/// can see task-by-task progress during a swarm run.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance", "CA1812", Justification = "Instantiated via DI.")]
internal sealed class CliSwarmProgressReporter : ISwarmProgressReporter
{
    public void Report(SwarmEvent evt)
    {
        switch (evt)
        {
            case SwarmEvent.PlanCreated pc:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [teal bold]Swarm:[/] {pc.TaskCount} tasks across {pc.WaveCount} waves"));
                break;

            case SwarmEvent.TaskStarted ts:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [blue]\u25b6[/] {Markup.Escape(ts.TaskId)} [grey]\u2192 {Markup.Escape(ts.AgentName)} (wave {ts.Wave})[/]"));
                break;

            case SwarmEvent.TaskCompleted tc:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [green]\u2713[/] {Markup.Escape(tc.TaskId)} [grey]({tc.TokensUsed} tokens)[/]"));
                break;

            case SwarmEvent.TaskFailed tf:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [red]\u2717[/] {Markup.Escape(tf.TaskId)} [grey](attempt {tf.RetryCount})[/]: {Markup.Escape(tf.Error)}"));
                break;

            case SwarmEvent.FileConflict fc:
                AnsiConsole.MarkupLine(
                    $"  [yellow]\u26a0[/] File conflict: {Markup.Escape(fc.FilePath)} claimed by {Markup.Escape(fc.HeldByTaskId)}");
                break;

            case SwarmEvent.BudgetExceeded be:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [red bold]Budget exceeded:[/] {be.Used}/{be.Limit} tokens"));
                break;

            case SwarmEvent.SwarmCompleted sc:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [teal bold]Swarm {sc.FinalStatus}[/] — {sc.TotalTokens} tokens, {sc.DurationSeconds:F1}s"));
                break;
        }
    }
}
