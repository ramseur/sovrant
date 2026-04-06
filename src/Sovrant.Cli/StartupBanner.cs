using System.Reflection;
using Spectre.Console;

namespace Sovrant.Cli;

/// <summary>Displays the startup banner with ASCII art, version, user, and session info.</summary>
internal static class StartupBanner
{
    internal static void Render(string? sessionId = null)
    {
        var version = typeof(StartupBanner).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";

        // Strip +commithash suffix if present.
        var plusIdx = version.IndexOf('+', StringComparison.Ordinal);
        if (plusIdx >= 0)
            version = version[..plusIdx];

        var user = Environment.UserName;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new FigletText("Sovrant").Color(Color.Teal));
        AnsiConsole.MarkupLine($"  [grey]v{Markup.Escape(version)}  \u00b7  Multi-provider agentic AI assistant[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [teal]User:[/]    [white]{Markup.Escape(user)}[/]");
        if (sessionId is not null)
            AnsiConsole.MarkupLine($"  [teal]Session:[/] [white]{Markup.Escape(sessionId)}[/]");
        AnsiConsole.WriteLine();
    }
}
