using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Documents.Templates;

namespace Sovrant.Runtime.Documents.Tests.Templates;

public sealed class UserDocumentTemplateRegistryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-doctmpl-{Guid.NewGuid():N}");

    public UserDocumentTemplateRegistryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static UserDocumentTemplateRegistry CreateRegistry() =>
        new(NullLogger<UserDocumentTemplateRegistry>.Instance);

    [Fact]
    public void LoadFrom_ParsesFrontmatterAndBody()
    {
        File.WriteAllText(Path.Combine(_tempDir, "weekly-report.md"), """
            ---
            name: Weekly status report
            description: A weekly project status update
            industry: business
            default_format: markdown
            ---
            # Weekly status — {{week_of}}

            ## Highlights
            - Item one
            """);

        var registry = CreateRegistry();
        registry.LoadFrom(_tempDir, UserTemplateTier.Global);

        var template = registry.TryGet("weekly-report");
        Assert.NotNull(template);
        Assert.Equal("Weekly status report", template.Name);
        Assert.Equal("business", template.Industry);
        Assert.Equal("markdown", template.DefaultFormat);
        Assert.Equal(UserTemplateTier.Global, template.Tier);
        Assert.Contains("Weekly status — {{week_of}}", template.Body);
    }

    [Fact]
    public void LoadFrom_HigherTierOverridesLower()
    {
        var lower = Path.Combine(_tempDir, "lower");
        var higher = Path.Combine(_tempDir, "higher");
        Directory.CreateDirectory(lower);
        Directory.CreateDirectory(higher);

        File.WriteAllText(Path.Combine(lower, "shared.md"), "---\nname: Lower\n---\nlower body");
        File.WriteAllText(Path.Combine(higher, "shared.md"), "---\nname: Higher\n---\nhigher body");

        var registry = CreateRegistry();
        registry.LoadFrom(lower, UserTemplateTier.BuiltIn);
        registry.LoadFrom(higher, UserTemplateTier.Global);

        var template = registry.TryGet("shared");
        Assert.NotNull(template);
        Assert.Equal("Higher", template.Name);
        Assert.Equal(UserTemplateTier.Global, template.Tier);
    }

    [Fact]
    public void LoadFrom_MalformedFile_Skipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "no-frontmatter.md"), "Just markdown, no frontmatter");
        File.WriteAllText(Path.Combine(_tempDir, "no-name.md"), "---\ndescription: missing name\n---\nbody");

        var registry = CreateRegistry();
        registry.LoadFrom(_tempDir, UserTemplateTier.Global);

        Assert.Null(registry.TryGet("no-frontmatter"));
        Assert.Null(registry.TryGet("no-name"));
    }
}
