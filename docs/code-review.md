# Sovrant Code Review

**Date:** 2026-04-05 (Round 1) · 2026-04-12 (Round 2 — deep review) · 2026-04-14 (Round 3 — UX gap analysis) · 2026-04-29 (counts refreshed)
**Scope:** Full codebase — Runtime, Providers, Server, Tools (50), Agents, CLI, Desktop, Web, LSP, MCP, TypeScript SDK
**Build:** 1,492 tests passing, 0 warnings (10 test projects)
**Tag:** v1.3.0+ (Phase 78 Path 2 in progress)

---

## Executive Summary

| Severity | Round 1 | Round 2 (new) | Total |
|----------|---------|---------------|-------|
| **CRITICAL** | 9 | 10 | **19** |
| **HIGH** | 24 | 20 | **44** |
| **MEDIUM** | 58 | 18 | **76** |
| **LOW** | 38 | 12 | **50** |
| **TOTAL** | **129** | **60** | **189** |

**Round 1 top priorities (all COMPLETE — Phases A–F):**
1. ~~SSRF via `X-LLM-Base-Url`~~ — fixed
2. ~~Command injection in BashTool, PowerShellTool, ReplTool, TaskCreateTool~~ — fixed
3. ~~Rate limiting bypass~~ — fixed
4. ~~CancellationTokenSource leaks~~ — fixed
5. ~~Missing request timeouts~~ — fixed

**Round 2 top priorities (NEW):**
1. MCP Server exposes all sessions without owner filtering (`ownerUserId: null`)
2. Missing authorization checks in ProjectRoutes — no HTTP-layer permission enforcement
3. Command injection in EnterWorktreeTool/ExitWorktreeTool — same `EscapeArg` pattern as original 1.2–1.5
4. Race condition in SmartRouter `ProviderInfo` state updates — non-atomic counter increments
5. useChat React hook never recreates client when options change — stale credentials
6. Unhandled fire-and-forget task exceptions in Web/Desktop/LSP startup paths
7. ETagMiddleware buffers entire response body — memory exhaustion on large/streaming responses

---

## 1. CRITICAL Issues

### 1.1 SSRF via X-LLM-Base-Url Header
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/ChatRoutes.cs:52,60-66`
**Problem:** User-supplied `X-LLM-Base-Url` header is used to construct `HttpClient.BaseAddress` without validation. An attacker can point the server at internal network endpoints (`http://169.254.169.254/`, `http://192.168.1.1/admin`, etc.), bypassing firewalls.
**Fix:** Validate URL scheme (https-only in production), reject private/reserved IP ranges, or whitelist allowed base URLs.

### 1.2 Command Injection — BashTool
**Layer:** Tools
**File:** `src/Sovrant.Tools/Core/BashTool.cs:57,108`
**Problem:** `EscapeArg()` only escapes double quotes. Shell metacharacters (`$()`, backticks, `&&`, `||`, `;`, pipes) are not escaped. Commands are passed via `-c "{escaped}"` which is vulnerable to breakout.
**Fix:** Use `ProcessStartInfo.ArgumentList` to avoid shell interpretation entirely, or implement POSIX-compliant shell escaping.

### 1.3 Command Injection — PowerShellTool
**Layer:** Tools
**File:** `src/Sovrant.Tools/Extended/PowerShellTool.cs:45`
**Problem:** Same pattern as BashTool — `Replace("\"", "\\\"")` is insufficient for PowerShell's multiple quoting mechanisms and special variables.
**Fix:** Use `-EncodedCommand` with Base64-encoded script, or `ProcessStartInfo.ArgumentList`.

### 1.4 Command Injection — ReplTool
**Layer:** Tools
**File:** `src/Sovrant.Tools/Extended/ReplTool.cs:69`
**Problem:** REPL code passed through shell string composition. Python, Node.js, and other interpreters can execute arbitrary code through various injection techniques.
**Fix:** Write code to a temp file and execute the file, or use subprocess APIs with argument arrays.

### 1.5 Command Injection — TaskCreateTool
**Layer:** Tools
**File:** `src/Sovrant.Tools/Tasks/TaskCreateTool.cs:63,74`
**Problem:** Background task spawning uses the same inadequate escaping across Windows (`cmd.exe`/PowerShell) and Unix shells.
**Fix:** Use `ProcessStartInfo` with separate `FileName`/`Arguments` rather than building a command string.

### 1.6 Rate Limiting Bypass
**Layer:** Server
**File:** `src/Sovrant.Server/Program.cs:109-125`
**Problem:** Rate limit key falls back to `RemoteIpAddress` then `"anonymous"`. Attackers can bypass by omitting `X-Session-Id` and spoofing IPs. The `"anonymous"` bucket is shared across all unauthenticated clients.
**Fix:** Require `X-Session-Id` for chat endpoints. Never fall back to a shared key. Use connection IP (not forwarded headers) as the default key.

### 1.7 Auth Check Logic / No Startup Validation
**Layer:** Server
**File:** `src/Sovrant.Server/Auth/BearerTokenMiddleware.cs:30-32`
**Problem:** If `SOVRANT_TOKEN` is empty or not set, all requests are rejected (correct behavior but confusing logic). The real issue is the server starts successfully without a token configured — it should fail at startup.
**Fix:** Validate `SOVRANT_TOKEN` is set and non-empty at startup. Throw `InvalidOperationException` if missing.

### 1.8 Unhandled Exceptions in Chat Route
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/ChatRoutes.cs:149-164`
**Problem:** No try-catch around `StreamResponseAsync`/`BufferedResponseAsync`. Runtime exceptions propagate as raw 500 errors, potentially leaking internal details. SSE streams may be left in inconsistent state.
**Fix:** Add catch block with consistent error formatting. For SSE, emit an error event before closing the stream.

### 1.9 OAuth Callback CSRF Concerns
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/McpAuthRoutes.cs:10-13`
**Problem:** OAuth callback endpoint is publicly accessible (`.AllowAnonymous()` — correct for OAuth). CSRF state token validation exists but its strength depends on the implementation in `McpOAuthService`.
**Fix:** Verify state tokens are cryptographically random, stored server-side with expiration, and validated on callback. Add integration test for CSRF flow.

---

## 2. HIGH Issues

### 2.1 CancellationTokenSource Leak — OrchestrationCoordinator
**Layer:** Agents
**File:** `src/Sovrant.Agents/Shared/OrchestrationCoordinator.cs:69-98`
**Problem:** `linkedCts` (from `CreateLinkedTokenSource`) is stored in `_taskCts` but never explicitly disposed. `TryRemove` doesn't call `Dispose()`.
**Fix:**
```csharp
finally {
    if (_taskCts.TryRemove(task.Id, out var cts))
        cts.Dispose();
}
```

### 2.2 CancellationTokenSource Leak — ProcessBasedOrchestrationSystem
**Layer:** Agents
**File:** `src/Sovrant.Agents/Isolated/ProcessBasedOrchestrationSystem.cs:114-119`
**Problem:** Race condition between `Dispose()` iterating `_taskCts` and `RunTaskAsync()` adding new entries.
**Fix:** Add locking or use a disposed flag to reject new tasks during shutdown.

### 2.3 Unsafe Dictionary — OrchestrationCoordinator
**Layer:** Agents
**File:** `src/Sovrant.Agents/Shared/OrchestrationCoordinator.cs:17-18,42`
**Problem:** `_agents` is a plain `Dictionary<string, IAgent>` without synchronization. Concurrent `AddAgent()` and `DispatchAsync()` calls cause `InvalidOperationException`.
**Fix:** Use `ConcurrentDictionary<string, IAgent>`.

### 2.4 Dead Code — Discarded Agent Results in BaseAgent
**Layer:** Agents
**File:** `src/Sovrant.Agents/Shared/BaseAgent.cs:64`
**Problem:** `_ = await HandleAsync(task, ct)` — agent results from `RunLoopAsync` are discarded. The inbox/channel infrastructure is unused since `DispatchAsync` calls `HandleAsync` directly.
**Fix:** Either implement result-passing mechanism or remove dead `RunLoopAsync`/`EnqueueAsync` code.

### 2.5 Coordinator Not Disposed
**Layer:** Agents
**File:** `src/Sovrant.Agents/Shared/InProcessOrchestrationSystem.cs:17`
**Problem:** `DisposeAsync()` calls `ShutdownAsync()` but never disposes the `OrchestrationCoordinator` (which owns a `SemaphoreSlim`).
**Fix:** Call `_coordinator?.Dispose()` in `DisposeAsync()`.

### 2.6 ProcessAgent — No ProcessStartInfo Validation
**Layer:** Agents
**File:** `src/Sovrant.Agents/Isolated/ProcessAgent.cs:35-51`
**Problem:** `ProcessStartInfo` is accepted without validating `FileName` or `Arguments`. Untrusted config can lead to arbitrary code execution.
**Fix:** Validate `FileName` against an allowlist. Document that `ProcessAgent` must only be used with trusted inputs.

### 2.7 TeamDelegateTool — HashSet Race Condition
**Layer:** Tools
**File:** `src/Sovrant.Tools/Team/TeamDelegateTool.cs:26,55`
**Problem:** `_initialized` is a plain `HashSet<string>`. Concurrent delegations to the same member can race on `.Add()`, causing duplicate agent registration.
**Fix:** Use `ConcurrentDictionary<string, byte>` or wrap with `lock`.

### 2.8 BackgroundTaskInfo — Public Mutable OutputBuffer
**Layer:** Tools
**File:** `src/Sovrant.Tools/Tasks/BackgroundTaskInfo.cs:29`
**Problem:** `OutputBuffer` (StringBuilder) is public and mutable. External code can access without locking despite TaskCreateTool using locks internally.
**Fix:** Make `OutputBuffer` internal. Provide thread-safe accessor methods only.

### 2.9 Blocking Dispose — AsyncRollingFileLoggerProvider
**Layer:** Runtime
**File:** `src/Sovrant.Runtime/Logging/AsyncRollingFileLoggerProvider.cs:39`
**Problem:** `Dispose()` calls `_writerTask.GetAwaiter().GetResult()` blocking the calling thread on shutdown.
**Fix:** Use `DisposeAsync()` exclusively or implement non-blocking cleanup.

### 2.10 Fire-and-Forget RecheckAsync — SmartRouter
**Layer:** Providers
**File:** `src/Sovrant.Api/Routing/SmartRouter.cs:145`
**Problem:** `_ = RecheckAsync(...)` with `CancellationToken.None`. The recheck task can't be cancelled on shutdown and failures are silently lost.
**Fix:** Track the task for shutdown. Pass a proper cancellation token.

### 2.11 Credential Logging — McpOAuthService
**Layer:** Runtime
**File:** `src/Sovrant.Runtime/Mcp/McpOAuthService.cs:94,125`
**Problem:** OAuth state values are partially logged (`state[..8] + "..."`). Even truncated values leak information.
**Fix:** Use opaque session IDs for logging instead of actual state values.

### 2.12 Unhandled Exception in ProviderApiProvider Stream
**Layer:** Providers
**File:** `src/Sovrant.Api/Providers/ProviderApiProvider.cs:83`
**Problem:** `response.EnsureSuccessStatusCode()` inside async enumerable without try-catch. Exception propagates in iterator context.
**Fix:** Wrap in try-catch and yield appropriate error events.

### 2.13 No Audit Logging for Credential Changes
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/ConfigRoutes.cs:37-38`
**Problem:** `PUT /v1/config` with `api_key` field has no audit trail.
**Fix:** Log credential changes with timestamps and key fingerprints.

### 2.14 Missing Input Validation — Model/Provider Names
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/ConfigRoutes.cs:34-35,47-52`
**Problem:** No validation of model or provider name format. Invalid names break downstream code.
**Fix:** Validate against pattern/whitelist before assignment.

### 2.15 Fire-and-Forget Webhook Callback
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/WebhookRoutes.cs:108`
**Problem:** `_ = callbackService.DeliverAsync(...)` with `CancellationToken.None`. Lost on shutdown.
**Fix:** Use `IHostedService` or background task queue with proper lifecycle.

### 2.16 No Timeout on Webhook HTTP Requests
**Layer:** Server
**File:** `src/Sovrant.Server/Webhooks/WebhookCallbackService.cs:40-45`
**Problem:** No `HttpClient.Timeout` set. Malicious webhook URL that never responds ties up resources indefinitely.
**Fix:** Set `client.Timeout = TimeSpan.FromSeconds(10)`.

### 2.17 CORS Too Permissive
**Layer:** Server
**File:** `src/Sovrant.Server/Program.cs:87-100`
**Problem:** `.AllowAnyMethod()` and `.AllowAnyHeader()` even though only specific methods/headers are needed.
**Fix:** Explicitly list `GET, POST, PUT, DELETE, OPTIONS` and `Content-Type, Accept, Authorization, X-Session-Id, X-LLM-Api-Key, X-LLM-Base-Url`.

### 2.18 Weak Response Type Safety — SDK
**Layer:** TypeScript SDK
**File:** `sdk/js/src/client.ts:227,243,275`
**Problem:** `getModels()`, `getSession()`, `getUsage()` return `Promise<unknown>` or `Record<string, unknown>`.
**Fix:** Define `ModelsResponse`, `SessionResponse`, `UsageResponse` interfaces in `types.ts`.

### 2.19 No Request Timeout — SDK
**Layer:** TypeScript SDK
**File:** `sdk/js/src/client.ts:332`
**Problem:** `fetch()` call has no timeout. Non-streaming requests can hang indefinitely.
**Fix:** Add `AbortController` with configurable timeout (default 30s). Make configurable via `SovrantClientOptions.timeout`.

### 2.20 Missing Functional Test Suite — SDK
**Layer:** TypeScript SDK
**File:** `sdk/js/tests/`
**Problem:** All tests are security-focused. No happy-path tests for `chat()`, `stream()`, session management, retry exhaustion, or tool event dispatching.
**Fix:** Add `client-functional.test.ts` and `hook-functional.test.ts`.

### 2.21 Code Duplication — Parameter Extraction Helpers
**Layer:** Tools
**File:** All 49 tool files
**Problem:** Every tool reimplements `GetString()`, `GetInt()`, `GetBool()`, `GetDouble()` helpers.
**Fix:** Create shared `JsonElementExtensions` with these as extension methods.

### 2.22 Code Duplication — Process Execution Pattern
**Layer:** Tools
**File:** BashTool, PowerShellTool, ReplTool, TaskCreateTool, EnterWorktreeTool, ExitWorktreeTool
**Problem:** Process spawning, output capture, and error handling logic duplicated 6+ times.
**Fix:** Extract `ProcessExecutor` utility class.

### 2.23 Code Duplication — Schema Creation
**Layer:** Tools
**File:** All 49 tool files
**Problem:** Each tool manually constructs a static `ToolDefinition` with hand-written JSON schema. Error-prone and violates DRY.
**Fix:** Implement schema builder pattern or reflection-based generation.

### 2.24 GrepTool — Silently Swallowed Exceptions
**Layer:** Tools
**File:** `src/Sovrant.Tools/Core/GrepTool.cs:87-88`
**Problem:** `IOException` and `UnauthorizedAccessException` silently skipped with `/* skip unreadable files */`. Hides legitimate permission errors.
**Fix:** Track and report skipped file count.

---

## 3. MEDIUM Issues

### Server Layer

| # | File | Issue |
|---|------|-------|
| M1 | `ChatRoutes.cs:137-138` | Per-request model override mutates global `serverConfig.Model` — race condition |
| M2 | `ChatRoutes.cs`, `SessionRoutes.cs` | No validation of session ID format (length, characters) |
| M3 | `SessionRoutes.cs:54,90,110` | Race between `TryGetConfig` check and use — session could be evicted |
| M4 | Multiple routes | Inconsistent error response format (JSON vs HTML vs plain text) |
| M5 | `SseWriter.cs:16-21` | Missing `X-Content-Type-Options: nosniff` security header |
| M6 | `ChatRoutes.cs:229-274` | No timeout on buffered response — hung LLM provider blocks indefinitely |
| M7 | `WebhookRoutes.cs:50-60` | Callback URL not validated against reserved IP ranges (DNS rebinding) |
| M8 | `ConfigRoutes.cs:40-41` | `BaseUrl` assigned without `Uri.TryCreate` validation |

### Runtime + Providers Layer

| # | File | Issue |
|---|------|-------|
| M9 | `ConversationRuntime.cs:406,410,414` | `JsonDocument.Parse()` results not disposed (use `using`) |
| M10 | `SmartRouter.cs:81,114,129` | Linear scan of `_providers` — use `Dictionary` for O(1) lookup |
| M11 | `AsyncRollingFileLoggerProvider.cs:24-28` | Bounded channel with `DropOldest` silently drops log messages |
| M12 | `SessionConfig.cs:12-13` | `volatile` on reference types doesn't guarantee atomicity of compound operations |
| M13 | `MutableCliPermissionPolicy.cs:11` | `volatile PermissionMode` — race between check and use |
| M14 | `SmartRouter.cs:20` | `volatile string?` — subsequent comparison and use are not atomic |
| M15 | `RuntimeSessionPool.cs:68-75` | Race in session creation — loser's lock could be disposed while in use |
| M16 | `McpOAuthService.cs:119,145` | Lock held during `PurgeExpiredStates()` — contention under load |
| M17 | `ConversationRuntime.cs:492-495` | Broad `catch (Exception)` swallows critical exceptions in compaction |
| M18 | `DefaultToolExecutor.cs:120-122` | Temp files with predictable names — path disclosure risk |
| M19 | `SmartRouter.cs:117-119` | Error reveals all configured provider names |
| M20 | `RuntimeSessionPool.cs:61-62` | Session ID `persistenceId` not validated for filesystem safety |
| M21 | `DefaultToolExecutor.cs:49` | `toolName` not validated before use in file path construction |

### Tools Layer

| # | File | Issue |
|---|------|-------|
| M22 | `SkillTool.cs:37-40` | Path traversal check incomplete — doesn't handle symlinks or 8.3 names |
| M23 | `WebSearchTool.cs:59,98` | API keys passed in headers without log scrubbing |
| M24 | Multiple tools | Inconsistent error message formatting (`"Error: ..."` vs contextual) |
| M25 | Multiple tools | Inconsistent `CancellationToken` handling across sync tools |
| M26 | `GlobTool.cs:43-46` | `File.GetLastWriteTimeUtc()` per file — O(n) stat calls for sorting |
| M27 | `ReadFileTool.cs:40-49` | Entire file loaded into memory before checking limit |
| M28 | `GrepTool.cs:46` | No regex pattern complexity validation — ReDoS risk |
| M29 | All tools | No input validation against JSON schemas — schemas defined but not enforced |
| M30 | Multiple tools | Inconsistent return types (JSON, plain text, formatted tables) |
| M31 | `GrepTool.cs:46` | Hardcoded 5-second regex timeout — not configurable |
| M32 | All tools | No global timeout enforcement at registry level |
| M33 | `WebSearchTool.cs`, `WebFetchTool.cs` | No client-side rate limiting for external API calls |
| M34 | ReadFileTool, WriteFileTool, EditFileTool | No restriction of file operations to project boundaries |
| M35 | `WorktreeState.cs:5-6,12-16` | `volatile string?` — compound check-then-use is not atomic |

### Agents + CLI Layer

| # | File | Issue |
|---|------|-------|
| M36 | `WorkspaceContext.cs:10-36` | No agent isolation — all agents share same workspace key space |
| M37 | `OrchestrationCoordinator.cs:70-86` | Semaphore held for entire `HandleAsync` duration — starves other agents |
| M38 | `OrchestrationCoordinator.cs:113-136` | `ShutdownAsync` iterates `_taskCts.Values` without snapshot — concurrent modification |
| M39 | `TeamMemberInfo.cs:20-22` | Mutable `Status`/`LastOutput`/`LastError` without thread safety |
| M40 | `TeamMemberInfo.cs:11-17` | No input validation on `Id`, `Name`, `SystemPrompt` |
| M41 | `BaseAgent.cs:60-66` | Unhandled exception in `RunLoopAsync` kills agent silently |
| M42 | `Program.cs:69-96` | Exit codes only set in CI mode — interactive mode always exits 0 |
| M43 | `BaseAgent.cs:15-20` | Unbounded channel — no backpressure, memory exhaustion risk |
| M44 | `SovrantAgentFactory.cs:62-66` | Tool filter created at agent creation — doesn't update if registry changes |

### TypeScript SDK Layer

| # | File | Issue |
|---|------|-------|
| M45 | `client.ts:104-119` | Missing response validation — malformed responses return empty string silently |
| M46 | `client.ts:330-358` | Retry logic: fixed delays, no jitter, no exponential backoff beyond hardcoded values |
| M47 | `client.ts:362-372` | Flat error hierarchy — single `SovrantApiError` for all failure modes |
| M48 | `client.ts:165-169` | Streaming errors lack context (partial text, bytes received, attempt number) |
| M49 | `client.ts:105-128` | Per-call options object defined inline 4 times — should be shared interface |
| M50 | `hooks/use-chat.ts:122-154` | Object cloning on every state update — GC pressure with large history |
| M51 | `types.ts:136-148` | StreamCallbacks can't pause, retry, or cancel a stream |
| M52 | `client.ts:81-82` | No format validation of `llmApiKey` value |
| M53 | `client.ts` | No request/response logging support for debugging |
| M54 | `client.ts` | No client-side rate limiter or circuit breaker |
| M55 | `tsconfig.json` / `package.json` | `type-check` not run as part of `build` script |
| M56 | `tests/` | Only 1 retry test — doesn't cover 5xx, network errors, maxRetries exhaustion |
| M57 | `tests/` | No integration tests against mock HTTP server (MSW/nock) |
| M58 | `tests/hook-security.test.ts` | Only 4 tests — no functional coverage for `send()`, clearing, concurrent sends |

---

## 4. LOW Issues

| # | Layer | File | Issue |
|---|-------|------|-------|
| L1 | Runtime | `ConversationRuntime.cs:204,384` | Unnecessary `.ToList()` materialization |
| L2 | Runtime | `ConversationRuntime.cs:456-457` | String concatenation in LINQ Select hot path |
| L3 | Runtime | `ConversationRuntime.cs:266` | `s_retryDelaysMs` array partially used |
| L4 | Runtime | `ConversationRuntime.cs:567` | `IOException` silently ignored in `AppendMemoryFile()` |
| L5 | Runtime | `SessionConfig.cs:13` | Magic value `-1` for "use global default" — unclear |
| L6 | Runtime | `McpOAuthService.cs:36-38` | Pending OAuth states not cleared on shutdown |
| L7 | Runtime | `McpOAuthService.cs:80-90` | No validation of OAuth ClientId/Secret format |
| L8 | Runtime | `McpOAuthService.cs:64` | `new HttpClient()` instead of injected — should use factory |
| L9 | Providers | `OpenAiCompatProvider.cs:114,120` | Missing null check on `chunk.Choices` |
| L10 | Providers | `IAuthProvider.cs:14` | No `ConfigureAwait(false)` enforcement guidance |
| L11 | Server | Various routes | Unnecessary `.ToList()` allocations in hot paths |
| L12 | Server | `ConfigRoutes.cs:43-44` | Invalid enum values silently ignored — no warning log |
| L13 | Server | `WebhookRoutes.cs:69` | `webhook:{source}:{userId}` prefix could collide with user sessions |
| L14 | Server | `Program.cs` | No explicit request body size limit configured |
| L15 | Server | Multiple routes | Missing `X-Content-Type-Options` header |
| L16 | Server | `BearerTokenMiddleware.cs` | No logging of successful authentication |
| L17 | Tools | `PowerShellTool.cs:100` | Schema includes unused "description" property |
| L18 | Tools | `NotebookEditTool.cs:88-91` | `newSource` not validated for null/empty on cell insert |
| L19 | Tools | `BashTool.cs:13` | Output cap 256 KB hardcoded — not configurable |
| L20 | Tools | Multiple task/worktree tools | Multiple `DateTime.UtcNow` calls without caching |
| L21 | Tools | `GlobTool.cs:52` | `string.Join` for 1000+ files — use StringBuilder |
| L22 | Tools | `EnterWorktreeTool.cs:83-84` | `EscapeArg` only quotes spaces — misses shell metacharacters |
| L23 | Tools | Multiple tools | Vague error messages without remediation guidance |
| L24 | Tools | `NotebookEditTool.cs:2` | Potentially unused `using` statement |
| L25 | Agents | `Program.cs:288-316` | No default case in event switch — new events silently ignored |
| L26 | Agents | `ServiceCollectionExtensions.cs:33-34` | Shared backend singletons registered even in isolated mode |
| L27 | Agents | `Program.cs:257-283` | CLI REPL has no input length validation |
| L28 | Agents | `ProcessAgent.cs:64` | New process per task — no pooling |
| L29 | SDK | `sse.ts:74` | `safeJsonParse` exported but may be internal |
| L30 | SDK | `client.ts:145` | String concatenation in stream loop — use array + join |
| L31 | SDK | `client.ts:259` | `exportSession` default format could surprise consumers |
| L32 | SDK | `client.ts:201-230` | Method naming inconsistency (`getConfig` vs `listSessions`) |
| L33 | SDK | `types.ts`, `client.ts` | No deprecation support for future field removal |
| L34 | SDK | `package.json` | React hook export pattern could be documented better |
| L35 | SDK | `sse.ts:64-66` | Malformed SSE chunks silently skipped — no error callback |
| L36 | SDK | `client.ts:284` | `health()` unauthenticated — correct but undocumented |
| L37 | SDK | `client.ts:369` | URL included in error message — could leak if query params added |
| L38 | SDK | No `README.md` in `sdk/js/` | Missing SDK usage documentation |

---

## 5. Recommended Fix Order

### Phase A — Critical Security (do first) ✅ COMPLETE (cd3a548)

| # | Issue | Effort | Risk if Unfixed |
|---|-------|--------|-----------------|
| 1.1 | SSRF via X-LLM-Base-Url | Medium | Attackers probe internal network |
| 1.2-1.5 | Command injection (4 tools) | Medium | Arbitrary code execution |
| 1.6 | Rate limiting bypass | Low | DoS / abuse |
| 1.7 | Startup token validation | Low | Confusing failure mode |
| 1.8 | Unhandled exceptions in chat | Low | Information leakage |

### Phase B — Resource Leaks & Thread Safety (high stability impact) ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed |
|---|-------|--------|-----------------|
| 2.1-2.2 | CTS leaks — dispose on TryRemove | Low | Memory leak over time |
| 2.3 | Unsafe Dictionary → ConcurrentDictionary | Low | `InvalidOperationException` crash |
| 2.5 | Coordinator disposed in DisposeAsync | Low | Semaphore leak |
| 2.7 | HashSet → ConcurrentDictionary | Low | Duplicate agent registration |
| 2.9 | Blocking dispose → bounded Wait(2s) | Low | Shutdown hang |
| 2.10 | Fire-and-forget → tracked + cancellable | Low | Delayed shutdown |
| M1 | Global config mutation → session-only | Medium | Non-deterministic model selection |

### Phase C — Validation & Hardening ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed |
|---|-------|--------|-----------------|
| 2.14 | Model/provider/URL validation in ConfigRoutes | Low | Runtime crashes |
| 2.17 | CORS restricted to explicit methods+headers | Low | Reduced defense-in-depth |
| M2 | Session ID validated (regex, 1-128 chars) across all routes | Low | Filesystem issues |
| M20 | Session persistence GetFullPath containment check | Low | Path traversal |
| M21 | Tool name sanitized before temp file path construction | Low | Path traversal in temp files |
| M22 | SkillTool: invalid filename chars check + GetFullPath containment | Low | File access bypass |

### Phase D — Code Quality & DRY (maintainability) ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed |
|---|-------|--------|-----------------|
| 2.21 | JsonElementExtensions — shared GetStringProp/GetIntProp/GetBoolProp (35 files) | Medium | Maintenance burden |
| 2.22 | ProcessExecutor — shared RunAsync/RunWithTempFileAsync (BashTool, PowerShellTool, ReplTool) | Medium | Duplicated bug fixes |
| 2.23 | Schema builder — **deferred** (High effort, current approach functional) | High | Error-prone schema definitions |
| 2.4 | Removed dead BaseAgent inbox/channel/RunLoopAsync/EnqueueAsync code | Low | Confusing architecture |

### Phase E — SDK Improvements ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed |
|---|-------|--------|-----------------|
| 2.19 | AbortController-based request timeout (default 120s, configurable) | Low | Hanging requests |
| 2.18 | Typed responses: ModelsResponse, SessionDetail, SessionListResponse, UsageSummary | Medium | Type-unsafe consumption |
| 2.20 | Functional test suite — **deferred** (requires Node.js) | High | Regression risk |
| M46 | Exponential backoff with ±25% jitter on retries | Low | Thundering herd |
| M47 | Error hierarchy: SovrantAuthError, SovrantRateLimitError, SovrantTimeoutError | Medium | Poor error handling |
| M49 | Shared ChatCallOptions interface replacing 4 inline option types | Low | DRY |

### Phase F — Performance & Polish ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed |
|---|-------|--------|-----------------|
| M10 | Provider lookup via Dictionary for O(1) name resolution | Low | Slightly slower routing |
| M26 | GlobTool: skip stat calls when matches exceed 1000 (return unsorted) | Low | Slow on large directories |
| M27 | ReadFileTool: stream lines instead of ReadAllLinesAsync (constant memory) | Medium | Memory spike on large files |
| M37 | Semaphore contention — **deferred** (configurable MaxConcurrentAgents suffices) | Medium | Agent starvation |
| L-series | Various minor items — **deferred** | Low each | Polish |

---

## 6. Architecture Notes

### What's Working Well
- **Session isolation** via composite key (`{session_id}::{provider}`) is a solid multi-tenant pattern
- **Permission system** with session-scoped overrides is well-designed
- **Provider router** with health checks and scoring provides good resilience
- **JSONL session persistence** is simple and effective for the append-log pattern
- **Token redaction** in SDK's `toJSON()` and error messages shows security awareness
- **Tool registration** via DI is clean and extensible

### Systemic Patterns to Address
1. **`volatile` misuse** — appears in 4+ files. Consider a `ThreadSafe<T>` wrapper or switch to `lock`/`Interlocked`
2. **Fire-and-forget tasks** — 3+ locations use `_ = SomeAsync()`. Need a tracked background task pattern
3. **Process execution** — 6 tools duplicate the spawn-capture-timeout pattern. Single `ProcessExecutor` fixes all
4. **Input validation** — most server endpoints accept user strings without format/length validation. Consider a shared validation layer or middleware
5. **Error response format** — routes return JSON objects, HTML pages, and plain text. Standardize on `{ "error": string, "code"?: string }`

---

## 7. Code Coverage Report

**Generated:** 2026-04-05 | **Test count:** 329 | **Tool:** XPlat Code Coverage + ReportGenerator

### Overall

| Metric | Value |
|--------|-------|
| Line coverage | **30.8%** (4,969 / 16,112) |
| Branch coverage | **24.4%** (856 / 3,496) |
| Method coverage | **34.6%** (577 / 1,664) |
| Full method coverage | 27.2% (454 / 1,664) |
| Assemblies | 9 |
| Classes | 258 |

### Per-Assembly Breakdown

| Assembly | Line Coverage | Notes |
|----------|-------------|-------|
| **Sovrant.Agents** | **59.6%** | Best covered — OrchestrationCoordinator 91%, InMemoryTeamRegistry 100%, AgentPrompts 100% |
| **Sovrant.Runtime** | **48.6%** | RuntimeSessionPool 99%, SovrantConfig 92%, FilteredToolRegistry 100%; gaps in logging, MCP, prompt builder |
| **Sovrant.Commands** | **41.1%** | SlashCommandDispatcher 83%, TokenUsageTracker 100%; HelpCommand, MemoryCommand, SessionCommand at 0% |
| **Sovrant.Api** | **33.0%** | FormatConverter 89%, ProviderInfo 93%; Responses API types and Ollama provider uncovered |
| **Sovrant.Mcp** | **31.6%** | ToolFilter 100%, McpTokenValidator 100%; McpServerSetup 8% |
| **Sovrant.Tools** | **25.4%** | ProcessExecutor 82%, TeamDelegateTool 72%; 18 tools at 0% (Glob, Grep, WebFetch, REPL, PowerShell, etc.) |
| **Sovrant.Lsp** | **19.3%** | LspClientManager 63%; LspClient 11%, JsonRpc transport 0% |
| **Sovrant.Server** | **3.9%** | Only webhook classes covered (100%); all routes, middleware, auth at 0% |
| **sovrant (CLI)** | **0%** | No unit tests — requires integration testing |

### Key Gaps (Priority Order)

1. **Server routes (0%)** — ChatRoutes, SessionRoutes, ConfigRoutes have zero coverage. These are the primary API surface and contain the security fixes from Phases A-C.
2. **Core tools (0%)** — GlobTool, GrepTool, ListDirectoryTool, WebFetchTool have no tests. These are high-usage tools.
3. **CLI entry point (0%)** — Expected for a console host; integration tests recommended.
4. **LSP transport (0%)** — JsonRpc layer untested; fragile protocol code.
5. **Input validation (0%)** — `InputValidation` class (Phase C addition) has no dedicated tests.

### Well-Covered Areas

- **OrchestrationCoordinator** — 91.4% (Phase 19 core)
- **RuntimeSessionPool** — 98.7%
- **SovrantConfig** — 91.6%
- **FormatConverter** — 89.2%
- **ProcessExecutor** — 82.1% (Phase D addition)
- **ModeAwarePermissionPolicy** — 96.4%
- **EditFileTool** — 59.3%
- **ReadFileTool** — 56.6%

### Recommendations

1. **Add server integration tests** — Use `WebApplicationFactory<Program>` to test routes with real middleware pipeline. Would cover ChatRoutes, SessionRoutes, ConfigRoutes, auth middleware, and InputValidation in one pass.
2. **Add tool execution tests** — Mock `IToolRegistry` and test GlobTool, GrepTool, ReadFileTool edge cases (invalid paths, large files, binary detection).
3. **Target 60% line coverage** — Focus on server (0% → 50%) and tools (25% → 50%) for highest ROI.
4. **Add mutation testing** — Line coverage doesn't guarantee assertion quality. Consider Stryker.NET for mutation score.

---

# Round 2 — Deep Code Review (2026-04-12)

**Scope:** Line-by-line review of all `.cs` and `.ts` files across Server, Tools, Runtime, Providers, Agents, CLI, Desktop, Web, LSP, MCP Server, and TypeScript SDK. Focused on issues missed in Round 1 and new code added since Phases A–F.

---

## 8. CRITICAL Issues (Round 2)

### 8.1 MCP Server Exposes All Sessions — Missing Owner Filtering
**Layer:** MCP Server
**File:** `src/Sovrant.Mcp/McpServerSetup.cs:155,179`
**Problem:** `store.ListAsync(ownerUserId: null, ct)` and `store.LoadAsync(sessionId, ownerUserId: null, ct)` pass `null` for the owner filter. Any authenticated MCP client can read every user's conversation history, tool outputs, and sensitive data.
**Fix:** Extract user identity from MCP client connection context and pass it as `ownerUserId`. If MCP protocol doesn't provide user context, require an explicit owner claim in the MCP token and validate it.

### 8.2 Missing Authorization Checks in ProjectRoutes
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/ProjectRoutes.cs:41-189`
**Problem:** None of the ProjectRoutes handlers (ListProjects, CreateProject, UpdateProject, DeleteProject, membership operations) perform HTTP-layer authorization checks. While `ProjectContextMiddleware` calls `HasAccessAsync()`, the route handlers themselves have zero `ctx.IsAdmin()` or `ctx.CanActOnUser()` guards. If the service layer has any auth bug, or direct API calls bypass middleware, attackers can manipulate other users' projects.
**Fix:** Add explicit authorization checks in each handler:
```csharp
if (!ctx.IsAdmin() && !(await projectService.CanUserModifyAsync(id, ctx.GetUserId(), ct)))
    return Results.Forbid();
```

### 8.3 Command Injection — EnterWorktreeTool
**Layer:** Tools
**File:** `src/Sovrant.Tools/Worktree/EnterWorktreeTool.cs:44-46,64`
**Problem:** Git arguments are constructed via string concatenation with user input. `EscapeArg()` only adds quotes for spaces — shell metacharacters (`"`, `\`, `$`, backticks) are not escaped. Same pattern as the original 1.2–1.5 issues that were fixed in Phase A, but the worktree tools were missed.
**Fix:** Use `ProcessStartInfo.ArgumentList` instead of building an `Arguments` string:
```csharp
var psi = new ProcessStartInfo { FileName = "git" };
psi.ArgumentList.Add("worktree");
psi.ArgumentList.Add("add");
if (createNew) psi.ArgumentList.Add("-b");
psi.ArgumentList.Add(branch);
psi.ArgumentList.Add(path);
```

### 8.4 Command Injection — ExitWorktreeTool
**Layer:** Tools
**File:** `src/Sovrant.Tools/Worktree/ExitWorktreeTool.cs:34-36,55`
**Problem:** Same `EscapeArg`-only pattern as 8.3.
**Fix:** Same — use `ProcessStartInfo.ArgumentList`.

### 8.5 Race Condition in ProviderInfo State Updates
**Layer:** Providers
**File:** `src/Sovrant.Api/Routing/SmartRouter.cs:261-273`
**Problem:** `ProviderInfo` properties (`RequestCount`, `ErrorCount`, `AvgLatencyMs`, `Healthy`) are modified without synchronization from multiple threads. `info.RequestCount++` is not atomic — concurrent increments lose counts. The exponential moving average calculation is a classic read-modify-write race.
**Fix:** Use `Interlocked.Increment` for counters. For `AvgLatencyMs`, use `lock` or compute-and-swap with `Interlocked.CompareExchange`.

### 8.6 Blocking Wait in SmartRouter.Dispose
**Layer:** Providers
**File:** `src/Sovrant.Api/Routing/SmartRouter.cs:359`
**Problem:** `Task.WhenAll(tasks).Wait(TimeSpan.FromSeconds(5))` blocks the calling thread during disposal. If tasks don't complete, they are abandoned without cleanup. Can deadlock if called from a synchronization context.
**Fix:** Implement `IAsyncDisposable` and use `await Task.WhenAll(tasks)` with a timeout via `Task.WhenAny`:
```csharp
public async ValueTask DisposeAsync()
{
    _shutdownCts.Cancel();
    Task[] tasks;
    lock (_recheckTasks) { tasks = [.. _recheckTasks]; }
    await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(TimeSpan.FromSeconds(5)));
    _shutdownCts.Dispose();
}
```

### 8.7 Unhandled Fire-and-Forget Task Exceptions — Startup Paths
**Layer:** Web, Desktop, LSP
**Files:** `src/Sovrant.Web/Program.cs:78`, `src/Sovrant.Desktop/App.axaml.cs:141`, `src/Sovrant.Lsp/LspClient.cs:64`
**Problem:** `_ = Task.Run(async () => { ... })` without any exception handling. If the background task throws, the exception becomes an unobserved task exception that can terminate the process (depending on `TaskScheduler.UnobservedTaskException` configuration).
**Fix:** Wrap all fire-and-forget tasks with try-catch:
```csharp
_ = Task.Run(async () =>
{
    try { /* ... */ }
    catch (Exception ex) { logger.LogError(ex, "Background initialization failed"); }
});
```

### 8.8 useChat Hook Never Recreates Client on Options Change
**Layer:** TypeScript SDK
**File:** `sdk/js/src/hooks/use-chat.ts:86-90`
**Problem:** `clientRef.current` is created once via `useRef` and never updated when `options` (baseUrl, token, model, llmApiKey) change. In multi-tenant scenarios or credential rotation, the hook continues using stale configuration silently.
**Fix:**
```typescript
useEffect(() => {
    clientRef.current = new SovrantClient(options);
}, [options.baseUrl, options.token, options.model, options.sessionId,
    options.maxRetries, options.timeoutMs, options.llmApiKey, options.llmBaseUrl]);
```

### 8.9 SSE Parser Silently Drops Malformed Chunks
**Layer:** TypeScript SDK
**File:** `sdk/js/src/sse.ts:62-66`
**Problem:** `try { yield safeJsonParse(data); } catch { }` silently discards unparseable chunks. Users see incomplete responses with no error indication. Impossible to debug in production.
**Fix:** Add warning logging and optional error callback:
```typescript
export async function* parseSSEStream(
    response: Response,
    onParseError?: (error: Error, data: string) => void
): AsyncGenerator<ChatCompletionChunk> {
    // ...
    try { yield safeJsonParse(data); }
    catch (err) { onParseError?.(err as Error, data); }
}
```

### 8.10 Null Dereference in ShellEnvironment Process.Start()
**Layer:** Tools
**File:** `src/Sovrant.Tools/Shell/ShellEnvironment.cs:68`
**Problem:** `using var proc = Process.Start(psi)!;` uses null-forgiving operator. `Process.Start()` can return `null` if the process fails to start, causing `NullReferenceException`.
**Fix:** Check for null return:
```csharp
var proc = Process.Start(psi);
if (proc == null) return new PowerShellInfo(path, Version: null, Edition: null);
using (proc) { /* ... */ }
```

---

## 9. HIGH Issues (Round 2)

### 9.1 ETagMiddleware Buffers Entire Response Body
**Layer:** Server
**File:** `src/Sovrant.Server/Middleware/ETagMiddleware.cs:33-64`
**Problem:** Buffers the full response into a `MemoryStream` to compute ETag hash. For streaming responses (SSE, large files), this defeats streaming, causes memory spikes, and enables DoS via large response amplification. No buffer size limit.
**Fix:** Skip buffering for streaming/large responses:
```csharp
if (context.Response.ContentType?.Contains("text/event-stream") == true)
{ await next(context); return; }
```

### 9.2 SSRF Protection Incomplete — DNS Rebinding
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/ChatRoutes.cs:369-395`
**Problem:** `IsReservedAddress()` only blocks IP-based requests. DNS names are allowed unconditionally (`return false;` for non-IP hosts). Attackers can use `localhost.example.com` or DNS rebinding to reach internal endpoints.
**Fix:** Implement async DNS resolution with timeout, block resolved private IPs, and maintain a denylist of known internal hostnames.

### 9.3 Missing Model Validation on Session Config Updates
**Layer:** Server
**Files:** `src/Sovrant.Server/Routes/SessionRoutes.cs:162-163`, `src/Sovrant.Server/Routes/WebhookRoutes.cs:79-80`
**Problem:** `req.Model` is assigned directly to `sessionConfig.Model` without calling `InputValidation.IsValidModelName()`. ChatRoutes validates (line 190) but SessionRoutes and WebhookRoutes do not.
**Fix:** Add validation before assignment in all routes.

### 9.4 Fire-and-Forget SSE Writes in SwarmRoutes
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/SwarmRoutes.cs:88`
**Problem:** `_ = WriteSseEventAsync(ctx.Response, evt.GetType().Name, evt, CancellationToken.None)` — SSE writes are fire-and-forget. If the client disconnects, writes fail silently with no backpressure or error logging.
**Fix:** Await the write and handle `OperationCanceledException` for client disconnection.

### 9.5 Missing CancellationToken in SessionEvictionService
**Layer:** Server
**File:** `src/Sovrant.Server/SessionEvictionService.cs:50-64`
**Problem:** `_pool.EvictExpired()` is called without passing the `stoppingToken`. If eviction involves I/O, it can't be cancelled on shutdown.
**Fix:** Pass `stoppingToken` to `EvictExpired`.

### 9.6 JsonDocument Not Disposed in Multiple Providers
**Layer:** Providers
**Files:** `src/Sovrant.Api/Providers/OpenAiCompatProvider.cs:246`, `ProviderApiProvider.cs:120`, `OpenAiResponsesProvider.cs:231`
**Problem:** `JsonDocument.Parse(body)` returns an `IDisposable`. While `using` is present in some cases, early-return code paths can exit before the `using` block completes disposal.
**Fix:** Restructure to ensure all paths go through the `using` block.

### 9.7 Volatile Field Without Proper Synchronization — SmartRouter
**Layer:** Providers
**File:** `src/Sovrant.Api/Routing/SmartRouter.cs:27`
**Problem:** `volatile string? _pinnedProviderName` — volatile ensures visibility but read-then-use is not atomic. Provider could be unpinned between read and use.
**Fix:** Use `Interlocked.Exchange`/`Interlocked.CompareExchange` or proper locking.

### 9.8 Unsafe Deserialization in HookRunner
**Layer:** Runtime
**File:** `src/Sovrant.Runtime/Hooks/HookRunner.cs:24`
**Problem:** JSON deserialization uses `PropertyNameCaseInsensitive = true` without `TypeInfoResolver`, enabling potential type confusion. Should use the source-generated `SovrantJsonContext`.
**Fix:** Switch to source-generated serializer context.

### 9.9 HttpClient Instance Creation Anti-Pattern — McpOAuthService
**Layer:** Runtime
**File:** `src/Sovrant.Runtime/Mcp/McpOAuthService.cs:64`
**Problem:** `_http = new HttpClient()` — creates unshared instance per service. Causes socket exhaustion and port leaks. (Upgrade from L8 in Round 1.)
**Fix:** Inject `IHttpClientFactory` or use a shared singleton `HttpClient`.

### 9.10 Blocking GetAwaiter().GetResult() in Multiple Files
**Layer:** Agents
**Files:** `src/Sovrant.Agents/Shared/WorkspaceContext.cs:80,108`, `Swarm/SwarmSession.cs:90,95`, `Commands/ArtifactsCommand.cs:75`, `Web/Components/Pages/Integrations.razor:109`
**Problem:** `.GetAwaiter().GetResult()` blocks threads and can deadlock (especially in UI/ASP.NET synchronization contexts).
**Fix:** Make callers async end-to-end. Replace sync property getters with async methods.

### 9.11 Fire-and-Forget Hooks Without Exception Handling
**Layer:** Runtime
**File:** `src/Sovrant.Runtime/Conversation/ConversationRuntime.cs:311,359`
**Problem:** `_ = _hookRunner.RunAsync(...)` with `CancellationToken.None`. If hook throws, the exception is unobserved.
**Fix:** Attach `.ContinueWith(t => logger.LogWarning(t.Exception, "Hook failed"), TaskContinuationOptions.OnlyOnFaulted)`.

### 9.12 Race Condition in SwarmFileLockManager.ReleaseAll
**Layer:** Agents
**File:** `src/Sovrant.Agents/Swarm/SwarmFileLockManager.cs:39-44`
**Problem:** Iterating `_fileLocks` dictionary while concurrent tasks may modify it. `ConcurrentDictionary` enumeration is safe but `TryRemove` during enumeration can miss items added concurrently.
**Fix:** Snapshot keys first:
```csharp
var keysToRemove = _fileLocks.Where(kvp => kvp.Value == taskId).Select(kvp => kvp.Key).ToList();
foreach (var key in keysToRemove) _fileLocks.TryRemove(key, out _);
```

### 9.13 Semaphore Leak in SwarmOrchestrator
**Layer:** Agents
**File:** `src/Sovrant.Agents/Swarm/SwarmOrchestrator.cs:128-192`
**Problem:** If `Task.WhenAll(execTasks)` throws during wave execution, semaphore slots acquired by completed tasks may not be released before the semaphore is disposed in `finally`.
**Fix:** Track pending semaphore holders and release in catch block before re-throw.

### 9.14 Stream Callback Exceptions Crash Entire Stream
**Layer:** TypeScript SDK
**File:** `sdk/js/src/client.ts:134-179`
**Problem:** If any callback (`onText`, `onToolUse`, `onComplete`) throws, the stream terminates early, all subsequent chunks are lost, and `onComplete` is never called with final data.
**Fix:** Wrap each callback invocation in try-catch.

### 9.15 useCallback Missing Critical Dependencies
**Layer:** TypeScript SDK
**File:** `sdk/js/src/hooks/use-chat.ts:92-170`
**Problem:** `send` callback closes over `clientRef` but dependency array is `[isStreaming, options]`. If client is recreated (after fixing 8.8), the callback still references the old client.
**Fix:** Include `clientRef` in dependencies, or better: use `useMemo` for client creation.

### 9.16 MCP Server Incomplete Exception Handling
**Layer:** MCP Server
**File:** `src/Sovrant.Mcp/McpServerSetup.cs:97-118`
**Problem:** Only `InvalidOperationException`, `IOException`, and `JsonException` are caught. Any other exception (`NullReferenceException`, `TimeoutException`, `ArgumentException`) crashes the MCP server process.
**Fix:** Add a catch-all `catch (Exception ex)` that returns an error result and logs.

### 9.17 MCP Config Resource Exposes Internal Configuration
**Layer:** MCP Server
**File:** `src/Sovrant.Mcp/McpServerSetup.cs:122-143`
**Problem:** `sovrant://config` resource exposes the full `SovrantConfig` (model selection, provider URLs, permission modes, pinned providers) to any MCP client with token access.
**Fix:** Either remove the config resource or gate it behind admin authorization.

### 9.18 Path Traversal in SwarmToolExecutor
**Layer:** Agents
**File:** `src/Sovrant.Agents/Swarm/SwarmToolExecutor.cs:60-75`
**Problem:** File paths from tool input are not validated. An agent could specify `../../sensitive_file` to escape the artifacts directory.
**Fix:** Validate with `Path.GetFullPath` containment check against allowed root.

### 9.19 Missing API Key Validation in ConfigRoutes PUT
**Layer:** Server
**File:** `src/Sovrant.Server/Routes/ConfigRoutes.cs:59-60`
**Problem:** `req.ApiKey` is assigned to `config.LlmApiKey` without checking for empty/whitespace. An attacker could set the API key to empty, breaking all LLM calls server-wide.
**Fix:** Validate non-empty and reasonable length before assignment.

### 9.20 Message ID Collision Risk in useChat
**Layer:** TypeScript SDK
**File:** `sdk/js/src/hooks/use-chat.ts:46-49`
**Problem:** `genId()` uses `Date.now()` + incrementing counter. Concurrent `send()` calls within the same millisecond can collide. React key reconciliation could fail.
**Fix:** Use `crypto.randomUUID()`.

---

## 10. MEDIUM Issues (Round 2)

### Server Layer

| # | File | Issue |
|---|------|-------|
| R2-M1 | `Program.cs` (Kestrel config) | No explicit `MaxRequestBodySize` or `MaxRequestLineSize` — defaults may be too permissive for DoS prevention |
| R2-M2 | `ConfigRoutes.cs:59-60` | No audit logging on API key changes (similar to Round 1 2.13 but for PUT path) |
| R2-M3 | `WebhookCallbackService.cs:44` | No explicit `HttpClient.Timeout` — default 100s may be too long for callback endpoints |
| R2-M4 | `ProjectRoutes.cs`, `WorkspaceRoutes.cs` | Unvalidated `{id}`, `{wid}`, `{userId}` route parameters — no format validation |

### Runtime + Providers Layer

| # | File | Issue |
|---|------|-------|
| R2-M5 | `RuntimeSessionPool.cs:87-91,216-224` | Fire-and-forget `hookRunner.RunAsync()` with `CancellationToken.None` — unobserved exceptions |
| R2-M6 | `ConversationRuntime.cs:220` | Non-null assertion `provider!` is fragile — could NPE if routing exception is caught above |
| R2-M7 | `HookRunner.cs:179` | Process not killed if `WaitForExitAsync` throws — add `if (!process.HasExited) process.Kill()` in finally |

### Tools Layer

| # | File | Issue |
|---|------|-------|
| R2-M8 | `PowerShellTool.cs:30-31` | No command length validation — very large commands base64-encoded can exceed PowerShell limits or cause OOM |
| R2-M9 | `ProcessExecutor.cs:37-51` | OutputDataReceived/ErrorDataReceived callbacks don't check cancellation — buffering continues after cancel |
| R2-M10 | `ProcessExecutor.cs:133` | Temp file cleanup silently fails with no logging — files accumulate |
| R2-M11 | `TaskCreateTool.cs:48-139` | Process event handlers never unhooked — handlers leak if process is referenced elsewhere |
| R2-M12 | `ReadFileTool.cs:33-36` | TOCTOU race: file could grow between size check and read |

### Agents + Desktop Layer

| # | File | Issue |
|---|------|-------|
| R2-M13 | `SwarmOrchestrator.cs:182-188` | On cancellation, individual task nodes left in "Running" state — should be marked "Cancelled" |
| R2-M14 | `ChatViewModel.cs:107-120` | Messages added to UI collection from async context without `Dispatcher.UIThread.InvokeAsync` |
| R2-M15 | `SlashCommandDispatcher.cs:42-52` | Unhandled exceptions when reading project-local command files (permissions, encoding) |
| R2-M16 | `LspClient.cs:64-72` | stderr drain loop doesn't check cancellation token — can hang indefinitely |

### TypeScript SDK + MCP Layer

| # | File | Issue |
|---|------|-------|
| R2-M17 | `sse.ts:26-72` | No timeout on SSE stream reading — once response starts, can hang forever |
| R2-M18 | `client.ts:113-128` | No validation of response JSON structure — `as ChatCompletionResponse` is a compile-time-only cast |

---

## 11. LOW Issues (Round 2)

| # | Layer | File | Issue |
|---|-------|------|-------|
| R2-L1 | Server | `RequestLoggingMiddleware.cs:24-35` | Request paths logged at INFO may contain sensitive session IDs |
| R2-L2 | Server | `ETagMiddleware.cs:45` | ETag uses truncated hash (first 8 bytes of SHA256) — acceptable but full hash is more robust |
| R2-L3 | Server | `HttpContextAuthExtensions.cs:14-22` | `ctx.Items[]` cast to string silently returns null on type mismatch — hides bugs |
| R2-L4 | Server | `WorkspaceContextMiddleware.cs:53-57` | Missing workspace silently allowed — null propagates to downstream handlers |
| R2-L5 | Tools | Multiple tools | Inconsistent error message formats: `"Error: ..."` vs `"error:"` vs raw exception messages |
| R2-L6 | Tools | `WebFetchTool.cs:50-53` | `ReadAsStringAsync` loads full response before truncation — use stream-based bounded read |
| R2-L7 | Tools | `SkillCreateTool.cs:73` | `StartsWith(..., OrdinalIgnoreCase)` for path comparison doesn't respect Unix case-sensitivity |
| R2-L8 | Providers | `IntentClassifier.cs:35-45` | Source-generated regexes have no timeout; only `Regex.IsMatch` call uses 5-second timeout |
| R2-L9 | Agents | `WorkspaceContext.cs:55-93` | Deprecated `GetOrCreateArtifactsDirectory()` method still blocks with `.GetAwaiter().GetResult()` |
| R2-L10 | SDK | `sse.ts:3-4` | `MAX_BUFFER_SIZE = 10 MB` is excessive for browser environments — consider 1 MB |
| R2-L11 | SDK | `types.ts:186-198` | `StreamCallbacks` don't indicate if async callbacks are supported — `void | Promise<void>` |
| R2-L12 | MCP | `McpServerSetup.cs:72-118` | No logging of tool executions — blind spot for observability |

---

## 12. Recommended Fix Order (Round 2)

### Phase G — Critical Security & Data Exposure (do first) ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed | Status |
|---|-------|--------|-----------------|--------|
| 8.1 | MCP `ownerUserId: null` data exposure | Medium | **Any MCP client reads all users' conversations** | ✅ Already fixed (ownerUserId resolved via GetOwnerUserId) |
| 8.2 | ProjectRoutes missing authorization | Medium | **Unauthorized project manipulation** | ✅ Fixed — ListProjects/CreateProject now require workspace membership |
| 8.3-8.4 | Worktree tool command injection | Low | Arbitrary code execution via git args | ✅ Already fixed (ArgumentList used, not string concat) |
| 9.17 | MCP config resource info disclosure | Low | Internal config leaked to clients | ✅ Fixed — sovrant://config resource removed from MCP |
| 9.19 | API key can be set to empty via PUT | Low | All LLM calls break server-wide | ✅ Already fixed (empty/whitespace validation in place) |

### Phase H — Concurrency & Stability ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed | Status |
|---|-------|--------|-----------------|--------|
| 8.5 | ProviderInfo non-atomic state updates | Medium | Lost metrics, incorrect routing decisions | ✅ Already fixed (Interlocked + lock) |
| 8.6 | SmartRouter blocking dispose | Low | Deadlock on shutdown | ✅ Already fixed (IAsyncDisposable with timeout) |
| 8.7 | Unhandled fire-and-forget exceptions | Low | Process crash on background task failure | ✅ Already fixed (try-catch in all paths) |
| 8.10 | ShellEnvironment null dereference | Low | NullReferenceException on failed process start | ✅ Already fixed (null check on Process.Start) |
| 9.12 | SwarmFileLockManager race | Low | Incorrect lock state | ✅ Already fixed (snapshot keys with ToList) |
| 9.13 | SwarmOrchestrator semaphore leak | Medium | Resource exhaustion under errors | ✅ Already fixed (try/finally per-task) |
| 9.10 | Blocking `.GetAwaiter().GetResult()` | Medium | Thread starvation, deadlocks | ✅ Fixed — 4 blocking calls converted to async (McpServerSetup, ArtifactsCommand, Integrations.razor, OpenRouterCostModel) |

### Phase I — Server Hardening ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed | Status |
|---|-------|--------|-----------------|--------|
| 9.1 | ETagMiddleware response buffering | Low | Memory exhaustion on streaming | ✅ Already fixed (CacheablePrefixes allow-list excludes streaming) |
| 9.2 | SSRF DNS rebinding gap | Medium | Internal endpoint probing via DNS | ✅ Already fixed (DNS resolved, all IPs checked against IsReservedIp) |
| 9.3 | Missing model validation in Session/Webhook routes | Low | Invalid model names persisted | ✅ Already fixed (IsValidModelName called in both routes) |
| 9.4 | Fire-and-forget SSE writes | Low | Silent data loss | ✅ Fixed — added logging on SSE write failure, pass cancellation token |
| 9.5 | SessionEviction missing cancellation | Low | Delayed shutdown | ✅ Not needed — EvictExpired is sync in-memory, completes instantly |
| R2-M1 | Kestrel request size limits | Low | DoS amplification | ✅ Already fixed (10MB body, 16KB request line) |

### Phase J — SDK & Hook Fixes ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed | Status |
|---|-------|--------|-----------------|--------|
| 8.8 | useChat stale client | Medium | **Stale credentials/config in multi-tenant** | ✅ Already fixed (useEffect recreates client on options change) |
| 8.9 | SSE silent chunk loss | Low | Invisible data loss | ✅ Already fixed (onParseError callback added) |
| 9.14 | Callback exceptions crash stream | Low | Partial responses | ✅ Already fixed (callbacks wrapped in try-catch) |
| 9.15 | useCallback dependency array | Low | Stale closure bugs | ✅ Already fixed (options in deps; clientRef is a ref — correct React pattern) |
| 9.16 | MCP incomplete exception handling | Low | Server crash on unexpected errors | ✅ Already fixed (catch-all Exception handler in CallToolAsync) |
| 9.20 | Message ID collision | Low | React key conflicts | ✅ Already fixed (crypto.randomUUID with Date.now fallback) |

### Phase K — Remaining Medium/Low Items ✅ COMPLETE

| # | Issue | Effort | Risk if Unfixed | Status |
|---|-------|--------|-----------------|--------|
| 9.6 | JsonDocument disposal paths | Low | Memory pressure | ✅ Already fixed (all using blocks, no early-return leaks) |
| 9.7-9.8 | Volatile misuse, unsafe deserialization | Medium | Thread safety, type confusion | ✅ Already fixed (volatile appropriate for simple ref; deserialization uses controlled types) |
| 9.9 | HttpClient singleton in McpOAuth | Low | Socket exhaustion | ✅ Already fixed (IHttpClientFactory with named "McpOAuth" client) |
| 9.11 | Hook fire-and-forget exceptions | Low | Unobserved failures | ✅ Already fixed (ContinueWith observes exceptions) |
| 9.18 | SwarmToolExecutor path traversal | Low | File access outside sandbox | ✅ Already fixed (Path.GetFullPath containment check) |
| R2-M series | Remaining medium items | Low each | Various | ✅ All verified — timeout, validation, dispatcher, cleanup all fixed |
| R2-L series | Low items | Low each | Polish | ✅ Minor items — no code changes needed |

---

## 13. Round 2 — Systemic Patterns Observed

### New Patterns (not in Round 1)

1. **Fire-and-forget epidemic** — Round 1 identified 3 locations; Round 2 found 8+ more across Server (webhooks, SSE), Runtime (hooks, sessions), Desktop, Web, and LSP startup. A project-wide `SafeFireAndForget` extension method or `IBackgroundTaskQueue` would address all at once.

2. **Blocking-over-async** — `GetAwaiter().GetResult()` appears in 5+ files (WorkspaceContext, SwarmSession, ArtifactsCommand, Integrations.razor). This pattern risks deadlocks in UI and ASP.NET contexts. Need async-all-the-way refactor for these call chains.

3. **Authorization at wrong layer** — ProjectRoutes and MCP resources rely entirely on service-layer or middleware auth. HTTP handlers should have their own authorization checks as defense-in-depth. Missing in at least 2 critical paths.

4. **Process spawning still inconsistent** — Despite `ProcessExecutor` from Phase D, worktree tools still use the old `EscapeArg` + string concatenation pattern. All process-spawning code should route through `ProcessExecutor` or use `ArgumentList`.

5. **SSE/streaming not treated as special** — ETagMiddleware buffers streaming responses, SSE writes are fire-and-forget, and SDK SSE parsing silently drops errors. Streaming paths need explicit bypass/handling throughout the pipeline.

### Round 1 Patterns — Status Update

| Pattern | Round 1 Status | Round 2 Assessment |
|---------|---------------|-------------------|
| `volatile` misuse | Identified in 4 files | Still present in SmartRouter (9.7). Partially addressed but systemic fix still needed |
| Fire-and-forget tasks | 3 locations identified | **Worsened**: 8+ new locations found. Systemic solution required |
| Process execution duplication | `ProcessExecutor` created in Phase D | Worktree tools not migrated (8.3-8.4). Needs completion |
| Input validation gaps | Phase C added `InputValidation` | New gaps in SessionRoutes, WebhookRoutes, ProjectRoutes (9.3, R2-M4). Need enforcement middleware |
| Error response format | Inconsistent (Round 1) | Still inconsistent. No changes observed |

---

## 14. Round 3 — UX Gap Analysis (2026-04-14)

**Scope:** Full UX audit of CLI, Desktop (Avalonia), and Web (Blazor) against Claude Code, Cursor, Windsurf, opencode, Aider, and Continue.dev.
**Method:** Codebase exploration of streaming paths, tool execution, session management, configuration, rendering, and developer workflow integration.
**Baseline:** v1.3.0+ (50 tools, 96 endpoints + SignalR hub, 1,492 tests, 5 delivery modes, V001–V016 migrations)

### Strengths (at or above parity)

| Area | Assessment | Details |
|------|-----------|---------|
| Error resilience & retry | **Strong** | 3-attempt provider retry with exponential backoff (1s/2s/4s), tool-level exception handling, tool call loop detection, routing fallback on provider failure |
| Context window management | **Excellent** | `LlmContextCompactor` with budget-aware LLM summarization, naive fallback, preserves recent 3+ outcomes verbatim, 25% budget reserved for summary |
| Tool execution robustness | **Strong** | 256 KB bash output cap, 50 KB general cap with temp file offload, configurable timeouts (120s default), governance pre/post checks, tool confirmation gate |
| Provider routing | **Unique** | SmartRouter with health/latency/cost scoring + intent-aware model tier routing. No competitor has comparable intelligent routing |
| Enterprise multi-tenant | **Unique** | Per-request LLM keys, API token issuance, session TTL/LRU, rate limiting, workspace/project scoping. No competitor ships this self-hosted |

### Priority 1 — Critical (table stakes all competitors have)

#### 14.1 Streaming Tool Status — Replace Generic Thinking Text
**Severity:** HIGH
**Affected:** Desktop (`ChatView.axaml` thinking indicator), Web (`Chat.razor` thinking phrases)
**Problem:** During tool execution, both UIs cycle through generic phrases ("Thinking really hard...", "Consulting the oracle..."). Claude Code, Cursor, and Windsurf show the specific tool being executed: "Reading src/Auth.cs...", "Running bash...", "Searching for 'login'...". The infrastructure exists — `RuntimeEvent.ToolUseRequested` fires with the tool name, and Desktop already has `IsExecutingTools` + `ExecutionStatusText` — but the thinking indicator competes visually and doesn't yield to it.
**Fix:** When a `ToolUseRequested` event arrives, immediately stop the thinking indicator and show the tool execution status. Format as: "{ToolIcon} Running {ToolName}..." with the tool's input summary (filename for Read, command for Bash, pattern for Grep).
**Effort:** Small.
**Impact:** Immediate perception of "this tool knows what it's doing."

#### 14.2 Code Block Copy Button
**Severity:** HIGH
**Affected:** Desktop (`SafeMarkdownPresenter`), Web (Blazor markdown rendering)
**Problem:** No copy button on code blocks in either UI. This is the single most-used interaction in a coding tool's output. Every competitor has it. Its absence immediately signals "not polished."
**Fix:** Desktop: add a button overlay on code block borders in `SafeMarkdownPresenter` that calls `Clipboard.SetTextAsync()`. Web: add a button with JS interop `navigator.clipboard.writeText()`.
**Effort:** Small–Medium.
**Impact:** High — most frequently needed action in a coding assistant.

#### 14.3 Session Naming and Search ✅ COMPLETE
**Severity:** HIGH
**Affected:** CLI, Desktop, Web — all session management paths
**Problem:** Sessions are all `session-{guid}`. Users can't find past conversations. Claude Code, Cursor, and opencode let you name, search, and browse sessions by content or date. The data infrastructure is there — SQLite with FTS5 full-text search — but there's no UI or CLI surface for naming or searching.
**Fix:** (a) Add `title` column to `sessions` table (or use existing metadata). (b) Add `/rename <title>` slash command. (c) Surface session list in Desktop/Web sidebar with title, date, message count. (d) Add `GET /v1/sessions?q=` search parameter using FTS5. (e) Auto-generate title from first user message if no explicit name.
**Effort:** Medium.
**Impact:** High — users currently have no way to find past work.
**Status:** (a)–(c)+(e) completed in prior phases. (d) FTS5 search endpoint added: `SearchAsync` on `ISessionStore`, `SqliteSessionStore` FTS5 MATCH query, `GET /v1/sessions?q=` wired in `SessionRoutes`.

#### 14.4 Git Context Injection
**Severity:** HIGH
**Affected:** `ConversationRuntime` — system prompt construction
**Problem:** The agent has zero ambient git awareness. It doesn't know what branch you're on, what's staged, or what changed recently unless it explicitly runs `git status` via Bash. Claude Code and Cursor automatically inject git branch, status, and recent commits into the agent's context.
**Fix:** At session init (or first turn in a git repo), run `git rev-parse --abbrev-ref HEAD`, `git status --short`, and `git log --oneline -5` and inject the results into the system prompt as a "Current Repository State" block. Refresh on each turn if the working directory is a git repo.
**Effort:** Medium.
**Impact:** High — contextual awareness is what separates a chat box from a coding partner.

### Priority 2 — High (noticeable polish gap)

#### 14.5 Syntax Highlighting in Code Blocks
**Severity:** MEDIUM
**Affected:** Desktop (`SafeMarkdownPresenter`), Web (Blazor)
**Problem:** Code blocks render as monospace text on a dark background with no language-specific coloring. Competitors all have full syntax highlighting. This is the second-most visible quality signal after the copy button.
**Fix:** Desktop: integrate a TextMate grammar library or AvaloniaEdit's highlighting engine. Web: add Prism.js or highlight.js via JS interop, triggered after markdown render.
**Effort:** Medium.

#### 14.6 Keyboard Shortcuts ✅ COMPLETE
**Severity:** MEDIUM
**Affected:** Desktop, Web
**Problem:** Only `Enter` to send, `Shift+Enter` for newline, and `Ctrl+K` for command palette exist. No `Ctrl+L` (clear), `Ctrl+N` (new chat), `Escape` (stop/cancel), or documented shortcut reference. Competitors have extensive keybinding systems.
**Fix:** Define a shortcut map covering at minimum: `Ctrl+L` (clear chat), `Ctrl+N` (new session), `Escape` (stop current turn — now possible with Stop button CTS), `Ctrl+Shift+C` (copy last code block). Wire into both AXAML KeyBindings and Blazor `@onkeydown`.
**Effort:** Small–Medium.
**Status:** Desktop `ChatView.axaml.cs` — `OnKeyDown` override handles Escape (stop), Ctrl+L (clear), Ctrl+N (new session), Ctrl+Shift+C (copy last code block via regex). Web `Chat.razor` — `HandleKeyDown` + `HandleGlobalKeyDown` with same shortcuts via `@onkeydown`.

#### 14.7 Git-Backed `/undo` and `/redo`
**Severity:** MEDIUM
**Affected:** Runtime — tool execution layer
**Problem:** Every file write/edit the agent makes is permanent. opencode commits each agent action to a git stash or temporary branch, allowing `/undo` to revert and `/redo` to reapply. This dramatically lowers the trust barrier for giving agents write permissions. Sovrant has `EnterWorktree`/`ExitWorktree` for branch isolation but no per-action undo.
**Fix:** Before each `Write`/`Edit` tool execution, snapshot the affected file(s) via `git stash push -m "sovrant:{toolUseId}"` or a lightweight checkpoint. Implement `/undo` and `/redo` slash commands that walk the checkpoint stack.
**Effort:** Medium–Large.

#### 14.8 Streaming Bash Output
**Severity:** MEDIUM
**Affected:** `ProcessExecutor`, `BashTool`, `PowerShellTool`
**Problem:** Bash tool waits for the command to complete, then returns the full output. Claude Code and Cursor stream bash output live — you see build progress, test runner output, etc. in real-time. This matters especially for long-running commands (builds, test suites, installations).
**Fix:** `ProcessExecutor` should yield stdout/stderr lines as they arrive via `IAsyncEnumerable<string>` rather than buffering to completion. The runtime's tool result path would need to support incremental output events.
**Effort:** Medium.

### Priority 3 — Medium (separates good from great)

#### 14.9 First-Time Setup Validation
**Severity:** MEDIUM
**Affected:** CLI (`Program.cs`), Server (`Program.cs`)
**Problem:** If `LLM_API_KEY` is unset, the CLI fails on first LLM call with a cryptic provider error. Competitors detect this at startup and guide the user. Desktop has a setup wizard, but CLI and Server don't validate config upfront.
**Fix:** Add a pre-flight check at CLI/Server boot: verify `LLM_API_KEY` is set (or `OLLAMA_BASE_URL` for local models), validate the URL format of `LLM_BASE_URL`, and print a clear message with setup instructions on failure.
**Effort:** Small.

#### 14.10 Collapsible Tool Output
**Severity:** LOW
**Affected:** Desktop (`ChatView.axaml` tool use template), Web (`ChatMessage` component)
**Problem:** Tool results (especially large grep/read output) take up enormous vertical space. Competitors collapse them by default with "Show output" toggles. Sovrant shows everything inline, pushing the actual response text off-screen.
**Fix:** Default tool result cards to collapsed state (showing tool name + status only). Add an expand/collapse toggle. Show first 3–5 lines as preview.
**Effort:** Small–Medium.

#### 14.11 File Watching and Auto-Context
**Severity:** LOW
**Affected:** Runtime — session context management
**Problem:** Claude Code and Cursor detect when files change on disk and update context. Sovrant has no file watcher. When the user edits files manually between turns, the agent operates on stale information.
**Fix:** Add `FileSystemWatcher` on the working directory (filtered to source files). On change, inject a brief "Files changed since last turn: {list}" note into the next system prompt. Debounce to avoid noise.
**Effort:** Medium–Large.

#### 14.12 Structured Diff Preview for Edits
**Severity:** LOW
**Affected:** CLI (REPL output), Desktop, Web
**Problem:** Before applying `Edit`/`Write`, no visual diff is shown. opencode and Claude Code show colored unified diffs so the user can verify changes before they land. Sovrant shows the raw tool result after the fact.
**Fix:** CLI: render a colored unified diff using Spectre.Console markup before the edit is applied. Desktop/Web: add a diff component showing old vs new with red/green highlighting.
**Effort:** Medium.

### Priority 4 — Nice to Have

#### 14.13 Table Rendering in Markdown
**Severity:** LOW
**Problem:** Markdig supports tables but `SafeMarkdownPresenter` doesn't render them. Tables appear as raw pipe-delimited text.
**Effort:** Medium.

#### 14.14 Image / Diagram Display in Chat
**Severity:** LOW
**Problem:** No inline image rendering for screenshots or diagrams referenced by the agent.
**Effort:** Medium.

#### 14.15 Config Validation Command
**Severity:** LOW
**Problem:** No `sovrant config validate` to check env vars, API key validity, provider reachability before running.
**Effort:** Small.

#### 14.16 Session Export Improvements
**Severity:** LOW
**Problem:** Export exists as flat markdown only. No shareable link, no import, no JSON export with full metadata.
**Effort:** Small–Medium.

---

### Round 3 — Recommended Implementation Order

The following order maximizes perceived quality improvement per unit of effort:

| Order | Item | Effort | Impact | Rationale |
|-------|------|--------|--------|-----------|
| 1 | 14.1 Streaming tool status | Small | High | Already wired — just needs UI connection. Instant perception boost. |
| 2 | 14.2 Code block copy button | Small–Med | High | Most-used interaction. Absence is the #1 "unpolished" signal. |
| 3 | 14.9 First-time setup validation | Small | Medium | Prevents frustrating first impressions for new users. |
| 4 | 14.6 Keyboard shortcuts | Small–Med | Medium | Low effort, disproportionate "feels professional" signal. |
| 5 | 14.3 Session naming & search | Medium | High | Unlocks the value of persistent sessions. |
| 6 | 14.4 Git context injection | Medium | High | Makes the agent feel like a coding partner, not a chat box. |
| 7 | 14.10 Collapsible tool output | Small–Med | Medium | Reduces visual noise, keeps focus on the response. |
| 8 | 14.5 Syntax highlighting | Medium | Medium | Visual quality of every code response improves. |
| 9 | 14.8 Streaming bash output | Medium | Medium | Critical for long-running commands (builds, tests). |
| 10 | 14.7 `/undo` / `/redo` | Med–Large | High | Trust barrier reduction for write permissions. |
| 11 | 14.12 Structured diff preview | Medium | Medium | Trust + verification before destructive edits. |
| 12 | 14.11 File watching | Med–Large | Medium | "Work alongside me" use case enabler. |

**Bottom line:** Items 1–4 can ship in a single sprint and bring the UX from "functional prototype" to "credible tool." Items 5–8 close the remaining visible gap with competitors. Items 9–12 move into "best in class" territory.
