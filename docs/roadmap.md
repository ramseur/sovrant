# Sovrant — Roadmap

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-04-04

This document tracks planned features, architectural decisions, and the reasoning behind them.

---

## Current State (Phase 7 complete)

The engine is fully functional as a single-user tool:

- CLI REPL and one-shot prompt mode
- Agentic loop with up to 20 tool rounds per turn
- 22 tools working on Windows and Linux (see Phase 7.5 for gap analysis vs OpenClaude)
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
| Implemented ✅ | 22 | Read, Write, Edit, Glob, Grep, LS, Bash, PowerShell, REPL, WebFetch, WebSearch, TaskCreate/Get/List/Output/Stop, TodoWrite, Agent, AskUserQuestion, Sleep, NotebookEdit |
| Missing — port ⬜ | 9 | TaskUpdate, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree, ListMcpResources, ReadMcpResource, ToolSearch, SkillTool, ScheduleCron, ConfigTool, LSP |
| Not portable ❌ | 13 | MCPTool, McpAuthTool, TeamCreate/Delete, RemoteTrigger, SendMessage, WorkflowTool, BriefTool, SuggestBackgroundPR, VerifyPlanExecution, SyntheticOutput, Tungsten |

#### Tier 1 — High priority (complete the core developer experience)

**`TaskUpdate`**
The task management suite has 5 of 6 tools. `TaskUpdate` lets the model update the status or description of a running background task while it is in flight. Trivial to add alongside the existing `BackgroundTaskRegistry`.

**`EnterPlanMode` / `ExitPlanMode`**
Sovrant already has a `plan` permission mode, but it is set by the user at startup (`--permission-mode plan`). These tools let the *model* signal a plan-mode transition mid-conversation — entering a read-only planning phase and exiting back to execution when ready. The runtime needs to honour the signal by toggling `_config.PermissionMode` (or a session-scoped override) when it sees these tool calls returned.

**`EnterWorktree` / `ExitWorktree`**
Creates a temporary git worktree so the agent performs all file edits on an isolated branch, then optionally commits and returns the branch name. Essential for safe autonomous multi-file coding work — the user's working tree is never touched until explicitly merged. Requires `git worktree add` / `git worktree remove` via `Bash` internally; the tool wraps this with session-scoped state tracking.

#### Tier 2 — Medium priority (MCP resource access + tool discovery)

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

1. `TaskUpdate` — add to `Sovrant.Tools/Tasks/`, register in `ToolRegistrar`, add unit test
2. `EnterPlanMode` / `ExitPlanMode` — add to `Sovrant.Tools/PlanMode/`; runtime handles the tool result by updating a session-scoped permission override
3. `EnterWorktree` / `ExitWorktree` — add to `Sovrant.Tools/Worktree/`; session-scoped worktree path stored in `ConversationRuntime` or a scoped service
4. `ListMcpResources` / `ReadMcpResource` — extend existing MCP client (`IMcpClient`) with a `ReadResourceAsync` method
5. `ToolSearch` — add deferred tool registry support to `IToolRegistry`; `ToolSearch` queries it by keyword
6. `SkillTool` — reads `.sovrant/skills/{name}.md` from disk and returns the content as a user message injection
7. `ScheduleCron` / `ConfigTool` / `LSPTool` — deferred; document as future work

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

### Phase 10 — Frontend SDK

**Goal:** A typed TypeScript/JavaScript client for `Sovrant.Server` that handles SSE streaming, session management, and tool event rendering.

#### Planned Features

- `SovrantClient` class: wraps `fetch` + SSE parsing
- `useChat()` React hook (mirrors Claude's `@anthropic-ai/sdk` pattern)
- Per-request credential injection (for Replit/browser use where the client holds the key)
- Built-in retry on transient errors
- Tool event callbacks: `onToolUse`, `onPermissionDenied`, `onError`

---

### Phase 11 — MCP Server Mode

**Goal:** Expose Sovrant as an MCP server so it can be consumed by Claude Code, other MCP clients, or composed into larger agent pipelines.

---

### Known Issues / Debt

| Issue | Priority | Notes |
|---|---|---|
| Token counts always `0↑ 0↓` | Medium | OpenAI streaming sends usage in the final chunk; the SSE parser needs to capture the `usage` field from the `[DONE]` predecessor chunk |
| `AskUserQuestion` blocked in server mode | Low | By design — no interactive console available over HTTP. Could be solved via a webhook/callback URL pattern |
| SmartRouter evicts a provider as unhealthy on first ping failure | Medium | Should retry a few times before marking unhealthy; add exponential backoff |
| No request-level timeout on agentic loop | Medium | A runaway tool loop can occupy a session indefinitely; add per-turn wall-clock timeout |
| CORS origins hardcoded | Low | Should be configurable via `SOVRANT_CORS_ORIGINS` env var |
