using System.Globalization;
using System.Text;
using Sovrant.Runtime.Missions;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Phase 51 — CLI surface for the mission layer.
///
/// Subcommands:
/// <list type="bullet">
///   <item><c>/mission create &lt;goal&gt;</c> — create a new mission</item>
///   <item><c>/mission list [--status &lt;s&gt;]</c> — list missions</item>
///   <item><c>/mission show &lt;id&gt;</c> — show mission details</item>
///   <item><c>/mission run &lt;id&gt;</c> — drive a mission forward one engine cycle</item>
///   <item><c>/mission events &lt;id&gt;</c> — print the event journal</item>
///   <item><c>/mission export &lt;id&gt; [--json]</c> — export a mission report</item>
///   <item><c>/mission cancel &lt;id&gt;</c> — cancel a mission</item>
/// </list>
/// </summary>
public sealed class MissionCommand : ISlashCommand
{
    private readonly IMissionStore _store;
    private readonly IMissionExecutor _executor;
    private readonly MissionExportService _exporter;

    public MissionCommand(
        IMissionStore store,
        IMissionExecutor executor,
        MissionExportService exporter)
    {
        _store = store;
        _executor = executor;
        _exporter = exporter;
    }

    public string Name => "mission";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Manage autonomous missions (create, list, show, run, events, export, cancel).";
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
            return new SlashCommandResult("Usage: /mission create <goal>");

        var mission = await _store.CreateAsync(goal, ct: ct).ConfigureAwait(false);
        return new SlashCommandResult(
            $"Mission created: `{mission.Id}`\nGoal: {mission.Goal}\nStatus: {mission.Status}");
    }

    private async Task<SlashCommandResult> ListAsync(string[] rest, CancellationToken ct)
    {
        MissionStatus? statusFilter = null;
        for (int i = 0; i < rest.Length - 1; i++)
        {
            if (rest[i].Equals("--status", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<MissionStatus>(rest[i + 1], ignoreCase: true, out var parsed))
            {
                statusFilter = parsed;
            }
        }

        var missions = await _store.ListAsync(status: statusFilter, limit: 25, ct: ct).ConfigureAwait(false);
        if (missions.Count == 0)
            return new SlashCommandResult("No missions found.");

        var sb = new StringBuilder();
        foreach (var m in missions)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {m.Status,-15} `{m.Id}`  {Truncate(m.Goal, 50)}");
        }
        return new SlashCommandResult(sb.ToString().TrimEnd());
    }

    private async Task<SlashCommandResult> ShowAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /mission show <id>");

        var mission = await _store.GetAsync(rest[0], ct).ConfigureAwait(false);
        if (mission is null)
            return new SlashCommandResult($"Mission '{rest[0]}' not found.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Mission:** `{mission.Id}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Goal:** {mission.Goal}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {mission.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Created:** {mission.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        if (mission.CompletedAt is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Completed:** {mission.CompletedAt.Value:yyyy-MM-dd HH:mm:ss}");
        if (mission.WorkspaceId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Workspace:** `{mission.WorkspaceId}`");
        if (mission.ProjectId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Project:** `{mission.ProjectId}`");
        return new SlashCommandResult(sb.ToString().TrimEnd());
    }

    private async Task<SlashCommandResult> RunAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /mission run <id>");

        try
        {
            var mission = await _executor.RunAsync(rest[0], ct).ConfigureAwait(false);
            return new SlashCommandResult(
                $"Mission `{mission.Id}` → {mission.Status}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
        {
            return new SlashCommandResult($"Error: {ex.Message}");
        }
    }

    private async Task<SlashCommandResult> EventsAsync(string[] rest, CancellationToken ct)
    {
        if (rest.Length == 0)
            return new SlashCommandResult("Usage: /mission events <id>");

        var events = await _store.GetEventsAsync(rest[0], ct).ConfigureAwait(false);
        if (events.Count == 0)
            return new SlashCommandResult("No events found for this mission.");

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
            return new SlashCommandResult("Usage: /mission export <id> [--json]");

        var missionId = rest[0];
        var useJson = rest.Length > 1
            && rest[1].Equals("--json", StringComparison.OrdinalIgnoreCase);

        try
        {
            var report = useJson
                ? await _exporter.ExportJsonAsync(missionId, ct).ConfigureAwait(false)
                : await _exporter.ExportMarkdownAsync(missionId, ct).ConfigureAwait(false);
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
            return new SlashCommandResult("Usage: /mission cancel <id>");

        var mission = await _store.GetAsync(rest[0], ct).ConfigureAwait(false);
        if (mission is null)
            return new SlashCommandResult($"Mission '{rest[0]}' not found.");

        if (mission.Status is MissionStatus.Completed or MissionStatus.Failed or MissionStatus.Cancelled)
            return new SlashCommandResult($"Mission already in terminal state: {mission.Status}");

        await _store.AppendEventAsync(
            mission.Id, MissionEventTypes.Cancelled, "{}", ct: ct).ConfigureAwait(false);
        await _store.UpdateStateAsync(
            mission.Id, MissionStatus.Cancelled,
            completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);

        return new SlashCommandResult($"Mission `{mission.Id}` cancelled.");
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");
    }

    private static string Usage() =>
        """
        Usage: /mission <subcommand> [args]

        Subcommands:
          create <goal>           Create a new mission
          list [--status <s>]     List missions
          show <id>               Show mission details
          run <id>                Drive a mission forward one engine cycle
          events <id>             Print the event journal
          export <id> [--json]    Export a mission report (Markdown or JSON)
          cancel <id>             Cancel a running mission
        """;
}
