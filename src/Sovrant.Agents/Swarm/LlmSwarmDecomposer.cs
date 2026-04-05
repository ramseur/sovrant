using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Templates;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Decomposes a user prompt into a <see cref="SwarmPlan"/> by asking a high-level LLM agent
/// to produce a JSON task array, then assigns waves via Kahn's topological sort.
/// </summary>
public sealed partial class LlmSwarmDecomposer : ISwarmDecomposer
{
    private static readonly JsonSerializerOptions s_parseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly SovrantAgentFactory _factory;
    private readonly AgentTemplateRegistry _templates;
    private readonly ILogger<LlmSwarmDecomposer> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Decomposing prompt into swarm plan ({PromptLength} chars)")]
    private static partial void LogDecomposing(ILogger logger, int promptLength);

    [LoggerMessage(Level = LogLevel.Information, Message = "Decomposed into {TaskCount} tasks across {WaveCount} waves")]
    private static partial void LogDecomposed(ILogger logger, int taskCount, int waveCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse decomposition response: {Error}")]
    private static partial void LogParseFailed(ILogger logger, string error);

    public LlmSwarmDecomposer(
        SovrantAgentFactory factory,
        AgentTemplateRegistry templates,
        ILogger<LlmSwarmDecomposer> logger)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(logger);
        _factory = factory;
        _templates = templates;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SwarmPlan> DecomposeAsync(string prompt, SwarmConfig config, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(config);

        LogDecomposing(_logger, prompt.Length);

        var templateNames = string.Join(", ", _templates.All.Select(t => t.Name));
        var decomposerPrompt = BuildDecomposerPrompt(prompt, templateNames);

        // Create a decomposer agent at the configured level
        var agent = _factory.Create(
            name: "swarm-decomposer",
            role: AgentRole.Planner,
            customInstructions: decomposerPrompt);

        var task = AgentTask.Create(decomposerPrompt);
        var result = await agent.HandleAsync(task, ct).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException($"Decomposer agent failed: {result.Error}");

        var tasks = ParseTaskArray(result.Output);
        AssignWaves(tasks);

        var waveCount = tasks.Count > 0 ? tasks.Max(t => t.Wave) + 1 : 0;
        LogDecomposed(_logger, tasks.Count, waveCount);

        return new SwarmPlan
        {
            Id = $"swarm-{Guid.NewGuid():N}",
            OriginalPrompt = prompt,
            Tasks = tasks,
            WaveCount = waveCount,
        };
    }

    /// <summary>Parses the JSON task array from the agent's output (handles fenced and raw JSON).</summary>
    internal static List<SwarmTaskNode> ParseTaskArray(string output)
    {
        var json = ExtractJsonArray(output)
            ?? throw new InvalidOperationException("Decomposer did not return a valid JSON task array.");

        var items = JsonSerializer.Deserialize<List<DecomposerTaskDto>>(json, s_parseOptions)
            ?? throw new InvalidOperationException("Decomposer returned null task array.");

        if (items.Count == 0)
            throw new InvalidOperationException("Decomposer returned empty task array.");

        return items.Select((dto, i) => new SwarmTaskNode
        {
            Id = string.IsNullOrWhiteSpace(dto.Id) ? $"task-{i + 1:D2}" : dto.Id,
            Description = dto.Description ?? $"Task {i + 1}",
            Dependencies = dto.Dependencies ?? [],
            FilesToModify = dto.FilesToModify ?? [],
            AgentTemplate = dto.AgentTemplate,
            AllowedTools = dto.AllowedTools,
        }).ToList();
    }

    /// <summary>Extracts a JSON array from the output, stripping markdown code fences if present.</summary>
    internal static string? ExtractJsonArray(string text)
    {
        // Try fenced code block first
        var match = JsonFenceRegex().Match(text);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Try raw JSON array
        var start = text.IndexOf('[', StringComparison.Ordinal);
        var end = text.LastIndexOf(']');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return null;
    }

    /// <summary>
    /// Assigns wave indices to tasks using Kahn's topological sort.
    /// Tasks with no unresolved dependencies get wave 0, their dependents get wave 1, etc.
    /// </summary>
    internal static void AssignWaves(IList<SwarmTaskNode> tasks)
    {
        var taskMap = tasks.ToDictionary(t => t.Id);
        var inDegree = tasks.ToDictionary(t => t.Id, t => t.Dependencies.Count(d => taskMap.ContainsKey(d)));

        var queue = new Queue<string>(tasks.Where(t => inDegree[t.Id] == 0).Select(t => t.Id));
        var wave = 0;

        while (queue.Count > 0)
        {
            var currentWave = new List<string>(queue);
            queue.Clear();

            foreach (var id in currentWave)
            {
                taskMap[id].Wave = wave;
            }

            foreach (var task in tasks)
            {
                if (inDegree[task.Id] > 0)
                {
                    var resolved = task.Dependencies.Count(d => currentWave.Contains(d));
                    inDegree[task.Id] -= resolved;
                    if (inDegree[task.Id] == 0)
                        queue.Enqueue(task.Id);
                }
            }

            wave++;
        }

        // Detect cycles: any task with remaining in-degree > 0 has a circular dependency
        foreach (var task in tasks)
        {
            if (inDegree[task.Id] > 0)
                throw new InvalidOperationException($"Circular dependency detected involving task '{task.Id}'.");
        }
    }

    private static string BuildDecomposerPrompt(string userPrompt, string availableTemplates)
    {
        return $"""
            You are a task decomposer for a swarm of AI agents. Analyze the following user request
            and break it into discrete, parallelizable tasks.

            Available agent templates: {availableTemplates}

            Return ONLY a JSON array (no markdown, no explanation) where each element has:
            - "id": unique task ID like "task-01", "task-02", etc.
            - "description": what the task does
            - "dependencies": array of task IDs that must complete first (empty if none)
            - "files_to_modify": array of file paths this task will modify (for conflict detection)
            - "agent_template": which template to use (from available list, or null for default)
            - "allowed_tools": optional tool whitelist (null for default)

            Rules:
            1. Maximize parallelism — only add dependencies where strictly required
            2. Each task should be self-contained enough for one agent
            3. File conflicts between parallel tasks should be avoided via dependencies
            4. Keep tasks granular but not trivial

            User request:
            {userPrompt}
            """;
    }

    [GeneratedRegex(@"```(?:json)?\s*(\[[\s\S]*?\])\s*```", RegexOptions.Compiled)]
    private static partial Regex JsonFenceRegex();

    private sealed class DecomposerTaskDto
    {
        public string? Id { get; set; }
        public string? Description { get; set; }
        public List<string>? Dependencies { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("files_to_modify")]
        public List<string>? FilesToModify { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("agent_template")]
        public string? AgentTemplate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("allowed_tools")]
        public List<string>? AllowedTools { get; set; }
    }
}
