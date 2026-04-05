using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Tests.Swarm;

public class LlmSwarmDecomposerTests
{
    // ── ExtractJsonArray ─────────────────────────────────────────────────

    [Fact]
    public void ExtractJsonArray_RawJson_Extracts()
    {
        var text = "Here are the tasks: [{\"id\":\"t1\"}]";
        var result = LlmSwarmDecomposer.ExtractJsonArray(text);
        Assert.NotNull(result);
        Assert.StartsWith("[", result);
    }

    [Fact]
    public void ExtractJsonArray_FencedJson_Extracts()
    {
        var text = "```json\n[{\"id\":\"t1\"}]\n```";
        var result = LlmSwarmDecomposer.ExtractJsonArray(text);
        Assert.NotNull(result);
        Assert.StartsWith("[", result);
    }

    [Fact]
    public void ExtractJsonArray_NoArray_ReturnsNull()
    {
        var text = "I couldn't decompose this task.";
        Assert.Null(LlmSwarmDecomposer.ExtractJsonArray(text));
    }

    [Fact]
    public void ExtractJsonArray_FencedWithoutLang_Extracts()
    {
        var text = "```\n[{\"id\":\"t1\"}]\n```";
        var result = LlmSwarmDecomposer.ExtractJsonArray(text);
        Assert.NotNull(result);
    }

    // ── ParseTaskArray ───────────────────────────────────────────────────

    [Fact]
    public void ParseTaskArray_ValidJson_ParsesTasks()
    {
        var json = """
        [
            {"id": "task-01", "description": "Create models", "dependencies": [], "files_to_modify": ["Model.cs"]},
            {"id": "task-02", "description": "Write tests", "dependencies": ["task-01"], "files_to_modify": ["Tests.cs"]}
        ]
        """;
        var tasks = LlmSwarmDecomposer.ParseTaskArray(json);
        Assert.Equal(2, tasks.Count);
        Assert.Equal("task-01", tasks[0].Id);
        Assert.Equal("task-02", tasks[1].Id);
        Assert.Empty(tasks[0].Dependencies);
        Assert.Single(tasks[1].Dependencies);
    }

    [Fact]
    public void ParseTaskArray_MissingIds_AssignsAutoIds()
    {
        var json = """[{"description": "First"}, {"description": "Second"}]""";
        var tasks = LlmSwarmDecomposer.ParseTaskArray(json);
        Assert.Equal("task-01", tasks[0].Id);
        Assert.Equal("task-02", tasks[1].Id);
    }

    [Fact]
    public void ParseTaskArray_EmptyArray_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => LlmSwarmDecomposer.ParseTaskArray("[]"));
    }

    [Fact]
    public void ParseTaskArray_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => LlmSwarmDecomposer.ParseTaskArray("not json"));
    }

    [Fact]
    public void ParseTaskArray_FencedJson_ParsesSuccessfully()
    {
        var output = "```json\n[{\"id\": \"t1\", \"description\": \"do stuff\"}]\n```";
        var tasks = LlmSwarmDecomposer.ParseTaskArray(output);
        Assert.Single(tasks);
    }

    // ── AssignWaves (topological sort) ───────────────────────────────────

    [Fact]
    public void AssignWaves_LinearChain()
    {
        var tasks = new List<SwarmTaskNode>
        {
            new() { Id = "A", Description = "A", Dependencies = [] },
            new() { Id = "B", Description = "B", Dependencies = ["A"] },
            new() { Id = "C", Description = "C", Dependencies = ["B"] },
        };

        LlmSwarmDecomposer.AssignWaves(tasks);

        Assert.Equal(0, tasks[0].Wave);
        Assert.Equal(1, tasks[1].Wave);
        Assert.Equal(2, tasks[2].Wave);
    }

    [Fact]
    public void AssignWaves_Parallel_AllWaveZero()
    {
        var tasks = new List<SwarmTaskNode>
        {
            new() { Id = "A", Description = "A", Dependencies = [] },
            new() { Id = "B", Description = "B", Dependencies = [] },
            new() { Id = "C", Description = "C", Dependencies = [] },
        };

        LlmSwarmDecomposer.AssignWaves(tasks);

        Assert.All(tasks, t => Assert.Equal(0, t.Wave));
    }

    [Fact]
    public void AssignWaves_Diamond()
    {
        // A → B, A → C, B+C → D
        var tasks = new List<SwarmTaskNode>
        {
            new() { Id = "A", Description = "A", Dependencies = [] },
            new() { Id = "B", Description = "B", Dependencies = ["A"] },
            new() { Id = "C", Description = "C", Dependencies = ["A"] },
            new() { Id = "D", Description = "D", Dependencies = ["B", "C"] },
        };

        LlmSwarmDecomposer.AssignWaves(tasks);

        Assert.Equal(0, tasks[0].Wave); // A
        Assert.Equal(1, tasks[1].Wave); // B
        Assert.Equal(1, tasks[2].Wave); // C
        Assert.Equal(2, tasks[3].Wave); // D
    }

    [Fact]
    public void AssignWaves_Cycle_Throws()
    {
        var tasks = new List<SwarmTaskNode>
        {
            new() { Id = "A", Description = "A", Dependencies = ["B"] },
            new() { Id = "B", Description = "B", Dependencies = ["A"] },
        };

        Assert.Throws<InvalidOperationException>(() => LlmSwarmDecomposer.AssignWaves(tasks));
    }

    [Fact]
    public void AssignWaves_MixedDependencies()
    {
        // A (no deps), B depends on A, C (no deps), D depends on B and C
        var tasks = new List<SwarmTaskNode>
        {
            new() { Id = "A", Description = "A", Dependencies = [] },
            new() { Id = "B", Description = "B", Dependencies = ["A"] },
            new() { Id = "C", Description = "C", Dependencies = [] },
            new() { Id = "D", Description = "D", Dependencies = ["B", "C"] },
        };

        LlmSwarmDecomposer.AssignWaves(tasks);

        Assert.Equal(0, tasks[0].Wave); // A
        Assert.Equal(1, tasks[1].Wave); // B
        Assert.Equal(0, tasks[2].Wave); // C
        Assert.Equal(2, tasks[3].Wave); // D
    }
}
