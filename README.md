# Sovrant

A .NET 10 port of an agentic AI engine — multi-provider, tool-using, session-persistent. Mirrors the behaviour of Claude Code: a CLI agent loop that reads/writes files, runs terminal commands, searches the web, and maintains conversation history across sessions.

**Branch:** `sovrant-openc-dotnet-port`
**Runtime:** .NET 10 / C# 13
**Status:** Engine fully functional. All 8 server endpoints smoke-tested. 99/99 unit tests passing.

---

## Projects

| Project | Description |
|---|---|
| `Sovrant.Cli` | Interactive REPL and one-shot `prompt` CLI. Entry point for local use. |
| `Sovrant.Server` | ASP.NET Core Minimal API — exposes the engine over HTTP (OpenAI-compatible endpoints + session management). |
| `Sovrant.Runtime` | Core agentic loop, session persistence (JSONL), permission system, tool executor, MCP client. |
| `Sovrant.Api` | LLM provider abstraction: OpenAI-compat, Ollama, Anthropic native. SmartRouter with health/latency scoring. |
| `Sovrant.Tools` | All tool implementations (Read, Write, Edit, Glob, Grep, LS, Bash, PowerShell, REPL, WebFetch, WebSearch, TodoWrite, TaskCreate/Get/List/Output/Stop, Agent, AskUserQuestion, Sleep, NotebookEdit). |
| `Sovrant.Commands` | Slash commands for the REPL (`/help`, `/exit`, `/clear`, `/session`, etc.). |

---

## Quick Start (CLI)

```bash
# Set your API key
export LLM_API_KEY="sk-..."
# Optional: override provider (default: OpenAI)
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
| `default` | Asks permission before each tool use |
| `acceptEdits` | Auto-approves file edits, asks for shell |
| `bypassPermissions` | Auto-approves everything |
| `dontAsk` | Never prompts; silently skips denied tools |
| `plan` | Read-only mode; no file/shell writes |

```bash
dotnet run --project src/Sovrant.Cli -- --permission-mode bypassPermissions prompt "..."
```

> **Note:** use camelCase for `--permission-mode` values (`bypassPermissions`, not `bypass-permissions`).

---

## Quick Start (Server)

```bash
export LLM_API_KEY="sk-..."
export SOVRANT_TOKEN="your-secret-token"   # Bearer token for all requests
export SOVRANT_PORT=5200                   # optional, default 5200

dotnet run --project src/Sovrant.Server
```

The server exposes an **OpenAI-compatible** chat completions endpoint plus session management, live config, status, and models endpoints. See [`docs/server.md`](docs/server.md) for the full API reference.

### Example request

```bash
# Non-streaming
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer your-secret-token" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"hello"}],"model":"gpt-4o-mini"}'

# Streaming (SSE)
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer your-secret-token" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"hello"}],"model":"gpt-4o-mini","stream":true}'

# With a persistent session (in-memory pool — history survives across requests)
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer your-secret-token" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"My name is Eric"}],"model":"gpt-4o-mini","session_id":"user-123"}'
```

---

## Tools

All tools confirmed working on Windows with `gpt-4o-mini`.

### File tools
`Read` · `Write` · `Edit` · `Glob` · `Grep` · `LS`

### Shell tools
`Bash` *(requires WSL on Windows)* · `PowerShell` *(uses pwsh or powershell.exe)* · `REPL` *(Python, Node, Ruby, Perl)*

### Web tools
`WebFetch` · `WebSearch` *(requires `BRAVE_API_KEY` or `FIRECRAWL_API_KEY`)*

### Task tools
`TodoWrite` · `TaskCreate` · `TaskGet` · `TaskList` · `TaskOutput` · `TaskStop`

### Agent & interaction
`Agent` *(spawns an isolated sub-agent)* · `AskUserQuestion` · `Sleep`

### Notebook
`NotebookEdit` *(read/write Jupyter `.ipynb` cells)*

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `LLM_API_KEY` | Yes (CLI/Server) | API key for the primary LLM provider |
| `LLM_BASE_URL` | No | Provider base URL (default: `https://api.openai.com/v1`) |
| `SOVRANT_TOKEN` | Yes (Server only) | Bearer token for server auth |
| `SOVRANT_PORT` | No | Server port (default: `5200`) |
| `PROVIDER_BASE_URL` | No | Enables Anthropic-native provider (`/v1/messages` format) |
| `PROVIDER_API_KEY` | No | API key for the Anthropic-native provider |
| `OLLAMA_BASE_URL` | No | Ollama base URL (default: `http://localhost:11434/v1`) |
| `BRAVE_API_KEY` | No | Enables WebSearch via Brave Search API |
| `FIRECRAWL_API_KEY` | No | Enables WebSearch via FireCrawl (fallback to Brave) |

---

## Providers

| Provider | How to enable |
|---|---|
| OpenAI | `LLM_API_KEY=sk-...` (default) |
| Google AI Studio (Gemini) | `LLM_BASE_URL=https://generativelanguage.googleapis.com/v1beta/openai/` + `LLM_API_KEY=...` |
| Ollama (local) | `OLLAMA_BASE_URL=http://localhost:11434/v1` |
| Anthropic native | `PROVIDER_BASE_URL=https://api.anthropic.com` + `PROVIDER_API_KEY=...` |

> Gemma models via Google AI Studio do **not** support function calling over the OpenAI-compat endpoint. Use Gemini 2.5 Flash or better.

---

## Session Persistence

Sessions are stored as JSONL append-logs at `~/.sovrant/sessions/{id}.jsonl`.

```bash
# CLI: resume a session
dotnet run --project src/Sovrant.Cli -- --session my-session prompt "What did we discuss earlier?"

# Server: include session_id in the request body
{"session_id": "user-abc", "messages": [...]}
```

The server maintains a live in-memory session pool (`IRuntimeSessionPool`) — one `ConversationRuntime` per session ID kept alive across requests, safe for concurrent multi-user use.

---

## Docs

| Document | Contents |
|---|---|
| [`docs/server.md`](docs/server.md) | Full server API reference — all 8 endpoints, auth, CORS, streaming |
| [`docs/frontend-integration.md`](docs/frontend-integration.md) | Node.js proxy setup, browser SSE client, Replit integration |
| [`docs/engine-status.md`](docs/engine-status.md) | Tool test results, provider compatibility, known issues |
| [`docs/roadmap.md`](docs/roadmap.md) | Planned features and architectural direction |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Clients                                                        │
│  ┌─────────────┐   ┌──────────────────┐   ┌─────────────────┐  │
│  │  CLI (REPL) │   │  Sovrant.Server  │   │  Frontend / SDK │  │
│  │  prompt     │   │  HTTP :5200      │   │  (browser/Node) │  │
│  └──────┬──────┘   └────────┬─────────┘   └────────┬────────┘  │
└─────────┼───────────────────┼────────────────────────┼──────────┘
          │                   │  OpenAI-compat API      │
          │                   │  + session_id           │
          └─────────┬─────────┘─────────────────────────┘
                    │
          ┌─────────▼──────────────────────────────────────┐
          │  Sovrant.Runtime                               │
          │                                                │
          │  ConversationRuntime                           │
          │  ├── agentic loop (up to 20 tool rounds)       │
          │  ├── session history (List<InputMessage>)      │
          │  └── permission gate                           │
          │                                                │
          │  IRuntimeSessionPool  ── ConcurrentDictionary  │
          │  (one runtime per session_id, kept alive)      │
          │                                                │
          │  ISessionStore  ──  JSONL (~/.sovrant/sessions)│
          └────────┬────────────────────┬──────────────────┘
                   │                    │
          ┌────────▼────────┐  ┌────────▼─────────────────┐
          │  Sovrant.Api    │  │  Sovrant.Tools            │
          │                 │  │                           │
          │  SmartRouter    │  │  File: Read Write Edit    │
          │  ├── OpenAI     │  │       Glob Grep LS        │
          │  ├── Ollama     │  │  Shell: Bash PowerShell   │
          │  └── Anthropic  │  │        REPL               │
          │                 │  │  Web: WebFetch WebSearch  │
          │  health ping    │  │  Task: TaskCreate/Get/... │
          │  latency score  │  │  Agent  AskUser  Sleep    │
          │  cost weight    │  │  NotebookEdit  TodoWrite  │
          └────────┬────────┘  └───────────────────────────┘
                   │
          ┌────────▼──────────────────┐
          │  LLM Providers            │
          │  OpenAI / Google / Ollama │
          │  / Anthropic              │
          └───────────────────────────┘
```

### Key design decisions

**Always streaming internally.** The `ConversationRuntime` always sets `Stream: true` on the `MessagesRequest` regardless of what the client requested. The server decides independently whether to forward chunks as SSE (`stream: true`) or buffer them into a single response (`stream: false`). This avoids a dual code path in the agentic loop.

**Session pool (Option B).** The server keeps one `ConversationRuntime` alive per `session_id` in a `ConcurrentDictionary`. Each runtime holds its own message history in memory and loads from JSONL on first creation. This means history is available immediately without a database read on every turn. Note: concurrent turns on the same session are not yet serialized — Phase 9 adds a per-session lock to prevent history corruption under concurrent load.

**SmartRouter.** All LLM calls go through `ISmartRouter`. On startup it pings every configured provider and scores them by latency, cost weight, and error rate. Routing strategies: `Balanced` (default), `LowestCost`, `LowestLatency`. A provider can also be pinned by name (`--provider ollama`).

**Tool execution is permission-gated.** Every tool call goes through `IPermissionPolicy` before execution. The policy is swappable: `ModeAwarePermissionPolicy` (CLI) uses the `PermissionMode` enum; `MutablePermissionPolicy` (server) always allows (DontAsk) and can be updated live via `PUT /v1/config`.

---

## Tests

```bash
dotnet test   # 99 tests across 5 test projects
```
