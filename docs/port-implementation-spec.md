# Sovrant — TypeScript Multi-Provider Agent → C# .NET 10 Port Implementation Spec

**Source repo:** `openclaude-main` (TypeScript/Bun, OpenAI-compatible multi-provider agentic engine)
**Target branch:** `sovrant-openc-dotnet-port`
**Target:** C# 13 / .NET 10 console application
**Series:** This is **Port 2 of 2**. Port 1 (`sovrant-rust-dotnet-port`) ported the Rust reference implementation.

This document is the complete tasking guide. Work through it phase by phase in order. Do not skip phases. After completing each phase, run the acceptance criteria before moving on.

---

## Key Differences from Port 1

| Concern | Port 1 (Rust) | Port 2 (TypeScript multi-provider engine) |
|---|---|---|
| Source language | Rust | TypeScript / TSX (React/Ink) |
| Provider model | proprietary-API-first | OpenAI-compatible shim over any provider |
| Permission modes | `ReadOnly / WorkspaceWrite / DangerFullAccess` | `default / acceptEdits / bypassPermissions / dontAsk / plan` |
| Tool count | 8 core tools | 40+ tools including Agent, Task, MCP, Notebook, REPL, PowerShell, WebSearch |
| Smart routing | No | Yes — `SmartRouter` benchmarks providers and routes by latency/cost/health |
| Config system | `.sovrant.json` walk-up | `.sovrant/settings.json` layered with profiles |
| Session format | JSON file per session | JSONL append-log with rotation |
| UI framework | Spectre.Console | Spectre.Console (same target) |

---

## API Compatibility Note (carried from Port 1)

This engine supports **any LLM provider** implementing the OpenAI-compatible messages API.

- `LLM_API_KEY` — primary API key env var
- `LLM_BASE_URL` — primary base URL env var
- `PROVIDER_API_KEY` / `OPENAI_API_KEY` — accepted as fallback aliases
- `PROVIDER_BASE_URL` / `OPENAI_BASE_URL` — accepted as fallback aliases
- `USE_OPENAI_COMPAT=1` — enable OpenAI-compat mode

---

## Source TypeScript Layout

```
openclaude-main/
├── src/
│   ├── Tool.ts               # ITool interface + buildTool factory
│   ├── tools.ts              # Tool registry
│   ├── commands.ts           # Command registry
│   ├── context.ts            # Session context
│   ├── main.tsx              # Ink/React REPL entry
│   ├── tools/                # 40+ tool implementations (each in own dir)
│   ├── commands/             # Slash command implementations
│   ├── services/api/         # Provider clients (OpenAI-compat, Gemini, Ollama, local)
│   ├── services/mcp/         # MCP client
│   ├── types/                # Core type definitions
│   │   ├── message.ts        # Message/content block types
│   │   └── permissions.ts    # Permission mode + rule types
│   ├── utils/                # Shared utilities
│   └── state/                # AppState management
├── smart_router.py           # Multi-provider smart router (Python reference)
├── atomic_chat_provider.py   # Atomic Chat provider
└── ollama_provider.py        # Ollama provider
```

---

## Target C# Solution Layout

```
Sovrant.slnx
├── src/
│   ├── Sovrant.Api/          # OpenAI-compat HTTP client, SmartRouter, SSE streaming
│   ├── Sovrant.Commands/     # Slash-command registry and dispatcher
│   ├── Sovrant.Runtime/      # Agentic loop, config, session (JSONL), permissions, MCP
│   ├── Sovrant.Tools/        # Built-in tool implementations
│   └── Sovrant.Cli/          # CLI entry point (System.CommandLine v2 + Spectre.Console)
├── tests/
│   ├── Sovrant.Api.Tests/
│   ├── Sovrant.Commands.Tests/
│   ├── Sovrant.Runtime.Tests/
│   ├── Sovrant.Tools.Tests/
│   └── Sovrant.Integration.Tests/
└── docs/
    └── port-implementation-spec.md   ← this file
```

---

## Global Rules (inherited from Port 1, extended)

### Language & runtime
- Target framework: `net10.0`
- Language version: C# 13 (`<LangVersion>latest</LangVersion>`)
- Nullable reference types enabled globally
- Implicit usings enabled globally
- `TreatWarningsAsErrors=true`, `AnalysisMode=All`
- AOT-safe patterns preferred

### Approved NuGet packages
- `System.CommandLine` v2 — CLI argument parsing
- `Spectre.Console` — terminal rendering / REPL display
- `Microsoft.Extensions.*` (DI, Configuration, Logging, Hosting)
- `Microsoft.Data.Sqlite` — session persistence (JSONL append log)
- `ModelContextProtocol` — official C# MCP client SDK
- `xunit` + `xunit.runner.visualstudio` — test framework

Do NOT add: RestSharp, Newtonsoft.Json, Polly.

### Code style (same as Port 1)
- File-scoped namespaces (`namespace Sovrant.X;`)
- Primary constructors for simple record types
- Records for immutable data; interfaces for all injectable services
- `async`/`await` throughout — no `.Result` or `.Wait()`
- `CancellationToken` as last parameter on every async method
- XML doc comments on all public types and members
- One class/interface/record per file

### Testing requirements
- Every new class must have a corresponding unit test file
- Minimum 80% line coverage on `Sovrant.Runtime` and `Sovrant.Api`
- Integration tests skip gracefully when `LLM_API_KEY` absent
- All tests pass: `dotnet test --configuration Release`

---

## Phase 1 — Solution Scaffold 🔲 TODO

Same structure as Port 1. See Port 1 Phase 1 for exact commands.

**Key difference:** This is a fresh scaffold in `sovrant-openc-dotnet-port/` — do not copy files from `sovrant-rust-dotnet-port`.

### Phase 1 acceptance criteria
- `dotnet build` exits 0, zero warnings
- `dotnet test` exits 0, zero failures
- `Directory.Build.props` in repo root

---

## Phase 2 — API Client (Sovrant.Api) 🔲 TODO

**Goal:** OpenAI-compatible HTTP client with SSE streaming + SmartRouter.

### New in Port 2 vs Port 1

**SmartRouter** — port `smart_router.py`:
```csharp
public interface ISmartRouter
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default);
}
```
- Pings all configured providers on startup (`GET /models` or equivalent)
- Scores by latency, cost-per-1k-tokens, and health
- Routes each request to the optimal provider
- Falls back automatically on failure
- Configurable via `ROUTER_MODE` (smart | fixed), `ROUTER_STRATEGY` (latency | cost | balanced)

**Provider abstraction:**
```csharp
public interface ILlmProvider
{
    string Name { get; }
    string BaseUrl { get; }
    Task<Result<MessagesResponse>> SendAsync(MessagesRequest req, CancellationToken ct);
    IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, CancellationToken ct);
}
```

Implement providers:
- `ProviderApiProvider` — adds vendor-specific version header, handles proprietary SSE format
- `OpenAiCompatProvider` — standard OpenAI chat completions format
- `OllamaProvider` — `LLM_BASE_URL=http://localhost:11434/v1`, no auth header needed

**Environment variables (priority order):**
1. `LLM_API_KEY` / `LLM_BASE_URL`
2. `OPENAI_API_KEY` / `OPENAI_BASE_URL`
3. `PROVIDER_API_KEY` / `PROVIDER_BASE_URL`
4. Config file values

### Phase 2 acceptance criteria
- All three provider types construct and send requests correctly
- SmartRouter selects fastest healthy provider
- SSE streaming works for OpenAI-compat and proprietary SSE formats
- Unit tests mock `HttpMessageHandler`
- Integration test skips if `LLM_API_KEY` absent

---

## Phase 3 — Runtime (Sovrant.Runtime) 🔲 TODO

**Goal:** Agentic loop, layered config, JSONL session persistence, permissions (source permission model), MCP.

### 3.1 Config — layered from the source project

Walk from CWD upward, layer in order (lowest wins):
1. `~/.sovrant/settings.json` — user-global
2. `.sovrant/settings.json` — project
3. `.sovrant/settings.local.json` — machine-local
4. Environment variables

Typed `SovrantConfig` record includes:
- `Model`, `MaxTokens`, `ApiKey`, `BaseUrl`
- `PermissionMode` (default | acceptEdits | bypassPermissions | dontAsk | plan)
- `McpServers`, `AllowRules`, `DenyRules`, `AskRules`
- `RouterMode` (smart | fixed), `RouterStrategy` (latency | cost | balanced)

### 3.2 Session persistence — JSONL append log

Port the JSONL session format from the source project:
- Append each message/event as a single JSON line
- Rotate when file exceeds 256 KB (keep last 3 rotations)
- Session files at `~/.sovrant/sessions/{id}.jsonl`
- Load by replaying all lines in order

```csharp
public interface ISessionStore
{
    Task<ConversationSession?> LoadAsync(string sessionId, CancellationToken ct = default);
    Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct = default);
}
```

### 3.3 Permissions — source permission model

Permission modes (from `types/permissions.ts`):
- `default` — ask before destructive operations
- `acceptEdits` — auto-accept file edits, ask for bash
- `bypassPermissions` — allow everything
- `dontAsk` — never prompt (silent allow)
- `plan` — read-only planning mode

Rules: `allow`, `deny`, `ask` lists with `toolName(ruleContent)` syntax (same as Port 1 engine).

### 3.4 Conversation Runtime — same agentic loop as Port 1

Extended with:
- `plan` mode awareness (refuse write tools when in plan mode)
- Tool result storage for large outputs (> 50 KB → write to temp file, return path)
- Compact/summarize session when input tokens exceed threshold

### 3.5 MCP client — same as Port 1

### Phase 3 acceptance criteria
- Config layers correctly (local overrides project overrides user)
- JSONL session round-trips (append + replay = same messages)
- `plan` mode blocks write tools
- All agentic loop paths covered by unit tests

---

## Phase 4 — Tools (Sovrant.Tools) 🔲 TODO

**Goal:** Full tool set ported from the source project. Many more tools than Port 1.

### Core tools (same as Port 1, extended)

| Class | Tool Name | Notes vs Port 1 |
|---|---|---|
| `ReadFileTool` | `Read` | Adds image support (base64), PDF page range, line numbers |
| `WriteFileTool` | `Write` | Adds file history tracking |
| `EditFileTool` | `Edit` | Unchanged |
| `BashTool` | `Bash` | Adds background task support, sandbox adapter, image output |
| `GlobTool` | `Glob` | Unchanged |
| `GrepTool` | `Grep` | Unchanged |
| `ListDirectoryTool` | `LS` | Renamed from `list_directory` |
| `WebFetchTool` | `WebFetch` | Prompt-aware summarisation pass |

### New tools in Port 2

| Class | Tool Name | Description |
|---|---|---|
| `WebSearchTool` | `WebSearch` | HTTP search API (Brave/Bing) with citation |
| `NotebookEditTool` | `NotebookEdit` | Jupyter `.ipynb` cell replace/insert/delete |
| `ReplTool` | `REPL` | Code execution in subprocess per language |
| `PowerShellTool` | `PowerShell` | Windows PowerShell with timeout |
| `TodoWriteTool` | `TodoWrite` | Structured task list (in-session) |
| `TaskCreateTool` | `TaskCreate` | Spawn background agent task |
| `TaskGetTool` | `TaskGet` | Poll background task status |
| `TaskListTool` | `TaskList` | List all background tasks |
| `TaskOutputTool` | `TaskOutput` | Stream output from background task |
| `TaskStopTool` | `TaskStop` | Cancel a background task |
| `AgentTool` | `Agent` | Launch sub-agent with isolated session |
| `McpTool` | `mcp__*` | Dynamic MCP tool proxy |
| `SleepTool` | `Sleep` | Wait without holding a shell |
| `AskUserQuestionTool` | `AskUserQuestion` | Elicit user input mid-turn |

### Phase 4 acceptance criteria
- All tools registered and resolvable via DI
- Round-trip tests for Read/Write/Edit
- BashTool: echo, stderr capture, non-zero exit, timeout
- NotebookEditTool: read/modify `.ipynb` JSON
- TodoWriteTool: in-memory task list persists across turns in same session

---

## Phase 5 — Commands (Sovrant.Commands) 🔲 TODO

**Goal:** Full slash-command set ported from the source project `src/commands/`.

### Commands to implement

| Command | Description |
|---|---|
| `/help` | List all commands |
| `/clear` | Start fresh session |
| `/model [name]` | Show or switch model |
| `/config [section]` | Inspect config |
| `/exit` / `/quit` | Terminate |
| `/session` | Session info |
| `/status` | Show provider health, model, permissions |
| `/compact` | Summarise and compact session history |
| `/permissions [mode]` | Show or switch permission mode |
| `/provider [name]` | Show or switch active provider |
| `/cost` | Show cumulative token usage and estimated cost |
| `/resume <id>` | Load a saved session |

### Phase 5 acceptance criteria
- All commands registered via DI and discovered by `/help`
- Unknown command prints error without throwing
- `/compact` calls runtime compaction and reports token reduction

---

## Phase 6 — CLI Entry Point (Sovrant.Cli) 🔲 TODO

Same structure as Port 1 Phase 6 with additions:

### Extended CLI surface
```
sovrant                                       # interactive REPL
sovrant prompt "..."                          # one-shot
sovrant --model gpt-4o prompt "..."
sovrant --provider openai prompt "..."        # explicit provider
sovrant --permission-mode bypass-permissions prompt "..."
sovrant --session <id> prompt "..."
sovrant --no-stream prompt "..."
sovrant status                                # show provider health
```

### Phase 6 acceptance criteria
- `sovrant --help` prints usage
- `sovrant status` shows provider health table
- `sovrant prompt "hello"` completes a turn with any configured provider
- REPL `/compact` reduces session token count
- Provider fallback: if primary provider fails, SmartRouter tries next

---

## Final Verification

```bash
# Build
dotnet build Sovrant.slnx --configuration Release
# Expected: 0 warnings, 0 errors

# Tests
dotnet test Sovrant.slnx --configuration Release
# Expected: all pass

# Smoke tests (requires LLM_API_KEY + LLM_BASE_URL)
export LLM_API_KEY="your-key"
export LLM_BASE_URL="https://api.openai.com/v1"

sovrant prompt "What is 2 + 2?"
sovrant prompt "List files in the current directory"
sovrant --session test-1 prompt "My name is Eric"
sovrant --session test-1 prompt "What is my name?"
sovrant status
sovrant
# > /help
# > /compact
# > /exit
```

---

## TypeScript → C# Reference Mapping

| TypeScript source pattern | C# .NET 10 equivalent |
|---|---|
| `zod` schema | `System.Text.Json` + manual validation |
| `React/Ink` components | `Spectre.Console` widgets |
| `bun:bundle` feature flags | `#if` preprocessor or config flags |
| `IAsyncIterable` | `IAsyncEnumerable<T>` |
| TypeScript `union` types | `abstract record` + `sealed record` subtypes |
| `Promise<T>` | `Task<T>` |
| `EventEmitter` | `IObservable<T>` or `Channel<T>` |
| `.env` / `process.env` | `Environment.GetEnvironmentVariable` |
| JSONL session log | `StreamWriter` append + `StreamReader` replay |
| Ink `useState` / context | DI-injected singleton state |
| `smart_router.py` | `SmartRouter` class with `HttpClient` pings |
| `ollama_provider.py` | `OllamaProvider : ILlmProvider` |
| `atomic_chat_provider.py` | `AtomicChatProvider : ILlmProvider` |
