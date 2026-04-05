using Microsoft.Extensions.DependencyInjection;
using Sovrant.Commands.Commands;

namespace Sovrant.Commands;

/// <summary>Extension methods for registering all Sovrant slash commands.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SlashCommandDispatcher"/>, <see cref="TokenUsageTracker"/>,
    /// and all built-in <see cref="ISlashCommand"/> implementations.
    /// </summary>
    public static IServiceCollection AddSovrantCommands(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Session-scoped singletons
        services.AddSingleton<TokenUsageTracker>();

        // Commands (registered as ISlashCommand so dispatcher picks them all up)
        // HelpCommand depends on SlashCommandDispatcher — registered separately below.
        services.AddSingleton<ISlashCommand, ExitCommand>();
        services.AddSingleton<ISlashCommand, ClearCommand>();
        services.AddSingleton<ISlashCommand, ModelCommand>();
        services.AddSingleton<ISlashCommand, ConfigCommand>();
        services.AddSingleton<ISlashCommand, SessionCommand>();
        services.AddSingleton<ISlashCommand, StatusCommand>();
        services.AddSingleton<ISlashCommand, CompactCommand>();
        services.AddSingleton<ISlashCommand, PermissionsCommand>();
        services.AddSingleton<ISlashCommand, CostCommand>();
        services.AddSingleton<ISlashCommand, ResumeCommand>();
        services.AddSingleton<ISlashCommand, ProviderCommand>();
        services.AddSingleton<ISlashCommand, MemoryCommand>();
        services.AddSingleton<ISlashCommand, RememberCommand>();
        services.AddSingleton<ISlashCommand, ForgetCommand>();
        services.AddSingleton<ISlashCommand, EvalCommand>();

        // HelpCommand injects IEnumerable<ISlashCommand> directly — must come after all other commands
        services.AddSingleton<ISlashCommand, HelpCommand>();

        // Dispatcher enumerates all registered ISlashCommand implementations
        services.AddSingleton<SlashCommandDispatcher>();

        return services;
    }
}
