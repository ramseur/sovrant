using Sovrant.Tools.Extended;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Toggles LLM-based web search fallback.
/// Usage: <c>/websearch</c>, <c>/websearch enable</c>, <c>/websearch disable</c>
/// </summary>
public sealed class WebSearchCommand : ISlashCommand
{
    public string Name => "websearch";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Toggle LLM-based web search fallback.";
    public string Category => "Tools";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var trimmed = (args ?? string.Empty).Trim();

        if (trimmed.Equals("enable", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            WebSearchTool.LlmFallbackEnabled = true;
            return Task.FromResult(new SlashCommandResult(
                "LLM web search fallback enabled. The model will search when no API keys are set."));
        }

        if (trimmed.Equals("disable", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            WebSearchTool.LlmFallbackEnabled = false;
            return Task.FromResult(new SlashCommandResult(
                "LLM web search fallback disabled. Only Brave/FireCrawl API keys will be used."));
        }

        var status = WebSearchTool.LlmFallbackEnabled ? "enabled" : "disabled";
        var braveSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BRAVE_API_KEY"));
        var firecrawlSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY"));

        var backends = new List<string>();
        if (braveSet) backends.Add("Brave Search (active)");
        if (firecrawlSet) backends.Add("FireCrawl (active)");
        backends.Add($"LLM fallback ({status})");

        return Task.FromResult(new SlashCommandResult(
            $"Web search backends: {string.Join(", ", backends)}\n\n" +
            "Usage:\n" +
            "  /websearch enable   Enable LLM-based search fallback\n" +
            "  /websearch disable  Disable LLM-based search fallback"));
    }
}
