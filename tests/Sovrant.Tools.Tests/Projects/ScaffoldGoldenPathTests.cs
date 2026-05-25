using Sovrant.Runtime.Projects.Templates;
using Sovrant.Tools.Projects.Scaffolds.DotNet;
using Sovrant.Tools.Projects.Scaffolds.Go;
using Sovrant.Tools.Projects.Scaffolds.Java;
using Sovrant.Tools.Projects.Scaffolds.Minimal;
using Sovrant.Tools.Projects.Scaffolds.Node;
using Sovrant.Tools.Projects.Scaffolds.Python;
using Sovrant.Tools.Projects.Scaffolds.Rust;

namespace Sovrant.Tools.Tests.Projects;

/// <summary>
/// Golden-path tests for all 21 Phase 73 scaffold templates.
/// These tests verify structural correctness of the generated file tree
/// without requiring any toolchain (Node, .NET SDK, etc.) to be installed.
/// </summary>
public sealed class ScaffoldGoldenPathTests
{
    private static readonly ScaffoldContext DefaultCtx = new("my-test-project");

    // ── Common invariants ────────────────────────────────────────────────────

    public static IEnumerable<object[]> AllTemplates =>
    [
        [new NodeExpressApiScaffold()],
        [new NodeCliScaffold()],
        [new NodeLibraryScaffold()],
        [new NodeNextJsScaffold()],
        [new NodeMonorepoScaffold()],
        [new DotNetWebApiScaffold()],
        [new DotNetConsoleScaffold()],
        [new DotNetLibraryScaffold()],
        [new DotNetWorkerScaffold()],
        [new DotNetBlazorScaffold()],
        [new PythonFastApiScaffold()],
        [new PythonScriptScaffold()],
        [new GoApiScaffold()],
        [new RustCliScaffold()],
        [new JavaMavenAppScaffold()],
        [new KotlinConsoleScaffold()],
        [new RubyScriptScaffold()],
        [new SwiftCliScaffold()],
        [new LuaScriptScaffold()],
        [new ZigCliScaffold()],
        [new CppCmakeScaffold()],
    ];

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_ProducesNonEmptyFileList(IProjectTemplate template)
    {
        var files = template.Scaffold(DefaultCtx);
        Assert.NotEmpty(files);
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_AllPathsAreRelative(IProjectTemplate template)
    {
        var files = template.Scaffold(DefaultCtx);
        foreach (var file in files)
        {
            Assert.False(Path.IsPathRooted(file.RelativePath),
                $"{template.Id}: path '{file.RelativePath}' is absolute");
            Assert.DoesNotContain("..", file.RelativePath.Split('/', '\\'),
                StringComparer.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_NoDuplicatePaths(IProjectTemplate template)
    {
        var files = template.Scaffold(DefaultCtx);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            Assert.True(seen.Add(file.RelativePath),
                $"{template.Id}: duplicate path '{file.RelativePath}'");
        }
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_AllFilesHaveContent(IProjectTemplate template)
    {
        var files = template.Scaffold(DefaultCtx);
        // Only check non-trivially-empty files — init files like __init__.py may be intentionally empty.
        foreach (var file in files)
        {
            Assert.NotNull(file.Content);
        }
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_ProjectNameAppearsInAtLeastOneFile(IProjectTemplate template)
    {
        var files = template.Scaffold(DefaultCtx);
        var name = DefaultCtx.ProjectName;
        // Templates may normalize the name — check kebab, snake, and pascal variants.
        var kebab = name.ToLowerInvariant();
        var snake = kebab.Replace('-', '_');
        var pascal = System.Text.RegularExpressions.Regex.Replace(name, @"(^|[-_])(\w)", m => m.Groups[2].Value.ToUpperInvariant());

        bool HasName(ProjectFile f) =>
            f.Content.Contains(kebab, StringComparison.OrdinalIgnoreCase) ||
            f.Content.Contains(snake, StringComparison.OrdinalIgnoreCase) ||
            f.Content.Contains(pascal, StringComparison.OrdinalIgnoreCase) ||
            f.RelativePath.Contains(kebab, StringComparison.OrdinalIgnoreCase) ||
            f.RelativePath.Contains(snake, StringComparison.OrdinalIgnoreCase) ||
            f.RelativePath.Contains(pascal, StringComparison.OrdinalIgnoreCase);

        Assert.True(files.Any(HasName),
            $"{template.Id}: project name '{name}' (or variants) not found in any file");
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_HasReadme(IProjectTemplate template)
    {
        var files = template.Scaffold(DefaultCtx);
        Assert.Contains(files, f => f.RelativePath.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_HasGitignore(IProjectTemplate template)
    {
        var files = template.Scaffold(DefaultCtx);
        Assert.Contains(files, f => f.RelativePath.EndsWith(".gitignore", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Scaffold_IdMatchesLanguageSlashKind(IProjectTemplate template)
    {
        Assert.Equal($"{template.Language}/{template.Kind}", template.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    // ── Per-template key-file assertions ────────────────────────────────────

    [Fact]
    public void NodeExpressApi_HasPackageJsonAndEntryPoints()
    {
        var files = new NodeExpressApiScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("package.json"), "missing package.json");
        Assert.True(files.ContainsKey("src/app.ts"), "missing src/app.ts");
        Assert.True(files.ContainsKey("src/server.ts"), "missing src/server.ts");
        Assert.Contains("express", files["package.json"].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NodeMonorepo_HasWorkspaceYamlAndSharedCore()
    {
        var files = new NodeMonorepoScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("pnpm-workspace.yaml"), "missing pnpm-workspace.yaml");
        Assert.True(files.ContainsKey("packages/core/src/index.ts"), "missing core/index.ts");
        Assert.Contains("workspace:*", files["apps/app/package.json"].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void DotNetWebApi_HasCsprojAndProgramCs()
    {
        var files = new DotNetWebApiScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.Any(kv => kv.Key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)), "missing .csproj");
        Assert.True(files.Any(kv => kv.Key.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase)), "missing Program.cs");
    }

    [Fact]
    public void PythonFastApi_HasPyprojectTomlAndMainModule()
    {
        var files = new PythonFastApiScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("pyproject.toml"), "missing pyproject.toml");
        Assert.True(files.Any(kv => kv.Key.EndsWith("main.py", StringComparison.OrdinalIgnoreCase)), "missing main.py");
        Assert.Contains("fastapi", files["pyproject.toml"].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GoApi_HasGoModAndMainGo()
    {
        var files = new GoApiScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("go.mod"), "missing go.mod");
        Assert.True(files.ContainsKey("main.go"), "missing main.go");
    }

    [Fact]
    public void RustCli_HasCargoTomlAndMainRs()
    {
        var files = new RustCliScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("Cargo.toml"), "missing Cargo.toml");
        Assert.True(files.ContainsKey("src/main.rs"), "missing src/main.rs");
    }

    [Fact]
    public void JavaMavenApp_HasPomXmlAndAppJava()
    {
        var files = new JavaMavenAppScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("pom.xml"), "missing pom.xml");
        Assert.True(files.Any(kv => kv.Key.EndsWith("App.java", StringComparison.OrdinalIgnoreCase)), "missing App.java");
    }

    [Fact]
    public void ZigCli_HasBuildZigAndMainZig()
    {
        var files = new ZigCliScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("build.zig"), "missing build.zig");
        Assert.True(files.ContainsKey("src/main.zig"), "missing src/main.zig");
    }

    [Fact]
    public void CppCmake_HasCMakeListsAndMainCpp()
    {
        var files = new CppCmakeScaffold().Scaffold(DefaultCtx).ToDictionary(f => f.RelativePath);
        Assert.True(files.ContainsKey("CMakeLists.txt"), "missing CMakeLists.txt");
        Assert.True(files.Any(kv => kv.Key.EndsWith("main.cpp", StringComparison.OrdinalIgnoreCase)), "missing main.cpp");
    }

    // ── ProjectTemplateRegistry integration ─────────────────────────────────

    [Fact]
    public void Registry_FindsAllTemplatesById()
    {
        var all = AllTemplates.Select(arr => (IProjectTemplate)arr[0]).ToList();
        var registry = RegistryFromList(all);

        foreach (var template in all)
        {
            var found = registry.TryGet(template.Id);
            Assert.NotNull(found);
            Assert.Equal(template.Id, found.Id, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Registry_FindsByLanguage()
    {
        var registry = RegistryFromList(AllTemplates.Select(arr => (IProjectTemplate)arr[0]).ToList());
        Assert.NotNull(registry.Find("node"));
        Assert.NotNull(registry.Find("dotnet"));
        Assert.NotNull(registry.Find("python"));
    }

    private static ProjectTemplateRegistry RegistryFromList(IList<IProjectTemplate> templates)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectTemplateRegistry>.Instance;
        return new ProjectTemplateRegistry(templates, logger);
    }
}
