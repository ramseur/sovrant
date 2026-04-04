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

        // Dispatcher (resolved before HelpCommand so HelpCommand can inject it)
        services.AddSingleton<SlashCommandDispatcher>();

        // HelpCommand injects the dispatcher — must come after dispatcher registration
        services.AddSingleton<ISlashCommand, HelpCommand>();

        return services;
    }
}
