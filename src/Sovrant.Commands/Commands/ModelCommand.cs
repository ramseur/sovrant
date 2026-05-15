using Sovrant.Runtime.Config;

namespace Sovrant.Commands.Commands;

/// <summary>Shows the active model. Changing it requires restarting with --model.</summary>
public sealed class ModelCommand : ISlashCommand
{
    private readonly SovrantConfig _config;

    public ModelCommand(SovrantConfig config) => _config = config;

    public string Name => "model";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show the active model (use --model to change).";
    public string Category => "Config";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(args))
            return Task.FromResult(new SlashCommandResult(
                $"Active model: {_config.Model}\n" +
                $"To switch models, restart with: sovrant --model {args}"));

        return Task.FromResult(new SlashCommandResult($"Active model: {_config.Model}"));
    }
}
