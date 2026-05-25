using Sovrant.Runtime.Projects.Templates;
using Sovrant.Runtime.Projects.Validation;
using Sovrant.Tools.Projects.Scaffolds.DotNet;
using Sovrant.Tools.Projects.Scaffolds.Go;
using Sovrant.Tools.Projects.Scaffolds.Node;
using Sovrant.Tools.Projects.Scaffolds.Python;
using Sovrant.Tools.Projects.Scaffolds.Rust;

namespace Sovrant.Tools.Tests.Projects;

/// <summary>
/// Phase 73 — Verifies that scaffold templates produce dependency manifests
/// that pass the ScaffoldManifestValidator (no errors, at most warnings).
/// </summary>
public sealed class ScaffoldManifestIntegrationTests
{
    public static IEnumerable<object[]> ManifestTemplates =>
    [
        ["node/express-api", (IProjectTemplate)new NodeExpressApiScaffold()],
        ["node/cli",         new NodeCliScaffold()],
        ["node/library",     new NodeLibraryScaffold()],
        ["node/monorepo",    new NodeMonorepoScaffold()],
        ["dotnet/webapi",    new DotNetWebApiScaffold()],
        ["dotnet/console",   new DotNetConsoleScaffold()],
        ["dotnet/library",   new DotNetLibraryScaffold()],
        ["dotnet/worker",    new DotNetWorkerScaffold()],
        ["dotnet/blazor",    new DotNetBlazorScaffold()],
        ["python/fastapi",   new PythonFastApiScaffold()],
        ["go/api",           new GoApiScaffold()],
        ["rust/cli",         new RustCliScaffold()],
    ];

    [Theory]
    [MemberData(nameof(ManifestTemplates))]
    public void Scaffold_ManifestsHaveNoErrors(string templateId, IProjectTemplate template)
    {
        var files = template.Scaffold(new ScaffoldContext("my-project"));
        var results = ScaffoldManifestValidator.Validate(files);

        var errors = results
            .SelectMany(r => r.Issues)
            .Where(i => i.Severity == ManifestIssueSeverity.Error)
            .Select(i => $"{i.Dependency ?? "?"}: {i.Message}")
            .ToList();

        Assert.True(errors.Count == 0,
            $"[{templateId}] manifest errors: {string.Join("; ", errors)}");
    }

    [Theory]
    [MemberData(nameof(ManifestTemplates))]
    public void Scaffold_HasAtLeastOneManifest(string templateId, IProjectTemplate template)
    {
        var files = template.Scaffold(new ScaffoldContext("my-project"));
        var results = ScaffoldManifestValidator.Validate(files);

        Assert.True(results.Count > 0,
            $"[{templateId}] no recognisable manifests found in scaffold output");
    }
}
