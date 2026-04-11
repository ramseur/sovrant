using System.Globalization;
using System.Text;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Commands.Commands;

/// <summary>Shows the active permission mode and explains available modes.</summary>
public sealed class PermissionsCommand : ISlashCommand
{
    private readonly SovrantConfig _config;

    public PermissionsCommand(SovrantConfig config) => _config = config;

    public string Name => "permissions";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show active permission mode and available modes.";
    public string Category => "Config";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Permissions");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Active mode:** {_config.PermissionMode}");
        sb.AppendLine();
        sb.AppendLine("| Mode | Description | |");
        sb.AppendLine("|------|-------------|-|");

        foreach (var mode in Enum.GetValues<PermissionMode>())
        {
            var active = mode == _config.PermissionMode ? "**Active**" : "";
            var desc = mode switch
            {
                PermissionMode.Default => "Prompt before destructive operations",
                PermissionMode.AcceptEdits => "Auto-accept file edits; prompt for bash and other ops",
                PermissionMode.BypassPermissions => "Allow all operations without prompting",
                PermissionMode.DontAsk => "Never prompt; silently allow everything",
                PermissionMode.Plan => "Read-only planning mode; all writes are blocked",
                _ => string.Empty,
            };
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {mode} | {desc} | {active} |");
        }

        return Task.FromResult(new SlashCommandResult(sb.ToString().TrimEnd()));
    }
}
