using System.Globalization;
using System.Text;
using Sovrant.Api.Routing;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Shows the health of all configured LLM providers, or pins routing to a specific one.
/// </summary>
public sealed class ProviderCommand : ISlashCommand
{
    private readonly ISmartRouter _router;

    public ProviderCommand(ISmartRouter router) => _router = router;

    public string Name => "provider";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show provider health (/provider), or pin to one (/provider <name>).";
    public string Category => "Config";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var name = args.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            try
            {
                await _router.PinProviderAsync(name, ct).ConfigureAwait(false);
                return new SlashCommandResult($"Pinned to provider: {name}. Use /provider without arguments to unpin.");
            }
            catch (InvalidOperationException ex)
            {
                return new SlashCommandResult(ex.Message);
            }
        }

        // No argument — show provider health table.
        var statuses = _router.GetStatus();
        if (statuses.Count == 0)
            return new SlashCommandResult("No providers configured.");

        var sb = new StringBuilder();
        sb.AppendLine("# Provider Health");
        sb.AppendLine();
        sb.AppendLine("| Provider | Healthy | Latency | Requests | Errors | Score |");
        sb.AppendLine("|----------|---------|---------|----------|--------|-------|");

        foreach (var s in statuses)
        {
            var healthy = s.Healthy ? "Yes" : "**NO**";
            var latency = s.RequestCount > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{s.LatencyMs:F0}ms")
                : "--";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {s.Name} | {healthy} | {latency} | {s.RequestCount} | {s.ErrorCount} | {s.Score} |");
        }

        return new SlashCommandResult(sb.ToString().TrimEnd());
    }
}
