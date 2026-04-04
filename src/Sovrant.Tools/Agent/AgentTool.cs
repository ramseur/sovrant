using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Api.Types;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Tools.Agent;

/// <summary>Launches a sub-agent with an isolated session and returns its final text response.</summary>
public sealed class AgentTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Agent", CreateSchema())
    {
        Description =
            "Launches a sub-agent with a fresh isolated session to handle a specific task. " +
            "The agent runs to completion and returns its full text output. " +
            "Use for parallelising independent sub-tasks.",
    };

    private const int MaxDepth = 5;
    private static readonly AsyncLocal<int> s_depth = new();

    private readonly IServiceProvider _services;

    public AgentTool(IServiceProvider services) => _services = services;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var prompt = GetString(input, "prompt");
        if (string.IsNullOrWhiteSpace(prompt))
            return "Error: prompt is required.";

        if (s_depth.Value >= MaxDepth)
            return $"Error: maximum agent recursion depth ({MaxDepth}) reached. Cannot spawn a nested sub-agent.";

        // Create a fresh isolated runtime (transient)
        var runtime = _services.GetRequiredService<IConversationRuntime>();
        var sb = new StringBuilder();

        var previousDepth = s_depth.Value;
        s_depth.Value = previousDepth + 1;
        try
        {
            await foreach (var ev in runtime.RunTurnAsync(prompt, ct).ConfigureAwait(false))
            {
                switch (ev)
                {
                    case RuntimeEvent.TextChunk tc:
                        sb.Append(tc.Text);
                        break;
                    case RuntimeEvent.RuntimeError err:
                        return $"Sub-agent error: {err.Message}";
                }
            }
        }
        finally
        {
            s_depth.Value = previousDepth;
        }

        return sb.Length > 0 ? sb.ToString() : "(sub-agent returned no output)";
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "description": {"type": "string", "description": "Brief description of the sub-task."},
                "prompt":      {"type": "string", "description": "The prompt to send to the sub-agent."}
            },
            "required": ["prompt"]
        }
        """).RootElement;
}
