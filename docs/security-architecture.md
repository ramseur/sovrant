# Security Architecture

This document describes Sovrant's security design decisions, controls, and known trade-offs. For vulnerability reporting see [`SECURITY.md`](../SECURITY.md) in the root.

---

## API Key & LLM Credential Handling

Sovrant operates in two modes with different trust boundaries.

**Remote mode** (web client or desktop connecting to a Sovrant server): The client authenticates using a session token (`svt_*`). The server holds LLM provider credentials in its own encrypted keystore and sends them directly to the provider. The client never sees or transmits the LLM API key.

```
Client  →  svt_* token  →  Sovrant Server  →  LLM API key (TLS)  →  Provider
```

**Embedded mode** (desktop running its own local runtime): The LLM API key is stored in the local encrypted keystore. On each turn it is read from the store and sent to the provider over TLS. No intermediate server is involved.

```
Desktop  →  LLM API key (TLS, direct)  →  Provider
```

The key is fetched per-request inside `OpenAiCompatProvider.BuildRequestAsync` rather than held in a long-lived HTTP client header. Key rotations in Settings take effect immediately without a restart.

---

## Authentication & Session Tokens

### Token Format

API tokens use a `svt_` prefix followed by 32 cryptographically random bytes (base64url-encoded), totalling ~47 characters. Server-generated token IDs are in the format `tok_{16 hex chars}` from a CSPRNG.

### Storage

Tokens are SHA-256 hashed before storage. The plaintext secret is returned only once at issuance and is never retrievable again. Revocation is logical (soft delete with `revoked_at` timestamp) to preserve audit trail.

### Validation

On every request, `BearerTokenMiddleware` extracts the Bearer token from the `Authorization` header (or from the `access_token` query parameter for SignalR WebSocket transport), validates the `svt_` prefix, and performs a single indexed lookup on `token_hash` joining to the `users` table. Expired, revoked, and inactive-user tokens are all rejected in the same query.

### Sliding-Window Refresh

Tokens expiring within 29 days are auto-refreshed to 30 days on use (at most once per day). Last-used timestamp is tracked for audit purposes.

### Password Hashing

Passwords are hashed with Argon2id using OWASP-minimum parameters: 65536 KiB memory, 3 iterations, parallelism=1, 32-byte random salt per password. Verification uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.

### Password Reset Tokens

Reset tokens use a `prt_` prefix with 32 random bytes (hex-encoded), SHA-256 hashed in storage. They expire after 24 hours, are one-time use (marked on redemption), and any existing unused token for a user is expired when a new one is generated.

### Registration & Approval

The first user to register becomes admin and registration closes automatically. Subsequent registrations can be held in a `pending` state requiring admin approval. Email uniqueness is enforced at the database level. Minimum password length is 8 characters.

---

## Credential Store

LLM API keys, OAuth tokens, and other secrets are stored using AES-256-GCM encryption with a 96-bit random nonce per credential (never reused). Each stored entry is structured as `nonce(12) || tag(16) || ciphertext`. Authentication tag mismatches (indicating tampering or corruption) are treated as missing credentials.

The master key is 256 bits, generated once at first run and stored at `~/.sovrant/credentials/.keystore`. On POSIX systems the file is created with `0600` permissions. On Windows, profile directory ACLs apply. Concurrent key initialization is protected by a `SemaphoreSlim(1, 1)` double-check pattern.

Credential lookup keys are SHA-256 hashed to produce safe filenames — no plaintext key material appears in filenames or logs.

Two storage backends exist: file-based (individual `.enc` files per credential) and SQLite-backed (encrypted blobs in a `credentials` table). The embedded desktop uses file-based; the server uses SQLite.

---

## Web & API Request Security

### Bearer Token Middleware

All routes except `/health`, OPTIONS, and auth endpoints require a valid `svt_*` token. On success, `userId`, `tokenId`, and `role` are stored in `HttpContext.Items` for downstream handlers. Failures return 401 with a JSON error body.

### Rate Limiting

A per-session rate limiter caps requests at 60 per minute (configurable via `SOVRANT_RATE_LIMIT_RPM`), keyed on the `X-Session-Id` header, falling back to client IP then connection ID. Returns 429 on violation.

Note: Auth endpoints (login, register, reset) are currently not separately rate limited. Per-IP brute-force protection on those endpoints is a planned improvement.

### CORS

Origins are configured via `SOVRANT_CORS_ORIGINS` (comma-separated). Default: localhost on ports 5173, 5100, 3000, 8080, and 127.0.0.1. The policy explicitly allows `Authorization`, `X-Session-Id`, `X-Workspace-Id`, and `X-Project-Id` headers with credentials. The CORS allowlist should be reviewed before production deployment.

### Request Limits

- Max request body: 10 MB
- Max request line: 16 KB

### Anti-Forgery

Blazor Web uses `app.UseAntiforgery()` to protect form submissions in Razor components.

### Input Validation

Session IDs, model names, resource IDs, and slugs are validated with regex (1–128 chars: alphanumeric + `-_:.`) before domain logic. Invalid inputs are rejected with 400.

### Log Sanitization

`RequestLoggingMiddleware` masks dynamic path segments in logs (e.g., `/v1/sessions/{id}` → `/v1/sessions/***`) to prevent session or token IDs appearing in log files.

### TLS

When `SOVRANT_TLS_CERT` is set, Kestrel is configured for HTTPS. Certificates can be loaded from PEM (`SOVRANT_TLS_CERT` + `SOVRANT_TLS_KEY`) or PFX (`SOVRANT_TLS_CERT` + `SOVRANT_TLS_CERT_PASSWORD`). HTTPS redirect middleware is applied when TLS is active.

---

## Workspace Access Control & RBAC

### Role Hierarchy

Workspace roles are: Owner > Admin > Member. Global server admins bypass workspace membership checks. Guards enforce the minimum required role at the handler level and return 403 on violation.

| Operation | Required role |
|---|---|
| Read workspace | Member+ |
| Manage workspace | Admin+ |
| Delete / transfer | Owner |

### Workspace Types

**Personal** workspaces are auto-created per user, cannot be deleted, and the owner is the user. **Team** workspaces are created explicitly, support full RBAC, and can be deleted by the owner.

### Invite Tokens

Workspace invites use 32 cryptographically random bytes (hex-encoded), expire after 7 days, and are one-time use. Note: invite tokens are currently stored as plaintext in the database (unlike API and reset tokens which are hashed). Hashing invite tokens is a planned improvement.

### Workspace-Scoped Settings

Per-workspace configuration (session TTL, max sessions, etc.) is stored in a `workspace_settings` table. Resolution order: environment variable → workspace DB value → global default.

---

## Session Management

A background `SessionEvictionService` sweeps every 5 minutes and evicts sessions using a hybrid LRU + TTL strategy:

- **TTL**: default 3600 seconds (configurable via `SOVRANT_SESSION_TTL_SECONDS`)
- **Max capacity**: default 500 sessions (configurable via `SOVRANT_MAX_SESSIONS`)

Settings are sourced from the workspace settings table; environment variables override.

---

## Tool Execution & Permissions

### Tool Tiers

Tools are classified into four tiers:

| Tier | Examples | Default behaviour |
|---|---|---|
| Safe | Read, Glob, Grep, WebFetch, WebSearch | Always allowed |
| Moderate | WriteFile, EditFile, TaskCreate, MCP* | Allowed in AcceptEdits+ |
| Dangerous | Bash, PowerShell, REPL | Requires confirmation |
| Escalation | Agent, Swarm, TeamCreate, Mission | Requires confirmation |

Unknown tools default to Moderate.

### Permission Modes

| Mode | Safe | Moderate | Dangerous | Escalation |
|---|---|---|---|---|
| Default | Allow | Confirm | Confirm | Confirm |
| AcceptEdits | Allow | Allow | Confirm | Confirm |
| DontAsk | Allow | Allow | Confirm | Confirm |
| BypassPermissions | Allow | Allow | Allow | Allow |
| Plan | Allow | Deny | Deny | Deny |

`SOVRANT_UNSAFE_DONTASK=true` removes the Dangerous tier confirmation in DontAsk mode. This is intended for CI pipelines only and should not be set in interactive deployments.

Token-level scopes are stored in the database but are not yet enforced at the tool level (planned).

### Dangerous Command Detection

`DangerousCommandDetector` checks shell commands against 20+ built-in patterns (e.g., `rm -rf /`, `DROP TABLE`, `git push --force`, fork bombs). Matching is case-insensitive substring. User-configured additional patterns are supported. This is an advisory layer — it does not replace the tier-based permission system.

---

## Governance & Secret Detection

`SecretDetector` scans content for common secret patterns:

- AWS access keys (`AKIA[0-9A-Z]{16}`)
- API keys (`sk-*`, `api_key=*`)
- PEM private keys
- JWTs (three base64url segments)
- Hardcoded passwords (`password=*`, `passwd=*`)
- Bearer tokens (generic patterns)
- Custom patterns from `GovernanceConfig.SecretPatterns`

Each regex runs with a 1-second timeout to prevent ReDoS. A timeout causes that pattern to be skipped silently — keep custom patterns simple.

An in-memory `EthicalAuditLog` (capped at 10,000 entries, thread-safe) records governance decisions (category, reason, severity, timestamp) for compliance review.

---

## Artifact Path Security

### Path Traversal Defense

`LocalArtifactStore.ResolveAndGuard()` applies two-layer protection:

1. **Pre-check**: Rejects any path containing `..` before combining with the root.
2. **Post-check**: Resolves the full path with `Path.GetFullPath()` and asserts it begins with the artifact root (case-insensitive).

Violations throw `ArgumentException` — there is no silent failure.

### Segment Validation

`ValidateSegment()` is applied to workspace IDs, project IDs, and run IDs before any disk operation. It rejects:

- Windows-invalid characters: `< > : " / \ | ? * @`
- Control characters (0–31)
- Names ending with space or period
- Path traversal sequences (`..`)

### Directory Layout

```
~/.sovrant/workspaces/
└── {workspace_id}/
    ├── artifacts/{run_id}/          ← workspace-level (no project selected)
    └── projects/{project_id}/
        └── artifacts/{run_id}/      ← project-level
```

Directory segments use `{id}__{safe-name}` format where the safe name is lowercased to letters, digits, and hyphens only. Access URL path segments are `Uri.EscapeDataString()`-encoded.

---

## MCP Security

### OAuth 2.0 + PKCE

`McpOAuthService` implements a full Authorization Code + PKCE flow (RFC 7636). A 16-byte random state parameter prevents CSRF. Pending OAuth states expire after **10 minutes** and are held in-memory only — a server restart clears pending states. Persistent OAuth state storage is a planned improvement.

### MCP Server Token

When `SOVRANT_MCP_TOKEN` is set, callers must supply a matching `--token` flag. Errors are written to stderr to preserve the JSON-RPC stdout transport. The token is sourced from an environment variable, making it visible in process listings. Moving it to the credential store is a planned improvement.

### OAuth Token Storage

OAuth access tokens, refresh tokens, and client secrets are stored in `ICredentialStore` (AES-256-GCM encrypted) under keys `mcp.{name}.access_token`, `mcp.{name}.refresh_token`, and `mcp.{name}.client_secret`.

### MCP Server Config Storage

Every MCP server configuration — including HTTP bearer tokens, API keys in headers, environment variables such as `GITHUB_TOKEN`, and connection strings in args — is stored as an AES-256-GCM encrypted blob in `ICredentialStore`.

`CredentialStoreMcpServerStore` serialises the full `McpServerConfig` to JSON and encrypts it under key `mcp.{name}.config`. A server name index is maintained at `mcp.__index__` (also encrypted) to support enumeration without knowing server names in advance. No MCP credential material touches the `mcp_servers` SQLite table after the one-shot migration.

**Migration**: `McpServerStoreMigrator` runs once on startup (idempotent via sentinel key `mcp.__v1_migrated__`). It reads any existing rows from the legacy `mcp_servers` SQLite table and imports them into the credential store. Existing installs are migrated automatically without user action.

### Integration Gallery

The Integrations Gallery (web and desktop) presents a catalog of pre-defined MCP servers (Composio, n8n, GitHub, Brave Search, PostgreSQL, etc.). When a user connects an integration from the gallery, any API key or connection string they enter is passed directly into `McpServerConfig.Headers` or `McpServerConfig.Env` and persisted via `IMcpServerStore` — subject to the plaintext SQLite gap described above. LLM provider entries (OpenAI, Anthropic, OpenRouter, Ollama) do not go through `IMcpServerStore`; they are configured separately in Settings.

---

## Logging & Audit Trail

No plaintext secrets are logged. Tokens are hashed at the service boundary before any logging occurs. Passwords are never stored or logged in plaintext.

Authentication events logged (with `userId`, `tokenId`, `role` as appropriate):

- Token issued / revoked
- User registered / logged in
- Login failed (with reason: user not found, wrong password, etc.)
- Password reset token generated
- User approved by admin

Request paths are sanitized before logging — dynamic segments are replaced with `***`. The rolling file logger writes asynchronously. Log format (plain or JSON) is configurable via `SOVRANT_LOG_FORMAT`.

---

## Known Trade-offs & Planned Improvements

| Item | Current state | Plan |
|---|---|---|
| Workspace invite tokens | Stored as plaintext | Hash like API tokens |
| Token scopes | Stored, not enforced | Enforce in a future phase |
| OAuth pending states | In-memory only | Persist to database |
| MCP server token | Env var | Move to credential store |
| MCP server API keys / bearer tokens / env vars | ~~Plaintext in SQLite~~ → **Fixed**: encrypted via `CredentialStoreMcpServerStore` | Complete — existing rows auto-migrated on first boot |
| Auth endpoint rate limiting | Not separately rate limited | Per-IP limit on login/register/reset |
| SignalR token transport | Query parameter (`access_token`) | Move to subprotocol or custom header |
