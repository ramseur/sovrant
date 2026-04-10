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
using Sovrant.Runtime.Storage;
using Sovrant.Agents;
using Sovrant.Tools;
using Sovrant.Tools.Extended;
using Sovrant.McpServer;
using Microsoft.Extensions.Hosting;
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
var dbPathOpt = new Option<string?>("--db-path")
    { Description = "Override the SQLite database path (also settable via SOVRANT_DB_PATH env var)." };

// ── Root command ──────────────────────────────────────────────────────────────
var root = new RootCommand("Sovrant — multi-provider agentic AI assistant.");
root.Add(modelOpt);
root.Add(providerOpt);
root.Add(permModeOpt);
root.Add(sessionOpt);
root.Add(noStreamOpt);
root.Add(ciOpt);
root.Add(dbPathOpt);

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

// ── 'router' subcommand group ────────────────────────────────────────────────
var routerCmd = new Command("router", "Intent-aware routing inspection and management.");

var routerModelsCmd = new Command("models", "Show discovered models and their tier assignments.");
routerModelsCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    await sp.InitializeRuntimeAsync(ct).ConfigureAwait(false);
    var router = sp.GetRequiredService<ISmartRouter>();
    await router.InitializeAsync(ct).ConfigureAwait(false);

    var tierResolver = sp.GetService<Sovrant.Api.Routing.IModelTierResolver>();
    if (tierResolver is null)
    {
        AnsiConsole.MarkupLine("[yellow]No tier resolver configured.[/]");
        return;
    }

    tierResolver.Rebuild();
    var assignments = tierResolver.GetTierAssignments();
    var routingConfig = sp.GetService<Sovrant.Api.Routing.RoutingConfig>();

    AnsiConsole.MarkupLine($"[bold]Intent routing:[/] {(router.IntentRoutingEnabled ? "[green]enabled[/]" : "[grey]disabled[/]")}");
    if (routingConfig is not null)
        AnsiConsole.MarkupLine($"[bold]Default tier:[/]   {Markup.Escape(routingConfig.DefaultTier)}");
    AnsiConsole.WriteLine();

    string[] tierOrder = [Sovrant.Api.Routing.ModelTier.Fast, Sovrant.Api.Routing.ModelTier.Standard, Sovrant.Api.Routing.ModelTier.High];
    foreach (var tier in tierOrder)
    {
        if (!assignments.TryGetValue(tier, out var candidates) || candidates.Count == 0)
        {
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(tier)}:[/] [grey](empty — will collapse to next available tier)[/]");
            continue;
        }

        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(tier)}:[/] {candidates.Count} model(s)");
        var table = new Table().AddColumns("Model", "Score", "Default");
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            table.AddRow(
                Markup.Escape(c.ModelId),
                c.Score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                i == 0 ? "[green]\u2713[/]" : "");
        }
        AnsiConsole.Write(table);
    }
});
routerCmd.Add(routerModelsCmd);

var routerStatusCmd = new Command("status", "Show intent routing configuration and tier defaults.");
routerStatusCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    var routingConfig = sp.GetService<Sovrant.Api.Routing.RoutingConfig>()
        ?? new Sovrant.Api.Routing.RoutingConfig();

    AnsiConsole.MarkupLine($"[bold]Intent routing:[/]      {(routingConfig.IntentRouting ? "[green]enabled[/]" : "[grey]disabled[/]")}");
    AnsiConsole.MarkupLine($"[bold]Default tier:[/]        {Markup.Escape(routingConfig.DefaultTier)}");
    AnsiConsole.MarkupLine($"[bold]Auto-tier assignment:[/] {(routingConfig.AutoTierAssignment ? "[green]yes[/]" : "[grey]no[/]")}");
    AnsiConsole.MarkupLine($"[bold]Free models only:[/]    {(routingConfig.FreeModelsOnly ? "[green]yes[/]" : "[grey]no[/]")}");
    AnsiConsole.MarkupLine($"[bold]Escalation:[/]          {(routingConfig.Escalation ? "[green]enabled[/]" : "[grey]disabled[/]")}");
    AnsiConsole.MarkupLine($"[bold]Max escalations:[/]     {routingConfig.MaxEscalationsPerTurn}");

    if (routingConfig.TierModels.Count > 0)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Tier → Model mappings:[/]");
        foreach (var (tier, model) in routingConfig.TierModels)
            AnsiConsole.MarkupLine($"  {Markup.Escape(tier)} → {Markup.Escape(model)}");
    }

    if (routingConfig.CustomRules.Count > 0)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Custom rules:[/] {routingConfig.CustomRules.Count}");
        foreach (var rule in routingConfig.CustomRules)
            AnsiConsole.MarkupLine($"  /{Markup.Escape(rule.Pattern)}/ → {Markup.Escape(rule.Tier)}");
    }

    await Task.CompletedTask.ConfigureAwait(false);
});
routerCmd.Add(routerStatusCmd);
root.Add(routerCmd);

// ── 'prompt' subcommand ───────────────────────────────────────────────────────
var messageArg = new Argument<string>("message") { Description = "The message to send to the assistant." };
var promptCmd = new Command("prompt", "Send a single message and exit.");
promptCmd.Add(messageArg);
promptCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var message = pr.GetValue(messageArg);
    var ciMode = pr.GetValue(ciOpt);

    if (!ciMode)
    {
        var config = ConfigLoader.Load();
        var keyError = ApiKeyValidator.Validate(config);
        if (keyError is not null)
        {
            AnsiConsole.MarkupLine("[red bold]Configuration Error[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(keyError);
            Environment.ExitCode = 1;
            return;
        }
    }

    await using var sp = BuildServices(pr);
    await InitAsync(sp, pr, ct).ConfigureAwait(false);
    var runtime = sp.GetRequiredService<IConversationRuntime>();
    var sessionId = pr.GetValue(sessionOpt);
    if (sessionId is not null)
        await runtime.InitializeSessionAsync(sessionId, ownerUserId: null, ct).ConfigureAwait(false);

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

// ── 'mcp-server' subcommand ───────────────────────────────────────────────────
var mcpTokenOpt = new Option<string?>("--token")
    { Description = "Bearer token required by SOVRANT_MCP_TOKEN. Must match the server-side env var." };

var mcpCmd = new Command("mcp-server", "Run as an MCP server on stdio for IDE integration (VS Code, Cursor, etc.).");
mcpCmd.Add(mcpTokenOpt);
mcpCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    // ── Token validation ──────────────────────────────────────────────────
    // If SOVRANT_MCP_TOKEN is set, the caller must provide a matching --token.
    // stderr is used for diagnostics so it doesn't corrupt the stdout JSON-RPC transport.
    var tokenError = McpTokenValidator.Validate(pr.GetValue(mcpTokenOpt));
    if (tokenError is not null)
    {
        await Console.Error.WriteLineAsync($"sovrant mcp-server: {tokenError}").ConfigureAwait(false);
        Environment.ExitCode = 1;
        return;
    }

    var config = ConfigLoader.Load();
    var model = pr.GetValue(modelOpt);

    // Force dontAsk permission mode — MCP server is non-interactive.
    config = new SovrantConfig
    {
        Model = model ?? config.Model,
        MaxTokens = config.MaxTokens,
        PermissionMode = PermissionMode.DontAsk,
        RouterMode = config.RouterMode,
        RouterStrategy = config.RouterStrategy,
        BaseUrl = config.BaseUrl,
        ApiKey = config.ApiKey,
        McpServers = config.McpServers,
        CompactThreshold = config.CompactThreshold,
    };

    var host = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            // Suppress console logging — stdout is the JSON-RPC transport.
            services.AddLogging(b => b.AddSovrantLogging(consoleMinOverride: LogLevel.None));
            services.AddSovrantRuntime(config);
            services.AddSovrantTools();
            services.AddSingleton<IPermissionPolicy>(new CiPermissionPolicy());
            services.AddSingleton<IUserInputProvider, CiUserInputProvider>();
            services.AddSovrantMcpServer();
        })
        .Build();

    // Seed tools and initialize runtime (MCP clients, router) before accepting connections.
    host.Services.GetRequiredService<ToolRegistrar>().RegisterAll();
    await host.Services.InitializeRuntimeAsync(ct).ConfigureAwait(false);

    // Pin to a specific provider if requested.
    var providerName = pr.GetValue(providerOpt);
    if (providerName is not null)
    {
        var router = host.Services.GetRequiredService<ISmartRouter>();
        await router.PinProviderAsync(providerName, ct).ConfigureAwait(false);
    }

    // Block on stdio until the IDE closes the pipe.
    await host.RunAsync(ct).ConfigureAwait(false);
});
root.Add(mcpCmd);

// ── 'swarm' subcommand ───────────────────────────────────────────────────────
// The positional `task` argument is *optional* because it can also come from a
// file via --file/-f. Long master prompts (multi-paragraph design briefs) are
// painful to paste on a terminal, especially with embedded quotes/backticks
// that the shell mangles, so reading from disk is the more accurate path.
var swarmTaskArg = new Argument<string?>("task")
    { Description = "The task to decompose and execute via swarm. Optional when --file is set.", Arity = ArgumentArity.ZeroOrOne };
var swarmFileOpt = new Option<string?>("--file", "-f")
    { Description = "Path to a .md or .txt file whose contents are the master prompt. Mutually exclusive with the positional task argument." };
var swarmBudgetOpt = new Option<int?>("--budget") { Description = "Override the token budget." };
var swarmMaxAgentsOpt = new Option<int?>("--max-agents") { Description = "Override max concurrent agents." };
var swarmDryRunOpt = new Option<bool>("--dry-run") { Description = "Show decomposed plan without executing." };

var swarmCmd = new Command("swarm", "Decompose and execute a task via parallel agent swarm.");
swarmCmd.Add(swarmTaskArg);
swarmCmd.Add(swarmFileOpt);
swarmCmd.Add(swarmBudgetOpt);
swarmCmd.Add(swarmMaxAgentsOpt);
swarmCmd.Add(swarmDryRunOpt);
swarmCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    await InitAsync(sp, pr, ct).ConfigureAwait(false);

    var swarmConfig = sp.GetRequiredService<Sovrant.Agents.Swarm.SwarmConfig>();
    if (!swarmConfig.Enabled)
    {
        AnsiConsole.MarkupLine("[red]Swarm orchestration is disabled.[/] Enable in .sovrant/swarm.json.");
        Environment.ExitCode = 1;
        return;
    }

    // Resolve the task: --file wins, then positional, then error.
    var positionalTask = pr.GetValue(swarmTaskArg);
    var filePath = pr.GetValue(swarmFileOpt);
    string task;
    if (!string.IsNullOrWhiteSpace(filePath))
    {
        if (!string.IsNullOrWhiteSpace(positionalTask))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] pass either a task argument or --file, not both.");
            Environment.ExitCode = 1;
            return;
        }
        try
        {
            task = await Sovrant.Agents.Swarm.SwarmPromptFile.LoadAsync(filePath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            Environment.ExitCode = 1;
            return;
        }
        AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
            $"[grey dim]Loaded {task.Length} chars from {Markup.Escape(Sovrant.Agents.Swarm.SwarmPromptFile.ResolvePath(filePath))}[/]");
    }
    else if (!string.IsNullOrWhiteSpace(positionalTask))
    {
        task = positionalTask;
    }
    else
    {
        AnsiConsole.MarkupLine("[red]Error:[/] provide a task argument or --file <path>.");
        Environment.ExitCode = 1;
        return;
    }

    var dryRun = pr.GetValue(swarmDryRunOpt);

    var decomposer = sp.GetRequiredService<Sovrant.Agents.Swarm.ISwarmDecomposer>();
    var orchestrator = sp.GetRequiredService<Sovrant.Agents.Swarm.SwarmOrchestrator>();
    var qualityGate = sp.GetRequiredService<Sovrant.Agents.Swarm.SwarmQualityGate>();
    var stateTracker = sp.GetRequiredService<Sovrant.Agents.Swarm.SwarmStateTracker>();

    AnsiConsole.MarkupLine("[bold]Decomposing task...[/]");
    var plan = await decomposer.DecomposeAsync(task, swarmConfig, ct).ConfigureAwait(false);
    AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
        $"[green]Plan:[/] {plan.Tasks.Count} tasks across {plan.WaveCount} waves");

    if (dryRun)
    {
        for (var w = 0; w < plan.WaveCount; w++)
        {
            AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture, $"\n[bold]Wave {w}[/]");
            foreach (var t in plan.GetWave(w))
                AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
                    $"  [cyan]{Markup.Escape(t.Id)}[/]: {Markup.Escape(t.Description)}");
        }
        return;
    }

    AnsiConsole.MarkupLine("[bold]Executing swarm...[/]");
    var result = await orchestrator.ExecuteAsync(plan, swarmConfig, onEvent: evt =>
    {
        switch (evt)
        {
            case Sovrant.Agents.Swarm.SwarmEvent.TaskStarted ts:
                AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
                    $"  [blue]\u25b6[/] {Markup.Escape(ts.TaskId)} \u2192 {Markup.Escape(ts.AgentName)}");
                break;
            case Sovrant.Agents.Swarm.SwarmEvent.TaskCompleted tc:
                AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
                    $"  [green]\u2713[/] {Markup.Escape(tc.TaskId)} ({tc.TokensUsed} tokens)");
                break;
            case Sovrant.Agents.Swarm.SwarmEvent.TaskFailed tf:
                AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
                    $"  [red]\u2717[/] {Markup.Escape(tf.TaskId)}: {Markup.Escape(tf.Error)}");
                break;
        }
    }, ct: ct).ConfigureAwait(false);

    AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
        $"\n[bold]Status:[/] {Markup.Escape(result.Status.ToString())} | Tokens: {result.TotalTokensUsed} | Duration: {result.Duration.TotalSeconds:F1}s");

    if (swarmConfig.QualityGateEnabled && result.Status == Sovrant.Agents.Swarm.SwarmStatus.Completed)
    {
        var verdict = await qualityGate.ReviewAsync(result.SwarmId, task, result.CombinedOutput, ct).ConfigureAwait(false);
        AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
            $"[bold]Quality Gate:[/] {Markup.Escape(verdict.Verdict)} (score {verdict.Score})");
    }
});
root.Add(swarmCmd);

// ── 'db import-swarm' subcommand ──────────────────────────────────────────────
// One-shot migration helper: imports legacy ~/.sovrant/swarm/sessions/*.jsonl
// files into the swarm_events table (Phase 37.5). Safe to re-run; existing rows
// for the same swarmId are not deduped at the row level — operators are expected
// to use --delete-source after a clean run.
var importDirOpt = new Option<string?>("--dir")
    { Description = "Sessions directory to import. Defaults to ~/.sovrant/swarm/sessions." };
var importDeleteSourceOpt = new Option<bool>("--delete-source")
    { Description = "Delete each JSONL file after it has been imported successfully." };

var dbCmd = new Command("db", "Database maintenance and migration helpers.");
var importSwarmCmd = new Command("import-swarm", "Import legacy JSONL swarm sessions into the swarm_events table.");
importSwarmCmd.Add(importDirOpt);
importSwarmCmd.Add(importDeleteSourceOpt);
importSwarmCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var dir = pr.GetValue(importDirOpt) ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "swarm", "sessions");
    var deleteSource = pr.GetValue(importDeleteSourceOpt);

    if (!Directory.Exists(dir))
    {
        AnsiConsole.MarkupLine($"[yellow]No sessions directory at[/] {Markup.Escape(dir)} — nothing to import.");
        return;
    }

    var files = Directory.GetFiles(dir, "*.jsonl");
    if (files.Length == 0)
    {
        AnsiConsole.MarkupLine($"[yellow]No .jsonl files in[/] {Markup.Escape(dir)} — nothing to import.");
        return;
    }

    await using var sp = BuildServices(pr);
    var store = sp.GetRequiredService<ISwarmEventStore>();

    var totalEvents = 0;
    var skipped = 0;
    var importedFiles = 0;

    foreach (var file in files)
    {
        var swarmId = Path.GetFileNameWithoutExtension(file);
        var eventsInFile = 0;

        try
        {
            using var reader = new StreamReader(file);
            while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var record = JsonlSwarmImport.TryBuildRecord(swarmId, line);
                if (record is null)
                {
                    skipped++;
                    continue;
                }

                await store.RecordEventAsync(record, ct).ConfigureAwait(false);
                eventsInFile++;
            }

            totalEvents += eventsInFile;
            importedFiles++;
            AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
                $"  [green]\u2713[/] {Markup.Escape(Path.GetFileName(file))} \u2192 {eventsInFile} events");

            if (deleteSource)
                File.Delete(file);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
                $"  [red]\u2717[/] {Markup.Escape(Path.GetFileName(file))}: {Markup.Escape(ex.Message)}");
        }
    }

    AnsiConsole.MarkupLine(System.Globalization.CultureInfo.InvariantCulture,
        $"\n[bold]Imported[/] {totalEvents} events from {importedFiles} file(s){(skipped > 0 ? $", skipped {skipped} unparseable line(s)" : "")}.");
    if (deleteSource && importedFiles > 0)
        AnsiConsole.MarkupLine("[grey dim]Source files deleted (--delete-source).[/]");
});
dbCmd.Add(importSwarmCmd);

// ── 'db status' — path, schema version, table row counts ─────────────────────
var dbStatusCmd = new Command("status", "Show DB path, schema version, and row counts per table.");
dbStatusCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    var storage = sp.GetRequiredService<IStorageProvider>();
    await storage.InitializeAsync(ct).ConfigureAwait(false);
    var health = storage.CheckHealth();

    AnsiConsole.MarkupLine($"[bold]DB path:[/]        {Markup.Escape(storage.DatabasePath ?? "(unknown)")}");
    AnsiConsole.MarkupLine($"[bold]Schema version:[/] {storage.SchemaVersion}");
    AnsiConsole.MarkupLine($"[bold]Health:[/]         {(health.Ok ? "[green]ok[/]" : $"[red]error[/] — {Markup.Escape(health.Error ?? "unknown")}")}");

    if (!health.Ok || storage is not ISqliteConnectionFactory factory)
        return;

    await using var conn = factory.CreateReadOnlyConnection();
    var tables = new List<string>();
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            tables.Add(r.GetString(0));
    }

    var table = new Table().AddColumns("Table", "Rows");
    foreach (var t in tables)
    {
        await using var cmd = conn.CreateCommand();
#pragma warning disable CA2100 // table names come from sqlite_master, not user input
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{t}\"";
#pragma warning restore CA2100
        var count = Convert.ToInt64(
            await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);
        table.AddRow(t, count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    AnsiConsole.Write(table);
});
dbCmd.Add(dbStatusCmd);

// ── 'db version' — just the integer schema version ──────────────────────────
var dbVersionCmd = new Command("version", "Print the current schema version integer.");
dbVersionCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    var storage = sp.GetRequiredService<IStorageProvider>();
    await storage.InitializeAsync(ct).ConfigureAwait(false);
    Console.WriteLine(storage.SchemaVersion);
});
dbCmd.Add(dbVersionCmd);

// ── 'db migrate --dry-run' — list pending migrations without applying ───────
var dryRunOpt = new Option<bool>("--dry-run")
    { Description = "List pending migrations without applying them." };
var dbMigrateCmd = new Command("migrate", "Apply pending migrations, or list them with --dry-run.");
dbMigrateCmd.Add(dryRunOpt);
dbMigrateCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var dryRun = pr.GetValue(dryRunOpt);
    await using var sp = BuildServices(pr);
    var storage = sp.GetRequiredService<IStorageProvider>();

    if (!dryRun)
    {
        await storage.InitializeAsync(ct).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[green]\u2713[/] Migrated to schema version {storage.SchemaVersion}.");
        return;
    }

    // Dry-run: open a read-only connection, ask MigrationRunner for pending.
    if (storage is not ISqliteConnectionFactory factory)
    {
        AnsiConsole.MarkupLine("[red]Current storage provider does not support dry-run.[/]");
        Environment.ExitCode = 1;
        return;
    }

    using var conn = factory.CreateConnection();
    var pending = MigrationRunner.GetPendingMigrations(conn);

    if (pending.Count == 0)
    {
        AnsiConsole.MarkupLine("[grey]No pending migrations — DB is up to date.[/]");
        return;
    }

    AnsiConsole.MarkupLine($"[bold]{pending.Count} pending migration(s):[/]");
    foreach (var (v, d) in pending)
        AnsiConsole.MarkupLine($"  V{v:D3} — {Markup.Escape(d)}");
});
dbCmd.Add(dbMigrateCmd);

// ── 'db backup' — checkpoint WAL and copy the DB file ───────────────────────
var backupPathArg = new Argument<string?>("path")
{
    Description = "Destination path. Defaults to {db}.bak-{schema_version}.",
    Arity = ArgumentArity.ZeroOrOne,
};
var dbBackupCmd = new Command("backup", "Checkpoint the WAL and copy the DB file to a backup path.");
dbBackupCmd.Add(backupPathArg);
dbBackupCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    var storage = sp.GetRequiredService<IStorageProvider>();
    await storage.InitializeAsync(ct).ConfigureAwait(false);

    var dbPath = storage.DatabasePath;
    if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
    {
        AnsiConsole.MarkupLine("[red]No DB file to back up.[/]");
        Environment.ExitCode = 1;
        return;
    }

    var dest = pr.GetValue(backupPathArg)
        ?? $"{dbPath}.bak-{storage.SchemaVersion}";

    if (storage is ISqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        // Fold the WAL into the main file so a plain File.Copy captures
        // a consistent snapshot. TRUNCATE mode flushes then resets the WAL.
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    File.Copy(dbPath, dest, overwrite: true);
    AnsiConsole.MarkupLine($"[green]\u2713[/] Backed up to {Markup.Escape(dest)}");
});
dbCmd.Add(dbBackupCmd);

// ── 'db inspect <table>' — print schema + first N rows ──────────────────────
var inspectTableArg = new Argument<string>("table") { Description = "Name of the table to inspect." };
var inspectLimitOpt = new Option<int>("--limit")
    { Description = "Number of rows to print (default 20).", DefaultValueFactory = _ => 20 };
var dbInspectCmd = new Command("inspect", "Print a table's schema and first N rows.");
dbInspectCmd.Add(inspectTableArg);
dbInspectCmd.Add(inspectLimitOpt);
dbInspectCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var tableName = pr.GetValue(inspectTableArg)!;
    var limit = pr.GetValue(inspectLimitOpt);

    await using var sp = BuildServices(pr);
    var storage = sp.GetRequiredService<IStorageProvider>();
    await storage.InitializeAsync(ct).ConfigureAwait(false);

    if (storage is not ISqliteConnectionFactory factory)
    {
        AnsiConsole.MarkupLine("[red]Current storage provider does not support inspect.[/]");
        Environment.ExitCode = 1;
        return;
    }

    // Validate the table name against sqlite_master to avoid injection via
    // the command-line argument, then interpolate the validated name.
    await using var conn = factory.CreateReadOnlyConnection();
    await using (var check = conn.CreateCommand())
    {
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$n";
        check.Parameters.AddWithValue("$n", tableName);
        if (await check.ExecuteScalarAsync(ct).ConfigureAwait(false) is null)
        {
            AnsiConsole.MarkupLine($"[red]No such table:[/] {Markup.Escape(tableName)}");
            Environment.ExitCode = 1;
            return;
        }
    }

    // Schema.
    AnsiConsole.MarkupLine($"[bold]Schema for[/] {Markup.Escape(tableName)}");
    await using (var cmd = conn.CreateCommand())
    {
#pragma warning disable CA2100 // name validated above against sqlite_master
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
#pragma warning restore CA2100
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var st = new Table().AddColumns("cid", "name", "type", "notnull", "dflt_value", "pk");
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            st.AddRow(
                r.GetValue(0)?.ToString() ?? "",
                r.GetValue(1)?.ToString() ?? "",
                r.GetValue(2)?.ToString() ?? "",
                r.GetValue(3)?.ToString() ?? "",
                r.GetValue(4)?.ToString() ?? "",
                r.GetValue(5)?.ToString() ?? "");
        }
        AnsiConsole.Write(st);
    }

    // Rows.
    AnsiConsole.MarkupLine($"[bold]First {limit} row(s)[/]");
    await using (var cmd = conn.CreateCommand())
    {
#pragma warning disable CA2100 // table name validated; limit bound as parameter
        cmd.CommandText = $"SELECT * FROM \"{tableName}\" LIMIT $lim";
#pragma warning restore CA2100
        cmd.Parameters.AddWithValue("$lim", limit);
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var cols = new string[r.FieldCount];
        for (int i = 0; i < r.FieldCount; i++) cols[i] = r.GetName(i);
        var rt = new Table().AddColumns(cols);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = new string[r.FieldCount];
            for (int i = 0; i < r.FieldCount; i++)
            {
                var v = await r.IsDBNullAsync(i, ct).ConfigureAwait(false)
                    ? "null"
                    : r.GetValue(i)?.ToString() ?? "";
                if (v.Length > 80) v = v[..77] + "...";
                row[i] = Markup.Escape(v);
            }
            rt.AddRow(row);
        }
        AnsiConsole.Write(rt);
    }
});
dbCmd.Add(dbInspectCmd);

// ── 'db init' — explicit first-boot initialisation ────────────────────────────
var dbInitCmd = new Command("init", "Initialise the database (run migrations and seed default data). Safe to re-run.");
dbInitCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    var storage = sp.GetRequiredService<IStorageProvider>();
    await storage.InitializeAsync(ct).ConfigureAwait(false);
    AnsiConsole.MarkupLine($"[green]\u2713[/] Database initialised at schema version {storage.SchemaVersion}.");
    AnsiConsole.MarkupLine($"   Path: {Markup.Escape(storage.DatabasePath ?? "(unknown)")}");
});
dbCmd.Add(dbInitCmd);

// ── 'db reset' — drop and re-create from scratch ──────────────────────────────
var resetConfirmOpt = new Option<bool>("--yes")
    { Description = "Skip the confirmation prompt (for scripting)." };
var dbResetCmd = new Command("reset", "Delete the database and re-initialise from scratch. All data will be lost.");
dbResetCmd.Add(resetConfirmOpt);
dbResetCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    await using var sp = BuildServices(pr);
    var storage = sp.GetRequiredService<IStorageProvider>();

    var dbPath = storage.DatabasePath;
    if (string.IsNullOrEmpty(dbPath))
    {
        AnsiConsole.MarkupLine("[red]Cannot determine database path.[/]");
        Environment.ExitCode = 1;
        return;
    }

    if (!pr.GetValue(resetConfirmOpt))
    {
        AnsiConsole.MarkupLine($"[yellow bold]WARNING:[/] This will permanently delete [bold]{Markup.Escape(dbPath)}[/] and all data in it.");
        if (!await AnsiConsole.ConfirmAsync("Are you sure you want to reset the database?", defaultValue: false, ct).ConfigureAwait(false))
        {
            AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
            return;
        }
    }

    // Delete DB + WAL/SHM sidecars.
    string[] dbSuffixes = ["", "-wal", "-shm"];
    foreach (var suffix in dbSuffixes)
    {
        var path = dbPath + suffix;
        if (File.Exists(path))
            File.Delete(path);
    }

    // Re-initialise from scratch.
    var freshStorage = new SqliteStorageProvider(
        sp.GetRequiredService<ILogger<SqliteStorageProvider>>(), dbPath);
    await freshStorage.InitializeAsync(ct).ConfigureAwait(false);

    AnsiConsole.MarkupLine($"[green]\u2713[/] Database reset to schema version {freshStorage.SchemaVersion}.");
    AnsiConsole.MarkupLine($"   Path: {Markup.Escape(dbPath)}");
});
dbCmd.Add(dbResetCmd);

root.Add(dbCmd);

// ── REPL (default handler) ────────────────────────────────────────────────────
root.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var config = ConfigLoader.Load();
    var keyError = ApiKeyValidator.Validate(config);
    if (keyError is not null)
    {
        AnsiConsole.MarkupLine("[red bold]Configuration Error[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine(keyError);
        Environment.ExitCode = 1;
        return;
    }

    await using var sp = BuildServices(pr);
    await InitAsync(sp, pr, ct).ConfigureAwait(false);
    var runtime = sp.GetRequiredService<IConversationRuntime>();
    var sessionId = pr.GetValue(sessionOpt);
    if (sessionId is not null)
        await runtime.InitializeSessionAsync(sessionId, ownerUserId: null, ct).ConfigureAwait(false);
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
    var dbPath = pr.GetValue(dbPathOpt);

    if (model is not null || permModeRaw is not null || dbPath is not null)
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
            DbPath = dbPath ?? config.DbPath,
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
    services.AddMultiAgentSystem();
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
        // Replace the default deny-all confirmation handler with the interactive one.
        services.AddSingleton<IToolConfirmationHandler, InteractiveConfirmationHandler>();
        // Replace the null swarm reporter with the CLI one for live progress.
        services.AddSingleton<Sovrant.Tools.Swarm.ISwarmProgressReporter, CliSwarmProgressReporter>();
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
    StartupBanner.Render(runtime.SessionId);
    AnsiConsole.MarkupLine("  Type [grey]/help[/] for commands, [grey]/exit[/] to quit.");
    AnsiConsole.WriteLine();

    // Use a solid (non-blinking) block cursor for the entire session.
    Console.Write("\x1b[2 q");

    // Gather slash command names for autocomplete.
    var commandNames = dispatcher.Commands.Select(c => c.Name).ToList();

    while (!ct.IsCancellationRequested)
    {
        var input = SovrantInputReader.ReadLine(commandNames, ct);

        if (input.WasCancelled)
        {
            if (ct.IsCancellationRequested)
                break;
            // Escape at the prompt — just redraw.
            continue;
        }

        var line = input.Text.Trim();
        if (string.IsNullOrEmpty(line)) continue;

        // Echo the user's input with a blank line before the response.
        AnsiConsole.MarkupLine($"[bold cyan]{Markup.Escape(Environment.UserName)}:[/] {Markup.Escape(line.Contains('\n', StringComparison.Ordinal) ? line[..line.IndexOf('\n', StringComparison.Ordinal)] + "..." : line)}");
        AnsiConsole.WriteLine();

        try
        {
            // Try to dispatch as a slash command first.
            var cmdResult = await dispatcher.TryDispatchAsync(line, ct).ConfigureAwait(false);
            if (cmdResult is not null)
            {
                if (cmdResult.ShouldExit)
                    break;
                if (cmdResult.ShouldClearHistory)
                {
                    runtime.Reset();
                    AnsiConsole.Clear();
                }
                if (cmdResult.Output is not null)
                {
                    AnsiConsole.WriteLine(cmdResult.Output);
                    AnsiConsole.Write(new Rule().RuleStyle("grey dim"));
                }
                if (cmdResult.InjectAsUserMessage is not null)
                    await RunTurnWithCancelAsync(runtime, cmdResult.InjectAsUserMessage, ct).ConfigureAwait(false);
                continue;
            }

            // Otherwise send to the LLM.
            await RunTurnWithCancelAsync(runtime, line, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            AnsiConsole.MarkupLine($"[red bold]Error:[/] [red]{Markup.Escape(ex.Message)}[/]");
        }
    }

    // Ensure the cursor is on a clean line after the input box is cleared,
    // then restore the default cursor shape so the shell prompt renders correctly.
    Console.WriteLine();
    Console.Write("\x1b[0 q");
}

async Task RunTurnWithCancelAsync(IConversationRuntime runtime, string message, CancellationToken outerCt)
{
    using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
    using var escMonitor = new EscapeKeyMonitor(turnCts);
    using var spinner = new ThinkingSpinner();

    // Show a read-only input box so the user knows they can press Escape.
    // Render before the spinner so the cursor is positioned in the content
    // area above the box — the spinner's \r writes stay above the box.
    SovrantInputReader.RenderProcessingBox();

    spinner.Start();
    if (!Console.IsInputRedirected)
        escMonitor.Start();

    var firstToken = true;

    try
    {
        await foreach (var ev in runtime.RunTurnAsync(message, turnCts.Token).ConfigureAwait(false))
        {
            switch (ev)
            {
                case RuntimeEvent.TextChunk { Text: var text }:
                    if (firstToken)
                    {
                        SovrantInputReader.ClearProcessingBox();
                        await spinner.StopAsync().ConfigureAwait(false);
                        AnsiConsole.MarkupLine("[bold teal]Sovrant:[/]");
                        firstToken = false;
                    }
                    AnsiConsole.Write(text);
                    break;

                case RuntimeEvent.ToolUseRequested { ToolName: var toolName, Input: var input }:
                    if (firstToken)
                    {
                        SovrantInputReader.ClearProcessingBox();
                        await spinner.StopAsync().ConfigureAwait(false);
                        firstToken = false;
                    }
                    AnsiConsole.MarkupLine($"\n  [blue bold]\u2699 {Markup.Escape(toolName)}[/]");
                    if (DiffRenderer.IsFileModifyTool(toolName))
                        DiffRenderer.RenderToolInput(toolName, input);
                    break;

                case RuntimeEvent.ToolResult { ToolName: var toolName, Content: var content, IsError: var isErr }:
                    if (isErr)
                        AnsiConsole.MarkupLine($"  [red dim]\u2717 {Markup.Escape(toolName)}: {Markup.Escape(Truncate(content, 200))}[/]");
                    else
                        AnsiConsole.MarkupLine($"  [grey dim]\u2713 {Markup.Escape(toolName)}[/]");
                    break;

                case RuntimeEvent.PermissionDenied { ToolName: var toolName, Reason: var reason }:
                    AnsiConsole.MarkupLine($"  [yellow]\u26a0 {Markup.Escape(toolName)}: {Markup.Escape(reason)}[/]");
                    break;

                case RuntimeEvent.TurnComplete { InputTokens: var inTok, OutputTokens: var outTok }:
                    AnsiConsole.MarkupLine($"\n[grey dim]({inTok}\u2191 {outTok}\u2193 tokens)[/]");
                    AnsiConsole.Write(new Rule().RuleStyle("grey dim"));
                    break;

                case RuntimeEvent.RuntimeError { Message: var msg }:
                    if (firstToken)
                    {
                        SovrantInputReader.ClearProcessingBox();
                        await spinner.StopAsync().ConfigureAwait(false);
                        firstToken = false;
                    }
                    AnsiConsole.MarkupLine($"[red bold]Error:[/] [red]{Markup.Escape(msg)}[/]");
                    break;
            }
        }
    }
    catch (OperationCanceledException) when (turnCts.IsCancellationRequested && !outerCt.IsCancellationRequested)
    {
        SovrantInputReader.ClearProcessingBox();
        await spinner.StopAsync().ConfigureAwait(false);
        AnsiConsole.MarkupLine("[yellow][[Cancelled]][/]");
    }
    finally
    {
        await escMonitor.StopAsync().ConfigureAwait(false);
        if (firstToken)
        {
            SovrantInputReader.ClearProcessingBox();
            await spinner.StopAsync().ConfigureAwait(false);
        }
    }
}

async Task RunTurnAsync(IConversationRuntime runtime, string message, CancellationToken ct)
{
    using var spinner = new ThinkingSpinner();
    spinner.Start();
    var firstToken = true;

    await foreach (var ev in runtime.RunTurnAsync(message, ct).ConfigureAwait(false))
    {
        switch (ev)
        {
            case RuntimeEvent.TextChunk { Text: var text }:
                if (firstToken)
                {
                    await spinner.StopAsync().ConfigureAwait(false);
                    AnsiConsole.MarkupLine("[bold teal]Sovrant:[/]");
                    firstToken = false;
                }
                AnsiConsole.Write(text);
                break;

            case RuntimeEvent.ToolUseRequested { ToolName: var toolName, Input: var input }:
                if (firstToken)
                {
                    await spinner.StopAsync().ConfigureAwait(false);
                    firstToken = false;
                }
                AnsiConsole.MarkupLine($"\n  [blue bold]\u2699 {Markup.Escape(toolName)}[/]");
                if (DiffRenderer.IsFileModifyTool(toolName))
                    DiffRenderer.RenderToolInput(toolName, input);
                break;

            case RuntimeEvent.ToolResult { ToolName: var toolName, Content: var content, IsError: var isErr }:
                if (isErr)
                    AnsiConsole.MarkupLine($"  [red dim]\u2717 {Markup.Escape(toolName)}: {Markup.Escape(Truncate(content, 200))}[/]");
                else
                    AnsiConsole.MarkupLine($"  [grey dim]\u2713 {Markup.Escape(toolName)}[/]");
                break;

            case RuntimeEvent.PermissionDenied { ToolName: var toolName, Reason: var reason }:
                AnsiConsole.MarkupLine($"  [yellow]\u26a0 {Markup.Escape(toolName)}: {Markup.Escape(reason)}[/]");
                break;

            case RuntimeEvent.TurnComplete { InputTokens: var inTok, OutputTokens: var outTok }:
                AnsiConsole.MarkupLine($"\n[grey dim]({inTok}\u2191 {outTok}\u2193 tokens)[/]");
                break;

            case RuntimeEvent.RuntimeError { Message: var msg }:
                if (firstToken)
                {
                    await spinner.StopAsync().ConfigureAwait(false);
                    firstToken = false;
                }
                AnsiConsole.MarkupLine($"[red bold]Error:[/] [red]{Markup.Escape(msg)}[/]");
                break;
        }
    }
}

static string Truncate(string text, int maxLen) =>
    text.Length > maxLen ? string.Concat(text.AsSpan(0, maxLen), "...") : text;

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
