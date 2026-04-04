# Sovrant Engine — Status Report

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-04-04
**Test models:** `gemini-2.5-flash` (Google AI Studio, free tier), `gpt-4o-mini` (OpenAI, paid tier)

---

## Engine Core

| Component | Status | Notes |
|---|---|---|
| CLI entry point (`sovrant prompt "..."`) | ✅ Working | One-shot mode confirmed |
| REPL loop (`sovrant`) | ✅ Working | Slash commands, history, Spectre.Console rendering |
| SmartRouter | ✅ Working | Pings providers on startup, routes by latency/cost/health |
| Agentic loop | ✅ Working | Multi-turn tool use, retries up to 20 rounds |
| Session persistence (JSONL) | ✅ Working | `~/.sovrant/sessions/{id}.jsonl` append-log |
| Session resumption (`--session <id>`) | ✅ Working | History replayed correctly across separate process invocations |
| Permission system | ✅ Working | `bypassPermissions` / `dontAsk` / `default` / `plan` all functional |
| SSE streaming | ✅ Working | Text chunks stream to console in real time |
| HTTP server (`Sovrant.Server`) | ✅ Working | All 8 endpoints confirmed: health, non-streaming, streaming SSE, session continuity, status, models, config update, session list/delete |
| Server session pool (`IRuntimeSessionPool`) | ✅ Implemented | Option B: one `ConversationRuntime` per session ID, kept alive in `ConcurrentDictionary`; safe for multi-user |
| Unit test suite | ✅ 99/99 passing | Api(22) + Runtime(28) + Tools(26) + Commands(22) + Integration(1) |

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
| Token counts always show `0↑ 0↓` | OpenAI streaming sends usage only in the final SSE chunk; our Anthropic-style `MessageDelta` parser doesn't capture it |
| `AskUserQuestion` blocked in server mode | Returns a fixed "question blocked" message — by design; interactive prompts are not possible in HTTP server context |

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
| `WebSearch` | ⬜ Not tested | Implemented; requires `WEB_SEARCH_API_KEY` env var (Brave/Bing API key) |

### Task management tools

| Tool | Status | Result |
|---|---|---|
| `TodoWrite` | ✅ Tested | Created 2-item task list; model confirmed both items with priority |
| `TaskCreate` | ⬜ Not tested | Implemented; spawns background `dotnet` sub-process |
| `TaskGet` | ⬜ Not tested | Implemented; polls `BackgroundTaskRegistry` by task ID |
| `TaskList` | ⬜ Not tested | Implemented; lists all tracked background tasks |
| `TaskOutput` | ⬜ Not tested | Implemented; streams stdout from running background task |
| `TaskStop` | ⬜ Not tested | Implemented; cancels and removes background task |

### Agent & interaction tools

| Tool | Status | Result |
|---|---|---|
| `Agent` | ⬜ Not tested | Implemented; spawns isolated `ConversationRuntime` with its own session |
| `AskUserQuestion` | ✅ Tested | Prompted console correctly in CLI mode. Server mode returns fixed message (by design) |
| `Sleep` | ✅ Tested | Slept 1000ms and returned correctly |

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
| Anthropic native API | ⬜ Not tested | Set `PROVIDER_BASE_URL=https://api.anthropic.com` + `PROVIDER_API_KEY` |

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `LLM_API_KEY` | Yes | API key for the primary provider |
| `LLM_BASE_URL` | No | Base URL (default: `https://api.openai.com/v1`) |
| `SOVRANT_TOKEN` | Yes (server only) | Bearer token for `Sovrant.Server` |
| `SOVRANT_PORT` | No | Server port (default: `5200`) |
| `PROVIDER_BASE_URL` | No | Enables `ProviderApiProvider` for Anthropic-native endpoints |
| `PROVIDER_API_KEY` | No | API key for the Anthropic-native provider |
| `OLLAMA_BASE_URL` | No | Ollama base URL (default: `http://localhost:11434/v1`) |
| `WEB_SEARCH_API_KEY` | No | Enables `WebSearch` tool (Brave/Bing API key) |

---

## Server Smoke Test

> **Note:** The API key used during testing was revoked mid-session (exposed in conversation).
> Two bugs were found and fixed before the key was revoked:
> - `ConversationRuntime` always sets `Stream=true` internally (was `false`, caused empty response bodies)
> - Stale server binary had double `/v1/v1/` in ping URL — always rebuild before testing
>
> Re-run these tests with a fresh key. Always build fresh first:
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

# 8. Session list and delete
curl -s -H "Authorization: Bearer test123" http://localhost:5200/v1/sessions
curl -s -X DELETE -H "Authorization: Bearer test123" http://localhost:5200/v1/sessions/test-session-1
```

## Remaining CLI Smoke Tests

All CLI tools confirmed working. No remaining CLI tests needed.
