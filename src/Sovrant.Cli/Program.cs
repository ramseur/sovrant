using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Logging;
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
using System.Text.Json;

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
var ciOpt = new Option<bool>("--ci")
    { Description = "CI mode: machine-readable JSON output, CI permission policy, non-zero exit on error." };

// ── Root command ──────────────────────────────────────────────────────────────
var root = new RootCommand("Sovrant — multi-provider agentic AI assistant.");
root.Add(modelOpt);
root.Add(providerOpt);
root.Add(permModeOpt);
root.Add(sessionOpt);
root.Add(noStreamOpt);
root.Add(ciOpt);

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
    var ciMode = pr.GetValue(ciOpt);
    await using var sp = BuildServices(pr);
    await InitAsync(sp, pr, ct).ConfigureAwait(false);
    var runtime = sp.GetRequiredService<IConversationRuntime>();
    var sessionId = pr.GetValue(sessionOpt);
    if (sessionId is not null)
        await runtime.InitializeSessionAsync(sessionId, ct).ConfigureAwait(false);

    if (ciMode)
    {
        var exitCode = await RunCiTurnAsync(runtime, message!, ct).ConfigureAwait(false);
        Environment.ExitCode = exitCode;
    }
    else
    {
        await RunTurnAsync(runtime, message!, ct).ConfigureAwait(false);
    }
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
    var ciMode = pr.GetValue(ciOpt);

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

    if (ciMode)
    {
        // CI mode: suppress all console logging — output is JSON only.
        services.AddLogging(b => b.AddSovrantLogging(consoleMinOverride: LogLevel.None));
    }
    else
    {
        // Default console to Warning in CLI so logs don't pollute the REPL.
        // File logging always uses the configured SOVRANT_LOG_LEVEL (default: Information).
        // Users can override with SOVRANT_LOG_LEVEL=Debug to see console debug output too.
        var explicitLevel = Environment.GetEnvironmentVariable("SOVRANT_LOG_LEVEL");
        services.AddLogging(b => b.AddSovrantLogging(
            consoleMinOverride: string.IsNullOrEmpty(explicitLevel) ? LogLevel.Warning : null));
    }

    services.AddSovrantRuntime(config);
    services.AddSovrantTools();
    services.AddSovrantCommands();

    if (ciMode)
    {
        // CI mode uses CiPermissionPolicy — auto-approves edits and shell, denies unknown destructive ops.
        services.AddSingleton<IPermissionPolicy>(new CiPermissionPolicy());
        // No interactive input in CI — use a no-op provider that returns empty strings.
        services.AddSingleton<IUserInputProvider, CiUserInputProvider>();
    }
    else
    {
        // Replace the null input provider with the real console one.
        services.AddSingleton<IUserInputProvider, ConsoleUserInputProvider>();
    }

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
            if (cmdResult.InjectAsUserMessage is not null)
                await RunTurnAsync(runtime, cmdResult.InjectAsUserMessage, ct).ConfigureAwait(false);
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

            case RuntimeEvent.ToolUseRequested { ToolName: var toolName, Input: var input }:
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[grey]⚙ {Markup.Escape(toolName)}[/]");
                if (DiffRenderer.IsFileModifyTool(toolName))
                    DiffRenderer.RenderToolInput(toolName, input);
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

async Task<int> RunCiTurnAsync(IConversationRuntime runtime, string message, CancellationToken ct)
{
    var textChunks = new System.Text.StringBuilder();
    var toolCalls = new List<CiToolCallResult>();
    var errors = new List<string>();
    int inputTokens = 0;
    int outputTokens = 0;

    await foreach (var ev in runtime.RunTurnAsync(message, ct).ConfigureAwait(false))
    {
        switch (ev)
        {
            case RuntimeEvent.TextChunk { Text: var text }:
                textChunks.Append(text);
                break;

            case RuntimeEvent.ToolUseRequested { ToolName: var toolName, ToolUseId: var id }:
                toolCalls.Add(new CiToolCallResult(id, toolName, null, false));
                break;

            case RuntimeEvent.ToolResult { ToolUseId: var id, ToolName: var toolName, Content: var content, IsError: var isErr }:
                toolCalls.Add(new CiToolCallResult(id, toolName, content, isErr));
                if (isErr)
                    errors.Add($"{toolName}: {content}");
                break;

            case RuntimeEvent.PermissionDenied { ToolName: var toolName, Reason: var reason }:
                errors.Add($"permission_denied: {toolName}: {reason}");
                break;

            case RuntimeEvent.TurnComplete { InputTokens: var inTok, OutputTokens: var outTok }:
                inputTokens = inTok;
                outputTokens = outTok;
                break;

            case RuntimeEvent.RuntimeError { Message: var msg }:
                errors.Add(msg);
                break;
        }
    }

    var result = new CiOutput(
        success: errors.Count == 0,
        text: textChunks.ToString(),
        tool_calls: toolCalls,
        errors: errors,
        input_tokens: inputTokens,
        output_tokens: outputTokens);

    var json = JsonSerializer.Serialize(result, CiJsonOptions.Instance);
    Console.WriteLine(json);

    return errors.Count == 0 ? 0 : 1;
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

// ── CI output types ──────────────────────────────────────────────────────────

sealed record CiOutput(
    bool success,
    string text,
    List<CiToolCallResult> tool_calls,
    List<string> errors,
    int input_tokens,
    int output_tokens);

sealed record CiToolCallResult(
    string id,
    string tool_name,
    string? content,
    bool is_error);

static class CiJsonOptions
{
    internal static readonly JsonSerializerOptions Instance = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
