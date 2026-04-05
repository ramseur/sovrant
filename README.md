# Sovrant

**An open-source, provider-agnostic agentic AI engine built on .NET 10.**

Sovrant is a general-purpose platform for running autonomous AI agents that can hold persistent conversations, use tools, coordinate teams of sub-agents, and integrate with any LLM provider. It is not limited to coding — Sovrant powers chat interfaces, research workflows, business process automation, content creation, project management, and any task that benefits from tool-augmented, session-persistent AI.

The engine runs as a **CLI agent** for individual use, an **OpenAI-compatible HTTP server** for team and application integration, an **MCP server** for IDE embedding, or via **webhooks** from Slack, Teams, Discord, and custom systems. Agents read and write files, execute shell commands, search the web, call tools autonomously, delegate to sub-agents, and maintain full conversation history across sessions — all with configurable permission controls.

**Runtime:** .NET 10 / C# 13
**License:** [see LICENSE]
**Status:** Engine fully functional. 41 tools. 14 server endpoints. Multi-agent team orchestration. MCP server mode. Frontend SDK. 416 tests passing.

---

## Table of Contents

- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Tools](#tools)
- [Agent System](#agent-system)
- [Providers](#providers)
- [Server API](#server-api)
- [Frontend SDK](#frontend-sdk)
- [MCP Server Mode](#mcp-server-mode)
- [LSP Integration](#lsp-integration)
- [Session Persistence](#session-persistence)
- [Agent Memory](#agent-memory)
- [Custom Slash Commands](#custom-slash-commands)
- [Configuration](#configuration)
- [Production Deployment](#production-deployment)
- [Tests](#tests)
- [Documentation](#documentation)
- [Roadmap](#roadmap)

---

## Key Features

### Provider-Agnostic LLM Routing
Connect to **any OpenAI-compatible API** — OpenAI, Anthropic (via proxy), Google Gemini, Ollama (local), or any provider that speaks the OpenAI chat completions format. The `SmartRouter` pings all configured providers on startup, scores them by latency, cost, and error rate, and routes each request to the optimal one. Switch providers by changing an environment variable — no code changes.

### 41 Built-in Tools
Agents autonomously use tools for file operations (read, write, edit, glob, grep), shell execution (Bash, PowerShell, REPL), web access (fetch, search), task management, plan/worktree mode, notebook editing, MCP resource access, LSP code intelligence, and multi-agent delegation. Up to 20 tool rounds per turn with automatic retries.

### Multi-Agent Team Orchestration
Create persistent named agents with specific roles, custom system prompts, and tool restrictions. Delegate tasks, track lifecycle status, and coordinate teams — all from within the agentic loop or via API. Two interchangeable backends: in-process async (default) or process-per-agent for hard isolation.

### Session Persistence
Every conversation is stored as a JSONL append-log. Resume sessions by name across CLI invocations or HTTP requests. The server keeps one runtime per session in memory for instant history access. Automatic context compaction when conversations exceed token limits.

### OpenAI-Compatible HTTP Server
Drop-in replacement for OpenAI's chat completions API with streaming (SSE) support. 14 endpoints covering chat, sessions, config, status, models, usage, and webhooks. Any frontend built for the OpenAI API works with Sovrant.

### MCP Server Mode
Expose all tools and resources via the Model Context Protocol (stdio transport). Connect Sovrant to VS Code, Cursor, Windsurf, or any MCP-aware IDE as a tool server — no extension required.

### Frontend SDK
TypeScript/JavaScript client with `SovrantClient`, SSE streaming support, and a React `useChat()` hook. Build chat UIs, dashboards, and integrations against the server API.

### IDE-Level Code Intelligence (LSP)
Built-in Language Server Protocol client supporting 18 languages. Agents get hover docs, go-to-definition, find references, diagnostics, and rename refactoring — without leaving the agentic loop.

### Webhook Integrations
Single endpoint that accepts messages from Slack, Teams, Discord, or custom systems. Build bots and automations that leverage the full agent toolkit.

### CI/CD Pipeline Support
Machine-readable JSON output mode (`--ci` flag) with non-zero exit on error. GitHub Actions action and GitLab CI templates included.

### Permission Controls
Five permission modes from fully interactive (`default` — asks before each tool use) to fully autonomous (`bypassPermissions`). Plan mode provides read-only access for safe exploration. Configurable per-session via API.

### Structured Logging & Observability
Rolling file logs, JSON structured output for log aggregators, configurable log levels, per-session token usage tracking, and rate limiting.

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An API key from any supported LLM provider

### CLI

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

# CI mode — machine-readable JSON output, non-zero exit on error
dotnet run --project src/Sovrant.Cli -- --ci --model gpt-4o-mini prompt "Fix the failing tests"
```

### Server

```bash
export LLM_API_KEY="sk-..."
export SOVRANT_TOKEN="your-secret-token"
export SOVRANT_PORT=5200    # optional, default 5200

dotnet run --project src/Sovrant.Server
```

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

# Persistent session
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer your-secret-token" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o-mini","messages":[{"role":"user","content":"My name is Eric"}],"session_id":"user-123"}'
```

### Permission Modes

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

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Clients                                                     │
│  ┌────────────┐   ┌──────────────────┐   ┌────────────────┐  │
│  │ CLI (REPL) │   │  Sovrant.Server  │   │ Frontend / SDK │  │
│  │ prompt     │   │  HTTP :5200      │   │ (browser/Node) │  │
│  └─────┬──────��   └────────┬─────────┘   └───────┬────────┘  │
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
         ┌──────────▼───────┐  ┌───────▼───────��──────────────┐
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
                    │          │  Team: Create Delete         │
                    │          │        Status Delegate       │
                    │          └──────────┬───────────────────┘
                    │                     │
         ┌─────────┘          ┌───────────▼──────────────────┐
         │                    │  Sovrant.Agents              │
         │                    │                              │
         │                    │  ITeamRegistry               │
         │                    │  SovrantAgentFactory          │
         │                    │  ├── AgentPrompts (6 roles)  │
         │                    │  └── FilteredToolRegistry    │
         │                    │                              │
         │                    │  IMultiAgentSystem            │
         │                    │  ├── Modern (in-process)     │
         │                    │  └── Legacy (process-based)  │
         │                    └──────────────────────────────┘
         │
         ┌──────────▼───────────────┐
         │  LLM Providers           │
         │  OpenAI · Gemini · Ollama│
         │  · Native API            │
         └──────────────────────────┘
```

### Projects

| Project | Description |
|---|---|
| `Sovrant.Cli` | Interactive REPL and one-shot `prompt` CLI. Entry point for local use. |
| `Sovrant.Server` | ASP.NET Core Minimal API — OpenAI-compatible endpoints plus session management and live config. |
| `Sovrant.Runtime` | Core agentic loop, session persistence (JSONL), permission system, tool executor, MCP client. |
| `Sovrant.Api` | LLM provider abstraction: OpenAI-compat, Ollama, native messages API. SmartRouter with health/latency/cost scoring. |
| `Sovrant.Tools` | All 41 tool implementations (core + LSP + team + MCP). |
| `Sovrant.Commands` | Slash commands for the REPL (`/help`, `/clear`, `/session`, `/memory`, etc.). |
| `Sovrant.Agents` | Multi-agent orchestration: team registry, agent factory, dual backends (modern in-process + legacy process-per-agent), role-specific prompts, tool filtering per agent. |
| `Sovrant.McpServer` | MCP server mode: exposes all tools and resources via stdio transport for IDE integration. |
| `Sovrant.Lsp` | Language Server Protocol client: JSON-RPC over stdio, manages language server lifecycle, 5 LSP tools. |
| `sdk/js` | TypeScript/JavaScript client SDK: `SovrantClient`, SSE streaming, React `useChat()` hook. |

### Key Design Decisions

**Always streaming internally.** `ConversationRuntime` always sets `Stream: true` on every `MessagesRequest`. The server decides independently whether to forward chunks as SSE or buffer into a single JSON response. One code path in the agentic loop regardless of the client's preference.

**One runtime per session.** The server keeps one `ConversationRuntime` alive per `session_id` in a `ConcurrentDictionary`. Each runtime holds its own message history in memory, loaded from JSONL on first access.

**SmartRouter with health fallback.** All LLM calls go through `ISmartRouter`. On startup it pings every configured provider and scores them by latency, cost, and error rate. If all providers fail the startup ping, the router falls back to the configured list rather than refusing to start.

**Tool execution is permission-gated.** Every tool call goes through `IPermissionPolicy` before execution. The CLI uses `ModeAwarePermissionPolicy` (interactive prompts based on `PermissionMode`). The server defaults to `DontAsk` and can be changed live via `PUT /v1/config`.

**Dual agent backends.** `IMultiAgentSystem` has two interchangeable backends. The active backend is selected at startup via `AGENT_MODE`; no other part of the system depends on the concrete implementation.

---

## Tools

41 tools available. All run inside the agentic loop with automatic retries up to 20 tool rounds per turn. Two additional tools (`MCPTool` and `McpAuth`) provide dynamic MCP server interaction.

### File
`Read` · `Write` · `Edit` · `Glob` · `Grep` · `LS`

### Shell
`Bash` *(WSL required on Windows)* · `PowerShell` · `REPL` *(Python, Node, Ruby, Perl)*

### Web
`WebFetch` · `WebSearch` *(requires `BRAVE_API_KEY` or `FIRECRAWL_API_KEY`, or set `LLM_WEB_SEARCH=true` for OpenAI native search)*

### Task Management
`TodoWrite` · `TaskCreate` · `TaskGet` · `TaskList` · `TaskOutput` · `TaskStop` · `TaskUpdate`

### Plan Mode & Worktree
`EnterPlanMode` · `ExitPlanMode` · `EnterWorktree` *(git worktree add)* · `ExitWorktree`

### Agent & Interaction
`Agent` *(spawns an isolated sub-agent session)* · `AskUserQuestion` · `Sleep`

### Team Orchestration
`TeamCreate` · `TeamDelete` · `TeamStatus` · `TeamDelegate`

*Create persistent named agents with roles, custom system prompts, and tool restrictions. Delegate tasks and track lifecycle. See [Agent System](#agent-system).*

### Discovery & Skills
`ToolSearch` *(keyword search over registered tools)* · `Skill` *(loads `.sovrant/skills/{name}.md` prompt template)*

### MCP Resources
`ListMcpResources` · `ReadMcpResource` · `MCPTool` *(dynamic proxy — calls any tool on any connected MCP server)*

### LSP (Language Server Protocol)
`LspHover` · `LspDefinition` · `LspReferences` · `LspDiagnostics` · `LspRename`

*Requires a language server configured in `~/.sovrant/settings.json`. See [LSP Integration](#lsp-integration).*

### Notebook
`NotebookEdit` *(read/write Jupyter `.ipynb` cells)*

---

## Agent System

Sovrant provides two complementary approaches to multi-agent work.

### Ad-hoc Sub-Agents (`Agent` tool)

The `Agent` tool spawns a lightweight, stateless sub-agent for a single task. The LLM decides when to use it — typically to parallelize independent research, explore multiple solution paths, or isolate risky operations.

- Each sub-agent gets its own `ConversationRuntime` with a fresh session
- No persistent identity — created, runs, and discarded
- Recursion depth limited to 5
- Same tool access as the parent

### Persistent Team Agents (Team tools)

The team tools (`TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`) provide structured, user-controlled multi-agent orchestration.

```
# Create a specialist agent
TeamCreate(name: "reviewer", role: "reviewer",
           prompt: "You review code for bugs and security issues",
           allowed_tools: ["Read", "Grep", "Glob"])

# Delegate work
TeamDelegate(member_id: "abc123",
             prompt: "Review the auth module for SQL injection")

# Check status
TeamStatus()  →  [{ name: "reviewer", status: "Completed", last_output: "Found 2 issues..." }]
```

Each team member:
- Has a **role** (General, Planner, Coder, Reviewer, Executor, Supervisor) with a role-specific system prompt
- Can be restricted to a **subset of tools** via `FilteredToolRegistry`
- Tracks **lifecycle state**: Idle → Running → Completed/Failed
- Is backed by a real `ConversationRuntime`, created lazily on first delegation

### Two Backend Modes

| | Modern (default) | Legacy |
|---|---|---|
| **Env var** | `AGENT_MODE=modern` | `AGENT_MODE=legacy` |
| **How it works** | In-process async — each agent runs as a `SovrantAgent` in the same process | Process-per-agent — spawns a separate OS process per agent |
| **Concurrency** | `SemaphoreSlim` enforcing `MaxConcurrentAgents` | One process per agent, OS-level isolation |
| **Cancellation** | Linked `CancellationTokenSource` (caller token + timeout) | Process tree kill on cancel |
| **Best for** | Most workloads — lower overhead, shared memory | Untrusted code execution, hard isolation |

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

## Server API

The server exposes an OpenAI-compatible chat completions endpoint plus session management, live config, status, and models. See [`docs/server.md`](docs/server.md) for the full API reference.

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Unauthenticated health check |
| `POST` | `/v1/chat/completions` | Agentic chat — streaming (SSE) or non-streaming |
| `GET` | `/v1/config` | Current live configuration |
| `PUT` | `/v1/config` | Update model, API key, base URL, or permission mode without restart |
| `GET` | `/v1/status` | Provider health, latency, and routing scores |
| `GET` | `/v1/models` | OpenAI-compatible model list |
| `GET` | `/v1/sessions` | List all saved session IDs |
| `GET` | `/v1/sessions/{id}` | Get message history and token totals for a session |
| `DELETE` | `/v1/sessions/{id}` | Delete a session |
| `GET` | `/v1/sessions/{id}/config` | Get per-session config overlay |
| `PUT` | `/v1/sessions/{id}/config` | Update per-session config without affecting other sessions |
| `GET` | `/v1/usage` | Per-session token usage summary |
| `POST` | `/v1/webhook` | Generic webhook — Slack, Teams, Discord, or custom sources |
| `GET` | `/v1/sessions/{id}/export` | Export session as human-readable markdown |

---

## Frontend SDK

The TypeScript/JavaScript SDK (`sdk/js`) provides:

- **`SovrantClient`** — typed HTTP client for all server endpoints
- **SSE streaming** — real-time token-by-token responses
- **React `useChat()` hook** — drop-in conversational UI component

See [`docs/frontend-integration.md`](docs/frontend-integration.md) for proxy setup, browser SSE, and Replit integration.

---

## MCP Server Mode

Sovrant can run as an MCP (Model Context Protocol) server, exposing all tools and resources via stdio transport. This lets MCP-aware IDEs use Sovrant as a tool backend.

```bash
dotnet run --project src/Sovrant.McpServer -- --token $SOVRANT_MCP_TOKEN
```

Supports VS Code (GitHub Copilot), Cursor, and Windsurf. See [`docs/mcp-server.md`](docs/mcp-server.md) for IDE configuration.

---

## LSP Integration

Built-in Language Server Protocol client giving agents IDE-level code intelligence without leaving the agentic loop.

**5 tools:** `LspHover` · `LspDefinition` · `LspReferences` · `LspDiagnostics` · `LspRename`

**18 languages supported:** C#, Go, Python, Rust, TypeScript, TSX, JavaScript, JSX, Java, C, C++, Ruby, Swift, Kotlin, Zig, Lua (and header files).

Configure in `~/.sovrant/settings.json`:

```json
{
  "lsp_servers": {
    "csharp": { "command": "OmniSharp", "args": ["-lsp"] },
    "python": { "command": "pylsp", "args": [] },
    "typescript": { "command": "typescript-language-server", "args": ["--stdio"] }
  }
}
```

Any language server that speaks LSP over stdio can be plugged in.

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

## Agent Memory

Sovrant reads two memory files at the start of every session and prepends their contents to the system prompt.

| File | Scope |
|---|---|
| `~/.sovrant/memory.md` | Global — your preferences, personal notes |
| `.sovrant/memory.md` | Project — architecture notes, conventions |

Use `/memory` (or `/mem`) in the REPL to view or create these files.

---

## Custom Slash Commands

Place a markdown file at `.sovrant/commands/{name}.md`. Invoking `/{name}` in the REPL injects the file's content as a user message. Use `$ARGUMENTS` as a placeholder.

```
.sovrant/commands/review.md  →  /review src/Auth.cs
```

---

## Configuration

### Environment Variables

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
| `SOVRANT_COMPACT_THRESHOLD` | No | Input token count that triggers context auto-compaction (default: `80000`). Set to `0` to disable. |
| `LLM_WEB_SEARCH` | No | Set to `true` to use OpenAI's Responses API with `web_search_preview` |
| `BRAVE_API_KEY` | No | Enables WebSearch via Brave Search API |
| `FIRECRAWL_API_KEY` | No | Enables WebSearch via FireCrawl (fallback if Brave not set) |
| `SOVRANT_SESSION_TTL_SECONDS` | No | Idle session TTL before automatic eviction (default: `3600`) |
| `SOVRANT_MAX_SESSIONS` | No | Maximum active sessions in the server pool (default: `500`). LRU eviction when exceeded. |
| `SOVRANT_LOG_LEVEL` | No | `Verbose`, `Debug`, `Information` (default), `Warning`, `Error`, `Fatal` |
| `SOVRANT_LOG_FILE` | No | Rolling file path pattern (default: `~/.sovrant/logs/sovrant-{Date}.log`). Empty = disabled. |
| `SOVRANT_LOG_CONSOLE` | No | Write logs to stdout (default: `true`). Set to `false` to silence. |
| `SOVRANT_LOG_FORMAT` | No | `text` (default) or `json` (structured) |
| `SOVRANT_RATE_LIMIT_RPM` | No | Per-session rate limit: requests per minute (default: `60`). Returns `429` when exceeded. |
| `SOVRANT_MCP_TOKEN` | No | Required bearer token for MCP server mode. Unset = no auth. |
| `SOVRANT_MCP_TOOLS` | No | Comma-separated allow-list of tools to expose in MCP server mode. Unset = all tools. |

---

## Production Deployment

### CLI — Native AOT

The CLI is short-lived and invoked repeatedly, so cold start time matters. Native AOT produces a single self-contained binary with no .NET runtime dependency.

```bash
dotnet publish src/Sovrant.Cli/Sovrant.Cli.csproj \
  -c Release -r linux-x64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:OptimizeSpeed=true \
  -p:InvariantGlobalization=true \
  -p:StripSymbols=true \
  -o ./publish/cli
```

### Server — ReadyToRun + Trimming

The server is long-running — JIT warmup is paid once, then tiered JIT optimizes hot paths. ReadyToRun gives fast startup without AOT compatibility issues.

```bash
dotnet publish src/Sovrant.Server/Sovrant.Server.csproj \
  -c Release -r linux-x64 \
  --self-contained true \
  -p:PublishReadyToRun=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=link \
  -o ./publish/server
```

### Reverse Proxy (nginx)

Disable response buffering for SSE streaming:

```nginx
location /v1/chat/completions {
    proxy_pass http://localhost:5200;
    proxy_buffering off;
    proxy_cache off;
    proxy_set_header Connection '';
    proxy_http_version 1.1;
    chunked_transfer_encoding off;
}
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .

RUN dotnet publish src/Sovrant.Server/Sovrant.Server.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishReadyToRun=true -p:PublishTrimmed=true -p:TrimMode=link \
    -o /publish/server

RUN dotnet publish src/Sovrant.Cli/Sovrant.Cli.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishAot=true -p:InvariantGlobalization=true -p:StripSymbols=true \
    -o /publish/cli

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
COPY --from=build /publish/server /app/server
COPY --from=build /publish/cli /app/cli
ENV PATH="/app/cli:${PATH}"
WORKDIR /app/server
EXPOSE 5200
ENTRYPOINT ["./sovrant-server"]
```

### Windows

Replace `-r linux-x64` with `-r win-x64` in all commands above.

---

## Tests

```bash
dotnet test Sovrant.slnx   # 416 tests across 9 projects
```

| Project | Tests |
|---|---|
| `Sovrant.Api.Tests` | 28 |
| `Sovrant.Runtime.Tests` | 106 |
| `Sovrant.Server.Tests` | 73 |
| `Sovrant.McpServer.Tests` | 30 |
| `Sovrant.Lsp.Tests` | 26 |
| `Sovrant.Tools.Tests` | 72 |
| `Sovrant.Commands.Tests` | 22 |
| `Sovrant.Agents.Tests` | 58 |
| `Sovrant.Integration.Tests` | 1 |

---

## Documentation

| Document | Contents |
|---|---|
| [`docs/server.md`](docs/server.md) | Full server API reference — all 14 endpoints, auth, CORS, streaming format |
| [`docs/frontend-integration.md`](docs/frontend-integration.md) | Node.js proxy setup, browser SSE client, Replit integration |
| [`docs/engine-status.md`](docs/engine-status.md) | Tool test results, provider compatibility, known issues |
| [`docs/ci-cd.md`](docs/ci-cd.md) | CI/CD integration — `--ci` flag, GitHub Actions action, GitLab CI template |
| [`docs/webhooks.md`](docs/webhooks.md) | Webhook endpoint, Slack bot setup, Teams/Discord integration guides |
| [`docs/mcp-server.md`](docs/mcp-server.md) | MCP server mode — IDE config, available tools/resources, env vars |
| [`docs/roadmap.md`](docs/roadmap.md) | Full development roadmap — 31 phases, current status, upcoming features |
| [`docs/code-review.md`](docs/code-review.md) | Code review findings and coverage report |

---

## Roadmap

See [`docs/roadmap.md`](docs/roadmap.md) for the full development roadmap. Upcoming phases include:

| Phase | What |
|---|---|
| 20 | Hook lifecycle system (7 events, 3 profiles) |
| 21 | 24 specialized agent templates across 3 categories |
| 22 | Verification loop & quality gates (6 phases) |
| 23 | Governance, security monitoring & audit |
| 24 | 32 built-in skills across 7 domains |
| 25 | Multi-layered memory (session summaries, learned patterns, instincts) |
| 26 | Cost tracking & token budget management |
| 27 | Eval-driven development framework |
| 28 | Swarm orchestrator (auto-decomposition + DAG execution) |
