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
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"ID",-10} {"Status",-12} {"Started",-20} {"Description"}");
        sb.AppendLine(new string('-', 65));

        foreach (var t in all)
        {
            var desc = t.Description;
            if (desc.Length > 30)
                desc = string.Concat(desc.AsSpan(0, 27), "...");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{t.Id,-10} {t.Status,-12} {t.StartedAt:HH:mm:ss,-20} {desc}");
        }

        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"{all.Count} tasks. Use /tasks <id> for details.");

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowTask(string id)
    {
        if (!_tasks.TryGet(id, out var t) || t is null)
            return new SlashCommandResult($"Task '{id}' not found. Use /tasks to list all.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"ID:          {t.Id}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Status:      {t.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Command:     {t.Command}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Description: {t.Description}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Started:     {t.StartedAt:u}");
        if (t.CompletedAt.HasValue)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Completed:   {t.CompletedAt:u}");
        if (t.ExitCode.HasValue)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Exit Code:   {t.ExitCode}");

        var output = t.OutputBuffer.ToString();
        if (!string.IsNullOrEmpty(output))
        {
            sb.AppendLine();
            sb.AppendLine("Output:");
            if (output.Length > 500)
                output = string.Concat(output.AsSpan(0, 497), "...");
            sb.Append(output);
        }

        return new SlashCommandResult(sb.ToString());
    }
}
