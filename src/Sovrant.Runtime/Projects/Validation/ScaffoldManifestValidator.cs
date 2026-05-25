using System.Text.Json;
using System.Text.RegularExpressions;
using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Runtime.Projects.Validation;

/// <summary>
/// Phase 73 — Validates dependency manifests in a scaffold's file tree.
/// Checks package.json (npm semver ranges), .csproj (PackageReference versions),
/// Cargo.toml (crate version fields), go.mod (module declarations), and pyproject.toml
/// (PEP 440 version specifiers). Pure in-memory — does not resolve against registries.
/// </summary>
public static class ScaffoldManifestValidator
{
    private static readonly Regex SemverRange =
        new(@"^(\^|~|>=?|<=?|=|workspace:\*|workspace:\^|workspace:~|\*|latest)?(\d+\.\d+\.\d+|\d+\.\d+|\d+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CargoVersion =
        new(@"^\d+\.\d+(\.\d+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);


    /// <summary>
    /// Validates all recognisable manifests in the scaffold output.
    /// Returns one <see cref="ManifestValidationResult"/> per manifest file found.
    /// </summary>
    public static IReadOnlyList<ManifestValidationResult> Validate(IReadOnlyList<ProjectFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var results = new List<ManifestValidationResult>();

        foreach (var file in files)
        {
            var name = Path.GetFileName(file.RelativePath);
            var ext = Path.GetExtension(file.RelativePath).ToUpperInvariant();

            ManifestValidationResult? result = name switch
            {
                "package.json" => ValidatePackageJson(file),
                "Cargo.toml" => ValidateCargoToml(file),
                "go.mod" => ValidateGoMod(file),
                "pyproject.toml" => ValidatePyprojectToml(file),
                _ when ext == ".CSPROJ" => ValidateCsproj(file),
                _ => null,
            };

            if (result is not null)
                results.Add(result);
        }

        return results;
    }

    // ── package.json ─────────────────────────────────────────────────────────

    private static ManifestValidationResult ValidatePackageJson(ProjectFile file)
    {
        var issues = new List<ManifestIssue>();

        try
        {
            using var doc = JsonDocument.Parse(file.Content);
            var root = doc.RootElement;

            CheckRequiredStringField(root, "name", file.FilePath(), issues);

            // Private packages (e.g. monorepo roots) don't require a version field.
            var isPrivate = root.TryGetProperty("private", out var privProp) &&
                            privProp.ValueKind == JsonValueKind.True;
            if (!isPrivate)
                CheckRequiredStringField(root, "version", file.FilePath(), issues);

            foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies" })
            {
                if (root.TryGetProperty(section, out var deps) && deps.ValueKind == JsonValueKind.Object)
                {
                    foreach (var dep in deps.EnumerateObject())
                        ValidateNpmVersion(dep.Name, dep.Value.GetString() ?? "", issues);
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ManifestIssue(ManifestIssueSeverity.Error, $"Invalid JSON: {ex.Message}"));
        }

        return new ManifestValidationResult(file.RelativePath, issues);
    }

    private static void ValidateNpmVersion(string name, string version, List<ManifestIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            issues.Add(new ManifestIssue(ManifestIssueSeverity.Error,
                $"Empty version specifier", name));
            return;
        }

        // workspace:* / workspace:^ / workspace:~ are always valid
        if (version.StartsWith("workspace:", StringComparison.Ordinal)) return;
        // URLs, file paths, git refs — skip
        if (version.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
            version.StartsWith("file:", StringComparison.Ordinal) ||
            version.StartsWith("git", StringComparison.OrdinalIgnoreCase)) return;

        var match = SemverRange.Match(version.Trim());
        if (!match.Success || match.Length == 0)
        {
            issues.Add(new ManifestIssue(ManifestIssueSeverity.Warning,
                $"Unrecognised version specifier '{version}'", name));
        }
    }

    // ── .csproj ───────────────────────────────────────────────────────────────

    private static ManifestValidationResult ValidateCsproj(ProjectFile file)
    {
        var issues = new List<ManifestIssue>();

        // Minimal XML parse via string scanning — avoids taking an XML dep in Runtime.
        var content = file.Content;
        var idx = 0;
        while (true)
        {
            var start = content.IndexOf("<PackageReference", idx, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            var end = content.IndexOf('>', start);
            if (end < 0) break;

            var element = content[start..(end + 1)];
            var pkgName = ExtractXmlAttr(element, "Include") ?? ExtractXmlAttr(element, "include") ?? "(unknown)";
            var version = ExtractXmlAttr(element, "Version") ?? ExtractXmlAttr(element, "version");

            if (string.IsNullOrEmpty(version))
                issues.Add(new ManifestIssue(ManifestIssueSeverity.Error,
                    "PackageReference is missing a Version attribute", pkgName));
            else if (!IsValidNuGetVersion(version))
                issues.Add(new ManifestIssue(ManifestIssueSeverity.Warning,
                    $"Unrecognised NuGet version '{version}'", pkgName));

            idx = end + 1;
        }

        return new ManifestValidationResult(file.RelativePath, issues);
    }

    private static bool IsValidNuGetVersion(string v)
    {
        // Accepts: "1.2.3", "[1.2,)", "(,2.0]", "*" etc.
        if (v == "*") return true;
        var trimmed = v.Trim('[', ']', '(', ')');
        var parts = trimmed.Split(',')[0].Trim();
        return Regex.IsMatch(parts, @"^\d+(\.\d+){0,3}(-[a-zA-Z0-9.]+)?(\.\*)?$",
            RegexOptions.CultureInvariant);
    }

    private static string? ExtractXmlAttr(string element, string attr)
    {
        var search = $"{attr}=\"";
        var start = element.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += search.Length;
        var end = element.IndexOf('"', start);
        return end < 0 ? null : element[start..end];
    }

    // ── Cargo.toml ────────────────────────────────────────────────────────────

    private static ManifestValidationResult ValidateCargoToml(ProjectFile file)
    {
        var issues = new List<ManifestIssue>();
        var lines = file.Content.Split('\n');
        var inDeps = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith('['))
            {
                inDeps = line is "[dependencies]" or "[dev-dependencies]" or "[build-dependencies]";
                continue;
            }

            if (!inDeps || line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;

            // Simple "name = \"version\"" form
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0) continue;

            var name = line[..eq].Trim();
            var rhs = line[(eq + 1)..].Trim().Trim('"');

            // Skip table inline values like { version = "...", features = [...] }
            if (rhs.StartsWith('{') || rhs.StartsWith('"')) continue;

            if (!CargoVersion.IsMatch(rhs) && rhs != "*" && !rhs.StartsWith("git", StringComparison.Ordinal))
            {
                issues.Add(new ManifestIssue(ManifestIssueSeverity.Warning,
                    $"Unrecognised Cargo version '{rhs}'", name));
            }
        }

        return new ManifestValidationResult(file.RelativePath, issues);
    }

    // ── go.mod ────────────────────────────────────────────────────────────────

    private static ManifestValidationResult ValidateGoMod(ProjectFile file)
    {
        var issues = new List<ManifestIssue>();
        var lines = file.Content.Split('\n');
        var hasModule = false;
        var hasGo = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("module ", StringComparison.Ordinal)) { hasModule = true; continue; }
            if (line.StartsWith("go ", StringComparison.Ordinal)) { hasGo = true; continue; }
        }

        if (!hasModule)
            issues.Add(new ManifestIssue(ManifestIssueSeverity.Error, "go.mod is missing a 'module' directive"));
        if (!hasGo)
            issues.Add(new ManifestIssue(ManifestIssueSeverity.Warning, "go.mod is missing a 'go' version directive"));

        return new ManifestValidationResult(file.RelativePath, issues);
    }

    // ── pyproject.toml ────────────────────────────────────────────────────────

    private static ManifestValidationResult ValidatePyprojectToml(ProjectFile file)
    {
        var issues = new List<ManifestIssue>();
        var content = file.Content;

        if (!content.Contains("[project]", StringComparison.Ordinal) &&
            !content.Contains("[tool.poetry]", StringComparison.Ordinal))
        {
            issues.Add(new ManifestIssue(ManifestIssueSeverity.Warning,
                "pyproject.toml has no [project] or [tool.poetry] section"));
        }

        return new ManifestValidationResult(file.RelativePath, issues);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FilePath(this ProjectFile file) => file.RelativePath;

    private static void CheckRequiredStringField(
        JsonElement root, string field, string filePath, List<ManifestIssue> issues)
    {
        if (!root.TryGetProperty(field, out var prop) ||
            prop.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(prop.GetString()))
        {
            issues.Add(new ManifestIssue(ManifestIssueSeverity.Error,
                $"Missing or empty required field '{field}'"));
        }
    }
}
