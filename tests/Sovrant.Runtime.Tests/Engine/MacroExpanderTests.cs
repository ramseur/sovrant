using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Tests.Engine;

public sealed class MacroExpanderTests
{
    private static RuntimeStep Step(int index, string intent = "concrete", string? macro = null) =>
        new(
            Index: index,
            Intent: intent,
            ExpectedOutcome: "it works",
            ModelTier: RuntimeModelTier.Standard,
            MacroName: macro);

    private static RuntimePlan Plan(params RuntimeStep[] steps) =>
        new(
            Id: "plan-test",
            PlanVersion: 1,
            Goal: "test",
            Steps: steps,
            CreatedAt: DateTimeOffset.UtcNow);

    private static MacroDefinition Bisect() =>
        new(
            Name: "bisect",
            Description: "bisect a failing test",
            Steps: new[]
            {
                new MacroStepTemplate("narrow the range", "range halved", RuntimeModelTier.Fast),
                new MacroStepTemplate("re-run", "new signal", RuntimeModelTier.Standard),
            });

    [Fact]
    public void Expand_NoMacros_ReturnsPlanWithSameSteps()
    {
        var registry = new InMemoryMacroRegistry(Array.Empty<MacroDefinition>());
        var expander = new MacroExpander(registry);
        var plan = Plan(Step(0, "a"), Step(1, "b"), Step(2, "c"));

        var result = expander.Expand(plan);

        Assert.Equal(3, result.Steps.Count);
        Assert.Equal(new[] { "a", "b", "c" }, result.Steps.Select(s => s.Intent).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, result.Steps.Select(s => s.Index).ToArray());
    }

    [Fact]
    public void Expand_MacroReference_InlinesTemplateSteps()
    {
        var registry = new InMemoryMacroRegistry(new[] { Bisect() });
        var expander = new MacroExpander(registry);
        var plan = Plan(
            Step(0, "setup"),
            Step(1, "bisect here", macro: "bisect"),
            Step(2, "finalise"));

        var result = expander.Expand(plan);

        Assert.Equal(4, result.Steps.Count);
        Assert.Equal(
            new[] { "setup", "narrow the range", "re-run", "finalise" },
            result.Steps.Select(s => s.Intent).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.Steps.Select(s => s.Index).ToArray());
        Assert.All(result.Steps, s => Assert.Null(s.MacroName));
    }

    [Fact]
    public void Expand_UnknownMacro_Throws()
    {
        var registry = new InMemoryMacroRegistry(Array.Empty<MacroDefinition>());
        var expander = new MacroExpander(registry);
        var plan = Plan(Step(0, "call it", macro: "does-not-exist"));

        var ex = Assert.Throws<MacroExpansionException>(() => expander.Expand(plan));
        Assert.Contains("does-not-exist", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Expand_MultipleMacros_CompositionIsCorrect()
    {
        var helloMacro = new MacroDefinition("hello", "say hi",
            new[] { new MacroStepTemplate("say hi", "said", RuntimeModelTier.Fast) });
        var registry = new InMemoryMacroRegistry(new[] { helloMacro, Bisect() });
        var expander = new MacroExpander(registry);
        var plan = Plan(
            Step(0, macro: "hello"),
            Step(1, macro: "bisect"),
            Step(2, "end"));

        var result = expander.Expand(plan);

        Assert.Equal(
            new[] { "say hi", "narrow the range", "re-run", "end" },
            result.Steps.Select(s => s.Intent).ToArray());
    }

    [Fact]
    public void Expand_RewritesSparseIndexes_EvenWithoutMacros()
    {
        // A replanner might hand back steps whose indexes are inherited
        // from the previous plan version. The expander should renumber.
        var registry = new InMemoryMacroRegistry(Array.Empty<MacroDefinition>());
        var expander = new MacroExpander(registry);
        var plan = Plan(Step(5, "a"), Step(9, "b"));

        var result = expander.Expand(plan);

        Assert.Equal(new[] { 0, 1 }, result.Steps.Select(s => s.Index).ToArray());
    }

    [Fact]
    public void Expand_PreservesAllowedTools()
    {
        var macro = new MacroDefinition("locked", "tool-scoped",
            new[]
            {
                new MacroStepTemplate(
                    "read file", "contents known", RuntimeModelTier.Fast,
                    AllowedTools: new[] { "read_file" }),
            });
        var registry = new InMemoryMacroRegistry(new[] { macro });
        var expander = new MacroExpander(registry);
        var plan = Plan(Step(0, macro: "locked"));

        var result = expander.Expand(plan);

        var step = Assert.Single(result.Steps);
        Assert.NotNull(step.AllowedTools);
        Assert.Equal(new[] { "read_file" }, step.AllowedTools!);
    }

    [Fact]
    public void InMemoryMacroRegistry_FindReturnsNullForMissing()
    {
        var registry = new InMemoryMacroRegistry(new[] { Bisect() });
        Assert.Null(registry.Find("nope"));
        Assert.NotNull(registry.Find("bisect"));
    }
}
