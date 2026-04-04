using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Cli;
using Sovrant.Commands;
using Sovrant.Runtime;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;
using Sovrant.Tools;
using Sovrant.Tools.Extended;
using Spectre.Console;
using System.CommandLine;

// ── Global options ────────────────────────────────────────────────────────────
var modelOpt = new Option<string?>("--model")
    { Description = "Override the active LLM model." };
var providerOpt = new Option<string?>("--provider")
    { Description = "Pin to a specific provider by name." };
var permModeOpt = new Option<string?>("--permission-mode")
    { Description = "Permission mode: default | acceptEdits | bypassPermissions | dontAsk | plan." };
var sessionOpt = new Option<string?>("--session")
    { Description = "Session ID to resume or create." };
var noStreamOpt = new Option<bool>("--no-stream")
    { Description = "Buffer the full response before printing." };

// ── Root command ──────────────────────────────────────────────────────────────
var root = new RootCommand("Sovrant — multi-provider agentic AI assistant.");
root.Add(modelOpt);
root.Add(providerOpt);
root.Add(permModeOpt);
root.Add(sessionOpt);
root.Add(noStreamOpt);

// ── 'status' subcommand ───────────────────────────────────────────────────────
var statusCmd = new Command("status", "Show provider health and routing statistics.");
statusCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    var router = sp.GetRequiredService<ISmartRouter>();
    await router.InitializeAsync(ct).ConfigureAwait(false);
    PrintStatus(router.GetStatus());
});
root.Add(statusCmd);

// ── 'prompt' subcommand ───────────────────────────────────────────────────────
var messageArg = new Argument<string>("message") { Description = "The message to send to the assistant." };
var promptCmd = new Command("prompt", "Send a single message and exit.");
promptCmd.Add(messageArg);
promptCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var message = pr.GetValue(messageArg);
    await using var sp = BuildServices(pr);
    await InitAsync(sp, pr, ct).ConfigureAwait(false);
    var runtime = sp.GetRequiredService<IConversationRuntime>();
    var sessionId = pr.GetValue(sessionOpt);
    if (sessionId is not null)
        await runtime.InitializeSessionAsync(sessionId, ct).ConfigureAwait(false);
    await RunTurnAsync(runtime, message!, ct).ConfigureAwait(false);
});
root.Add(promptCmd);

// ── REPL (default handler) ────────────────────────────────────────────────────
root.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    await InitAsync(sp, pr, ct).ConfigureAwait(false);
    var runtime = sp.GetRequiredService<IConversationRuntime>();
    var sessionId = pr.GetValue(sessionOpt);
    if (sessionId is not null)
        await runtime.InitializeSessionAsync(sessionId, ct).ConfigureAwait(false);
    var dispatcher = sp.GetRequiredService<SlashCommandDispatcher>();
    await RunReplAsync(runtime, dispatcher, ct).ConfigureAwait(false);
});

var parseResult = root.Parse(args);
return await parseResult.InvokeAsync(parseResult.InvocationConfiguration, CancellationToken.None)
    .ConfigureAwait(false);

// ── Local helpers ─────────────────────────────────────────────────────────────

ServiceProvider BuildServices(ParseResult pr)
{
    var config = ConfigLoader.Load();

    // Apply CLI overrides on top of file/env config.
    var model = pr.GetValue(modelOpt);
    var permModeRaw = pr.GetValue(permModeOpt);

    if (model is not null || permModeRaw is not null)
    {
        var pm = config.PermissionMode;
        if (permModeRaw is not null)
            Enum.TryParse(permModeRaw, ignoreCase: true, out pm);

        config = new SovrantConfig
        {
            Model = model ?? config.Model,
            MaxTokens = config.MaxTokens,
            PermissionMode = pm,
            RouterMode = config.RouterMode,
            RouterStrategy = config.RouterStrategy,
            BaseUrl = config.BaseUrl,
            ApiKey = config.ApiKey,
            McpServers = config.McpServers,
        };
    }

    var services = new ServiceCollection();
    services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
    services.AddSovrantRuntime(config);
    services.AddSovrantTools();
    services.AddSovrantCommands();

    // Replace the null input provider with the real console one.
    services.AddSingleton<IUserInputProvider, ConsoleUserInputProvider>();

    var sp = services.BuildServiceProvider();

    // Seed the tool registry with all discovered ITool implementations.
    sp.GetRequiredService<ToolRegistrar>().RegisterAll();

    return sp;
}

async Task InitAsync(ServiceProvider sp, ParseResult pr, CancellationToken ct)
{
    // Ping all providers, populate health and latency data.
    var router = sp.GetRequiredService<ISmartRouter>();
    await router.InitializeAsync(ct).ConfigureAwait(false);

    // Pin to a specific provider if requested.
    var providerName = pr.GetValue(providerOpt);
    if (providerName is not null)
        await router.PinProviderAsync(providerName, ct).ConfigureAwait(false);

    // Connect to MCP servers and register their tools.
    await sp.InitializeRuntimeAsync(ct).ConfigureAwait(false);

}

async Task RunReplAsync(IConversationRuntime runtime, SlashCommandDispatcher dispatcher, CancellationToken ct)
{
    AnsiConsole.MarkupLine("[bold green]Sovrant[/]  Type [grey]/help[/] for commands, [grey]/exit[/] to quit.");

    while (!ct.IsCancellationRequested)
    {
        AnsiConsole.Markup("[bold cyan]>[/] ");
        var line = Console.ReadLine();
        if (line is null) break; // Ctrl+Z / EOF

        line = line.Trim();
        if (string.IsNullOrEmpty(line)) continue;

        // Try to dispatch as a slash command first.
        var cmdResult = await dispatcher.TryDispatchAsync(line, ct).ConfigureAwait(false);
        if (cmdResult is not null)
        {
            if (cmdResult.Output is not null)
                AnsiConsole.WriteLine(cmdResult.Output);
            if (cmdResult.ShouldExit)
                break;
            if (cmdResult.ShouldClearHistory)
                runtime.Reset();
            continue;
        }

        // Otherwise send to the LLM.
        await RunTurnAsync(runtime, line, ct).ConfigureAwait(false);
    }
}

async Task RunTurnAsync(IConversationRuntime runtime, string message, CancellationToken ct)
{
    await foreach (var ev in runtime.RunTurnAsync(message, ct).ConfigureAwait(false))
    {
        switch (ev)
        {
            case RuntimeEvent.TextChunk { Text: var text }:
                AnsiConsole.Write(text);
                break;

            case RuntimeEvent.ToolUseRequested { ToolName: var toolName }:
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[grey]⚙ {Markup.Escape(toolName)}[/]");
                break;

            case RuntimeEvent.PermissionDenied { ToolName: var toolName, Reason: var reason }:
                AnsiConsole.MarkupLine($"[yellow]⚠ {Markup.Escape(toolName)}: {Markup.Escape(reason)}[/]");
                break;

            case RuntimeEvent.TurnComplete { InputTokens: var inTok, OutputTokens: var outTok }:
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[grey dim]({inTok}↑ {outTok}↓ tokens)[/]");
                break;

            case RuntimeEvent.RuntimeError { Message: var msg }:
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(msg)}[/]");
                break;
        }
    }

    AnsiConsole.WriteLine();
}

void PrintStatus(IReadOnlyList<ProviderStatus> statuses)
{
    if (statuses.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No providers configured.[/]");
        return;
    }

    var table = new Table();
    table.AddColumn("Provider");
    table.AddColumn("Healthy");
    table.AddColumn("Latency");
    table.AddColumn("Requests");
    table.AddColumn("Errors");
    table.AddColumn("Score");

    foreach (var s in statuses)
    {
        var healthy = s.Healthy ? "[green]yes[/]" : "[red]NO[/]";
        var latency = s.RequestCount > 0
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{s.LatencyMs:F0}ms")
            : "—";
        table.AddRow(
            Markup.Escape(s.Name),
            healthy,
            latency,
            s.RequestCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            s.ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Markup.Escape(s.Score));
    }

    AnsiConsole.Write(table);
}
