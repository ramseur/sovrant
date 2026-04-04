# Sovrant Engine — Status Report

**Branch:** `sovrant-openc-dotnet-port`
**Date:** 2026-04-04
**Test model:** `gemini-2.5-flash` via Google AI Studio OpenAI-compat endpoint
**Test key:** Free tier (5 RPM limit observed in practice)

---

## Engine Core

| Component | Status | Notes |
|---|---|---|
| CLI entry point (`sovrant prompt "..."`) | ✅ Working | One-shot and REPL modes functional |
| REPL loop (`sovrant`) | ✅ Working | Slash commands, history, Spectre.Console rendering |
| SmartRouter | ✅ Working | Pings providers on startup, routes by latency/cost/health |
| Agentic loop | ✅ Working | Multi-turn tool use, retries up to 20 rounds |
| Session persistence (JSONL) | ✅ Working | `~/.sovrant/sessions/{id}.jsonl` append-log |
| Session resumption (`--session <id>`) | ✅ Working | History replayed on `InitializeSessionAsync` |
| Permission system | ✅ Working | `bypassPermissions` / `dontAsk` / `default` / `plan` all functional |
| SSE streaming | ✅ Working | Text chunks stream to console in real time |
| HTTP server (`Sovrant.Server`) | ✅ Built | Not smoke-tested live; unit-tested |

### Known issues fixed during testing

| Issue | Fix |
|---|---|
| Provider URL: hardcoded `/v1/chat/completions` overrode base URL path | Changed to relative `chat/completions`; base URL normalised to always have trailing slash |
| `ProviderApiProvider` (Anthropic format) was always registered and routed alongside `OpenAiCompatProvider` | Now only registered when `PROVIDER_BASE_URL` env var is explicitly set |
| `--permission-mode bypass-permissions` (hyphen) silently fell back to `Default` | Use `bypassPermissions` (camelCase) — matches the `PermissionMode` enum |

---

## Tools — Test Results

Tests run with `gemini-2.5-flash`, `--permission-mode bypassPermissions`, free tier (rate-limited to ~5 RPM).

### Core file tools

| Tool | Tested | Result |
|---|---|---|
| `Read` | ✅ | Reads file contents correctly; confirmed on `README.md` |
| `Write` | ✅ | Creates file with specified content; `/tmp/sovrant_test.txt` created successfully |
| `Edit` | ✅ | String replacement in existing file; `hello world` → `hello sovrant` confirmed |
| `Glob` | ✅ | Pattern match returns correct file list; `*.slnx` found `Sovrant.slnx` |
| `Grep` | ✅ | Regex search across files; found `agentic` in 4 source files |
| `LS` | ✅ | Directory listing returned correctly |

### Shell tools

| Tool | Tested | Result |
|---|---|---|
| `Bash` | ✅ | Tool fires and executes; **WSL not installed/outdated on this machine** — bash commands fail at runtime. Works on Linux/macOS or Windows with WSL updated (`wsl.exe --update`) |
| `PowerShell` | ⬜ Not tested | Rate limited before reaching this test. Implemented via `pwsh.exe` — should work on Windows where PowerShell 7 is installed |
| `REPL` | ⬜ Not tested | Implemented; spawns subprocess per language (`python`, `node`, etc.) |

### Web tools

| Tool | Tested | Result |
|---|---|---|
| `WebFetch` | ⬜ Not tested | Implemented via `HttpClient`; fetches URL and returns content. Requires outbound HTTP access |
| `WebSearch` | ⬜ Not tested | Implemented; requires `WEB_SEARCH_API_KEY` env var (Brave/Bing API). Returns 400/error if key absent |

### Task management tools

| Tool | Tested | Result |
|---|---|---|
| `TodoWrite` | ⬜ Not tested (rate limited) | Implemented; in-session task list, persists across turns within same runtime instance |
| `TaskCreate` | ⬜ Not tested | Implemented; spawns background `dotnet` sub-process |
| `TaskGet` | ⬜ Not tested | Implemented; polls `BackgroundTaskRegistry` by task ID |
| `TaskList` | ⬜ Not tested | Implemented; lists all tracked background tasks |
| `TaskOutput` | ⬜ Not tested | Implemented; streams stdout from running background task |
| `TaskStop` | ⬜ Not tested | Implemented; cancels and removes background task |

### Agent & interaction tools

| Tool | Tested | Result |
|---|---|---|
| `Agent` | ⬜ Not tested | Implemented; spawns isolated `ConversationRuntime` with its own session |
| `AskUserQuestion` | ⬜ Not tested | Implemented; prompts stdin in CLI mode; returns fixed message in server mode |
| `Sleep` | ⬜ Not tested | Implemented; `Task.Delay(ms)` — straightforward |

### Notebook tools

| Tool | Tested | Result |
|---|---|---|
| `NotebookEdit` | ⬜ Not tested | Implemented; reads/writes Jupyter `.ipynb` JSON; cell replace/insert/delete |

---

## Provider Compatibility

| Provider | Tool Calling | Notes |
|---|---|---|
| `gemini-2.5-flash` (Google AI Studio) | ✅ | Confirmed working. Free tier: ~5 RPM |
| `gemma-4-31b-it` (Google AI Studio) | ❌ | Text generation works; function calling not supported via OpenAI-compat endpoint |
| `gemma-3-27b-it` (Google AI Studio) | ⬜ Not tested | Likely same limitation as Gemma 4 |
| OpenAI (`gpt-4o` etc.) | ⬜ Not tested | Should work — standard OpenAI format |
| Ollama (local) | ⬜ Not tested | Implemented; set `OLLAMA_BASE_URL`. WSL/Linux only for bash tool |
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

## Remaining Smoke Tests Needed

These were not run due to rate limiting. Run with a paid-tier key or after quota resets:

```bash
export LLM_API_KEY="..."
export LLM_BASE_URL="https://generativelanguage.googleapis.com/v1beta/openai/"
MODEL="gemini-2.5-flash"
PM="bypassPermissions"

# TodoWrite
sovrant --model $MODEL --permission-mode $PM prompt "Use TodoWrite to create tasks: 'item one', 'item two'"

# Sleep
sovrant --model $MODEL --permission-mode $PM prompt "Use the Sleep tool to sleep 1000ms then say done"

# WebFetch
sovrant --model $MODEL --permission-mode $PM prompt "Use WebFetch to fetch https://httpbin.org/get"

# AskUserQuestion
sovrant --model $MODEL --permission-mode $PM prompt "Use AskUserQuestion to ask me my favourite colour"

# Session continuity
sovrant --model $MODEL --session test-session-1 prompt "My name is Eric"
sovrant --model $MODEL --session test-session-1 prompt "What is my name?"

# Server smoke test
SOVRANT_TOKEN=test123 LLM_API_KEY=... dotnet run --project src/Sovrant.Server
curl -s http://localhost:5200/health
curl -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"hello"}],"model":"gemini-2.5-flash"}'
```
