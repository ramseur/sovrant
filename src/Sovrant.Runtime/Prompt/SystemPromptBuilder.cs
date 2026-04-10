using Sovrant.Api.Types;
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

        // Phase 43 — tell the model what shell will run its commands so it
        // generates native syntax instead of producing bash one-liners that
        // PowerShell can't parse. On Windows we run commands through
        // PowerShell (pwsh or Windows PowerShell 5.1). Without this hint,
        // models default to bash-isms like `&&`, `||`, backticks for command
        // substitution, `ls -la`, and `rm -rf`, which either error out or
        // behave differently under PowerShell 5.1.
        if (OperatingSystem.IsWindows())
        {
            parts.Add(
                "The Bash tool on this system runs commands through PowerShell " +
                "(pwsh 7+ if available, otherwise Windows PowerShell 5.1). " +
                "Generate PowerShell-native syntax rather than bash: use `;` to " +
                "chain commands (not `&&` — it is not supported in Windows PowerShell 5.1), " +
                "use `Get-ChildItem`, `Remove-Item`, `Copy-Item`, `Get-Content`, " +
                "and `Set-Location` instead of `ls`, `rm`, `cp`, `cat`, and `cd` " +
                "when you need switches beyond the basic aliases. " +
                "Use forward slashes in paths — PowerShell accepts them. " +
                "The working directory persists across Bash invocations, so " +
                "`cd` in one call is visible to the next.");
        }
        else
        {
            parts.Add(
                "The Bash tool on this system runs commands through /bin/bash. " +
                "Use standard POSIX shell syntax. The working directory persists " +
                "across Bash invocations, so `cd` in one call is visible to the next.");
        }

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

            // Orchestration strategy — teach the LLM when to escalate
            if (HasOrchestrationTools(tools))
            {
                parts.Add(
                    "ORCHESTRATION STRATEGY — choose the lightest approach that fits the task:\n\n" +
                    "1. SOLO (default): Handle the task yourself using your tools directly. " +
                    "This is the right choice for most requests — reading, writing, editing, " +
                    "running commands, searching code. Do not escalate unless the task genuinely " +
                    "needs it.\n\n" +
                    "2. SUB-AGENT (Agent tool): Spawn a single sub-agent for a focused sub-task " +
                    "that benefits from an isolated context — e.g. researching a specific topic, " +
                    "reviewing a file in depth, or running a long computation while you continue " +
                    "other work. Use when you need one helper, not a whole team.\n\n" +
                    "3. TEAM (TeamCreate → TeamRun): Build a named team of specialist agents " +
                    "when the task requires multiple coordinated roles — e.g. a coder, a reviewer, " +
                    "and a tester working together. Teams persist across conversations, so create " +
                    "one when the user will reuse it. Use TeamDelegate for quick single-member " +
                    "delegation, TeamRun when the whole team should execute in parallel.\n\n" +
                    "4. SWARM (Swarm tool): Use the full swarm pipeline only for complex, " +
                    "multi-step master prompts that need automatic DAG decomposition, file " +
                    "locking, wave scheduling, and a quality gate — e.g. \"refactor the entire " +
                    "authentication module\" or \"add internationalization to all user-facing strings\". " +
                    "The swarm pays an LLM decomposition cost up front; do not use it for tasks " +
                    "you can decompose yourself.\n\n" +
                    "5. MISSION (Mission tool): Create a mission for long-lived goals that span " +
                    "multiple engine runs and may need re-planning or human approval — e.g. " +
                    "\"migrate the database from Postgres to SQLite\" or \"implement the billing system\". " +
                    "Missions track progress with an event journal and acceptance gates.\n\n" +
                    "Escalate only when the lower level genuinely cannot handle it. " +
                    "A 3-file change is solo work, not a swarm. A focused code review is a " +
                    "sub-agent, not a team. Match the tool to the actual complexity.");
            }
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Returns true if the tool registry contains orchestration tools,
    /// indicating the orchestration strategy section should be included.
    /// </summary>
    private static bool HasOrchestrationTools(IReadOnlyList<ToolDefinition> tools) =>
        tools.Any(t => t.Name is "Agent" or "Swarm" or "TeamCreate" or "TeamRun" or "Mission");
}
