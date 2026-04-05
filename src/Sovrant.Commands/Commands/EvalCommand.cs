using System.Globalization;
using System.Text;
using Sovrant.Runtime.Evals;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Runs eval suites from <c>.sovrant/evals/</c> and displays results.
/// Usage: <c>/eval [suite-name] [--history] [--tag tag]</c>
/// </summary>
public sealed class EvalCommand : ISlashCommand
{
    private readonly IEvalRunner _runner;
    private readonly EvalResultStore _resultStore;

    public EvalCommand(IEvalRunner runner, EvalResultStore resultStore)
    {
        _runner = runner;
        _resultStore = resultStore;
    }

    public string Name => "eval";
    public IReadOnlyList<string> Aliases => ["evals"];
    public string Description => "Run eval suites or view eval history.";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // /eval --history [suite-name]
        if (parts.Any(p => p.Equals("--history", StringComparison.OrdinalIgnoreCase)))
        {
            var suiteName = parts.FirstOrDefault(p => !p.StartsWith('-'));
            return new SlashCommandResult(FormatHistory(suiteName));
        }

        // Load all evals
        var searchPaths = GetSearchPaths();
        var suites = EvalLoader.LoadAll(searchPaths);

        if (suites.Count == 0)
            return new SlashCommandResult("No eval suites found in .sovrant/evals/ or ~/.sovrant/evals/.\n" +
                "Create a JSON file with eval definitions to get started.");

        // Filter by tag if requested
        var tagFilter = GetArgValue(parts, "--tag");

        // Filter by suite name if specified
        var suiteName2 = parts.FirstOrDefault(p => !p.StartsWith('-'));
        if (!string.IsNullOrEmpty(suiteName2))
        {
            suites = suites.Where(s => s.Name.Equals(suiteName2, StringComparison.OrdinalIgnoreCase)).ToList();
            if (suites.Count == 0)
                return new SlashCommandResult($"No eval suite found with name: {suiteName2}");
        }

        // /eval (no args or just suite name) — list available suites
        if (parts.Length == 0 || (parts.Length == 1 && suites.Count > 1))
        {
            return new SlashCommandResult(FormatSuiteList(suites));
        }

        // Run the suite(s)
        var sb = new StringBuilder();
        foreach (var suite in suites)
        {
            // Apply tag filter
            var filteredSuite = suite;
            if (!string.IsNullOrEmpty(tagFilter))
            {
                var filteredEvals = suite.Evals
                    .Where(e => e.Tags.Any(t => t.Equals(tagFilter, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (filteredEvals.Count == 0)
                    continue;
                filteredSuite = new EvalSuite { Name = suite.Name, Description = suite.Description, Evals = filteredEvals, Tags = suite.Tags };
            }

            var report = await _runner.RunAsync(filteredSuite, ct).ConfigureAwait(false);
            sb.Append(report.ToReport());
            sb.AppendLine();
        }

        return new SlashCommandResult(sb.Length > 0 ? sb.ToString().TrimEnd() : "No evals matched the filter.");
    }

    private static IReadOnlyList<string> GetSearchPaths()
    {
        var projectLocal = Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "evals");
        var global = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "evals");
        return [projectLocal, global];
    }

    private string FormatHistory(string? suiteName)
    {
        if (string.IsNullOrEmpty(suiteName))
            return "Usage: /eval --history <suite-name>";

        var history = _resultStore.LoadHistory(suiteName);
        if (history.Count == 0)
            return $"No history found for suite: {suiteName}";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Eval History: {suiteName}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"Date",-20} {"pass@1",-8} {"pass@k",-8} {"Pass",-6} {"Fail",-6} {"Skip",-6} {"Duration"}");
        sb.AppendLine(new string('-', 70));

        foreach (var entry in history)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{entry.StartedAt:yyyy-MM-dd HH:mm,-20} {entry.PassAt1Rate:P0,-8} {entry.PassRate:P0,-8} {entry.TotalPassed,-6} {entry.TotalFailed,-6} {entry.TotalSkipped,-6} {entry.DurationSeconds:F1}s");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatSuiteList(IReadOnlyList<EvalSuite> suites)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Available Eval Suites");
        sb.AppendLine();
        foreach (var suite in suites)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {suite.Name} — {suite.Evals.Count} evals{(suite.Description is not null ? $" — {suite.Description}" : "")}");
        }
        sb.AppendLine();
        sb.AppendLine("Run: /eval <suite-name>  |  History: /eval --history <suite-name>");
        return sb.ToString().TrimEnd();
    }

    private static string? GetArgValue(string[] parts, string flag)
    {
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }
        return null;
    }
}
