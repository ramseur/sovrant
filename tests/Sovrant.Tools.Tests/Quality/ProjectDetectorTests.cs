using Sovrant.Tools.Quality;
using static Sovrant.Tools.Quality.ProjectDetector;

namespace Sovrant.Tools.Tests.Quality;

public sealed class ProjectDetectorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-detect-{Guid.NewGuid():N}");

    public ProjectDetectorTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Detect_Csproj_ReturnsDotnet()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Foo.csproj"), "<Project/>");
        Assert.Equal(ProjectType.Dotnet, ProjectDetector.Detect(_tempDir));
    }

    [Fact]
    public void Detect_Slnx_ReturnsDotnet()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Foo.slnx"), "");
        Assert.Equal(ProjectType.Dotnet, ProjectDetector.Detect(_tempDir));
    }

    [Fact]
    public void Detect_PackageJson_ReturnsNode()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), "{}");
        Assert.Equal(ProjectType.Node, ProjectDetector.Detect(_tempDir));
    }

    [Fact]
    public void Detect_GoMod_ReturnsGo()
    {
        File.WriteAllText(Path.Combine(_tempDir, "go.mod"), "module example");
        Assert.Equal(ProjectType.Go, ProjectDetector.Detect(_tempDir));
    }

    [Fact]
    public void Detect_Pyproject_ReturnsPython()
    {
        File.WriteAllText(Path.Combine(_tempDir, "pyproject.toml"), "[project]");
        Assert.Equal(ProjectType.Python, ProjectDetector.Detect(_tempDir));
    }

    [Fact]
    public void Detect_SetupPy_ReturnsPython()
    {
        File.WriteAllText(Path.Combine(_tempDir, "setup.py"), "setup()");
        Assert.Equal(ProjectType.Python, ProjectDetector.Detect(_tempDir));
    }

    [Fact]
    public void Detect_Empty_ReturnsUnknown()
    {
        Assert.Equal(ProjectType.Unknown, ProjectDetector.Detect(_tempDir));
    }

    [Fact]
    public void DefaultBuildCommand_Dotnet() =>
        Assert.Equal("dotnet build", ProjectDetector.DefaultBuildCommand(ProjectType.Dotnet));

    [Fact]
    public void DefaultBuildCommand_Node() =>
        Assert.Equal("npm run build", ProjectDetector.DefaultBuildCommand(ProjectType.Node));

    [Fact]
    public void DefaultBuildCommand_Go() =>
        Assert.Equal("go build ./...", ProjectDetector.DefaultBuildCommand(ProjectType.Go));

    [Fact]
    public void DefaultBuildCommand_Unknown_ReturnsNull() =>
        Assert.Null(ProjectDetector.DefaultBuildCommand(ProjectType.Unknown));

    [Fact]
    public void DefaultTestCommand_Dotnet() =>
        Assert.Equal("dotnet test", ProjectDetector.DefaultTestCommand(ProjectType.Dotnet));

    [Fact]
    public void DefaultTestCommand_Node() =>
        Assert.Equal("npm test", ProjectDetector.DefaultTestCommand(ProjectType.Node));

    [Fact]
    public void DefaultTestCommand_Go() =>
        Assert.Equal("go test ./...", ProjectDetector.DefaultTestCommand(ProjectType.Go));

    [Fact]
    public void DefaultTestCommand_Python() =>
        Assert.Equal("pytest", ProjectDetector.DefaultTestCommand(ProjectType.Python));

    [Fact]
    public void DefaultLintCommand_Dotnet() =>
        Assert.Equal("dotnet format --verify-no-changes", ProjectDetector.DefaultLintCommand(ProjectType.Dotnet));

    [Fact]
    public void DefaultLintCommand_Node() =>
        Assert.Equal("npx eslint .", ProjectDetector.DefaultLintCommand(ProjectType.Node));

    [Fact]
    public void DefaultLintCommand_Go() =>
        Assert.Equal("golangci-lint run", ProjectDetector.DefaultLintCommand(ProjectType.Go));

    [Fact]
    public void DefaultLintCommand_Python() =>
        Assert.Equal("ruff check .", ProjectDetector.DefaultLintCommand(ProjectType.Python));

    [Fact]
    public void DefaultSecurityCommand_Dotnet() =>
        Assert.Equal("dotnet list package --vulnerable", ProjectDetector.DefaultSecurityCommand(ProjectType.Dotnet));

    [Fact]
    public void DefaultSecurityCommand_Node() =>
        Assert.Equal("npm audit", ProjectDetector.DefaultSecurityCommand(ProjectType.Node));

    [Fact]
    public void DefaultTypeCheckCommand_Node_ReturnsTsc() =>
        Assert.Equal("npx tsc --noEmit", ProjectDetector.DefaultTypeCheckCommand(ProjectType.Node));

    [Fact]
    public void DefaultTypeCheckCommand_Dotnet_ReturnsNull() =>
        Assert.Null(ProjectDetector.DefaultTypeCheckCommand(ProjectType.Dotnet));
}
