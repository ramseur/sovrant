# Sovrant

**An open-source, provider-agnostic agentic AI engine built on .NET 10.**

Sovrant is a general-purpose platform for running autonomous AI agents that can hold persistent conversations, use tools, coordinate teams of sub-agents, and integrate with any LLM provider. It is not limited to coding — Sovrant powers chat interfaces, research workflows, business process automation, content creation, project management, and any task that benefits from tool-augmented, session-persistent AI.

The engine runs as a **CLI agent** for individual use, an **OpenAI-compatible HTTP server** for team and application integration, an **MCP server** for IDE embedding, a **desktop application** (Windows/macOS/Linux via Avalonia), a **web application** (Blazor Server), or via **webhooks** from Slack, Teams, Discord, and custom systems. Agents read and write files, execute shell commands, search the web, call tools autonomously, delegate to sub-agents, and maintain full conversation history across sessions — all with configurable permission controls.

> **Architecture note:** The CLI, Server, Desktop, and Web are independent frontends. All consume the runtime layer (`Sovrant.Runtime`) directly — the server does **not** depend on the CLI, and the desktop/web apps run the runtime in-process. You can deploy any frontend independently.

**Runtime:** .NET 10 / C# 14
**License:** [see LICENSE]
**Status:** Engine fully functional. 45 tools. 24 agent templates. 32 built-in skills. 95 server endpoints. Multi-agent team orchestration. Swarm orchestrator. Eval framework. MCP server mode. Desktop app. Web app. Frontend SDK. 1,285 tests passing.

---

## Table of Contents

- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Tools](#tools)
- [Agent System](#agent-system)
  - [Team vs Swarm — When to Use Which](#team-vs-swarm--when-to-use-which)
- [Swarm Orchestrator](#swarm-orchestrator)
- [Eval Framework](#eval-framework)
- [Providers](#providers)
- [Server API](#server-api)
- [Desktop App](#desktop-app)
- [Web App](#web-app)
- [Frontend SDK](#frontend-sdk)
- [MCP Server Mode](#mcp-server-mode)
- [LSP Integration](#lsp-integration)
- [Session Persistence](#session-persistence)
- [Agent Memory](#agent-memory)
- [Custom Slash Commands](#custom-slash-commands)
- [Configuration](#configuration)
- [Production Deployment](#production-deployment)
- [Tests](#tests)
- [Roadmap](#roadmap)
- [Documentation](#documentation)

---

## Key Features

### Provider-Agnostic LLM Routing
Connect to **any OpenAI-compatible API** — OpenAI, OpenRouter, Google Gemini, DeepSeek, Groq, Mistral, Together AI, Ollama (local), LM Studio (local), Azure OpenAI, or any provider that speaks the OpenAI chat completions format. The `SmartRouter` pings all configured providers on startup, scores them by latency, cost, and error rate, and routes each request to the optimal one. Switch providers by changing an environment variable or from the desktop/web settings UI — no code changes.

### Intent-Aware Model Routing
Automatically selects the best model for each turn based on what you're asking. A rule-based `IntentClassifier` categorizes every input into one of 10 intent classes (`SimpleQa`, `CodeGeneration`, `Refactor`, `Planning`, `Debugging`, etc.) with a complexity score (0.0-1.0), then routes to the appropriate model tier (`fast` / `standard` / `high`). The `ModelTierResolver` auto-assigns your discovered models into tiers from OpenRouter pricing data — no manual configuration needed. Simple questions go to cheap/fast models; complex reasoning goes to your strongest model.

**Free models only mode** — set `SOVRANT_FREE_MODELS_ONLY=true` to restrict routing to zero-cost models. On OpenRouter this means only `:free` variants are eligible; local/self-hosted models (Ollama) are always included. This prevents accidental charges when using OpenRouter's free tier.

**Tool-aware routing** — when the request includes tools, the resolver automatically filters to models that support native tool use, preventing 404 errors from providers that don't support `tool_calls`.

Configure via `.sovrant/routing.json` or environment variables:
```bash
# Use OpenRouter as the provider
export LLM_BASE_URL="https://openrouter.ai/api/v1"
export LLM_API_KEY="sk-or-v1-..."
export OPENROUTER_API_KEY="sk-or-v1-..."  # enables live model discovery
export SOVRANT_MODEL="google/gemma-4-31b-it:free"

# Restrict to free models only (no charges)
export SOVRANT_FREE_MODELS_ONLY=true

# Inspect tier assignments
sovrant router models
sovrant router status
```

### Desktop Application (Avalonia)
Full-featured desktop app for Windows, macOS, and Linux. Dark/light theming, sidebar navigation with workspace/project context, streaming chat with tool use blocks and inline approve/deny, markdown rendering, multi-provider settings with profile management, and CRUD pages for all knowledge and orchestration entities (artifacts, tools, skills, agents, memory, projects, workspaces, multi-agent teams, integrations, automations, governance). First-run setup wizard for provider configuration.

### Web Application (Blazor Server)
Browser-based UI running the full runtime in-process via Blazor Server (SignalR). Matches the desktop feature set: streaming chat, tool use with confirmation, sidebar navigation, workspace/project management, settings with live model switching, and all 15 pages. Runs on port 5100. Designed for dual-mode: embedded mode (current) runs the runtime in-process; remote mode (future) can talk to `Sovrant.Server` via HTTP without changing any components.

### Swarm Orchestrator (Auto-Decomposition + Parallel DAG Execution)
Submit a single complex prompt and the swarm auto-decomposes it into a task DAG, executes tasks in parallel waves via specialized agents, enforces file-level locking and token budgets, and runs an optional quality gate review. OFF by default — enable in `.sovrant/swarm.json`. Different orchestrations can reference different multi-agent teams, so the same swarm engine can leverage purpose-built agent rosters per use case. Available via CLI (`sovrant swarm "task"`), the `Swarm` tool, `/swarm` slash command, and `POST /v1/swarm` (SSE streaming).

### Eval-Driven Development Framework
Define evaluation suites as JSON files in `.sovrant/evals/`. Three grader types: code (exit code + regex), model (LLM-judged with rubric), and human (manual review). Pass@1 and pass@k metrics with configurable attempt counts. Trend tracking via `EvalResultStore`. Run from CLI (`/eval`), programmatically (`IEvalRunner`), or via API (`POST /v1/evals/run`).

### 45 Built-in Tools
Agents autonomously use tools for file operations (read, write, edit, glob, grep), shell execution (Bash, PowerShell, REPL), web access (fetch, search), task management, plan/worktree mode, notebook editing, MCP resource access, LSP code intelligence, code verification, skill execution, multi-agent delegation, and swarm orchestration. Up to 20 tool rounds per turn with automatic retries.

### 24 Specialized Agent Templates
Define reusable agent roles as `.md` files with YAML frontmatter — each specifying a name, recommended model tier, system prompt, and allowed tools. 24 built-in templates ship with the engine (10 general-purpose, 8 code-specific, 6 creative/domain). Drop custom templates into `.sovrant/agents/` to override built-ins or add your own. Agents spawned via the `Agent` tool or `TeamCreate` can reference a template by name for purpose-built behavior without manual prompt engineering.

### 32 Built-in Skills (Composable Workflow Packages)
Skills are single `.md` files — YAML frontmatter (name, trigger, agents, tools) plus a markdown body with steps and instructions. When a skill needs embedded code (JavaScript, Python, etc.), it lives inside a fenced code block in the body — no sidecar files, no directory-per-skill. 32 built-in skills across 7 domains: research (5), writing (5), business (5), project management (4), coding (7), media (3), and agent infrastructure (3). Invoke via `/trigger` slash commands or programmatically. Create new skills at runtime with `SkillCreate`.

### Multi-Agent Team Orchestration
Create persistent named agents with specific roles, custom system prompts, and tool restrictions. Delegate tasks, track lifecycle status, and coordinate teams — all from within the agentic loop or via API. Two interchangeable backends: process-per-agent for hard isolation (default) or in-process async for lower overhead.

### Session Persistence
Every conversation is stored in a SQLite database (`~/.sovrant/data/sovrant.db`) with full-text search via FTS5. Resume sessions by name across CLI invocations or HTTP requests. The server keeps one runtime per session in memory for instant history access. Automatic context compaction when conversations exceed token limits. Optional dual-write to legacy JSONL files via `SOVRANT_SESSION_JSONL=true`. See [Persistence](docs/persistence.md).

### OpenAI-Compatible HTTP Server
Drop-in replacement for OpenAI's chat completions API with streaming (SSE) support. 95 endpoints covering chat, sessions, config, status, models, usage, webhooks, projects, workspaces, teams, agents, skills, tools, evals, swarm, and user management. Any frontend built for the OpenAI API works with Sovrant.

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

### Clone & Build

```bash
git clone https://github.com/ramseur/sovrant-engine.git
cd sovrant-engine
git checkout sovrant-openc-dotnet-port
dotnet restore
dotnet build
```

### Desktop App

The fastest way to get started. Full GUI with settings, chat, and all management pages.

```bash
dotnet run --project src/Sovrant.Desktop
```

On first launch, the setup wizard guides you through provider configuration (API key, model selection). Supports OpenAI, OpenRouter, DeepSeek, Groq, Mistral, Together AI, Google, Azure OpenAI, Ollama, and LM Studio.

### Web App

Browser-based UI on port 5100 with the full runtime embedded.

```bash
dotnet run --project src/Sovrant.Web
# Open http://localhost:5100
```

### CLI

**Linux / macOS / WSL:**
```bash
export LLM_API_KEY="sk-..."
# Optional: use a different provider
export LLM_BASE_URL="https://generativelanguage.googleapis.com/v1beta/openai/"
```

**Windows (PowerShell):**
```powershell
$env:LLM_API_KEY = "sk-..."
# Optional: use a different provider
$env:LLM_BASE_URL = "https://generativelanguage.googleapis.com/v1beta/openai/"
```

**Then run:**
```bash
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

**Linux / macOS / WSL:**
```bash
export LLM_API_KEY="sk-..."
export SOVRANT_TOKEN="your-secret-token"
export SOVRANT_PORT=5200    # optional, default 5200
```

**Windows (PowerShell):**
```powershell
$env:LLM_API_KEY = "sk-..."
$env:SOVRANT_TOKEN = "your-secret-token"
$env:SOVRANT_PORT = 5200    # optional, default 5200
```

**Then run:**
```bash
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
│  ┌──────────┐ ┌────────────┐ ┌──────────┐ ┌──────────────┐  │
│  │ CLI      │ │  Desktop   │ │   Web    │ │  Server      │  │
│  │ (REPL)   │ │ (Avalonia) │ │ (Blazor) │ │ HTTP :5200   │  │
│  └────┬─────┘ └─────┬──────┘ └────┬─────┘ └──────┬───────┘  │
│       │             │             │               │          │
│  ┌────┴─────────────┴─────────────┴───────────────┘          │
│  │  All consume Sovrant.Runtime in-process                   │
└──┼───────────────────────────────────────────────────────────┘
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
   │  ISessionStore  (SQLite ~/.sovrant/data/)       │
   └──────────┬──────────────────┬──────────────────┘
              │                  │
   ┌──────────▼───────┐  ┌──────▼────────────────────┐
   │  Sovrant.Api     │  │  Sovrant.Tools             │
   │                  │  │                            │
   │  SmartRouter     │  │  File:  Read Write Edit    │
   │  ├── OpenAI      │  │         Glob Grep LS       │
   │  ├── OpenRouter   │  │  Shell: Bash PowerShell   │
   │  ├── DeepSeek    │  │         REPL               │
   │  ├── Groq        │  │  Web:   WebFetch WebSearch │
   │  ├── Mistral     │  │  Tasks: TodoWrite Task*    │
   │  ├── Google      │  │  Agent  AskUser  Sleep     │
   │  ├── Ollama      │  │  PlanMode  Worktree        │
   │  ├── LM Studio   │  │  Skill  ToolSearch         │
   │  └── Custom      │  │  MCP: ListResources Read   │
   │                  │  │  NotebookEdit              │
   │  health ping     │  │  Team: Create Delete       │
   │  latency/cost    │  │        Status Delegate     │
   │  error rate      │  └──────────┬─────────────────┘
   └──────────┬───────┘             │
              │          ┌──────────▼──────────────────┐
              │          │  Sovrant.Agents              │
              │          │                              │
              │          │  ITeamRegistry               │
              │          │  SovrantAgentFactory          │
              │          │  ├── AgentPrompts (6 roles)  │
              │          │  └── FilteredToolRegistry    │
              │          │                              │
              │          │  IMultiAgentSystem            │
              │          │  ├── Isolated (process-based) │
              │          │  └── Shared (in-process)     │
              │          │                              │
              │          │  Swarm Orchestrator           │
              │          │  ├── LlmSwarmDecomposer      │
              │          │  ├── SwarmOrchestrator (DAG)  │
              │          │  └── SwarmQualityGate         │
              │          └──────────────────────────────┘
              │
   ┌──────────▼───────────────┐
   │  LLM Providers           │
   │  OpenAI · OpenRouter ·   │
   │  DeepSeek · Groq ·       │
   │  Mistral · Google ·      │
   │  Together AI · Ollama ·  │
   │  LM Studio · Azure ·    │
   │  Custom                  │
   └──────────────────────────┘
```

### Projects

| Project | Description |
|---|---|
| `Sovrant.Cli` | Interactive REPL and one-shot `prompt` CLI. Entry point for local use. |
| `Sovrant.Server` | ASP.NET Core Minimal API — OpenAI-compatible endpoints plus session, project, workspace, team, and user management. 95 endpoints. |
| `Sovrant.Desktop` | Avalonia desktop app — full GUI with streaming chat, tool use, settings, and all knowledge/orchestration CRUD pages. |
| `Sovrant.Web` | Blazor Server web app — browser-based UI with embedded runtime, matching the desktop feature set. Port 5100. |
| `Sovrant.Runtime` | Core agentic loop, SQLite persistence layer (sessions, memory, audit, credentials, token usage), permission system, tool executor, MCP client. |
| `Sovrant.Api` | LLM provider abstraction: OpenAI-compat, Ollama, native messages API. SmartRouter with health/latency/cost scoring. Per-model max_tokens capping. |
| `Sovrant.Tools` | All 45 tool implementations (core + LSP + team + swarm + MCP + quality + skills). 32 built-in skill `.md` files. |
| `Sovrant.Commands` | Slash commands for the REPL (`/help`, `/clear`, `/session`, `/memory`, etc.). |
| `Sovrant.Agents` | Multi-agent orchestration: team registry, agent factory, dual backends (isolated process-per-agent + shared in-process), 24 agent templates, tool filtering per agent, swarm orchestrator (auto-decomposition, DAG execution, file locking, quality gate). |
| `Sovrant.McpServer` | MCP server mode: exposes all tools and resources via stdio transport for IDE integration. |
| `Sovrant.Lsp` | Language Server Protocol client: JSON-RPC over stdio, manages language server lifecycle, 5 LSP tools. |
| `sdk/js` | TypeScript/JavaScript client SDK: `SovrantClient`, SSE streaming, React `useChat()` hook. |

### Key Design Decisions

**Always streaming internally.** `ConversationRuntime` always sets `Stream: true` on every `MessagesRequest`. The server decides independently whether to forward chunks as SSE or buffer into a single JSON response. One code path in the agentic loop regardless of the client's preference.

**One runtime per session.** The server keeps one `ConversationRuntime` alive per `session_id` in a `ConcurrentDictionary`. Each runtime holds its own message history in memory, loaded from SQLite on first access.

**SmartRouter with health fallback.** All LLM calls go through `ISmartRouter`. On startup it pings every configured provider and scores them by latency, cost, and error rate. If all providers fail the startup ping, the router falls back to the configured list rather than refusing to start.

**Tool execution is permission-gated.** Every tool call goes through `IPermissionPolicy` before execution. The CLI uses `ModeAwarePermissionPolicy` (interactive prompts based on `PermissionMode`). The server defaults to `DontAsk` and can be changed live via `PUT /v1/config`.

**Dual agent backends.** `IMultiAgentSystem` has two interchangeable backends. The active backend is selected at startup via `AGENT_MODE`; no other part of the system depends on the concrete implementation.

**Per-model token capping.** The runtime automatically caps `max_tokens` / `max_completion_tokens` to model-specific limits (e.g., gpt-4o: 16,384, gpt-4.1: 32,768, legacy gpt-4: 4,096) to prevent 400 errors from providers that enforce strict limits. OpenAI's direct API also requires `max_completion_tokens` instead of `max_tokens` — the provider layer handles this automatically.

---

## Tools

45 tools available. All run inside the agentic loop with automatic retries up to 20 tool rounds per turn. Two additional tools (`MCPTool` and `McpAuth`) provide dynamic MCP server interaction.

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

### Swarm Orchestration
`Swarm` *(auto-decompose + parallel DAG execution with optional team)* · `SwarmStatus` *(live progress tracking)*

*Submit complex tasks for automatic decomposition into parallel waves. Optionally reference a multi-agent team. See [Swarm Orchestrator](#swarm-orchestrator).*

### Discovery & Skills
`ToolSearch` *(keyword search over registered tools)* · `Skill` *(loads and executes a skill by name or /trigger)* · `SkillCreate` *(creates new `.md` skill files at runtime)*

### Quality
`Verify` *(6-phase quality gate: build, type-check, lint, test, security scan, diff review)*

### MCP Resources
`ListMcpResources` · `ReadMcpResource` · `MCPTool` *(dynamic proxy — calls any tool on any connected MCP server)*

### LSP (Language Server Protocol)
`LspHover` · `LspDefinition` · `LspReferences` · `LspDiagnostics` · `LspRename`

*Requires a language server configured in `~/.sovrant/settings.json`. See [LSP Integration](#lsp-integration).*

### Notebook
`NotebookEdit` *(read/write Jupyter `.ipynb` cells)*

---

## Agent System

Sovrant provides three layers of multi-agent capability: ad-hoc sub-agents for quick delegation, reusable agent templates for purpose-built roles, and persistent team agents for structured orchestration.

### Ad-hoc Sub-Agents (`Agent` tool)

The `Agent` tool spawns a lightweight, stateless sub-agent for a single task. The LLM decides when to use it — typically to parallelize independent research, explore multiple solution paths, or isolate risky operations.

- Each sub-agent gets its own `ConversationRuntime` with a fresh session
- No persistent identity — created, runs, and discarded
- Recursion depth limited to 5
- Same tool access as the parent (unless a template restricts it)

### 24 Agent Templates (`.md` files)

Agent templates are `.md` files with YAML frontmatter — each defines a reusable agent persona with a name, recommended model tier (High/Standard/Fast), system prompt body, and optional tool restrictions. Templates live in `src/Sovrant.Agents/agents/` (built-in) and can be overridden or extended by dropping `.md` files into `.sovrant/agents/`.

**24 built-in templates across 3 categories:**

| Category | Templates |
|---|---|
| **General-purpose (10)** | researcher, writer, analyst, planner, summarizer, translator, tutor, debater, advisor, fact-checker |
| **Code-specific (8)** | coder, reviewer, debugger, refactorer, test-writer, architect, doc-writer, security-auditor |
| **Creative / Domain (6)** | storyteller, copywriter, data-scientist, sysadmin, product-manager, interviewer |

Use a template from the `Agent` tool or `TeamCreate`:

```
# Spawn a purpose-built agent from a template
Agent(template: "security-auditor", prompt: "Audit the auth module for OWASP Top 10")

# Create a team member backed by a template
TeamCreate(name: "reviewer", template: "reviewer")
```

Each template specifies a `recommended_level` that maps to a model string via `ModelLevels` config — so a "Fast" agent can use a cheaper model while a "High" agent gets the most capable one, without hardcoding model names.

### Persistent Team Agents (Team tools)

The team tools (`TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`) provide structured, user-controlled multi-agent orchestration.

```
# Create a specialist agent (with or without a template)
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
- Can optionally be backed by an **agent template** for pre-configured behavior
- Can be restricted to a **subset of tools** via `FilteredToolRegistry`
- Tracks **lifecycle state**: Idle → Running → Completed/Failed
- Is backed by a real `ConversationRuntime`, created lazily on first delegation

### Two Backend Modes

| | Isolated (default) | Shared |
|---|---|---|
| **Env var** | `AGENT_MODE=isolated` (or unset) | `AGENT_MODE=shared` |
| **How it works** | Process-per-agent — spawns a separate OS process per agent | In-process async — each agent runs as a `SovrantAgent` in the same process |
| **Concurrency** | One process per agent, OS-level isolation | `SemaphoreSlim` enforcing `MaxConcurrentAgents` |
| **Cancellation** | Process tree kill on cancel | Linked `CancellationTokenSource` (caller token + timeout) |
| **Best for** | Security-sensitive workloads, untrusted code, production defaults | Lower overhead when agents share trusted memory space |

### Team vs Swarm — When to Use Which

Sovrant ships **two distinct multi-agent systems** that share the same agent factory and templates underneath but solve very different problems. The short version:

> **Team** is "the LLM has co-workers it can hire and fire mid-conversation."
> **Swarm** is "the user has a build system for one big task — break it up, schedule it, run it in parallel, gate the result."

| Dimension | **Team** | **Swarm** |
|---|---|---|
| **Trigger** | LLM tool calls (`TeamCreate`, `TeamDelegate`, …) inside a conversation | User runs `sovrant swarm "<goal>"` from the CLI, or POST `/v1/swarm` |
| **Lifecycle** | Persistent across turns — the LLM creates members and reuses them | Ephemeral — one swarm, one task, torn down at the end |
| **Decomposition** | Caller (the LLM) decides what to delegate and when | `LlmSwarmDecomposer` calls an LLM to produce a task DAG with deps and predicted file touches |
| **Concurrency** | Sequential — one delegation at a time | Wave-based parallelism with topological sort + concurrency cap |
| **Coordination** | None — each delegation is independent | File locks (`SwarmFileLockManager`), retries, quality gate, token budget |

**Use Team when** the model needs persistent specialists it can call repeatedly during the same conversation.

**Use Swarm when** you have one large task that benefits from parallel sub-tasks with file-touch coordination.

**They are not exclusive.** A swarm plan can have a `TeamId` to draw its workers from a Team registry instead of from templates.

---

## Swarm Orchestrator

The swarm orchestrator auto-decomposes complex tasks into parallel DAGs and executes them across multiple agents. See [Team vs Swarm](#team-vs-swarm--when-to-use-which) above for how it differs from the persistent Team system.

### How It Works

```
User prompt → [1. Decompose] → SwarmPlan (task DAG with waves)
                                    ↓
              [2. Execute]   → wave-by-wave parallel execution
                                    ↓
              [3. Quality Gate] → score + verdict → SwarmResult
```

**Phase 1 — Decomposition:** A high-level LLM agent analyzes the prompt and produces a JSON task array with dependencies, file predictions, and agent template assignments. Kahn's topological sort assigns parallel wave indices.

**Phase 2 — Execution:** The orchestrator processes waves sequentially, tasks within a wave in parallel (bounded by `SemaphoreSlim`). File-level pessimistic locking prevents conflicts. Token budget enforcement with auto-cancellation of remaining waves on breach. Per-task retry with configurable timeout.

**Phase 3 — Quality Gate (optional):** A reviewer agent scores the combined output on a 1-10 scale with pass/needs_revision/fail verdict.

### Configuration

Config lives in `.sovrant/swarm.json` (OFF by default):

```json
{
  "enabled": true,
  "max_concurrent": 4,
  "max_token_budget": 500000,
  "max_retries": 1,
  "quality_gate": true,
  "decomposer_level": "High",
  "worker_level": "Standard",
  "task_timeout_seconds": 300,
  "templates": {}
}
```

---

## Eval Framework

Define evaluation suites as JSON files in `.sovrant/evals/`. Run them from the CLI, programmatically, or via the server API.

### Grader Types

| Type | How It Works |
|---|---|
| **Code** | Runs a command with the output as a temp file. Checks exit code + optional regex pattern. |
| **Model** | Sends output to an LLM with a rubric prompt. Parses `VERDICT: PASS/FAIL` + `SCORE: N`. |
| **Human** | Returns "pending human review" — designed for manual assessment workflows. |

### Usage

```bash
# REPL
/eval my-suite
/eval --history my-suite

# API
POST /v1/evals/run  {"suite_name": "my-suite", "tag": "regression"}
GET  /v1/evals
GET  /v1/evals/{name}/history
```

---

## Providers

| Provider | How to enable |
|---|---|
| OpenAI | `LLM_API_KEY=sk-...` (default base URL) |
| OpenRouter | `LLM_BASE_URL=https://openrouter.ai/api/v1` + `LLM_API_KEY=sk-or-v1-...` |
| Google AI Studio (Gemini) | `LLM_BASE_URL=https://generativelanguage.googleapis.com/v1beta/openai/` + `LLM_API_KEY=...` |
| DeepSeek | `LLM_BASE_URL=https://api.deepseek.com/v1` + `LLM_API_KEY=...` |
| Groq | `LLM_BASE_URL=https://api.groq.com/openai/v1` + `LLM_API_KEY=...` |
| Mistral | `LLM_BASE_URL=https://api.mistral.ai/v1` + `LLM_API_KEY=...` |
| Together AI | `LLM_BASE_URL=https://api.together.xyz/v1` + `LLM_API_KEY=...` |
| Azure OpenAI | `LLM_BASE_URL=https://your-resource.openai.azure.com/openai/deployments/your-deployment/` + `LLM_API_KEY=...` |
| Ollama (local) | `LLM_BASE_URL=http://localhost:11434/v1` (no API key needed) |
| LM Studio (local) | `LLM_BASE_URL=http://localhost:1234/v1` (no API key needed) |

> The desktop and web apps provide a GUI for managing providers with saved profiles — no environment variables needed.

> Gemma models via Google AI Studio do not support function calling over the OpenAI-compat endpoint. Use Gemini 2.5 Flash or a newer Gemini model.

The `SmartRouter` pings all configured providers on startup, scores them by latency, cost, and error rate, and routes each request to the optimal one. Use `ROUTER_MODE=Fixed` to always route to the first configured provider, or `ROUTER_STRATEGY=Latency` / `Cost` to change the scoring weight.

---

## Server API

The server exposes an OpenAI-compatible chat completions endpoint plus comprehensive management APIs. 95 endpoints covering chat, sessions, config, status, models, usage, webhooks, projects, workspaces, teams, agents, skills, tools, evals, swarm, and user management. See [`docs/server.md`](docs/server.md) for the full API reference.

---

## Desktop App

The Avalonia-based desktop app provides a full GUI for interacting with the Sovrant runtime. It runs on Windows, macOS, and Linux.

**Features:**
- Streaming chat with thinking indicators, tool use blocks, and inline approve/deny
- Markdown rendering with code blocks, lists, headings, and inline formatting
- Dark and light theme toggle
- Sidebar navigation with workspace/project context selectors
- Settings with provider profiles (add, activate, delete), live model switching, and model auto-complete
- 15 management pages: Chat, Settings, Diagnostics, Artifacts, Tools, Skills, Agents, Memory, Projects, Workspaces, Multi-Agent (Teams + Swarm + Claws), Integrations, Automations, Governance, Setup
- First-run setup wizard
- Session history with search
- Governance configuration (blocked commands, protected files, secret detection)
- Auto-focus input after send, workspace auto-selection on startup

```bash
dotnet run --project src/Sovrant.Desktop
```

---

## Web App

The Blazor Server web app provides a browser-based UI with the full runtime embedded in-process via SignalR.

**Features:**
- Streaming chat with real-time token rendering via SignalR circuit
- Tool use blocks with inline Allow/Deny confirmation
- Markdown rendering via Markdig (server-side HTML)
- Dark/light theme with CSS custom properties
- Sidebar navigation matching the desktop layout
- Cross-component state synchronization (sidebar, settings, projects, chat)
- Workspace and project management with context switching
- Provider profile management with live model switching
- All 15 pages matching the desktop feature set
- Centered layout on wide screens
- Auto-focus input after send

```bash
dotnet run --project src/Sovrant.Web
# Open http://localhost:5100
```

**Dual-mode design:** Components depend on runtime interfaces (`IConversationRuntime`, `ISessionStore`, `IToolRegistry`, etc.). In embedded mode (current), these are real implementations via `AddSovrantRuntime()`. In remote mode (future), they can be HTTP client wrappers calling `Sovrant.Server`'s endpoints — components never change.

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

Sessions are stored in a SQLite database at `~/.sovrant/data/sovrant.db` with full-text search (FTS5) on message content. Override the path with `SOVRANT_DB_PATH`.

```bash
# CLI: resume a session
dotnet run --project src/Sovrant.Cli -- --session my-project prompt "What did we change last time?"

# Server: include session_id in the request body
{ "session_id": "user-123", "messages": [...] }
```

The server keeps one `ConversationRuntime` alive per `session_id` in an in-memory pool — history is available immediately without a disk read on every turn. Set `SOVRANT_SESSION_JSONL=true` to also write sessions to legacy JSONL files during migration.

For full details on the persistence architecture, schema, and security model, see [docs/persistence.md](docs/persistence.md).

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
| `AGENT_MODE` | No | `isolated` (default, process-per-agent) or `shared` (in-process) |
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
| `SOVRANT_DB_PATH` | No | SQLite database path (default: `~/.sovrant/data/sovrant.db`) |
| `SOVRANT_USER_ID` | No | User identity for session ownership and audit (default: OS username) |
| `SOVRANT_SESSION_JSONL` | No | Set to `true` to also write sessions to legacy JSONL files (dual-write) |
| `SOVRANT_AUDIT_JSONL` | No | Set to `true` to also write audit events to legacy JSONL files (dual-write) |
| `OPENROUTER_API_KEY` | No | OpenRouter API key — enables live model metadata discovery (pricing, capabilities, context length) from `/api/v1/models` at startup |
| `SOVRANT_INTENT_ROUTING` | No | `true` (default) or `false` — enables/disables intent-aware model routing |
| `SOVRANT_FREE_MODELS_ONLY` | No | `true` to restrict intent routing to free/zero-cost models only (OpenRouter `:free` variants + local models) |
| `SOVRANT_MCP_TOKEN` | No | Required bearer token for MCP server mode. Unset = no auth. |
| `SOVRANT_MCP_TOOLS` | No | Comma-separated allow-list of tools to expose in MCP server mode. Unset = all tools. |

> **Desktop and Web apps** store provider configuration in `~/.sovrant/settings.json` and `~/.sovrant/providers.json` via the Settings UI — no environment variables needed for basic usage.

---

## File Locations

Sovrant stores all runtime data under `~/.sovrant/`. On each platform this resolves to:

| Directory / File | Windows | Linux / macOS | Purpose |
|---|---|---|---|
| `~/.sovrant/` | `C:\Users\{user}\.sovrant\` | `/home/{user}/.sovrant/` | Root data directory |
| `~/.sovrant/data/sovrant.db` | `C:\Users\{user}\.sovrant\data\sovrant.db` | `/home/{user}/.sovrant/data/sovrant.db` | SQLite database (sessions, audit, memory, credentials, evals, swarm) |
| `~/.sovrant/settings.json` | `C:\Users\{user}\.sovrant\settings.json` | `/home/{user}/.sovrant/settings.json` | User settings (model, provider, API key, permissions) |
| `~/.sovrant/providers.json` | `C:\Users\{user}\.sovrant\providers.json` | `/home/{user}/.sovrant/providers.json` | Saved provider profiles (desktop/web) |
| `~/.sovrant/governance.json` | `C:\Users\{user}\.sovrant\governance.json` | `/home/{user}/.sovrant/governance.json` | Governance config (blocked commands, protected files, secrets) |
| `~/.sovrant/logs/` | `C:\Users\{user}\.sovrant\logs\` | `/home/{user}/.sovrant/logs/` | Rolling application log files |
| `~/.sovrant/credentials/.keystore` | `C:\Users\{user}\.sovrant\credentials\.keystore` | `/home/{user}/.sovrant/credentials/.keystore` | AES-256-GCM master key (auto-generated on first use) |
| `~/.sovrant/sessions/` | `C:\Users\{user}\.sovrant\sessions\` | `/home/{user}/.sovrant/sessions/` | Legacy JSONL session files (only when `SOVRANT_SESSION_JSONL=true`) |
| `~/.sovrant/evals/` | `C:\Users\{user}\.sovrant\evals\` | `/home/{user}/.sovrant/evals/` | Eval suite definitions and results |

> **Auto-creation:** The `data/` directory and SQLite database are created automatically on first run — no installer required. If directory creation fails (e.g. permissions), the error is logged and the application continues with degraded persistence.

> **Override:** Set `SOVRANT_DB_PATH` to place the database anywhere (e.g. `D:\sovrant\data\sovrant.db`).

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
dotnet test Sovrant.slnx   # 1,285 tests across 9 projects
```

| Project | Tests |
|---|---|
| `Sovrant.Api.Tests` | 28 |
| `Sovrant.Runtime.Tests` | 302 |
| `Sovrant.Server.Tests` | 73 |
| `Sovrant.McpServer.Tests` | 30 |
| `Sovrant.Lsp.Tests` | 26 |
| `Sovrant.Tools.Tests` | 174 |
| `Sovrant.Commands.Tests` | 42 |
| `Sovrant.Agents.Tests` | 160 |
| `Sovrant.Integration.Tests` | 1 |

---

## Roadmap

### Planned

- **Knowledge Pages** — User-created markdown knowledge pages stored in the database, injectable into system prompt context. Tag-based organization, search, and management via both desktop and web UI. Future phases will support connecting external knowledge sources through integrations.
- **Claws** — Third multi-agent orchestration mode (alongside Teams and Swarm). Details TBD.
- **Workspace-Scoped Sessions & Artifacts** — All sessions and artifacts belong to a workspace (personal or team) and optionally a project. Enables solo users to organize work and teams to share conversation history, artifacts, and context across members.
- **Web Remote Mode** — Connect the Blazor web app to `Sovrant.Server` via HTTP instead of embedding the runtime in-process, enabling multi-user deployments.
- **External Knowledge Integrations** — Connect knowledge sources (Google Drive, Notion, Confluence, etc.) for automatic context enrichment.

### Recently Completed

- Unified user identity across CLI/Desktop/Web (`SOVRANT_USER_ID` or OS username) with automatic DB migration
- Desktop app (Avalonia) — full GUI with 15 pages, streaming chat, tool use, setup wizard
- Web app (Blazor Server) — browser-based UI matching desktop feature set, port 5100
- Cross-component state synchronization (ActiveContextService)
- Per-model max_tokens capping for OpenAI compatibility
- Provider profile management with live model switching
- Workspace/project CRUD with sidebar context switching
- Governance configuration UI (blocked commands, protected files, secret detection)
- Multi-agent UI with Teams, Swarm, and Claws tabs
- Markdig-based markdown rendering (desktop + web)

---

## Documentation

| Document | Contents |
|---|---|
| [`docs/server.md`](docs/server.md) | Full server API reference — all endpoints, auth, CORS, streaming format |
| [`docs/frontend-integration.md`](docs/frontend-integration.md) | Node.js proxy setup, browser SSE client, Replit integration |
| [`docs/engine-status.md`](docs/engine-status.md) | Tool test results, provider compatibility, known issues |
| [`docs/ci-cd.md`](docs/ci-cd.md) | CI/CD integration — `--ci` flag, GitHub Actions action, GitLab CI template |
| [`docs/webhooks.md`](docs/webhooks.md) | Webhook endpoint, Slack bot setup, Teams/Discord integration guides |
| [`docs/mcp-server.md`](docs/mcp-server.md) | MCP server mode — IDE config, available tools/resources, env vars |
| [`docs/code-review.md`](docs/code-review.md) | Code review findings and coverage report |
