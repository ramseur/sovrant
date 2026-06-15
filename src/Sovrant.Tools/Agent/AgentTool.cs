using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Templates;
using Sovrant.Api.Types;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Tools.Agent;

/// <summary>Launches a sub-agent with an isolated session and returns its final text response.</summary>
public sealed class AgentTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Agent", CreateSchema())
    {
        Description =
            "Launches a sub-agent with a fresh isolated session to handle a specific task. " +
            "Optionally specify a template (e.g. 'security-reviewer') for a purpose-built agent. " +
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
        var prompt = input.GetStringProp("prompt");
        if (string.IsNullOrWhiteSpace(prompt))
            return "Error: prompt is required.";

        if (s_depth.Value >= MaxDepth)
            return $"Error: maximum agent recursion depth ({MaxDepth}) reached. Cannot spawn a nested sub-agent.";

        var templateName = input.TryGetProperty("template", out var tEl) ? tEl.GetString() : null;
        var previousDepth = s_depth.Value;
        s_depth.Value = previousDepth + 1;
        try
        {
            // When a template is specified, delegate to a purpose-built agent.
            if (!string.IsNullOrWhiteSpace(templateName))
            {
                var registry = _services.GetService<AgentTemplateRegistry>();
                var template = registry?.TryGet(templateName);
                if (template is null)
                    return $"Error: unknown template '{templateName}'.";

                var factory = _services.GetService<SovrantAgentFactory>();
                if (factory is null)
                    return "Error: SovrantAgentFactory is not registered. Ensure AddOrchestrationSystem() was called.";

                var modelOverride = input.TryGetProperty("model", out var mEl) ? mEl.GetString() : null;
                var agent = factory.Create(template, modelOverride: modelOverride);
                var taskId = Guid.NewGuid().ToString("N");
                var result = await agent.HandleAsync(new AgentTask(taskId, prompt), ct).ConfigureAwait(false);
                if (result.Success)
                {
                    // Phase 116 F — attribute the template used.
                    var scope = AttributionScope.Current;
                    if (scope is not null)
                        await scope.RecordAsync("agents", templateName, ct).ConfigureAwait(false);
                    return result.Output;
                }
                return $"Sub-agent error: {result.Error}";
            }

            // Default path: fresh transient runtime with no template.
            var runtime = _services.GetRequiredService<IConversationRuntime>();
            var sb = new StringBuilder();
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
            return sb.Length > 0 ? sb.ToString() : "(sub-agent returned no output)";
        }
        finally
        {
            s_depth.Value = previousDepth;
        }
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "description": {"type": "string",  "description": "Brief description of the sub-task."},
                "prompt":      {"type": "string",  "description": "The prompt to send to the sub-agent."},
                "template":    {"type": "string",  "description": "Optional template name (e.g. 'coder', 'security-reviewer') for a purpose-built agent."},
                "model":       {"type": "string",  "description": "Optional model override (only used when template is specified)."}
            },
            "required": ["prompt"]
        }
        """).RootElement;
}
