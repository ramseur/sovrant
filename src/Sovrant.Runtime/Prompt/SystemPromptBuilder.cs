using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Prompt;

/// <summary>Builds the system prompt to be sent with each LLM request.</summary>
public sealed class SystemPromptBuilder
{
    private readonly SovrantConfig _config;

    public SystemPromptBuilder(SovrantConfig config) => _config = config;

    /// <summary>Builds the system prompt incorporating the current configuration and tool context.</summary>
    /// <param name="registry">The tool registry, used to list available tools in the prompt.</param>
    public string Build(IToolRegistry? registry = null)
    {
        var parts = new List<string>();

        parts.Add("You are a highly capable agentic AI assistant.");

        if (_config.PermissionMode == PermissionMode.Plan)
        {
            parts.Add(
                "You are operating in PLAN MODE. " +
                "You may only read files and gather information. " +
                "You must not execute any write, edit, delete, or shell operations. " +
                "Describe what you would do, but do not take destructive actions.");
        }

        if (registry is not null)
        {
            var tools = registry.GetDefinitions();
            if (tools.Count > 0)
            {
                var toolList = string.Join(", ", tools.Select(t => t.Name));
                parts.Add($"You have access to the following tools: {toolList}.");
            }
        }

        return string.Join("\n\n", parts);
    }
}
