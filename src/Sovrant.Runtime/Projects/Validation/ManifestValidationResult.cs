namespace Sovrant.Runtime.Projects.Validation;

/// <summary>Phase 73 — Result of validating a dependency manifest (package.json, .csproj, Cargo.toml, etc.).</summary>
public sealed record ManifestValidationResult(
    string FilePath,
    IReadOnlyList<ManifestIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>A single issue found in a dependency manifest.</summary>
public sealed record ManifestIssue(
    ManifestIssueSeverity Severity,
    string Message,
    string? Dependency = null);

public enum ManifestIssueSeverity { Warning, Error }
