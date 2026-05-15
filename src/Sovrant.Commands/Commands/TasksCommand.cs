using System.Globalization;
using System.Text;
using Sovrant.Tools.Tasks;

namespace Sovrant.Commands.Commands;

/// <summary>Lists background tasks and their status.</summary>
public sealed class TasksCommand : ISlashCommand
{
    private readonly BackgroundTaskRegistry _tasks;

    public TasksCommand(BackgroundTaskRegistry tasks) => _tasks = tasks;

    public string Name => "tasks";
    public IReadOnlyList<string> Aliases => ["task"];
    public string Description => "List background tasks and their status.";
    public string Category => "Advanced";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(args))
            return Task.FromResult(ShowTask(args.Trim()));

        return Task.FromResult(ListTasks());
    }

    private SlashCommandResult ListTasks()
    {
        var all = _tasks.All;
        if (all.Count == 0)
            return new SlashCommandResult("No background tasks.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Background Tasks ({all.Count})");
        sb.AppendLine();
        sb.AppendLine("| ID | Status | Started | Description |");
        sb.AppendLine("|----|--------|---------|-------------|");

        foreach (var t in all)
        {
            var desc = t.Description;
            if (desc.Length > 40)
                desc = string.Concat(desc.AsSpan(0, 37), "...");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {t.Id} | {t.Status} | {t.StartedAt:HH:mm:ss} | {desc} |");
        }

        sb.AppendLine();
        sb.Append("Use /tasks (id) for details.");

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowTask(string id)
    {
        if (!_tasks.TryGet(id, out var t) || t is null)
            return new SlashCommandResult($"Task '{id}' not found. Use /tasks to list all.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Task: {t.Id}");
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Status | {t.Status} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Command | {t.Command} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Description | {t.Description} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Started | {t.StartedAt:u} |");
        if (t.CompletedAt.HasValue)
            sb.AppendLine(CultureInfo.InvariantCulture, $"| Completed | {t.CompletedAt:u} |");
        if (t.ExitCode.HasValue)
            sb.AppendLine(CultureInfo.InvariantCulture, $"| Exit Code | {t.ExitCode} |");

        var output = t.OutputBuffer.ToString();
        if (!string.IsNullOrEmpty(output))
        {
            sb.AppendLine();
            sb.AppendLine("**Output:**");
            sb.AppendLine();
            if (output.Length > 500)
                output = string.Concat(output.AsSpan(0, 497), "...");
            sb.Append(output);
        }

        return new SlashCommandResult(sb.ToString());
    }
}
