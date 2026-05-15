using Microsoft.Extensions.Configuration;

namespace Sovrant.Runtime.Config;

/// <summary>
/// Loads <see cref="SovrantConfig"/> from a layered set of sources:
/// <list type="number">
///   <item><c>~/.sovrant/settings.json</c> – user-level defaults</item>
///   <item><c>.sovrant/settings.json</c> – project-level settings</item>
///   <item><c>.sovrant/settings.local.json</c> – local overrides (git-ignored)</item>
///   <item>Environment variables prefixed with <c>SOVRANT_</c></item>
/// </list>
/// </summary>
public static class ConfigLoader
{
    /// <summary>Loads and returns the merged <see cref="SovrantConfig"/>.</summary>
    public static SovrantConfig Load()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userSettings = Path.Combine(userHome, ".sovrant", "settings.json");
        var projectSettings = Path.Combine(".sovrant", "settings.json");
        var projectLocalSettings = Path.Combine(".sovrant", "settings.local.json");

        var config = new ConfigurationBuilder()
            .AddJsonFile(userSettings, optional: true, reloadOnChange: false)
            .AddJsonFile(projectSettings, optional: true, reloadOnChange: false)
            .AddJsonFile(projectLocalSettings, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "SOVRANT_")
            .Build();

        var result = new SovrantConfig();
        config.Bind(result);
        return result;
    }

    /// <summary>Builds a layered <see cref="IConfiguration"/> rooted at the Sovrant config sources.</summary>
    public static IConfiguration BuildConfiguration()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userSettings = Path.Combine(userHome, ".sovrant", "settings.json");
        var projectSettings = Path.Combine(".sovrant", "settings.json");
        var projectLocalSettings = Path.Combine(".sovrant", "settings.local.json");

        return new ConfigurationBuilder()
            .AddJsonFile(userSettings, optional: true, reloadOnChange: false)
            .AddJsonFile(projectSettings, optional: true, reloadOnChange: false)
            .AddJsonFile(projectLocalSettings, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "SOVRANT_")
            .Build();
    }
}
