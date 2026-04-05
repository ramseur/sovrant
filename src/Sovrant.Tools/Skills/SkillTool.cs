using System.Globalization;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Skills;

/// <summary>
/// Loads and returns a skill prompt from a Markdown file.
/// Looks in <c>.sovrant/skills/{name}.md</c> (project-local) first,
/// then <c>~/.sovrant/skills/{name}.md</c> (global).
/// Substitutes <c>$ARGUMENTS</c> with the supplied arguments string.
/// </summary>
public sealed class SkillTool : ITool
{
    private static readonly string GlobalSkillsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "skills");

    private static string ProjectSkillsDir =>
        Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "skills");

    private static readonly ToolDefinition s_definition = new("Skill", CreateSchema())
    {
        Description = "Loads a reusable skill prompt from a .md file and returns it as a prompt string. " +
                      "Searches .sovrant/skills/{name}.md (project) then ~/.sovrant/skills/{name}.md (global).",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var name = input.GetStringProp("name");
        if (string.IsNullOrWhiteSpace(name))
            return "Error: name is required.";

        // Sanitise: no path traversal
        if (name.Contains('/', StringComparison.Ordinal) ||
            name.Contains('\\', StringComparison.Ordinal) ||
            name.Contains("..", StringComparison.Ordinal) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Error: name must be a plain filename without path separators or special characters.";

        var args = input.GetStringProp("args");

        var projectPath = Path.Combine(ProjectSkillsDir, string.Create(CultureInfo.InvariantCulture, $"{name}.md"));
        var globalPath  = Path.Combine(GlobalSkillsDir,  string.Create(CultureInfo.InvariantCulture, $"{name}.md"));

        // Defense-in-depth: verify resolved paths stay within the skills directories.
        if (!Path.GetFullPath(projectPath).StartsWith(Path.GetFullPath(ProjectSkillsDir), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(globalPath).StartsWith(Path.GetFullPath(GlobalSkillsDir), StringComparison.OrdinalIgnoreCase))
            return "Error: name resolves outside the skills directory.";

        var path = File.Exists(projectPath) ? projectPath
                 : File.Exists(globalPath)  ? globalPath
                 : null;

        if (path is null)
            return $"Error: skill '{name}' not found. Create it at {projectPath} or {globalPath}.";

        try
        {
            var content = (await File.ReadAllTextAsync(path, ct).ConfigureAwait(false)).Trim();
            if (content.Contains("$ARGUMENTS", StringComparison.Ordinal))
                content = content.Replace("$ARGUMENTS", args, StringComparison.Ordinal);
            else if (!string.IsNullOrEmpty(args))
                content = $"{content}\n\n{args}";

            return content;
        }
        catch (IOException ex)
        {
            return $"Error reading skill file: {ex.Message}";
        }
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Skill name (filename without .md extension)."},
                "args": {"type": "string", "description": "Optional arguments substituted for $ARGUMENTS in the skill template."}
            },
            "required": ["name"]
        }
        """).RootElement;
}
