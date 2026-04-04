# Sovrant

A .NET 10 agentic AI engine — multi-provider, tool-using, session-persistent. Runs as a CLI agent or an OpenAI-compatible HTTP server. The agent reads and writes files, executes shell commands, searches the web, calls tools autonomously, and maintains full conversation history across sessions.

**Runtime:** .NET 10 / C# 13
**Status:** Engine fully functional. 31 tools. 9 server endpoints. 111/111 tests passing.

---

## Projects

| Project | Description |
|---|---|
| `Sovrant.Cli` | Interactive REPL and one-shot `prompt` CLI. Entry point for local use. |
| `Sovrant.Server` | ASP.NET Core Minimal API — OpenAI-compatible endpoints plus session management and live config. |
| `Sovrant.Runtime` | Core agentic loop, session persistence (JSONL), permission system, tool executor, MCP client. |
| `Sovrant.Api` | LLM provider abstraction: OpenAI-compat, Ollama, native messages API. SmartRouter with health/latency/cost scoring. |
| `Sovrant.Tools` | All 31 tool implementations. |
| `Sovrant.Commands` | Slash commands for the REPL (`/help`, `/clear`, `/session`, `/memory`, etc.). |
| `Sovrant.Agents` | Multi-agent infrastructure: `IAgent` / `IMultiAgentSystem` interfaces, modern in-process backend, legacy process-based backend, `AGENT_MODE` config switch. |

---

## Quick Start — CLI

```bash
export LLM_API_KEY="sk-..."
# Optional: use a different provider
export LLM_BASE_URL="https://generativelanguage.googleapis.com/v1beta/openai/"

# One-shot prompt
dotnet run --project src/Sovrant.Cli -- --model gpt-4o-mini prompt "List all .cs files in src/"

# Interactive REPL
dotnet run --project src/Sovrant.Cli -- --model gpt-4o-mini

# Resume a named session
dotnet run --project src/Sovrant.Cli -- --model gpt-4o-mini --session my-project
```

### Permission modes

| Mode | Behaviour |
|---|---|
| `default` | Asks before each tool use |
| `acceptEdits` | Auto-approves file edits, asks for shell |
| `bypassPermissions` | Auto-approves everything |
| `dontAsk` | Never prompts; silently skips denied tools |
| `plan` | Read-only — no file or shell writes |

```bash
dotnet run --project src/Sovrant.Cli -- --permission-mode bypassPermissions prompt "Refactor the auth module"
```

> Use camelCase for `--permission-mode` values: `bypassPermissions`, not `bypass-permissions`.

---

## Quick Start — Server

```bash
export LLM_API_KEY="sk-..."
export SOVRANT_TOKEN="your-secret-token"
export SOVRANT_PORT=5200    # optional, default 5200

dotnet run --project src/Sovrant.Server
```

The server exposes an OpenAI-compatible chat completions endpoint plus session management, live config, status, and models. See [`docs/server.md`](docs/server.md) for the full API reference.

```bash
# Non-streaming
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer your-secret-token" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o-mini","messages":[{"role":"user","content":"Hello"}]}'

# Streaming (SSE)
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer your-secret-token" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o-mini","messages":[{"role":"user","content":"Hello"}],"stream":true}'

# Persistent session — history survives across requests
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer your-secret-token" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o-mini","messages":[{"role":"user","content":"My name is Eric"}],"session_id":"user-123"}'
```

### Server endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Unauthenticated health check |
| `POST` | `/v1/chat/completions` | Agentic chat — streaming (SSE) or non-streaming |
| `GET` | `/v1/config` | Current live configuration |
| `PUT` | `/v1/config` | Update model, API key, base URL, or permission mode without restart |
| `GET` | `/v1/status` | Provider health, latency, and routing scores |
| `GET` | `/v1/models` | OpenAI-compatible model list |
| `GET` | `/v1/sessions` | List all saved session IDs |
| `GET` | `/v1/sessions/{id}` | Get message history for a session |
| `DELETE` | `/v1/sessions/{id}` | Delete a session |

---

## Tools

31 tools available. All run inside the agentic loop with automatic retries up to 20 tool rounds per turn.

### File
`Read` · `Write` · `Edit` · `Glob` · `Grep` · `LS`

### Shell
`Bash` *(WSL required on Windows)* · `PowerShell` · `REPL` *(Python, Node, Ruby, Perl)*

### Web
`WebFetch` · `WebSearch` *(requires `BRAVE_API_KEY` or `FIRECRAWL_API_KEY`)*

### Task management
`TodoWrite` · `TaskCreate` · `TaskGet` · `TaskList` · `TaskOutput` · `TaskStop` · `TaskUpdate`

### Plan mode & worktree
`EnterPlanMode` · `ExitPlanMode` · `EnterWorktree` *(git worktree add)* · `ExitWorktree`

### Agent & interaction
`Agent` *(spawns an isolated sub-agent session)* · `AskUserQuestion` · `Sleep`

### Discovery & skills
`ToolSearch` *(keyword search over registered tools)* · `Skill` *(loads `.sovrant/skills/{name}.md` prompt template)*

### MCP resources
`ListMcpResources` · `ReadMcpResource`

### Notebook
`NotebookEdit` *(read/write Jupyter `.ipynb` cells)*

---

## Agent Memory

Sovrant reads two memory files at the start of every session and prepends their contents to the system prompt, making the agent immediately aware of your preferences and project conventions without re-explaining them each time.

| File | Scope |
|---|---|
| `~/.sovrant/memory.md` | Global — your coding style, preferred tools, personal notes |
| `.sovrant/memory.md` | Project — architecture notes, conventions, files to avoid |

Use `/memory` (or `/mem`) in the REPL to view or create these files.

---

## Custom Slash Commands

Place a markdown file at `.sovrant/commands/{name}.md`. Invoking `/{name}` in the REPL injects the file's content as a user message to the LLM. Use `$ARGUMENTS` as a placeholder for any text typed after the command name.

```
.sovrant/commands/review.md  →  /review src/Auth.cs
```

---

## Providers

| Provider | How to enable |
|---|---|
| OpenAI | `LLM_API_KEY=sk-...` (default) |
| Google AI Studio (Gemini) | `LLM_BASE_URL=https://generativelanguage.googleapis.com/v1beta/openai/` + `LLM_API_KEY=...` |
| Ollama (local) | `OLLAMA_BASE_URL=http://localhost:11434/v1` |
| Native messages API | `PROVIDER_BASE_URL=https://api.anthropic.com` + `PROVIDER_API_KEY=...` |

> Gemma models via Google AI Studio do not support function calling over the OpenAI-compat endpoint. Use Gemini 2.5 Flash or a newer Gemini model.

The `SmartRouter` pings all configured providers on startup, scores them by latency, cost, and error rate, and routes each request to the optimal one. Use `ROUTER_MODE=Fixed` to always route to the first configured provider, or `ROUTER_STRATEGY=Latency` / `Cost` to change the scoring weight.

---

## Session Persistence

Sessions are stored as JSONL append-logs at `~/.sovrant/sessions/{id}.jsonl`.

```bash
# CLI: resume a session
dotnet run --project src/Sovrant.Cli -- --session my-project prompt "What did we change last time?"

# Server: include session_id in the request body
{ "session_id": "user-123", "messages": [...] }
```

The server keeps one `ConversationRuntime` alive per `session_id` in an in-memory pool — history is available immediately without a disk read on every turn.

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `LLM_API_KEY` | Yes | API key for the primary provider. Aliases: `OPENAI_API_KEY`, `PROVIDER_API_KEY` |
| `LLM_BASE_URL` | No | Provider base URL (default: `https://api.openai.com/v1`). Alias: `OPENAI_BASE_URL` |
| `SOVRANT_TOKEN` | Yes (server) | Bearer token — all requests return 401 if unset |
| `SOVRANT_PORT` | No | Server port (default: `5200`) |
| `PROVIDER_BASE_URL` | No | Enables native messages API provider (`/v1/messages` format) |
| `PROVIDER_API_KEY` | No | API key for the native messages API provider |
| `OLLAMA_BASE_URL` | No | Enables local Ollama provider |
| `ROUTER_MODE` | No | `Smart` (default) or `Fixed` |
| `ROUTER_STRATEGY` | No | `Balanced` (default), `Latency`, or `Cost` |
| `AGENT_MODE` | No | `modern` (default, in-process) or `legacy` (process-per-agent) |
| `SOVRANT_COMPACT_THRESHOLD` | No | Input token count that triggers context auto-compaction (history summarisation). Default: `80000`. Set to `0` to disable. |
| `LLM_WEB_SEARCH` | No | Set to `true` to use OpenAI's Responses API with `web_search_preview`. No Brave/FireCrawl key required. |
| `BRAVE_API_KEY` | No | Enables WebSearch via Brave Search API |
| `FIRECRAWL_API_KEY` | No | Enables WebSearch via FireCrawl (fallback if Brave not set) |
| `SOVRANT_LOG_LEVEL` | No | Minimum log level: `Verbose`, `Debug`, `Information` (default), `Warning`, `Error`, `Fatal` |
| `SOVRANT_LOG_FILE` | No | Rolling file path pattern (default: `~/.sovrant/logs/sovrant-{Date}.log`). Empty string disables file logging. |
| `SOVRANT_LOG_CONSOLE` | No | Write logs to stdout (default: `true`). Set to `false` to silence console output. |
| `SOVRANT_LOG_FORMAT` | No | `text` (default, human-readable) or `json` (structured — better for log aggregators) |

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Clients                                                     │
│  ┌────────────┐   ┌──────────────────┐   ┌────────────────┐  │
│  │ CLI (REPL) │   │  Sovrant.Server  │   │ Frontend / SDK │  │
│  │ prompt     │   │  HTTP :5200      │   │ (browser/Node) │  │
│  └─────┬──────┘   └────────┬─────────┘   └───────┬────────┘  │
└────────┼───────────────────┼─────────────────────┼───────────┘
         │                   │  OpenAI-compat API   │
         └──────────┬─────────┘─────────────────────┘
                    │
         ┌──────────▼─────────────────────────────────────┐
         │  Sovrant.Runtime                               │
         │                                                │
         │  ConversationRuntime                           │
         │  ├── agentic loop (up to 20 tool rounds)       │
         │  ├── session history (List<InputMessage>)      │
         │  ├── permission gate                           │
         │  └── MCP client (tool registration)            │
         │                                                │
         │  IRuntimeSessionPool  (one runtime/session_id) │
         │  ISessionStore  (JSONL ~/.sovrant/sessions/)   │
         └──────────┬──────────────────┬──────────────────┘
                    │                  │
         ┌──────────▼───────┐  ┌───────▼──────────────────────┐
         │  Sovrant.Api     │  │  Sovrant.Tools               │
         │                  │  │                              │
         │  SmartRouter     │  │  File:  Read Write Edit      │
         │  ├── OpenAI      │  │         Glob Grep LS         │
         │  ├── Ollama      │  │  Shell: Bash PowerShell REPL │
         │  └── Native API  │  │  Web:   WebFetch WebSearch   │
         │                  │  │  Tasks: TodoWrite Task*      │
         │  health ping     │  │  Agent  AskUser  Sleep       │
         │  latency/cost    │  │  PlanMode  Worktree          │
         │  error rate      │  │  Skill  ToolSearch           │
         └──────────┬───────┘  │  MCP: ListResources Read     │
                    │          │  NotebookEdit                │
                    │          └──────────────────────────────┘
         ┌──────────▼───────────────┐
         │  LLM Providers           │
         │  OpenAI · Gemini · Ollama│
         │  · Native API            │
         └──────────────────────────┘
```

### Key design decisions

**Always streaming internally.** `ConversationRuntime` always sets `Stream: true` on every `MessagesRequest`. The server decides independently whether to forward chunks as SSE or buffer into a single JSON response. This means there is one code path in the agentic loop regardless of the client's preference.

**One runtime per session.** The server keeps one `ConversationRuntime` alive per `session_id` in a `ConcurrentDictionary`. Each runtime holds its own message history in memory, loaded from JSONL on first access. History is immediately available without a disk read on every turn.

**SmartRouter with health fallback.** All LLM calls go through `ISmartRouter`. On startup it pings every configured provider and scores them by latency, cost, and error rate. If all providers fail the startup ping (e.g. a transient DNS failure in WSL or CI), the router falls back to the configured list rather than refusing to start — the error surfaces on the first actual request instead.

**Tool execution is permission-gated.** Every tool call goes through `IPermissionPolicy` before execution. The CLI uses `ModeAwarePermissionPolicy` (interactive prompts based on `PermissionMode`). The server defaults to `DontAsk` (all tools run without prompts) and can be changed live via `PUT /v1/config`.

**Dual agent backends.** `Sovrant.Agents` defines `IMultiAgentSystem` with two interchangeable backends: a modern in-process backend (async message channels, recommended) and a legacy process-based backend (one process per agent, stdin/stdout). Switch via `AGENT_MODE=modern|legacy`. The active backend is selected at startup; no other part of the system depends on the concrete implementation.

---

## Tests

```bash
dotnet test   # 111 tests across 5 projects
```

| Project | Tests |
|---|---|
| `Sovrant.Api.Tests` | 28 |
| `Sovrant.Runtime.Tests` | 34 |
| `Sovrant.Tools.Tests` | 26 |
| `Sovrant.Commands.Tests` | 22 |
| `Sovrant.Integration.Tests` | 1 |

---

## Docs

| Document | Contents |
|---|---|
| [`docs/server.md`](docs/server.md) | Full server API reference — all 9 endpoints, auth, CORS, streaming format |
| [`docs/frontend-integration.md`](docs/frontend-integration.md) | Node.js proxy setup, browser SSE client, Replit integration |
| [`docs/engine-status.md`](docs/engine-status.md) | Tool test results, provider compatibility, known issues |
