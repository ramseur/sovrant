using System.Text.Json.Serialization;
using Sovrant.Runtime.Evals;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/evals</c> and <c>POST /v1/evals/run</c>.</summary>
internal static class EvalRoutes
{
    public static void Map(WebApplication app)
    {
        // GET /v1/evals — list available suites
        app.MapGet("/v1/evals", () =>
        {
            var searchPaths = GetSearchPaths();
            var suites = EvalLoader.LoadAll(searchPaths);

            var response = suites.Select(s => new EvalSuiteDto
            {
                Name = s.Name,
                Description = s.Description,
                EvalCount = s.Evals.Count,
                Tags = s.Tags.ToList(),
            }).ToList();

            return Results.Ok(response);
        });

        // GET /v1/evals/{suiteName}/history — get trend data
        app.MapGet("/v1/evals/{suiteName}/history", (string suiteName, IEvalResultStore resultStore) =>
        {
            var history = resultStore.LoadHistory(suiteName);
            return Results.Ok(history);
        });

        // POST /v1/evals/run — run a named suite and return the report
        app.MapPost("/v1/evals/run", async (
            EvalRunRequest request,
            IEvalRunner runner,
            CancellationToken ct) =>
        {
            var searchPaths = GetSearchPaths();
            var suites = EvalLoader.LoadAll(searchPaths);
            var suite = suites.FirstOrDefault(s =>
                s.Name.Equals(request.SuiteName, StringComparison.OrdinalIgnoreCase));

            if (suite is null)
                return Results.NotFound(new { error = $"Suite not found: {request.SuiteName}" });

            // Apply tag filter if specified
            if (!string.IsNullOrEmpty(request.Tag))
            {
                var filtered = suite.Evals
                    .Where(e => e.Tags.Any(t => t.Equals(request.Tag, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (filtered.Count == 0)
                    return Results.NotFound(new { error = $"No evals in suite match tag: {request.Tag}" });

                suite = new EvalSuite { Name = suite.Name, Description = suite.Description, Evals = filtered, Tags = suite.Tags };
            }

            var report = await runner.RunAsync(suite, ct);

            return Results.Ok(new EvalRunResponse
            {
                SuiteName = report.SuiteName,
                StartedAt = report.StartedAt,
                DurationSeconds = report.Duration.TotalSeconds,
                PassRate = report.PassRate,
                PassAt1Rate = report.PassAt1Rate,
                TotalPassed = report.TotalPassed,
                TotalFailed = report.TotalFailed,
                TotalSkipped = report.TotalSkipped,
                Results = report.Results.Select(r => new EvalResultResponseDto
                {
                    EvalName = r.EvalName,
                    Category = r.Category.ToString().ToLowerInvariant(),
                    GraderType = r.GraderType.ToString().ToLowerInvariant(),
                    Passed = r.Passed,
                    PassAt1 = r.PassAt1,
                    PassCount = r.PassCount,
                    AttemptCount = r.Attempts.Count,
                    AverageScore = r.AverageScore,
                    DurationSeconds = r.TotalDuration.TotalSeconds,
                    Skipped = r.Skipped,
                }).ToList(),
            });
        });
    }

    private static IReadOnlyList<string> GetSearchPaths()
    {
        var projectLocal = Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "evals");
        var global = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "evals");
        return [projectLocal, global];
    }
}

internal sealed class EvalRunRequest
{
    [JsonPropertyName("suite_name")] public string SuiteName { get; init; } = string.Empty;
    [JsonPropertyName("tag")] public string? Tag { get; init; }
}

internal sealed class EvalSuiteDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("description")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Description { get; init; }
    [JsonPropertyName("eval_count")] public int EvalCount { get; init; }
    [JsonPropertyName("tags")] public List<string> Tags { get; init; } = [];
}

internal sealed class EvalRunResponse
{
    [JsonPropertyName("suite_name")] public string SuiteName { get; init; } = string.Empty;
    [JsonPropertyName("started_at")] public DateTimeOffset StartedAt { get; init; }
    [JsonPropertyName("duration_seconds")] public double DurationSeconds { get; init; }
    [JsonPropertyName("pass_rate")] public double PassRate { get; init; }
    [JsonPropertyName("pass_at_1_rate")] public double PassAt1Rate { get; init; }
    [JsonPropertyName("total_passed")] public int TotalPassed { get; init; }
    [JsonPropertyName("total_failed")] public int TotalFailed { get; init; }
    [JsonPropertyName("total_skipped")] public int TotalSkipped { get; init; }
    [JsonPropertyName("results")] public IReadOnlyList<EvalResultResponseDto> Results { get; init; } = [];
}

internal sealed class EvalResultResponseDto
{
    [JsonPropertyName("eval_name")] public string EvalName { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; init; } = string.Empty;
    [JsonPropertyName("grader_type")] public string GraderType { get; init; } = string.Empty;
    [JsonPropertyName("passed")] public bool Passed { get; init; }
    [JsonPropertyName("pass_at_1")] public bool PassAt1 { get; init; }
    [JsonPropertyName("pass_count")] public int PassCount { get; init; }
    [JsonPropertyName("attempt_count")] public int AttemptCount { get; init; }
    [JsonPropertyName("average_score")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? AverageScore { get; init; }
    [JsonPropertyName("duration_seconds")] public double DurationSeconds { get; init; }
    [JsonPropertyName("skipped")] public bool Skipped { get; init; }
}
