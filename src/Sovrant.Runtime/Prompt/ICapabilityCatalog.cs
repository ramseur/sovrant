namespace Sovrant.Runtime.Prompt;

/// <summary>
/// Provides a snapshot of available skills and agent templates so
/// <see cref="Conversation.ConversationRuntime"/> can include them in the
/// system prompt without taking a direct dependency on Sovrant.Tools or
/// Sovrant.Agents.
/// </summary>
public interface ICapabilityCatalog
{
    /// <summary>
    /// All registered skills. Each entry is (name, description, trigger)
    /// where trigger may be null or empty when the skill has no slash-command.
    /// </summary>
    IReadOnlyList<(string Name, string Description, string? Trigger)> Skills { get; }

    /// <summary>
    /// Names of all registered agent templates (e.g. "coder", "security-reviewer").
    /// </summary>
    IReadOnlyList<string> AgentTemplateNames { get; }

    /// <summary>
    /// User-authored tool guides (kind='tools' knowledge pages). Each entry carries the
    /// slug, name, description, category, and full guide body so the model can read the
    /// guidance when deciding how and when to invoke a specific tool.
    /// Empty when no guides have been authored.
    /// </summary>
    IReadOnlyList<(string Slug, string Name, string Description, string Category, string Body)> ToolGuides { get; }
}
