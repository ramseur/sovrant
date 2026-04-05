# Sovrant — Roadmap

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-04-04 (Phase 18+19 complete — Multi-Agent Backend & Team Tools)

This document tracks planned features, architectural decisions, and the reasoning behind them.

---

## Current State

The engine is fully functional as a single-user tool:

- CLI REPL and one-shot prompt mode
- Agentic loop with up to 20 tool rounds per turn
- 31 tools working on Windows and Linux (22 original + 5 Phase 7.5 Tier 1 + 4 Phase 7.5 Tier 2)
- Agent memory files (`~/.sovrant/memory.md` + `.sovrant/memory.md`) injected into every system prompt (Phase 7.6 item 1 ✅)
- Token counts reported correctly after each turn (Phase 7.6 item 2 ✅)
- Per-runtime mutable permission mode (`IPermissionModeAccessor`) for model-driven plan mode transitions
- JSONL session persistence
- SmartRouter with health/latency scoring across multiple providers
- HTTP server (`Sovrant.Server`) with OpenAI-compatible endpoints
- In-memory session pool (one `ConversationRuntime` per `session_id`, safe for multiple users)
- **OpenAI Responses API provider** (`LLM_WEB_SEARCH=true`) — routes through `POST /v1/responses`, injects `web_search_preview` built-in tool, suppresses the `WebSearch` function tool; full multi-turn + function tool support tested ✅
- `Sovrant.Agents` scaffolding — `IAgent`, `IMultiAgentSystem`, both backends, `AGENT_MODE` config switch (Phase 17.5 ✅)
- **Phase 7 hardening complete** ✅:
  - Context auto-compaction — LLM-based history summarisation when input tokens ≥ `SOVRANT_COMPACT_THRESHOLD` (default 80k); summary replaces old messages; compaction event persisted to JSONL
  - BashTool — 256 KB output cap per stream (stdout/stderr), strips `LD_PRELOAD`/`DYLD_INSERT_LIBRARIES`/`LD_LIBRARY_PATH` and related dangerous env vars from subprocess
  - WebFetchTool SSRF protection — blocks RFC-1918 private IPs, loopback, link-local, IPv6 ULA/link-local, `localhost` by name, and all non-HTTP(S) schemes
  - Provider retry — 3 attempts with 1 s/2 s/4 s backoff on 429/5xx and network errors
  - AgentTool recursion depth guard — `AsyncLocal<int>` counter; rejects at depth ≥ 5
  - ReadFileTool 10 MB cap — file size checked before reading
  - GlobTool 1000-result cap — truncation notice when exceeded
  - Atomic writes — `WriteFileTool` and `EditFileTool` write to `.tmp` then `File.Move` with overwrite

### Still pending (known gaps)

| Gap | Phase |
|---|---|
| Session TTL eviction + per-session lock | Phase 9.1 |
| Per-session config overlay (fixes global `EnterPlanMode`) | Phase 9.5 |
| ~~Multi-agent `DispatchAsync` + CLI/Server DI wiring~~ | ~~Phase 18–19~~ ✅ |

### Agent System: Current State

| Layer | Status | Notes |
|---|---|---|
| Single sub-agent (`AgentTool`) | ✅ Working | Spawns a fresh `ConversationRuntime` via DI, runs one isolated turn, returns text. Used by the LLM today. |
| Recursion depth guard | ✅ Done | `AsyncLocal<int>` counter; rejects at depth ≥ 5 with a clear error message. |
| Multi-agent interfaces (`IAgent`, `IMultiAgentSystem`) | ✅ Defined | `Sovrant.Agents` project. Both backends implement the same interface. |
| Modern backend (`InProcessMultiAgentSystem`) | ⚠️ Partial | `BaseAgent` channel/loop, `WorkspaceContext`, `AgentSystemFactory` all working. `MultiAgentCoordinator.DispatchAsync` is a stub — throws `NotImplementedException`. |
| Legacy backend (`ProcessBasedMultiAgentSystem`) | ⚠️ Partial | Scaffold only. `ProcessAgent.HandleAsync` and `RunTaskAsync` throw `NotImplementedException`. |
| Config switch (`AGENT_MODE`) | ✅ Working | `AgentSystemConfig.FromEnvironment()` + `AgentSystemFactory.Create()`. `AddMultiAgentSystem()` DI extension ready. |
| DI wiring in CLI / Server | ❌ Not wired | `services.AddMultiAgentSystem()` is not yet called in `Sovrant.Cli` or `Sovrant.Server`. `AgentTool` still uses the old direct `ConversationRuntime` path. |
| Team tools (`TeamCreate` / `TeamDelete` / `TeamStatus` / `TeamDelegate`) | ✅ Done | Phase 18. Uses `IMultiAgentSystem.RunTaskAsync` via `SovrantAgentFactory`. |
| V2 placeholders (`ITeamWorkspace`, `IArtifact`, `IMultiAgentCollaboration`) | ⚠️ Interfaces only | No implementations. Post-Phase 19. |

**What needs to happen before multi-agent is usable:**
1. ~~Phase 19 — implement `MultiAgentCoordinator.DispatchAsync` (modern backend)~~ ✅
2. ~~Phase 18 — `TeamCreateTool` / `TeamDeleteTool` + wire `IMultiAgentSystem` into DI~~ ✅
3. ~~Phase 19 — implement `ProcessAgent` / process backend~~ ✅

---

## Roadmap

### Phase 7.5 — Tool Parity with OpenClaude

**Goal:** Close the gap between Sovrant's 22 tools and the full OpenClaude tool set. A comparison of the OpenClaude source against Sovrant's `Sovrant.Tools` project identified 9 missing tools worth porting and 13 cloud/platform-only stubs that are not portable.

#### Tool comparison summary

| Category | Count | Tools |
|---|---|---|
| Implemented ✅ | 31 | Read, Write, Edit, Glob, Grep, LS, Bash, PowerShell, REPL, WebFetch, WebSearch, TaskCreate/Get/List/Output/Stop/Update, TodoWrite, Agent, AskUserQuestion, Sleep, NotebookEdit, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree, Skill, ToolSearch, ListMcpResources, ReadMcpResource |
| Missing — port ⬜ | 3 | ScheduleCron, ConfigTool, LSP |
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

1. ~~Agent memory files — extend `BuildSystemPrompt()` in `ConversationRuntime`; add `/memory` slash command~~ ✅ Done — `AppendMemoryFile()` helper reads both files; `/memory` and `/mem` commands registered; `InjectAsUserMessage` on `SlashCommandResult` wired into REPL
2. ~~Token count fix — update `CollectStreamEventsAsync` to capture OpenAI `usage` field from final SSE chunk~~ ✅ Done — `OpenAiCompatProvider` removed `yield break` on `finish_reason`; continues loop to capture trailing usage chunk; `ConversationRuntime` captures `InputTokens` from `MessageDelta`
3. Context auto-compaction ⬜ — add compaction logic to `RunTurnAsync`; add `SOVRANT_COMPACT_THRESHOLD` config; persist compaction events to JSONL
4. Expose token counts in REPL status line and `GET /v1/sessions/{id}` response ⬜

---

### Phase 7.6.5 — Native Model Web Search (OpenAI Responses API)

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

### Phase 7.7 — Security Hardening

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

### Phase 7.8 — Reliability & Safety

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

### Phase 7.9 — Robustness

**Source:** OpenClaude improvement document review (2026-04-04).

**Goal:** Eliminate edge-case data corruption and unbounded output in the remaining core tools.

#### Atomic file writes (`WriteFileTool` / `EditFileTool`)

**Current state:** Both tools call `File.WriteAllTextAsync(path, content)` directly. A crash or cancellation mid-write leaves a partial file at the destination path, corrupting the original.

**Fix:** Write to a temporary file in the same directory, then `File.Move(tmp, destination, overwrite: true)`. `File.Move` is atomic on POSIX (same filesystem). On Windows it is not formally atomic but is best-effort via a rename. This eliminates partial writes in the common case.

#### GlobTool result cap

**Current state:** No explicit cap on result count. On a very large repository (monorepos with 100K+ files) a `**/*` glob can produce an enormous result string.

**Fix:** Cap results at 1000 files. If truncated, append `[truncated: showing 1000 of N files]`.

#### Deferred — `ToolResult` structured record

The OpenClaude document suggests replacing the `string` return from `ITool.ExecuteAsync` with a structured `ToolResult` record carrying `Output`, `IsError`, and optional `Metadata`. This is a **breaking change** to the `ITool` interface and all 31 implementations. Defer to a dedicated Phase 10+ refactor when the tool surface has stabilised.

#### Implementation Plan

1. `WriteFileTool` — write to `{path}.tmp`, then `File.Move` with overwrite
2. `EditFileTool` — same: write full updated content to temp, then rename
3. `GlobTool` — cap `files` list at 1000; append truncation notice if exceeded

---

### Phase 8 — Structured Async Logging

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

### Phase 9 — Multi-Tenant Per-Request Credentials

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

### Phase 9.1 — Session Lifecycle Management (TTL Eviction + Turn Serialization)

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

### Phase 9.5 — Small Team Hardening

**Depends on:** Phase 9.1 (session TTL + per-session lock)

**Goal:** Make Sovrant solid for a small trusted team sharing a single deployment — a single bearer token is fine, but config changes must be session-scoped, each user should bring their own LLM key, and the server must be observable (token usage, context window) and resilient (rate limiting, usage tracking). No user login system required at this stage.

#### What works today for a team

Session history is already isolated per `session_id` — ten users with different session IDs have fully independent conversation histories and tool state. A shared `SOVRANT_TOKEN` is fine for a trusted team where everyone is known and has the token. This is sufficient for a team of up to ~50 engineers sharing an internal deployment.

#### What this phase adds

| Gap | Problem | Fix |
|---|---|---|
| `PUT /v1/config` is global | One user changing the model or permission mode (or calling `EnterPlanMode`) changes it for all sessions simultaneously | Session-scoped config overlay — each `SessionEntry` carries a `SessionConfig` that shadows the global defaults |
| Single `LLM_API_KEY` | All sessions bill to the same key — no per-session cost visibility | Phase 9 per-request `x_api_key` — each user or client supplies their own key in the request body |
| No per-session rate limiting | A single session can saturate the server with concurrent requests | Per-session request rate limiter (ASP.NET Core `RateLimiter` middleware) |
| No cost visibility | No way to see which session is responsible for LLM spend | Per-session token accumulation in `SessionEntry`; `GET /v1/usage` summary endpoint |
| No context window visibility | Users can't see how much context is consumed | `context_used_pct` and `tokens_remaining` in `GET /v1/sessions/{id}`; REPL status line |

#### Design

**Session-scoped config overlay**
`PUT /v1/config` currently mutates a single `MutableServerConfig` singleton — this is what makes `EnterPlanMode` global. Fix: each `SessionEntry` carries a `SessionConfig` (model, permission mode) that overlays the global defaults for that session only. Add `PUT /v1/sessions/{id}/config` for per-session overrides. The global `PUT /v1/config` remains available for operators to set server-wide defaults.

**Per-session rate limiting**
Use ASP.NET Core's built-in `RateLimiter` middleware keyed on `session_id`. Policy: N requests/minute per session (configurable via `SOVRANT_RATE_LIMIT_RPM`, default 60). Returns `429` with a `Retry-After` header.

**Usage tracking**
Each `SessionEntry` accumulates `TotalInputTokens` and `TotalOutputTokens` across all turns. Token count capture (Phase 7.6 prerequisite) must be fixed first. `GET /v1/usage` returns a per-session summary.

#### Implementation Plan

1. Add `SessionConfig` overlay to `SessionEntry` — model, permission mode; initialised from global `MutableServerConfig` defaults on session creation
2. Add `PUT /v1/sessions/{id}/config` endpoint; update `EnterPlanMode`/`ExitPlanMode` to write to `SessionConfig` rather than the shared singleton (fixes the global plan mode issue)
3. Add ASP.NET Core `RateLimiter` middleware keyed on `session_id`; `SOVRANT_RATE_LIMIT_RPM` env var
4. Fix token count capture (Phase 7.6 prerequisite); add `TotalInputTokens` / `TotalOutputTokens` to `SessionEntry`
5. Add `GET /v1/usage` endpoint — per-session token summary
6. Add `context_used_pct` and `tokens_remaining` to `GET /v1/sessions/{id}`; surface in REPL status line

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

### Phase 11 — CI/CD Pipeline Integration ✅

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

### Phase 12 — Slack / Webhook Integration ✅

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

### Phase 13 — Frontend SDK, Diff View, Session Export ✅

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

### Phase 14 — MCP Server Mode ✅

**Goal:** Expose Sovrant as an MCP server so it can be consumed by MCP clients or composed into larger agent pipelines.

**Status:** Complete. `sovrant mcp-server` subcommand activates stdio-based MCP server. Bridges all `IToolRegistry` tools + synthetic `chat` tool + session/config resources. `SOVRANT_MCP_TOOLS` env var for optional filtering. 20 tests. See [`docs/mcp-server.md`](mcp-server.md).

**Files added:**
- `src/Sovrant.McpServer/` — `McpServerSetup.cs`, `ChatToolHandler.cs`, `ToolFilter.cs`
- `tests/Sovrant.McpServer.Tests/` — `ToolBridgeTests.cs`, `ToolFilterTests.cs`, `ChatToolHandlerTests.cs`
- `docs/mcp-server.md` — full documentation with IDE config examples

---

### Phase 15 — IDE Extension (VS Code) ⏸️ Deferred (nice-to-have)

**Competitor precedent:** Claude Code ✅ · opencode ✅ (beta)
**Depends on:** Phase 14 (MCP server mode) — once Sovrant exposes an MCP server, MCP-aware IDEs (VS Code with GitHub Copilot, Cursor, Windsurf) can connect without a bespoke extension.
**Status:** Deferred. MCP-based IDE integration (Phase 14) covers the core use case. A native extension adds polish but is not required for the core product.

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
- Per-user token isolation is required in multi-tenant deployments (Phase 9 session pool key applies here too)

#### Implementation Plan

1. Add OAuth state machine to `IMcpClient` — `InitiateOAuthAsync`, `CompleteOAuthAsync`, `RefreshTokenAsync`
2. Implement `McpAuthTool` in `Sovrant.Tools/Mcp/`
3. Add encrypted credential store (`ICredentialStore`) with OS keychain backend for CLI and AES-GCM backend for server
4. Add callback endpoint to `Sovrant.Server` (`GET /v1/mcp/auth/callback`)
5. Surface the auth URL in SSE stream as a new `RuntimeEvent.AuthRequired` event type

---

### Phase 17.5 — Dual Agent Architecture (Scaffolding) ✅

**Source:** Dual Agent Architecture design document (2026-04-04).
**Status:** ✅ Complete — scaffolding done, full execution implemented in Phase 19.

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
  Legacy/
    ProcessAgent.cs                    ← IAgent backed by ProcessStartInfo; stdin/stdout stdio
    ProcessBasedMultiAgentSystem.cs    ← spawns ProcessAgent per task; AGENT_MODE=legacy
  Modern/
    BaseAgent.cs                       ← abstract IAgent with Channel<AgentTask> inbox + RunLoopAsync
    MultiAgentCoordinator.cs           ← routes tasks; per-task CTS; shutdown drain
    InProcessMultiAgentSystem.cs       ← wraps coordinator + WorkspaceContext; AGENT_MODE=modern (default)
    WorkspaceContext.cs                ← thread-safe ConcurrentDictionary scratch space for a run
  Config/
    AgentSystemConfig.cs               ← UseLegacyAgents bool; MaxConcurrentAgents; TaskTimeoutSeconds
    AgentSystemFactory.cs              ← static Create(config, services) → IMultiAgentSystem
  V2/
    IArtifact.cs                       ← placeholder: versioned content blob with ReadAsync
    ITeamWorkspace.cs                  ← placeholder: per-team artifact store
    IMultiAgentCollaboration.cs        ← placeholder: structured plan-code-review collaboration
  ServiceCollectionExtensions.cs       ← AddMultiAgentSystem(config?) reads AGENT_MODE env var
```

#### Configuration switch

| Mechanism | Effect |
|---|---|
| `AGENT_MODE=legacy` | `ProcessBasedMultiAgentSystem` (process-per-agent) |
| `AGENT_MODE=modern` or unset | `InProcessMultiAgentSystem` (default, recommended) |
| `AgentSystemConfig.UseLegacyAgents = true` | Legacy, programmatic override |

#### Fully implemented (Phase 19)

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
- V2 interfaces — `ITeamWorkspace`, `IArtifact`, `IMultiAgentCollaboration` remain placeholders

#### Also working

- `WorkspaceContext` — thread-safe `ConcurrentDictionary` variable store
- `BaseAgent` — channel construction, `EnqueueAsync`, `RunLoopAsync`, `Complete`
- `AgentSystemConfig.FromEnvironment()` — reads `AGENT_MODE`
- `AgentSystemFactory.Create` — correct backend selection
- `ServiceCollectionExtensions.AddMultiAgentSystem` — DI wiring (includes `ITeamRegistry` and `SovrantAgentFactory`)
- All interfaces and model records

---

### Phase 18 — Multi-Agent Teams (`TeamCreateTool` / `TeamDeleteTool`) ✅

**Depends on:** Phase 17.5 (`Sovrant.Agents` scaffolding — already done)

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

- Each team member is a full `ConversationRuntime` with its own session pool slot, tool set, and optionally its own LLM provider/key (Phase 9)
- The supervisor coordinates via `TaskOutput` polling or a lightweight message bus
- Team lifecycle: `TeamCreate` spawns and starts, `TeamDelete` cancels and evicts
- Teams are scoped to the supervisor's session — they are cleaned up when the supervisor session is evicted (Phase 9.1 TTL)

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

### Phase 19 — Dual Agent Architecture: Full Implementation ✅

**Depends on:** Phase 17.5 (scaffolding), Phase 18 (team tools that will consume `IMultiAgentSystem`)
**Status:** ✅ Complete — both backends implemented, team tools wired, 58 tests passing.

**Goal:** Complete the two multi-agent backends stubbed in Phase 17.5. At this point the `TeamCreateTool` / `TeamDeleteTool` from Phase 18 will be wired to `IMultiAgentSystem` and the choice of legacy vs. modern backend becomes a runtime configuration decision.

#### Option A completion: `ProcessBasedMultiAgentSystem`

1. `ProcessAgent.HandleAsync` — spawn child process from `ProcessStartInfo`; write task JSON to stdin; stream stdout line by line; parse structured tool-use blocks (same format as original OpenClaude); propagate `CancellationToken` via `Process.Kill()`
2. `ProcessBasedMultiAgentSystem.RunTaskAsync` — resolve target agent; create linked CTS; stream result incrementally; record CTS for `CancelTask`

#### Option B completion: `MultiAgentCoordinator.DispatchAsync`

1. Resolve target agent by `AgentTask.AssignedAgentName` or by `AgentRole` (planner → coder → reviewer pipeline)
2. For `BaseAgent` subtypes: enqueue via `EnqueueAsync`, pair with a `TaskCompletionSource<AgentResult>` registered by `RunLoopAsync`
3. For plain `IAgent` implementations: call `HandleAsync` directly on a `Task.Run` thread
4. Per-task linked CTS stored in `_taskCts`; `CancelTask` triggers it
5. `ShutdownAsync` — await all `BaseAgent.RunLoopAsync` background tasks

#### V2 implementations (deferred beyond Phase 19)

- `ITeamWorkspace` → `InMemoryTeamWorkspace` — `ConcurrentDictionary<string, IArtifact>`
- `IArtifact` → `StringArtifact` and `FileArtifact` concrete types
- `IMultiAgentCollaboration` → `PlanCodeReviewCollaboration` — planner + coder + reviewer pipeline sharing one `ITeamWorkspace`

#### Implementation Plan

1. Implement `ProcessAgent.HandleAsync` with stdin/stdout pipes and tool-use message parser
2. Implement `ProcessBasedMultiAgentSystem.RunTaskAsync` with full lifecycle management
3. Implement `MultiAgentCoordinator.DispatchAsync` and update `ShutdownAsync`
4. Wire `TeamCreateTool` to use `IMultiAgentSystem.RunTaskAsync` (replaces ad-hoc `ConversationRuntime` spawning in Phase 18)
5. Add integration tests: legacy backend with a mock echo process; modern backend with a test `BaseAgent` subclass
6. Implement V2 in-memory types when `IMultiAgentCollaboration` use cases materialise

---

### Phase 20 — Enterprise Auth & Multi-Tenancy

**Depends on:** Phase 9.5 (session-scoped config) + Phase 9 (per-request credentials)

**Goal:** Replace the single shared `SOVRANT_TOKEN` with per-user identity — named tokens, user-scoped session isolation, and per-user billing visibility. This is the gate to offering Sovrant as a product to external users or as a managed internal platform with login, not just a shared team tool.

#### When to implement

This phase is deliberately deferred. A small trusted team sharing a single deployment does not need per-user auth — everyone knows the token, sessions are already isolated by `session_id`, and Phase 9.5 handles config isolation and rate limiting. Add this phase when:
- External users (outside the trusted team) need access, **or**
- Per-user billing or audit trails are required, **or**
- Session deletion / config access needs to be restricted to the owning user

#### What changes

| Item | Change |
|---|---|
| Single `SOVRANT_TOKEN` | Replace with a named token registry: operator issues one opaque token per user. `SOVRANT_TOKENS` env var (JSON map) or a config file. No OAuth required at this stage. |
| Auth middleware | `BearerTokenMiddleware` resolves the token to a caller identity (`string CallerId`) instead of a boolean pass/fail |
| Session ownership | Each session is tagged with its `CallerId` on creation; only the owning caller can read, write, or delete it |
| `PUT /v1/config` | Global config changes restricted to tokens listed in `SOVRANT_ADMIN_TOKENS`; other callers get `403` |
| `GET /v1/usage` | Returns per-caller summary (requires Phase 9.5 token tracking as prerequisite) |
| `POST /v1/admin/tokens` | Optional: runtime token issuance without server restart |

#### Security model

- Tokens are opaque random strings (32+ bytes, base64url). No JWT required at this stage — JWTs add complexity without benefit when the server is the issuer and the verifier.
- The token registry is loaded at startup and can be reloaded via `POST /v1/admin/reload` without restart.
- Tokens must never appear in logs, session JSONL, or API error responses.

#### Implementation Plan

1. Add `ITokenRegistry` — maps `token → CallerId`; loaded from `SOVRANT_TOKENS` env var or config file
2. Update `BearerTokenMiddleware` to resolve `CallerId` and attach it to `HttpContext.Items`
3. Tag `SessionEntry` with `CallerId` on creation; enforce ownership on read/delete endpoints
4. Restrict `PUT /v1/config` to callers listed in `SOVRANT_ADMIN_TOKENS`
5. Extend `GET /v1/usage` to group by `CallerId`
6. Add `POST /v1/admin/reload` to hot-reload the token registry without restart

---

### Known Issues / Debt

| Issue | Priority | Notes |
|---|---|---|
| ~~Token counts always `0↑ 0↓`~~ | ~~Medium~~ | ✅ Fixed — `OpenAiCompatProvider` now captures trailing OpenAI usage chunk; `ConversationRuntime` reads `InputTokens` from `MessageDelta` |
| ~~SmartRouter crashes on WSL DNS failure~~ | ~~High~~ | ✅ Fixed — `SmartRouter` falls back to configured providers when all fail startup ping; `ConversationRuntime` catches `RouteAsync` exception and emits `RuntimeError` instead of crashing |
| `AskUserQuestion` blocked in server mode | Low | By design — no interactive console available over HTTP. Could be solved via a webhook/callback URL pattern |
| Provider has no retry on 429/5xx | Medium | `ApiError.Retryable` flag is set but never used. Addressed in Phase 7.8. |
| No request-level timeout on agentic loop | Medium | A runaway tool loop can occupy a session indefinitely; add per-turn wall-clock timeout |
| CORS origins hardcoded | Low | Should be configurable via `SOVRANT_CORS_ORIGINS` env var |
| `launchSettings.json` port conflicts with `SOVRANT_PORT` default | Low | `launchSettings.json` declares `5091`; Kestrel overrides to `5200`. Rapid restart or parallel test runs cause `SocketException (10048)`. Fix: align `launchSettings.json` with `SOVRANT_PORT`; add `--urls` CLI override for CI. |
| `EnterPlanMode`/`ExitPlanMode` are global in server mode | Medium | Server uses shared `MutableServerConfig` singleton — calling `EnterPlanMode` in one session sets plan mode for all sessions simultaneously. Fixed in Phase 9.5 step 2 (session-scoped `SessionConfig` overlay). |
