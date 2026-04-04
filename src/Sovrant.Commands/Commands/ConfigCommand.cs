using System.Globalization;
using System.Text;
using Sovrant.Runtime.Config;

namespace Sovrant.Commands.Commands;

/// <summary>Displays the active Sovrant configuration.</summary>
public sealed class ConfigCommand : ISlashCommand
{
    private readonly SovrantConfig _config;

    public ConfigCommand(SovrantConfig config) => _config = config;

    public string Name => "config";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show active configuration.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Active configuration:");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Model:            {_config.Model}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Max tokens:       {_config.MaxTokens}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Permission mode:  {_config.PermissionMode}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Router mode:      {_config.RouterMode}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Router strategy:  {_config.RouterStrategy}");

        if (_config.BaseUrl is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Base URL:         {_config.BaseUrl}");

        if (_config.McpServers.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  MCP servers ({_config.McpServers.Count}):");
            foreach (var (name, srv) in _config.McpServers)
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {name}: {srv.Command} {string.Join(" ", srv.Args)}");
        }
        else
        {
            sb.AppendLine("  MCP servers:      (none)");
        }

        return Task.FromResult(new SlashCommandResult(sb.ToString().TrimEnd()));
    }
}
