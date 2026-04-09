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
        }

        return string.Join("\n\n", parts);
    }
}
