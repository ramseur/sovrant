# Sovrant — Roadmap

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-04-05 (Phase 32 SQLite Persistence Layer complete)

This document tracks planned features, architectural decisions, and the reasoning behind them.

---

## Current State

The engine is fully functional for individual and small-team use:

- **45 tools** across 8 categories (file, shell, web, task, agent, team, MCP, LSP)
- **910 tests** across 9 projects, 0 warnings
- **28 server endpoints** (OpenAI-compatible chat, sessions, config, usage, webhooks, export, registry discovery, swarm, evals)
- CLI REPL, one-shot `prompt`, CI mode (`--ci`), MCP server mode (`mcp-server`)
- Agentic loop with up to 20 tool rounds per turn
- SQLite persistence layer with 5 versioned migrations, 26+ tables (Phase 32 ✅)
- Session, memory, audit, credential, and token usage stores backed by SQLite
- SmartRouter with health/latency/cost scoring across multiple providers
- Multi-provider support: OpenAI, Gemini, Ollama, native messages API, OpenAI Responses API (`LLM_WEB_SEARCH=true`)
- Per-session config overlay, rate limiting, token usage tracking (Phase 10 ✅)
- Session TTL eviction + LRU cap + per-session turn serialization (Phase 9 ✅)
- Multi-tenant per-request credentials (`X-LLM-Api-Key` / `X-LLM-Base-Url` headers) (Phase 8 ✅)
- Structured async logging with 4 env vars, source-generated delegates (Phase 7 ✅)
- Agent memory files (`~/.sovrant/memory.md` + `.sovrant/memory.md`) injected into system prompt
- Context auto-compaction at configurable token threshold
- Security hardening: BashTool 256 KB cap + env stripping, WebFetch SSRF protection, provider retry 3×, AgentTool depth ≤ 5, ReadFile 10 MB cap, GlobTool 1000 cap, atomic writes
- Webhook integration (Slack, Teams, Discord, custom)
- Frontend SDK (TypeScript/React), structured diff view, session export
- MCP server mode (stdio JSON-RPC 2.0) + dynamic MCP tool proxy (`MCPTool`) + MCP OAuth (`McpAuthTool`)
- LSP integration (5 tools, 18 language extensions)
- CI/CD integration (`--ci` flag, GitHub Actions composite action, GitLab CI template)
- Registry discovery API — tools, skills, agent templates (Phase 30 ✅)
- **Multi-agent team orchestration** (Phase 19+20 ✅) — see below

### Agent System: Current State

| Layer | Status | Notes |
|---|---|---|
| Ad-hoc sub-agent (`AgentTool`) | ✅ Working | Spawns a fresh `ConversationRuntime`, runs one isolated turn, returns text. Recursion depth ≤ 5. LLM-driven parallelization. |
| Multi-agent interfaces (`IAgent`, `IMultiAgentSystem`) | ✅ Complete | `Sovrant.Agents` project. Both backends implement the same interface. |
| Shared backend (`MultiAgentCoordinator`) | ✅ Complete | Semaphore-based concurrency control (`MaxConcurrentAgents`), linked CTS with timeout, agent resolution by name or first-registered, proper shutdown drain. |
| Isolated backend (`ProcessBasedMultiAgentSystem`) | ✅ Complete | Process spawn, stdin/stdout JSON, process tree kill on cancel, timeout handling. |
| `SovrantAgent` + `SovrantAgentFactory` | ✅ Complete | Runtime-backed agents with role-specific system prompts (`AgentPrompts`) and optional tool filtering (`FilteredToolRegistry`). |
| Config switch (`AGENT_MODE`) | ✅ Working | `isolated` (default, process-per-agent) or `shared` (in-process). |
| DI wiring in CLI / Server | ✅ Complete | `services.AddMultiAgentSystem()` called in both hosts. `ITeamRegistry`, `SovrantAgentFactory`, team tools all registered. |
| Team tools | ✅ Complete | `TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`. Named agents with roles, custom prompts, tool restrictions, lifecycle tracking. |

### Completed phases (1–30)

| Phase | Summary |
|---|---|
| 1 | Tool parity — 9 new tools (EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree, TaskUpdate, Skill, ToolSearch, ListMcpResources, ReadMcpResource) |
| 2 | Agent memory files, token count fix, context auto-compaction |
| 3 | OpenAI Responses API (`LLM_WEB_SEARCH=true`) |
| 4–6 | Security hardening, reliability, robustness |
| 7 | Structured async logging |
| 8 | Multi-tenant per-request credentials |
| 9 | Session lifecycle (TTL eviction, LRU cap, turn serialization) |
| 10 | Small team hardening (session-scoped config, rate limiting, usage tracking) |
| 11 | LSP integration (5 tools, 18 language extensions) |
| 12 | CI/CD pipeline integration |
| 13 | Slack / webhook integration |
| 14 | Frontend SDK, diff view, session export |
| 15 | MCP server mode |
| 16 | Dynamic MCP tool proxy (`MCPTool`) |
| 17 | MCP OAuth authentication (`McpAuthTool`) |
| 18 | Dual agent architecture scaffolding |
| 19+20 | Multi-agent backend + team tools (58 tests) |
| 21 | Hook lifecycle system |
| 22 | Specialized agent definitions (role templates + model routing) |
| 23 | Template externalisation (built-ins as markdown files) |
| 24 | Verification loop & quality gates |
| 25 | Governance, security monitoring & audit |
| 26 | Skills system (composable workflow packages) |
| 27 | Multi-layered memory system |
| 28 | Eval-driven development framework (3 graders, pass@k metrics, 62 tests) |
| 29 | Swarm orchestrator (auto-decomposition, DAG execution, file locking, quality gate, 62 tests) |
| 30 | Registry discovery API (tools, skills, agent templates — 11 tests) |
| 31 | Server response caching & cache infrastructure (in-memory, ETag, TTL — 32 tests) |
| 32 | SQLite persistence layer — 5 migrations, 26+ tables, 7 SQLite stores, dual-write decorators, graceful error handling (31 tests) |

### Still pending

| Gap | Phase | Priority |
|---|---|---|
| CLI quick wins (banner, env errors, thinking messages, escape cancel, sticky input, paste, /help audit) | Phase 33 | Low–Medium |
| CLI visual polish (coloring, conversation separation, /help formatting, web search fix, Windows perms) | Phase 34 | Low–Medium |
| Workspaces (personal + team areas, isolated memory/config/sessions) | Phase 35 | Medium–High |
| Projects (workspace-scoped containers for isolated work) | Phase 36 | Medium |
| User management API (CRUD users, per-user data views) | Phase 37 | Low–Medium |
| Per-user token auth & database hardening | Phase 38 | Medium |
| Cost tracking, token budgets & model pricing registry | Phase 39 (deferred) | Deferred |
| Enterprise auth & multi-tenancy (RBAC, OAuth/OIDC, SSO) | Phase 40 (deferred) | Deferred |
| Artifact system (`ITeamWorkspace`, `IArtifact`) | Phase 41 (deferred) | Deferred |
| VS Code native extension | Phase 42 (deferred) | Deferred |
| SearXNG web search backend (self-hosted, key-free) | Phase 49 | Low–Medium |
| OpenClaw integration & federated swarms over a routed bus (manager-led + siloed modes) | Phase 50 | Medium–High |

---

## Roadmap

### Phase 1 — Tool Parity with OpenClaude ✅

**Goal:** Close the gap between Sovrant's 22 tools and the full OpenClaude tool set. A comparison of the OpenClaude source against Sovrant's `Sovrant.Tools` project identified 9 missing tools worth porting and 13 cloud/platform-only stubs that are not portable.

#### Tool comparison summary

| Category | Count | Tools |
|---|---|---|
| Implemented ✅ | 31 | Read, Write, Edit, Glob, Grep, LS, Bash, PowerShell, REPL, WebFetch, WebSearch, TaskCreate/Get/List/Output/Stop/Update, TodoWrite, Agent, AskUserQuestion, Sleep, NotebookEdit, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree, Skill, ToolSearch, ListMcpResources, ReadMcpResource |
| Missing — port ⬜ | 3 | ScheduleCron, ConfigTool, LSP |
| Cloud — future phases ☁️ | 3 | MCPTool (Phase 13), McpAuthTool (Phase 14), TeamCreate/Delete (Phase 15) |
| Not portable ❌ | 10 | RemoteTrigger, SendMessage, WorkflowTool, BriefTool, SuggestBackgroundPR, VerifyPlanExecution, SyntheticOutput, Tungsten |

#### Tier 1 — High priority (complete the core developer experience)

> **Status: ✅ Implemented** — TaskUpdate, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree all implemented and building clean (99/99 tests passing).

**`TaskUpdate`**
The task management suite has 5 of 6 tools. `TaskUpdate` lets the model update the status or description of a running background task while it is in flight. Trivial to add alongside the existing `BackgroundTaskRegistry`.

**`EnterPlanMode` / `ExitPlanMode`**
Sovrant already has a `plan` permission mode, but it is set by the user at startup (`--permission-mode plan`). These tools let the *model* signal a plan-mode transition mid-conversation — entering a read-only planning phase and exiting back to execution when ready. The runtime needs to honour the signal by toggling `_config.PermissionMode` (or a session-scoped override) when it sees these tool calls returned.

**`EnterWorktree` / `ExitWorktree`**
Creates a temporary git worktree so the agent performs all file edits on an isolated branch, then optionally commits and returns the branch name. Essential for safe autonomous multi-file coding work — the user's working tree is never touched until explicitly merged. Requires `git worktree add` / `git worktree remove` via `Bash` internally; the tool wraps this with session-scoped state tracking.

#### Tier 2 — Medium priority (MCP resource access + tool discovery + safety)

> **Status: ✅ Implemented** — `Skill`, `ToolSearch`, `ListMcpResources`, `ReadMcpResource`, custom project slash commands, `/memory` command all implemented and building clean.

**`/undo` / `/redo` (git-backed)**
Before every `Write` or `Edit` tool call, stash the current file state to a temporary git commit or diff buffer. `/undo` reverts the last agent file change; `/redo` reapplies it. Builds user trust significantly — the user can always roll back an agent mistake without losing their own work. Related to `EnterWorktree`/`ExitWorktree` (Tier 1) but applies even outside a worktree. Implementation: wrap `Write`/`Edit` tool execution in `ConversationRuntime` with pre/post git snapshot calls.

**Custom project slash commands (`.sovrant/commands/`)** ✅ Done
Project-specific slash commands defined as markdown files in `.sovrant/commands/{name}.md`. When invoked, the file's content is injected as a user message. `SlashCommandDispatcher.TryDispatchAsync` checks this directory when a built-in command is not found.

**`ListMcpResources` / `ReadMcpResource`** ✅ Done
`ListMcpResources` enumerates what each connected MCP server exposes; `ReadMcpResource` fetches a resource by URI. Backed by `McpClientRegistry` (populated at `InitializeRuntimeAsync` time).

**`ToolSearch`** ✅ Done
Searches registered tool names/descriptions by keyword. Useful once tool count exceeds ~30 or MCP servers register many tools.

**`SkillTool`** ✅ Done
Reads `.sovrant/skills/{name}.md` (project-local first, then `~/.sovrant/skills/{name}.md`). Substitutes `$ARGUMENTS` placeholder. The model calls `Skill("commit")` to trigger a specialised behaviour from a prompt template.

#### Tier 3 — Lower priority

**`ScheduleCron`**
Schedules a recurring or one-shot prompt via a 5-field cron expression. The scheduled task fires at the next cron match and enqueues a new conversation turn. Requires a persistent cron scheduler (`IHostedService` + cron file). Complex but powerful for automation use cases.

**`ConfigTool`**
Lets the model read and write Sovrant's runtime config from within a conversation (change model, permission mode, base URL). The `PUT /v1/config` endpoint already does this from the outside; the tool exposes the same capability to the model itself.

**`LSPTool`**
Language Server Protocol integration: hover type info, go-to-definition, find-references, document symbols. Powerful for code-heavy agentic work but requires a language server process to be running alongside Sovrant. Out of scope until there is a clear IDE integration story.

#### Implementation plan

1. ~~`TaskUpdate` — add to `Sovrant.Tools/Tasks/`, register in `ToolRegistrar`, add unit test~~ ✅ Done
2. ~~`EnterPlanMode` / `ExitPlanMode` — add to `Sovrant.Tools/PlanMode/`; runtime handles the tool result by updating a session-scoped permission override~~ ✅ Done — `IPermissionModeAccessor` added to Runtime; `MutableCliPermissionPolicy` and `MutableServerPermissionModeAdapter` registered in both contexts
3. ~~`EnterWorktree` / `ExitWorktree` — add to `Sovrant.Tools/Worktree/`; session-scoped worktree path stored in `ConversationRuntime` or a scoped service~~ ✅ Done — `WorktreeState` singleton; tools invoke `git worktree add/remove` directly
4. `/undo`/`/redo` — wrap `Write`/`Edit` execution with git snapshot; add `/undo` and `/redo` slash commands in `Sovrant.Commands`
5. ~~Custom project commands — `SlashCommandDispatcher.TryDispatchAsync` checks `.sovrant/commands/{name}.md`~~ ✅ Done
6. ~~`ListMcpResources` / `ReadMcpResource` — `McpClientRegistry` + two tools in `Sovrant.Tools/Mcp/`~~ ✅ Done
7. ~~`ToolSearch` — injects `IToolRegistry`, filters `GetDefinitions()` by keyword~~ ✅ Done
8. ~~`SkillTool` — reads `.sovrant/skills/{name}.md` from disk~~ ✅ Done
9. `ScheduleCron` / `ConfigTool` / `LSPTool` — deferred; document as future work

---

### Phase 2 — Quick Wins from Competitor Analysis ✅

**Goal:** Implement three high-value features identified in the competitor analysis that have no external dependencies, can be done in isolation, and dramatically improve day-to-day usability.

#### Agent Memory Files (`.sovrant/memory.md`)

**Competitor precedent:** Claude Code (`CLAUDE.md` project + global memory files)

The agent reads two markdown memory files at the start of every session and prepends their contents to the system prompt:

- **Global:** `~/.sovrant/memory.md` — user-level preferences, coding style, preferred tools
- **Project:** `.sovrant/memory.md` (in the working directory) — project-specific conventions, which files to avoid, how to run tests, architecture notes

This makes the agent immediately context-aware of any codebase without the user having to re-explain conventions every session. It is the single highest-perceived-value feature for regular users.

**Implementation:** In `ConversationRuntime.BuildSystemPrompt()` — read both files at construction time (if they exist), append their contents after the base system prompt. No new tools required. Add a `/memory` slash command to open the project memory file for editing.

#### Context Auto-Compaction

**Competitor precedent:** Claude Code ✅ · opencode ✅ · OpenClaude ✅

When the accumulated `_history` token count approaches the model's context window limit (configurable threshold, default 80% of `MaxTokens`), the runtime automatically summarises the oldest portion of the history into a compact representation and replaces it. This allows arbitrarily long sessions without hitting limits or paying for redundant tokens.

**Implementation:**
1. After each turn, estimate token count of `_history` (rough: `sum(message.Content.Length) / 4`)
2. If above the threshold, call a summarisation turn: `"Summarise the conversation so far in 500 words, preserving all key decisions, code changes, and open questions."`
3. Replace the oldest N messages with a single `[Compacted: {summary}]` assistant message
4. Persist the compaction event to JSONL so it survives session reload
5. Add `SOVRANT_COMPACT_THRESHOLD` env var (default: `0.8` = 80% of MaxTokens)

#### Token Count Fix (OpenAI Streaming)

**Blocks:** Phase 10 usage tracking, context window visualisation

OpenAI sends `usage` data in the second-to-last SSE chunk (before `[DONE]`), not in `MessageDelta`. The current `CollectStreamEventsAsync` in `ConversationRuntime` only reads `MessageDelta.Usage.OutputTokens` — the OpenAI format uses a top-level `usage` field on the final content chunk.

Fix: detect the `usage` field on the final OpenAI SSE chunk and capture `prompt_tokens` / `completion_tokens` into `accumulated.InputTokens` / `accumulated.OutputTokens`. Once fixed, expose live token counts in the REPL status line and in `GET /v1/sessions/{id}`.

#### Implementation Plan

1. ~~Agent memory files — extend `BuildSystemPrompt()` in `ConversationRuntime`; add `/memory` slash command~~ ✅ Done — `AppendMemoryFile()` helper reads both files; `/memory` and `/mem` commands registered; `InjectAsUserMessage` on `SlashCommandResult` wired into REPL
2. ~~Token count fix — update `CollectStreamEventsAsync` to capture OpenAI `usage` field from final SSE chunk~~ ✅ Done — `OpenAiCompatProvider` removed `yield break` on `finish_reason`; continues loop to capture trailing usage chunk; `ConversationRuntime` captures `InputTokens` from `MessageDelta`
3. Context auto-compaction ⬜ — add compaction logic to `RunTurnAsync`; add `SOVRANT_COMPACT_THRESHOLD` config; persist compaction events to JSONL
4. Expose token counts in REPL status line and `GET /v1/sessions/{id}` response ⬜

---

### Phase 3 — Native Model Web Search (OpenAI Responses API) ✅

> **Status: ✅ Complete** — `LLM_WEB_SEARCH=true` activates `OpenAiResponsesProvider`; tested end-to-end with `gpt-4o-mini`.

**Goal:** Allow teams using OpenAI to skip the Brave/FireCrawl API key requirement by using OpenAI's native `web_search_preview` tool instead.

#### What was built

| Component | File | Notes |
|---|---|---|
| `OpenAiResponsesTypes.cs` | `Sovrant.Api/OpenAi/` | Request/response/SSE types for `POST /v1/responses` |
| `ResponsesFormatConverter.cs` | `Sovrant.Api/OpenAi/` | Converts `MessagesRequest` ↔ Responses API format; handles multi-turn history including function call/result replay |
| `OpenAiResponsesProvider.cs` | `Sovrant.Api/Providers/` | Full `ILlmProvider` implementation; streaming + non-streaming; injects `web_search_preview`; suppresses `WebSearch` function tool |
| `ServiceCollectionExtensions.cs` | `Sovrant.Api/` | Registers `OpenAiResponsesProvider` instead of `OpenAiCompatProvider` when `LLM_WEB_SEARCH=true` |

#### Key design notes

- **Why a separate provider (not inject into chat/completions):** OpenAI's `/v1/chat/completions` only accepts `function` and `custom` tool types. `web_search_preview` is exclusively available on `POST /v1/responses`.
- **`WebSearch` suppressed:** The Responses API provider filters `WebSearch` from the function tools list before sending. This prevents the model from calling our function tool (which requires Brave/FireCrawl) when the native built-in is available.
- **`function_call.id` vs `call_id`:** The Responses API requires `function_call.id` to start with `fc_` (item ID) while `call_id` (starts with `call_`) is used to match tool results. Our `ToolUseBlock.Id` stores `call_id`; the converter prefixes a synthetic `fc_` item ID when rebuilding history for multi-turn requests.
- **`web_search_preview` is transparent:** The model's search calls are handled server-side by OpenAI. No tool call events are emitted to the agentic loop; search results appear directly in the model's text output.

#### Usage

```bash
LLM_WEB_SEARCH=true dotnet run --project src/Sovrant.Cli -- --model gpt-4o-mini prompt "What are today's top tech headlines?"
```

#### What it does NOT cover (future)

- Anthropic does not yet expose a native web search tool via their API — no action needed until they do.
- Gemini via the OpenAI-compat endpoint (`LLM_BASE_URL=.../openai/`) does not support `web_search_preview` — `LLM_WEB_SEARCH=true` should only be set with OpenAI.
- Other providers (Ollama, etc.) are unaffected; `OpenAiCompatProvider` is still used when `LLM_WEB_SEARCH` is not set.

---

### Phase 4 — Security Hardening ✅

**Source:** OpenClaude improvement document review (2026-04-04) — items applicable to Sovrant's tool surface.

**Goal:** Address the most critical security and safety gaps in `BashTool` and `WebFetchTool` before the server is used in any shared or internet-facing deployment.

#### BashTool hardening

**Current gaps:**
- No stdout/stderr size limit — a runaway command can exhaust memory or produce an enormous tool result
- No dangerous env var stripping — inherits `LD_PRELOAD`, `DYLD_INSERT_LIBRARIES`, and other vars that can be abused to hijack process behaviour
- No workspace guard — the tool will happily run with `$HOME` or `/` as its working directory

**Fixes:**
1. Hard `MAX_OUTPUT_BYTES` limit (256 KB) — truncate combined stdout+stderr and append `[truncated: N bytes omitted]` marker
2. Strip `LD_PRELOAD`, `DYLD_INSERT_LIBRARIES`, `LD_LIBRARY_PATH` from the child process environment before exec
3. Workspace guard — if the resolved working directory is `$HOME`, `/`, or outside the configured workspace root, log a warning and surface it in the tool output

#### WebFetchTool SSRF protection

**Current gaps:**
- No SSRF (Server-Side Request Forgery) protection — the agent can be instructed to fetch internal IPs (AWS metadata at `169.254.169.254`, internal services at `192.168.x.x`, `10.x.x.x`, etc.)
- No `file://` scheme blocking — `file:///etc/passwd` would succeed on some HTTP client configurations
- Response is buffered entirely in memory via `ReadAsStringAsync`

**Fixes:**
1. DNS resolution + IP blocklist: after resolving the target hostname, reject requests to RFC-1918 private ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), loopback (`127.0.0.0/8`, `::1`), link-local (`169.254.0.0/16`), and metadata endpoints
2. Block `file://`, `ftp://`, and other non-HTTP schemes at the URL parse stage
3. Block redirect chains that resolve to private IPs (check each redirect destination)
4. Stream response with `HttpCompletionOption.ResponseHeadersRead` + capped `StreamReader` — avoid buffering large responses in memory

#### Implementation Plan

1. `BashTool` — add `MAX_OUTPUT_BYTES = 262144`; pipe stdout+stderr through a capped `MemoryStream`; strip dangerous env vars; add workspace guard warning
2. `WebFetchTool` — add `IsPrivateIp(IPAddress)` helper; resolve DNS before sending; block private IPs and `file://`; follow redirects manually to check each hop; switch to streaming read
3. Add tests for each guard in `Sovrant.Tools.Tests`

---

### Phase 5 — Reliability & Safety ✅

**Source:** OpenClaude improvement document review (2026-04-04).

**Goal:** Prevent runaway agent behaviour and improve resilience against transient LLM provider errors.

#### Provider retry with exponential backoff

**Current state:** `SmartRouter` makes a single attempt. On a `429` or `5xx`, the request fails immediately and the error propagates to the user. The `ApiError.Retryable` flag is already set correctly but never used.

**Fix:** Wrap `RouteAsync` + `StreamAsync` in a retry loop (max 3 attempts, delays 1s / 2s / 4s) for errors where `ApiError.Retryable == true`. Respect `Retry-After` header if present. Non-retryable errors (`400`, `401`, `403`) skip retries immediately.

#### AgentTool recursion depth limit

**Current state:** `AgentTool` spawns a new `ConversationRuntime` and runs a full agentic loop. That sub-agent can itself call `AgentTool`, with no limit on nesting depth — a model instruction can cause infinite recursion, exhausting threads/memory.

**Fix:** Use `AsyncLocal<int>` to track the current nesting depth. Reject calls at depth ≥ 5 with an error result. Pass the depth counter through the `IServiceProvider` or as an ambient context.

#### ReadFileTool file size limit

**Current state:** `ReadAllLinesAsync` loads the entire file into memory regardless of size. A 2 GB log file would be loaded completely.

**Fix:** Check `FileInfo.Length` before reading. If size exceeds 10 MB, return an error: `"Error: file too large to read (N MB). Use Grep to search within it."`. For files between 1–10 MB, include a warning in the output.

#### Implementation Plan

1. Add retry logic to `ConversationRuntime.CollectStreamEventsAsync` — detect retryable errors from `HttpRequestException`, inspect status code, apply exponential backoff
2. Add `AsyncLocal<int> s_agentDepth` to `AgentTool`; increment on enter, check ≥ 5, decrement on exit
3. `ReadFileTool` — add `FileInfo.Length` check; return error if > 10 MB

---

### Phase 6 — Robustness ✅

**Source:** OpenClaude improvement document review (2026-04-04).

**Goal:** Eliminate edge-case data corruption and unbounded output in the remaining core tools.

#### Atomic file writes (`WriteFileTool` / `EditFileTool`)

**Current state:** Both tools call `File.WriteAllTextAsync(path, content)` directly. A crash or cancellation mid-write leaves a partial file at the destination path, corrupting the original.

**Fix:** Write to a temporary file in the same directory, then `File.Move(tmp, destination, overwrite: true)`. `File.Move` is atomic on POSIX (same filesystem). On Windows it is not formally atomic but is best-effort via a rename. This eliminates partial writes in the common case.

#### GlobTool result cap

**Current state:** No explicit cap on result count. On a very large repository (monorepos with 100K+ files) a `**/*` glob can produce an enormous result string.

**Fix:** Cap results at 1000 files. If truncated, append `[truncated: showing 1000 of N files]`.

#### Deferred — `ToolResult` structured record

The OpenClaude document suggests replacing the `string` return from `ITool.ExecuteAsync` with a structured `ToolResult` record carrying `Output`, `IsError`, and optional `Metadata`. This is a **breaking change** to the `ITool` interface and all 31 implementations. Defer to a dedicated Phase 11+ refactor when the tool surface has stabilised.

#### Implementation Plan

1. `WriteFileTool` — write to `{path}.tmp`, then `File.Move` with overwrite
2. `EditFileTool` — same: write full updated content to temp, then rename
3. `GlobTool` — cap `files` list at 1000; append truncation notice if exceeded

---

### Phase 7 — Structured Async Logging ✅

**Goal:** Add non-blocking, structured logging throughout the engine and server so that runtime behaviour, errors, and performance are easy to observe without adding latency to the hot path.

#### Motivation

The engine currently logs sparingly — mostly ad-hoc `ILogger` calls with string interpolation. There is no consistent structure, no correlation IDs, and no sink that can write to a file without blocking the calling thread. When something goes wrong (provider error, tool failure, compaction event) there is no reliable way to reconstruct what happened from logs alone.

#### Provider choice — ZLogger or Serilog + Async sink

| Option | Pros | Cons |
|---|---|---|
| **ZLogger** (`Cysharp/ZLogger`) | Zero-allocation log formatting; designed for .NET high-throughput; outputs structured JSON; no extra abstraction | Less ecosystem documentation; smaller community |
| **Serilog + `Serilog.Sinks.Async`** | Widely known; rich ecosystem; `Serilog.Sinks.File` (rolling) + `Serilog.Sinks.Console`; async sink wraps any sink with a bounded `BlockingCollection` | Small allocation per log event (mitigated by buffering) |

**Recommendation:** Use **Serilog** with `Serilog.Sinks.Async` wrapping both a rolling file sink and the console sink. The async wrapper ensures all disk I/O happens on a dedicated background thread and never blocks the agentic loop. Keep `Microsoft.Extensions.Logging` as the surface API — Serilog plugs in as the backing provider via `UseSerilog()`.

#### Source-generated log delegates

All hot-path log calls must use `[LoggerMessage]`-source-generated delegates (already the pattern in `OpenAiCompatProvider`). Do not use `LogInformation("text {Arg}", value)` directly — it allocates a `string[]` on every call even when the log level is disabled.

Convert all existing inline `_logger.LogXxx(...)` calls to static `[LoggerMessage]`-attributed methods.

#### Structured fields

Every log event in the critical path should carry a consistent set of structured properties:

| Field | Type | Source |
|---|---|---|
| `session_id` | `string` | `ConversationRuntime` |
| `model` | `string` | `MessagesRequest.Model` |
| `provider` | `string` | `ILlmProvider.Name` |
| `tool_name` | `string` | `ITool.Name` (tool events) |
| `turn` | `int` | Loop counter in `RunTurnAsync` |
| `duration_ms` | `long` | `Stopwatch.ElapsedMilliseconds` |
| `tokens_in` | `int` | `Usage.InputTokens` |
| `tokens_out` | `int` | `Usage.OutputTokens` |
| `is_error` | `bool` | Exception / error result |
| `retry_attempt` | `int` | Retry loop (provider retry) |

Use Serilog's `LogContext.PushProperty` to attach `session_id` and `turn` as ambient properties at the start of each `RunTurnAsync` call so they appear on every log line without threading them through every method signature.

#### Critical log points

| Component | Event | Level |
|---|---|---|
| `ConversationRuntime` | Turn start (session, model, turn number) | `Information` |
| `ConversationRuntime` | Turn complete (duration_ms, tokens_in, tokens_out) | `Information` |
| `ConversationRuntime` | Tool call dispatched (tool_name, turn) | `Debug` |
| `ConversationRuntime` | Tool call complete (tool_name, duration_ms, is_error) | `Debug` |
| `ConversationRuntime` | Retry attempt (attempt, delay_ms, reason) | `Warning` |
| `ConversationRuntime` | Context compaction triggered (messages_before, messages_after, tokens_before) | `Information` |
| `ConversationRuntime` | Max tool rounds reached | `Warning` |
| `SmartRouter` | Provider selected (provider_name, reason) | `Debug` |
| `SmartRouter` | Provider health ping result (provider_name, latency_ms, is_healthy) | `Debug` |
| `SmartRouter` | All providers unhealthy — fallback to configured list | `Warning` |
| `OpenAiCompatProvider` | SSE parse skip (unparseable data) | `Warning` |
| `OpenAiCompatProvider` | HTTP / JSON / IO error | `Error` |
| `ToolExecutor` | Permission denied (tool_name, mode) | `Warning` |
| Server request pipeline | Request received (method, path, session_id) | `Debug` |
| Server request pipeline | Request complete (status, duration_ms) | `Information` |
| Server request pipeline | Streaming begun / ended | `Debug` |

#### Configuration

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_LOG_LEVEL` | `Information` | Minimum log level: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `SOVRANT_LOG_FILE` | `~/.sovrant/logs/sovrant-.log` (rolling daily) | Path pattern for the rolling file sink. Set to empty string to disable file logging. |
| `SOVRANT_LOG_CONSOLE` | `true` | Whether to write logs to stdout. Set to `false` to silence console output. |
| `SOVRANT_LOG_FORMAT` | `text` | `text` (human-readable) or `json` (structured JSON — better for log aggregators). |

#### Implementation Plan

1. Add NuGet packages: `Serilog`, `Serilog.Extensions.Hosting`, `Serilog.Sinks.Async`, `Serilog.Sinks.File`, `Serilog.Sinks.Console`, `Serilog.Enrichers.Thread`
2. Wire `UseSerilog()` in `Sovrant.Server` and `Sovrant.Cli` host builders; read `SOVRANT_LOG_LEVEL` / `SOVRANT_LOG_FILE` / `SOVRANT_LOG_CONSOLE` / `SOVRANT_LOG_FORMAT` from environment
3. Convert all existing inline `_logger.LogXxx(...)` calls across `Sovrant.Api`, `Sovrant.Runtime`, `Sovrant.Tools`, `Sovrant.Server` to `[LoggerMessage]`-source-generated delegates
4. Add `LogContext.PushProperty("session_id", ...)` and `LogContext.PushProperty("turn", ...)` scope at the top of `RunTurnAsync`
5. Add `Stopwatch` timing around each tool dispatch in the agentic loop; log `duration_ms` on completion
6. Add provider name to every LLM call log line via `LogContext`
7. Add structured server request middleware (or minimal API filter) that logs request start/end with correlation
8. Add `Sovrant.Server.Tests` integration test: capture log output, verify `session_id` appears on turn-complete log events
9. Update `README.md` environment variable table with the four new `SOVRANT_LOG_*` vars
10. Update `docs/engine-status.md` to mark logging as implemented

---

### Phase 8 — Multi-Tenant Per-Request Credentials ✅

**Goal:** Allow each client to supply its own LLM provider, model, and API key per HTTP request, without the server storing or managing those credentials.

#### Motivation

The current server model has one global `LLM_API_KEY` and `LLM_BASE_URL` set at startup. This works for a single-user or single-organisation deployment. However, the vision for Sovrant is a server that can act as a shared runtime for multiple users or frontends — each potentially using a different LLM provider and key.

Examples:
- A Replit frontend where each user has their own OpenAI key stored in Replit Secrets
- A multi-tenant SaaS where the server must not hold customer API keys at rest
- A dev environment where different engineers use different models (GPT-4o vs Gemini vs local Ollama)

#### Design

Extend the `POST /v1/chat/completions` request body with optional override fields:

```json
{
  "model": "gpt-4o-mini",
  "messages": [...],
  "session_id": "user-123",
  // Per-request credentials are now sent as headers:
  // X-LLM-Api-Key: sk-...
  // X-LLM-Base-Url: https://api.openai.com/v1
}
```

The server:
1. Checks for `X-LLM-Api-Key` / `X-LLM-Base-Url` headers on the request
2. If present, creates a **request-scoped** `IConversationRuntime` with a temporary `ApiKeyAuthProvider` and provider pointing at the override URL
3. If absent, falls back to the global `MutableServerConfig` key/URL as today
4. The session pool keyed on `session_id` must also account for the provider — otherwise two users with the same session ID but different keys would collide

#### Session Pool Key

When per-request credentials are supported, the session pool key must include the provider identity:

```csharp
var poolKey = $"{req.SessionId}::{baseUrl}";
```

This ensures session history is isolated per user even if two users share the same `session_id` string.

#### Security Considerations

- `X-LLM-Api-Key` is a client-supplied secret. The server must never log it, persist it, or include it in error responses.
- The server's own `SOVRANT_TOKEN` continues to gate all requests — the per-request key is only for the downstream LLM call.
- Rate limiting per `X-LLM-Api-Key` or per `SOVRANT_TOKEN` client should be added to prevent abuse.

#### Implementation Plan

1. Read `X-LLM-Api-Key` and `X-LLM-Base-Url` from request headers
2. In `ChatRoutes.HandleAsync`:
   - If headers are present, build a scoped `OpenAiCompatProvider` with those credentials
   - Wrap it in a scoped `SmartRouter` (single-provider, skip ping)
   - Create a scoped `ConversationRuntime` using the scoped router
3. Session pool key: `{sessionId}::{baseUrl}` or a hash of both
4. Add `X-LLM-Api-Key` to the server's sensitive-field redaction list

---

### Phase 9 — Session Lifecycle Management (TTL Eviction + Turn Serialization) ✅

**Goal:** Allow the server to manage many concurrent sessions safely — one `ConversationRuntime` per active session — with automatic eviction of idle sessions and correct serialization of concurrent turns within the same session.

#### Architectural Decision: One Runtime Per Session (Not a Shared Pool)

The alternative considered was a fixed pool of runtimes shared across users (like a database connection pool). This was rejected because:

- `ConversationRuntime` is not stateless — it owns `_history` (`List<InputMessage>`), which IS the session state. Sharing a runtime across users would require loading history from disk before every turn and clearing it after — trading RAM for disk I/O on every single request.
- The "heavy" DI services (`SmartRouter`, `IToolExecutor`, `IToolRegistry`, `ISessionStore`, `SovrantConfig`) are already **shared singletons**. Each runtime holds only references to them. The per-session memory cost is essentially just the conversation history (~40–80 KB for a typical 20-turn session).
- The JSONL store is already the source of truth on disk. Evicting an idle runtime is safe and reversible — the next request for that session reloads history from JSONL and creates a fresh runtime.

One runtime per session is the correct model. The gaps to close are eviction and turn ordering.

#### Gap 1 — Unbounded Growth (TTL + LRU Eviction)

`RuntimeSessionPool` currently keeps runtimes alive indefinitely. Fix:

- `RuntimeSessionPool` records `DateTimeOffset LastAccess` per entry (update on every `GetOrCreateAsync`)
- A background `IHostedService` (e.g., `SessionEvictionService`) runs on a timer (every 5 min) and removes entries where `LastAccess < UtcNow - TTL`
- `SOVRANT_SESSION_TTL_SECONDS` env var (default: `3600`)
- LRU cap: if the active session count exceeds `SOVRANT_MAX_SESSIONS` (default: `500`), evict the least-recently-used sessions above the cap immediately, before the timer fires
- `DELETE /v1/sessions/{id}` continues to work for explicit eviction

Evicted sessions are not lost — their JSONL history persists on disk. The next request with that session ID recreates the runtime and replays history.

#### Gap 2 — Concurrent Turn Corruption (Per-Session Lock)

`ConcurrentDictionary` in the current pool prevents creating two runtimes for the same session, but it does **not** prevent two simultaneous HTTP requests from calling `RunTurnAsync` on the same session concurrently. Since `_history` is a plain `List<InputMessage>`, concurrent turns would corrupt it (both turns append to the same list, producing an interleaved, invalid history).

Fix: add a `SemaphoreSlim(1,1)` per session in the pool. Callers acquire it before starting a turn and release it after.

```csharp
// RuntimeSessionPool internal entry
private sealed record SessionEntry(
    IConversationRuntime Runtime,
    SemaphoreSlim Lock,         // serializes turns within this session
    DateTimeOffset LastAccess);
```

`GetOrCreateAsync` returns both the runtime and the lock. The HTTP handler acquires the lock, runs the turn, and releases it — ensuring turns are strictly ordered per session regardless of how many concurrent HTTP requests arrive.

#### Implementation Plan

1. Add `SessionEntry` record to `RuntimeSessionPool` (runtime + lock + last-access timestamp)
2. Update `GetOrCreateAsync` to return `SessionEntry` (or expose a `RunExclusiveAsync` helper)
3. Update `ChatRoutes.HandleAsync` to acquire/release the per-session lock around `RunTurnAsync`
4. Add `SessionEvictionService : IHostedService` — timer-based TTL sweep + LRU cap enforcement
5. Add `SOVRANT_SESSION_TTL_SECONDS` and `SOVRANT_MAX_SESSIONS` to env var config and `MutableServerConfig`
6. Expose TTL and max-sessions in `GET /v1/status` response

---

### Phase 10 — Small Team Hardening ✅

**Depends on:** Phase 9 (session TTL + per-session lock)

**Goal:** Make Sovrant solid for a small trusted team sharing a single deployment — a single bearer token is fine, but config changes must be session-scoped, each user should bring their own LLM key, and the server must be observable (token usage, context window) and resilient (rate limiting, usage tracking). No user login system required at this stage.

#### What works today for a team

Session history is already isolated per `session_id` — ten users with different session IDs have fully independent conversation histories and tool state. A shared `SOVRANT_TOKEN` is fine for a trusted team where everyone is known and has the token. This is sufficient for a team of up to ~50 engineers sharing an internal deployment.

#### What this phase adds

| Gap | Problem | Fix |
|---|---|---|
| `PUT /v1/config` is global | One user changing the model or permission mode (or calling `EnterPlanMode`) changes it for all sessions simultaneously | Session-scoped config overlay — each `SessionEntry` carries a `SessionConfig` that shadows the global defaults |
| Single `LLM_API_KEY` | All sessions bill to the same key — no per-session cost visibility | Phase 8 per-request `X-LLM-Api-Key` header — each user or client supplies their own key |
| No per-session rate limiting | A single session can saturate the server with concurrent requests | Per-session request rate limiter (ASP.NET Core `RateLimiter` middleware) |
| No cost visibility | No way to see which session is responsible for LLM spend | Per-session token accumulation in `SessionEntry`; `GET /v1/usage` summary endpoint |
| No context window visibility | Users can't see how much context is consumed | `context_used_pct` and `tokens_remaining` in `GET /v1/sessions/{id}`; REPL status line |

#### Design

**Session-scoped config overlay**
`PUT /v1/config` currently mutates a single `MutableServerConfig` singleton — this is what makes `EnterPlanMode` global. Fix: each `SessionEntry` carries a `SessionConfig` (model, permission mode) that overlays the global defaults for that session only. Add `PUT /v1/sessions/{id}/config` for per-session overrides. The global `PUT /v1/config` remains available for operators to set server-wide defaults.

**Per-session rate limiting**
Use ASP.NET Core's built-in `RateLimiter` middleware keyed on `session_id`. Policy: N requests/minute per session (configurable via `SOVRANT_RATE_LIMIT_RPM`, default 60). Returns `429` with a `Retry-After` header.

**Usage tracking**
Each `SessionEntry` accumulates `TotalInputTokens` and `TotalOutputTokens` across all turns. Token count capture (Phase 2 prerequisite) must be fixed first. `GET /v1/usage` returns a per-session summary.

#### Implementation Plan

1. Add `SessionConfig` overlay to `SessionEntry` — model, permission mode; initialised from global `MutableServerConfig` defaults on session creation
2. Add `PUT /v1/sessions/{id}/config` endpoint; update `EnterPlanMode`/`ExitPlanMode` to write to `SessionConfig` rather than the shared singleton (fixes the global plan mode issue)
3. Add ASP.NET Core `RateLimiter` middleware keyed on `session_id`; `SOVRANT_RATE_LIMIT_RPM` env var
4. Fix token count capture (Phase 2 prerequisite); add `TotalInputTokens` / `TotalOutputTokens` to `SessionEntry`
5. Add `GET /v1/usage` endpoint — per-session token summary
6. Add `context_used_pct` and `tokens_remaining` to `GET /v1/sessions/{id}`; surface in REPL status line

---

### Phase 11 — LSP Integration (Language Server Protocol) ✅

**Competitor precedent:** opencode ✅ (20+ language servers, diagnostics, go-to-definition, symbol search)

**Goal:** Give the agent semantic code intelligence — not just text search — by connecting to real language servers running alongside Sovrant.

#### Motivation

Grep and Glob find text. They cannot tell the agent that a variable is unused, that a function signature has changed, or where all callers of a method are. A language server speaks the code's actual type system: it can answer "what type does this return?", "what breaks if I rename this?", "what are all the diagnostics in this file?". This makes refactoring and bug-fixing dramatically more accurate and less likely to introduce regressions.

#### Architecture

An `ILspClient` service manages the lifecycle of one or more language server processes (e.g., `omnisharp`, `pyright`, `typescript-language-server`, `clangd`). The client communicates via JSON-RPC over stdio (the LSP standard transport). Five tool wrappers expose LSP capabilities to the model:

| Tool | LSP request | What it returns |
|---|---|---|
| `LspHover` | `textDocument/hover` | Type info, documentation for a symbol at a file position |
| `LspDefinition` | `textDocument/definition` | File + line where a symbol is defined |
| `LspReferences` | `textDocument/references` | All locations that reference a symbol |
| `LspDiagnostics` | `textDocument/publishDiagnostics` | All errors and warnings in a file |
| `LspRename` | `textDocument/rename` | Workspace-wide rename with preview |

#### Implementation Plan

1. Add `ILspClient` / `LspClient` to a new `Sovrant.Lsp` project — JSON-RPC over stdio, manages server process lifecycle
2. Add language server configuration to `SovrantConfig` (`LspServers: { "csharp": "omnisharp", "python": "pyright" }`)
3. Implement the five tool wrappers in `Sovrant.Tools/Lsp/`
4. Auto-start configured language servers when the CLI starts; shut down on exit
5. In server mode, start language servers per active session (or shared if single-workspace)

---

### Phase 12 — CI/CD Pipeline Integration ✅

**Competitor precedent:** Claude Code ✅ (GitHub Actions + GitLab CI)

**Goal:** Enable Sovrant to run autonomously inside CI pipelines — fix failing tests, resolve build errors, update generated code — without human intervention.

**Status: ✅ Complete**

#### What was implemented

1. **`--ci` flag** on the CLI `prompt` command — machine-readable JSON output (`CiOutput` record), non-zero exit code on error, suppressed console logging
2. **`CiPermissionPolicy`** — auto-approves file edits and shell commands, denies unknown destructive operations (43 tests)
3. **`CiUserInputProvider`** — no-op input provider for non-interactive CI environments
4. **GitHub Actions composite action** at `.github/actions/sovrant-agent/action.yml` — builds, runs the agent in CI mode, exposes `success`, `output`, and `json` outputs
5. **Documentation** at `docs/ci-cd.md` — full reference for `--ci` flag, JSON output format, GitHub Actions usage, GitLab CI template, security notes

#### Key files

- `src/Sovrant.Runtime/Permissions/CiPermissionPolicy.cs`
- `src/Sovrant.Cli/Program.cs` (RunCiTurnAsync, --ci option, CiOutput DTOs)
- `src/Sovrant.Cli/CiUserInputProvider.cs`
- `.github/actions/sovrant-agent/action.yml`
- `docs/ci-cd.md`
- `tests/Sovrant.Runtime.Tests/Permissions/CiPermissionPolicyTests.cs`

---

### Phase 13 — Slack / Webhook Integration ✅

**Goal:** Invoke Sovrant from any messaging platform (Slack, Teams, Discord) or custom system via a generic webhook endpoint.

**Status: ✅ Complete**

#### What was implemented

1. **`POST /v1/webhook`** — generic webhook endpoint in `Sovrant.Server` that accepts `{ source, user_id, message, callback_url, model, thread_id }`, derives a stable session ID (`webhook:{source}:{user_id}`), runs an agentic turn, and returns the result synchronously or POSTs it to a callback URL
2. **`WebhookCallbackService`** — posts results to callback URLs in the background using a named `HttpClient`; errors are logged but do not propagate
3. **Webhook DTOs** — `WebhookRequest`, `WebhookResponse`, `WebhookToolCall` with full JSON serialization
4. **Slack bot** — `integrations/slack/manifest.json` (app manifest) + `integrations/slack/handler.js` (Node.js Bolt SDK event handler using Socket Mode)
5. **Documentation** at `docs/webhooks.md` — webhook endpoint reference, Slack setup guide, Teams/Discord examples, security notes
6. **New test project** `Sovrant.Server.Tests` with 12 tests for webhook DTOs and callback service

#### Key files

- `src/Sovrant.Server/Webhooks/WebhookRequest.cs`
- `src/Sovrant.Server/Webhooks/WebhookResponse.cs`
- `src/Sovrant.Server/Webhooks/WebhookCallbackService.cs`
- `src/Sovrant.Server/Routes/WebhookRoutes.cs`
- `integrations/slack/manifest.json`
- `integrations/slack/handler.js`
- `docs/webhooks.md`
- `tests/Sovrant.Server.Tests/` (12 tests)

---

### Phase 14 — Frontend SDK, Diff View, Session Export ✅

**Goal:** TypeScript/JavaScript client SDK, structured diff rendering in the CLI, and session export endpoint.

**Status: ✅ Complete**

#### What was implemented

1. **TypeScript SDK** at `sdk/js/` — `@sovrant/sdk` npm package
   - `SovrantClient` class: chat (sync + streaming), webhook, config, status, sessions, usage, health
   - SSE stream parser (`parseSSEStream`) for raw chunk iteration
   - React `useChat()` hook with streaming text, tool events, and error handling
   - Per-request credential injection via options
   - Built-in retry on 429/5xx with configurable max retries
   - `SovrantApiError` class for structured error handling
   - Full TypeScript types for all request/response shapes

2. **Structured Diff View** in CLI REPL
   - `DiffRenderer` class renders edit/write tool inputs as colored diffs
   - Edit tools: red lines for `old_string`, green lines for `new_string`
   - Write tools: file path + line count + first 5 lines preview
   - Integrated into `ToolUseRequested` event handling in the REPL

3. **Session Export** — `GET /v1/sessions/{id}/export`
   - Returns full session as human-readable markdown (`text/markdown`)
   - User turns, assistant turns (with model + token counts), tool calls (as code blocks), tool results
   - Long tool results truncated to 2000 chars for readability

#### Key files

- `sdk/js/src/client.ts` — `SovrantClient`
- `sdk/js/src/sse.ts` — SSE parser
- `sdk/js/src/hooks/use-chat.ts` — React hook
- `sdk/js/src/types.ts` — TypeScript types
- `sdk/js/src/index.ts` — barrel export
- `src/Sovrant.Cli/DiffRenderer.cs` — structured diff display
- `src/Sovrant.Server/Routes/SessionRoutes.cs` — added `ExportSession`

---

### Phase 15 — MCP Server Mode ✅

**Goal:** Expose Sovrant as an MCP server so it can be consumed by MCP clients or composed into larger agent pipelines.

**Status:** Complete. `sovrant mcp-server` subcommand activates stdio-based MCP server. Bridges all `IToolRegistry` tools + synthetic `chat` tool + session/config resources. `SOVRANT_MCP_TOOLS` env var for optional filtering. 20 tests. See [`docs/mcp-server.md`](mcp-server.md).

**Files added:**
- `src/Sovrant.McpServer/` — `McpServerSetup.cs`, `ChatToolHandler.cs`, `ToolFilter.cs`
- `tests/Sovrant.McpServer.Tests/` — `ToolBridgeTests.cs`, `ToolFilterTests.cs`, `ChatToolHandlerTests.cs`
- `docs/mcp-server.md` — full documentation with IDE config examples

---

### Phase 16 — Dynamic MCP Tool Proxy (`MCPTool`) ✅

**Goal:** Allow the model to discover and invoke any tool exposed by a connected MCP server dynamically, without those tools being statically registered in `ToolRegistrar` at startup.

**Status:** Complete. `MCPTool` added to `Sovrant.Tools/Mcp/`. Parameters: `tool` (required), `server` (optional — searches all clients when omitted), `input` (object). Uses existing `McpClientRegistry` — no new infrastructure. 8 tests.

**Files added:**
- `src/Sovrant.Tools/Mcp/McpProxyTool.cs` — implementation
- `tests/Sovrant.Tools.Tests/Mcp/McpProxyToolTests.cs` — 8 tests

---

### Phase 17 — MCP OAuth Authentication (`McpAuthTool`) ✅

**Goal:** Support MCP servers that require OAuth 2.0 authentication, enabling Sovrant to connect to SaaS APIs (GitHub, Google, Salesforce, etc.) through MCP without the server holding static API keys.

#### Motivation

At cloud scale, users will want to connect MCP servers that front OAuth-protected APIs. Static API keys are a liability — they don't expire, can't be scoped per user, and are a breach risk if the server is compromised. OAuth tokens are user-scoped, short-lived, and revocable.

#### Design

- `McpAuthTool` initiates an OAuth flow for a named MCP server
- The server generates an authorization URL and returns it to the model
- The model surfaces the URL to the user (CLI: prints it; Server: returns it in the SSE stream for the frontend to open)
- After the user completes the OAuth flow, the callback exchanges the code for tokens
- Tokens are stored in a scoped, encrypted credential store (never in session history or logs)
- The `IMcpClient` for that server is updated with the new token and reconnected

#### Security Considerations

- OAuth tokens must never appear in session JSONL, server logs, or API responses
- Token storage must be encrypted at rest (use OS keychain on CLI, server-side encrypted store for the HTTP server)
- Refresh token rotation must be handled automatically on expiry
- Per-user token isolation is required in multi-tenant deployments (Phase 8 session pool key applies here too)

#### Implementation Plan

1. Add OAuth state machine to `IMcpClient` — `InitiateOAuthAsync`, `CompleteOAuthAsync`, `RefreshTokenAsync`
2. Implement `McpAuthTool` in `Sovrant.Tools/Mcp/`
3. Add encrypted credential store (`ICredentialStore`) with OS keychain backend for CLI and AES-GCM backend for server
4. Add callback endpoint to `Sovrant.Server` (`GET /v1/mcp/auth/callback`)
5. Surface the auth URL in SSE stream as a new `RuntimeEvent.AuthRequired` event type

---

### Phase 18 — Dual Agent Architecture (Scaffolding) ✅

**Source:** Dual Agent Architecture design document (2026-04-04).
**Status:** ✅ Complete — scaffolding done, full execution implemented in Phase 20.

**Goal:** Introduce two interchangeable multi-agent backends behind a shared `IMultiAgentSystem` interface so the rest of the system never depends on a specific implementation. Whichever architecture proves superior in practice can be promoted as the default without touching consumers.

#### Why two backends

Multi-agent coordination is an unsettled space. Process-per-agent (spawning a child process for each agent) matches the original OpenClaude approach and is easy to reason about in isolation. In-process async channels are lighter, faster, and compose naturally with the existing `ConversationRuntime` model. Both are viable; the winner is not yet clear. The interface abstraction preserves both options with zero coupling.

#### Project: `Sovrant.Agents`

Depends on `Sovrant.Runtime` (for `IConversationRuntime`, `IToolRegistry`, `FilteredToolRegistry`). Consumers reference it for the `IMultiAgentSystem` interface and register via `services.AddMultiAgentSystem()`.

```
src/Sovrant.Agents/
  Abstractions/
    IAgent.cs                          ← interface: Name + HandleAsync
    IMultiAgentSystem.cs               ← interface: RegisterAgent, RunTaskAsync, CancelTask, ShutdownAsync
  Models/
    AgentTask.cs                       ← record: Id, Prompt, AssignedAgentName, Metadata, CreatedAt
    AgentResult.cs                     ← record: TaskId, Success, Output, Error; Ok/Fail factories
    AgentRole.cs                       ← enum: General, Planner, Coder, Reviewer, Executor, Supervisor
  Isolated/
    ProcessAgent.cs                    ← IAgent backed by ProcessStartInfo; stdin/stdout stdio
    ProcessBasedMultiAgentSystem.cs    ← spawns ProcessAgent per task; AGENT_MODE=isolated
  Shared/
    BaseAgent.cs                       ← abstract IAgent with Channel<AgentTask> inbox + RunLoopAsync
    MultiAgentCoordinator.cs           ← routes tasks; per-task CTS; shutdown drain
    InProcessMultiAgentSystem.cs       ← wraps coordinator + WorkspaceContext; AGENT_MODE=shared (default)
    WorkspaceContext.cs                ← thread-safe ConcurrentDictionary scratch space for a run
  Config/
    AgentSystemConfig.cs               ← UseIsolatedAgents bool; MaxConcurrentAgents; TaskTimeoutSeconds
    AgentSystemFactory.cs              ← static Create(config, services) → IMultiAgentSystem
  ServiceCollectionExtensions.cs       ← AddMultiAgentSystem(config?) reads AGENT_MODE env var
```

#### Configuration switch

| Mechanism | Effect |
|---|---|
| `AGENT_MODE=isolated` | `ProcessBasedMultiAgentSystem` (process-per-agent) |
| `AGENT_MODE=shared` or unset | `InProcessMultiAgentSystem` (shared, in-process) |
| `AgentSystemConfig.UseIsolatedAgents = true` | Isolated (default), programmatic override |

#### Fully implemented (Phase 20)

- `ProcessAgent.HandleAsync` — process spawn, stdin write, stdout/stderr read, cancellation kills process tree
- `ProcessBasedMultiAgentSystem.RunTaskAsync` — agent resolution, linked CTS, timeout handling
- `MultiAgentCoordinator.DispatchAsync` — agent selection by name/first-registered, semaphore-based concurrency control, linked CTS with timeout, proper cleanup
- `MultiAgentCoordinator.ShutdownAsync` — awaiting all `BaseAgent.RunLoopAsync` tasks
- `SovrantAgent` — `BaseAgent` subclass backed by `IConversationRuntime`, collects `TextChunk` events
- `SovrantAgentFactory` — creates `SovrantAgent` from `TeamMemberInfo` with role-specific prompts and tool filtering
- `AgentPrompts` — role-specific system prompts for each `AgentRole`
- `FilteredToolRegistry` — decorator restricting tool visibility per agent
- `ITeamRegistry` / `InMemoryTeamRegistry` — team member lifecycle management
- Team tools: `TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`

#### Also working

- `WorkspaceContext` — thread-safe `ConcurrentDictionary` variable store
- `BaseAgent` — channel construction, `EnqueueAsync`, `RunLoopAsync`, `Complete`
- `AgentSystemConfig.FromEnvironment()` — reads `AGENT_MODE`
- `AgentSystemFactory.Create` — correct backend selection
- `ServiceCollectionExtensions.AddMultiAgentSystem` — DI wiring (includes `ITeamRegistry` and `SovrantAgentFactory`)
- All interfaces and model records

---

### Phase 19 — Multi-Agent Teams (`TeamCreateTool` / `TeamDeleteTool`) ✅

**Depends on:** Phase 18 (`Sovrant.Agents` scaffolding — already done)

**Goal:** Allow a single Sovrant session to orchestrate a team of independent sub-agents running in parallel, each with its own session, tool access, and LLM provider — coordinated by a supervisor agent.

#### Motivation

Complex long-horizon tasks (large refactors, multi-service deployments, research pipelines) exceed what a single agentic loop can handle reliably. Teams decompose the problem: a supervisor breaks the work into subtasks, spawns specialist agents for each, monitors progress, and synthesises results. This is the foundation for Sovrant as a cloud-scale autonomous engineering platform.

#### Architecture

```
Supervisor Agent (ConversationRuntime)
  ├── TeamCreateTool → spawns Agent A (isolated runtime, own session, own tools)
  ├── TeamCreateTool → spawns Agent B
  ├── TeamCreateTool → spawns Agent C
  └── Collects results via TaskOutput / team message bus
```

- Each team member is a full `ConversationRuntime` with its own session pool slot, tool set, and optionally its own LLM provider/key (Phase 8)
- The supervisor coordinates via `TaskOutput` polling or a lightweight message bus
- Team lifecycle: `TeamCreate` spawns and starts, `TeamDelete` cancels and evicts
- Teams are scoped to the supervisor's session — they are cleaned up when the supervisor session is evicted (Phase 9 TTL)

#### Design Decisions to Resolve

- **Message bus vs polling:** Polling via `TaskOutput` is simpler and avoids a new infrastructure dependency. Start with polling; add a message bus if latency becomes a problem.
- **Resource limits:** Each team member occupies a session pool slot and may trigger many LLM calls in parallel. Rate limiting per team and per supervisor session is required before production use.
- **Nesting depth:** Start with depth-1 (supervisor + workers only); recursive teams add significant complexity.

#### Implementation Plan

1. Design `ITeamRegistry` — tracks team members per supervisor session ID
2. Implement `TeamCreateTool` — accepts agent prompt, tool subset, optional provider override; spawns a `ConversationRuntime` and registers it
3. Implement `TeamDeleteTool` — cancels and evicts the named team member
4. Extend `TaskOutput` to support reading from team member sessions (not just background shell processes)
5. Add team-scoped resource limits to `MutableServerConfig`
6. Add team lifecycle events to the SSE stream (`RuntimeEvent.TeamMemberStarted`, `RuntimeEvent.TeamMemberComplete`)

---

### Phase 20 — Dual Agent Architecture: Full Implementation ✅

**Depends on:** Phase 18 (scaffolding), Phase 19 (team tools that will consume `IMultiAgentSystem`)
**Status:** ✅ Complete — both backends implemented, team tools wired, 58 tests passing.

**Goal:** Complete the two multi-agent backends stubbed in Phase 18. At this point the `TeamCreateTool` / `TeamDeleteTool` from Phase 19 will be wired to `IMultiAgentSystem` and the choice of isolated vs. shared backend becomes a runtime configuration decision.

#### Option A completion: `ProcessBasedMultiAgentSystem`

1. `ProcessAgent.HandleAsync` — spawn child process from `ProcessStartInfo`; write task JSON to stdin; stream stdout line by line; parse structured tool-use blocks (same format as original OpenClaude); propagate `CancellationToken` via `Process.Kill()`
2. `ProcessBasedMultiAgentSystem.RunTaskAsync` — resolve target agent; create linked CTS; stream result incrementally; record CTS for `CancelTask`

#### Option B completion: `MultiAgentCoordinator.DispatchAsync`

1. Resolve target agent by `AgentTask.AssignedAgentName` or by `AgentRole` (planner → coder → reviewer pipeline)
2. For `BaseAgent` subtypes: enqueue via `EnqueueAsync`, pair with a `TaskCompletionSource<AgentResult>` registered by `RunLoopAsync`
3. For plain `IAgent` implementations: call `HandleAsync` directly on a `Task.Run` thread
4. Per-task linked CTS stored in `_taskCts`; `CancelTask` triggers it
5. `ShutdownAsync` — await all `BaseAgent.RunLoopAsync` background tasks

#### Implementation Plan

1. Implement `ProcessAgent.HandleAsync` with stdin/stdout pipes and tool-use message parser
2. Implement `ProcessBasedMultiAgentSystem.RunTaskAsync` with full lifecycle management
3. Implement `MultiAgentCoordinator.DispatchAsync` and update `ShutdownAsync`
4. Wire `TeamCreateTool` to use `IMultiAgentSystem.RunTaskAsync` (replaces ad-hoc `ConversationRuntime` spawning in Phase 19)
5. Add integration tests: isolated backend with a mock echo process; shared backend with a test `BaseAgent` subclass

---

### Phase 21 — Hook Lifecycle System ✅

**Inspired by:** everything-claude-code (34 hooks across 7 lifecycle events, ~16 built-in implementations)
**Depends on:** None — can be implemented independently

**Goal:** Add an event-driven hook system that fires user-defined scripts at key points in the agent lifecycle: before/after tool use, session start/end, pre-compaction, and response stop. This enables quality enforcement, security monitoring, cost tracking, and memory persistence without modifying the core agent loop.

#### Why this is high priority

Hooks are the single most extensible feature in everything-claude-code. They enable the entire ecosystem of quality gates, governance, learning, and observability — all without touching the engine itself. Every subsequent phase (verification loops, governance, cost tracking, memory) becomes dramatically easier to implement when hooks exist.

#### Hook Events

| Event | When | Use Cases |
|---|---|---|
| `SessionStart` | New session begins | Load previous session summary, detect project type, seed context |
| `PreToolUse` | Before any tool executes | Block dangerous operations, validate inputs, config protection |
| `PostToolUse` | After a tool completes | Lint/format edited files, audit log bash commands, accumulate edits |
| `PostToolUseFailure` | After a tool fails | Error analysis, retry suggestions |
| `PreCompact` | Before context compaction | Save state to memory before context is lost |
| `Stop` | Agent response ends | Batch format/typecheck, desktop notifications |
| `SessionEnd` | Session terminates | Persist session summary, extract patterns |

#### Architecture

```
src/Sovrant.Runtime/Hooks/
  IHookRunner.cs           ← interface: RunAsync(HookEvent, HookContext)
  HookEvent.cs             ← enum of lifecycle events
  HookContext.cs            ← event-specific context (tool name, file path, session ID, etc.)
  HookRunner.cs            ← loads hooks from config, executes matching scripts
  HookConfig.cs            ← hook definition: event, matcher (tool/glob), command, timeout
```

#### Configuration

Hooks defined in `.sovrant/hooks.json` or `~/.sovrant/hooks.json`:

```json
{
  "hooks": [
    { "event": "PostToolUse", "matcher": { "tool": "Edit" }, "command": "dotnet format {file}", "timeout": 30000 },
    { "event": "PreToolUse", "matcher": { "tool": "Bash" }, "command": "scripts/validate-command.sh {command}" },
    { "event": "Stop", "command": "scripts/batch-format.sh" }
  ]
}
```

#### Hook Profiles

Three enforcement levels configurable via `SOVRANT_HOOK_PROFILE`:
- `minimal` — session start/end only
- `standard` — all non-blocking hooks (default)
- `strict` — all hooks, including blocking pre-tool-use validators

#### Implementation Plan

1. Define `IHookRunner`, `HookEvent`, `HookContext`, `HookConfig` in `Sovrant.Runtime/Hooks/`
2. Implement `HookRunner` — loads from config, matches event + tool name, executes via `ProcessExecutor`
3. Add hook firing points to `ConversationRuntime.RunTurnAsync` (pre/post tool use) and `RuntimeSessionPool` (session start/end)
4. Add `PreCompact` hook to context compaction logic
5. Add `Stop` hook after the final `TurnComplete` event
6. Support both blocking (pre-tool-use: can abort the tool) and fire-and-forget (post-tool-use) execution modes
7. Add `SOVRANT_HOOK_PROFILE` and `SOVRANT_DISABLED_HOOKS` env vars
8. Tests: hook matching, execution, timeout, blocking vs fire-and-forget, profile filtering

---

### Phase 22 — Specialized Agent Definitions (Role Templates + Model Routing) ✅

**Inspired by:** everything-claude-code (38 specialized agents with tool restrictions and model selection)
**Depends on:** Phase 19+20 (multi-agent team tools — already complete)

**Goal:** Define a library of **24 specialized agent role templates** spanning coding, research, communication, operations, and creative work — each with a structured methodology, constrained tool access, and a **recommended capability level** (high/standard/fast). Templates specify what capability a task *needs*, not which vendor model to use — admins and users choose the actual model and API key. These templates are loaded by `TeamCreate` and `AgentTool` to spawn purpose-built sub-agents. Sovrant is a general-purpose agentic platform — coding is one vertical, not the entire product.

#### Why this is high priority

The team tools exist (Phase 19+20) but agents are generic — they get a role enum and a freeform prompt. Structured role templates with defined methodologies, tool restrictions, and output formats make agents dramatically more effective. This is the difference between "ask an LLM to do research" and "run a structured 6-step deep research workflow with source attribution and confidence scoring."

#### Agent Templates — General-Purpose (10 templates)

| Template | Recommended Level | Tools | Methodology |
|---|---|---|---|
| **Planner** | High | Read, Grep, Glob (read-only) | Requirements analysis → architecture review → step breakdown → implementation ordering |
| **Architect** | High | Read, Grep, Glob (read-only) | System design, trade-off analysis, ADR generation, scalability review |
| **Researcher** | High | Read, Grep, Glob, WebSearch, WebFetch | Multi-source research with citation, 6-step workflow (scope → search → evaluate → synthesize → cite → report) |
| **Chief of Staff** | High | Read, Grep, Glob, Bash, Edit, Write | Multi-channel communication triage (email, Slack, calendar), 4-tier priority classification, response drafting |
| **Content Writer** | Standard | Read, Write, Edit, WebSearch | Long-form content creation with voice matching, anti-slop enforcement, multi-platform adaptation |
| **Data Analyst** | Standard | Read, Grep, Glob, Bash | Data exploration, pattern detection, visualization generation, statistical analysis |
| **Project Manager** | Standard | Read, Grep, Glob, Edit | Issue triage, cross-system coordination (GitHub/Linear/Jira), status tracking, dependency mapping |
| **Doc Updater** | Standard | Read, Write, Edit, Grep, Glob | Documentation sync with code changes, API doc generation, changelog maintenance |
| **Loop Operator** | Standard | Read, Grep, Glob, Bash, Edit | Safe autonomous loop management with monitoring, stall detection, and human escalation triggers |
| **Executor** | Standard | Bash, PowerShell, Read | Run commands, monitor output, report results |

#### Agent Templates — Code-Specific (8 templates)

| Template | Recommended Level | Tools | Methodology |
|---|---|---|---|
| **Code Reviewer** | High | Read, Grep, Glob (read-only) | Multi-severity review (CRITICAL/HIGH/MEDIUM/LOW) with confidence thresholds |
| **Security Reviewer** | High | Read, Grep, Glob (read-only) | OWASP Top 10 scan, secret detection, dependency audit, attack surface analysis |
| **TDD Guide** | Standard | All | Red-Green-Refactor cycle enforcement, 80%+ coverage target, edge case generation |
| **Refactor Cleaner** | Standard | Read, Grep, Glob, Edit | Dead code detection, safe incremental removal with SAFE/CAREFUL/RISKY classification |
| **Coder** | Standard | All | Implementation from specs, test writing, error handling |
| **Build Error Resolver** | Standard | Read, Grep, Glob, Bash | Parse build errors, identify root cause, apply fix, verify build passes |
| **E2E Test Runner** | Standard | Read, Bash, Grep | Playwright/Selenium E2E test execution, failure analysis, screenshot capture |
| **Database Reviewer** | High | Read, Grep, Glob (read-only) | Schema review, query optimization, migration safety analysis |

#### Agent Templates — Creative & Domain (6 templates)

| Template | Recommended Level | Tools | Methodology |
|---|---|---|---|
| **GAN Planner** | High | Read, Grep, Glob | Expands briefs into comprehensive product specs with success criteria |
| **GAN Generator** | Standard | All | Implements features per spec, manages sprint iterations |
| **GAN Evaluator** | High | Read, Bash, Grep | Tests live apps against rubric, scores 4 dimensions (design, originality, craft, functionality) |
| **Prompt Optimizer** | High | Read, Write, Edit | 6-phase prompt analysis: gap identification, ecosystem mapping, structure optimization |
| **Sales Intelligence** | Standard | Read, WebSearch, WebFetch | Lead scoring, prospecting pipeline, warm-path discovery, outreach draft generation |
| **Compliance Reviewer** | High | Read, Grep, Glob (read-only) | Policy adherence verification, PHI/PII detection, regulatory checklist enforcement |

#### Architecture

```
src/Sovrant.Agents/Templates/
  AgentTemplate.cs          ← record: Name, Role, RecommendedLevel, AllowedTools, SystemPrompt, Methodology, OutputFormat
  AgentTemplateRegistry.cs  ← loads built-in + user-defined templates from .sovrant/agents/
  RecommendedLevel.cs       ← enum: High (complex reasoning), Standard (general tasks), Fast (triage/simple)
```

Templates stored as markdown files in `.sovrant/agents/` (user-defined) or embedded resources (built-in):

```markdown
---
name: security-reviewer
role: reviewer
recommended_level: high
allowed_tools: [Read, Grep, Glob]
---
# Security Reviewer

## Identity
You are a security-focused code reviewer specializing in OWASP Top 10...

## Methodology
1. Attack surface mapping
2. Input validation audit
3. Authentication/authorization review
4. Secret detection scan
...
```

#### Model Resolution (Provider-Agnostic)

Templates specify a `RecommendedLevel` (high/standard/fast), not a specific model. The actual model used is resolved at runtime based on what the admin or user has configured:

1. **User override** — `TeamCreate` or `AgentTool` accepts an optional `model` parameter (e.g., `"gpt-4o"`, `"claude-sonnet-4-6"`, `"deepseek-r1"`)
2. **Admin defaults** — `.sovrant/config.json` or env vars map levels to models for the deployment:
   ```json
   { "model_levels": { "high": "claude-opus-4-6", "standard": "gpt-4o", "fast": "gpt-4o-mini" } }
   ```
3. **Template recommendation** — if no override and no admin default, the template's `recommended_level` is logged as a suggestion but the session's current model is used

This keeps Sovrant provider-agnostic. Users who care about cost can route "high" tasks to a cheaper model; users who want the best results can route everything to their top-tier model. The template defines *what capability is needed*, not *which vendor to call*.

#### Implementation Plan

1. Define `AgentTemplate` and `RecommendedLevel` in `Sovrant.Agents/Templates/`
2. Implement `AgentTemplateRegistry` — loads built-in templates from embedded resources + user templates from `.sovrant/agents/`
3. Update `SovrantAgentFactory` to accept `AgentTemplate` and resolve model via user override → admin default → session model
4. Update `TeamCreateTool` to accept a `template` parameter (e.g., `"security-reviewer"`)
5. Update `AgentTool` to accept an optional `template` parameter for ad-hoc sub-agents
6. Ship **24 built-in templates**: 10 general-purpose, 8 code-specific, 6 creative/domain
7. Tests: template loading, model level resolution (override → admin default → session), tool restriction enforcement, user template override

---

### Phase 23 — Template Externalisation (Built-ins as Markdown Files) ✅

**Depends on:** Phase 22 (AgentTemplateRegistry already loads user templates from `.sovrant/agents/`)

**Goal:** Move the 24 built-in agent templates out of `BuiltInTemplates.cs` and into
plain markdown files on disk. This lets operators tune system prompts, adjust tool
lists, and add new templates without recompiling — changes take effect immediately on
the next agent spawn.

#### Why this matters

Right now the built-in templates are embedded in source code. That means:
- Changing a prompt requires a build + deploy cycle.
- Operators running Sovrant as a shared service can't customise templates without forking.
- A/B testing or iterating on prompt methodology requires code changes.

With file-resident templates, a single `.md` edit is all it takes.

#### Design

Built-in templates ship as markdown files in a well-known location, loaded at startup
by `AgentTemplateRegistry`. The search order mirrors the existing layering:

```
1. ~/.sovrant/agents/          — user-global overrides (highest priority)
2. .sovrant/agents/            — project-level overrides
3. <install>/agents/           — shipped built-ins (lowest priority, always present)
```

The install directory is resolved from the executing assembly's location:
`Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)/agents/`

This means the 24 `.md` files are deployed alongside the binary (copied to output
via `<Content CopyToOutputDirectory="Always" />`), but any of them can be shadowed by
a file with the same name in `.sovrant/agents/` or `~/.sovrant/agents/`.

`BuiltInTemplates.cs` is deleted once the files are in place. The fallback
`NullObject` pattern (one hardcoded emergency template) is optional and not required.

#### File layout

```
src/Sovrant.Agents/agents/
  planner.md
  architect.md
  researcher.md
  chief-of-staff.md
  content-writer.md
  data-analyst.md
  project-manager.md
  doc-updater.cs
  loop-operator.md
  executor.md
  code-reviewer.md
  security-reviewer.md
  tdd-guide.md
  refactor-cleaner.md
  coder.md
  build-error-resolver.md
  e2e-test-runner.md
  database-reviewer.md
  gan-planner.md
  gan-generator.md
  gan-evaluator.md
  prompt-optimizer.md
  sales-intelligence.md
  compliance-reviewer.md
```

Each file uses the same front matter format already supported by `AgentTemplateRegistry.ParseTemplateFile`:

```markdown
---
name: security-reviewer
role: Reviewer
recommended_level: High
allowed_tools: [Read, Grep, Glob]
---
You are a security review agent...
```

#### Implementation Plan

1. Create `src/Sovrant.Agents/agents/` and write one `.md` file per built-in template
   (content ported verbatim from `BuiltInTemplates.cs`).
2. Mark each file `<Content CopyToOutputDirectory="PreserveNewest" />` in the `.csproj`.
3. Update `AgentTemplateRegistry` constructor to resolve the install-directory path
   and call `LoadUserTemplates` on it as the lowest-priority source.
4. Delete `BuiltInTemplates.cs`.
5. Update tests: `All_Returns24BuiltInTemplates` and per-template metadata tests should
   still pass — behaviour is unchanged, only the loading mechanism differs.
6. Verify that placing a custom `.md` in `.sovrant/agents/coder.md` correctly shadows
   the shipped `coder.md`.

#### Hot-reload (optional stretch goal)

Use `FileSystemWatcher` on the three template directories to reload changed files
without restarting the process. Gate behind `SOVRANT_TEMPLATE_WATCH=true` to avoid
unnecessary overhead in production.

---

### Phase 24 — Verification Loop & Quality Gates ✅

**Inspired by:** everything-claude-code (6-phase verification loop, quality gate hooks, TDD workflow)
**Depends on:** Phase 21 (hooks — gates run as post-stop hooks) or can be standalone tools

**Goal:** A structured multi-phase quality verification pipeline that runs automatically before PR submission or on demand via a `/verify` skill. Covers: build, type check, lint, test (with coverage threshold), security scan, and diff review.

#### Why this is high priority

The code review (Phases A-F) was manual. A verification loop automates the same checks as repeatable, enforceable gates. Combined with hooks (Phase 21), this runs automatically at the end of every coding session.

#### Verification Phases

| Phase | What it does | Pass criteria |
|---|---|---|
| **Build** | `dotnet build` / `npm run build` / `go build` | Zero errors |
| **Type Check** | Language-specific type checking (e.g., `tsc --noEmit`) | Zero errors |
| **Lint** | `dotnet format --verify-no-changes` / `eslint` / `golangci-lint` | Zero warnings at configured severity |
| **Test** | `dotnet test` with coverage collection | All tests pass, coverage ≥ configurable threshold (default 60%) |
| **Security Scan** | `dotnet list package --vulnerable` / `npm audit` / secret detection | Zero critical/high vulnerabilities |
| **Diff Review** | Git diff analysis: no debug code, no secrets, no unintended files | Clean diff |

#### Architecture

```
src/Sovrant.Tools/Quality/
  VerifyTool.cs              ← runs all 6 phases, returns structured report
  VerificationPhase.cs       ← enum + per-phase runner
  VerificationConfig.cs      ← thresholds, skip list, per-project overrides
  VerificationResult.cs      ← phase results with pass/fail + detail
```

Also a `/verify` skill that the model can invoke or that fires as a `Stop` hook.

#### Configuration

`.sovrant/verify.json`:
```json
{
  "coverage_threshold": 60,
  "lint_severity": "warning",
  "skip_phases": [],
  "build_command": "dotnet build Sovrant.slnx",
  "test_command": "dotnet test Sovrant.slnx --collect:\"XPlat Code Coverage\"",
  "security_command": "dotnet list package --vulnerable"
}
```

#### Implementation Plan

1. Define `VerificationPhase`, `VerificationConfig`, `VerificationResult` in `Sovrant.Tools/Quality/`
2. Implement `VerifyTool` — runs phases sequentially, collects results, returns structured report
3. Auto-detect project type (`.csproj` → dotnet, `package.json` → npm, `go.mod` → Go) for default commands
4. Add `/verify` skill registration
5. Optionally wire as a `Stop` hook (Phase 21) for automatic end-of-session verification
6. Tests: each phase runner, threshold enforcement, skip list, auto-detection

---

### Phase 25 — Governance, Security Monitoring & Audit ✅

**Inspired by:** everything-claude-code (governance capture, config protection, commit quality, enterprise controls)
**Depends on:** Phase 21 (hooks — governance runs as PreToolUse/PostToolUse hooks)

**Goal:** Defense-in-depth for agentic operations: detect secrets in tool outputs, block dangerous commands, protect configuration files, audit-log all bash commands, and provide enterprise control policies.

#### Components

| Component | What it does | Hook Event |
|---|---|---|
| **Secret Detection** | Scans tool inputs/outputs for AWS keys, tokens, JWTs, API keys | PostToolUse |
| **Dangerous Command Blocker** | Blocks `rm -rf /`, `git push --force`, `DROP TABLE`, `chmod 777` | PreToolUse (Bash) |
| **Config Protection** | Prevents modification of `.editorconfig`, `.eslintrc`, `Directory.Build.props` | PreToolUse (Edit/Write) |
| **Bash Audit Log** | Logs every bash command to `~/.sovrant/audit/bash-commands.jsonl` | PostToolUse (Bash) |
| **Commit Quality Gate** | Validates staged files for debug code, secrets, formatting before commit | PreToolUse (Bash, matcher: `git commit`) |
| **Governance Event Stream** | Structured JSON events for security-relevant actions: severity, session, timestamp | All |

#### Architecture

```
src/Sovrant.Runtime/Governance/
  IGovernanceMonitor.cs       ← interface: EvaluateAsync(GovernanceContext) → GovernanceVerdict
  GovernanceMonitor.cs        ← aggregates all detection rules
  GovernanceVerdict.cs        ← Allow / Warn / Block + reason
  SecretDetector.cs           ← regex patterns for common secret formats
  DangerousCommandDetector.cs ← blocked command patterns
  ConfigProtectionRule.cs     ← protected file path patterns
  AuditLogger.cs              ← append-only JSONL audit log
```

#### Enterprise Controls

`.sovrant/governance.json`:
```json
{
  "blocked_commands": ["rm -rf /", "git push --force main"],
  "protected_files": [".editorconfig", "Directory.Build.props", "*.sln"],
  "secret_patterns": ["AKIA[0-9A-Z]{16}", "sk-[a-zA-Z0-9]{48}"],
  "audit_log": true,
  "governance_level": "standard"
}
```

Three levels: `minimal` (audit only), `standard` (audit + warn), `strict` (audit + block).

#### Implementation Plan

1. Define governance interfaces and models in `Sovrant.Runtime/Governance/`
2. Implement `SecretDetector` with configurable regex patterns
3. Implement `DangerousCommandDetector` with configurable blocked patterns
4. Implement `ConfigProtectionRule` with configurable protected file globs
5. Implement `AuditLogger` — append-only JSONL to `~/.sovrant/audit/`
6. Implement `GovernanceMonitor` — aggregates all rules, returns `GovernanceVerdict`
7. Wire into hook system (Phase 21) or directly into `DefaultToolExecutor` as a pre-execution check
8. Add `SOVRANT_GOVERNANCE_LEVEL` env var
9. Tests: secret detection patterns, command blocking, config protection, audit log format

---

### Phase 26 — Skills System (Composable Workflow Packages) ✅

**Inspired by:** everything-claude-code (156+ skills as directory-based workflow definitions)
**Depends on:** Phase 21 (hooks — skills can trigger hooks), Phase 22 (agent templates — skills can spawn agents)

**Goal:** A modular system for packaging multi-step workflows as reusable, composable "skills" — each a single `.md` file with YAML frontmatter and a markdown body. Skills are invoked via `/skill-name` slash commands or programmatically by agents. Ship with **32 built-in skills** across 7 domains — coding is just one.

#### Why this matters

Today Sovrant has a basic `SkillTool` that loads markdown files as system prompt overlays. The everything-claude-code approach is richer: skills are full workflow definitions with steps, agent delegation, tool restrictions, and cross-harness compatibility. Sovrant is a general-purpose agentic platform — chat, research, writing, business ops, project management, and coding. The skill system turns Sovrant from a tool-using agent into a workflow engine that serves all these verticals.

#### Skill Structure — Flat .md Files (mirrors Phase 23 agent templates)

Each skill is a single `.md` file — no directory-per-skill, no sidecar files. When a skill needs embedded code (JavaScript, Python, etc.), the code lives inside a fenced code block within the markdown body. This keeps skills self-contained and allows thousands of skills without directory explosion.

```
.sovrant/skills/
  tdd-workflow.md
  code-review.md
  deep-research.md
  article-writing.md
  market-research.md
```

Built-in skills ship in `src/Sovrant.Tools/Skills/skills/` and are copied to the output directory at build time.

Skill `.md` format:
```markdown
---
name: deep-research
description: Multi-source research with citation and confidence scoring
trigger: /research
agents: [researcher]
tools: [Read, Grep, Glob, WebSearch, WebFetch]
---

# Deep Research

## Steps
1. Define research scope and questions
2. Search multiple sources (web, docs, codebase)
3. Evaluate source quality and relevance
4. Synthesize findings with cross-reference
5. Generate cited report with confidence levels
6. Identify gaps and suggest follow-up queries

## Output Format
- Executive summary
- Detailed findings with citations
- Confidence assessment per finding
- Recommended next steps
```

Skills that need embedded code include it as a fenced block:
```markdown
---
name: data-scraper
description: Autonomous data collection pipeline
trigger: /scrape
tools: [WebFetch, Write]
---

## Handler

\```js
// Embedded JS — no separate file needed
async function scrape(url) { ... }
\```
```

#### Built-in Skills — 32 skills across 7 domains

**Research & Intelligence (5 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `deep-research` | `/research` | Multi-source research from 15-30 sources with cited reports |
| `market-research` | `/market` | Competitive analysis, market sizing, source-attributed business intelligence |
| `data-scraper` | `/scrape` | Autonomous data collection pipeline: collect → enrich → store with scheduling |
| `doc-lookup` | `/docs-lookup` | API and documentation research with structured extraction |
| `search-first` | `/search-first` | Forces web/doc lookup before any implementation begins |

**Writing & Content (5 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `article-writing` | `/article` | Long-form blog posts, essays, newsletters with voice matching and anti-slop rules |
| `content-engine` | `/content` | Multi-platform content for social, newsletters, blogs — repurposes anchor content |
| `brand-voice` | `/brand-voice` | Extracts durable voice profiles from 5-20 writing samples |
| `crosspost` | `/crosspost` | Adapts content across platforms (X, LinkedIn, Threads) with per-platform rules |
| `slides` | `/slides` | HTML presentations and PPTX conversion |

**Business & Operations (5 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `investor-materials` | `/pitch` | Pitch decks (12-slide structure), one-pagers, financial models |
| `lead-intelligence` | `/leads` | AI-native prospecting: signal scoring, mutual ranking, warm-path discovery |
| `billing-ops` | `/billing` | Subscription management, refund triage, churn analysis, plan optimization |
| `connections-optimizer` | `/network` | Network pruning, expansion, rebalancing with warm-path outreach drafts |
| `product-lens` | `/product` | Product analysis, feature evaluation, competitive positioning |

**Project Management (4 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `plan` | `/plan` | Structured planning with phased implementation and dependency mapping |
| `project-flow` | `/flow` | GitHub + Linear/Jira coordination, issue triage, cross-system consistency |
| `team-builder` | `/team` | Interactive agent selection and parallel dispatch with result synthesis |
| `architecture-decision` | `/adr` | Structured ADR creation and management |

**Coding & Quality (7 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `tdd-workflow` | `/tdd` | Red-Green-Refactor with coverage enforcement |
| `code-review` | `/review` | Multi-severity code review (CRITICAL/HIGH/MEDIUM/LOW) |
| `verification-loop` | `/verify` | 6-phase quality gate (Phase 24) |
| `security-review` | `/security` | OWASP-based security audit |
| `refactor` | `/refactor` | Dead code detection + safe removal |
| `doc-update` | `/docs` | Documentation maintenance synced with code changes |
| `codebase-onboard` | `/onboard` | New contributor onboarding — architecture walkthrough, key patterns, setup guide |

**Media & Creative (3 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `media-gen` | `/media` | Image, video, audio generation via AI services (fal.ai, etc.) |
| `video-explainer` | `/explainer` | Animated technical explainers with progressive reveal |
| `ui-demo` | `/ui-demo` | Interactive UI demonstration creation |

**Agent Infrastructure (3 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `autonomous-loop` | `/loop` | 6 loop patterns from simple pipelines to DAG orchestration with merge queues |
| `prompt-optimize` | `/optimize-prompt` | 6-phase prompt analysis: gap identification, structure optimization |
| `skill-create` | `/new-skill` | Define new skills at runtime from successful workflow patterns |

#### Architecture

```
src/Sovrant.Tools/Skills/
  SkillDefinition.cs          ← record: Name, Description, Trigger, Agents, Tools, Body
  SkillParser.cs              ← YAML frontmatter parser (same pattern as AgentTemplateRegistry)
  SkillRegistry.cs            ← 3-tier discovery: assembly dir skills/ → ~/.sovrant/skills/ → .sovrant/skills/
  SkillRunner.cs              ← resolves skill, formats prompt with metadata, $ARGUMENTS substitution
  SkillTool.cs                ← registry-backed; supports "list" and args passthrough
  SkillCreateTool.cs          ← creates .md files in .sovrant/skills/ at runtime
  skills/                     ← 32 built-in .md files (copied to output dir via CopyToOutputDirectory)
```

#### Implementation (complete)

1. `SkillDefinition` record with `IReadOnlyList<string>` for Agents and Tools
2. `SkillParser` — parses .md files with YAML frontmatter; handles `[A, B]` and `A, B` list syntax
3. `SkillRegistry` — 3-tier discovery (assembly dir → global → project-local), case-insensitive name + trigger dictionaries
4. `SkillRunner` — formats prompt with metadata header, tool/agent constraints, body, `$ARGUMENTS` substitution
5. `SkillTool` rewritten to use `SkillRunner`; `SkillCreateTool` writes .md files with path traversal protection
6. 32 built-in skills shipped as flat .md files in `src/Sovrant.Tools/Skills/skills/`
7. MSBuild: `<Content Include="Skills\skills\**" CopyToOutputDirectory="PreserveNewest" Link="skills\%(Filename)%(Extension)" />`
8. DI: `SkillRegistry`, `SkillRunner` singletons + `SkillCreateTool` registered as `ITool`
9. 46 tests across 5 test files (SkillParser, SkillRegistry, SkillRunner, SkillTool, SkillCreateTool)

---

### Phase 27 — Multi-Layered Memory System ✅

**Inspired by:** everything-claude-code (session summaries, learned skills, instincts with confidence scoring)
**Depends on:** Phase 21 (hooks — memory persisted via session start/end hooks)

**Goal:** Evolve Sovrant's current flat memory files (`~/.sovrant/memory.md`) into a multi-layered memory system: session summaries (short-term), learned patterns (medium-term), and instincts with confidence scoring (long-term). Each layer has different persistence, scope, and retrieval characteristics.

#### Memory Layers

| Layer | Scope | Lifetime | What it stores |
|---|---|---|---|
| **Session Summary** | Per-session | Until next session on same project | Tasks attempted, tools used, files modified, outcome |
| **Learned Patterns** | Per-project | Persistent | Extracted code patterns, common fixes, project conventions |
| **Instincts** | Global | Persistent, evolving | Behavioral rules with confidence scores (0.0–1.0), evidence trails |

#### Session Summaries

At session end (via hook or explicit), extract from the JSONL transcript:
- User messages (tasks requested)
- Tools used and files modified
- Outcome (success/failure)
- Tokens consumed

Stored at `~/.sovrant/sessions/{project}/{timestamp}-summary.md`. Loaded at next session start for continuity.

#### Learned Patterns

Extracted from high-quality sessions (configurable threshold: minimum N turns, user-confirmed success). Examples:
- "This project uses NUnit, not xUnit"
- "API routes follow RESTful naming: `/v1/{resource}/{id}`"
- "Tests go in `tests/{Project}.Tests/` mirroring `src/{Project}/`"

Stored at `.sovrant/learned/` per project.

#### Instincts (Confidence-Scored Behavioral Rules)

Long-term behavioral learning with evidence tracking:

```yaml
- id: instinct-001
  trigger: "user corrects test framework choice"
  action: "Check project test dependencies before generating tests"
  confidence: 0.85
  evidence:
    - "2026-04-03: user corrected xUnit → NUnit in Project X"
    - "2026-04-05: correctly used NUnit based on dependency scan"
```

Confidence increases on positive reinforcement, decreases on correction. Low-confidence instincts (< 0.3) are pruned.

#### Architecture

```
src/Sovrant.Runtime/Memory/
  IMemoryStore.cs             ← interface: sessions, patterns, instincts
  SessionSummaryStore.cs      ← JSONL-based session summaries
  LearnedPatternStore.cs      ← markdown files per project
  InstinctStore.cs            ← YAML instinct definitions with confidence tracking
  MemoryInjector.cs           ← selects relevant memories for system prompt injection
```

#### Implementation Plan

1. Define `IMemoryStore` and memory layer models
2. Implement `SessionSummaryStore` — extract from JSONL, persist as markdown
3. Implement `LearnedPatternStore` — markdown files in `.sovrant/learned/`
4. Implement `InstinctStore` — YAML with confidence scoring and evidence
5. Implement `MemoryInjector` — selects relevant memories based on project, recency, and confidence
6. Wire session summary extraction into `SessionEnd` hook (Phase 21) or `RuntimeSessionPool.Evict`
7. Update system prompt builder to inject multi-layered memory
8. Add `/remember` and `/forget` commands for explicit memory management
9. Tests: summary extraction, pattern storage, instinct confidence updates, memory selection

---

### Phase 28 — Eval-Driven Development Framework ✅

**Inspired by:** everything-claude-code (eval harness with pass@k metrics, capability/regression evals, multiple grader types)
**Depends on:** Phase 24 (verification loop), Phase 22 (agent templates)

**Goal:** A formal evaluation framework for testing agent behavior itself — not just the code it produces. Define expected behaviors as evals, run them against agent sessions, and track pass@k metrics over time. This applies to all agent verticals: "Does the research agent cite sources?" "Does the content writer avoid slop phrases?" "Does the planner create actionable steps?" — not just coding tasks.

#### Why this matters

Unit tests verify code. Evals verify the agent. "Does the agent correctly identify security vulnerabilities?" "Does the research agent produce cited, multi-source reports?" "Does the content writer match the brand voice?" "Does the planner break tasks into parallelizable sub-tasks?" These questions can't be answered by running `dotnet test` — they require evaluating the agent's behavior against structured expectations across every domain Sovrant serves.

#### Eval Types

| Type | What it tests | Example |
|---|---|---|
| **Capability Eval** | Can the agent do X? | "Given a failing test, does the agent identify and fix the root cause?" |
| **Regression Eval** | Does Y still work after changes? | "After refactoring the tool executor, does BashTool still handle timeouts?" |
| **Quality Eval** | How well does the agent do X? | "Rate the code review output on completeness (1-5)" |

#### Grader Types

| Grader | How it works |
|---|---|
| **Code-based** | Deterministic: run a command, check exit code / output pattern (e.g., `dotnet test`, `grep "CRITICAL"`) |
| **Model-based** | LLM judges the output against a rubric (e.g., "Does this review identify all 3 planted bugs?") |
| **Human-based** | Flags for manual review, stores rating |

#### Metrics

- `pass@1` — passes on first attempt
- `pass@3` — passes within 3 attempts
- Trend tracking over time (did agent quality improve after template changes?)

#### Architecture

```
src/Sovrant.Runtime/Evals/
  IEvalRunner.cs            ← interface: RunAsync(EvalSuite) → EvalReport
  EvalDefinition.cs         ← eval: name, prompt, expected behavior, grader config
  EvalSuite.cs              ← collection of evals with metadata
  EvalReport.cs             ← results: per-eval pass/fail, pass@k, duration
  Graders/
    CodeGrader.cs           ← runs command, checks output
    ModelGrader.cs          ← sends output to LLM with rubric
```

Evals stored in `.sovrant/evals/`:
```yaml
- name: security-review-detects-sqli
  prompt: "Review src/Controllers/UserController.cs for security issues"
  grader: code
  check: "grep -q 'SQL injection' {output}"
  pass_threshold: 1
```

#### Implementation Plan

1. Define eval interfaces and models in `Sovrant.Runtime/Evals/`
2. Implement `CodeGrader` — command execution + output matching
3. Implement `ModelGrader` — sends eval output to LLM with rubric prompt
4. Implement `EvalRunner` — loads suite, runs evals, computes pass@k
5. Add `/eval` CLI command and skill
6. Add `GET /v1/evals` endpoint for results
7. Store results in `~/.sovrant/evals/results/` for trend tracking
8. Tests: grader execution, pass@k computation, eval loading

---

### Phase 29 — Swarm Orchestrator (Auto-Decomposition + DAG Execution) ✅

> **Status: ✅ Complete** — 17 new files, 62 new tests, 0 warnings. OFF by default; foundation for frontend-driven orchestration.

**Inspired by:** [claude-swarm](https://github.com/affaan-m/claude-swarm) (parallel task decomposition with dependency DAGs, file locking, budget enforcement, quality gate)
**Depends on:** Phase 19+20 (multi-agent team tools), Phase 22 (agent templates), Phase 39 (cost tracking — optional)

**Goal:** Add a **swarm orchestration layer** on top of Sovrant's existing multi-agent infrastructure. A user gives a single complex prompt; a high-capability model automatically decomposes it into a dependency graph of 2-8 subtasks; subtasks execute in parallel waves respecting dependencies, with file-level conflict prevention, budget enforcement, and a quality gate review phase. The swarm uses whatever models the admin/user has configured — decomposition and quality gates use the "high" level model, workers use the "standard" level (all provider-agnostic via Phase 22's model resolution). Available via CLI (`sovrant swarm "task"`), the `SwarmTool` for programmatic use, and `POST /v1/swarm` for frontend integration.

#### What Sovrant already has vs. what this adds

| Existing (Phase 19+20) | This Phase adds |
|---|---|
| Manual agent creation (`TeamCreate`) | **Auto-decomposition**: one prompt → task DAG |
| Manual delegation (`TeamDelegate`) | **Dependency-aware scheduling**: topological wave execution |
| Basic status tracking (`TeamStatus`) | **File conflict prevention**: pessimistic locking per agent |
| Per-agent timeout | **Cross-swarm budget enforcement** with auto-cancellation |
| — | **Quality gate**: post-execution review of combined output |
| — | **Session recording/replay**: full JSONL audit trail of swarm runs |
| — | **Rich progress**: real-time swarm status via SSE to frontend |

#### How it works (3-phase pipeline)

**Phase 1 — Decomposition** (high-level model)
1. User submits a single complex prompt
2. Decomposer calls `IConversationRuntime` (using the admin's configured "high" model) with a structured system prompt instructing it to output a JSON task graph
3. Each task in the graph has: `id`, `description`, `dependencies` (list of task IDs), `files_to_modify` (predicted), `agent_template` (from Phase 22), `allowed_tools`
4. Tasks are organized into parallel waves (levels of the DAG) — wave 0 has no dependencies, wave 1 depends on wave 0, etc.

**Phase 2 — Parallel Execution** (standard/fast-level workers)
1. `SwarmOrchestrator` processes waves sequentially, tasks within a wave in parallel
2. Before launching each task: check file locks (pessimistic), check budget ceiling
3. Each task spawns an agent via `SovrantAgentFactory` using the appropriate `AgentTemplate`
4. PostToolUse tracking records which files each agent actually modifies
5. On completion: release file locks, accumulate cost, pass output to dependent tasks as context
6. On failure: retry up to `max_retries`, then mark failed and cancel dependents

**Phase 3 — Quality Gate** (high-level model)
1. Collect all task outputs
2. Send combined output to the "high" model with a quality review prompt
3. Score 1-10 with verdict: `pass` / `needs_revision` / `fail`
4. If `needs_revision`: identify specific tasks to re-run, loop back to Phase 2
5. Return final combined result to user

#### Architecture

```
src/Sovrant.Agents/Swarm/
  SwarmTask.cs                ← task model: id, prompt, dependencies, files_to_modify, template, status
  SwarmPlan.cs                ← decomposed DAG: tasks, parallel_waves, metadata
  SwarmResult.cs              ← final result: task outputs, quality score, total cost, duration
  ISwarmDecomposer.cs         ← interface: DecomposeAsync(prompt, ct) → SwarmPlan
  LlmSwarmDecomposer.cs       ← high-level model decomposition via IConversationRuntime
  SwarmOrchestrator.cs        ← DAG execution engine with file locking, budget, retries
  SwarmQualityGate.cs         ← post-execution high-level model review with score + verdict
  SwarmSession.cs             ← JSONL event recording and replay

src/Sovrant.Tools/Swarm/
  SwarmTool.cs                ← tool: accepts prompt, returns swarm result (for agent-initiated swarms)
  SwarmStatusTool.cs          ← tool: returns live swarm progress (tasks, status, cost)

src/Sovrant.Cli/Commands/
  SwarmCommand.cs             ← CLI: `sovrant swarm "task" [--budget 5.0] [--max-agents 4] [--dry-run]`
```

#### Configuration

`.sovrant/swarm.yaml` (optional — overrides defaults):
```yaml
swarm:
  max_concurrent: 4          # max parallel agents
  budget_usd: 5.0            # hard cost ceiling
  max_retries: 1             # per-task retry limit
  quality_gate: true         # enable post-execution review
  decomposer_level: high       # recommended level for decomposition (Phase 22 RecommendedLevel)
  worker_level: standard       # default recommended level for workers

templates:                    # override agent templates per task type
  code_change: coder
  review: security-reviewer
  test: tdd-guide
  docs: doc-updater
```

#### CLI Interface

```bash
# Basic: decompose + execute + quality gate
sovrant swarm "Refactor the auth module to use JWT tokens with refresh rotation"

# Dry run: show the decomposed plan without executing
sovrant swarm "Add pagination to all API endpoints" --dry-run

# Budget-constrained
sovrant swarm "Build a complete CRUD API for the inventory module" --budget 3.0 --max-agents 6

# List past swarm sessions
sovrant swarm sessions

# Replay a past session
sovrant swarm replay <session-id>
```

#### Server API (frontend integration)

```
POST /v1/swarm
  Body: { "prompt": "...", "budget_usd": 5.0, "max_concurrent": 4, "quality_gate": true }
  Response: SSE stream of SwarmEvent objects

GET /v1/swarm/{session_id}
  Response: { tasks: [...], status, quality_score, total_cost, duration }

GET /v1/swarm/{session_id}/events
  Response: JSONL event stream (for replay)
```

The SSE stream emits events matching the session recorder: `plan_created`, `task_started`, `task_completed`, `task_failed`, `file_conflict`, `quality_gate_started`, `quality_gate_result`, `swarm_completed`. The frontend can render a live DAG visualization showing task status, active agents, file locks, and cost accumulation.

#### File Conflict Prevention

```
SwarmOrchestrator maintains:
  _fileLocks: ConcurrentDictionary<string, string>  // filePath → agentId

Before launching a task:
  1. Check if any file in task.FilesToModify is locked by another agent
  2. If conflict: mark task BLOCKED, re-check after blocking agent completes
  3. On task start: lock all files in task.FilesToModify
  4. PostToolUse hook: track additional files the agent edits at runtime
  5. On task completion: release all locks held by that agent
```

#### Key difference from claude-swarm

Claude-swarm agents are isolated — downstream tasks wait for predecessors but never receive their output. Sovrant's swarm **passes predecessor output as context** to dependent tasks, enabling true information flow through the DAG. Combined with the existing `TeamDelegate` infrastructure, agents within a swarm can also delegate ad-hoc subtasks beyond the original plan.

#### Implementation Plan (10 steps, ~12 new files)

1. Define `SwarmTask`, `SwarmPlan`, `SwarmResult` models in `Sovrant.Agents/Swarm/`
2. Implement `LlmSwarmDecomposer` — structured high-level model call that outputs JSON task DAG
3. Implement `SwarmOrchestrator` — DAG scheduler with `SemaphoreSlim`, file locks, budget tracking, retry logic
4. Implement `SwarmQualityGate` — post-execution high-level model review with structured scoring
5. Implement `SwarmSession` — JSONL event recording and replay
6. Create `SwarmTool` and `SwarmStatusTool` in `Sovrant.Tools/Swarm/`
7. Add `SwarmCommand` to CLI (`sovrant swarm "prompt" [flags]`)
8. Add `POST /v1/swarm` SSE endpoint and `GET /v1/swarm/{id}` to `Sovrant.Server`
9. Wire into DI: `ISwarmDecomposer`, `SwarmOrchestrator`, session store
10. Tests: decomposition parsing, DAG scheduling, file lock conflicts, budget enforcement, quality gate scoring, SSE event streaming, session replay

---

### Phase 30 — Registry Discovery API (Tools, Skills, Agent Templates) ✅

**Depends on:** Phase 26 (skills system), Phase 23 (agent templates), existing tool registry
**Difficulty:** Low–Medium

**Goal:** Expose read-only API endpoints that let frontends discover what the engine can do — every registered tool, every built-in and user-defined skill, and every agent template. Today the engine has 43 tools, 32 skills, and 24 agent templates, but a frontend has no way to enumerate them or display their metadata to the user. These endpoints close that gap so UIs can render tool palettes, skill catalogs, and agent template pickers.

#### Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/v1/tools` | List all registered tools with name, description, and parameter schema |
| `GET` | `/v1/tools/{name}` | Get a single tool's full definition (name, description, parameters JSON Schema) |
| `GET` | `/v1/skills` | List all skills (built-in + global + project-local) with name, description, trigger |
| `GET` | `/v1/skills/{name}` | Get a single skill's full definition including body, agents, and tools constraints |
| `GET` | `/v1/agents/templates` | List all agent templates with name, description, recommended level, allowed tools |
| `GET` | `/v1/agents/templates/{name}` | Get a single agent template's full definition including system prompt body |

All endpoints are authenticated (same `SOVRANT_TOKEN` bearer auth as existing endpoints). Responses are JSON.

#### Response Shapes

**`GET /v1/tools`**
```json
{
  "tools": [
    {
      "name": "Read",
      "description": "Read a file from the filesystem",
      "parameters": { "type": "object", "properties": { "file_path": { ... } }, ... }
    }
  ],
  "count": 43
}
```

**`GET /v1/skills`**
```json
{
  "skills": [
    {
      "name": "deep-research",
      "description": "Multi-source research with citation and confidence scoring",
      "trigger": "/research",
      "agents": ["researcher"],
      "tools": ["Read", "Grep", "Glob", "WebSearch", "WebFetch"]
    }
  ],
  "count": 32
}
```

**`GET /v1/agents/templates`**
```json
{
  "templates": [
    {
      "name": "security-auditor",
      "description": "Security-focused code review agent",
      "recommended_level": "High",
      "allowed_tools": ["Read", "Grep", "Glob"]
    }
  ],
  "count": 24
}
```

#### Architecture

- `ToolRegistryRoutes` — maps `IToolRegistry.All` to JSON; parameter schema from `ToolDefinition.Parameters`
- `SkillRegistryRoutes` — maps `SkillRegistry.All` to JSON; detail endpoint includes body
- `AgentTemplateRoutes` — maps `AgentTemplateRegistry.All` to JSON; detail endpoint includes system prompt
- All three registries already exist as singletons in DI — no new infrastructure needed

#### Implementation Plan

1. Add `GET /v1/tools` and `GET /v1/tools/{name}` route group in `Sovrant.Server`
2. Add `GET /v1/skills` and `GET /v1/skills/{name}` route group
3. Add `GET /v1/agents/templates` and `GET /v1/agents/templates/{name}` route group
4. Update `docs/server.md` with new endpoints
5. Update README server API table
6. Tests: list endpoints return correct counts, detail endpoints return 404 for unknown names, auth required

---

### Phase 31 — Server Response Caching & Cache Infrastructure ✅

**Depends on:** Phase 30 (registry discovery API — primary consumer), Phase 7 (server)
**Difficulty:** Medium

**Goal:** Add a caching layer to the server so that expensive or repeated reads (registry listings, session metadata, provider health) are served from cache with proper TTL expiry and invalidation on mutation. Ship with an in-memory cache by default and an optional Redis adapter for multi-instance deployments.

#### Why this matters

The Phase 30 registry endpoints (`/v1/tools`, `/v1/skills`, `/v1/agents/templates`) return data that changes only when skills are created or the server restarts. Without caching, every request re-enumerates the registry. Session metadata, provider health scores, and config are similarly stable between mutations. A cache with proper invalidation turns these into near-zero-cost reads and enables HTTP cache headers so frontends can cache client-side too.

#### What gets cached

| Resource | Cache key | TTL | Invalidation trigger |
|---|---|---|---|
| Tool registry listing | `tools:list` | 1 hour | Server restart (tools are static) |
| Skill registry listing | `skills:list` | 5 min | `SkillCreate` tool execution, file watcher on `.sovrant/skills/` |
| Agent template listing | `templates:list` | 5 min | File watcher on `.sovrant/agents/` |
| Single tool/skill/template detail | `{type}:{name}` | Same as listing | Same as listing |
| Session metadata (message count, token totals) | `session:{id}:meta` | 30 sec | Any turn completion on that session |
| Provider health/status | `providers:health` | 10 sec | Health ping cycle |
| `/v1/config` response | `config:current` | Until mutation | `PUT /v1/config`, `PUT /v1/sessions/{id}/config` |

#### HTTP Cache Headers

- `ETag` on all cacheable GET responses — computed from a hash of the serialized response
- `Cache-Control: private, max-age={ttl}` matching the server-side TTL
- `304 Not Modified` when client sends `If-None-Match` with a matching ETag
- Mutable endpoints (`PUT`, `POST`, `DELETE`) return `Cache-Control: no-store`

#### Architecture

```
src/Sovrant.Runtime/Caching/
  ICacheProvider.cs            ← Get<T>, Set<T>(key, value, ttl), Remove(key), RemoveByPrefix(prefix)
  InMemoryCacheProvider.cs     ← ConcurrentDictionary + timer-based TTL sweep (default)
  RedisCacheProvider.cs        ← StackExchange.Redis adapter (opt-in via SOVRANT_CACHE_REDIS_URL)
  CacheInvalidator.cs          ← event-driven: listens to registry changes, session events, config mutations

src/Sovrant.Server/Middleware/
  ETagMiddleware.cs            ← computes ETag for GET responses, handles If-None-Match → 304
```

#### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_CACHE_PROVIDER` | `memory` | `memory` or `redis` |
| `SOVRANT_CACHE_REDIS_URL` | — | Redis connection string (required when provider is `redis`) |
| `SOVRANT_CACHE_DEFAULT_TTL` | `300` | Default TTL in seconds for entries without an explicit TTL |

#### Implementation Plan

1. Define `ICacheProvider` interface (Get/Set/Remove/RemoveByPrefix with `TimeSpan` TTL)
2. Implement `InMemoryCacheProvider` — `ConcurrentDictionary<string, CacheEntry>` with background `Timer` sweep (every 60s)
3. Implement `RedisCacheProvider` — thin wrapper over `StackExchange.Redis` `IDatabase`; serialize values as JSON
4. Add `CacheInvalidator` — subscribes to `SkillRegistry` changes, `RuntimeSessionPool` events, config mutations; calls `RemoveByPrefix`
5. Wire caching into registry routes (Phase 30): check cache before enumerating, populate on miss
6. Wire caching into existing routes: `/v1/status`, `/v1/config`, `/v1/sessions/{id}` metadata
7. Add `ETagMiddleware` — hash response body for GET routes, compare `If-None-Match`, return `304` or full response
8. Register `ICacheProvider` in DI (factory selects implementation based on `SOVRANT_CACHE_PROVIDER`)
9. Tests: TTL expiry, invalidation on mutation, ETag match/mismatch, Redis adapter (integration test with Testcontainers or mock)

---

### Phase 32 — Persistence Layer (SQLite) ✅

**Depends on:** Phase 7 (structured logging), Phase 25 (governance audit), Phase 27 (memory system)
**Difficulty:** Medium–High

**Goal:** Introduce SQLite as **the** persistence layer for the engine. Everything except `.md` files moves into a single database — config, sessions, audit, credentials, token usage, and CLI memory. SQLite is the starting point: zero-config, zero-infrastructure, works offline, ships as a single file. The abstraction (`IStorageProvider`) is designed so that a future phase can swap in Postgres, CockroachDB, or Turso without touching any consumer code.

Both the CLI and the server share the same `IStorageProvider` singleton from `Sovrant.Runtime` — one database, two entry points.

#### The principle

**`.md` files stay as files. Everything else moves to the database.**

`.md` files (skills, agent templates, memory notes) are version-controlled, git-diffable, human-authored content. They belong in a repo or a dotfiles directory. Everything else — config, sessions, audit, credentials, usage — is operational data that the engine produces and consumes. Operational data belongs in a database where it can be scoped per-user, per-team, queried, and migrated.

#### Why this matters

**Multi-user/multi-team config.** Today config is a 3-file merge (`~/.sovrant/settings.json` → `.sovrant/settings.json` → `.sovrant/settings.local.json`). Adding per-user or per-team overrides means more files, more merge logic, more edge cases. With a `config` table, scoping is a query:

```sql
SELECT key, value FROM config
WHERE scope IN ('global', 'team:engineering', 'user:eric')
ORDER BY priority DESC  -- user > team > global
```

**Queryable operations.** Finding "all governance violations in the last 7 days" means scanning every line of `governance.jsonl`. Finding "which sessions touched file X" means opening every JSONL. Token usage is lost entirely on restart. SQLite gives indexed queries with zero infrastructure.

**Session search.** Cross-session search (`GET /v1/sessions?query=auth+module`) currently requires scanning every JSONL file on disk. With transcripts in SQLite, it's a full-text search index.

**Credential isolation.** Per-user credential scoping becomes natural: `(user_id, credential_key, encrypted_blob)` instead of a flat directory of `.enc` files.

**Growth path.** SQLite today, Postgres tomorrow. The `IStorageProvider` abstraction means swapping the backend is a DI registration change, not a rewrite.

#### Complete persistence inventory

Every place the engine currently writes data to disk:

**Moves to SQLite**

| Data | Current storage | Current format | What SQLite enables |
|---|---|---|---|
| **Config — settings** | `~/.sovrant/settings.json` + `.sovrant/settings.json` + `.sovrant/settings.local.json` | JSON (3-file merge) | Per-user/per-team scoped config; `PUT /v1/config` writes to DB, not files; no more merge logic |
| **Config — hooks** | `~/.sovrant/hooks.json` + `.sovrant/hooks.json` | JSON | Per-user hook overrides; query "which hooks are active for user X in project Y" |
| **Config — governance** | `~/.sovrant/governance.json` + `.sovrant/governance.json` | JSON (merged) | Per-team governance rules; audit trail of rule changes |
| **Config — verification** | `.sovrant/verify.json` | JSON | Per-project with team overrides; consistent with other config |
| **Session transcripts** | `~/.sovrant/sessions/*.jsonl` | JSONL (AppendAllTextAsync) | Cross-session full-text search; per-user session isolation; no filesystem scanning; `GET /v1/sessions?query=...` |
| **Governance audit log** (Phase 25) | `~/.sovrant/audit/governance.jsonl` | JSONL (AppendAllTextAsync) | Query by tool, session, severity, date range |
| **Bash command audit log** (Phase 25) | `~/.sovrant/audit/bash-commands.jsonl` | JSONL (AppendAllTextAsync) | Query by command, exit code, session |
| **Session index** | Implicit (scan JSONL filenames) | No storage (derived) | Query by creation date, last access, project, message count, files touched, token totals |
| **Token usage history** (Phase 10) | In-memory only (`SessionConfig.AddTokens()`) | Not persisted | Persist across restarts; query by date/model/session; historical cost analysis |
| **Encrypted credentials** (Phase 17) | `~/.sovrant/credentials/*.enc` + `.keystore` | Binary (AES-GCM) / Hex | Per-user credential isolation; no directory scanning; key rotation without file juggling |
| **Memory entries** | `~/.sovrant/memory.md` + `.sovrant/memory.md` | Markdown (2 flat files) | Per-user, per-project, typed (user/feedback/project/reference); searchable; concurrent writes without file lock; multiple sessions can add entries simultaneously |
| **CLI memory — learned patterns** (Phase 27) | `.sovrant/learned/*.md` (planned) | Markdown (planned) | Full-text search, confidence scoring, evidence trails, recency ranking |
| **CLI memory — instincts** (Phase 27) | `~/.sovrant/instincts/*.yaml` (planned) | YAML (planned) | Query by trigger, confidence threshold, decay pruning |

**Stays as files**

| Data | Path | Format | Why it stays |
|---|---|---|---|
| **Skills** | `.sovrant/skills/*.md` + built-in `src/.../skills/` | Markdown + YAML frontmatter | Version-controlled, git-diffable, human-authored; loaded into memory at startup |
| **Agent templates** | `.sovrant/agents/*.md` + built-in `src/.../agents/` | Markdown + YAML frontmatter | Same as skills |
| **Memory `.md` bootstrap files** | `~/.sovrant/memory.md` + `.sovrant/memory.md` | Markdown | Read-only seed layer — imported into `memory_entries` table on first run; existing files still loaded as fallback if DB is empty; new entries go to SQLite |
| **Rolling app logs** (Phase 7) | `~/.sovrant/logs/sovrant-{Date}.log` | Text or JSON | Standard log files consumed by log aggregators (Datadog, ELK, etc.); daily rotation; external tooling expects files |
| **Temp scripts** | `{TempPath}/sovrant_*.{sh,ps1,cmd}` | Text | Ephemeral — created for tool execution, deleted immediately after |

#### Shared by CLI and Server

`IStorageProvider` lives in `Sovrant.Runtime` — both `Sovrant.Cli` and `Sovrant.Server` reference it. Both register it as a singleton in DI. Same database, same schema, same migrations.

| Entry point | Writes | Reads |
|---|---|---|
| **CLI** | Config, memory entries, audit events, bash commands, session transcripts, session index, token usage, credentials, learned patterns, instincts | Config (scoped), memory (prepend to system prompt), session index (resume), audit queries, credential lookup |
| **Server** | Same as CLI, plus per-request token tracking at higher volume | Same as CLI, plus `GET /v1/audit`, `GET /v1/usage`, `GET /v1/sessions?query=...`, per-user memory via API |

#### Config scoping model

Config moves from file-merge to scope-priority resolution:

| Scope | Priority | Who sets it | Example |
|---|---|---|---|
| `global` | 0 (lowest) | Server admin / CLI defaults | Default model, log level |
| `team:{name}` | 1 | Team lead via API | Team-specific governance level, allowed tools |
| `project:{path}` | 2 | Project `.sovrant/` directory (migrated) | Project-specific verify config, hooks |
| `user:{id}` | 3 (highest) | Individual via CLI or API | Personal model preference, permission mode |

Resolution: for a given key, the highest-priority scope wins. `ConfigLoader` becomes `ConfigResolver` backed by `IStorageProvider` instead of file I/O.

#### Lightweight user identity

Phase 32 introduces a `users` table — not full enterprise auth (that's Phase 37), just enough to give every other table an `owner_id` foreign key. This enables per-user config, session isolation, credential scoping, and usage attribution without building a login system.

| Context | How user ID is resolved |
|---|---|
| **CLI** | `SOVRANT_USER_ID` env var, or defaults to OS username (`Environment.UserName`) |
| **Server** | Derived from bearer token → user mapping (simple `SOVRANT_TOKENS` JSON map: `{"token": "user_id"}`). Falls back to `"anonymous"` for single-token setups. |

The `users` table is an anchor row — created on first seen, referenced everywhere by `user_id`. No passwords, no OAuth, no sessions-as-auth. Phase 38 adds per-user token auth on top of this same schema.

```sql
-- Every table references user_id for scoping
SELECT * FROM session_index WHERE user_id = 'eric';
SELECT * FROM config WHERE scope = 'user:eric';
SELECT SUM(output_tokens) FROM token_usage WHERE user_id = 'eric' AND timestamp > '2026-04-01';

-- Memory: per-user, per-project, typed, searchable
SELECT * FROM memory_entries WHERE user_id = 'eric' AND project = 'sovrant' AND type = 'feedback';
SELECT * FROM memory_entries WHERE user_id = 'eric' AND content LIKE '%testing%' AND is_stale = 0;
```

#### Architecture

```
src/Sovrant.Runtime/Storage/
  IStorageProvider.cs           ← abstraction: query, insert, update, delete, transaction support
  SqliteStorageProvider.cs      ← Microsoft.Data.Sqlite, WAL mode, connection pooling
  StorageMigrator.cs            ← versioned schema migrations (version table + ordered scripts)

  Tables:
    users                      ← (user_id PK, display_name, created_at, last_seen_at)
    config                     ← (id, scope, key, value_json, updated_at)
    sessions                   ← (id, session_id, user_id FK, role, content, tool_calls_json, timestamp)
    session_index              ← (session_id PK, user_id FK, project, created_at, last_accessed,
                                  message_count, files_modified, total_input_tokens, total_output_tokens)
    audit_events               ← (id, timestamp, user_id FK, session_id, tool, action, severity, detail)
    bash_commands              ← (id, timestamp, user_id FK, session_id, command, exit_code, duration_ms)
    credentials                ← (id, user_id FK, credential_key, encrypted_blob, created_at, updated_at)
    token_usage                ← (id, user_id FK, session_id, model, input_tokens, output_tokens, timestamp)
    memory_entries             ← (id, user_id FK, project, type, name, description, content,
                                  source_session, created_at, updated_at, is_stale)
    learned_patterns           ← (id, user_id FK, project, pattern, source_session, confidence, created_at, last_used)
    instincts                  ← (id, user_id FK, trigger, action, confidence, evidence_json, created_at, updated_at)
```

All tables except `users` and `config` have a `user_id` foreign key. Queries naturally scope by user without extra logic. Phase 38's per-user token auth adds token management and access control on top of this existing schema — the data model doesn't change, only who is allowed to set `user_id`.

#### Database Location

All persistent data lives under `{project-root}/data/`. This keeps the database alongside the codebase (easy to find, back up, and `.gitignore`), separate from config/template files in `.sovrant/`, and avoids polluting the user's home directory.

| Context | Path | Rationale |
|---|---|---|
| CLI (per-user) | `{project-root}/data/sovrant.db` | Alongside the project; `data/` is gitignored |
| Server | `$SOVRANT_DB_PATH` or `{project-root}/data/sovrant.db` | Configurable for containers; defaults to project root |

The `data/` directory also holds WAL and SHM files that SQLite creates automatically (`sovrant.db-wal`, `sovrant.db-shm`). Add `data/` to `.gitignore`.

#### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_STORAGE_PROVIDER` | `sqlite` | `sqlite` now; `postgres` in a future phase |
| `SOVRANT_DB_PATH` | `{project-root}/data/sovrant.db` | SQLite database file path |
| `SOVRANT_USER_ID` | OS username | User identity for CLI (server resolves from token) |
| `SOVRANT_TOKENS` | — | JSON map of `{"bearer-token": "user_id"}` for server multi-user identity resolution |
| `SOVRANT_AUDIT_JSONL` | `false` | Dual-write audit to JSONL alongside SQLite (migration period) |
| `SOVRANT_SESSION_JSONL` | `false` | Dual-write session transcripts to JSONL alongside SQLite (migration period) |

#### Migration from flat files

On first run, `StorageMigrator` detects existing flat-file data and imports it:

1. **Config files** → Reads each JSON config file, inserts key-value pairs into `config` table with appropriate scope (`global` for `~/.sovrant/`, `project:{path}` for `.sovrant/`)
2. **Memory files** → Parses `~/.sovrant/memory.md` (global scope) and `.sovrant/memory.md` (project scope), splits into individual entries, inserts into `memory_entries` with `type` inferred from content structure
3. **Session transcripts** → Reads each `*.jsonl`, inserts messages into `sessions` table, builds `session_index` row from metadata
4. **Audit logs** → Reads `governance.jsonl` and `bash-commands.jsonl`, inserts into respective tables
5. **Credentials** → Reads `*.enc` files, inserts encrypted blobs into `credentials` table (master key migrates too)
6. **Token usage** → No existing persistence to migrate (currently in-memory only)

Post-migration:
- Original files are **not deleted** — they become archival copies
- `SOVRANT_AUDIT_JSONL=true` and `SOVRANT_SESSION_JSONL=true` enable dual-write during transition
- Migration is **idempotent** — keyed by content hash + timestamp, re-running skips already-imported records
- `ConfigLoader` falls back to file-based resolution if SQLite is unavailable (graceful degradation)

#### Future growth path

SQLite is the starting persistence layer, not the final one. The `IStorageProvider` abstraction is designed for this:

| Scale | Backend | When |
|---|---|---|
| Single user / small team | SQLite (this phase) | Now |
| Multi-instance server | Postgres / CockroachDB | When horizontal scaling is needed |
| Edge / embedded | SQLite remains ideal | Always |
| Distributed edge | Turso (libSQL) | SQLite-compatible with replication |

Swapping backends is a DI registration change + migration script, not a rewrite. All consumers use `IStorageProvider` — they never touch `SqliteConnection` directly.

#### Relationship to Phase 27 (Memory System → SQLite)

Phase 27 introduces three memory layers that initially use flat files. Phase 32 migrates all three into SQLite:

| Memory layer | Phase 27 storage | Phase 32 table | What changes |
|---|---|---|---|
| **Session summaries** | `~/.sovrant/sessions/{project}/{ts}-summary.md` | `session_summaries` `(id, user_id, project, session_id, summary_md, tasks, tools_used, files_modified, outcome, tokens_in, tokens_out, created_at)` | Full-text search across summaries; per-user scoping; no filesystem scanning |
| **Learned patterns** | `.sovrant/learned/*.md` | `learned_patterns` `(id, user_id, project, pattern, source_session, confidence, created_at, last_used)` | Query by project + confidence; automatic decay; concurrent multi-session writes |
| **Instincts** | `~/.sovrant/instincts/*.yaml` | `instincts` `(id, user_id, trigger, action, confidence, evidence_json, created_at, updated_at)` | Query by trigger keyword; confidence-threshold pruning via SQL; evidence append without YAML rewrite |

Migration strategy:
- `StorageMigrator` reads existing Phase 27 flat files and inserts into respective tables
- `IMemoryStore` implementations get a second constructor path accepting `IStorageProvider`
- When `IStorageProvider` is available (Phase 32+), stores write to SQLite; otherwise fall back to flat files
- `.md` and `.yaml` files are kept as archival copies post-migration

#### Relationship to Phase 31 (Caching)

Phase 31's `ICacheProvider` is for **hot, ephemeral data** — fast reads with TTL expiry, no durability guarantee. Phase 32's `IStorageProvider` is for **cold, durable data** — structured records that survive restarts and support queries. They compose:
- Cache a query result from SQLite in the in-memory/Redis cache for repeated fast access
- Invalidate the cache entry when the underlying SQLite data changes
- Example: `GET /v1/audit?last=7d` → first request queries SQLite, caches for 30s; new audit event invalidates

#### Implementation Plan

1. Add `Microsoft.Data.Sqlite` package to `Sovrant.Runtime`
2. Define `IStorageProvider` interface — generic query/insert/update/delete with typed results, transaction support
3. Implement `SqliteStorageProvider` — connection pooling, WAL mode, `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL`
4. Implement `StorageMigrator` — `schema_version` table + ordered migration classes
5. **Config migration:** Replace `ConfigLoader` file-merge with `ConfigResolver` backed by `config` table; scoped resolution by priority
6. **Session migration:** Replace `JsonlSessionStore` with SQLite-backed `SqliteSessionStore`; `sessions` table for messages, `session_index` for metadata
7. **Audit migration:** Replace `AuditLogger` file writes with SQLite inserts; add `SOVRANT_AUDIT_JSONL` dual-write toggle
8. **Credential migration:** Replace `AesGcmCredentialStore` file I/O with `credentials` table; same AES-GCM encryption, different storage
9. **Token usage:** Add `token_usage` table; wire `SessionConfig.AddTokens()` to persist
10. **Memory migration:** Add `memory_entries` table; replace `MemoryCommand` file I/O with SQLite reads/writes; import existing `.md` files on first run; `/memory` command writes to DB
11. Prepare `learned_patterns` and `instincts` tables (schema only — populated by Phase 27)
12. Wire `IStorageProvider` into DI as singleton; CLI and Server share same registration
13. Add server endpoints: `GET /v1/audit` (query events), `GET /v1/sessions?query=...` (full-text search)
14. Import tool: `StorageMigrator` auto-imports existing flat files on first run
15. Tests: migration idempotency, config scope resolution, session CRUD, memory CRUD + type filtering, concurrent write safety, JSONL import, credential round-trip, CLI + Server both resolve same provider

---

### Phase 33 — CLI Quick Wins ✅

**Depends on:** None (CLI presentation only)
**Difficulty:** Low–Medium

**Goal:** Foundational UX improvements that make the CLI feel polished and professional on first launch. All changes are CLI presentation and interaction only — no backend, server, agent, or runtime changes.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | ASCII art home screen | Startup banner with "Sovrant" ASCII art, version number, and tagline. Displayed once on launch. Style modeled on Fastfetch/Angular CLI. |
| 2 | Graceful env var errors | If `LLM_API_KEY` is missing, show a friendly message naming the variable, explaining its purpose, and showing the exact `export` command. Exit cleanly — no stack trace, no blank screen. |
| 3 | Thinking messages | Randomly rotating warm teal phrases ("Thinking really hard...", "Consulting the oracle...", etc.) that cycle every 2–3 seconds while the model processes. Vanish the instant the first token arrives. Uses Spectre.Console. |
| 4 | Escape to cancel | Detect Escape during generation, cancel the request mid-stream, clean up the display, and print `[Cancelled]`. Show a persistent `Esc to cancel` hint at the prompt in muted text. |
| 5 | Sticky input bar | Input prompt pinned to the bottom of the terminal at all times. Output scrolls above it. Bar includes a horizontal rule, `You:` label, text input, and `Esc to cancel` hint. Redraws correctly on terminal resize. Uses Spectre.Console live rendering. |
| 6 | Paste acknowledgment | Detect multi-line paste as a single operation, show `[Pasted N lines]`, hold content until Enter is pressed. No per-line echo. |
| 7 | /help coverage audit | Audit all slash commands and ensure every one appears in `/help` output with a short description. Full visual formatting deferred to Phase 34. |

#### Acceptance Criteria

- `dotnet build` exits 0, no new warnings
- ASCII banner on fresh launch, no repeat mid-session
- Missing `LLM_API_KEY` → readable error + clean exit
- Thinking phrases rotate in teal, vanish on first token
- Escape cancels generation cleanly, prints `[Cancelled]`
- Input bar pinned to bottom, redraws on resize
- Multi-line paste → `[Pasted N lines]`, submits only on Enter
- Every slash command appears in `/help`

---

### Phase 34 — CLI Visual Polish ✅

**Depends on:** Phase 33 (CLI quick wins — input bar, /help audit, thinking messages must be in place)
**Difficulty:** Low–Medium

**Goal:** Consistent visual language across all CLI output. Builds on Phase 33's interaction changes with color, spacing, and formatting polish. Includes two small functional fixes (web search, Windows write perms) that are CLI-scoped.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Output coloring | Apply consistent Spectre.Console coloring: bold headings, muted code blocks, distinct tool call names, dimmer tool results, red errors, amber warnings, gray system messages. No plain-white-on-black content remaining. |
| 2 | Conversation visual separation | Clear rhythm between turns: blank line before user turn, bold colored `You:` label, blank line before assistant, teal bold `Sovrant:` label, subtle separator after each response. Scannable at a glance. |
| 3 | /help formatting | Group commands by category (Session, Memory, Config, Navigation). Accent-colored command names, aligned descriptions, bold category headers. Builds on Phase 33's coverage audit. |
| 4 | Web search indicator | Fix `LLM_WEB_SEARCH=true` pass-through to the provider layer. Add `[web search on]` status badge at the prompt. Warn if the provider doesn't support it. |
| 5 | Windows write permissions | Detect access/permission errors on file writes (WriteFile, EditFile, session/memory files). Display a clear message instructing the user to restart as Administrator. Never fail silently. |

#### Acceptance Criteria

- `dotnet build` exits 0, no new warnings
- All output types (headings, code, tool calls, errors, warnings, system) are distinctly colored
- User/assistant turns visually separated with labels and spacing
- `/help` grouped by category with aligned Spectre.Console columns
- `LLM_WEB_SEARCH=true` → visible badge, search works end-to-end
- Windows file write failure → readable permissions error with restart instructions

---

### Phase 35 — Workspaces ✅

**Depends on:** Phase 32 (SQLite persistence — `users` table already exists), Phase 27 (memory system)

**Goal:** Personal and team areas that house projects. Every user gets an isolated personal workspace by default; team workspaces allow groups to collaborate. Workspaces own their own memory, configuration, sessions, credentials, and audit data.

#### Core concepts

- **Personal workspace** — auto-created when a user is created. Single-owner, cannot be deleted, always exists. This is where a user's solo work lives.
- **Team workspace** — created explicitly. Has membership (owner/admin/member/viewer) and invite-based onboarding. This is where teams collaborate.
- **Fallback rule** — if no `workspace_id` is provided in a request, resolve to the authenticated user's personal workspace. No request is ever "unscoped."
- **Isolation** — workspaces cannot see each other's data. Sessions, config, memory, credentials, audit — all scoped per-workspace.
- **Memory** — Phase 27's memory layers (SessionSummary, LearnedPattern, Instinct) are scoped per-workspace. A team workspace accumulates shared learned patterns; a personal workspace keeps individual ones. Memory does not leak across workspace boundaries.

#### Motivation

Today Sovrant is single-user (the `users` table from Phase 32 seeds one user). There's no separation between "my stuff" and "our team's stuff." Workspaces give every user a private area from day one (personal workspace) and let teams form shared areas when needed (team workspaces):
- A user's personal workspace is their default — solo work, personal memory, personal config
- A team workspace lets multiple users share sessions, memory, config, and projects
- Isolation means workspace A cannot see workspace B's data, memory, or sessions
- Users can belong to multiple team workspaces while always having their personal workspace as home base

#### SQLite schema

| Table | Key columns | Notes |
|---|---|---|
| `workspaces` | `workspace_id` (PK), `type` (personal/team), `name`, `slug` (unique), `owner_id` (FK → users), `created_at`, `updated_at` | Personal workspaces: `type='personal'`, one per user, `owner_id` = user. Team workspaces: `type='team'`, `owner_id` = creator. |
| `workspace_members` | `workspace_id` (FK), `user_id` (FK), `role` (owner/admin/member/viewer), `joined_at` | Personal workspaces have exactly one member (the owner). Team workspaces have many. |
| `workspace_config` | `workspace_id` (FK), `key`, `value` | Workspace-scoped settings (model defaults, governance level, budget caps) |
| `workspace_invites` | `invite_id` (PK), `workspace_id` (FK), `email`, `role`, `token`, `expires_at`, `accepted_at` | Team workspaces only — personal workspaces don't accept invites |
| `workspace_memory` | `memory_id` (PK), `workspace_id` (FK), `layer` (summary/pattern/instinct), `content`, `confidence`, `project_id` (FK, nullable), `created_at`, `updated_at` | Replaces flat-file memory storage from Phase 27. Scoped to workspace, optionally to project. |

All existing tables with `user_id` gain a `workspace_id` FK (not nullable — every row belongs to a workspace). Migration backfills existing data into each user's personal workspace.

#### Workspace resolution

1. Request includes `X-Workspace-Id` header → use that workspace (validate membership)
2. Request includes no workspace context → resolve to the authenticated user's personal workspace
3. Never unscoped — the personal workspace is always the fallback

#### API surface

| Endpoint | Method | Description |
|---|---|---|
| `/v1/workspaces` | GET | List workspaces the authenticated user belongs to (always includes personal) |
| `/v1/workspaces` | POST | Create a team workspace (creator becomes owner) |
| `/v1/workspaces/{id}` | GET/PUT | Workspace read/update. Personal workspaces can be renamed but not deleted. |
| `/v1/workspaces/{id}` | DELETE | Delete team workspace only (personal workspaces cannot be deleted) |
| `/v1/workspaces/{id}/members` | GET/POST/DELETE | Membership management (team workspaces only) |
| `/v1/workspaces/{id}/invites` | POST/DELETE | Invite lifecycle (team workspaces only) |
| `/v1/workspaces/{id}/config` | GET/PUT | Workspace-scoped configuration |
| `/v1/workspaces/{id}/usage` | GET | Aggregated token/cost usage for workspace |
| `/v1/workspaces/{id}/memory` | GET | Workspace memory (summaries, patterns, instincts) |

#### Implementation plan

1. Add SQLite tables (`workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `workspace_memory`)
2. Add `workspace_id` FK (not nullable) to `sessions`, `audit_events`, `credentials`, `usage`, `config` tables
3. Auto-create personal workspace on user creation (triggered when a new user row is inserted)
4. Migration: backfill existing data into each user's personal workspace
5. Implement `IWorkspaceService` — CRUD, membership, invite token generation/validation, personal workspace creation
6. Implement `WorkspaceContextMiddleware` — resolves workspace from `X-Workspace-Id` header, falls back to personal workspace
7. Migrate Phase 27 `IMemoryStore` from flat files to `workspace_memory` table — `FileMemoryStore` becomes fallback for non-SQLite mode
8. Scope all existing queries by `workspace_id` (no unscoped queries remain)
9. Add workspace API endpoints
10. Workspace-scoped config inheritance: workspace config → user config → global defaults
11. Tests: personal workspace auto-creation, fallback resolution, isolation (workspace A can't see workspace B data), memory scoping, membership roles, invite flow (team only), personal workspace delete protection

#### Relationship to Phase 40 (Enterprise Auth)

Phase 40 adds external IdP login and RBAC on top of the workspace model — workspaces become the RBAC scope boundary.

---

### Phase 36 — Projects ✅

**Depends on:** Phase 35 (workspaces)

**Goal:** Isolated containers within workspaces that group related work — sessions, config, memory, agent templates, and artifacts. A project belongs to exactly one workspace and inherits its isolation boundary.

#### Core concepts

- **Project** — a named container within a workspace (like a repo, initiative, or client engagement)
- **Workspace-scoped** — projects belong to a workspace. Personal workspace projects are solo; team workspace projects are collaborative.
- **Memory inheritance** — projects can accumulate their own learned patterns and instincts (stored in `workspace_memory` with `project_id` set). Project memory is a refinement of workspace memory, not a replacement — both layers are visible within the project.
- **Membership** — project members are a subset of workspace members. If no project members are explicitly set, all workspace members have access (open by default within the workspace).

#### Motivation

Workspaces isolate people. Projects isolate work. Without projects:
- All sessions in a workspace are in one flat list
- Config overrides apply workspace-wide or per-session — no middle ground for "this initiative uses model X with budget Y"
- Memory accumulates at the workspace level with no way to separate patterns learned on different initiatives
- Agent templates and skills can't be scoped to a specific effort

Projects let a team (or individual) say "this engagement has its own model config, budget, memory, and agent templates" without affecting other work in the same workspace.

#### SQLite schema

| Table | Key columns | Notes |
|---|---|---|
| `projects` | `project_id` (PK), `workspace_id` (FK), `name`, `slug`, `description`, `created_at`, `updated_at`, `archived_at` | Belongs to exactly one workspace |
| `project_members` | `project_id` (FK), `user_id` (FK), `role` (lead/contributor/viewer), `joined_at` | Subset of workspace members; optional — no rows means all workspace members have access |
| `project_config` | `project_id` (FK), `key`, `value` | Project-scoped settings (model, budget, governance overrides) |

Phase 35's `workspace_memory` table already has an optional `project_id` FK — project-scoped memory writes set this field. Existing tables gain optional `project_id` FK: `sessions`, `audit_events`, `usage`.

#### Config and memory inheritance

```
project config  →  workspace config  →  user config  →  global defaults
project memory  +  workspace memory  (both visible within project context)
```

#### API surface

| Endpoint | Method | Description |
|---|---|---|
| `/v1/workspaces/{wid}/projects` | GET/POST | List/create projects in workspace |
| `/v1/projects/{id}` | GET/PUT/DELETE | Project CRUD (delete archives by default) |
| `/v1/projects/{id}/members` | GET/POST/DELETE | Project membership (subset of workspace) |
| `/v1/projects/{id}/config` | GET/PUT | Project-scoped configuration |
| `/v1/projects/{id}/sessions` | GET | Sessions scoped to project |
| `/v1/projects/{id}/usage` | GET | Token/cost usage for project |
| `/v1/projects/{id}/memory` | GET | Project-scoped memory (project + inherited workspace memory) |

#### Implementation plan

1. Add SQLite tables (`projects`, `project_members`, `project_config`)
2. Add optional `project_id` FK to `sessions`, `audit_events`, `usage` tables
3. Implement `IProjectService` — CRUD, membership, archival
4. Implement `ProjectContextMiddleware` — resolves project from `X-Project-Id` header
5. Config inheritance chain: project config → workspace config → user config → global defaults
6. Memory scoping: project memory writes go to `workspace_memory` with `project_id` set; reads merge project + workspace layers
7. Session creation auto-associates with active project context
8. Project-scoped agent templates and skills (load from project config in addition to workspace/global)
9. Budget enforcement at project level (Phase 39's `ICostModel` + project config budget cap)
10. Tests: project isolation within workspace, config inheritance chain, memory scoping (project sees own + workspace memory), membership subset validation, archive behavior

#### Relationship to Phase 41 (Artifact System)

If Phase 41 is implemented, artifacts are scoped to projects rather than just teams. The `ITeamWorkspace` from Phase 41 becomes project-aware — artifacts persist in the project context and survive across team agent lifetimes.

---

### Phase 37 — User Management API ✅

**Depends on:** Phase 32 (persistence layer — `users` table)
**Difficulty:** Low–Medium

**Goal:** Expose CRUD endpoints for user management so that frontends can create users, manage profiles, and view per-user data. Phase 32 creates the `users` table and `user_id` foreign keys on every other table. This phase builds the API surface to manage those users.

This is **not** auth — there are no per-user tokens, no login, no RBAC. The existing `SOVRANT_TOKEN` gate remains the only auth mechanism. Per-user bearer tokens and token resolution are deferred to Phase 38 (per-user token auth). Role-based access control is deferred to Phase 40 (enterprise auth).

#### Why this matters

After Phase 32, the database has per-user scoping on sessions, audit, credentials, and usage. But there's no way to create or manage users through the API — they're auto-seeded from the OS username. A frontend building a multi-user dashboard needs to:
- Create and list users
- View per-user usage, sessions, and audit history
- Deactivate users without deleting their data
- Assign users to teams for organizational grouping

#### Endpoints

All endpoints are protected by the existing `SOVRANT_TOKEN` bearer auth (same as all other endpoints).

**User CRUD**

| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/users` | Create a user — returns `user_id` |
| `GET` | `/v1/users` | List all users (filterable by status/team/role) |
| `GET` | `/v1/users/{id}` | Get user profile, session count, total tokens consumed |
| `PUT` | `/v1/users/{id}` | Update display name, role, team, status |
| `DELETE` | `/v1/users/{id}` | Soft-delete (deactivate) — sets `status: inactive`, preserves data for audit |

**Per-User Data Views**

| Method | Path | Description |
|---|---|---|
| `GET` | `/v1/users/{id}/sessions` | List sessions owned by this user |
| `GET` | `/v1/users/{id}/usage` | Token usage summary for this user (by model, by date range) |
| `GET` | `/v1/users/{id}/audit` | Audit events for this user |

#### Request/Response Shapes

**`POST /v1/users`**
```json
// Request
{ "display_name": "Eric", "role": "user", "team": "engineering" }

// Response (201 Created)
{
  "user_id": "usr_a1b2c3d4",
  "display_name": "Eric",
  "role": "user",
  "team": "engineering",
  "status": "active",
  "created_at": "2026-04-06T..."
}
```

**`GET /v1/users`**
```json
{
  "users": [
    {
      "user_id": "usr_a1b2c3d4",
      "display_name": "Eric",
      "role": "user",
      "team": "engineering",
      "status": "active",
      "session_count": 47,
      "total_input_tokens": 1250000,
      "total_output_tokens": 380000,
      "created_at": "2026-04-01T...",
      "last_seen_at": "2026-04-05T..."
    }
  ],
  "count": 1,
  "total": 1
}
```

#### Schema notes

No schema changes needed — Phase 32 already created the `users` table with `role`, `team`, and `status` columns. This phase only adds the API surface.

#### Implementation Plan

1. Implement `IUserService` — CRUD operations backed by `ISqliteConnectionFactory`
2. Add `UserRoutes` route group: `/v1/users` CRUD
3. Add per-user data view routes: `/v1/users/{id}/sessions`, `/v1/users/{id}/usage`, `/v1/users/{id}/audit`
4. Update `docs/server.md` with new endpoints
5. Tests: user CRUD, soft-delete preserves data, per-user data views filter correctly
6. **Update the Frontend SDK** — extend the TypeScript client to cover the new `/v1/users/*` endpoints (and the Phase 35 `/v1/workspaces/*` and Phase 36 `/v1/workspaces/{wid}/projects/*` surfaces if not already wired). Bump the SDK version, regenerate types, and publish so frontends/apps can consume users, workspaces, and projects through a single typed client.

#### What this does NOT include (deferred to Phase 38)

- Per-user bearer tokens and `api_tokens` table population
- Token hash-based user resolution middleware
- Admin vs user role enforcement
- `SOVRANT_ADMIN_TOKENS` environment variable
- Per-user auth scoping (any authenticated request can access any user's data)
- Database hardening (secure_delete, file permissions, read-only connections)

---

### Phase 37.5 — Swarm Sessions Into the Database ✅

**Status:** Complete (2026-04-08)
**Depends on:** Phase 32 (SQLite persistence — `swarm_events` table exists from V005)
**Difficulty:** Small–Medium

**What shipped:**
- `ISwarmEventStore` + `SqliteSwarmEventStore` against the existing `swarm_events` table — no schema migration was needed (V005 columns + V006/V007 indexes were already in place).
- `SwarmSession` rewritten to write through the new store; public surface (`RecordAsync` / `ReplayAsync` / `ListSessions` / `Exists`) preserved.
- `SwarmExecutionContext(UserId, WorkspaceId, ProjectId)` threaded through `SwarmOrchestrator.ExecuteAsync` so every event is stamped with scope. `SwarmRoutes` populates it from `WorkspaceContextMiddleware`'s `HttpContext.Items` plus the `X-Project-Id` header.
- `GET /v1/swarm/sessions` accepts `workspace_id`, `project_id`, `limit` query parameters (handed to `SwarmListFilter`).
- `sovrant db import-swarm [--dir <path>] [--delete-source]` migrates legacy `~/.sovrant/swarm/sessions/*.jsonl` files into `swarm_events` for existing installs.
- New tests: `SqliteSwarmEventStoreTests` (12 cases) plus 2 workspace-scoping tests added to `SwarmSessionTests`. Full suite green.
- `persistence.md` updated (new Swarm Event Store subsection, swarm row dropped from "Stays as Files", concern #7 retired).


**Goal:** Replace the JSONL-only swarm session store (`SwarmSession` writing `.sovrant/swarm/sessions/{id}.jsonl`) with a SQLite-backed `ISwarmEventStore` that writes to the existing `swarm_events` table. Promotes item #14 from Phase 42.5's deferred list to a discrete phase so swarm runs become queryable, joinable to users/workspaces, and covered by the same backup/migration story as everything else.

#### Why this matters

Today swarm is the **only** subsystem that still treats flat files as the source of truth. The `swarm_events` table was created in V005 and has sat empty ever since. As a result:

- Swarm runs are invisible to per-user / per-workspace queries (`/v1/users/{id}/sessions` won't find them).
- They are not covered by the SQLite backup story — only by ad-hoc filesystem copies of `~/.sovrant/swarm/sessions/`.
- The directory grows unbounded with no rotation.
- There is no foreign-key linkage to `users`, `workspaces`, or `sessions`, so cross-cutting analytics (cost per swarm, swarm-vs-direct usage breakdown) require ad-hoc JSONL parsing.
- The audit trail for governance events triggered inside swarm sub-agents is split across two stores.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Define `ISwarmEventStore` | New interface in `Sovrant.Runtime/Storage` with `RecordEventAsync(SwarmEvent)`, `LoadEventsAsync(swarmId)`, `ListSwarmsAsync(filter)`, `DeleteSwarmAsync(swarmId)`. |
| 2 | Implement `SqliteSwarmEventStore` | Backed by the existing `swarm_events` table. Stores `swarm_id`, `event_type`, `agent_id`, `workspace_id`, `project_id`, `payload` (JSON), `timestamp`. Reuses `ISqliteConnectionFactory`. |
| 3 | Refactor `SwarmSession` | Take `ISwarmEventStore` via constructor injection. Replace `File.AppendAllText(...)` writes with `_store.RecordEventAsync(...)`. Replace directory globbing in `LoadAsync` with a SQL query keyed on `swarm_id`. |
| 4 | DI registration | Add `services.AddSingleton<ISwarmEventStore, SqliteSwarmEventStore>()` in `Sovrant.Runtime.ServiceCollectionExtensions`. |
| 5 | Workspace + project scoping | When `SwarmOrchestrator` starts a swarm, capture the active `workspace_id` / `project_id` from the request context and stamp every event with it. This is what enables the per-workspace and per-project queries. |
| 6 | One-shot JSONL importer | `sovrant db import-swarm` (or a one-time migration helper) that reads any existing `~/.sovrant/swarm/sessions/*.jsonl` files and inserts the events into `swarm_events`, then optionally deletes the source files with `--delete-source`. |
| 7 | Update `/v1/swarms` routes | Existing `SwarmRoutes` reads from the JSONL store. Switch them to query through `ISwarmEventStore`. Add workspace/project filters to the list endpoint. |
| 8 | Tests | `SqliteSwarmEventStoreTests` (CRUD, filters, workspace scoping), `SwarmSessionTests` against the new store, and an integration test that runs a real swarm and verifies the events land in `swarm_events`. |
| 9 | Update persistence.md | Move swarm out of "What Stays as Files", add it to the Domain Stores section, and remove items #14 (and partial #13) from the Phase 42.5 known-concerns table. |
| 10 | Update server.md | Document the new query/filter capabilities on `/v1/swarms`. |

#### Acceptance Criteria

- `swarm_events` table is the canonical store for all new swarm runs after upgrade.
- `~/.sovrant/swarm/sessions/` is no longer written to by default; opt-in JSONL export via a flag if needed.
- A swarm started inside a workspace is queryable via `/v1/workspaces/{wid}/swarms`.
- A one-shot importer migrates existing JSONL session files into the DB.
- All existing swarm tests pass against the new store; new tests cover workspace scoping.
- `docs/persistence.md` no longer lists swarm under "What Stays as Files".

#### What this does NOT include

- Backfilling `user_id` onto historical swarm events (the JSONL files don't carry it).
- Per-event session linkage to `sessions` / `session_entries` — that's a future enhancement (would let you join a swarm event to its parent agent's full chat history).
- Live progress streaming via Server-Sent Events — `SwarmRoutes` already has SSE; this phase preserves it but doesn't expand it.

---

### Phase 38 — Per-User Token Auth & Database Hardening

**Depends on:** Phase 37 (user management API — users exist in the DB)
**Difficulty:** Medium

**Goal:** Replace the single shared `SOVRANT_TOKEN` with per-user bearer tokens so the frontend can authenticate individual users. Each user gets their own tokens stored as SHA-256 hashes in the `api_tokens` table (created empty in Phase 32). Also hardens the SQLite layer now that per-user identity is enforceable.

This is the bridge between "everyone shares one token" (Phase 8) and "enterprise SSO with RBAC" (Phase 40). It gives the frontend what it needs — user-specific auth — without the complexity of OAuth/OIDC.

#### Why this matters

After Phase 37, the server can CRUD users but still authenticates everyone with a single shared token. The frontend can't tell which user is making a request. This phase resolves that:
- Each user gets their own bearer tokens
- The server resolves incoming tokens to a `user_id`
- Session, audit, and usage data is scoped to the authenticated user
- Admin users can manage other users; regular users can only access their own data

#### Token Model

| Item | Description |
|---|---|
| Token format | `svt_` prefix + 32 bytes crypto-random (base64url) |
| Storage | Only the SHA-256 hash is stored in `api_tokens`. Plaintext is returned once on creation, never retrievable again. |
| Resolution | `BearerTokenMiddleware` hashes the incoming token, looks up `api_tokens` by `token_hash`, resolves `user_id`, checks `status: active` and `revoked_at IS NULL`. Attaches `user_id` and `role` to `HttpContext.Items`. |
| Admin check | User management endpoints require `role = 'admin'`. Non-admin users can only access their own data. |
| Backward compat | `SOVRANT_TOKEN` (single shared token) still works — mapped to a built-in `admin` user on first run. Existing deployments don't break. |
| Security | Tokens never appear in logs, audit events, error responses, or session data. |

#### Token Management Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/users/{id}/tokens` | Issue a new API token for this user (returns token once, never again) |
| `GET` | `/v1/users/{id}/tokens` | List active tokens (masked — shows prefix + last 4 chars only) |
| `DELETE` | `/v1/users/{id}/tokens/{token_id}` | Revoke a specific token |

#### Database Hardening

Applied in this phase because per-user identity makes ownership enforcement meaningful:

| Hardening | Description |
|---|---|
| `secure_delete=ON` pragma | Zeros deleted rows on disk instead of leaving residual data. No write performance impact in WAL mode. |
| File permissions on creation | `chmod 600` on Linux/macOS, restricted ACL on Windows for `sovrant.db` and `.keystore`. |
| Connection-scoped read-only mode | Read-only endpoints use a read-only SQLite connection. |
| Token hash timing-safe comparison | `CryptographicOperations.FixedTimeEquals` prevents timing attacks on token lookups. |
| Session ownership enforcement | All session/audit/usage queries filter by authenticated `user_id`. Admin role bypasses for management endpoints only. |

#### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_ADMIN_TOKENS` | — | Comma-separated admin bearer tokens (bootstrap — creates admin users on first seen) |
| `SOVRANT_TOKEN` | — | Legacy single-token mode (mapped to built-in admin user for backward compat) |

#### Implementation Plan

1. Implement `ITokenService` — token generation (`svt_` + crypto-random), SHA-256 hashing, issuance, revocation
2. Implement `ITokenResolver` — hash-based token-to-user resolution middleware, replaces boolean `SOVRANT_TOKEN` check
3. Add token management endpoints (`/v1/users/{id}/tokens`)
4. Add admin authorization middleware — `role = 'admin'` for user/token management; self-access for regular users
5. Scope all data view queries by authenticated `user_id` (sessions, audit, usage)
6. Backward compat: `SOVRANT_TOKEN` auto-creates a built-in admin user + token on first run
7. Apply database hardening (secure_delete pragma, file permissions, read-only connections, timing-safe comparison)
8. Update `docs/server.md` with token endpoints and auth model
9. Tests: token issuance/revocation, hash-based resolution, admin-only access, self-read, ownership enforcement, timing-safe comparison, backward compat with `SOVRANT_TOKEN`

---

### Phase 39 — Cost Tracking, Token Budgets & Model Pricing Registry ⏸️ Deferred (nice-to-have)

**Depends on:** Phase 10 (token usage tracking — already complete)

**Goal:** End-to-end cost management — per-session/per-project token tracking with JSONL metrics log, budget enforcement, a `/cost` dashboard, and a multi-source pricing registry that maps model names to USD-per-token rates. Merges the former Phase 26 (cost tracking) with model pricing so the full cost pipeline ships as one coherent feature.

#### What exists today

Phase 10 already tracks `TotalInputTokens` and `TotalOutputTokens` per session in `SessionConfig`, and `GET /v1/usage` returns per-session token summaries. This phase adds budget limits, persistent metrics logging, a cost dashboard, and USD estimation via a layered pricing registry.

#### Components

| Component | What it does |
|---|---|
| **`ICostModel`** | Interface: `decimal? EstimateCost(string model, long inputTokens, long outputTokens)` — layered pricing lookup |
| **Metrics Logger** | Appends per-turn token/cost events to `~/.sovrant/metrics/cost.jsonl` |
| **Budget Enforcer** | Optional per-session or per-project budget cap; warns at 80%, blocks at 100% |
| **Cost Dashboard** | `GET /v1/cost` endpoint and `/cost` CLI command — daily/weekly/monthly breakdown |
| **Pricing Registry** | Multi-source, user-overridable model-to-price mapping |

#### Why pricing is non-trivial

- Vendors change prices without notice — any static table is immediately a liability
- Provider-agnostic design means Sovrant must price models from OpenAI, Anthropic, Google, Mistral, Ollama (free), Azure (different pricing than direct), and arbitrary OpenAI-compatible endpoints
- Model name aliasing: `gpt-4o-2024-08-06` vs `gpt-4o`, `claude-sonnet-4-6` vs `claude-sonnet-4-6-20250514`
- Some deployments are free (Ollama, self-hosted) or have custom enterprise pricing
- Cache tokens, batch API, and prompt caching have different rates

#### Design space (needs resolution before implementation)

| Approach | Pros | Cons |
|---|---|---|
| **User-editable local config** (`~/.sovrant/cost-models.json`) | Simple, no network, user controls everything | User must manually track vendor changes |
| **Bundled defaults + user overrides** | Works offline, good defaults, overridable | Stale between releases |
| **Remote pricing URL** (fetch on startup, cache locally) | Always current if maintained | Network dependency, who hosts it? |
| **Provider API introspection** | Authoritative | Most providers don't expose pricing via API |
| **Community-maintained registry** (e.g., GitHub-hosted JSON) | Crowdsourced freshness | Depends on external contributors |

#### Recommended architecture (layered, highest priority wins)

1. **User overrides** — `~/.sovrant/cost-models.json` (always wins)
2. **Project overrides** — `.sovrant/cost-models.json` (per-project custom pricing)
3. **Remote registry** (optional) — URL in config, fetched periodically, cached to disk
4. **Bundled defaults** — ships with each release, covers major models at time of build

#### Open questions

- Should the remote registry be opt-in or opt-out?
- What schema handles cache token pricing, batch discounts, and per-region Azure pricing?
- Should `ICostModel` expose a confidence level (exact vs estimated vs unknown)?
- How to handle model name normalization (fuzzy match? alias table?)
- Is there value in a `sovrant update-pricing` CLI command that fetches latest?

#### Implementation Plan

1. Define `ICostModel` interface in `Sovrant.Runtime/Metrics/`
2. Define `CostModelEntry` record: `InputPer1KTokens`, `OutputPer1KTokens`, optional `CacheReadPer1KTokens`, `CacheWritePer1KTokens`
3. Implement `LayeredCostModel : ICostModel` — walks the 4-tier pricing chain
4. `CostModelFileLoader` — reads/merges JSON from user → project → bundled paths
5. Optional `RemoteCostModelFetcher` — periodic background refresh with local disk cache
6. Model name normalization — alias map or prefix matching
7. Ship bundled `cost-models.json` with current pricing for top ~20 models
8. Add `CostMetricsLogger` — appends to `~/.sovrant/metrics/cost.jsonl` per turn
9. Add `BudgetEnforcer` — reads `SOVRANT_SESSION_BUDGET_USD` and `SOVRANT_PROJECT_BUDGET_USD`
10. Add `GET /v1/cost` endpoint with daily/weekly/monthly aggregation
11. Update `/cost` CLI command to show token counts + estimated spend
12. Wire cost logging into `TurnComplete` event handling
13. `sovrant update-pricing` CLI command (if remote registry enabled)
14. Tests: layering precedence, alias resolution, metrics logging, budget enforcement, JSONL format, offline fallback

---

### Phase 40 — Enterprise Auth & Multi-Tenancy ⏸️ Deferred

**Depends on:** Phase 38 (per-user token auth), Phase 35 (workspaces — provides tenant boundary)

**Goal:** Add external identity providers (OAuth/OIDC, SAML), fine-grained role-based access control (RBAC), and enterprise multi-tenancy on top of the Phase 38 per-user token model. This is where the RBAC tables (created empty in Phase 32) get populated and enforced.

#### When to implement

This phase is deliberately deferred. Phase 38's per-user tokens cover small-to-medium teams. Add this phase when:
- External identity providers (Google, GitHub, Azure AD, Okta) are required for login, **or**
- Fine-grained permissions beyond admin/user/readonly are needed (e.g., "can use tool X but not Y"), **or**
- Organizational boundaries require tenant isolation (separate data, separate billing)

#### What it adds on top of Phase 38 (Per-User Tokens)

Phase 38 gives each user their own bearer tokens with admin/user role enforcement. This phase upgrades that model with external identity, granular permissions, and compliance tooling.

| Item | Change |
|---|---|
| External IdP | OAuth 2.0 / OIDC integration — login via Google, GitHub, Azure AD, Okta. Maps external identity to `users.user_id`. |
| RBAC | Populate `roles` + `permissions` + `role_permissions` + `user_roles` tables. Define granular permissions: `tools:execute`, `config:write`, `sessions:read-all`, etc. |
| SSO enforcement | Workspace admins can require SSO login — disable token-only access for their workspace. |
| Billing isolation | Per-workspace token usage aggregation already exists (Phase 35); this adds billing plan association and usage alerts. |
| Audit | All auth events (login, token issue, token revoke, permission change) logged to `audit_events`. |

#### Implementation Plan

1. Add OAuth/OIDC middleware — `Microsoft.AspNetCore.Authentication.OpenIdConnect`
2. Populate `roles`, `permissions` tables (replace simple role columns in `workspace_members`/`project_members`)
3. Implement `IRbacService` — permission checks at endpoint and tool-execution level
4. Update `BearerTokenMiddleware` to also accept JWT from external IdP
5. Add SSO enforcement flag on workspace config
6. Add `POST /v1/admin/reload` to hot-reload token registry without restart
7. Tests: OAuth flow, RBAC permission checks, SSO enforcement, JWT + svt_ token coexistence

---

### Phase 41 — Artifact System ⏸️ Deferred

**Depends on:** Phase 19+20 (multi-agent team tools)

**Goal:** Give team agents a structured way to share work products — code files, plans, review notes, intermediate results — through a versioned artifact store rather than passing everything through prompt text.

#### Motivation

Today, team agents communicate solely through prompt/response text via `TeamDelegate`. This works for simple tasks but breaks down when agents need to iterate on shared outputs — a planner writes a plan, a coder implements it, a reviewer annotates it. Passing multi-kilobyte code blocks back and forth through prompts wastes tokens, loses formatting, and has no versioning. An artifact store gives agents a shared workspace with named, versioned content blobs that persist across delegations.

#### Design

| Component | Description |
|---|---|
| `IArtifact` | Versioned content blob — `Name`, `Version`, `ContentType` (text, code, json), `ReadAsync()`, `WriteAsync()` |
| `ITeamWorkspace` | Per-team artifact store — `GetAsync(name)`, `PutAsync(name, content)`, `ListAsync()`, `DeleteAsync(name)` |
| `InMemoryTeamWorkspace` | `ConcurrentDictionary<string, IArtifact>` implementation for in-process teams |
| `FileBackedTeamWorkspace` | Persists artifacts to `~/.sovrant/workspaces/{team_id}/` for durability across sessions |

#### How agents use it

- `TeamCreate` with `workspace: true` creates a shared `ITeamWorkspace` for that team
- Agents read/write artifacts via two new tools: `ArtifactRead(name)` and `ArtifactWrite(name, content)`
- The supervisor can inspect workspace contents via `ArtifactList()` or `TeamStatus` (which would include artifact summaries)
- Artifacts are scoped to the team — cleaned up when the team is deleted

#### Implementation Plan

1. Define `IArtifact` and `ITeamWorkspace` interfaces (replace the deleted V2 placeholders)
2. Implement `InMemoryTeamWorkspace` — `ConcurrentDictionary` backed, version counter per artifact
3. Implement `FileBackedTeamWorkspace` — file-per-artifact with `.version` metadata
4. Add `ArtifactRead`, `ArtifactWrite`, `ArtifactList` tools to `Sovrant.Tools/Team/`
5. Wire workspace creation into `TeamCreateTool` when `workspace` param is set
6. Add workspace cleanup to `TeamDeleteTool`
7. Tests: workspace CRUD, versioning, concurrent access, cleanup on team delete

---

### Phase 42 — IDE Extension (VS Code) ⏸️ Deferred (nice-to-have)

**Competitor precedent:** Claude Code ✅ · opencode ✅ (beta)
**Depends on:** Phase 15 (MCP server mode) — once Sovrant exposes an MCP server, MCP-aware IDEs (VS Code with GitHub Copilot, Cursor, Windsurf) can connect without a bespoke extension.
**Status:** Deferred. MCP-based IDE integration (Phase 15) covers the core use case. A native extension adds polish but is not required for the core product.

**Goal:** Embed Sovrant into VS Code as a sidebar panel — chat interface, inline diff approval, tool event rendering, permission dialogs with file highlighting.

#### Architecture

Two-layer approach:
1. **Phase 15 (MCP):** Zero-code IDE integration for MCP-aware clients. Sovrant appears as an MCP tool server. No extension required.
2. **Phase 42 (native extension):** A dedicated VS Code extension that connects to `Sovrant.Server` via HTTP/SSE for richer UX — inline diffs, file decorations, permission dialogs anchored to the relevant file.

#### Implementation Plan

1. Publish `Sovrant.Server` as a local background service (`sovrant serve` command) with auto-start on VS Code activation
2. Implement the VS Code extension (`vscode-sovrant`) — TypeScript, connects to `Sovrant.Server` via the Phase 14 frontend SDK
3. Sidebar: chat panel backed by `useChat()` hook
4. Inline diffs: intercept `Edit`/`Write` tool events, show diff decoration in the editor
5. Permission dialogs: VS Code `window.showInformationMessage` with approve/deny buttons
6. Publish to VS Code Marketplace

---

### Phase 42.5 — Database Lifecycle: Setup, Upgrade Safety & Introspection

**Depends on:** Phase 32 (SQLite persistence), Phases 35–37 (workspaces, projects, users)
**Difficulty:** Medium

**Goal:** Make the SQLite layer a first-class, observable, recoverable system. Today the database is created and migrated transparently on first boot — which is great for new users, but failures are silent, there is no introspection, multiple persistence stores still write parallel JSONL files, and we have no automated proof that an old DB upgrades cleanly. This phase closes those gaps.

**Why this matters**

Today every test starts from a fresh empty SQLite file. The migration runner is correctly designed to be additive (`CREATE TABLE`, `CREATE INDEX IF NOT EXISTS`) and skips already-applied versions, so in principle an old DB upgrades in place on next boot — but we have **no automated proof**, no docs telling users this, and no recovery story when something goes wrong. Users hitting "I have an old DB and the new server crashes" today have no guidance other than "delete the file".

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Old-DB upgrade tests | Add tests that build a DB at schema version N (e.g. by applying only V001–V005), insert representative rows, then bring the DB up to current and assert all data survives and new tables/indexes exist. One test per supported upgrade path. |
| 2 | Idempotent re-seed verification | Test that `SeedDefaultUser` + `SeedPersonalWorkspace` correctly add a personal workspace to a pre-Phase-35 user row without duplicating or breaking the existing user. |
| 3 | Migration checksum drift detection | The runner already records a SHA-256 of each migration; surface a clear error (not a silent skip) if a previously-applied migration's checksum no longer matches the embedded resource — this catches "someone edited an old V005__*.sql in-place". |
| 4 | Backup-before-migrate flag | Optional `SOVRANT_DB_BACKUP_ON_UPGRADE=true` that copies `sovrant.db` to `sovrant.db.bak-{schema_version}` before running any pending migrations. Off by default; recommended in upgrade docs. |
| 5 | Upgrade docs page | New `docs/db-upgrades.md` covering: where the DB lives, what happens on first boot of a new build, how to take a manual backup, how to roll back (restore the .bak), how to read `schema_version` to find your current version. |
| 6 | Server startup log line | At boot, log `Database schema is at version N (latest=M)`; if N < M, log each migration as it runs. Already partially done — confirm log lines are emitted at INFO level so users see them in normal output. |
| 7 | Hard-failure reporting | If a migration throws, surface the failing version + a one-line "to recover, restore your backup" message instead of a raw `SqliteException` stack trace. |
| 8 | Per-table data-loss audit | One-time review: are there any cases where a future migration might need to drop or rename a column? If so, document the safe-rename pattern (add new col → backfill → switch reads → drop old col across two releases). |
| 9 | `sovrant db` CLI subcommand | Add `sovrant db status` (path, schema version, table sizes), `sovrant db version`, `sovrant db migrate --dry-run`, `sovrant db backup [path]`, `sovrant db inspect <table>`. None of these exist today — users have to query `schema_version` by hand. |
| 10 | First-boot UX | Today the first-boot DB creation is invisible unless you have INFO logging on. Surface a one-line "Created Sovrant database at {path} (schema v{N})" at WARN-or-higher on a fresh install so it's visible by default. |
| 11 | `--db-path` CLI flag | `SOVRANT_DB_PATH` is the only way to override the location and it's undocumented. Add a CLI flag and document both. |
| 12 | Fail-loud option for init failures | Today `SqliteStorageProvider.InitializeAsync` swallows exceptions to a log line ("data will not be persisted") and the server keeps running. Add `SOVRANT_DB_REQUIRE=true` to make startup fail hard if the DB can't be initialized — recommended for production. |
| 13 | Consolidate parallel JSONL persistence | `audit/` and `sessions/` directories still write JSONL files alongside the DB when `SOVRANT_AUDIT_JSONL=true` / `SOVRANT_SESSION_JSONL=true` (`DualWriteAuditStore`, `DualWriteSessionStore`). With SQLite as the source of truth, the JSONL streams should become an opt-in **export**, not a parallel write path. (Swarm's separate JSONL store is now tracked under Phase 37.5.) |
| 14 | ~~Move swarm sessions into the DB~~ | **Promoted to Phase 37.5.** |
| 15 | Empty-string `user_id` cleanup | `sessions`, `token_usage`, and `credentials` all use `user_id TEXT NOT NULL DEFAULT ''`. Rows with `''` won't appear in any user-scoped query (Phase 37's `/v1/users/{id}/sessions` etc.). One-shot migration to backfill `''` rows to the boot identity, plus drop the empty-string default going forward. (Note: V008 already handles the related "orphan workspace_id" backfill — this item is the parallel cleanup for `user_id`.) |
| 16 | Connection pooling | Every store call opens a new `SqliteConnection`. `Cache=Shared` mitigates the cost, but a real pool would help under load — especially on Windows where connection-open is non-trivial. |
| 17 | Health-check endpoint coverage | `/health` should report DB status (path, schema version, last successful query) so monitors can detect a degraded "running but DB broken" state. Currently `/health` only returns `{ status: "ok" }`. |
| 18 | `sovrant init` / reset | For demos/CI/onboarding, a `sovrant init` command that bootstraps a fresh DB at the chosen path (with optional `--reset` to wipe an existing one with confirmation). |

#### Acceptance Criteria

- A test suite that takes a "Phase 32 era" DB through to current and verifies workspaces, projects, and users all coexist with pre-existing rows.
- Documented upgrade procedure in `docs/db-upgrades.md`.
- A failing migration produces a friendly, actionable error.
- `SOVRANT_DB_BACKUP_ON_UPGRADE` works end-to-end.
- `sovrant db status` reports path, schema version, and table sizes.
- `/health` reports DB status (degraded vs ok).
- JSONL parallel writes are gated behind explicit export flags, not on by default.
- Swarm orchestrator sessions live in the `sessions` table.
- All `user_id = ''` rows are backfilled or migrated.

---

### Phase 43 — Windows PowerShell Integration & Platform Shell Strategy

**Depends on:** Phase 34 (CLI visual polish — BashTool already switched to PowerShell on Windows)
**Difficulty:** Medium

**Goal:** Fully leverage PowerShell capabilities on Windows environments. The current BashTool was designed for Unix shells and uses PowerShell on Windows only as a compatibility shim. This phase explores native PowerShell idioms, cmdlet access, and Windows-specific capabilities to make Sovrant a first-class citizen on Windows.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | PowerShell-native command generation | Teach the system prompt / tool descriptions that Windows uses PowerShell, so the LLM generates `New-Item`, `Get-ChildItem`, `Remove-Item` etc. instead of `mkdir`, `ls`, `rm` with Unix flags. |
| 2 | PowerShell module discovery | Detect installed PowerShell modules and surface them as available capabilities (e.g., `Az`, `SqlServer`, `ActiveDirectory`). |
| 3 | Windows API access via cmdlets | Leverage PowerShell's access to .NET types, COM objects, WMI/CIM, and the Windows registry — capabilities unavailable in bash. |
| 4 | Execution policy handling | Robust detection and handling of PowerShell execution policies. Provide clear guidance when scripts are blocked by policy. |
| 5 | PowerShell 7+ vs 5.1 feature matrix | Detect which PowerShell version is available and adjust generated commands accordingly (e.g., `pwsh` supports `ForEach-Object -Parallel`, `ConvertFrom-Json -Depth`). |
| 6 | Credential & secret management | Integrate with PowerShell `SecretManagement` module for secure credential handling on Windows. |
| 7 | Windows-specific file operations | Leverage NTFS features: ACLs (`Get-Acl`/`Set-Acl`), alternate data streams, symbolic links with proper elevation detection. |
| 8 | Admin elevation detection | Detect whether the current session is elevated (Administrator) and adjust tool behavior — warn before operations that require elevation, suggest `Start-Process -Verb RunAs` when needed. |

#### Acceptance Criteria

- LLM generates idiomatic PowerShell on Windows (not bash-translated-to-PS)
- Available PowerShell modules are discoverable by the agent
- Execution policy errors produce actionable guidance, not raw errors
- Admin elevation status is detected and surfaced in tool confirmations
- All existing Unix/macOS behavior is preserved (no regressions on non-Windows)

---

### Phase 44 — Desktop Application (Cross-Platform)

**Depends on:** Phase 32 (SQLite persistence), Phase 34 (CLI visual polish)
**Difficulty:** High

**Goal:** A native desktop application that mirrors the Sovrant web frontend's design and UX, built with Avalonia UI on .NET 10. The app embeds the Sovrant runtime directly in-process for maximum performance and security — no server, no CLI subprocess, no network overhead. The runtime (`ConversationRuntime`, `ToolExecutor`, `SessionStore`, `AgentOrchestrator`, etc.) runs in the same process as the UI, with SQLite persistence, OS credential store for API keys, and the full tool/agent/memory stack available natively. The GUI translates user actions into direct runtime calls and renders `RuntimeEvent` streams as rich UI. Windows is the primary target, with macOS and Linux as a goal.

#### Framework Evaluation

| Framework | Windows | macOS | Linux | Maturity | Notes |
|---|---|---|---|---|---|
| **Avalonia UI** | ✅ | ✅ | ✅ | Stable (11.x) | XAML-based, CSS-like styling, JetBrains uses it. Best Linux story. Single codebase → all 3 platforms. Hot reload, design-time preview. **Recommended.** |
| **Uno Platform** | ✅ | ✅ | ✅ (via Skia/GTK) | Stable (5.x) | WinUI 3 / XAML dialect. Also targets web (WASM) and mobile. Heavier runtime. Good if we want web+desktop from one XAML codebase. |
| **.NET MAUI** | ✅ | ✅ | ❌ | Stable but desktop-weak | Microsoft official. No Linux support. Desktop experience is secondary to mobile. Community frustration with bugs and slow fixes. |
| **Blazor Hybrid** | ✅ | ✅ | ❌ (needs MAUI) | Stable | Renders web UI inside a native WebView. Could reuse the existing React/TypeScript frontend directly. But tied to MAUI for the host shell → no Linux. |
| **Photino** | ✅ | ✅ | ✅ | Early | Lightweight native WebView wrapper. Tiny footprint. Could host the existing web frontend as-is. Less mature, small community. |

**Recommendation: Avalonia UI** — best cross-platform coverage (including Linux), most active community for desktop-focused .NET apps, XAML familiarity for .NET developers, and proven at scale. Uno Platform is a strong alternative if we later want to share XAML between desktop and a WASM web target. Photino is worth revisiting if we decide to simply wrap the existing web frontend in a native shell instead of building native UI.

#### Architecture — Embedded Runtime with GUI Shell

The desktop app references `Sovrant.Runtime`, `Sovrant.Tools`, `Sovrant.Commands`, and `Sovrant.Agents` directly as project references — the same way `Sovrant.Cli` does. The runtime runs in-process: `AddSovrantRuntime()`, `AddSovrantTools()`, `AddMultiAgentSystem()`, and `AddSovrantCommands()` wire up DI exactly as they do in the CLI. No HTTP layer, no server process, no network roundtrip.

```
┌───────────────────────────────────────────────┐
│              Sovrant.Desktop                  │
│         (Avalonia UI — .NET 10)               │
│                                               │
│  ┌─────────────┐  ┌───────────────────────┐   │
│  │  Chat View  │  │  Session Sidebar      │   │
│  │  (streaming │  │  (history, search,    │   │
│  │   tokens)   │  │   resume, export)     │   │
│  ├─────────────┤  ├───────────────────────┤   │
│  │  Tool Panel │  │  Settings / Config    │   │
│  │  (diffs,    │  │  (providers, keys,    │   │
│  │   approvals)│  │   permissions, model) │   │
│  └─────────────┘  └───────────────────────┘   │
│                                               │
│  ┌───────────────────────────────────────────┐│
│  │         Sovrant Runtime (in-process)      ││
│  │  ConversationRuntime · ToolExecutor ·     ││
│  │  SmartRouter · SessionStore (SQLite) ·    ││
│  │  AgentOrchestrator · HookRunner ·         ││
│  │  MemoryManager · MCP Client               ││
│  └───────────────────────────────────────────┘│
└───────────────────────────────────────────────┘
         │ direct .NET calls
         ▼
   LLM Providers (OpenAI, Anthropic, etc.)
```

`IAsyncEnumerable<RuntimeEvent>` streams tokens directly to the UI thread via Avalonia's dispatcher. User actions (send message, approve tool, cancel generation) call `ConversationRuntime` and `SlashCommandDispatcher` directly — zero serialization, zero latency. SQLite sessions, memory, and audit data persist locally. The desktop app is the primary product for end users; the CLI and server remain available for developers and automation.

**Future: Embedded Terminal (Phase 45)**
A later phase adds an integrated terminal panel within the desktop app (similar to VS Code's terminal or Windows Terminal) that can optionally run the Sovrant CLI for power users who want raw terminal access.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Project scaffolding | Create `src/Sovrant.Desktop/` as an Avalonia MVVM app targeting .NET 10. Project-reference `Sovrant.Runtime`, `Sovrant.Tools`, `Sovrant.Commands`, `Sovrant.Agents`. Wire up DI with `AddSovrantRuntime()`, `AddSovrantTools()`, `AddMultiAgentSystem()`, `AddSovrantCommands()` in `App.axaml.cs`. Single-process, no external dependencies. |
| 2 | Design system | Port the web frontend's visual language to Avalonia: color palette (teal accents, dark/light themes), typography, spacing, iconography. Create `Styles/` with reusable control templates and theme resources. |
| 3 | Chat view | Main conversation panel consuming `IAsyncEnumerable<RuntimeEvent>` directly. Markdown rendering, code block syntax highlighting, auto-scroll. `TextChunk` events append to the current message; `TurnComplete` finalizes it. Match the web frontend's message layout and turn separation. |
| 4 | Inline diff view | Render `ToolUseRequested` events for Edit/Write tools as side-by-side or unified diffs with red/green coloring. Inline Allow/Deny buttons wired to `IToolConfirmationHandler`. |
| 5 | Tool confirmation UI | Implement `IToolConfirmationHandler` as an Avalonia dialog — show tool name, parameters, risk level. `RequestConfirmationAsync` opens a modal and returns the user's choice. Native desktop experience for permission management. |
| 6 | Session sidebar | List previous sessions from `ISessionStore` (direct SQLite query, no HTTP). Search, resume, delete, export as Markdown. Show session metadata (date, token count, model, provider). |
| 7 | Settings panel | Provider configuration, API key management via OS credential store (Windows Credential Manager / macOS Keychain / libsecret on Linux), permission mode selection, model picker. Writes to `sovrant.json` config file. |
| 8 | System tray integration | Minimize to system tray, global hotkey to summon, notification badges for long-running agent completions. Windows: NotifyIcon. macOS: NSStatusItem. Linux: StatusNotifierItem/AppIndicator. |
| 9 | Slash command palette | Searchable command palette (Ctrl+K / Cmd+K) surfacing all `ISlashCommand` implementations with their categories and descriptions. Equivalent to `/help` but as a filterable overlay. |
| 10 | Auto-update mechanism | Check for new versions on startup, download and apply updates. Windows: MSIX or Squirrel.Windows. macOS: Sparkle. Linux: AppImage with built-in update check. |
| 11 | Packaging & distribution | Platform-specific installers: MSIX/WinGet (Windows), DMG (macOS), AppImage/Flatpak (Linux). Self-contained single-file publish (no .NET runtime install). CI pipeline for all three. Code signing for Windows and macOS. |

#### Acceptance Criteria

- Single self-contained executable — no server, no CLI, no external process required
- Runtime embedded in-process: full `ConversationRuntime`, tools, agents, memory, sessions
- Zero-latency streaming: in-process `RuntimeEvent` delivery, no serialization overhead
- SQLite persistence: sessions, memory, audit all stored locally and survive restarts
- API keys stored in OS credential store (Windows Credential Manager / macOS Keychain / libsecret), never in plaintext
- Tool confirmations via native Avalonia dialogs implementing `IToolConfirmationHandler`
- Windows build runs on Windows 10+ with full feature parity to the web frontend
- macOS and Linux builds launch and render correctly (may have reduced OS integration)
- `dotnet build` and `dotnet test` pass with no new warnings

---

### Phase 45 — Embedded Terminal Panel ⏸️ Deferred

**Depends on:** Phase 44 (Desktop application)
**Difficulty:** Medium

**Goal:** Add an integrated terminal panel within the Sovrant desktop app, similar to VS Code's integrated terminal or Windows Terminal's tab system. Power users can drop into a full Sovrant CLI session without leaving the app, or open a raw shell for system commands.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Terminal emulator control | Embed a VT100/xterm-compatible terminal emulator as an Avalonia control. Evaluate existing .NET terminal libraries (e.g., Terminal.Gui, XtermSharp) or implement a minimal VT parser. |
| 2 | Sovrant CLI integration | Launch `sovrant` CLI as a child process with stdin/stdout piped to the terminal control. Full REPL experience — banner, sticky input bar, thinking spinner, tool diffs — rendered natively in the terminal panel. |
| 3 | Shell tab support | Support multiple terminal tabs: Sovrant CLI sessions, PowerShell/bash shells, or any arbitrary command. Tab management with keyboard shortcuts (Ctrl+` to toggle, Ctrl+Shift+T for new tab). |
| 4 | Split panes | Horizontal/vertical split within the terminal area. Run a Sovrant session alongside a shell for manual verification. |
| 5 | Shared context | Terminal sessions can reference files and sessions visible in the main desktop UI. Copy file paths, session IDs, or tool outputs between the GUI and terminal with a click. |

#### Acceptance Criteria

- Terminal panel toggles with a keyboard shortcut, docks to bottom or side
- Sovrant CLI renders correctly in the embedded terminal (ANSI colors, cursor positioning, input bar)
- Multiple tabs supported with independent sessions
- Split pane layout with drag-to-resize
- Works on Windows (PowerShell), macOS (zsh), Linux (bash)

---

---

## Phase 46 — n8n Automation Integration

**Goal:** Give Sovrant access to 1,000+ third-party integrations (Slack, Gmail, Sheets, Salesforce, Jira, etc.) by integrating with n8n as a headless automation engine. The runtime orchestrates n8n workflows via API, turning Sovrant agents into automation controllers that can trigger and receive data from external services without building each integration from scratch.

**Integration approaches (not mutually exclusive):**

| Approach | How It Works | Best For |
|---|---|---|
| **API-First** | `HttpClient` triggers n8n workflows via Webhook nodes; n8n returns data to the calling agent | Simple request/response automations, synchronous tool use |
| **Headless Orchestration** | n8n runs in a Docker container alongside Sovrant; the runtime acts as the "brain", n8n as a "worker" for third-party APIs | Complex multi-step workflows, scheduled automations |
| **.NET Aspire Composition** | Aspire orchestrates Sovrant services + n8n container as a managed distributed app | Cloud-native deployments, dev/test parity, health monitoring |

**Items:**
1. `AutomationTool` — agent-callable tool that triggers n8n workflows by name/ID via webhook, returns the result
2. `AutomationListTool` — lists available n8n workflows the agent can trigger
3. n8n Docker Compose profile — `docker-compose.automation.yml` with n8n container, shared network, volume for workflow persistence
4. Webhook callback endpoint — `POST /v1/automation/callback` so n8n can push async results back to Sovrant
5. Workflow template library — starter n8n workflows for common patterns (Slack notify, email send, spreadsheet update, Jira ticket create)
6. .NET Aspire resource definition — `AddN8nContainer()` extension for orchestrated deployments
7. Credential bridging — securely pass API keys from Sovrant's credential store to n8n at container startup
8. `/automation` slash command — list, trigger, and check status of n8n workflows from the CLI
9. Rate limiting and circuit breaker — protect against n8n overload or downstream service failures
10. Documentation — setup guide, workflow authoring, security model

**Verification:**
- `dotnet build` exits 0
- Agent can trigger an n8n workflow via the `Automation` tool and receive structured results
- n8n container starts via Docker Compose with persistent workflow storage
- Webhook callback delivers async results to the correct session
- Aspire orchestration starts both Sovrant and n8n with health checks

---

## Phase 47 — Workspace Backup, Import & Export

**Depends on:** Phase 35 (Workspaces), Phase 32 (SQLite persistence)

**Goal:** Enable full backup, restore, import, and export of Sovrant workspaces so users can snapshot their work, migrate between machines, share workspace templates, and recover from data loss. A workspace export is a self-contained portable archive containing all sessions, memory, config, credentials (encrypted), agent templates, skills, and audit history.

#### Motivation

As workspaces accumulate sessions, learned patterns, custom agents, and configuration, they become valuable intellectual property. Users need to:
- **Back up** before risky operations or machine migrations
- **Restore** a workspace to a known-good state after corruption or accidental deletion
- **Export** a workspace to share with teammates or move to another Sovrant instance
- **Import** a workspace archive received from another user or CI pipeline
- **Template** a workspace setup (agents, skills, config) for repeatable project bootstrapping

#### Archive format

A workspace export is a `.sovrant-workspace` file (ZIP archive) with a deterministic internal layout:

```
workspace-export/
├── manifest.json          # version, workspace metadata, export timestamp, checksums
├── sessions/              # session history (JSONL per session)
│   ├── session-abc.jsonl
│   └── session-def.jsonl
├── memory/
│   ├── summaries.json     # session summaries
│   ├── patterns.json      # learned patterns
│   └── instincts.json     # instinct rules
├── config/
│   ├── workspace.json     # workspace-level settings
│   └── sovrant.json       # project-level config snapshot
├── agents/                # custom agent templates (.md files)
├── skills/                # custom skills (.md files)
├── commands/              # custom slash commands (.md files)
├── credentials.enc        # encrypted credential vault (AES-256-GCM, key derived from user passphrase)
└── audit/
    └── audit-log.jsonl    # governance and tool audit events
```

#### Core operations

| Operation | CLI | API | Description |
|---|---|---|---|
| **Backup** | `sovrant backup [--output path]` | `POST /v1/workspaces/{id}/backup` | Full snapshot of current workspace to `.sovrant-workspace` archive |
| **Restore** | `sovrant restore <archive>` | `POST /v1/workspaces/{id}/restore` | Replace current workspace data with archive contents |
| **Export** | `sovrant export [--exclude sessions,audit]` | `POST /v1/workspaces/{id}/export` | Selective export with optional exclusions |
| **Import** | `sovrant import <archive> [--merge\|--replace]` | `POST /v1/workspaces/import` | Import archive as new workspace or merge into existing |
| **Schedule** | `/backup schedule daily 02:00` | `POST /v1/workspaces/{id}/backup/schedule` | Automated periodic backups with retention policy |

#### Import modes

- **Replace** (default for restore): Drops existing workspace data and replaces with archive contents. Destructive — requires confirmation.
- **Merge**: Adds archive data alongside existing data. Sessions are deduplicated by ID. Config values from the archive override existing where keys collide. Memory entries are merged (no duplicates by content hash). Agent/skill/command files are overwritten if names match.

#### Security

- Credential vault is encrypted with AES-256-GCM; passphrase required on export and import
- Archives can optionally exclude credentials entirely (`--no-credentials`)
- Manifest includes SHA-256 checksums for every file — import validates integrity before applying
- Audit log records all backup/restore/import/export operations with timestamp and user

#### Scheduled backups

- Configurable via workspace config or CLI slash command
- Retention policy: keep last N backups, or backups from last N days
- Storage: local filesystem (`~/.sovrant/backups/`) or configurable path
- Backup rotation: oldest archives pruned when retention limit is exceeded

#### Implementation plan

1. Define `WorkspaceArchive` model — manifest schema, file layout, version numbering
2. Implement `IWorkspaceExporter` — reads workspace data from SQLite/filesystem, writes ZIP archive
3. Implement `IWorkspaceImporter` — reads ZIP archive, validates manifest checksums, applies data
4. Implement credential encryption/decryption with passphrase-derived key (PBKDF2 + AES-256-GCM)
5. Add `sovrant backup` / `sovrant restore` / `sovrant export` / `sovrant import` CLI subcommands
6. Add `/backup` slash command for in-session backup/schedule management
7. Add API endpoints: backup, restore, export, import (streaming file upload/download)
8. Implement merge logic — session dedup, config merge, memory content-hash dedup
9. Implement scheduled backup service with retention policy and rotation
10. Tests: round-trip export→import, merge vs replace modes, credential encryption, checksum validation, scheduled backup trigger, retention pruning

**Verification:**
- `dotnet build` exits 0
- `sovrant backup` produces a valid `.sovrant-workspace` archive
- `sovrant restore <archive>` restores all workspace data
- `sovrant export --exclude credentials` omits credential vault
- `sovrant import <archive> --merge` merges without data loss
- Round-trip: export workspace A → import into workspace B → B has identical data
- Credential vault requires passphrase and is AES-256-GCM encrypted
- Scheduled backup fires at configured time and prunes old archives

---

## Phase 48 — Intent-Aware, Capability-Discovered Model Routing

**Depends on:** Phase 8 (multi-provider), SmartRouter

**Goal:** Automatically select the best LLM model for each turn based on (a) what models the user actually has connected and (b) the input's intent, complexity, and task type — without the user switching models manually. The router discovers each provider's available models at startup, builds a tier ladder from the user's real fleet (Anthropic, OpenAI, Ollama, fine-tunes, or any mix), and routes simple questions to fast/cheap models while sending complex reasoning, code generation, and multi-step planning to high-capability models. Users can override with explicit model selection, but the default is intelligent automatic routing that adapts to whatever they have wired up.

#### Motivation

Today the SmartRouter picks a provider based on latency, cost, and error rate — but it always uses the same model tier. Users must manually `/model <name>` to switch. In practice:
- A simple "what time is it?" doesn't need Opus-class reasoning
- A complex "refactor this module and write tests" benefits from the strongest model
- Creative writing, code review, and data analysis have different optimal model tiers
- Cost-conscious users want cheap models by default with automatic escalation for hard tasks

Intent-aware routing solves this by classifying each input and routing to the appropriate model tier automatically.

#### Core concepts

- **Intent classifier** — lightweight local classifier (rule-based + optional small LLM call) that categorizes user input into intent classes: `simple_qa`, `code_generation`, `code_review`, `refactor`, `planning`, `creative`, `analysis`, `debugging`, `conversation`, `tool_heavy`
- **Complexity estimator** — scores input complexity (0.0–1.0) based on token count, question depth, number of files referenced, presence of code blocks, multi-step indicators ("first...then...finally")
- **Model tier mapping** — maps intent + complexity to model tiers: `fast` (Haiku-class), `standard` (Sonnet-class), `high` (Opus-class)
- **Escalation** — if the model produces a low-confidence or incomplete response, automatically retry with a higher-tier model
- **User override** — `/model <name>` pins to a specific model; `/model auto` re-enables intent routing
- **Cost budget awareness** — if a cost budget is set (Phase 39), prefer cheaper models when budget is running low
- **Capability-aware tier resolution** — the tier map (`fast` / `standard` / `high`) is **not hardcoded to Claude names**. At startup, the router enumerates every connected provider's available models (via `/v1/models` for OpenAI-compatible endpoints, `/api/tags` for Ollama, the configured model list for native providers) and builds the tier map from what is actually reachable. A user running only Ollama gets tiers populated from local models (e.g. `qwen2.5:3b` → fast, `qwen2.5:14b` → standard, `qwen2.5:72b` → high); a user with GPT-4o-mini + GPT-4o gets those; a user with the full Anthropic suite gets Haiku/Sonnet/Opus. If a tier has no candidate, it collapses to the next-best available tier rather than failing.

#### Routing rules (configurable)

| Intent | Default tier | Escalation trigger |
|---|---|---|
| `simple_qa` | fast | N/A |
| `conversation` | fast | Multi-turn depth > 5 |
| `code_review` | standard | File count > 3 |
| `code_generation` | standard | Complexity > 0.7 → high |
| `refactor` | high | Always high |
| `planning` | high | Always high |
| `debugging` | standard | After 2 failed tool rounds → high |
| `creative` | standard | Length > 2000 tokens → high |
| `analysis` | standard | Data size indicators → high |
| `tool_heavy` | standard | Tool round count > 5 → high |

#### Model capability discovery

The router needs to know **which models the user actually has connected** and what each one is good at before it can pick one for a task. This is a discrete subsystem that runs alongside the existing health pings:

- **Discovery pass** at startup (and on `/v1/router/refresh`):
  - OpenAI-compatible providers → `GET /v1/models`
  - Ollama → `GET /api/tags`
  - Native Anthropic / Gemini → declared model list from config
  - Per-user credential overrides (Phase 8 headers) → discovery is per-session, not just per-process
- **Capability metadata** for each discovered model — stored in a `model_capabilities` registry seeded with known data and overridable by the user:
  - `tier_hint` (`fast` / `standard` / `high`) — derived from model family + parameter count
  - `context_window`, `max_output_tokens`
  - `supports_tools`, `supports_vision`, `supports_json_mode`, `supports_thinking`
  - `intent_affinity` — optional per-intent score boost (e.g. a coding-specialised model gets +0.3 on `code_generation`)
  - `cost_per_1k_input`, `cost_per_1k_output` (feeds Phase 39)
- **Auto-tier assignment** — when discovery finishes, the router groups every reachable model by `tier_hint`, picks the cheapest healthy one in each bucket as the default for that tier, and falls back to the next bucket up if a tier is empty (e.g. user only has one large local model → `fast` collapses to `standard`).
- **User override** — `.sovrant/routing.json` can pin specific models to specific tiers, overriding auto-assignment. Discovery still runs so unknown models surface in `/v1/router/status`.
- **Re-discovery triggers** — startup, manual refresh, provider added/removed, ping recovery from unhealthy.

This is what makes the routing "user-aware" rather than Anthropic-flavoured: a developer running pure-local Ollama gets a working tier ladder out of the box, and a team mixing OpenAI + Anthropic + a fine-tune gets tiers built from their actual fleet.

#### Configuration

```json
// .sovrant/routing.json
{
  "intent_routing": true,
  "default_tier": "standard",
  "auto_tier_assignment": true,
  "tier_models": {
    "fast": "auto",
    "standard": "auto",
    "high": "auto"
  },
  "model_overrides": {
    "qwen2.5-coder:14b": { "intent_affinity": { "code_generation": 0.4, "refactor": 0.3 } }
  },
  "escalation": true,
  "max_escalations_per_turn": 1,
  "custom_rules": [
    { "pattern": "*.test.*", "tier": "standard" },
    { "pattern": "security|vulnerability|CVE", "tier": "high" }
  ]
}
```

#### API surface

| Endpoint | Method | Description |
|---|---|---|
| `/v1/config/routing` | GET/PUT | View or update intent routing configuration |
| `/v1/chat/completions` | POST | Existing endpoint — adds `X-Model-Tier` response header showing which tier was selected |

#### Implementation plan

1. Define `IntentClass` enum and `IntentClassification` record (intent, complexity score, recommended tier)
2. Implement `RuleBasedIntentClassifier` — keyword matching, regex patterns, structural analysis (code blocks, file references, question indicators)
3. Implement `IModelCapabilityRegistry` — discovers reachable models per provider (`/v1/models`, `/api/tags`, declared lists), stores capability metadata, supports per-session re-discovery for credential overrides, persists to SQLite so first-run latency is hidden on warm starts
4. Implement `IModelTierResolver` — maps intent classification to a concrete model name using the discovered registry; auto-assigns tier defaults from `tier_hint`; collapses missing tiers to the next-best available
5. Integrate into `SmartRouter.RouteAsync` — classify intent before selecting provider, use tier to filter eligible providers, prefer models with higher `intent_affinity` for the classified intent
6. Add `/model auto` support to `ModelCommand` — sets session config to use intent routing
7. Add `X-Model-Tier` and `X-Intent-Class` response headers to chat completions endpoint
8. Implement escalation logic in `ConversationRuntime.RunTurnAsync` — detect low-quality responses and retry with higher tier
9. Add routing config file loading (`RoutingConfigLoader`)
10. Add custom rule support (pattern-based tier overrides)
11. CLI: show selected tier in token usage line (e.g., `(150↑ 200↓ tokens · standard)`)
12. Surface discovered models + tier assignments in `/v1/router/status` and a new `sovrant router models` CLI command so the user can see which models the router knows about and which tier each one is in
13. Tests: capability discovery per provider type, auto-tier assignment with sparse fleets (Ollama-only, OpenAI-only, mixed), tier collapse when buckets are empty, intent classification accuracy, tier selection, escalation triggers, user override, budget awareness

**Verification:**
- `dotnet build` exits 0
- Capability discovery enumerates models from every connected provider (OpenAI-compatible, Ollama, native)
- Auto-tier assignment produces a working `fast`/`standard`/`high` ladder for an Ollama-only setup, an OpenAI-only setup, and a mixed setup
- Empty tier buckets collapse to the next-best tier without erroring
- Simple questions route to fast tier, complex tasks route to high tier
- `/model auto` enables intent routing; `/model claude-opus-4-6` pins to specific model
- Escalation retries with higher tier on low-quality responses
- `X-Model-Tier` header present in API responses
- Custom routing rules override default tier mapping
- Cost budget reduces tier preference when budget is low
- `sovrant router models` lists discovered models grouped by assigned tier

---

## Phase 49 — SearXNG Web Search Backend

**Depends on:** existing `WebSearch` tool, multi-provider infrastructure

**Goal:** Add a self-hosted, key-free, privacy-preserving web search backend by integrating [SearXNG](https://github.com/searxng/searxng) as a `WebSearch` provider option. Lets users running a local-first stack (Ollama + SQLite + local memory) close the last cloud dependency in the agent loop without paying for Brave / Tavily / SerpAPI keys.

#### Motivation

Today `WebSearch` requires either the OpenAI Responses API native tool (`LLM_WEB_SEARCH=true`, paid) or one of the configured external search APIs. For users who:
- run Ollama for the LLM
- run Sovrant Server locally
- already self-host other infra in a homelab

…there is no fully-offline-capable search option. SearXNG is the natural fit: a single Docker container that aggregates 70+ upstream search engines (Google, Bing, DuckDuckGo, Brave, Wikipedia, GitHub, Stack Overflow, arXiv, …) behind a clean JSON API, with no API keys, no tracking, and per-category source selection.

#### Core concepts

- **`SearxngSearchProvider`** — implements the same internal search-provider interface used by the existing `WebSearch` tool. Calls `GET {SEARXNG_BASE_URL}/search?q=...&format=json` and maps the result list (`title`, `url`, `content`, `engine`, `category`) to Sovrant's existing `WebSearchResult` shape.
- **Selection via env var** — `SOVRANT_WEB_SEARCH=searxng` (alongside the existing `brave`, `tavily`, etc. options) plus `SEARXNG_BASE_URL=http://localhost:8080`. No code changes required for users on other backends.
- **Category filtering** — the `WebSearch` tool gains an optional `category` argument (`general`, `code`, `news`, `science`, `images`, `files`). Maps to SearXNG's `categories=` query param. Other backends ignore the argument.
- **Rate-limit hygiene** — local SearXNG instances are rate-limited by their *own* IP against upstream engines. Provider applies a small per-request delay budget (configurable via `SEARXNG_MIN_INTERVAL_MS`) and surfaces 429s clearly so users learn to back off rather than burn upstream goodwill.
- **Health check** — registered with `SmartRouter`'s health-ping framework so a dead SearXNG container shows up in `/v1/router/status` instead of failing silently mid-conversation.
- **No public-instance default** — `SEARXNG_BASE_URL` must be set explicitly. We will not ship a default pointing at `searx.space` instances; they have their own rate limits and reliability varies.

#### Configuration

```bash
# Self-hosted SearXNG (recommended)
docker run -d --name searxng -p 8080:8080 searxng/searxng

# Sovrant
export SOVRANT_WEB_SEARCH=searxng
export SEARXNG_BASE_URL=http://localhost:8080
export SEARXNG_MIN_INTERVAL_MS=500   # optional, default 0
```

#### Implementation plan

1. Add `SearxngSearchProvider` under `src/Sovrant.Tools/Web/Providers/` (or wherever the existing `BraveSearchProvider` / equivalent lives)
2. Wire selection in the `WebSearch` tool's provider factory keyed on `SOVRANT_WEB_SEARCH=searxng`
3. Add `category` argument to the `WebSearch` tool schema (optional, defaults to `general`)
4. Register a health-check ping (`GET /healthz`) with `SmartRouter` so status shows up in `/v1/router/status`
5. Document the Docker one-liner and env vars in `README.md` § Web Search and `docs/configuration.md`
6. Tests: provider unit tests with a mocked HTTP client, category mapping, error path on 429, health-check integration

**Verification:**
- `dotnet build` exits 0
- With a local SearXNG container running, `WebSearch` returns aggregated results from multiple engines
- `SOVRANT_WEB_SEARCH=searxng` with no `SEARXNG_BASE_URL` fails fast with a clear error
- `category=code` returns developer-oriented results (GitHub, Stack Overflow, etc.)
- `/v1/router/status` shows SearXNG health when configured
- Other web search backends (Brave, Tavily, OpenAI Responses) continue to work unchanged

**Non-goals:**
- Shipping a bundled SearXNG container (users self-host)
- Crawling / indexing — this is purely query-time aggregation; tools like Crawl4AI / Firecrawl are out of scope
- Public-instance fallback — explicitly not provided for rate-limit hygiene reasons

---

## Phase 50 — OpenClaw Integration & Federated Swarms Over a Routed Bus

**Depends on:** Phase 16 (Dynamic MCP Tool Proxy), Phase 29 (Swarm Orchestrator), Phase 35 (Workspaces), Phase 37.5 (Swarm event store), Phase 19+20 (Multi-agent teams)

**Goal:** Make [OpenClaw](https://docs.openclaw.ai/) the **routed message bus** for federated Sovrant swarms. Sovrant swarms (running Sovrant's own workers — not OpenClaw workers, because OpenClaw isn't a coding agent) post events, findings, and approval requests into OpenClaw routes; other swarms and human operators on Discord / Telegram / WhatsApp / Slack / Signal / iMessage / Matrix subscribe to those routes and respond. Three federation modes (`silo`, `federated`, `manager-led`) map onto OpenClaw's routing primitives so multiple swarms can run side-by-side either fully isolated, sharing a common channel, or reporting up to a manager. As a free side-effect, every running swarm becomes reachable from a phone.

#### What OpenClaw is (and isn't)

OpenClaw is a **self-hosted gateway/orchestrator** that bridges messaging platforms (Discord, Telegram, WhatsApp, Slack, Signal, iMessage, Matrix, …) to AI agents. It is *not* a coding agent and does not execute swarm tasks itself — it routes messages, attachments, events, and approval requests between channels and the agents that consume them. Routes are isolated per agent / workspace / sender.

**Integration surface — verified:** OpenClaw ships an **MCP server mode** invoked as `openclaw mcp serve` over **stdio**. The bridge exposes nine standard MCP tools:

| Tool | Purpose |
|---|---|
| `conversations_list` / `conversation_get` | Discover and retrieve routed conversations |
| `messages_read` / `attachments_fetch` | Read transcript history and message metadata |
| `messages_send` | Send replies through existing routes |
| `events_poll` / `events_wait` | Consume the live event queue (`events_wait` is a long-poll) |
| `permissions_list_open` / `permissions_respond` | Manage approval requests |

The server holds an in-memory event queue that starts when the bridge connects; older history is fetched separately via `messages_read`. With Claude channel mode enabled it also emits `notifications/claude/channel`.

**Why this matters for performance:** Sovrant already speaks MCP fluently — Phase 16 (`MCPTool`) lets any MCP server's tools surface inside the Sovrant agentic loop, and Phase 17 covers OAuth. **Adding OpenClaw is mostly configuration**, not a new transport layer. We launch `openclaw mcp serve` as a managed MCP child process (the same way every other MCP server is launched today), the nine OpenClaw tools become callable by Sovrant agents on first start, and the existing MCP infrastructure handles framing, lifecycle, restarts, and audit. No gRPC, no protobuf, no new IPC, no parallel transport story — and the long-lived stdio connection means the per-call overhead is dominated by the underlying messaging-platform RTT, not by Sovrant's wire format.

#### Motivation

Today Sovrant swarms run in isolation. There is no way for:
- Two swarms to share intermediate findings without going through the parent orchestrator's event store
- A human operator to be notified on their phone when a swarm hits a permission prompt or finishes a long-running task
- A "manager" swarm to fan tasks out to several "worker" swarms and collect their results through anything other than direct in-process function calls
- Adversarial / red-team workflows to guarantee that two competing swarms cannot read each other's intermediate state

OpenClaw solves all four with a single routing primitive: **the route**. A route is a named channel that one or more agents subscribe to, with isolation guarantees enforced by the gateway. Mapping Sovrant's federation modes onto OpenClaw routes gives us inter-swarm communication, human-in-the-loop chat-channel access, and red-team isolation in one phase.

#### Core concepts

- **`OpenClawBusClient`** — a thin Sovrant-side wrapper around the existing MCP tool proxy that exposes named, ergonomic methods (`PublishAsync`, `SubscribeAsync`, `RequestApprovalAsync`) backed by `messages_send`, `events_wait`, and `permissions_*`. Workers and the manager don't call MCP tools directly; they call this wrapper. Reuses the channel and lifecycle that Phase 16 already provides.
- **Route naming convention** — every Sovrant swarm gets a deterministic OpenClaw route derived from its `swarm_id` plus federation key, e.g. `sovrant/swarm/{swarmId}` for silo mode and `sovrant/federation/{federationId}` for federated mode. The naming convention is enforced by `SwarmExecutionContext` so workers cannot accidentally subscribe to a route outside their scope.
- **`SwarmFederationMode`** — new field on `SwarmConfig`:
  - `silo` (default) — each swarm posts to and reads from a private route `sovrant/swarm/{swarmId}`. No other swarm has the route name. OpenClaw's per-route isolation gives us red-team-grade separation for free.
  - `federated` — every child swarm of a parent shares one route `sovrant/federation/{parentSwarmId}`. Workers can `events_wait` on this route to consume each other's intermediate findings without going through the manager.
  - `manager-led` — workers post to a manager-owned route `sovrant/manager/{managerId}/inbox`; only the manager is subscribed via `events_wait`. Worker-to-worker traffic is impossible. The manager re-publishes rolled-up events onto a separate `sovrant/manager/{managerId}/outbox` route that the parent swarm and any human operators subscribe to.
- **`SwarmManagerAgent`** — a regular Sovrant agent template (`swarm-manager.md`) that owns a parent `swarm_id`, spawns child swarms, holds an open `events_wait` against its inbox route, and emits roll-up events (`ChildSwarmCompleted`, `ChildSwarmFailed`, `FederationFinding`) onto its outbox route **and** into the local `swarm_events` table via the Phase 37.5 store. Persistence is unchanged — the manager just dual-publishes to the bus.
- **Human-in-the-loop for free** — because OpenClaw routes are first-class channels in Discord / Slack / Telegram / etc., a human operator who joins `sovrant/manager/{managerId}/outbox` from their phone instantly sees the swarm's rolled-up status, and replies routed back via `messages_send` reach the manager agent. The same plumbing handles `permissions_list_open` / `permissions_respond` so an approval prompt from a Sovrant tool call can be answered from a phone without any extra Sovrant code.
- **Permission bridge** — when a Sovrant tool inside a swarm hits the existing `IPermissionPolicy` and the policy returns "ask", Sovrant publishes the request via `permissions_list_open` (with the route in scope) and blocks on `permissions_respond`. From the operator's view this is identical to the existing in-process permission flow; the only difference is that the answer can come from any subscribed channel.
- **Auth & isolation** — every published message inherits the parent swarm's `SwarmExecutionContext` (workspace, project, user) and is tagged in OpenClaw via the route name plus message metadata. Silo mode also enforces filesystem isolation by running workers in scratch directories (or `EnterWorktree` clones) that are torn down on completion.
- **Health & status** — the OpenClaw MCP server is registered with `SmartRouter`'s health-ping framework via the existing MCP server lifecycle. `GET /v1/swarm/openclaw/routes` lists active routes Sovrant currently has subscriptions or publishers on, with last-event timestamps.

#### Configuration

```jsonc
// .sovrant/swarm.json
{
  "enabled": true,
  "openClaw": {
    "enabled": true,
    // Sovrant launches `openclaw mcp serve` as a managed MCP child process via the existing
    // MCP server config (Phase 16). No new transport. The block below is just sugar over the
    // standard MCP server entry — the launcher and lifecycle live in mcp.json.
    "mcpServerName": "openclaw",
    "claudeChannelMode": "auto",            // maps to --claude-channel-mode
    "gateway": {
      "url": "wss://openclaw.local/gateway", // --url
      "tokenFile": "~/.sovrant/openclaw/token" // --token-file
    },
    "routePrefix": "sovrant",
    "approvalTimeoutSec": 300                 // how long Sovrant blocks on permissions_respond
  },
  "federation": {
    "mode": "manager-led",                    // "silo" | "federated" | "manager-led"
    "managerAgent": "swarm-manager",          // template name under .sovrant/agents/templates
    "maxChildSwarms": 8
  }
}
```

```jsonc
// .sovrant/mcp.json — the standard MCP server entry Sovrant already understands
{
  "mcpServers": {
    "openclaw": {
      "command": "openclaw",
      "args": ["mcp", "serve",
               "--url", "wss://openclaw.local/gateway",
               "--token-file", "/home/me/.sovrant/openclaw/token",
               "--claude-channel-mode", "auto"]
    }
  }
}
```

#### Implementation plan

1. Add `OpenClawBusClient` under `src/Sovrant.Agents/Swarm/Bus/OpenClaw/`. It is a thin facade over the existing `MCPTool` proxy — given the MCP server name `openclaw`, it resolves the nine bridge tools and exposes `PublishAsync`, `SubscribeAsync`, `RequestApprovalAsync`, `LoadHistoryAsync`. **No new transport code** — Phase 16's `MCPTool` does the wire work.
2. Extend `SwarmConfig` with the `OpenClaw` and `Federation` blocks. Default `OpenClaw.Enabled = false` so existing installs are unaffected. Validate that the named MCP server exists in `mcp.json` at startup.
3. Add `SwarmFederationMode` enum + per-swarm field. Add a `RouteResolver` that maps `(SwarmExecutionContext, federationMode)` → route name using the documented convention so workers cannot fabricate route names.
4. Add `parent_swarm_id` column to `swarm_events` in a new migration `Vxxx__swarm_federation.sql`. Index it. Extend `SwarmListFilter` and `ISwarmEventStore.LoadEventsAsync` with an optional `parentSwarmId` filter. The manager's roll-up roll-up still lands in the local store; the bus is for live distribution, not durable history.
5. `SwarmOrchestrator.ExecuteAsync` honors the federation mode: in `silo` it publishes only to the per-swarm route, in `federated` it publishes to and reads from the shared parent route, in `manager-led` it publishes to the manager inbox and refuses to subscribe to anything else.
6. Implement `SwarmManagerAgent` as a regular Sovrant agent template (`swarm-manager.md`). Its system prompt encodes the fan-out / fan-in protocol: decompose, spawn child swarms with appropriate `SwarmExecutionContext`, hold an `events_wait` long-poll on the inbox, aggregate, publish to outbox, persist roll-up to `swarm_events`.
7. Wire approval routing: when `IToolConfirmationHandler` is asked to confirm a tool call inside a swarm with OpenClaw enabled, it publishes via `permissions_list_open` and blocks on `permissions_respond` (with timeout from config).
8. Extend the existing `Swarm` tool with a `federation` argument (`silo` / `federated` / `manager-led`) plus an optional `parent_swarm_id` so Sovrant agents can spawn child swarms from the agentic loop without a brand-new tool.
9. New routes:
    - `POST /v1/swarm/manager` — start a manager-led federated swarm with N child sub-tasks
    - `GET  /v1/swarm/openclaw/routes` — active OpenClaw routes Sovrant has subscriptions or publishers on
    - `GET  /v1/swarm/{id}/children` — child swarms of a parent (uses `parent_swarm_id`)
10. CLI subcommand `sovrant openclaw routes {list|tail <route>}` so operators can inspect what's flowing on a route from the terminal without opening a chat client.
11. Tests:
    - `OpenClawBusClient` against an in-process fake MCP server that implements the nine bridge tools (no real OpenClaw needed for unit tests)
    - `RouteResolver`: silo / federated / manager-led modes produce the documented route names and reject attempts to subscribe outside scope
    - Federation behavior: 3 child swarms in silo mode see zero cross-traffic; in federated mode they see each other's findings; in manager-led mode workers see only the manager
    - Approval round-trip: tool call → `permissions_list_open` → simulated `permissions_respond` → tool call resumes
    - Manager roll-up: 3 child swarms emit `SwarmCompleted`, manager aggregates into one parent event in `swarm_events` and one outbox publish
    - One slow integration test gated behind `OPENCLAW_E2E=1` that exercises a real `openclaw mcp serve` against a test gateway
12. Docs: `docs/persistence.md` (new column on `swarm_events`), `docs/server.md` (new routes), `README.md` § Swarms (federation modes + OpenClaw bus), `agent-systems.md` (manager-led pattern with chat-channel HITL).

**Verification:**
- `dotnet build` exits 0
- With OpenClaw configured and a test gateway running, `sovrant swarm "Refactor X" --federation manager-led` runs three child swarms and lands a single rolled-up event in the parent's `swarm_events` row
- `mode=silo`: spawning two child swarms shows zero cross-route events; each only sees its own route
- `mode=federated`: worker B can `events_wait` on the shared route and see worker A's findings
- `mode=manager-led`: manager `events_wait` receives every child event; worker-to-worker traffic returns nothing
- A permission prompt raised inside a swarm tool call appears via `permissions_list_open` and resumes the tool call when answered via `permissions_respond` from a chat channel
- Killing the manager mid-run leaves child swarms in a terminal state (either completed or explicitly cancelled), not orphaned
- A human in a Discord/Slack channel subscribed to the manager outbox receives the same roll-up events Sovrant writes to `swarm_events`

**Non-goals:**
- Bundling or distributing OpenClaw itself (users install and configure it separately, including any messaging-platform credentials)
- Reimplementing OpenClaw's routing or channel adapters in Sovrant — the whole value of this phase is that OpenClaw already does that
- Treating OpenClaw routes as durable storage. The bus is for live distribution; the SQLite `swarm_events` table is still the source of truth. If a subscriber is offline, they catch up via `messages_read`, not by replaying a Sovrant-side queue.
- Cross-tenant federation (one Sovrant install, one OpenClaw gateway, in this phase). Multi-tenant federation can come later if there's demand.

---

### Known Issues / Debt

| Issue | Priority | Notes |
|---|---|---|
| `AskUserQuestion` blocked in server mode | Low | By design — no interactive console available over HTTP. Could be solved via a webhook/callback URL pattern. |
| No request-level timeout on agentic loop | Medium | A runaway tool loop can occupy a session indefinitely; add per-turn wall-clock timeout. |
| CORS origins hardcoded | Low | Should be configurable via `SOVRANT_CORS_ORIGINS` env var. |
| `launchSettings.json` port conflicts with `SOVRANT_PORT` default | Low | `launchSettings.json` declares `5091`; Kestrel overrides to `5200`. Rapid restart or parallel test runs cause `SocketException (10048)`. Fix: align `launchSettings.json` with `SOVRANT_PORT`; add `--urls` CLI override for CI. |
| Team tools not yet smoke-tested with live LLM | Medium | `TeamCreate`/`TeamDelete`/`TeamStatus`/`TeamDelegate` have 58 unit tests but no end-to-end smoke test with a real provider. |

### Resolved Issues

| Issue | Resolution |
|---|---|
| Token counts always `0↑ 0↓` | ✅ `OpenAiCompatProvider` captures trailing OpenAI usage chunk |
| SmartRouter crashes on WSL DNS failure | ✅ Falls back to configured providers when all fail startup ping |
| Provider has no retry on 429/5xx | ✅ Phase 5 — 3 attempts with 1s/2s/4s backoff |
| `EnterPlanMode`/`ExitPlanMode` are global in server mode | ✅ Phase 10 — session-scoped `SessionConfig` overlay |
| `Sovrant.Agents` not wired into CLI or Server | ✅ Phase 19+20 — `AddMultiAgentSystem()` called in both hosts |
