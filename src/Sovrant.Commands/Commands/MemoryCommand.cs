using System.Globalization;
using System.Text;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Displays or initialises the Sovrant memory files that are injected into the system prompt.
/// <list type="bullet">
///   <item><c>/memory</c> — show both memory files and their contents.</item>
///   <item><c>/memory edit</c> — create the project memory file if absent and show its path.</item>
///   <item><c>/memory edit global</c> — create the global memory file if absent and show its path.</item>
/// </list>
/// </summary>
public sealed class MemoryCommand : ISlashCommand
{
    private static readonly string GlobalPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "memory.md");

    private static string ProjectPath =>
        Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "memory.md");

    public string Name => "memory";
    public IReadOnlyList<string> Aliases => ["mem"];
    public string Description => "Show or edit Sovrant memory files injected into the system prompt.";
    public string Category => "Memory";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var arg = args.Trim();

        if (arg.StartsWith("edit", StringComparison.OrdinalIgnoreCase))
        {
            var global = arg.Contains("global", StringComparison.OrdinalIgnoreCase);
            var path = global ? GlobalPath : ProjectPath;
            EnsureFile(path, global
                ? "# Global memory\n\nAdd preferences, coding style, and tool hints here.\nThis file is included in the system prompt for every Sovrant session.\n"
                : "# Project memory\n\nAdd project-specific conventions, architecture notes, and context here.\nThis file is included in the system prompt whenever Sovrant runs from this directory.\n");
            return Task.FromResult(new SlashCommandResult(
                $"Memory file: {path}\nEdit it with your preferred editor — changes take effect on the next session start."));
        }

        // Default: show both files
        var sb = new StringBuilder();
        AppendFileSection(sb, "Global memory", GlobalPath);
        AppendFileSection(sb, "Project memory", ProjectPath);
        return Task.FromResult(new SlashCommandResult(sb.ToString().TrimEnd()));
    }

    private static void AppendFileSection(StringBuilder sb, string label, string path)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"## {label}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Path:** {path}");
        sb.AppendLine();
        if (!File.Exists(path))
        {
            sb.AppendLine("*(not found -- run /memory edit to create)*");
        }
        else
        {
            var content = File.ReadAllText(path).Trim();
            sb.AppendLine(string.IsNullOrEmpty(content) ? "*(empty)*" : content);
        }
        sb.AppendLine();
    }

    private static void EnsureFile(string path, string template)
    {
        if (File.Exists(path)) return;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(path, template);
    }
}
