using System.Globalization;
using System.Text;
using Sovrant.Api.Routing;

namespace Sovrant.Commands.Commands;

/// <summary>Shows the health and routing statistics for all configured LLM providers.</summary>
public sealed class StatusCommand : ISlashCommand
{
    private readonly ISmartRouter _router;

    public StatusCommand(ISmartRouter router) => _router = router;

    public string Name => "status";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show provider health and routing statistics.";
    public string Category => "Config";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var statuses = _router.GetStatus();
        if (statuses.Count == 0)
            return Task.FromResult(new SlashCommandResult("No providers configured."));

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"Provider",-20} {"Healthy",-8} {"Latency",-10} {"Reqs",-6} {"Errors",-7} {"Score"}");
        sb.AppendLine(new string('-', 62));

        foreach (var s in statuses)
        {
            var healthy = s.Healthy ? "yes" : "NO";
            var latency = s.RequestCount > 0 ? string.Create(CultureInfo.InvariantCulture, $"{s.LatencyMs:F0}ms") : "—";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{s.Name,-20} {healthy,-8} {latency,-10} {s.RequestCount,-6} {s.ErrorCount,-7} {s.Score}");
        }

        return Task.FromResult(new SlashCommandResult(sb.ToString().TrimEnd()));
    }
}
