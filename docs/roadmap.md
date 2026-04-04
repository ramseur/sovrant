# Sovrant — Roadmap

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-04-04

This document tracks planned features, architectural decisions, and the reasoning behind them.

---

## Current State (Phase 7 complete)

The engine is fully functional as a single-user tool:

- CLI REPL and one-shot prompt mode
- Agentic loop with up to 20 tool rounds per turn
- All 20+ tools working on Windows and Linux
- JSONL session persistence
- SmartRouter with health/latency scoring across multiple providers
- HTTP server (`Sovrant.Server`) with OpenAI-compatible endpoints
- In-memory session pool (one `ConversationRuntime` per `session_id`, safe for multiple users)

---

## Roadmap

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

### Phase 9 — Multiple Engine Instances Per Server

**Goal:** Allow the server to manage multiple independent `ConversationRuntime` instances concurrently — one per active session — with automatic eviction of idle sessions.

#### Motivation

The current session pool keeps runtimes alive indefinitely. For a production deployment serving many users, idle sessions consume memory and potentially hold references to expensive resources.

#### Design

- Add a `session_ttl_seconds` config (default: 3600)
- `RuntimeSessionPool` tracks last-access time per session
- A background `IHostedService` evicts sessions older than the TTL
- `DELETE /v1/sessions/{id}` continues to work for explicit eviction
- Optionally: LRU eviction with a max session count cap

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
