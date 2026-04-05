# Sovrant Server

`Sovrant.Server` is an ASP.NET Core minimal-API HTTP server that wraps the sovrant engine and exposes an OpenAI-compatible interface. It is designed to sit behind a Node.js proxy (e.g. on Replit) so that a frontend application can stream chat responses without directly holding LLM credentials.

---

## Running the Server

```bash
# Minimum — no auth (all requests blocked until SOVRANT_TOKEN is set)
dotnet run --project src/Sovrant.Server

# Typical
LLM_API_KEY=sk-...        \
LLM_BASE_URL=https://api.openai.com/v1 \
SOVRANT_TOKEN=my-secret   \
dotnet run --project src/Sovrant.Server
```

The server binds to `http://127.0.0.1:5200` by default.

> **Port conflict:** `launchSettings.json` declares port `5091` which Kestrel immediately overrides with `5200`. If you start a second instance before the first has released the socket you will see `SocketException (10048): address already in use`. Always stop the running instance first (`pkill -f Sovrant.Server` on Linux/macOS, `Stop-Process -Name dotnet` on Windows) or set `SOVRANT_PORT` to a different port for the second instance.

---

## Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `LLM_API_KEY` | Yes | — | API key forwarded to the LLM provider. Aliases: `OPENAI_API_KEY`, `PROVIDER_API_KEY` (checked in order) |
| `LLM_BASE_URL` | No | `https://api.openai.com/v1` | LLM provider base URL. Alias: `OPENAI_BASE_URL` |
| `SOVRANT_TOKEN` | Yes | — | Bearer token all clients must supply. Returns 401 for all requests if unset. |
| `SOVRANT_PORT` | No | `5200` | TCP port Kestrel listens on |
| `PROVIDER_BASE_URL` | No | — | Enables the native messages API provider (`/v1/messages` format, e.g. Anthropic direct) |
| `PROVIDER_API_KEY` | No | — | API key for the native messages API provider |
| `OLLAMA_BASE_URL` | No | — | Enables the local Ollama provider when set (e.g. `http://localhost:11434/v1`) |
| `ROUTER_MODE` | No | `Smart` | `Smart` (latency/cost scoring) or `Fixed` (always first provider) |
| `ROUTER_STRATEGY` | No | `Balanced` | `Balanced`, `Latency`, or `Cost` |
| `AGENT_MODE` | No | `modern` | `modern` (in-process async, recommended) or `legacy` (process-per-agent stdio). Controls which `IMultiAgentSystem` backend is used by team tools (`TeamCreate`, `TeamDelegate`, etc.). |
| `LLM_WEB_SEARCH` | No | `false` | Set to `true` to route through OpenAI Responses API with `web_search_preview`. No Brave/FireCrawl key required. |
| `BRAVE_API_KEY` | No | — | Enables `WebSearch` via Brave Search API |
| `FIRECRAWL_API_KEY` | No | — | Enables `WebSearch` via FireCrawl (fallback if `BRAVE_API_KEY` not set) |
| `SOVRANT_SESSION_TTL_SECONDS` | No | `3600` | Idle session TTL in seconds before automatic eviction from the in-memory pool |
| `SOVRANT_MAX_SESSIONS` | No | `500` | Maximum active sessions in the pool. LRU eviction when exceeded. |
| `SOVRANT_LOG_LEVEL` | No | `Information` | Minimum log level: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `SOVRANT_LOG_FILE` | No | `~/.sovrant/logs/sovrant-{Date}.log` | Rolling file path pattern. Empty string disables file logging. |
| `SOVRANT_LOG_CONSOLE` | No | `true` | Write logs to stdout. Set to `false` to silence console output. |
| `SOVRANT_LOG_FORMAT` | No | `text` | `text` (human-readable) or `json` (structured) |
| `SOVRANT_RATE_LIMIT_RPM` | No | `60` | Per-session request rate limit (requests per minute). Keyed on `X-Session-Id` header or client IP. Returns `429` when exceeded. |

---

## Authentication

Every request (except `GET /health` and CORS preflight `OPTIONS`) must include:

```
Authorization: Bearer <SOVRANT_TOKEN>
```

A missing or wrong token returns `401 Unauthorized`.

---

## Endpoints

### Health

```
GET /health
```

Unauthenticated. Returns `{"status":"ok"}`. Safe for load-balancer probes.

---

### Chat Completions — `POST /v1/chat/completions`

OpenAI-compatible. Streams by default.

**Request body**

```json
{
  "model": "gpt-4o",
  "messages": [
    { "role": "user", "content": "Hello" }
  ],
  "stream": true,
  "max_tokens": 4096,
  "session_id": "optional-session-id"
}
```

**Per-request credential headers** (optional):

```
X-LLM-Api-Key: sk-...
X-LLM-Base-Url: https://api.openai.com/v1
```

- `model` — overrides server default for this request
- `session_id` — if supplied the server resumes that JSONL session from `~/.sovrant/sessions/{id}.jsonl`
- `stream` — defaults to `true`; set `false` for a single JSON response
- `X-LLM-Api-Key` header — per-request LLM API key; overrides the server's global `LLM_API_KEY` for this call only. The server never logs, persists, or includes this value in error responses.
- `X-LLM-Base-Url` header — per-request LLM base URL; overrides the server's global `LLM_BASE_URL` for this call only. When combined with `X-LLM-Api-Key`, creates a request-scoped provider.

When `X-LLM-Api-Key` is supplied, the server creates a request-scoped `OpenAiCompatProvider` using the provided credentials. Sessions are isolated by a composite key (`{session_id}::{provider_name}`) so two clients with different keys sharing the same `session_id` will not collide.

**Streaming response (SSE)**

```
data: {"id":"...","object":"chat.completion.chunk","choices":[{"delta":{"content":"Hi"},"index":0}]}

data: {"id":"...","choices":[{"delta":{},"finish_reason":"stop"}],"sovrant":{"event":"tool_use","tool_name":"Bash","tool_use_id":"tu_123","is_error":false}}

data: [DONE]
```

The `sovrant` extension field appears on tool-related chunks and carries:

| Field | Type | Description |
|---|---|---|
| `event` | string | `tool_use`, `tool_result`, or `text` |
| `tool_name` | string? | Name of the tool being called |
| `tool_use_id` | string? | Correlates call with result |
| `is_error` | bool | Whether the tool result is an error |

**Non-streaming response**

```json
{
  "id": "chatcmpl-...",
  "object": "chat.completion",
  "choices": [{ "message": { "role": "assistant", "content": "Hi there" }, "finish_reason": "stop", "index": 0 }],
  "usage": { "prompt_tokens": 12, "completion_tokens": 4, "total_tokens": 16 }
}
```

> Token counts are now captured correctly. OpenAI sends usage in a trailing SSE chunk after `finish_reason`; the provider continues reading after `finish_reason` and emits a final `MessageDelta` with the correct `prompt_tokens` / `completion_tokens`.

---

### Config — `GET /v1/config`

Returns the current live configuration (API key is never returned).

```json
{
  "model": "gpt-4o",
  "base_url": "https://api.openai.com/v1",
  "permission_mode": "DontAsk",
  "pinned_provider": null
}
```

---

### Config — `PUT /v1/config`

Mutates configuration without restarting the server. All fields are optional.

```json
{
  "model": "gpt-4o-mini",
  "api_key": "sk-new-key",
  "base_url": "https://api.openai.com/v1",
  "permission_mode": "DontAsk",
  "provider": "openai"
}
```

Returns `200 {"updated": true}` on success, or `400 {"error": "..."}` if the provider name is unknown.

---

### Status — `GET /v1/status`

Returns provider health and current settings.

```json
{
  "providers": [
    { "name": "openai", "healthy": true, "latency_ms": 142, "score": 0.91 }
  ],
  "model": "gpt-4o",
  "permission_mode": "DontAsk"
}
```

---

### Models — `GET /v1/models`

OpenAI-compatible model list built from known providers.

```json
{
  "object": "list",
  "data": [
    { "id": "openai", "object": "model", "created": 1700000000, "owned_by": "sovrant" }
  ]
}
```

---

### Sessions — `GET /v1/sessions`

Lists all saved session IDs.

```json
{ "sessions": [{ "id": "abc123" }, { "id": "def456" }] }
```

---

### Sessions — `GET /v1/sessions/{id}`

Returns the user/assistant message history for a session.

```json
{
  "session_id": "abc123",
  "messages": [
    {
      "role": "user",
      "content": "Hello",
      "timestamp": "2026-04-03T12:00:00+00:00",
      "input_tokens": 5,
      "output_tokens": 0
    }
  ]
}
```

Returns `404` if the session does not exist.

---

### Sessions — `DELETE /v1/sessions/{id}`

Permanently deletes the JSONL file for the session.

```json
{ "deleted": "abc123" }
```

Returns `404` if the session does not exist.

---

### Session Config — `GET /v1/sessions/{id}/config`

Returns the effective configuration for an active session (model, permission mode).

```json
{
  "model": "gpt-4o",
  "permission_mode": "dontask",
  "is_overridden": false
}
```

`is_overridden` is `true` if the session has a per-session model or permission mode override. Returns `404` if the session is not currently active in the pool.

---

### Session Config — `PUT /v1/sessions/{id}/config`

Updates per-session config overlay. All fields are optional.

```json
{
  "model": "gpt-4o-mini",
  "permission_mode": "Plan"
}
```

Returns `200 {"updated": true}` on success. Returns `404` if the session is not active.

Per-session config shadows the global defaults set via `PUT /v1/config`. Only the specified session is affected — other sessions continue using global defaults or their own overrides.

---

### Usage — `GET /v1/usage`

Returns per-session token usage summary across all sessions.

```json
{
  "sessions": [
    {
      "session_id": "user-123",
      "input_tokens": 1200,
      "output_tokens": 800,
      "total_tokens": 2000
    }
  ],
  "total_input_tokens": 1200,
  "total_output_tokens": 800,
  "total_tokens": 2000
}
```

Active sessions report live in-memory counters. Inactive sessions sum from persisted JSONL entries.

---

## CORS

Allowed origins (no credentials required from the proxy — the proxy injects the bearer token):

```
http://localhost
http://localhost:3000
http://localhost:5173
http://localhost:8080
http://127.0.0.1
http://127.0.0.1:3000
http://127.0.0.1:5173
http://127.0.0.1:8080
```

Requests from any other origin are blocked by the browser. The server itself does not enforce origin on server-to-server calls.

---

## Permissions

The server defaults to `DontAsk` — tools run without interactive prompts. This is required because there is no user to prompt over an HTTP stream.

Change the permission mode live via `PUT /v1/config`:

```bash
curl -X PUT http://127.0.0.1:5200/v1/config \
  -H "Authorization: Bearer $SOVRANT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"permission_mode":"BypassPermissions"}'
```

Valid values: `Default`, `AcceptEdits`, `BypassPermissions`, `DontAsk`, `Plan`

> The `EnterPlanMode` and `ExitPlanMode` tools change the permission mode for the **current session only** via `SessionConfig`, not the global config. This prevents one user entering Plan mode from affecting other sessions. Use `PUT /v1/sessions/{id}/config` for explicit per-session overrides, or `PUT /v1/config` for server-wide defaults.

---

## Session Persistence

Sessions are stored as JSONL append-logs at:

```
~/.sovrant/sessions/{session_id}.jsonl
```

Pass `session_id` in the chat request body to resume a conversation. If the ID does not exist a new session is created with that ID. Session files rotate at 256 KB (3 rotations kept).

---

## AskUserQuestion Tool

The `AskUserQuestion` tool cannot pause an SSE stream to wait for a response. When the model calls it the server returns a fixed message:

```
[User input is not available in server mode. Please proceed without it.]
```
