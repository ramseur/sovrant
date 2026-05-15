using Sovrant.Api.Errors;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Documents.Tests;

/// <summary>
/// Verifies every Sovrant-originated exception is catchable as
/// <see cref="SovrantException"/> so callers can distinguish domain errors
/// from framework exceptions without catching <see cref="Exception"/>.
/// </summary>
public sealed class SovrantExceptionHierarchyTests
{
    [Fact]
    public void ApiError_IsSovrantException()
    {
        var ex = new ApiError("test");
        Assert.IsAssignableFrom<SovrantException>(ex);
    }

    [Fact]
    public void MacroExpansionException_IsSovrantException()
    {
        var ex = new MacroExpansionException("test");
        Assert.IsAssignableFrom<SovrantException>(ex);
    }

    [Fact]
    public void MigrationDriftException_IsSovrantException()
    {
        var ex = new MigrationDriftException("test");
        Assert.IsAssignableFrom<SovrantException>(ex);
    }

    [Fact]
    public void TemplateValidationException_IsSovrantException()
    {
        var ex = new TemplateValidationException("test");
        Assert.IsAssignableFrom<SovrantException>(ex);
    }
}
