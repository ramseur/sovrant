using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Tools.Templates;

namespace Sovrant.Tools.Tests.Templates;

public sealed class UserToolTemplateRegistryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-tooltmpl-{Guid.NewGuid():N}");

    public UserToolTemplateRegistryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static UserToolTemplateRegistry CreateRegistry() =>
        new(NullLogger<UserToolTemplateRegistry>.Instance);

    [Fact]
    public void LoadFrom_ParsesFrontmatterAndBody()
    {
        File.WriteAllText(Path.Combine(_tempDir, "code-review.md"), """
            ---
            name: Code review checklist
            description: Use before merging any PR
            category: quality
            ---
            ## Steps
            1. Run tests
            2. Check coverage
            """);

        var registry = CreateRegistry();
        registry.LoadFrom(_tempDir, UserToolTier.Global);

        var template = registry.TryGet("code-review");
        Assert.NotNull(template);
        Assert.Equal("Code review checklist", template.Name);
        Assert.Equal("quality", template.Category);
        Assert.Equal(UserToolTier.Global, template.Tier);
        Assert.Contains("Run tests", template.Body);
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
        registry.LoadFrom(lower, UserToolTier.BuiltIn);
        registry.LoadFrom(higher, UserToolTier.Global);

        var template = registry.TryGet("shared");
        Assert.NotNull(template);
        Assert.Equal("Higher", template.Name);
        Assert.Equal(UserToolTier.Global, template.Tier);
    }

    [Fact]
    public void LoadFrom_MalformedFile_Skipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "no-frontmatter.md"), "Just markdown");
        File.WriteAllText(Path.Combine(_tempDir, "no-name.md"), "---\ndescription: missing\n---\nbody");

        var registry = CreateRegistry();
        registry.LoadFrom(_tempDir, UserToolTier.Global);

        Assert.Null(registry.TryGet("no-frontmatter"));
        Assert.Null(registry.TryGet("no-name"));
    }
}
