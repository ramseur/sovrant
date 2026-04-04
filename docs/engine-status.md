# Sovrant Engine — Status Report

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-04-04 (Phase 7 complete — all hardening done; Phase 17.5 agent scaffolding done; OpenAI Responses API + native web search done — 31 tools, 100 tests)
**Test models:** `gemini-2.5-flash` (Google AI Studio, free tier), `gpt-4o-mini` (OpenAI, paid tier)

---

## Engine Core

| Component | Status | Notes |
|---|---|---|
| CLI entry point (`sovrant prompt "..."`) | ✅ Working | One-shot mode confirmed |
| REPL loop (`sovrant`) | ✅ Working | Slash commands, history, Spectre.Console rendering |
| SmartRouter | ✅ Working | Pings providers on startup, routes by latency/cost/health. Falls back to configured providers when all fail startup ping (WSL/CI DNS resilience). |
| Agentic loop | ✅ Working | Multi-turn tool use, up to 20 rounds per turn |
| Session persistence (JSONL) | ✅ Working | `~/.sovrant/sessions/{id}.jsonl` append-log |
| Session resumption (`--session <id>`) | ✅ Working | History replayed correctly across separate process invocations |
| Permission system | ✅ Working | `bypassPermissions` / `dontAsk` / `default` / `plan` all functional |
| SSE streaming | ✅ Working | Text chunks stream to console in real time |
| Token counts | ✅ Fixed | OpenAI trailing usage chunk now captured. Input + output tokens reported correctly after each turn. |
| HTTP server (`Sovrant.Server`) | ✅ Working | 9 endpoints: `GET /health`, `POST /v1/chat/completions`, `GET+PUT /v1/config`, `GET /v1/status`, `GET /v1/models`, `GET /v1/sessions`, `GET+DELETE /v1/sessions/{id}` |
| Server session pool (`IRuntimeSessionPool`) | ✅ Implemented | One `ConversationRuntime` per session ID, `ConcurrentDictionary`. Concurrent turns on the same session not yet serialised — Phase 9 adds per-session lock. |
| Unit test suite | ✅ 100/100 passing | Api(23) + Runtime(28) + Tools(26) + Commands(22) + Integration(1) |
| Phase 7.5 Tier 1 tools | ✅ Implemented | TaskUpdate, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree (27 tools total) |
| Phase 7.5 Tier 2 tools | ✅ Implemented | Skill, ToolSearch, ListMcpResources, ReadMcpResource + custom project slash commands + `/memory` command (31 tools total) |
| Phase 7.6 memory files | ✅ Implemented | `~/.sovrant/memory.md` + `.sovrant/memory.md` injected into system prompt at session start |
| Phase 17.5 agent scaffolding | ✅ Implemented | `Sovrant.Agents` project: `IAgent`, `IMultiAgentSystem`, both backends as stubs, `AGENT_MODE` config switch, V2 placeholder interfaces. **Not yet wired into CLI or Server DI.** |
| OpenAI Responses API provider | ✅ Implemented + tested | `OpenAiResponsesProvider` routes through `POST /v1/responses` when `LLM_WEB_SEARCH=true`. Injects `web_search_preview`, suppresses `WebSearch` function tool, full multi-turn agentic loop support. |
| Phase 7 hardening | ✅ Complete | Context auto-compaction (`SOVRANT_COMPACT_THRESHOLD`, default 80k tokens); BashTool 256 KB cap + dangerous env stripping; WebFetchTool SSRF guard (RFC-1918, loopback, link-local, non-HTTP(S)); provider retry 3×(1s/2s/4s) on 429/5xx; AgentTool recursion depth ≤ 5; ReadFileTool 10 MB cap; GlobTool 1000-file cap; atomic writes in Write/Edit tools. |

### Known issues fixed during testing

| Issue | Fix |
|---|---|
| Provider URL: hardcoded `/v1/chat/completions` overrode base URL path | Changed to relative `chat/completions`; base URL normalised to always have trailing slash |
| `ProviderApiProvider` (Anthropic `/v1/messages` format) was always registered and routed alongside `OpenAiCompatProvider` | Now only registered when `PROVIDER_BASE_URL` env var is explicitly set |
| `--permission-mode bypass-permissions` (hyphen) silently fell back to `Default` | Use `bypassPermissions` (camelCase) — matches the `PermissionMode` enum |
| `--session` option was parsed but never wired to `InitializeSessionAsync` | Fixed: session ID now applied to the same `IConversationRuntime` instance used for the turn |
| `DisableFastUpToDateCheck` missing — MSB3492 cache file race on parallel Windows builds | Added to `Directory.Build.props` |
| `ConversationRuntime` set `Stream=false` on internal `MessagesRequest` | Fixed: runtime always sets `Stream=true`; server buffers or forwards SSE independently |
| Server ran stale binary (pre-URL-fix) during smoke test — ping URL was `v1/v1/models` → 404, all providers unhealthy | Always rebuild server before smoke testing: `dotnet build src/Sovrant.Server` |

### Known open issues

| Issue | Details |
|---|---|
| ~~Token counts always show `0↑ 0↓`~~ | ✅ Fixed — `OpenAiCompatProvider` captures trailing OpenAI usage chunk; runtime reads `InputTokens` from `MessageDelta`. |
| ~~SmartRouter crashes when all providers fail startup ping~~ | ✅ Fixed — falls back to configured providers when all fail ping; `ConversationRuntime` catches routing exception and emits `RuntimeError` instead of crashing. |
| `AskUserQuestion` blocked in server mode | Returns a fixed "question blocked" message — by design; interactive prompts not possible in HTTP server context. |
| `launchSettings.json` / port conflict on rapid server restart | `src/Sovrant.Server/Properties/launchSettings.json` declares port `5091` that Kestrel overrides with `5200`. Rapid restart causes `SocketException (10048)`. **Mitigation:** always `pkill -f Sovrant.Server` first. **Fix (Phase 9):** align `launchSettings.json` port with `SOVRANT_PORT` and add `--urls` override for CI. |
| `EnterPlanMode` / `ExitPlanMode` are global in server mode | `IPermissionModeAccessor` wraps the shared `MutableServerConfig` singleton — `EnterPlanMode` in one session sets plan mode for all sessions. Fixed in Phase 9.5 (session-scoped `SessionConfig` overlay). |
| ~~No provider retry on 429 / 5xx~~ | ✅ Fixed — 3 attempts with 1s/2s/4s backoff on retryable errors in `ConversationRuntime`. |
| ~~`AgentTool` has no recursion depth limit~~ | ✅ Fixed — `AsyncLocal<int>` counter; rejects at depth ≥ 5. |
| `Sovrant.Agents` not wired into CLI or Server | `AddMultiAgentSystem()` exists but is not called in either host. `AgentTool` still uses direct `ConversationRuntime`. Phase 18 wires it up. |

---

## Tools — Test Results

Core tools tested with `gpt-4o-mini` (paid tier), `--permission-mode bypassPermissions`.
File tools also confirmed with `gemini-2.5-flash` (free tier, rate-limited).

### Core file tools

| Tool | Status | Result |
|---|---|---|
| `Read` | ✅ Tested | Reads file contents correctly |
| `Write` | ✅ Tested | Creates file with specified content |
| `Edit` | ✅ Tested | String replacement in existing file confirmed |
| `Glob` | ✅ Tested | Pattern match returns correct file list |
| `Grep` | ✅ Tested | Regex search across files works correctly |
| `LS` | ✅ Tested | Directory listing returned correctly |

### Shell tools

| Tool | Status | Result |
|---|---|---|
| `Bash` | ✅ Tested | Tool fires and executes. **Windows caveat:** requires WSL installed and updated (`wsl.exe --update`). Works on Linux/macOS natively |
| `PowerShell` | ⬜ Not tested | Implemented via `pwsh.exe` — should work on Windows with PowerShell 7 |
| `REPL` | ⬜ Not tested | Implemented; spawns subprocess per language (`python`, `node`, etc.) |

### Web tools

| Tool | Status | Result |
|---|---|---|
| `WebFetch` | ✅ Tested | Fetched `https://httpbin.org/get`; model correctly extracted response data |
| `WebSearch` | ⬜ Not tested | Implemented; requires `BRAVE_API_KEY` (or `FIRECRAWL_API_KEY` as fallback) |
| Native web search (`LLM_WEB_SEARCH=true`) | ✅ Tested | Routes through OpenAI Responses API (`/v1/responses`); `web_search_preview` built-in tool injected; `WebSearch` function tool suppressed; no Brave/FireCrawl key required |

### Task management tools

| Tool | Status | Result |
|---|---|---|
| `TodoWrite` | ✅ Tested | Created 2-item task list; model confirmed both items with priority |
| `TaskCreate` | ⬜ Not tested | Implemented; spawns background `dotnet` sub-process |
| `TaskGet` | ⬜ Not tested | Implemented; polls `BackgroundTaskRegistry` by task ID |
| `TaskList` | ⬜ Not tested | Implemented; lists all tracked background tasks |
| `TaskOutput` | ⬜ Not tested | Implemented; streams stdout from running background task |
| `TaskStop` | ⬜ Not tested | Implemented; cancels and removes background task |
| `TaskUpdate` | ⬜ Not tested | Implemented (Phase 7.5); updates task description |

### Agent & interaction tools

| Tool | Status | Result |
|---|---|---|
| `Agent` | ⬜ Not tested | Implemented; spawns isolated `ConversationRuntime` with its own session |
| `AskUserQuestion` | ✅ Tested | Prompted console correctly in CLI mode. Server mode returns fixed message (by design) |
| `Sleep` | ✅ Tested | Slept 1000ms and returned correctly |

### Plan mode tools *(Phase 7.5 Tier 1)*

| Tool | Status | Result |
|---|---|---|
| `EnterPlanMode` | ⬜ Not tested | Implemented; sets `IPermissionModeAccessor.Mode = Plan`. CLI: updates `MutableCliPermissionPolicy`. Server: updates `MutableServerConfig` via adapter |
| `ExitPlanMode` | ⬜ Not tested | Implemented; restores permission mode; optional `permission_mode` param (default: `DontAsk`) |

### Worktree tools *(Phase 7.5 Tier 1)*

| Tool | Status | Result |
|---|---|---|
| `EnterWorktree` | ⬜ Not tested | Implemented; runs `git worktree add`, records path in `WorktreeState` singleton; `create_branch` param for `-b` flag |
| `ExitWorktree` | ⬜ Not tested | Implemented; runs `git worktree remove`, clears `WorktreeState`; `force` param for `--force` |

### Skill & discovery tools *(Phase 7.5 Tier 2)*

| Tool | Status | Result |
|---|---|---|
| `Skill` | ⬜ Not tested | Implemented; reads `.sovrant/skills/{name}.md` (project-first, then global); substitutes `$ARGUMENTS` |
| `ToolSearch` | ⬜ Not tested | Implemented; searches registered tool names/descriptions by keyword via `IToolRegistry.GetDefinitions()` |

### MCP resource tools *(Phase 7.5 Tier 2)*

| Tool | Status | Result |
|---|---|---|
| `ListMcpResources` | ⬜ Not tested | Implemented; lists resources from connected MCP servers via `McpClientRegistry` |
| `ReadMcpResource` | ⬜ Not tested | Implemented; reads a resource by URI from a connected MCP server |

### Notebook tools

| Tool | Status | Result |
|---|---|---|
| `NotebookEdit` | ⬜ Not tested | Implemented; reads/writes Jupyter `.ipynb` JSON; cell replace/insert/delete |

---

## Provider Compatibility

| Provider | Tool Calling | Notes |
|---|---|---|
| `gemini-2.5-flash` (Google AI Studio) | ✅ Confirmed | Free tier: ~5 RPM. All core tools tested |
| `gpt-4o-mini` (OpenAI) | ✅ Confirmed | All tested tools pass; session continuity confirmed |
| `gemma-4-31b-it` (Google AI Studio) | ❌ No tool calls | Text generation works; function calling not supported via OpenAI-compat endpoint |
| `gemma-3-27b-it` (Google AI Studio) | ⬜ Not tested | Likely same limitation as Gemma 4 |
| Ollama (local) | ⬜ Not tested | Implemented; set `OLLAMA_BASE_URL`. Bash tool requires WSL/Linux |
| Native messages API (`ProviderApiProvider`) | ⬜ Not tested | Set `PROVIDER_BASE_URL=https://api.anthropic.com` + `PROVIDER_API_KEY` |

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `LLM_API_KEY` | Yes | API key for the primary provider. Aliases: `OPENAI_API_KEY`, `PROVIDER_API_KEY` (checked in order) |
| `LLM_BASE_URL` | No | Base URL (default: `https://api.openai.com/v1`). Alias: `OPENAI_BASE_URL` |
| `SOVRANT_TOKEN` | Yes (server only) | Bearer token for `Sovrant.Server`. All requests return 401 if unset. |
| `SOVRANT_PORT` | No | Server port (default: `5200`) |
| `PROVIDER_BASE_URL` | No | Enables the native messages API provider (`/v1/messages` format, e.g. `https://api.anthropic.com`) |
| `PROVIDER_API_KEY` | No | API key for the native messages API provider |
| `OLLAMA_BASE_URL` | No | Enables the local Ollama provider (default when set: `http://localhost:11434/v1`) |
| `ROUTER_MODE` | No | `Smart` (default) or `Fixed`. Overrides `Router:Mode` in config. |
| `ROUTER_STRATEGY` | No | `Balanced` (default), `Latency`, or `Cost`. Overrides `Router:Strategy` in config. |
| `AGENT_MODE` | No | `modern` (default, in-process async channels) or `legacy` (process-per-agent stdio). Used by `Sovrant.Agents` when wired in Phase 18. |
| `LLM_WEB_SEARCH` | No | Set to `true` to use the model's native web search capability (e.g. OpenAI `web_search_preview`). No external API key needed. |
| `BRAVE_API_KEY` | No | Enables `WebSearch` via Brave Search API |
| `FIRECRAWL_API_KEY` | No | Enables `WebSearch` via FireCrawl (fallback if `BRAVE_API_KEY` not set) |

---

## Server Smoke Test

> All 9 endpoints confirmed working. Two bugs found during initial testing are now fixed:
> - `ConversationRuntime` always sets `Stream=true` internally (was `false`, caused empty response bodies)
> - Stale server binary had double `/v1/v1/` in ping URL — always rebuild before testing
>
> Always build fresh before smoke testing:
> ```bash
> dotnet build src/Sovrant.Server -c Debug
> ```

```bash
export LLM_API_KEY="..."    # fresh key — never paste keys into chat
export SOVRANT_TOKEN=test123

# Start server
dotnet run --project src/Sovrant.Server --no-build &
sleep 5

# 1. Health (unauthenticated)
curl -s http://localhost:5200/health
# expected: {"status":"ok"}

# 2. Non-streaming chat
curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Reply with one word: pong"}],"model":"gpt-4o-mini","stream":false}'
# expected: {"choices":[{"message":{"content":"pong",...},...}],...}

# 3. Streaming chat (SSE)
curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Reply with one word: pong"}],"model":"gpt-4o-mini","stream":true}'
# expected: data: {...,"delta":{"content":"pong"},...}  then  data: [DONE]

# 4. Session continuity via server pool
curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"My name is Eric"}],"model":"gpt-4o-mini","session_id":"test-session-1"}'

curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"What is my name?"}],"model":"gpt-4o-mini","session_id":"test-session-1"}'
# expected: second response references "Eric"

# 5. Status endpoint
curl -s -H "Authorization: Bearer test123" http://localhost:5200/v1/status

# 6. Models endpoint
curl -s -H "Authorization: Bearer test123" http://localhost:5200/v1/models

# 7. Config update
curl -s -X PUT http://localhost:5200/v1/config \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o"}'

# 8. Session list
curl -s -H "Authorization: Bearer test123" http://localhost:5200/v1/sessions

# 9. Session delete
curl -s -X DELETE -H "Authorization: Bearer test123" http://localhost:5200/v1/sessions/test-session-1
```

## Tools Needing Smoke Tests

The following tools are implemented but have not been manually tested end-to-end with a live LLM:

| Tool | Notes |
|---|---|
| `PowerShell` | Requires PowerShell 7 (`pwsh`) on Windows |
| `REPL` | Spawns subprocess per language (`python`, `node`, etc.) |
| `WebSearch` | Requires `BRAVE_API_KEY` or `FIRECRAWL_API_KEY` |
| `TaskCreate` / `TaskGet` / `TaskList` / `TaskOutput` / `TaskStop` / `TaskUpdate` | Background task management suite |
| `Agent` | Spawns isolated `ConversationRuntime`; no recursion depth guard yet (Phase 7.8) |
| `EnterPlanMode` / `ExitPlanMode` | Global in server mode until Phase 9.5 |
| `EnterWorktree` / `ExitWorktree` | Requires git repo with at least one commit |
| `Skill` / `ToolSearch` | Requires `.sovrant/skills/` dir or registered tools |
| `ListMcpResources` / `ReadMcpResource` | Requires at least one connected MCP server |
| `NotebookEdit` | Requires a `.ipynb` file |
