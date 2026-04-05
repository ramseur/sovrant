using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Tools.Quality;

/// <summary>Configuration for the verification pipeline, loaded from <c>.sovrant/verify.json</c>.</summary>
public sealed class VerificationConfig
{
    [JsonPropertyName("coverage_threshold")]
    public int CoverageThreshold { get; set; } = 60;

    [JsonPropertyName("lint_severity")]
    public string LintSeverity { get; set; } = "warning";

    [JsonPropertyName("skip_phases")]
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Collection<string> SkipPhases { get; } = [];

    [JsonPropertyName("build_command")]
    public string? BuildCommand { get; set; }

    [JsonPropertyName("test_command")]
    public string? TestCommand { get; set; }

    [JsonPropertyName("lint_command")]
    public string? LintCommand { get; set; }

    [JsonPropertyName("typecheck_command")]
    public string? TypeCheckCommand { get; set; }

    [JsonPropertyName("security_command")]
    public string? SecurityCommand { get; set; }

    /// <summary>Loads config from <c>.sovrant/verify.json</c> in the given directory, or returns defaults.</summary>
    public static VerificationConfig Load(string workingDirectory)
    {
        var path = Path.Combine(workingDirectory, ".sovrant", "verify.json");
        if (!File.Exists(path))
            return new VerificationConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<VerificationConfig>(json) ?? new VerificationConfig();
        }
        catch (JsonException)
        {
            return new VerificationConfig();
        }
    }

    /// <summary>Returns true if the given phase should be skipped per configuration.</summary>
    public bool ShouldSkip(VerificationPhase phase) =>
        SkipPhases.Contains(phase.ToString(), StringComparer.OrdinalIgnoreCase);
}
