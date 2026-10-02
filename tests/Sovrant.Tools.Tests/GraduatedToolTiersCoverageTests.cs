using System.Runtime.CompilerServices;
using Sovrant.Runtime.Governance;

namespace Sovrant.Tools.Tests;

/// <summary>
/// Guards against built-in tools silently falling back to the Moderate
/// default because nobody added them to <see cref="GraduatedToolTiers"/>
/// (or added them under a name that doesn't match the tool's real
/// <c>Definition.Name</c>, as happened with <c>LS</c> and <c>MCPTool</c>).
/// </summary>
public sealed class GraduatedToolTiersCoverageTests
{
    private static IReadOnlyList<string> BuiltInToolNames() =>
        typeof(ITool).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ITool).IsAssignableFrom(t))
            // Every tool exposes a static ToolDefinition, so an uninitialized
            // instance is enough to read the name without building its dependencies.
            .Select(t => ((ITool)RuntimeHelpers.GetUninitializedObject(t)).Definition.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void EveryBuiltInTool_HasAnExplicitTier()
    {
        var tiered = new HashSet<string>(GraduatedToolTiers.All.Keys, StringComparer.Ordinal);

        var missing = BuiltInToolNames().Where(n => !tiered.Contains(n)).ToList();

        Assert.True(missing.Count == 0,
            $"Tools missing from GraduatedToolTiers (they default to Moderate): {string.Join(", ", missing)}");
    }

    [Fact]
    public void BuiltInToolNames_AreUnique()
    {
        var names = BuiltInToolNames();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
