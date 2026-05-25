using Sovrant.Runtime.Projects.Templates;
using Sovrant.Runtime.Projects.Validation;

namespace Sovrant.Runtime.Tests.Projects;

/// <summary>Phase 73 — Unit tests for ScaffoldManifestValidator (pure validator, no scaffold deps).</summary>
public sealed class ManifestValidatorTests
{
    private static ProjectFile File(string path, string content) => new(path, content);

    // ── package.json ─────────────────────────────────────────────────────────

    [Fact]
    public void PackageJson_ValidManifest_NoIssues()
    {
        var file = File("package.json", """
            {
              "name": "my-app",
              "version": "0.1.0",
              "dependencies": {
                "express": "^4.21.0"
              },
              "devDependencies": {
                "typescript": "^5.7.0",
                "vitest": "^3.0.0"
              }
            }
            """);

        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.Single(results);
        Assert.True(results[0].IsValid, string.Join("; ", results[0].Issues.Select(i => i.Message)));
    }

    [Fact]
    public void PackageJson_MissingName_Error()
    {
        var file = File("package.json", """{"version":"1.0.0"}""");
        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.Contains(results[0].Issues, i => i.Severity == ManifestIssueSeverity.Error && i.Message.Contains("name"));
    }

    [Fact]
    public void PackageJson_MissingVersion_Error()
    {
        var file = File("package.json", """{"name":"my-app"}""");
        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.Contains(results[0].Issues, i => i.Severity == ManifestIssueSeverity.Error && i.Message.Contains("version"));
    }

    [Fact]
    public void PackageJson_WorkspaceDependencies_NoIssues()
    {
        var file = File("apps/app/package.json", """
            {
              "name": "@my-app/app",
              "version": "0.1.0",
              "dependencies": {
                "@my-app/core": "workspace:*"
              }
            }
            """);

        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.True(results[0].IsValid, string.Join("; ", results[0].Issues.Select(i => i.Message)));
    }

    [Fact]
    public void PackageJson_InvalidJson_Error()
    {
        var file = File("package.json", "{ not valid json }");
        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.Contains(results[0].Issues, i => i.Severity == ManifestIssueSeverity.Error);
    }

    // ── .csproj ───────────────────────────────────────────────────────────────

    [Fact]
    public void Csproj_ValidPackageReferences_NoIssues()
    {
        var file = File("MyApp/MyApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
                <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
              </ItemGroup>
            </Project>
            """);

        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.True(results[0].IsValid, string.Join("; ", results[0].Issues.Select(i => i.Message)));
    }

    [Fact]
    public void Csproj_MissingVersion_Error()
    {
        var file = File("MyApp/MyApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="SomePackage" />
              </ItemGroup>
            </Project>
            """);

        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.Contains(results[0].Issues, i => i.Severity == ManifestIssueSeverity.Error);
    }

    // ── go.mod ────────────────────────────────────────────────────────────────

    [Fact]
    public void GoMod_ValidModFile_NoIssues()
    {
        var file = File("go.mod", """
            module github.com/example/my-app

            go 1.23
            """);

        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.True(results[0].IsValid, string.Join("; ", results[0].Issues.Select(i => i.Message)));
    }

    [Fact]
    public void GoMod_MissingModule_Error()
    {
        var file = File("go.mod", "go 1.23\n");
        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.Contains(results[0].Issues, i => i.Severity == ManifestIssueSeverity.Error);
    }

    // ── pyproject.toml ────────────────────────────────────────────────────────

    [Fact]
    public void PyprojectToml_WithProjectSection_NoIssues()
    {
        var file = File("pyproject.toml", """
            [project]
            name = "my-app"
            version = "0.1.0"
            dependencies = ["fastapi>=0.115.0"]
            """);

        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.True(results[0].IsValid, string.Join("; ", results[0].Issues.Select(i => i.Message)));
    }

    [Fact]
    public void PyprojectToml_NoSection_Warning()
    {
        var file = File("pyproject.toml", """
            name = "my-app"
            """);

        var results = ScaffoldManifestValidator.Validate([file]);
        Assert.Contains(results[0].Issues, i => i.Severity == ManifestIssueSeverity.Warning);
    }

    // ── Non-manifests are ignored ─────────────────────────────────────────────

    [Fact]
    public void NonManifestFiles_ReturnNoResults()
    {
        var files = new[]
        {
            File("src/index.ts", "export {};"),
            File("README.md", "# Hello"),
            File(".gitignore", "node_modules/"),
        };

        var results = ScaffoldManifestValidator.Validate(files);
        Assert.Empty(results);
    }

    [Fact]
    public void EmptyFileList_ReturnsNoResults()
    {
        var results = ScaffoldManifestValidator.Validate([]);
        Assert.Empty(results);
    }

    // ── Multiple manifests in one call ────────────────────────────────────────

    [Fact]
    public void MultipleManifests_EachValidatedIndependently()
    {
        var files = new[]
        {
            File("package.json", """{"name":"a","version":"1.0.0"}"""),
            File("MyApp/MyApp.csproj", """<Project><ItemGroup><PackageReference Include="X" Version="1.0"/></ItemGroup></Project>"""),
        };

        var results = ScaffoldManifestValidator.Validate(files);
        Assert.Equal(2, results.Count);
    }
}
