using System.Globalization;
using System.Text;
using Sovrant.Api.Config;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Commands.Commands;

/// <summary>Displays the active Sovrant configuration.</summary>
public sealed class ConfigCommand : ISlashCommand
{
    private readonly SovrantConfig _config;
    private readonly IMcpServerStore _mcpStore;
    private readonly RouterOptions _routerOptions;

    public ConfigCommand(SovrantConfig config, IMcpServerStore mcpStore, RouterOptions routerOptions)
    {
        _config = config;
        _mcpStore = mcpStore;
        _routerOptions = routerOptions;
    }

    public string Name => "config";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show active configuration.";
    public string Category => "Config";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Active Configuration");
        sb.AppendLine();
        sb.AppendLine("| Setting | Value |");
        sb.AppendLine("|---------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Model | {_config.Model} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Max tokens | {_config.MaxTokens} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Permission mode | {_config.PermissionMode} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Router mode | {_routerOptions.Mode} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Router strategy | {_routerOptions.Strategy} |");

        if (_config.BaseUrl is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"| Base URL | {_config.BaseUrl} |");

        var mcpServers = await _mcpStore.GetAllAsync(ct).ConfigureAwait(false);
        if (mcpServers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"## MCP Servers ({mcpServers.Count})");
            sb.AppendLine();
            sb.AppendLine("| Name | Command |");
            sb.AppendLine("|------|---------|");
            foreach (var (name, srv) in mcpServers)
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {name} | {srv.Command} {string.Join(" ", srv.Args)} |");
        }

        return new SlashCommandResult(sb.ToString().TrimEnd());
    }
}
