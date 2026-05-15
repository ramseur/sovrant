using Sovrant.Runtime.Documents.Packages;

namespace Sovrant.Runtime.Documents.Tests;

public class DocumentPackageRegistryTests
{
    [Fact]
    public void BuiltIn_Includes_Expected_Packages()
    {
        var registry = new DocumentPackageRegistry(BuiltInDocumentPackages.All);

        var ids = registry.All.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("real-estate/closing-package", ids);
        Assert.Contains("business/onboarding-package", ids);
        Assert.Contains("healthcare/intake-package", ids);
    }

    [Fact]
    public void TryResolve_IsCaseInsensitive()
    {
        var registry = new DocumentPackageRegistry(BuiltInDocumentPackages.All);

        Assert.True(registry.TryResolve("REAL-ESTATE/CLOSING-PACKAGE", out var pkg));
        Assert.NotNull(pkg);
        Assert.Equal("real-estate/closing-package", pkg!.Id);
    }

    [Fact]
    public void TryResolve_UnknownId_ReturnsFalse()
    {
        var registry = new DocumentPackageRegistry(BuiltInDocumentPackages.All);

        Assert.False(registry.TryResolve("does-not-exist", out var pkg));
        Assert.Null(pkg);
    }

    [Fact]
    public void Closing_Package_Has_ThreeRealEstateTemplates()
    {
        var registry = new DocumentPackageRegistry(BuiltInDocumentPackages.All);

        Assert.True(registry.TryResolve("real-estate/closing-package", out var pkg));
        Assert.Equal(3, pkg!.Items.Count);
        Assert.All(pkg.Items, item => Assert.StartsWith("real-estate/", item.TemplateId));
    }
}
