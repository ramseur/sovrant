using System.Globalization;
using System.Text;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Phase 51 — CLI surface for the workflow layer.
///
/// Subcommands:
/// <list type="bullet">
///   <item><c>/workflow create &lt;goal&gt;</c> — create a new workflow</item>
///   <item><c>/workflow list [--status &lt;s&gt;]</c> — list workflows</item>
///   <item><c>/workflow show &lt;id&gt;</c> — show workflow details</item>
///   <item><c>/workflow run &lt;id&gt;</c> — drive a workflow forward one engine cycle</item>
///   <item><c>/workflow events &lt;id&gt;</c> — print the event journal</item>
///   <item><c>/workflow export &lt;id&gt; [--json]</c> — export a workflow report</item>
///   <item><c>/workflow cancel &lt;id&gt;</c> — cancel a workflow</item>
/// </list>
/// </summary>
public sealed class WorkflowCommand : ISlashCommand
{
    private readonly IWorkflowStore _store;
    private readonly IWorkflowExecutor _executor;
    private readonly WorkflowExportService _exporter;

    public WorkflowCommand(
        IWorkflowStore store,
        IWorkflowExecutor executor,
        WorkflowExportService exporter)
    {
        _store = store;
        _executor = executor;
        _exporter = exporter;
    }

    public string Name => "workflow";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Manage autonomous workflows (create, list, show, run, events, export, cancel).";
    public string Category => "Advanced";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return new SlashCommandResult(Usage());

        var sub = parts[0];
        var rest = parts[1..];

        if (sub.Equals("create", StringComparison.OrdinalIgnoreCase))
            return await CreateAsync(rest, ct).ConfigureAwait(false);
        if (sub.Equals("list", StringComparison.OrdinalIgnoreCase))
            return await ListAsync(rest, ct).ConfigureAwait(false);
        if (sub.Equals("show", StringComparison.OrdinalIgnoreCase))
            return await ShowAsync(rest, ct).ConfigureAwait(false);
        if (sub.Equals("run", StringComparison.OrdinalIgnoreCase))
            return await RunAsync(rest, ct).ConfigureAwait(false);
        if (sub.Equals("events", StringComparison.OrdinalIgnoreCase))
            return await EventsAsync(rest, ct).ConfigureAwait(false);
        if (sub.Equals("export", StringComparison.OrdinalIgnoreCase))
            return await ExportAsync(rest, ct).ConfigureAwait(false);
        if (sub.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            return await CancelAsync(rest, ct).ConfigureAwait(false);

        return new SlashCommandResult($"Unknown subcommand '{sub}'.\n\n{Usage()}");
    }

    private async Task<SlashCommandResult> CreateAsync(string[] rest, CancellationToken ct)
    {
        var goal = string.Join(' ', rest);
        if (string.IsNullOrWhiteSpace(goal))
            return new SlashCommandResult("Usage: /workflow create <goal>");

        var workflow = await _store.CreateAsync(goal, ct: ct).ConfigureAwait(false);
        return new SlashCommandResult(
            $"Workflow created: `{workflow.Id}`\nGoal: {workflow.Goal}\nStatus: {workflow.Status}");
    }

    private async Task<SlashCommandResult> ListAsync(string[] rest, CancellationToken ct)
    {
        WorkflowStatus? statusFilter = null;
        for (int i = 0; i < rest.Length - 1; i++)
        {
            if (rest[i].Equals("--status", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<WorkflowStatus>(rest[i + 1], ignoreCase: true, out var parsed))
            {
                statusFilter = parsed;
            }
        }

        var workflows = await _store.ListAsync(status: statusFilter, limit: 25, ct: ct).ConfigureAwait(false);
        if (workflows.Count == 0)
            return new SlashCommandResult("No workflows found.");

        var sb = new StringBuilder();
        foreach (var m in workflows)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {m.Status,-15} `{m.Id}`  {Truncate(m.Goal, 50)}");
        }
        return new SlashCommandResult(sb.ToString().TrimEnd());
    }

    private async Task<SlashCommandResult> ShowAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /workflow show <id>");

        var workflow = await _store.GetAsync(rest[0], ct).ConfigureAwait(false);
        if (workflow is null)
            return new SlashCommandResult($"Workflow '{rest[0]}' not found.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Workflow:** `{workflow.Id}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Goal:** {workflow.Goal}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {workflow.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Created:** {workflow.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        if (workflow.CompletedAt is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Completed:** {workflow.CompletedAt.Value:yyyy-MM-dd HH:mm:ss}");
        if (workflow.WorkspaceId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Workspace:** `{workflow.WorkspaceId}`");
        if (workflow.ProjectId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Project:** `{workflow.ProjectId}`");
        return new SlashCommandResult(sb.ToString().TrimEnd());
    }

    private async Task<SlashCommandResult> RunAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /workflow run <id>");

        try
        {
            var workflow = await _executor.RunAsync(rest[0], ct).ConfigureAwait(false);
            return new SlashCommandResult(
                $"Workflow `{workflow.Id}` → {workflow.Status}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
        {
            return new SlashCommandResult($"Error: {ex.Message}");
        }
    }

    private async Task<SlashCommandResult> EventsAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /workflow events <id>");

        var events = await _store.GetEventsAsync(rest[0], ct).ConfigureAwait(false);
        if (events.Count == 0)
            return new SlashCommandResult("No events found for this workflow.");

        var sb = new StringBuilder();
        foreach (var e in events)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {e.Timestamp:HH:mm:ss}  {e.EventType,-24} {Truncate(e.PayloadJson, 50)}");
        }
        return new SlashCommandResult(sb.ToString().TrimEnd());
    }

    private async Task<SlashCommandResult> ExportAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /workflow export <id> [--json]");

        var workflowId = rest[0];
        var useJson = rest.Length > 1
            && rest[1].Equals("--json", StringComparison.OrdinalIgnoreCase);

        try
        {
            var report = useJson
                ? await _exporter.ExportJsonAsync(workflowId, ct).ConfigureAwait(false)
                : await _exporter.ExportMarkdownAsync(workflowId, ct).ConfigureAwait(false);
            return new SlashCommandResult(report);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
        {
            return new SlashCommandResult($"Error: {ex.Message}");
        }
    }

    private async Task<SlashCommandResult> CancelAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /workflow cancel <id>");

        var workflow = await _store.GetAsync(rest[0], ct).ConfigureAwait(false);
        if (workflow is null)
            return new SlashCommandResult($"Workflow '{rest[0]}' not found.");

        if (workflow.Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
            return new SlashCommandResult($"Workflow already in terminal state: {workflow.Status}");

        await _store.AppendEventAsync(
            workflow.Id, WorkflowEventTypes.Cancelled, "{}", ct: ct).ConfigureAwait(false);
        await _store.UpdateStateAsync(
            workflow.Id, WorkflowStatus.Cancelled,
            completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);

        return new SlashCommandResult($"Workflow `{workflow.Id}` cancelled.");
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");
    }

    private static string Usage() =>
        """
        Usage: /workflow <subcommand> [args]

        Subcommands:
          create <goal>           Create a new workflow
          list [--status <s>]     List workflows
          show <id>               Show workflow details
          run <id>                Drive a workflow forward one engine cycle
          events <id>             Print the event journal
          export <id> [--json]    Export a workflow report (Markdown or JSON)
          cancel <id>             Cancel a running workflow
        """;
}
