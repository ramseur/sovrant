using System.Globalization;
using System.Text;

namespace Sovrant.Commands.Commands;

/// <summary>Shows accumulated token usage and an estimated cost for the current session.</summary>
public sealed class CostCommand : ISlashCommand
{
    // Approximate pricing for a typical frontier model (USD per 1M tokens).
    // The REPL can override this by configuring a different rate.
    private const double InputPricePerMToken = 3.0;
    private const double OutputPricePerMToken = 15.0;

    private readonly TokenUsageTracker _tracker;

    public CostCommand(TokenUsageTracker tracker) => _tracker = tracker;

    public string Name => "cost";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show accumulated token usage and estimated cost.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var input = _tracker.InputTokens;
        var output = _tracker.OutputTokens;
        var total = input + output;

        var estimatedCost = (input / 1_000_000.0 * InputPricePerMToken)
                          + (output / 1_000_000.0 * OutputPricePerMToken);

        var sb = new StringBuilder();
        sb.AppendLine("Token usage this session:");
        sb.AppendLine();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  Input tokens:   {input,12:N0}"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  Output tokens:  {output,12:N0}"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  Total:          {total,12:N0}"));
        sb.AppendLine();
        sb.Append(string.Create(CultureInfo.InvariantCulture,
            $"  Estimated cost: ~${estimatedCost:F4} USD  (indicative only — actual billing depends on your provider)"));

        return Task.FromResult(new SlashCommandResult(sb.ToString()));
    }
}
