using System.Reflection;
using Spectre.Console;

namespace Sovrant.Cli;

/// <summary>Displays the startup banner with ASCII art, version, and tagline.</summary>
internal static class StartupBanner
{
    internal static void Render()
    {
        var version = typeof(StartupBanner).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";

        // Strip +commithash suffix if present.
        var plusIdx = version.IndexOf('+', StringComparison.Ordinal);
        if (plusIdx >= 0)
            version = version[..plusIdx];

        AnsiConsole.Write(new FigletText("Sovrant").Color(Color.Teal));
        AnsiConsole.MarkupLine($"  [grey]v{Markup.Escape(version)}  \u00b7  Multi-provider agentic AI assistant[/]");
        AnsiConsole.WriteLine();
    }
}
