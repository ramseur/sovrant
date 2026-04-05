namespace Sovrant.Tools.Quality;

/// <summary>Detects the project type from marker files in the working directory.</summary>
internal static class ProjectDetector
{
    internal enum ProjectType { Dotnet, Node, Go, Python, Unknown }

    /// <summary>
    /// Detects project type by looking for marker files.
    /// Checks for .csproj/.fsproj/.slnx/.sln, package.json, go.mod, pyproject.toml/setup.py.
    /// </summary>
    internal static ProjectType Detect(string directory)
    {
        if (Directory.GetFiles(directory, "*.slnx").Length > 0 ||
            Directory.GetFiles(directory, "*.sln").Length > 0 ||
            Directory.GetFiles(directory, "*.csproj").Length > 0 ||
            Directory.GetFiles(directory, "*.fsproj").Length > 0)
            return ProjectType.Dotnet;

        if (File.Exists(Path.Combine(directory, "package.json")))
            return ProjectType.Node;

        if (File.Exists(Path.Combine(directory, "go.mod")))
            return ProjectType.Go;

        if (File.Exists(Path.Combine(directory, "pyproject.toml")) ||
            File.Exists(Path.Combine(directory, "setup.py")))
            return ProjectType.Python;

        return ProjectType.Unknown;
    }

    /// <summary>Returns the default build command for the detected project type.</summary>
    internal static string? DefaultBuildCommand(ProjectType type) => type switch
    {
        ProjectType.Dotnet => "dotnet build",
        ProjectType.Node => "npm run build",
        ProjectType.Go => "go build ./...",
        ProjectType.Python => null, // no standard build
        _ => null,
    };

    /// <summary>Returns the default type-check command for the detected project type.</summary>
    internal static string? DefaultTypeCheckCommand(ProjectType type) => type switch
    {
        ProjectType.Node => "npx tsc --noEmit",
        ProjectType.Python => "mypy .",
        _ => null, // dotnet type-checks during build; Go type-checks during build
    };

    /// <summary>Returns the default lint command for the detected project type.</summary>
    internal static string? DefaultLintCommand(ProjectType type) => type switch
    {
        ProjectType.Dotnet => "dotnet format --verify-no-changes",
        ProjectType.Node => "npx eslint .",
        ProjectType.Go => "golangci-lint run",
        ProjectType.Python => "ruff check .",
        _ => null,
    };

    /// <summary>Returns the default test command for the detected project type.</summary>
    internal static string? DefaultTestCommand(ProjectType type) => type switch
    {
        ProjectType.Dotnet => "dotnet test",
        ProjectType.Node => "npm test",
        ProjectType.Go => "go test ./...",
        ProjectType.Python => "pytest",
        _ => null,
    };

    /// <summary>Returns the default security scan command for the detected project type.</summary>
    internal static string? DefaultSecurityCommand(ProjectType type) => type switch
    {
        ProjectType.Dotnet => "dotnet list package --vulnerable",
        ProjectType.Node => "npm audit",
        ProjectType.Go => "govulncheck ./...",
        ProjectType.Python => "pip-audit",
        _ => null,
    };
}
