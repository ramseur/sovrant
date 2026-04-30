# Sovrant

**An open-source, provider-agnostic agentic AI engine built on .NET 10.**

Sovrant is a clean-room C# reimplementation inspired by the architecture and feature set of [OpenClaude](https://github.com/Gitlawb/openclaude) (the community fork of Anthropic's Claude Code). **No Anthropic source code was copied, translated, or incorporated.** Every line of Sovrant is original C# / .NET 10 code written from scratch — the project uses OpenClaude only as a functional reference for what an agentic coding tool should be able to do, not as a source of code.

Sovrant is a general-purpose platform for running autonomous AI agents that can hold persistent conversations, use tools, coordinate teams of sub-agents, and integrate with any LLM provider. It is not limited to coding — Sovrant powers chat interfaces, research workflows, business process automation, content creation, project management, and any task that benefits from tool-augmented, session-persistent AI.

The engine runs as a **CLI agent**, an **OpenAI-compatible HTTP server**, a **desktop application** (Windows/macOS/Linux), a **web application** (Blazor Server), an **MCP server** for IDE embedding, or via **webhooks** from Slack, Teams, Discord, and custom systems. Agents read and write files, execute shell commands, search the web, call tools autonomously, delegate to sub-agents, and maintain full conversation history across sessions — all with configurable permission controls.

> **Architecture note:** The CLI, Server, Desktop, and Web are independent frontends. All consume the runtime layer (`Sovrant.Runtime`) directly — the server does **not** depend on the CLI, and the desktop/web apps run the runtime in-process. You can deploy any frontend independently.

**Runtime:** .NET 10 / C# 14
**License:** [see LICENSE]
**Status:** 56 tools. 25 agent templates. 32 built-in skills. 96 server endpoints + SignalR hub. Team orchestration (with run profiles — Phase 78 Path 2). Swarm orchestrator. Mission engine. Inter-agent coordination. Cost tracking. Eval framework. MCP server mode. Desktop app. Web app (embedded + remote mode). Frontend SDK. 1,492 tests passing across 10 projects.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Key Features](#key-features)
- [Architecture](#architecture)
- [Tools](#tools)
- [Agent System](#agent-system)
- [Missions](#missions)
- [Eval Framework](#eval-framework)
- [Providers](#providers)
- [Server API](#server-api)
- [Desktop App](#desktop-app)
- [Web App](#web-app)
- [Frontend SDK](#frontend-sdk)
- [MCP Server Mode](#mcp-server-mode)
- [LSP Integration](#lsp-integration)
- [Persistence](#persistence)
- [Configuration](#configuration)
- [Production Deployment](#production-deployment)
- [Tests](#tests)
- [Documentation](#documentation)

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An API key from any supported LLM provider

### Clone & Build

```bash
git clone https://github.com/ramseur/sovrant-engine.git
cd sovrant-engine
dotnet restore
dotnet build
```

### Desktop App (recommended for first-time users)

The fastest way to get started. Full GUI with provider setup wizard, chat, and management pages.

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

Set your provider credentials:

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

## Key Features

### Provider-Agnostic LLM Routing

Connect to **any OpenAI-compatible API** — OpenAI, OpenRouter, Google Gemini, DeepSeek, Groq, Mistral, Together AI, Ollama (local), LM Studio (local), Azure OpenAI, or any provider that speaks the OpenAI chat completions format. The `SmartRouter` pings all configured providers on startup, scores them by latency, cost, and error rate, and routes each request to the optimal one. Switch providers by changing an environment variable or from the desktop/web settings UI — no code changes.

### Intent-Aware Model Routing

Automatically selects the best model for each turn based on what you're asking. A rule-based `IntentClassifier` categorizes every input into one of 10 intent classes (`SimpleQa`, `CodeGeneration`, `Refactor`, `Planning`, `Debugging`, etc.) with a complexity score (0.0–1.0), then routes to the appropriate model tier (`fast` / `standard` / `high`). The `ModelTierResolver` auto-assigns your discovered models into tiers from OpenRouter pricing data — no manual configuration needed.

**Free models only mode** — set `SOVRANT_FREE_MODELS_ONLY=true` to restrict routing to zero-cost models. On OpenRouter this means only `:free` variants are eligible; local/self-hosted models (Ollama) are always included.

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

### 50 Built-in Tools

Agents autonomously use tools for file operations, shell execution, web access, task management, plan/worktree mode, notebook editing, MCP resource access, LSP code intelligence, code verification, skill execution, agent delegation, team orchestration, swarm orchestration, and mission management. Up to 20 tool rounds per turn with automatic retries.

### 25 Specialized Agent Templates

Define reusable agent roles as `.md` files with YAML frontmatter — each specifying a name, recommended model tier, system prompt, and allowed tools. 25 built-in templates ship with the engine. Drop custom templates into `.sovrant/agents/` to override built-ins or add your own.

### 32 Built-in Skills (Composable Workflow Packages)

Skills are single `.md` files — YAML frontmatter (name, trigger, agents, tools) plus a markdown body with steps and instructions. 32 built-in skills across 7 domains: research (5), writing (5), business (5), project management (4), coding (7), media (3), and agent infrastructure (3). Invoke via `/trigger` slash commands or programmatically. Create new skills at runtime with `SkillCreate`.

### Team Orchestration

Create persistent named agents with specific roles, custom system prompts, and tool restrictions. Teams persist to SQLite across restarts with full workspace/project scoping. Manage teams from within the agentic loop (LLM tool calls), via the HTTP API (`/v1/teams/*`, `/v1/runs/*`), or from the desktop/web UI. Run teams with optional parallelism and file-level locking. Publish swarm workers as reusable team members.

### Swarm Orchestrator

Submit a single complex prompt and the swarm auto-decomposes it into a task DAG, executes tasks in parallel waves via specialized agents, enforces file-level locking and token budgets, and runs an optional quality gate review. Available via CLI (`sovrant swarm "task"`), the `Swarm` tool, `/swarm` slash command, and `POST /v1/swarm` (SSE streaming).

### Mission Engine

Long-lived, goal-driven execution that spans multiple engine runs. A mission pursues an objective autonomously with re-planning, acceptance gates, and a full event journal. Missions are durable (persisted to SQLite), workspace-scoped, and manageable via API (`/v1/missions/*`).

### Session Persistence

Every conversation is stored in a SQLite database with full-text search via FTS5. Resume sessions by name across CLI invocations or HTTP requests. Automatic context compaction when conversations exceed token limits. See [Persistence](#persistence).

### Webhook Integrations

Single endpoint that accepts messages from Slack, Teams, Discord, or custom systems. Build bots and automations that leverage the full agent toolkit. See [`docs/webhooks.md`](docs/webhooks.md).

### CI/CD Pipeline Support

Machine-readable JSON output mode (`--ci` flag) with non-zero exit on error. GitHub Actions action and GitLab CI templates included. See [`docs/ci-cd.md`](docs/ci-cd.md).

### Permission Controls

Five permission modes from fully interactive (`default` — asks before each tool use) to fully autonomous (`bypassPermissions`). Plan mode provides read-only access for safe exploration. Configurable per-session via API.

### Structured Logging & Observability

Rolling file logs, JSON structured output for log aggregators, configurable log levels, per-session token usage tracking, and rate limiting.

---

## Architecture

```
┌───────────────────────────────────────────────────────────────────┐
│  Frontends                                                        │
│  ┌──────────┐ ┌────────────┐ ┌──────────┐ ┌──────────────┐       │
│  │ CLI      │ │  Desktop   │ │   Web    │ │  Server      │       │
│  │ (REPL)   │ │ (Avalonia) │ │ (Blazor) │ │ HTTP :5200   │       │
│  └────┬─────┘ └─────┬──────┘ └────┬─────┘ └──────┬───────┘       │
│       └─────────────┬┴─────────────┘              │               │
│                     │  All consume Sovrant.Runtime in-process     │
└─────────────────────┼─────────────────────────────────────────────┘
                      │
    ┌─────────────────▼──────────────────────────────────────────┐
    │  Sovrant.Runtime                                           │
    │                                                            │
    │  Mission Engine (Phase 51)                                 │
    │  ├── IMissionStore (SQLite)                                │
    │  ├── LlmMissionPlanner → RuntimePlan                       │
    │  └── ParallelMissionExecutor                               │
    │                                                            │
    │  Engine Layer (Phase 51)                                   │
    │  ├── IPlanner → LlmPlanner (plan/re-plan)                  │
    │  ├── IExecutor → LlmExecutor (crash-safe trace)            │
    │  └── IStepRunner → LlmStepRunner (tool dispatch)           │
    │                                                            │
    │  ConversationRuntime                                       │
    │  ├── agentic loop (up to 20 tool rounds)                   │
    │  ├── session history (SQLite + in-memory)                  │
    │  ├── permission gate                                       │
    │  └── MCP client (tool registration)                        │
    │                                                            │
    │  IRuntimeSessionPool  (one runtime per session_id)         │
    │  IStorageProvider  (SQLite, 16 migrations, 36 tables)      │
    └───────────┬──────────────────┬─────────────────────────────┘
                │                  │
    ┌───────────▼────────┐  ┌──────▼──────────────────────────┐
    │  Sovrant.Api       │  │  Sovrant.Tools (56 tools)        │
    │                    │  │                                  │
    │  SmartRouter       │  │  File:  Read Write Edit          │
    │  ├── OpenAI        │  │         Glob Grep LS             │
    │  ├── OpenRouter    │  │  Shell: Bash PowerShell REPL     │
    │  ├── DeepSeek      │  │  Web:   WebFetch WebSearch       │
    │  ├── Groq          │  │  Tasks: TodoWrite Task*          │
    │  ├── Mistral       │  │  Agent  AskUser  Sleep           │
    │  ├── Google        │  │  PlanMode  Worktree              │
    │  ├── Ollama        │  │  Skill  ToolSearch  SkillCreate  │
    │  ├── LM Studio     │  │  Verify  NotebookEdit            │
    │  └── Custom        │  │  MCP: List Read MCPTool McpAuth  │
    │                    │  │  LSP: 5 tools (18 languages)     │
    │  IntentClassifier  │  └──────────┬───────────────────────┘
    │  ModelTierResolver │             │
    └───────────┬────────┘  ┌──────────▼───────────────────────┐
                │           │  Sovrant.Agents                   │
                │           │                                   │
                │           │  Unified Orchestration (Phase 52) │
                │           │  ├── SqliteTeamRegistry (DB)      │
                │           │  ├── AgentOrchestrator             │
                │           │  └── AgentRunStore (run ledger)   │
                │           │                                   │
                │           │  SovrantAgentFactory               │
                │           │  ├── 25 agent templates            │
                │           │  └── FilteredToolRegistry          │
                │           │                                   │
                │           │  IOrchestrationSystem              │
                │           │  ├── Isolated (process-per-agent)  │
                │           │  └── Shared (in-process async)    │
                │           │                                   │
                │           │  Swarm Orchestrator                │
                │           │  ├── LlmSwarmDecomposer (DAG)     │
                │           │  ├── SwarmOrchestrator (waves)     │
                │           │  ├── SwarmFileLockManager          │
                │           │  └── SwarmQualityGate              │
                │           └───────────────────────────────────┘
                │
    ┌───────────▼──────────────────┐
    │  LLM Providers               │
    │  OpenAI · OpenRouter ·       │
    │  DeepSeek · Groq ·           │
    │  Mistral · Google ·          │
    │  Together AI · Ollama ·      │
    │  LM Studio · Azure · Custom  │
    └──────────────────────────────┘
```

### Projects

| Project | Description |
|---|---|
| `Sovrant.Cli` | Interactive REPL and one-shot `prompt` CLI. Entry point for local use. |
| `Sovrant.Server` | ASP.NET Core Minimal API — OpenAI-compatible endpoints plus management APIs. 96 endpoints + SignalR hub. |
| `Sovrant.Desktop` | Avalonia desktop app — full GUI with streaming chat, tool use, settings, and management pages. |
| `Sovrant.Web` | Blazor Server web app — browser-based UI with embedded or remote runtime. Port 5100. Dual-mode: `SOVRANT_RUNTIME_MODE=embedded` (default) or `remote` (connects to Sovrant.Server via SignalR). |
| `Sovrant.Runtime` | Core agentic loop, mission engine, planner/executor, SQLite persistence (16 migrations V001–V016, 36 tables, 61 indexes), permission system, tool executor, MCP client, cost tracking. |
| `Sovrant.Api` | LLM provider abstraction: OpenAI-compat, Ollama, native messages API. SmartRouter with health/latency/cost scoring. Intent-aware model routing. |
| `Sovrant.Tools` | All 50 tool implementations. 32 built-in skill `.md` files. |
| `Sovrant.Commands` | Slash commands for the REPL (`/help`, `/clear`, `/session`, `/memory`, etc.). |
| `Sovrant.Agents` | Orchestration: team registry (SQLite-backed), agent factory, dual backends (isolated + shared), 25 agent templates, swarm orchestrator, unified run ledger, inter-agent coordination (PM agents + mailbox). |
| `Sovrant.McpServer` | MCP server mode: exposes all tools and resources via stdio transport for IDE integration. |
| `Sovrant.Lsp` | Language Server Protocol client: JSON-RPC over stdio, manages language server lifecycle, 5 LSP tools. |
| `sdk/js` | TypeScript/JavaScript client SDK: `SovrantClient` covering the 96-endpoint server (incl. `updateTeamProfile` for Phase 78 Path 2), SSE streaming, React `useChat()` hook, 75+ TypeScript interfaces. |

### Key Design Decisions

**Always streaming internally.** `ConversationRuntime` always sets `Stream: true` on every request. The server decides independently whether to forward chunks as SSE or buffer into a single JSON response. One code path in the agentic loop regardless of the client's preference.

**One runtime per session.** The server keeps one `ConversationRuntime` alive per `session_id` in a `ConcurrentDictionary`. Each runtime holds its own message history in memory, loaded from SQLite on first access.

**SmartRouter with health fallback.** All LLM calls go through `ISmartRouter`. On startup it pings every configured provider and scores them by latency, cost, and error rate. If all providers fail the startup ping, the router falls back to the configured list rather than refusing to start.

**Tool execution is permission-gated.** Every tool call goes through `IPermissionPolicy` before execution. The CLI uses `ModeAwarePermissionPolicy` (interactive prompts based on `PermissionMode`). The server defaults to `DontAsk` and can be changed live via `PUT /v1/config`.

**Per-model token capping.** The runtime automatically caps `max_tokens` to model-specific limits (e.g., gpt-4o: 16,384, gpt-4.1: 32,768) to prevent 400 errors from providers that enforce strict limits.

---

## Tools

56 tools available. All run inside the agentic loop with automatic retries up to 20 tool rounds per turn.

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
`TeamCreate` · `TeamDelete` · `TeamStatus` · `TeamDelegate` · `TeamRun` *(run a team with optional parallelism)* · `TeamPublish` *(publish swarm workers as reusable team members)*

*Persistent named agents with roles, system prompts, and tool restrictions. SQLite-backed, workspace-scoped. See [Agent System](#agent-system).*

### Missions
`Mission` *(create and drive long-lived goals with re-planning and acceptance gates)*

### Swarm Orchestration
`Swarm` *(auto-decompose + parallel DAG execution with optional team)* · `SwarmStatus` *(live progress tracking)*

*Submit complex tasks for automatic decomposition into parallel waves. See [Agent System](#agent-system).*

### Discovery & Skills
`ToolSearch` *(keyword search over registered tools)* · `Skill` *(loads and executes a skill by name or /trigger)* · `SkillCreate` *(creates new `.md` skill files at runtime)*

### Quality
`Verify` *(6-phase quality gate: build, type-check, lint, test, security scan, diff review)*

### MCP
`ListMcpResources` · `ReadMcpResource` · `MCPTool` *(dynamic proxy — calls any tool on any connected MCP server)* · `McpAuth` *(OAuth 2.0 + PKCE flow for MCP servers that require authorization)*

### LSP (Language Server Protocol)
`LspHover` · `LspDefinition` · `LspReferences` · `LspDiagnostics` · `LspRename`

*Requires a language server configured in `~/.sovrant/settings.json`. See [LSP Integration](#lsp-integration).*

### Notebook
`NotebookEdit` *(read/write Jupyter `.ipynb` cells)*

---

## Agent System

Sovrant provides a layered orchestration capability: ad-hoc sub-agents for quick delegation, reusable agent templates for purpose-built roles, persistent teams for structured orchestration, and swarm for parallel task execution.

### Ad-hoc Sub-Agents (`Agent` tool)

The `Agent` tool spawns a lightweight, stateless sub-agent for a single task. The LLM decides when to use it — typically to parallelize independent research, explore multiple solution paths, or isolate risky operations.

- Each sub-agent gets its own `ConversationRuntime` with a fresh session
- No persistent identity — created, runs, and discarded
- Recursion depth limited to 5
- Same tool access as the parent (unless a template restricts it)

### 25 Agent Templates

Agent templates are `.md` files with YAML frontmatter — each defines a reusable agent persona with a name, recommended model tier (High/Standard/Fast), system prompt, and optional tool restrictions. Templates live in `src/Sovrant.Agents/agents/` (built-in) and can be overridden by dropping `.md` files into `.sovrant/agents/`.

| Category | Templates |
|---|---|
| **General-purpose (10)** | researcher, writer, analyst, planner, summarizer, translator, tutor, debater, advisor, fact-checker |
| **Code-specific (8)** | coder, reviewer, debugger, refactorer, test-writer, architect, doc-writer, security-auditor |
| **Creative / Domain (6)** | storyteller, copywriter, data-scientist, sysadmin, product-manager, interviewer |
| **Coordination (1)** | pm-coordinator |

```
# Spawn a purpose-built agent from a template
Agent(template: "security-auditor", prompt: "Audit the auth module for OWASP Top 10")

# Create a team member backed by a template
TeamCreate(name: "reviewer", template: "reviewer")
```

Each template specifies a `recommended_level` that maps to a model string via `ModelLevels` config — so a "Fast" agent can use a cheaper model while a "High" agent gets the most capable one, without hardcoding model names.

### Persistent Teams

Teams provide structured orchestration with persistent, named agents. Teams are stored in SQLite and survive process restarts with full workspace and project scoping.

**Create and use teams from the agentic loop:**

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

# Run a team with parallelism and file-locking
TeamRun(team_id: "team-abc", prompt: "Implement the feature across all modules")

# Publish ephemeral swarm workers as reusable team members
TeamPublish(team_id: "team-abc")
```

**Or manage teams via the HTTP API:**

```
POST   /v1/teams              — create a team
GET    /v1/teams              — list teams
GET    /v1/teams/{id}         — get team + members
DELETE /v1/teams/{id}         — delete a team
POST   /v1/teams/{id}/members — add a member
POST   /v1/teams/{id}/runs    — start a team run
GET    /v1/runs               — list all runs
GET    /v1/runs/{id}          — get run details
```

Each team member has a **role** (General, Planner, Coder, Reviewer, Executor, Supervisor) with a role-specific system prompt, can be backed by an **agent template**, restricted to a **subset of tools**, and tracks **lifecycle state** (Idle → Running → Completed/Failed). All runs are recorded in the unified `agent_runs` ledger with token counts and status.

### Swarm Orchestrator

The swarm auto-decomposes complex tasks into parallel DAGs and executes them across multiple agents.

```
User prompt → [1. Decompose] → SwarmPlan (task DAG with waves)
                                    ↓
              [2. Execute]   → wave-by-wave parallel execution
                                    ↓
              [3. Quality Gate] → score + verdict → SwarmResult
```

**Phase 1 — Decomposition:** A high-level LLM agent analyzes the prompt and produces a JSON task array with dependencies, file predictions, and agent template assignments. Kahn's topological sort assigns parallel wave indices.

**Phase 2 — Execution:** The orchestrator processes waves sequentially, tasks within a wave in parallel (bounded by `SemaphoreSlim`). File-level pessimistic locking prevents conflicts. Token budget enforcement with auto-cancellation on breach. Per-task retry with configurable timeout.

**Phase 3 — Quality Gate (optional):** A reviewer agent scores the combined output on a 1–10 scale with pass/needs_revision/fail verdict.

**Configuration** (`.sovrant/swarm.json`, OFF by default):

```json
{
  "enabled": true,
  "max_concurrent": 4,
  "max_token_budget": 500000,
  "max_retries": 1,
  "quality_gate": true,
  "decomposer_level": "High",
  "worker_level": "Standard",
  "task_timeout_seconds": 300
}
```

### Team vs Swarm — When to Use Which

| Dimension | **Team** | **Swarm** |
|---|---|---|
| **Trigger** | LLM tool calls or HTTP API inside a conversation | User runs `sovrant swarm "<goal>"` from CLI, or `POST /v1/swarm` |
| **Lifecycle** | Persistent across turns and restarts (SQLite-backed) | Ephemeral — one swarm, one task, torn down at the end |
| **Decomposition** | Caller decides what to delegate and when | `LlmSwarmDecomposer` auto-produces a task DAG |
| **Concurrency** | Sequential by default; `TeamRun` adds optional parallelism | Wave-based parallelism with topological sort + concurrency cap |
| **Coordination** | Independent delegations (or file-locking via `TeamRun`) | File locks, retries, quality gate, token budget |

**Use Team when** the model needs persistent specialists it can call repeatedly.
**Use Swarm when** you have one large task that benefits from parallel sub-tasks with file-touch coordination.
**They are not exclusive.** A swarm can draw workers from a Team registry, and `TeamPublish` converts swarm workers into reusable team members.

### Two Backend Modes

| | Isolated (default) | Shared |
|---|---|---|
| **Env var** | `AGENT_MODE=isolated` (or unset) | `AGENT_MODE=shared` |
| **How it works** | Process-per-agent — spawns a separate OS process | In-process async — each agent runs in the same process |
| **Best for** | Security-sensitive workloads, untrusted code | Lower overhead when agents share trusted memory space |

---

## Missions

Missions are long-lived, goal-driven executions that span multiple engine runs. Unlike a single conversation turn, a mission pursues an objective autonomously — planning steps, executing them, re-planning when things change, and optionally pausing for human approval.

**Lifecycle:** `Planning → Running → Awaiting Human → Completed / Failed / Cancelled`

**API:**

```
POST   /v1/missions           — create a mission with a goal
GET    /v1/missions            — list missions (filter by status, owner)
GET    /v1/missions/{id}       — get mission state + current plan
POST   /v1/missions/{id}/run   — drive the mission forward one cycle
GET    /v1/missions/{id}/events — full event journal (reconstructable history)
GET    /v1/missions/{id}/export — export as JSON or Markdown
```

Missions are durable (persisted to SQLite), workspace-scoped, and include a full append-only event journal so history is always reconstructable. The engine layer underneath provides crash-safe execution via `runtime_traces` — every state transition is committed before the corresponding side effect runs, so a crash mid-step leaves a recoverable trail.

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

The server exposes an OpenAI-compatible chat completions endpoint plus comprehensive management APIs. 96 endpoints + SignalR hub across 22 route groups:

| Group | Endpoints | Description |
|---|---|---|
| **Chat** | `POST /v1/chat/completions` | OpenAI-compatible chat with streaming (SSE) support |
| **Sessions** | 7 endpoints | CRUD, config, export, message history |
| **Workspaces** | 17 endpoints | Workspace CRUD, members, invites, config, memory, usage |
| **Projects** | 15 endpoints | Project CRUD within workspaces, members, config, archive |
| **Users** | 9 endpoints | User management, profiles, usage, audit |
| **Teams** | 10 endpoints | Team CRUD, members, runs, run profile (Phase 78 Path 2 — `PUT /v1/teams/{id}/profile`) |
| **Missions** | 6 endpoints | Mission CRUD, run, events, export |
| **Swarm** | 4 endpoints | Start swarm, status, events, session history |
| **Engine** | 4 endpoints | Runtime trace, in-flight runs, recovery |
| **Evals** | 3 endpoints | Run evals, list suites, history |
| **Artifacts** | 3 endpoints | List, download, delete scoped artifacts |
| **Tools / Skills / Agents** | 6 endpoints | Registry queries for tools, skills, and agent templates |
| **Cost** | 1 endpoint | `GET /v1/cost` — cost tracking with daily/weekly/monthly rollups |
| **Config / Status / Models** | 5 endpoints | Runtime config, engine status, model discovery |
| **Usage / Webhook / Health** | 4 endpoints | Token usage, webhook ingress, health check |
| **MCP Auth** | 1 endpoint | OAuth callback for MCP server authorization |
| **SignalR Hub** | `/hubs/chat` | Real-time streaming for web frontend (StreamTurn, ConfirmTool, DenyTool, CancelTurn) |

All endpoints require `Authorization: Bearer <SOVRANT_TOKEN>` (except `/health` and MCP auth callback). See [`docs/server.md`](docs/server.md) for the full API reference.

---

## Desktop App

The Avalonia-based desktop app provides a full GUI for interacting with the Sovrant runtime. Runs on Windows, macOS, and Linux.

- Streaming chat with thinking indicators, tool use blocks, and inline approve/deny
- Markdown rendering with code blocks, lists, headings, and inline formatting
- Dark and light theme toggle
- Sidebar navigation with workspace/project context selectors
- Settings with provider profiles (add, activate, delete), live model switching
- 15 management pages: Chat, Settings, Diagnostics, Artifacts, Tools, Skills, Agents, Memory, Projects, Workspaces, Orchestration, Integrations, Automations, Governance, Setup
- First-run setup wizard for provider configuration
- Session history with search

```bash
dotnet run --project src/Sovrant.Desktop
```

---

## Web App

The Blazor Server web app provides a browser-based UI with the full runtime embedded in-process via SignalR.

- Streaming chat with real-time token rendering
- Tool use blocks with inline Allow/Deny confirmation
- Dark/light theme with CSS custom properties
- All 15 pages matching the desktop feature set
- Workspace and project management with context switching
- Provider profile management with live model switching

```bash
dotnet run --project src/Sovrant.Web
# Open http://localhost:5100
```

**Dual-mode design:** Components depend on runtime interfaces (`IConversationRuntime`, `ISessionStore`, `IToolRegistry`, etc.). In embedded mode (default), these are real implementations via `AddSovrantRuntime()`. In remote mode (`SOVRANT_RUNTIME_MODE=remote`), they are replaced by HTTP/SignalR client wrappers that call `Sovrant.Server` via `AddSovrantClient()` — components never change. Set `SOVRANT_SERVER_URL` and `SOVRANT_API_TOKEN` to connect to a remote server.

---

## Frontend SDK

The TypeScript/JavaScript SDK (`sdk/js`) provides a typed client for building custom frontends against the Sovrant server.

- **`SovrantClient`** — covers the 96-endpoint server: chat, sessions, users, workspaces, projects, teams (incl. `updateTeamProfile`), missions, swarm, engine, evals, artifacts, and registries
- **SSE streaming** — real-time token-by-token responses with `streamChat()`
- **React `useChat()` hook** — drop-in conversational UI component
- **75+ TypeScript interfaces** — full type coverage for all request/response shapes
- **Security** — AbortController timeouts, error body truncation, runtime input validation

```bash
npm install @sovrant/sdk
```

```typescript
import { SovrantClient } from "@sovrant/sdk";

const client = new SovrantClient({
  baseUrl: "http://localhost:5200",
  token: "your-secret-token",
});

// Non-streaming
const response = await client.chat("gpt-4o-mini", [
  { role: "user", content: "Hello" },
]);

// Streaming
const stream = client.streamChat("gpt-4o-mini", [
  { role: "user", content: "Explain recursion" },
]);
for await (const chunk of stream) {
  process.stdout.write(chunk.choices?.[0]?.delta?.content ?? "");
}
```

See [`docs/frontend-integration.md`](docs/frontend-integration.md) for proxy setup, browser SSE, multi-tenant LLM key support, and the full API reference.

---

## MCP Server Mode

Sovrant can run as an MCP (Model Context Protocol) server, exposing all 56 tools and resources via stdio transport. This lets MCP-aware IDEs use Sovrant as a tool backend — no extension required.

```bash
dotnet run --project src/Sovrant.Cli -- mcp-server
```

**Supported IDEs:** VS Code (GitHub Copilot), Cursor, Windsurf, Claude Code.

Add to your IDE's MCP config (example for VS Code):

```json
{
  "github.copilot.chat.mcpServers": {
    "sovrant": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Sovrant.Cli", "--", "mcp-server"],
      "env": { "LLM_API_KEY": "sk-..." }
    }
  }
}
```

The synthetic `chat` tool runs a full agentic turn — the IDE sends a message and Sovrant runs it through the LLM with the full tool loop. Token-based authentication via `SOVRANT_MCP_TOKEN` and `--token`. Tool filtering via `SOVRANT_MCP_TOOLS`.

See [`docs/mcp-server.md`](docs/mcp-server.md) for full IDE configuration, OAuth support, and environment variables.

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

## Persistence

All durable state is stored in a single SQLite database at `~/.sovrant/data/sovrant.db`. The database is created automatically on first run — no installer or manual setup required.

**16 migrations (V001–V016), 36 application tables, 61 indexes.** Covers sessions (with FTS5 full-text search and titles), agent memory, audit logs, credentials (AES-256-GCM encrypted), token usage, workspaces, projects, users, swarm events, runtime traces, missions, teams (with run profiles — Phase 78 Path 2), agent runs, and inter-agent coordination.

### Session Persistence

```bash
# CLI: resume a session
dotnet run --project src/Sovrant.Cli -- --session my-project prompt "What did we change last time?"

# Server: include session_id in the request body
{ "session_id": "user-123", "messages": [...] }
```

The server keeps one `ConversationRuntime` alive per `session_id` in an in-memory pool — history is available immediately without a disk read on every turn.

### Agent Memory

Sovrant has two parallel memory systems: **file-based memory** (injected into the system prompt today) and **database-backed memory** (workspace + project, exposed over the API).

**File memory** — read at the start of every session and prepended to the system prompt:

| File | Scope |
|---|---|
| `~/.sovrant/memory.md` | Global — your preferences, personal notes |
| `.sovrant/memory.md` | Project — architecture notes, conventions |

Use `/memory` (or `/mem`) in the REPL to view or create these files.

**Database memory** — persisted in SQLite and managed via the API (Web/Desktop UI surfaces both):

| Scope | Storage | API |
|---|---|---|
| Workspace | `workspace_memory` table (layered entries with confidence scores) | `GET/POST/DELETE /v1/workspaces/{id}/memory` |
| Project | Same table, scoped by `project_id`; reads merge project-scoped + workspace-level entries | `GET /v1/projects/{id}/memory` (writes go through the workspace endpoint with `project_id`) |

> **Note:** Database-backed memory is not yet injected into the system prompt automatically — see Phase 81 in the roadmap. Today it is read/written via the API and UI; it will be merged into the prompt builder in a future phase so workspace and project memory reach the LLM the same way file memory does.

### Database Management

```bash
sovrant db status           # schema version, table row counts
sovrant db version          # current schema version
sovrant db migrate          # apply pending migrations
sovrant db migrate --dry-run # preview pending migrations
sovrant db backup [path]    # checkpoint WAL and copy DB
sovrant db inspect <table>  # PRAGMA table_info + first N rows
sovrant db import-swarm     # import legacy JSONL swarm sessions
```

Set `SOVRANT_DB_REQUIRE=true` in production so database init failures halt the process instead of silently running without persistence. Set `SOVRANT_DB_BACKUP_ON_UPGRADE=true` to snapshot the DB before applying migrations.

See [`docs/persistence.md`](docs/persistence.md) for the full schema reference, migration details, and security model.

### Custom Slash Commands

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
| `SOVRANT_TOKEN` | Yes (server) | Bearer token for HTTP API authentication |
| `SOVRANT_PORT` | No | Server port (default: `5200`) |
| `SOVRANT_MODEL` | No | Default model name |
| `PROVIDER_BASE_URL` | No | Enables native messages API provider (`/v1/messages` format) |
| `PROVIDER_API_KEY` | No | API key for the native messages API provider |
| `OLLAMA_BASE_URL` | No | Enables local Ollama provider |
| `ROUTER_MODE` | No | `Smart` (default) or `Fixed` |
| `ROUTER_STRATEGY` | No | `Balanced` (default), `Latency`, or `Cost` |
| `AGENT_MODE` | No | `isolated` (default, process-per-agent) or `shared` (in-process) |
| `SOVRANT_COMPACT_THRESHOLD` | No | Token count that triggers context auto-compaction (default: `80000`). `0` to disable. |
| `SOVRANT_FREE_MODELS_ONLY` | No | `true` to restrict routing to free/zero-cost models only |
| `SOVRANT_INTENT_ROUTING` | No | `true` (default) or `false` — enables/disables intent-aware model routing |
| `LLM_WEB_SEARCH` | No | `true` to use OpenAI's Responses API with `web_search_preview` |
| `BRAVE_API_KEY` | No | Enables WebSearch via Brave Search API |
| `FIRECRAWL_API_KEY` | No | Enables WebSearch via FireCrawl |
| `SOVRANT_DB_PATH` | No | SQLite database path (default: `~/.sovrant/data/sovrant.db`) |
| `SOVRANT_DB_REQUIRE` | No | `true` to fail fast on DB init errors (recommended for production) |
| `SOVRANT_DB_BACKUP_ON_UPGRADE` | No | `true` to snapshot DB before applying migrations |
| `SOVRANT_USER_ID` | No | User identity for session ownership (default: OS username) |
| `SOVRANT_SESSION_TTL_SECONDS` | No | Idle session TTL before eviction (default: `3600`) |
| `SOVRANT_MAX_SESSIONS` | No | Maximum active sessions in server pool (default: `500`) |
| `SOVRANT_RATE_LIMIT_RPM` | No | Per-session rate limit: requests per minute (default: `60`) |
| `SOVRANT_LOG_LEVEL` | No | `Verbose`, `Debug`, `Information` (default), `Warning`, `Error`, `Fatal` |
| `SOVRANT_LOG_FILE` | No | Rolling file path (default: `~/.sovrant/logs/sovrant-{Date}.log`). Empty = disabled. |
| `SOVRANT_LOG_FORMAT` | No | `text` (default) or `json` (structured) |
| `SOVRANT_MCP_TOKEN` | No | Required bearer token for MCP server mode |
| `SOVRANT_MCP_TOOLS` | No | Comma-separated allow-list of tools for MCP server mode |
| `OPENROUTER_API_KEY` | No | Enables live model metadata discovery from OpenRouter |

> **Desktop and Web apps** store provider configuration in `~/.sovrant/settings.json` via the Settings UI — no environment variables needed for basic usage.

### File Locations

| Path | Purpose |
|---|---|
| `~/.sovrant/data/sovrant.db` | SQLite database (sessions, memory, audit, credentials, teams, missions, etc.) |
| `~/.sovrant/settings.json` | User settings (model, provider, API key, permissions) |
| `~/.sovrant/providers.json` | Saved provider profiles (desktop/web) |
| `~/.sovrant/governance.json` | Governance config (blocked commands, protected files, secrets) |
| `~/.sovrant/logs/` | Rolling application log files |
| `~/.sovrant/credentials/.keystore` | AES-256-GCM master key (auto-generated) |
| `~/.sovrant/memory.md` | Global memory (human-edited, injected into system prompt) |
| `.sovrant/memory.md` | Project memory (human-edited, injected into system prompt) |
| `.sovrant/settings.json` | Project-level settings overrides |
| `.sovrant/agents/*.md` | Custom agent templates |
| `.sovrant/skills/*.md` | Custom skills |
| `.sovrant/commands/*.md` | Custom slash commands |
| `.sovrant/evals/*.json` | Eval suite definitions |
| `.sovrant/swarm.json` | Swarm orchestrator configuration |
| `.sovrant/routing.json` | Model routing overrides |
| `.sovrant/governance.json` | Project-level governance overrides |

> **Auto-creation:** `~/.sovrant/data/` and the SQLite database are created automatically on first run. Override with `SOVRANT_DB_PATH`.

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

The server is long-running — JIT warmup is paid once. ReadyToRun gives fast startup without AOT compatibility issues.

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

Replace `-r linux-x64` with `-r win-x64` for Windows deployments.

---

## Tests

```bash
dotnet test Sovrant.slnx   # 1,492 tests across 10 projects
```

| Project | Tests |
|---|---|
| `Sovrant.Runtime.Tests` | 668 |
| `Sovrant.Agents.Tests` | 197 |
| `Sovrant.Tools.Tests` | 190 |
| `Sovrant.Server.Tests` | 133 |
| `Sovrant.Api.Tests` | 104 |
| `Sovrant.Runtime.Documents.Tests` | 84 |
| `Sovrant.Commands.Tests` | 56 |
| `Sovrant.McpServer.Tests` | 34 |
| `Sovrant.Lsp.Tests` | 25 |
| `Sovrant.Integration.Tests` | 1 |

All tests use isolated in-memory SQLite databases. No external services or API keys required.

---

## Documentation

| Document | Contents |
|---|---|
| [`docs/server.md`](docs/server.md) | Full server API reference — all 96 endpoints + SignalR hub, auth, CORS, streaming format, cost tracking, remote mode |
| [`docs/frontend-integration.md`](docs/frontend-integration.md) | SDK reference, proxy setup, browser SSE, multi-tenant LLM keys, React hook, remote mode (dual-mode web frontend) |
| [`docs/persistence.md`](docs/persistence.md) | SQLite schema reference — 16 migrations (V001–V016), 36 tables, 61 indexes, domain stores, security model |
| [`docs/agent-systems.md`](docs/agent-systems.md) | Team vs Swarm deep dive — architecture, value analysis, unified orchestration, inter-agent coordination |
| [`docs/mcp-server.md`](docs/mcp-server.md) | MCP server mode — IDE config, available tools/resources, OAuth, env vars |
| [`docs/webhooks.md`](docs/webhooks.md) | Webhook endpoint, Slack bot setup, Teams/Discord integration guides |
| [`docs/ci-cd.md`](docs/ci-cd.md) | CI/CD integration — `--ci` flag, GitHub Actions, GitLab CI template |
| [`docs/engine-status.md`](docs/engine-status.md) | Tool test results, provider compatibility, known issues |
| [`docs/code-review.md`](docs/code-review.md) | Code review findings and coverage report |
