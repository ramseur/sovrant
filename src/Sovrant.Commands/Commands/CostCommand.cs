using System.Globalization;
using System.Text;
using Sovrant.Runtime.Metrics;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Shows accumulated token usage and estimated cost for the current session.
/// When <c>/cost status</c> is used, shows pricing snapshot info and budget status.
/// When Phase 55 cost tracking is active, uses live OpenRouter pricing.
/// Falls back to hardcoded estimates when cost tracking is disabled.
/// </summary>
public sealed class CostCommand : ISlashCommand
{
    // Fallback pricing when ICostModel is not available (USD per 1M tokens).
    private const double FallbackInputPricePerMToken = 3.0;
    private const double FallbackOutputPricePerMToken = 15.0;

    private readonly TokenUsageTracker _tracker;
    private readonly ICostModel? _costModel;
    private readonly CostDashboardService? _dashboard;
    private readonly BudgetEnforcer? _budgetEnforcer;

    public CostCommand(
        TokenUsageTracker tracker,
        ICostModel? costModel = null,
        CostDashboardService? dashboard = null,
        BudgetEnforcer? budgetEnforcer = null)
    {
        _tracker = tracker;
        _costModel = costModel;
        _dashboard = dashboard;
        _budgetEnforcer = budgetEnforcer;
    }

    public string Name => "cost";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show accumulated token usage and estimated cost.";
    public string Category => "Config";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var subcommand = args.Trim().ToUpperInvariant();

        return Task.FromResult(subcommand switch
        {
            "STATUS" => ExecuteStatus(),
            "WEEKLY" => ExecuteRange(CostRange.Weekly),
            "MONTHLY" => ExecuteRange(CostRange.Monthly),
            "ALL" => ExecuteRange(CostRange.All),
            _ => ExecuteSession()
        });
    }

    private SlashCommandResult ExecuteSession()
    {
        var input = _tracker.InputTokens;
        var output = _tracker.OutputTokens;
        var total = input + output;

        var sb = new StringBuilder();
        sb.AppendLine("# Token Usage (Current Session)");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Input tokens | {input:N0} |"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Output tokens | {output:N0} |"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Total | {total:N0} |"));
        sb.AppendLine();

        if (_costModel is not null and not NullCostModel)
        {
            // Use live pricing — the accumulated cost comes from the JSONL log via dashboard
            if (_dashboard is not null)
            {
                var summary = _dashboard.GetSummary(CostRange.All);
                sb.Append(string.Create(CultureInfo.InvariantCulture,
                    $"**Estimated cost: ${summary.TotalEstimatedUsd:F4} USD** (via OpenRouter pricing)"));
            }
        }
        else
        {
            var estimatedCost = (input / 1_000_000.0 * FallbackInputPricePerMToken)
                              + (output / 1_000_000.0 * FallbackOutputPricePerMToken);
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"*Estimated cost: ~${estimatedCost:F4} USD (indicative only — actual billing depends on your provider)*"));
        }

        if (_budgetEnforcer?.IsEnabled == true)
        {
            sb.AppendLine();
            sb.AppendLine();
            if (_budgetEnforcer.SessionBudgetUsd.HasValue)
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Session budget: ${_budgetEnforcer.SessionBudgetUsd:F2}"));
            if (_budgetEnforcer.ProjectBudgetUsd.HasValue)
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Project budget: ${_budgetEnforcer.ProjectBudgetUsd:F2} (spent: ${_budgetEnforcer.ProjectSpend:F4})"));
        }

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ExecuteStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cost Tracking Status");
        sb.AppendLine();

        if (_costModel is null or NullCostModel)
        {
            sb.AppendLine("Cost tracking is **disabled** (`SOVRANT_COST_PROVIDER=none`).");
            return new SlashCommandResult(sb.ToString());
        }

        sb.AppendLine("Cost provider: **OpenRouter**");

        if (_budgetEnforcer is not null)
        {
            sb.AppendLine();
            sb.AppendLine("| Budget | Limit | Spent |");
            sb.AppendLine("|--------|-------|-------|");
            if (_budgetEnforcer.SessionBudgetUsd.HasValue)
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"| Session | ${_budgetEnforcer.SessionBudgetUsd:F2} | — |"));
            if (_budgetEnforcer.ProjectBudgetUsd.HasValue)
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"| Project | ${_budgetEnforcer.ProjectBudgetUsd:F2} | ${_budgetEnforcer.ProjectSpend:F4} |"));
        }

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ExecuteRange(CostRange range)
    {
        if (_dashboard is null)
            return new SlashCommandResult("Cost tracking is disabled.");

        var summary = _dashboard.GetSummary(range);
        var sb = new StringBuilder();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"# Cost Summary ({range})"));
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"| Turns | {summary.TurnCount:N0} |"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"| Input tokens | {summary.TotalInputTokens:N0} |"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"| Output tokens | {summary.TotalOutputTokens:N0} |"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"| Estimated cost | ${summary.TotalEstimatedUsd:F4} |"));

        if (summary.ByModels.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### By Model");
            sb.AppendLine("| Model | Turns | Cost |");
            sb.AppendLine("|-------|-------|------|");
            foreach (var m in summary.ByModels)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"| {m.Model} | {m.TurnCount} | ${m.EstimatedUsd:F4} |"));
            }
        }

        return new SlashCommandResult(sb.ToString());
    }
}
