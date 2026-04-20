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
| `AGENT_MODE` | No | `isolated` | `isolated` (process-per-agent stdio, default) or `shared` (in-process async). Controls which `IOrchestrationSystem` backend is used by team tools (`TeamCreate`, `TeamDelegate`, etc.). |
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
| `SOVRANT_DB_PATH` | No | `~/.sovrant/data/sovrant.db` | SQLite database file path. CLI and server share the same database. |
| `SOVRANT_USER_ID` | No | OS username | User identity for session ownership and audit logging |
| `SOVRANT_SESSION_JSONL` | No | `false` | Set to `true` to also write sessions to legacy JSONL files (dual-write for migration) |
| `SOVRANT_AUDIT_JSONL` | No | `false` | Set to `true` to also write audit events to legacy JSONL files (dual-write for migration) |
| `SOVRANT_UNSAFE_DONTASK` | No | `false` | Set to `true` to disable graduated tool tier enforcement in `DontAsk` mode (auto-approve all tools, including Dangerous/Escalation) |
| `SOVRANT_RUNTIME_MODE` | No | `embedded` | Controls how `Sovrant.Web` connects to the runtime. `embedded` runs the agentic loop in-process; `remote` connects to a `Sovrant.Server` instance via SignalR + REST. |
| `SOVRANT_SERVER_URL` | No | `http://localhost:5200` | Base URL of the remote `Sovrant.Server` instance. Required when `SOVRANT_RUNTIME_MODE=remote`. |
| `SOVRANT_API_TOKEN` | No | — | Bearer token sent to the remote server for auth. Required when `SOVRANT_RUNTIME_MODE=remote`. |
| `SOVRANT_COST_PROVIDER` | No | `openrouter` | Cost tracking provider. Set to `none` to disable cost tracking. |

---

## Authentication (Phase 38)

Every request (except `GET /health` and CORS preflight `OPTIONS`) must include:

```
Authorization: Bearer <token>
```

The server accepts **two** kinds of bearer token, checked in this order by `BearerTokenMiddleware`:

1. **Per-user tokens** — strings of the form `svt_<base64url-secret>`, issued to a specific user via `POST /v1/users/me/tokens` (self-service) or `POST /v1/users/{id}/tokens` (admin). Tokens are stored as SHA-256 hashes in the `api_tokens` table; the plaintext is returned **exactly once** at issuance and is never recoverable. The middleware resolves the caller's `user_id` and role from this token and stamps them onto `HttpContext` for downstream ownership checks. Revoked or expired tokens return `401`.

2. **Legacy static token** — the value of the `SOVRANT_TOKEN` env var, used for server-to-server / bootstrap scenarios. Requests authenticated with the static token are treated as **admin** (unrestricted cross-user view) but have no associated `user_id`. `/v1/users/me/*` routes reject static-token callers with `400` since "me" is meaningless without a user identity.

A missing, malformed, expired, or revoked token returns `401 Unauthorized`. Token comparison is timing-safe (`CryptographicOperations.FixedTimeEquals`).

### Admin bypass

A request is treated as admin when **any** of the following holds:

- it is authenticated with the legacy `SOVRANT_TOKEN`, or
- it is authenticated with an `svt_*` token whose owning user has `users.role = 'admin'`.

Admins bypass ownership scoping on session/usage routes (see below) and can manage any user via `/v1/users/{id}/*`. Non-admin `svt_*` callers see only their own data.

### Ownership scoping

All session-bearing endpoints — `GET /v1/sessions`, `GET /v1/sessions/{id}`, `DELETE /v1/sessions/{id}`, `GET /v1/sessions/{id}/export`, `GET/PUT /v1/sessions/{id}/config`, and `GET /v1/usage` — filter results by the caller's `user_id`. A non-admin caller attempting to read, modify, or delete another user's session receives `404 Not Found` (not `403`) so that session IDs cannot be enumerated across users. Admin callers (static token or `users.role = 'admin'`) see the full set.

Ownership is stamped at the `SqliteSessionStore` layer with `INSERT OR IGNORE` semantics: the first append to a given `session_id` records the owner in `sessions.user_id`; subsequent appends from any caller cannot overwrite it. The `POST /v1/chat/completions` path performs a pre-flight `GetOwnerAsync` check and rejects attempts to attach to an already-owned session belonging to a different user. The runtime session pool is keyed on `{session_id}##{owner_user_id}` so two concurrent users cannot race to share a pool entry.

### Database hardening

- The SQLite file is created with mode `0600` (user-only) on first initialization.
- `PRAGMA secure_delete = ON` so revoked tokens and deleted session entries do not linger in free pages.
- `ISqliteConnectionFactory.CreateReadOnlyConnection()` opens the database with `Mode=ReadOnly` and `PRAGMA query_only = ON` for read-only code paths that must not mutate state.
- Token hashes are 32 raw bytes (SHA-256) — no reversible encoding, no pepper stored alongside.

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
- `session_id` — if supplied the server resumes that session from the SQLite database
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
| `event` | string | `tool_use`, `tool_result`, `text`, `clarification`, `plan_presented`, or `step_progress` |
| `tool_name` | string? | Name of the tool being called |
| `tool_use_id` | string? | Correlates call with result |
| `is_error` | bool | Whether the tool result is an error |
| `clarification` | string? | Phase 59 — clarification question when intent is ambiguous |
| `plan_id` | string? | Phase 59 — unique plan identifier |
| `formatted_plan` | string? | Phase 59 — human-readable plan description |
| `requires_approval` | bool? | Phase 59 — whether user must approve before execution |
| `step_current` | int? | Phase 59 — current step number (1-based) |
| `step_total` | int? | Phase 59 — total steps in plan |
| `step_intent` | string? | Phase 59 — intent description for current step |
| `step_status` | string? | Phase 59 — step lifecycle status (started/completed/failed) |

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

Returns provider health, session pool state, and current settings.

```json
{
  "providers": [
    { "name": "openai", "healthy": true, "latency_ms": 142, "request_count": 50, "error_count": 1, "error_rate": "2.0%", "score": "0.91" }
  ],
  "active_model": "gpt-4o",
  "permission_mode": "DontAsk",
  "pinned_provider": null,
  "active_sessions": 3,
  "max_sessions": 500,
  "session_ttl_seconds": 3600
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

Lists saved session IDs. Non-admin callers see only sessions they own; admin callers see all sessions.

```json
{ "sessions": [{ "id": "abc123" }, { "id": "def456" }] }
```

---

### Sessions — `GET /v1/sessions/{id}`

Returns the user/assistant message history for a session. Non-admin callers attempting to read a session they do not own receive `404 Not Found`.

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

Permanently deletes the session and all its entries from the database. The deletion goes through `ISessionStore.DeleteAsync` which enforces ownership at the storage layer — non-admin callers attempting to delete a session they do not own receive `404 Not Found` and the session is left untouched.

```json
{ "deleted": "abc123" }
```

Returns `404` if the session does not exist or is not visible to the caller.

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

Returns per-session token usage. Non-admin callers see only their own sessions; admin callers see all sessions.

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

Active sessions report live in-memory counters. Inactive sessions sum from persisted SQLite entries.

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
http://127.0.0.1:5100
http://127.0.0.1:5173
http://127.0.0.1:8080
```

Requests from any other origin are blocked by the browser. The server itself does not enforce origin on server-to-server calls.

---

## Permissions

The server defaults to `DontAsk` — tools run without interactive prompts. This is required because there is no user to prompt over an HTTP stream.

> **Phase 59 — Graduated Tool Tiers:** `DontAsk` mode no longer auto-approves all tools. Tools are classified into four tiers via `GraduatedToolTiers`:
>
> | Tier | Behavior in DontAsk | Examples |
> |---|---|---|
> | **Safe** | Auto-approve | Read, Glob, Grep, LS, WebFetch, WebSearch, Sleep, AskUserQuestion |
> | **Moderate** | Auto-approve | Write, Edit, NotebookEdit, TodoWrite, Skill, Artifact, McpProxy |
> | **Dangerous** | Require confirmation | Bash, PowerShell, REPL |
> | **Escalation** | Always show plan | Agent, TeamDelegate, Swarm, Mission |
>
> To restore the old behavior (auto-approve everything), set `SOVRANT_UNSAFE_DONTASK=true`.

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

## Trust Boundary (Phase 58)

Every LLM provider interaction is optionally wrapped by the **Sovrant Trust Boundary** — a three-stage pipeline that runs as an `ILlmProvider` decorator (`TrustBoundaryProvider`):

1. **Intent Verification** — bridges Phase 59's `IIntentGate` to catch ambiguous or harmful intent before any data is sent
2. **Ethical Harness** — Sovrant-level content policy enforcement independent of model safety (works with uncensored models)
3. **Data Sanitizer** — strips PII and corporate data from outbound prompts, restores placeholders on response

Configure via `trust_boundary` in `settings.json`:

```json
{
  "trust_boundary": {
    "enabled": true,
    "sanitizer": {
      "enabled": true,
      "mode": "redact",
      "corporate_domains": ["acme.com", "internal.corp"],
      "custom_patterns": [{ "name": "codenames", "regex": "\\bProject Titan\\b", "action": "redact" }],
      "allow_list": ["github.com"],
      "exempt_providers": ["ollama"]
    },
    "ethical_harness": {
      "enabled": true,
      "strictness": "standard",
      "response_scanning": true,
      "audit_log": true
    },
    "intent_verification": {
      "enabled": true,
      "clarify_ambiguous": true,
      "block_harmful_intent": true
    }
  }
}
```

Blocked responses include `StopReason = "trust_boundary_block"` and a clear explanation of what was blocked and why. The ethical audit log records all blocks and flags for compliance reporting.

---

## Session Persistence

Sessions are stored in the SQLite database at `~/.sovrant/data/sovrant.db` (override with `SOVRANT_DB_PATH`). Session entries include full-text search via FTS5.

Pass `session_id` in the chat request body to resume a conversation. If the ID does not exist a new session is created with that ID. Set `SOVRANT_SESSION_JSONL=true` to also write to legacy JSONL files.

For the full persistence architecture, see [persistence.md](persistence.md).

---

## AskUserQuestion Tool

The `AskUserQuestion` tool cannot pause an SSE stream to wait for a response. When the model calls it the server returns a fixed message:

```
[User input is not available in server mode. Please proceed without it.]
```

---

## Eval Endpoints

### List Eval Suites — `GET /v1/evals`

Returns all available eval suites found in `.sovrant/evals/` and `~/.sovrant/evals/`.

### Eval History — `GET /v1/evals/{suiteName}/history`

Returns trend data (pass rates, durations) for a named suite from `~/.sovrant/evals/results/`.

### Run Eval Suite — `POST /v1/evals/run`

```json
{ "suite_name": "my-suite", "tag": "regression" }
```

Runs the named suite (optionally filtered by tag) and returns the full report with per-eval results.

---

## Swarm Endpoints

### Start Swarm — `POST /v1/swarm`

Starts a swarm orchestration. Returns an SSE stream of swarm events (plan creation, task start/complete/fail, quality gate, final result).

```json
{
  "prompt": "Refactor auth to use JWT",
  "team": "backend-team",
  "dry_run": false
}
```

- `prompt` (required) — the task to decompose and execute
- `team` (optional) — orchestrated team whose members should be used as swarm workers
- `dry_run` (optional) — if `true`, only decompose and return the plan without executing

### Swarm Status — `GET /v1/swarm/{id}`

Returns the current status and task states for a swarm.

### Swarm Events — `GET /v1/swarm/{id}/events`

Replays the full event log for a swarm session from the `swarm_events` table (Phase 37.5 — previously read from JSONL files).

### List Swarm Sessions — `GET /v1/swarm/sessions`

Returns recorded swarm session IDs, most-recently-active first. As of Phase 37.5 the response is sourced from the `swarm_events` table and supports server-side filtering:

| Query parameter | Meaning |
|---|---|
| `workspace_id` | Restrict to swarms whose events were stamped with this workspace |
| `project_id`   | Restrict to swarms whose events were stamped with this project |
| `limit`        | Maximum number of swarm IDs to return |

When `POST /v1/swarm` runs, the workspace/project scope is taken from `WorkspaceContextMiddleware` (`HttpContext.Items["WorkspaceId"]`) and the optional `X-Project-Id` request header — every event written during that swarm's run is stamped with that scope, so subsequent calls to `GET /v1/swarm/sessions?workspace_id=…` will only see swarms the caller was scoped to.

Legacy JSONL session files from before Phase 37.5 can be imported into the table with the CLI helper:

```
sovrant db import-swarm [--dir <path>] [--delete-source]
```

## User Management

CRUD over the `users` table plus per-user data views. **Auth model (Phase 38):** these endpoints require an **admin** bearer token — either the legacy `SOVRANT_TOKEN` or an `svt_*` token belonging to a user with `users.role = 'admin'`. Non-admin `svt_*` callers receive `403 Forbidden`. Self-service profile and token routes that don't require admin live under `/v1/users/me/*` (see [Self-Service](#self-service-phase-38) below).

**Soft-delete only.** `DELETE /v1/users/{id}` flips `status='inactive'`; the row is preserved so all FK references (workspaces, projects, sessions, audit) remain valid. Hard-delete is intentionally not exposed.

**Mass-assignment protection.** Request bodies only accept client-writable fields (`username`, `email`, `role`, `team`, `status`). `user_id`, `created_at`, and `updated_at` are server-controlled and cannot be set via JSON.

**Validation.**
- `username` must match `^[a-zA-Z0-9._-]{1,64}$`
- `email` is optional, ≤ 254 chars, must contain `@` and `.`
- `role` ∈ {`user`, `admin`}
- `status` ∈ {`active`, `inactive`}
- `team` is optional, ≤ 64 chars

### Create User — `POST /v1/users`

Request:
```json
{
  "username": "alice",
  "email": "alice@example.com",
  "role": "user",
  "team": "engineering"
}
```

Response (`201 Created`):
```json
{
  "user_id": "usr_a1b2c3d4e5f60718",
  "username": "alice",
  "email": "alice@example.com",
  "role": "user",
  "team": "engineering",
  "status": "active",
  "created_at": "2026-04-06T...",
  "updated_at": "2026-04-06T..."
}
```

The server generates a `usr_{16hex}` `user_id` and **auto-creates a personal workspace** for the new user (mirrors the seeded default user behavior). Returns `400` for validation failures and `409` if the username or email is already taken.

### List Users — `GET /v1/users`

Query params: `status`, `role`, `team`, `limit` (1–1000, default 100), `offset` (default 0).

Response:
```json
{ "users": [ ... ], "count": 5, "total": 17 }
```

### Get User — `GET /v1/users/{id}`

Returns the user profile with derived stats: `session_count`, `total_input_tokens`, `total_output_tokens`, `total_cost_usd`, and `last_seen_at` (computed from `MAX(updated_at)` on the user's sessions; `null` if the user has no sessions).

### Update User — `PUT /v1/users/{id}`

Request body — every field optional; only the provided fields are changed:
```json
{ "username": "alice2", "email": "new@example.com", "role": "admin", "team": "ops", "status": "active" }
```

Returns `404` if the user does not exist, `400` for validation failures, `409` for unique-constraint violations.

### Deactivate User — `DELETE /v1/users/{id}`

Sets `status='inactive'`. Returns `409` if the target is the **server boot identity** (the row matching `SOVRANT_USER_ID` / OS username) — deactivating it would brick session seeding on the next restart.

### Reactivate User — `POST /v1/users/{id}/reactivate`

Restores `status='active'` for a previously deactivated user.

### List User Sessions — `GET /v1/users/{id}/sessions`

Query params: `limit`, `offset`. Returns session IDs newest-first.

### Get User Usage — `GET /v1/users/{id}/usage`

Query params: `model`, `from`, `to`. Returns aggregated token totals plus a per-model breakdown sourced from `token_usage`.

### List User Audit — `GET /v1/users/{id}/audit`

Query param: `limit`. Joins `audit_governance` through `sessions.user_id`. As of Phase 38, sessions are stamped with the authenticated caller's `user_id` at creation (pulled from the `svt_*` token), so audit views reflect real per-user activity. Sessions created via the legacy `SOVRANT_TOKEN` fall back to `SOVRANT_USER_ID` / OS username.

---

## Self-Service (Phase 38)

Routes scoped to the authenticated caller. Every endpoint resolves the target user from the `svt_*` token's identity — there is no `{userId}` path parameter to forge. Static-token callers receive `400` since "me" is meaningless without a user identity.

### Profile — `GET /v1/users/me`

Returns the caller's own profile plus derived stats (same shape as `GET /v1/users/{id}`).

### Issue Token — `POST /v1/users/me/tokens`

Issues a new `svt_*` bearer token for the caller. The plaintext secret is returned **exactly once** — capture it immediately.

Request:
```json
{
  "name": "laptop-dev",
  "scopes": "chat,sessions",
  "expires_at": "2026-12-31T23:59:59Z"
}
```

All fields optional. `scopes` is a free-form comma-separated string persisted for future use (not yet enforced beyond admin/user roles). `expires_at` is optional — omit for a non-expiring token.

Response (`201 Created`):
```json
{
  "token": {
    "token_id": "tok_...",
    "user_id": "usr_...",
    "name": "laptop-dev",
    "scopes": "chat,sessions",
    "created_at": "2026-04-09T...",
    "last_used_at": null,
    "expires_at": "2026-12-31T23:59:59+00:00",
    "revoked_at": null
  },
  "plaintext": "svt_BASE64URL..."
}
```

### List Tokens — `GET /v1/users/me/tokens`

Returns the caller's tokens (metadata only — no plaintext, no hash).

### Revoke Token — `DELETE /v1/users/me/tokens/{tokenId}`

Marks the token as revoked (`revoked_at = now()`). Callers cannot revoke tokens they do not own — the server verifies ownership by listing the caller's tokens before issuing the revoke, returning `404` otherwise.

---

## Workspace Endpoints

Workspaces are the top-level organizational unit. Every user gets a personal workspace on creation. Team workspaces are created via the API.

### List Workspaces — `GET /v1/workspaces`

Returns workspaces the authenticated user belongs to.

### Create Workspace — `POST /v1/workspaces`

```json
{ "name": "Engineering", "slug": "engineering" }
```

Returns `201` with the created workspace. The caller is automatically added as owner.

### Get Workspace — `GET /v1/workspaces/{id}`

### Update Workspace — `PUT /v1/workspaces/{id}`

```json
{ "name": "New Name", "slug": "new-slug" }
```

### Delete Workspace — `DELETE /v1/workspaces/{id}`

Personal workspaces cannot be deleted (returns `400`).

### List Members — `GET /v1/workspaces/{id}/members`

### Add Member — `POST /v1/workspaces/{id}/members`

```json
{ "user_id": "usr_...", "role": "editor" }
```

### Remove Member — `DELETE /v1/workspaces/{id}/members/{userId}`

### Create Invite — `POST /v1/workspaces/{id}/invites`

```json
{ "email": "bob@example.com", "role": "editor" }
```

Returns the invite with a one-time token.

### Delete Invite — `DELETE /v1/workspaces/{id}/invites/{inviteId}`

### Accept Invite — `POST /v1/workspaces/invites/accept`

```json
{ "token": "invite-token-here" }
```

### Get Config — `GET /v1/workspaces/{id}/config`

### Update Config — `PUT /v1/workspaces/{id}/config`

```json
{ "key": "value" }
```

### Get Usage — `GET /v1/workspaces/{id}/usage`

Returns aggregated token usage for the workspace.

### List Memory — `GET /v1/workspaces/{id}/memory`

Query param: `layer` (optional filter by memory layer).

### Save Memory — `POST /v1/workspaces/{id}/memory`

```json
{ "layer": "project", "content": "Remember this", "confidence": 0.9 }
```

### Delete Memory — `DELETE /v1/workspaces/{id}/memory/{memoryId}`

---

## Project Endpoints

Projects belong to a workspace and provide scoping for sessions, artifacts, memory, and config.

### List Projects — `GET /v1/workspaces/{wid}/projects`

Query param: `includeArchived` (optional, default false).

### Create Project — `POST /v1/workspaces/{wid}/projects`

```json
{ "name": "API", "slug": "api", "description": "Backend API" }
```

### Get Project — `GET /v1/projects/{id}`

### Update Project — `PUT /v1/projects/{id}`

```json
{ "name": "New Name", "slug": "new-slug", "description": "Updated" }
```

### Delete Project — `DELETE /v1/projects/{id}`

Admin only.

### Archive Project — `POST /v1/projects/{id}/archive`

### Unarchive Project — `POST /v1/projects/{id}/unarchive`

### List Members — `GET /v1/projects/{id}/members`

### Add Member — `POST /v1/projects/{id}/members`

```json
{ "user_id": "usr_...", "role": "editor" }
```

### Remove Member — `DELETE /v1/projects/{id}/members/{userId}`

### Get Config — `GET /v1/projects/{id}/config`

Query param: `resolved` (optional, merge with workspace config).

### Update Config — `PUT /v1/projects/{id}/config`

### List Sessions — `GET /v1/projects/{id}/sessions`

### Get Usage — `GET /v1/projects/{id}/usage`

### Get Memory — `GET /v1/projects/{id}/memory`

Query param: `layer` (optional filter by memory layer).

---

## Team Endpoints

Teams are named groups of agents that can be run together. Each team member has a role, system prompt, and optional agent template.

### Create Team — `POST /v1/teams`

```json
{ "name": "reviewers", "workspace_id": "ws_...", "project_id": "proj_..." }
```

### List Teams — `GET /v1/teams`

Query param: `workspaceId` (optional filter).

### Get Team — `GET /v1/teams/{id}`

Returns the team record and its members.

### Delete Team — `DELETE /v1/teams/{id}`

### Add Member — `POST /v1/teams/{id}/members`

```json
{ "name": "alice", "role": "reviewer", "template": "reviewer", "system_prompt": "..." }
```

### List Members — `GET /v1/teams/{id}/members`

### Run Team — `POST /v1/teams/{id}/runs`

```json
{
  "goal": "Review auth module for security issues",
  "decompose": true,
  "lock_files": true,
  "quality_gate": true,
  "max_parallel": 4
}
```

Returns the run result with `run_id`, `status`, `output`, and `tokens_used`.

---

## Run Endpoints

Agent runs are recorded for teams, swarms, and missions.

### Get Run — `GET /v1/runs/{id}`

### List Runs — `GET /v1/runs`

Query params: `workspaceId`, `userId`, `teamId`, `kind`, `status`, `limit`.

---

## Mission Endpoints

Missions are goal-driven, multi-step agent tasks tracked through their lifecycle.

### Create Mission — `POST /v1/missions`

```json
{ "goal": "Migrate to v2 API", "workspace_id": "ws_...", "project_id": "proj_..." }
```

### List Missions — `GET /v1/missions`

Query params: `ownerUserId`, `status`, `limit`.

### Get Mission — `GET /v1/missions/{id}`

### Run Mission — `POST /v1/missions/{id}/run`

Drives the mission forward one engine cycle.

### Get Events — `GET /v1/missions/{id}/events`

Returns the full event journal.

### Export Mission — `GET /v1/missions/{id}/export`

Query param: `format` (`markdown` default, or `json`).

---

## Engine Endpoints

Internal engine inspection and recovery.

### Get Trace — `GET /v1/engine/runs/{id}/trace`

Returns the full trace for a runtime run.

### List In-Flight — `GET /v1/engine/runs/in-flight`

Returns runtime run IDs that crashed mid-step.

### Recover — `POST /v1/engine/runs/recover`

Attempts to recover crashed runs. Returns `{ "recovered": ["run_id_1", ...] }`.

### Delete Run — `DELETE /v1/engine/runs/{id}`

Deletes all trace rows for a runtime run. Returns `{ "runtime_run_id": "...", "deleted_rows": N }`.

---

## Artifact Endpoints

Artifacts are files produced by agent runs, scoped by workspace and project.

### List Artifacts — `GET /v1/artifacts`

Query params: `workspace_id`, `project_id`, `run_id`.

### Download Artifact — `GET /v1/artifacts/{path}`

Returns the artifact file content with appropriate content type.

### Delete Artifacts — `DELETE /v1/artifacts/{runId}`

Query params: `workspace_id`, `project_id`. Deletes all artifacts for a run.

---

## Registry Endpoints

Read-only registries for tools, skills, and agent templates.

### List Tools — `GET /v1/tools`

Returns `{ "tools": [...], "count": N }`.

### Get Tool — `GET /v1/tools/{name}`

Returns the tool definition including parameters schema.

### List Skills — `GET /v1/skills`

Returns `{ "skills": [...], "count": N }`.

### Get Skill — `GET /v1/skills/{name}`

Returns the skill definition including body (markdown content).

### List Agent Templates — `GET /v1/agents/templates`

Returns `{ "templates": [...], "count": N }`.

### Get Agent Template — `GET /v1/agents/templates/{name}`

Returns the template including system prompt.

---

## Cost Tracking — `GET /v1/cost` (Phase 55)

Returns cost tracking data from the OpenRouter pricing model. Query param: `range` (`daily`, `weekly`, `monthly`, `all`; default `daily`).

```json
{
  "range": "daily",
  "total_cost_usd": 1.42,
  "sessions": [ ... ],
  "by_model": [ ... ]
}
```

If cost tracking is disabled (`SOVRANT_COST_PROVIDER=none`), returns `{ "enabled": false, "message": "..." }`.

---

## SignalR Hub — `/hubs/chat` (Phase 61)

`Sovrant.Server` exposes a SignalR hub at `/hubs/chat` for real-time streaming to the web frontend. This is the transport layer for `SOVRANT_RUNTIME_MODE=remote`.

### Authentication

Standard WebSocket upgrades cannot send `Authorization` headers. The `BearerTokenMiddleware` accepts `?access_token=<token>` on `/hubs/*` paths as an alternative to the header.

### Hub Methods

| Method | Direction | Description |
|---|---|---|
| `StreamTurn(sessionId, userMessage)` | Client → Server | Streams an agentic turn as `IAsyncEnumerable<RuntimeEventDto>` |
| `ConfirmTool(toolUseId)` | Client → Server | Approves a pending tool confirmation |
| `DenyTool(toolUseId)` | Client → Server | Denies a pending tool confirmation |
| `CancelTurn` | Client → Server | Cancels the current turn |

### RuntimeEventDto

All 13 `RuntimeEvent` subtypes are mapped to a flat JSON-friendly `RuntimeEventDto` with a string `Type` discriminator. The DTO lives in `Sovrant.Runtime.Conversation` so both `Sovrant.Server` (serialization) and `Sovrant.Web` (deserialization) can reference it without a cross-project dependency.

### Tool Confirmation Flow

When a tool requires user confirmation, the hub emits a `ToolConfirmationRequested` event. The client calls `ConfirmTool(toolUseId)` or `DenyTool(toolUseId)` to resolve the pending `TaskCompletionSource<bool>`. Pending confirmations are keyed by `{connectionId}:{toolUseId}` and cleaned up on disconnect.

### Dual-Mode Web Frontend

`Sovrant.Web` supports two runtime modes controlled by `SOVRANT_RUNTIME_MODE`:

- **`embedded`** (default) — The agentic loop runs in-process via `AddSovrantRuntime()`. The web app is self-contained with its own SQLite database.
- **`remote`** — The web app connects to an external `Sovrant.Server` via SignalR + REST using `AddSovrantClient()`. All Blazor components depend on interfaces (`IRuntimeSessionPool`, `ISessionStore`, `IToolRegistry`, `IArtifactStore`, `IToolConfirmationHandler`), so swapping modes is purely a DI registration concern.
