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
| `LLM_API_KEY` | Yes | — | API key forwarded to the LLM provider. Alias: `OPENAI_API_KEY` |
| `LLM_BASE_URL` | No | `https://api.openai.com/v1` | LLM provider base URL. Alias: `OPENAI_BASE_URL` |
| `SOVRANT_TOKEN` | Yes | — | Bearer token that every client must supply. If unset the server returns 401 for all requests. |
| `SOVRANT_PORT` | No | `5200` | TCP port Kestrel listens on |
| `PROVIDER_BASE_URL` | No | — | Enables the native messages API provider (`/v1/messages` format). |
| `PROVIDER_API_KEY` | No | — | API key for the native messages API provider |
| `OLLAMA_BASE_URL` | No | `http://localhost:11434/v1` | Enables the local Ollama provider |
| `BRAVE_API_KEY` | No | — | Enables `WebSearch` via Brave Search API |
| `FIRECRAWL_API_KEY` | No | — | Enables `WebSearch` via FireCrawl (used if `BRAVE_API_KEY` is not set) |

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

- `model` — overrides server default for this request
- `session_id` — if supplied the server resumes that JSONL session from `~/.sovrant/sessions/{id}.jsonl`
- `stream` — defaults to `true`; set `false` for a single JSON response

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

> **Known issue:** Token counts are currently always `0` for OpenAI providers. OpenAI sends usage only in the final SSE chunk in a field our parser does not yet capture. Tracked in `docs/roadmap.md` under Known Issues.

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

> The `EnterPlanMode` and `ExitPlanMode` tools can also change the permission mode from within a conversation. In server mode they update `MutableServerConfig.PermissionMode` via the `IPermissionModeAccessor` interface — identical to calling `PUT /v1/config`.

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
