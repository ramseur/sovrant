using System.Text.Json;

namespace Sovrant.Agents.Swarm;

/// <summary>Loads <see cref="SwarmConfig"/> from <c>.sovrant/swarm.json</c>.</summary>
public static class SwarmConfigLoader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads config from <c>.sovrant/swarm.json</c> relative to <paramref name="workingDirectory"/>.
    /// Returns defaults (Enabled=false) if file does not exist or is invalid.
    /// </summary>
    public static SwarmConfig Load(string? workingDirectory = null)
    {
        var dir = workingDirectory ?? Directory.GetCurrentDirectory();
        var path = Path.Combine(dir, ".sovrant", "swarm.json");

        if (!File.Exists(path))
            return new SwarmConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SwarmConfig>(json, s_options) ?? new SwarmConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new SwarmConfig();
        }
    }
}
