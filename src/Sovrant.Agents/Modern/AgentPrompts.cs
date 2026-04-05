using Sovrant.Agents.Models;

namespace Sovrant.Agents.Modern;

/// <summary>
/// Generates role-specific system prompts for <see cref="SovrantAgent"/> instances.
/// </summary>
public static class AgentPrompts
{
    /// <summary>
    /// Returns a system prompt tailored to the given <paramref name="role"/>,
    /// with optional <paramref name="customInstructions"/> appended.
    /// </summary>
    public static string GetSystemPrompt(AgentRole role, string? customInstructions = null)
    {
        var basePrompt = role switch
        {
            AgentRole.Planner =>
                "You are a planning agent. Your job is to decompose complex tasks into clear, " +
                "actionable sub-tasks. Do not write code directly — instead describe what needs " +
                "to be done, which files to modify, and in what order. Output a numbered plan.",

            AgentRole.Coder =>
                "You are a coding agent. Write clean, correct, well-structured code. " +
                "Follow existing conventions in the codebase. Keep changes minimal and focused. " +
                "Do not add unnecessary abstractions or features beyond what is requested.",

            AgentRole.Reviewer =>
                "You are a code review agent. Analyze code for bugs, security issues, " +
                "performance problems, and style inconsistencies. Be specific about what is wrong " +
                "and suggest concrete fixes. Do not rewrite code — point out issues clearly.",

            AgentRole.Executor =>
                "You are an execution agent. Run commands, scripts, and processes as instructed. " +
                "Report output faithfully. If a command fails, report the error and suggest fixes.",

            AgentRole.Supervisor =>
                "You are a supervisor agent. Coordinate work between other agents. " +
                "Assign tasks, review results, and decide next steps. Summarize progress clearly.",

            _ => // General
                "You are a general-purpose AI assistant. Help with whatever task is given to you.",
        };

        if (!string.IsNullOrWhiteSpace(customInstructions))
            return basePrompt + "\n\n" + customInstructions;

        return basePrompt;
    }
}
