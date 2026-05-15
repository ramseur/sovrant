using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Quality;

/// <summary>
/// Runs a structured 6-phase verification pipeline: Build, TypeCheck, Lint, Test, SecurityScan, DiffReview.
/// Auto-detects the project type and uses sensible defaults that can be overridden via <c>.sovrant/verify.json</c>.
/// </summary>
public sealed class VerifyTool : ITool
{
    private const int DefaultTimeoutMs = 300_000; // 5 minutes per phase
    private const int OutputCapChars = 64 * 1024;

    private static readonly ToolDefinition s_definition = new("Verify", CreateSchema())
    {
        Description =
            "Runs a multi-phase quality verification pipeline (build, type check, lint, test, security scan, diff review). " +
            "Auto-detects project type (.csproj→dotnet, package.json→npm, go.mod→Go, pyproject.toml→Python). " +
            "Configure via .sovrant/verify.json. Returns a structured pass/fail report.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var workDir = input.GetStringProp("path", Directory.GetCurrentDirectory());
        if (!Directory.Exists(workDir))
            return $"Error: directory not found: {workDir}";

        var config = VerificationConfig.Load(workDir);

        // Allow per-invocation skip_phases override
        var skipOverride = input.GetStringArrayProp("skip_phases");
        if (skipOverride.Length > 0)
        {
            foreach (var s in skipOverride)
            {
                if (!config.SkipPhases.Contains(s, StringComparer.OrdinalIgnoreCase))
                    config.SkipPhases.Add(s);
            }
        }

        var projectType = ProjectDetector.Detect(workDir);
        var result = new VerificationResult { ProjectType = projectType.ToString() };

        // Phase 1: Build
        result.Phases.Add(await RunCommandPhaseAsync(
            VerificationPhase.Build,
            config.BuildCommand ?? ProjectDetector.DefaultBuildCommand(projectType),
            config, workDir, ct).ConfigureAwait(false));

        // Phase 2: TypeCheck
        result.Phases.Add(await RunCommandPhaseAsync(
            VerificationPhase.TypeCheck,
            config.TypeCheckCommand ?? ProjectDetector.DefaultTypeCheckCommand(projectType),
            config, workDir, ct).ConfigureAwait(false));

        // Phase 3: Lint
        result.Phases.Add(await RunCommandPhaseAsync(
            VerificationPhase.Lint,
            config.LintCommand ?? ProjectDetector.DefaultLintCommand(projectType),
            config, workDir, ct).ConfigureAwait(false));

        // Phase 4: Test
        result.Phases.Add(await RunCommandPhaseAsync(
            VerificationPhase.Test,
            config.TestCommand ?? ProjectDetector.DefaultTestCommand(projectType),
            config, workDir, ct).ConfigureAwait(false));

        // Phase 5: SecurityScan
        result.Phases.Add(await RunCommandPhaseAsync(
            VerificationPhase.SecurityScan,
            config.SecurityCommand ?? ProjectDetector.DefaultSecurityCommand(projectType),
            config, workDir, ct).ConfigureAwait(false));

        // Phase 6: DiffReview
        result.Phases.Add(await RunDiffReviewAsync(config, workDir, ct).ConfigureAwait(false));

        return result.ToReport();
    }

    private static async Task<PhaseResult> RunCommandPhaseAsync(
        VerificationPhase phase,
        string? command,
        VerificationConfig config,
        string workDir,
        CancellationToken ct)
    {
        if (config.ShouldSkip(phase))
            return new PhaseResult(phase, true, string.Empty, Skipped: true);

        if (string.IsNullOrWhiteSpace(command))
            return new PhaseResult(phase, true, "No command configured or detected — skipped.", Skipped: true);

        var shellName = OperatingSystem.IsWindows() ? "cmd" : "sh";
        var shellArgs = OperatingSystem.IsWindows()
            ? new[] { "/c", $"cd /d \"{workDir}\" && {command}" }
            : new[] { "-c", $"cd \"{workDir}\" && {command}" };

        var proc = await ProcessExecutor.RunAsync(
            shellName,
            shellArgs,
            DefaultTimeoutMs,
            OutputCapChars,
            ct: ct).ConfigureAwait(false);

        return new PhaseResult(phase, proc.ExitCode == 0, proc.Output);
    }

    private static async Task<PhaseResult> RunDiffReviewAsync(
        VerificationConfig config,
        string workDir,
        CancellationToken ct)
    {
        if (config.ShouldSkip(VerificationPhase.DiffReview))
            return new PhaseResult(VerificationPhase.DiffReview, true, string.Empty, Skipped: true);

        // Get staged + unstaged diff
        var shellName = OperatingSystem.IsWindows() ? "cmd" : "sh";
        var shellArgs = OperatingSystem.IsWindows()
            ? new[] { "/c", $"cd /d \"{workDir}\" && git diff HEAD 2>NUL" }
            : new[] { "-c", $"cd \"{workDir}\" && git diff HEAD 2>/dev/null" };

        var proc = await ProcessExecutor.RunAsync(
            shellName,
            shellArgs,
            60_000,
            OutputCapChars,
            ct: ct).ConfigureAwait(false);

        if (proc.ExitCode != 0)
        {
            // Not a git repo or no HEAD — skip gracefully
            return new PhaseResult(VerificationPhase.DiffReview, true, "Not a git repository — skipped.", Skipped: true);
        }

        var findings = DiffReviewer.Review(proc.Output);
        return string.IsNullOrEmpty(findings)
            ? new PhaseResult(VerificationPhase.DiffReview, true, "Clean diff.")
            : new PhaseResult(VerificationPhase.DiffReview, false, findings);
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Working directory to verify (defaults to cwd). Must contain the project files."
                },
                "skip_phases": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "Phases to skip: Build, TypeCheck, Lint, Test, SecurityScan, DiffReview."
                }
            }
        }
        """).RootElement;
}
