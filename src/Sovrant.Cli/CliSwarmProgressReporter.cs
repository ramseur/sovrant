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
                    $"  [blue]\u25b6[/] [bold]{Markup.Escape(ts.AgentName)}[/] [grey]{Markup.Escape(Truncate(ts.TaskDescription, 80))} (wave {ts.Wave})[/]"));
                break;

            case SwarmEvent.TaskCompleted tc:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [green]\u2713[/] [bold]{Markup.Escape(tc.AgentName)}[/] [grey]{Markup.Escape(Truncate(tc.TaskDescription, 60))} ({tc.TokensUsed} tokens)[/]"));
                break;

            case SwarmEvent.TaskFailed tf:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [red]\u2717[/] [bold]{Markup.Escape(tf.AgentName)}[/] [grey]{Markup.Escape(Truncate(tf.TaskDescription, 60))} (attempt {tf.RetryCount})[/]: {Markup.Escape(tf.Error)}"));
                break;

            case SwarmEvent.FileConflict fc:
                AnsiConsole.MarkupLine(
                    $"  [yellow]\u26a0[/] {Markup.Escape(fc.AgentName)} blocked — [grey]{Markup.Escape(fc.FilePath)} locked by {Markup.Escape(fc.HeldByAgentName)}[/]");
                break;

            case SwarmEvent.BudgetExceeded be:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [red bold]Budget exceeded:[/] {be.Used}/{be.Limit} tokens"));
                break;

            case SwarmEvent.SwarmCompleted sc:
                AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [teal bold]Swarm {Markup.Escape(sc.FinalStatus.ToString())}[/] — {sc.TotalTokens} tokens, {sc.DurationSeconds:F1}s"));
                break;
        }
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : string.Concat(text.AsSpan(0, maxLen - 3), "...");
}
