# Sovrant Code Review

**Date:** 2026-04-05
**Scope:** Full codebase — Runtime, Providers, Server, Tools (39), Agents, CLI, TypeScript SDK
**Build:** 329 tests passing, 0 warnings
**Tag:** v1.1.0-port2-agents (commit e8db520)

---

## Executive Summary

| Severity | Count | Breakdown |
|----------|-------|-----------|
| **CRITICAL** | 9 | Server SSRF, command injection (4 tools), rate limit bypass, auth logic, unhandled exceptions, OAuth callback |
| **HIGH** | 24 | CTS leaks, thread safety, credential exposure, missing timeouts, weak typing, test gaps |
| **MEDIUM** | 58 | Concurrency races, validation gaps, inconsistent patterns, performance, error handling |
| **LOW** | 38 | Naming, dead code, minor allocations, missing logging |
| **TOTAL** | **129** | |

**Top priorities:**
1. SSRF via `X-LLM-Base-Url` — no URL validation on user-supplied provider base URL
2. Command injection in BashTool, PowerShellTool, ReplTool, TaskCreateTool — simplistic escaping
3. Rate limiting bypass via IP spoofing / "anonymous" fallback key
4. CancellationTokenSource leaks in MultiAgentCoordinator and ProcessBasedMultiAgentSystem
5. Missing request timeouts across server, SDK, and webhook delivery

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

### 2.1 CancellationTokenSource Leak — MultiAgentCoordinator
**Layer:** Agents
**File:** `src/Sovrant.Agents/Modern/MultiAgentCoordinator.cs:69-98`
**Problem:** `linkedCts` (from `CreateLinkedTokenSource`) is stored in `_taskCts` but never explicitly disposed. `TryRemove` doesn't call `Dispose()`.
**Fix:**
```csharp
finally {
    if (_taskCts.TryRemove(task.Id, out var cts))
        cts.Dispose();
}
```

### 2.2 CancellationTokenSource Leak — ProcessBasedMultiAgentSystem
**Layer:** Agents
**File:** `src/Sovrant.Agents/Legacy/ProcessBasedMultiAgentSystem.cs:114-119`
**Problem:** Race condition between `Dispose()` iterating `_taskCts` and `RunTaskAsync()` adding new entries.
**Fix:** Add locking or use a disposed flag to reject new tasks during shutdown.

### 2.3 Unsafe Dictionary — MultiAgentCoordinator
**Layer:** Agents
**File:** `src/Sovrant.Agents/Modern/MultiAgentCoordinator.cs:17-18,42`
**Problem:** `_agents` is a plain `Dictionary<string, IAgent>` without synchronization. Concurrent `AddAgent()` and `DispatchAsync()` calls cause `InvalidOperationException`.
**Fix:** Use `ConcurrentDictionary<string, IAgent>`.

### 2.4 Dead Code — Discarded Agent Results in BaseAgent
**Layer:** Agents
**File:** `src/Sovrant.Agents/Modern/BaseAgent.cs:64`
**Problem:** `_ = await HandleAsync(task, ct)` — agent results from `RunLoopAsync` are discarded. The inbox/channel infrastructure is unused since `DispatchAsync` calls `HandleAsync` directly.
**Fix:** Either implement result-passing mechanism or remove dead `RunLoopAsync`/`EnqueueAsync` code.

### 2.5 Coordinator Not Disposed
**Layer:** Agents
**File:** `src/Sovrant.Agents/Modern/InProcessMultiAgentSystem.cs:17`
**Problem:** `DisposeAsync()` calls `ShutdownAsync()` but never disposes the `MultiAgentCoordinator` (which owns a `SemaphoreSlim`).
**Fix:** Call `_coordinator?.Dispose()` in `DisposeAsync()`.

### 2.6 ProcessAgent — No ProcessStartInfo Validation
**Layer:** Agents
**File:** `src/Sovrant.Agents/Legacy/ProcessAgent.cs:35-51`
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
| M37 | `MultiAgentCoordinator.cs:70-86` | Semaphore held for entire `HandleAsync` duration — starves other agents |
| M38 | `MultiAgentCoordinator.cs:113-136` | `ShutdownAsync` iterates `_taskCts.Values` without snapshot — concurrent modification |
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
| L26 | Agents | `ServiceCollectionExtensions.cs:33-34` | Modern backend singletons registered even in legacy mode |
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
| **Sovrant.Agents** | **59.6%** | Best covered — MultiAgentCoordinator 91%, InMemoryTeamRegistry 100%, AgentPrompts 100% |
| **Sovrant.Runtime** | **48.6%** | RuntimeSessionPool 99%, SovrantConfig 92%, FilteredToolRegistry 100%; gaps in logging, MCP, prompt builder |
| **Sovrant.Commands** | **41.1%** | SlashCommandDispatcher 83%, TokenUsageTracker 100%; HelpCommand, MemoryCommand, SessionCommand at 0% |
| **Sovrant.Api** | **33.0%** | FormatConverter 89%, ProviderInfo 93%; Responses API types and Ollama provider uncovered |
| **Sovrant.McpServer** | **31.6%** | ToolFilter 100%, McpTokenValidator 100%; McpServerSetup 8% |
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

- **MultiAgentCoordinator** — 91.4% (Phase 19 core)
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
