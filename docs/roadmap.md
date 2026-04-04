# Sovrant — Roadmap

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-04-04 (Phase 7.5 Tier 1 implemented — 27 tools)

This document tracks planned features, architectural decisions, and the reasoning behind them.

---

## Current State (Phase 7 complete)

The engine is fully functional as a single-user tool:

- CLI REPL and one-shot prompt mode
- Agentic loop with up to 20 tool rounds per turn
- 27 tools working on Windows and Linux (22 original + 5 Phase 7.5 Tier 1) (Phase 7.5 Tier 1 complete; Tier 2 in progress)
- Per-runtime mutable permission mode (`IPermissionModeAccessor`) for model-driven plan mode transitions
- JSONL session persistence
- SmartRouter with health/latency scoring across multiple providers
- HTTP server (`Sovrant.Server`) with OpenAI-compatible endpoints
- In-memory session pool (one `ConversationRuntime` per `session_id`, safe for multiple users)

---

## Roadmap

### Phase 7.5 — Tool Parity with OpenClaude

**Goal:** Close the gap between Sovrant's 22 tools and the full OpenClaude tool set. A comparison of the OpenClaude source against Sovrant's `Sovrant.Tools` project identified 9 missing tools worth porting and 13 cloud/platform-only stubs that are not portable.

#### Tool comparison summary

| Category | Count | Tools |
|---|---|---|
| Implemented ✅ | 27 | Read, Write, Edit, Glob, Grep, LS, Bash, PowerShell, REPL, WebFetch, WebSearch, TaskCreate/Get/List/Output/Stop/Update, TodoWrite, Agent, AskUserQuestion, Sleep, NotebookEdit, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree |
| Missing — port ⬜ | 7 | ListMcpResources, ReadMcpResource, ToolSearch, SkillTool, ScheduleCron, ConfigTool, LSP |
| Cloud — future phases ☁️ | 3 | MCPTool (Phase 12), McpAuthTool (Phase 13), TeamCreate/Delete (Phase 14) |
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

**`/undo` / `/redo` (git-backed)**
Before every `Write` or `Edit` tool call, stash the current file state to a temporary git commit or diff buffer. `/undo` reverts the last agent file change; `/redo` reapplies it. Builds user trust significantly — the user can always roll back an agent mistake without losing their own work. Related to `EnterWorktree`/`ExitWorktree` (Tier 1) but applies even outside a worktree. Implementation: wrap `Write`/`Edit` tool execution in `ConversationRuntime` with pre/post git snapshot calls.

**Custom project slash commands (`.sovrant/commands/`)**
Project-specific slash commands defined as markdown files in `.sovrant/commands/{name}.md`. When invoked, the file's content is injected as a user message — equivalent to a project-local skill. Extends the global `SkillTool` (below) to support per-repo, version-controlled command libraries. Example: `/deploy`, `/review`, `/test`. Implementation: extend `SkillTool` to check `.sovrant/commands/` before the global skills directory.

**`ListMcpResources` / `ReadMcpResource`**
OpenClaude distinguishes between MCP *tools* (callable functions) and MCP *resources* (readable data — files, database rows, API responses). Sovrant's MCP client invokes tools but cannot read resources. These two tools add resource access: `ListMcpResources` enumerates what each connected MCP server exposes; `ReadMcpResource` fetches a resource by server name and URI.

**`ToolSearch`**
When the tool list grows large (many MCP servers, many registered tools), including all tool definitions in every LLM context window is expensive and noisy. `ToolSearch` lets the model search available tools by keyword and load them on demand — "deferred tool discovery". Relevant once the tool count exceeds ~30 or MCP servers register many tools.

**`SkillTool`**
Invokes a named skill — a pre-defined prompt template stored in `.sovrant/skills/{name}.md`. The model calls `Skill("commit")` and the skill's prompt is injected as a user message, triggering a specialised behaviour. Enables reusable, project-specific agent workflows without changing code.

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
5. Custom project commands — extend `SkillTool` to resolve `.sovrant/commands/{name}.md` before global skills directory
6. `ListMcpResources` / `ReadMcpResource` — extend existing MCP client (`IMcpClient`) with a `ReadResourceAsync` method
7. `ToolSearch` — add deferred tool registry support to `IToolRegistry`; `ToolSearch` queries it by keyword
8. `SkillTool` — reads `.sovrant/skills/{name}.md` from disk and returns the content as a user message injection
9. `ScheduleCron` / `ConfigTool` / `LSPTool` — deferred; document as future work

---

### Phase 7.6 — Quick Wins from Competitor Analysis

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

**Blocks:** Phase 9.5 usage tracking, context window visualisation

OpenAI sends `usage` data in the second-to-last SSE chunk (before `[DONE]`), not in `MessageDelta`. The current `CollectStreamEventsAsync` in `ConversationRuntime` only reads `MessageDelta.Usage.OutputTokens` — the OpenAI format uses a top-level `usage` field on the final content chunk.

Fix: detect the `usage` field on the final OpenAI SSE chunk and capture `prompt_tokens` / `completion_tokens` into `accumulated.InputTokens` / `accumulated.OutputTokens`. Once fixed, expose live token counts in the REPL status line and in `GET /v1/sessions/{id}`.

#### Implementation Plan

1. Agent memory files — extend `BuildSystemPrompt()` in `ConversationRuntime`; add `/memory` slash command
2. Token count fix — update `CollectStreamEventsAsync` to capture OpenAI `usage` field from final SSE chunk
3. Context auto-compaction — add compaction logic to `RunTurnAsync`; add `SOVRANT_COMPACT_THRESHOLD` config; persist compaction events to JSONL
4. Expose token counts in REPL status line and `GET /v1/sessions/{id}` response

---

### Phase 8 — Multi-Tenant Per-Request Credentials

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
  "x_api_key": "sk-...",
  "x_base_url": "https://api.openai.com/v1"
}
```

The server:
1. Checks for `x_api_key` / `x_base_url` in the request body
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

- `x_api_key` is a client-supplied secret. The server must never log it, persist it, or include it in error responses.
- The server's own `SOVRANT_TOKEN` continues to gate all requests — the per-request key is only for the downstream LLM call.
- Rate limiting per `x_api_key` or per `SOVRANT_TOKEN` client should be added to prevent abuse.

#### Implementation Plan

1. Add `XApiKey` and `XBaseUrl` to `ChatCompletionRequest`
2. In `ChatRoutes.HandleAsync`:
   - If `XApiKey` / `XBaseUrl` are present, build a scoped `OpenAiCompatProvider` with those credentials
   - Wrap it in a scoped `SmartRouter` (single-provider, skip ping)
   - Create a scoped `ConversationRuntime` using the scoped router
3. Session pool key: `{sessionId}::{baseUrl}` or a hash of both
4. Add `x_api_key` to the server's sensitive-field redaction list

---

### Phase 9 — Session Lifecycle Management (TTL Eviction + Turn Serialization)

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

### Phase 9.5 — Production Multi-User Hardening

**Depends on:** Phase 8 (per-request credentials) + Phase 9 (session TTL + per-session lock)

**Goal:** Make Sovrant safe and correct for a real product deployment where multiple users log in independently — isolated identity, isolated config, per-user cost tracking, and no shared global state leaking between users.

#### What works today for a team

Session history is already isolated per `session_id` — ten users with different session IDs have fully independent conversation histories and tool state. For a **trusted internal team sharing a single deployment** (shared `SOVRANT_TOKEN`, shared `LLM_API_KEY`, no per-user billing) this is sufficient today.

#### What is missing for a product with user logins

| Gap | Problem | Fix |
|---|---|---|
| Single `SOVRANT_TOKEN` | All users share one bearer token — no per-user identity, anyone can delete anyone else's session or change global config | Per-user auth tokens (JWT or opaque tokens issued at login) |
| Single `LLM_API_KEY` | All users bill to one key — no per-user cost tracking or LLM-level rate limiting | Phase 8 per-request `x_api_key` — each user supplies their own key from the frontend |
| `PUT /v1/config` is global | One user changing the model or permission mode changes it for all users simultaneously | Session-scoped config overlay — per-session `MutableServerConfig` that shadows the global one |
| No per-user rate limiting | A single user can saturate the server with concurrent requests, starving others | Per-token request rate limiter (ASP.NET Core rate limiting middleware) |
| No cost visibility | No way to see which user or session is responsible for LLM spend | Per-session token usage log aggregated in `GET /v1/sessions/{id}` and a new `GET /v1/usage` summary endpoint |

#### Design

**Per-user auth tokens**
Replace the single `SOVRANT_TOKEN` with a small token registry: the server operator issues named tokens (one per user or team member) via a config file or `POST /v1/admin/tokens`. Each request carries its token; the server resolves the caller identity from it. No OAuth required at this stage — simple opaque tokens are sufficient for a team of 10–50.

**Session-scoped config overlay**
`PUT /v1/config` currently mutates a single `MutableServerConfig` singleton. After Phase 8, each session pool entry already has its own provider credentials. Extend this: each `SessionEntry` carries a `SessionConfig` (model, permission mode) that overlays the global defaults. `PUT /v1/sessions/{id}/config` mutates only that session's overlay. The global `PUT /v1/config` becomes an admin-only operation.

**Per-token rate limiting**
Use ASP.NET Core's built-in `RateLimiter` middleware. Policy: N requests/minute per token (configurable via `SOVRANT_RATE_LIMIT_RPM`, default 60). The 429 response includes a `Retry-After` header.

**Usage tracking**
Each `SessionEntry` accumulates `TotalInputTokens` and `TotalOutputTokens` across all turns. The existing token-count bug (always 0 for OpenAI streaming) must be fixed first — this is the forcing function to fix it. `GET /v1/usage` returns a summary per session ID and per caller token.

#### Implementation Plan

1. Add token registry (`ITokenRegistry`) — maps opaque token → caller name; loaded from `SOVRANT_TOKENS` env var (JSON) or a config file
2. Replace single `SOVRANT_TOKEN` check in auth middleware with `ITokenRegistry.Resolve(token)`
3. Add `SessionConfig` overlay to `SessionEntry`; add `PUT /v1/sessions/{id}/config`; restrict global `PUT /v1/config` to admin tokens
4. Add ASP.NET Core `RateLimiter` middleware keyed on caller token
5. Fix token count capture — moved to Phase 7.6 (prerequisite); Phase 9.5 consumes the result
6. Add `TotalInputTokens` / `TotalOutputTokens` to `SessionEntry`; add `GET /v1/usage` endpoint
7. Add context window visualisation to `GET /v1/sessions/{id}` — `context_used_pct`, `tokens_remaining`; surface in REPL status line

---

### Phase 10 — LSP Integration (Language Server Protocol)

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

### Phase 11 — CI/CD Pipeline Integration

**Competitor precedent:** Claude Code ✅ (GitHub Actions + GitLab CI)

**Goal:** Enable Sovrant to run autonomously inside CI pipelines — fix failing tests, resolve build errors, update generated code — without human intervention.

#### Design

`Sovrant.Server` already provides the HTTP API. CI integration is a thin wrapper:

1. A GitHub Actions action (`sovrant-agent-action`) that:
   - Starts `Sovrant.Server` (or calls a hosted instance)
   - POSTs the failing CI context (test output, build log, diff) as a user message to `/v1/chat/completions`
   - Streams the response; captures any file edits the agent makes
   - Commits the changes if the agent signals success
   - Fails the action if the agent cannot fix the issue within N tool rounds

2. A GitLab CI template equivalent

#### Use cases

- "Fix the failing tests" — post test output, agent reads code, makes fixes, reruns
- "Update generated code" — post schema changes, agent regenerates affected files
- "Resolve merge conflicts" — post conflicted diff, agent resolves and commits

#### Security Note

The CI environment must never pass production secrets into the agent session. Use read-only API keys scoped to the CI provider. The `plan` permission mode is recommended for CI runs that should not make destructive changes.

#### Implementation Plan

1. Add `--ci` flag to the CLI one-shot mode — machine-readable JSON output, non-zero exit on error
2. Create `sovrant-agent-action` GitHub Actions action (YAML + shell wrapper around the CLI)
3. Document GitLab CI equivalent
4. Add CI-specific permission policy: `CiPermissionPolicy` — auto-approves file edits, denies shell commands that touch outside the working directory

---

### Phase 12 — Slack / Webhook Integration

**Competitor precedent:** Claude Code ✅ (OAuth Slack app)
**Depends on:** Phase 9.5 (per-user auth tokens — each Slack user maps to an isolated session)

**Goal:** Invoke Sovrant from a Slack message and receive streamed responses in a Slack thread — enabling "ask the codebase" workflows for teams without leaving Slack.

#### Design

A Sovrant Slack app (self-hosted) that:
- Listens for messages in designated channels or direct messages
- Forwards the message to `POST /v1/chat/completions` with a session ID derived from the Slack user ID
- Streams the response back as a series of Slack message updates (progressive edit)
- Surfaces tool events as Slack attachments (e.g., "Running Bash: `npm test`")

Alternatively, a generic webhook integration — `POST /v1/webhook` accepts a message from any source (Slack, Teams, Discord, custom) and routes it into the session pool, returning the response to a configured callback URL.

#### Implementation Plan

1. Add `POST /v1/webhook` to `Sovrant.Server` — accepts `{ source, user_id, message, callback_url }`, runs a turn, POSTs result to `callback_url`
2. Build Sovrant Slack app manifest and event handler (Node.js thin wrapper or Bolt SDK)
3. Map Slack user IDs to Sovrant session IDs using Phase 9.5 token registry
4. Document Teams and Discord equivalents using the webhook endpoint

---

### Phase 13 — Frontend SDK

**Goal:** A typed TypeScript/JavaScript client for `Sovrant.Server` that handles SSE streaming, session management, and tool event rendering.

#### Planned Features

- `SovrantClient` class: wraps `fetch` + SSE parsing
- `useChat()` React hook (standard streaming chat hook pattern)
- Per-request credential injection (for Replit/browser use where the client holds the key)
- Built-in retry on transient errors
- Tool event callbacks: `onToolUse`, `onPermissionDenied`, `onError`

#### Additional: Structured Diff View in REPL

**Competitor precedent:** Claude Code ✅ · opencode ✅

Before applying `Edit` or `Write` tool calls, render a colour unified diff of the proposed change using Spectre.Console markup. The permission dialog shows exactly what will change — filename, line numbers, added/removed lines in green/red — rather than raw tool arguments. Significantly increases trust in the agent's edits.

Implementation: intercept `Edit`/`Write` permission prompts in `ModeAwarePermissionPolicy`; compute and render the diff before asking for approval.

#### Additional: Session Export

**Competitor precedent:** opencode (`/share`)

Add `GET /v1/sessions/{id}/export?format=markdown` — returns the full session rendered as human-readable markdown (user/assistant turns, tool calls summarised, timestamps). Useful for sharing debugging sessions, creating audit trails, and archiving completed tasks. The JSONL format already contains everything needed; the endpoint is a thin renderer.

---

### Phase 14 — MCP Server Mode

**Goal:** Expose Sovrant as an MCP server so it can be consumed by MCP clients or composed into larger agent pipelines.

---

### Phase 15 — IDE Extension (VS Code)

**Competitor precedent:** Claude Code ✅ · opencode ✅ (beta)
**Depends on:** Phase 14 (MCP server mode) — once Sovrant exposes an MCP server, MCP-aware IDEs (VS Code with GitHub Copilot, Cursor, Windsurf) can connect without a bespoke extension.

**Goal:** Embed Sovrant into VS Code as a sidebar panel — chat interface, inline diff approval, tool event rendering, permission dialogs with file highlighting.

#### Architecture

Two-layer approach:
1. **Phase 14 (MCP):** Zero-code IDE integration for MCP-aware clients. Sovrant appears as an MCP tool server. No extension required.
2. **Phase 15 (native extension):** A dedicated VS Code extension that connects to `Sovrant.Server` via HTTP/SSE for richer UX — inline diffs, file decorations, permission dialogs anchored to the relevant file.

#### Implementation Plan

1. Publish `Sovrant.Server` as a local background service (`sovrant serve` command) with auto-start on VS Code activation
2. Implement the VS Code extension (`vscode-sovrant`) — TypeScript, connects to `Sovrant.Server` via the Phase 13 frontend SDK
3. Sidebar: chat panel backed by `useChat()` hook
4. Inline diffs: intercept `Edit`/`Write` tool events, show diff decoration in the editor
5. Permission dialogs: VS Code `window.showInformationMessage` with approve/deny buttons
6. Publish to VS Code Marketplace

---

### Phase 16 — Dynamic MCP Tool Proxy (`MCPTool`)

**Goal:** Allow the model to discover and invoke any tool exposed by a connected MCP server dynamically, without those tools being statically registered in `ToolRegistrar` at startup.

#### Motivation

Today Sovrant's MCP integration pre-registers a fixed set of tools from configured MCP servers on startup. As Sovrant becomes a cloud platform, users will connect arbitrary MCP servers (databases, APIs, SaaS integrations) that expose dozens or hundreds of tools. Pre-registering everything is impractical — it bloats the context window and requires a server restart to pick up new servers.

`MCPTool` acts as a **dynamic proxy**: the model calls `MCPTool({ server: "myserver", tool: "query_table", input: {...} })` and the runtime forwards the call to the named MCP server at execution time. This decouples tool discovery from startup.

#### Design

- `MCPTool` takes `server`, `tool`, and `input` as parameters
- At execution time it resolves the named MCP server from the active `IMcpClient` registry
- Validates that the named tool exists on that server
- Forwards `input` as-is and returns the MCP tool result
- Works alongside the existing static tool registration — statically registered tools remain preferred; `MCPTool` is the fallback for everything else
- Pairs with `ToolSearch` (Phase 7.5 Tier 2) which lets the model discover what tools are available before calling them

#### Implementation Plan

1. Extend `IMcpClient` with `CallToolAsync(serverName, toolName, input)`
2. Implement `McpProxyTool` (name: `MCPTool`) in `Sovrant.Tools/Mcp/`
3. Register `McpProxyTool` only when at least one MCP server is configured
4. Coordinate with `ToolSearch` so the model can list-then-call in a single pattern

---

### Phase 17 — MCP OAuth Authentication (`McpAuthTool`)

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

### Phase 18 — Multi-Agent Teams (`TeamCreateTool` / `TeamDeleteTool`)

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

### Known Issues / Debt

| Issue | Priority | Notes |
|---|---|---|
| Token counts always `0↑ 0↓` | Medium | OpenAI streaming sends usage in the final chunk; the SSE parser needs to capture the `usage` field from the `[DONE]` predecessor chunk |
| `AskUserQuestion` blocked in server mode | Low | By design — no interactive console available over HTTP. Could be solved via a webhook/callback URL pattern |
| SmartRouter evicts a provider as unhealthy on first ping failure | Medium | Should retry a few times before marking unhealthy; add exponential backoff |
| No request-level timeout on agentic loop | Medium | A runaway tool loop can occupy a session indefinitely; add per-turn wall-clock timeout |
| CORS origins hardcoded | Low | Should be configurable via `SOVRANT_CORS_ORIGINS` env var |
| `launchSettings.json` port conflicts with `SOVRANT_PORT` default | Low | `launchSettings.json` declares `5091`; Kestrel overrides to `5200`. Rapid restart or parallel test runs cause `SocketException (10048)`. Fix: align `launchSettings.json` with `SOVRANT_PORT`; add `--urls` CLI override for CI. |
| `EnterPlanMode`/`ExitPlanMode` are global in server mode | Medium | Server uses shared `MutableServerConfig` singleton — calling `EnterPlanMode` in one session sets plan mode for all sessions simultaneously. Requires session-scoped config overlay (Phase 9.5). |
