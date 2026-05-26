# Frontend Integration

This guide covers connecting any frontend — browser, Node.js, React, or server-side — to `Sovrant.Server` using the official **@sovrant/sdk** TypeScript SDK.

> **Use the SDK.** It handles authentication, streaming, retries, session management, and security hardening so you don't have to write (or maintain) that code yourself.

---

## How authentication works

`Sovrant.Server` authenticates clients with **per-user bearer tokens** (`svt_*` strings issued to a specific user). Every request must include:

```
Authorization: Bearer <your-svt_-token>
```

If the token is missing, expired, revoked, or wrong, the server returns `401 Unauthorized`.

Tokens are minted via the auth/registration flow or by calling `POST /v1/users/me/tokens` after logging in. The plaintext token is returned exactly once at issuance — store it as you would a database password.

**The SDK sends this header automatically** — you set the token once in the constructor and never touch it again.

---

## Quick Start

### Install

```bash
npm install @sovrant/sdk
```

### Basic usage (server-side or internal tools)

```ts
import { SovrantClient } from "@sovrant/sdk";

const client = new SovrantClient({
  baseUrl: "http://localhost:5200",
  token: process.env.SOVRANT_API_TOKEN!,  // svt_* token — never hardcode, always from environment
  model: "gpt-4o",
});

// Non-streaming
const { text, usage } = await client.chat("Summarize our Q2 roadmap");
console.log(text);

// Streaming
await client.stream("Draft a project plan", {
  onText: (chunk) => process.stdout.write(chunk),
  onToolUse: (event) => console.log(`Tool started: ${event.tool_name}`),
  onToolResult: (event) => console.log(`Tool done: ${event.tool_name}`),
  onComplete: ({ text, usage }) => console.log(`\n\nTokens: ${usage?.total_tokens}`),
  onError: (err) => console.error(err),
});
```

---

## Architecture: Browser vs Server

A user's `svt_*` token grants full access to that user's sessions, workspaces, and agent runs. Where you put the SDK depends on whether your app is public-facing or internal.

### Public-facing browser apps — proxy pattern (required)

Never ship a long-lived `svt_*` token to a browser. Any user can read browser network requests, local storage, or bundle source maps. Instead, put the SDK and the token on a Node.js backend and have the browser call your proxy:

```
Browser  ──►  Your Node.js proxy  ──►  Sovrant.Server :5200
              (holds svt_* token,        (validates token,
               uses SDK server-side)      runs the agent)
```

The browser sends unauthenticated requests to your proxy at `/v1`. The proxy uses the SDK with the real token and streams responses back to the browser. See the [Proxy Setup Reference](#proxy-setup-reference) section.

### Internal tools and admin dashboards

If the application is protected by its own authentication layer (SSO, VPN, corporate identity provider) and the users are trusted members of your organisation, it is acceptable to issue a per-user `svt_*` token to authenticated sessions and use the SDK directly in the browser.

In this pattern, your backend:
1. Authenticates the user (SSO / login)
2. Calls `POST /v1/users/me/tokens` (using its own admin credentials) to mint a scoped token for that user — or uses a short-lived proxy token your backend validates
3. The browser uses that credential with the SDK

This is appropriate for: internal developer tools, admin dashboards, Replit-style sandboxes, or any app where you control who can log in.

```ts
// Obtained from your auth endpoint — never from the bundle
const sessionToken = await yourAuthApi.getSovrantToken();

const client = new SovrantClient({
  baseUrl: "https://sovrant.internal",
  token: sessionToken,
  sessionId: `user:${currentUserId}`,
});
```

### Server-side rendering (Next.js, Nuxt, etc.)

Use the SDK in server components or API routes. The token never leaves the server:

```ts
// app/api/chat/route.ts (Next.js App Router — runs on the server)
import { SovrantClient } from "@sovrant/sdk";
import { NextRequest } from "next/server";

const client = new SovrantClient({
  baseUrl: process.env.SOVRANT_URL!,
  token: process.env.SOVRANT_API_TOKEN!,
});

export async function POST(req: NextRequest) {
  const { message } = await req.json();
  const { text } = await client.chat(message);
  return Response.json({ text });
}
```

---

## Multi-Tenant: Per-Team LLM Keys

For cloud platforms where each team or user brings their own LLM API key, the SDK supports sending team credentials with every request via `llmApiKey` and `llmBaseUrl`.

### How it works

```
Team's browser/server
  ──[Authorization: Bearer svt_team-...]──►  Sovrant.Server
  ──[X-LLM-Api-Key: sk-team-...]──────────►    ──[sk-team-...]──►  OpenAI / Gemini / etc.
```

- The `svt_*` token still authenticates the client to your Sovrant server (via `Authorization` header)
- `X-LLM-Api-Key` and `X-LLM-Base-Url` travel as **HTTP headers**, encrypted by HTTPS
- The server uses them for that LLM call only — they are never logged, stored, or included in error responses
- Each team's session history is isolated by a composite key (`session_id + provider`)

### Set at client construction (same key for all calls)

```ts
// One client per team — constructed with their credentials
const teamClient = new SovrantClient({
  baseUrl: "https://sovrant.yourcompany.com",
  token: team.sovrantApiToken,           // The team owner's svt_* token
  llmApiKey: team.llmApiKey,             // Team's own LLM key
  llmBaseUrl: team.llmBaseUrl,           // Team's provider (optional)
  sessionId: `team:${team.id}`,
});

const { text } = await teamClient.chat("Summarise our sprint");
```

### Override per individual call

```ts
// Single shared client, different credentials per request
await client.chat("hello", {
  llmApiKey: currentUser.llmApiKey,
  llmBaseUrl: currentUser.llmBaseUrl,
  sessionId: `user:${currentUser.id}`,
});
```

### Security properties

| Property | Detail |
|---|---|
| **In transit** | Sent as `X-LLM-Api-Key` HTTP header, HTTPS-encrypted in transit. Never in URLs or request bodies. |
| **On the server** | Used only for that LLM call. Never logged, never persisted, not in error bodies. |
| **In the SDK** | `toJSON()` / `JSON.stringify()` redacts `llmApiKey` as `"[REDACTED]"` |
| **Session isolation** | Server keys sessions by `{session_id}::{provider}` so team A never sees team B's history |

### What to watch for

- **HTTPS is required.** The LLM key travels as an HTTP header (`X-LLM-Api-Key`). HTTP exposes it in plaintext — always use `https://` in production (the SDK rejects non-HTTP(S) base URLs at construction).
- **Don't hardcode keys.** Even with redaction, LLM keys should come from your auth/secrets layer, not from JavaScript source files.
- **The key leaves the browser.** If you use this in a public browser app, the user's key goes to your Sovrant server over HTTPS. That is expected and appropriate for a bring-your-own-key model, but make it clear to users in your terms of service.

---

## SDK Reference

### Constructor Options

```ts
const client = new SovrantClient({
  baseUrl: "http://localhost:5200",  // Required — only http: and https: allowed
  token: "your-token",               // Required — per-user svt_* bearer token
  model: "gpt-4o",                   // Optional — default model for requests
  sessionId: "session-abc",          // Optional — default session for persistent conversations
  maxRetries: 3,                     // Optional — retry count on 429/5xx (default: 3)

  // Multi-tenant: supply each team's own LLM credentials.
  // Sent as X-LLM-Api-Key / X-LLM-Base-Url HTTP headers, HTTPS-encrypted in transit.
  // The server uses them for the LLM call and never logs or persists them.
  llmApiKey: "sk-team-a-key",        // Optional — overrides server's LLM_API_KEY
  llmBaseUrl: "https://...",         // Optional — overrides server's LLM_BASE_URL
});
```

### Chat — Non-streaming

```ts
const { text, usage } = await client.chat("What is our burn rate?");
// text: string — assistant's response
// usage?: { prompt_tokens, completion_tokens, total_tokens }
```

Override model, session, or LLM credentials per-call:

```ts
const { text } = await client.chat("Quick question", {
  model: "gpt-4o-mini",
  sessionId: "one-off",
  llmApiKey: "sk-this-call-only",   // per-call override
  llmBaseUrl: "https://...",
});
```

### Chat — Streaming

```ts
await client.stream("Analyze this dataset", {
  onText: (chunk) => { /* incremental text */ },
  onToolUse: (event) => { /* tool invocation started */ },
  onToolResult: (event) => { /* tool invocation completed */ },
  onComplete: ({ text, usage }) => { /* full response + token counts */ },
  onError: (err) => { /* error during streaming */ },
});
```

All callbacks are optional — subscribe only to what you need.

### Chat — Raw streaming (advanced)

For full control, iterate over raw SSE chunks:

```ts
for await (const chunk of client.streamRaw("Build the report")) {
  const content = chunk.choices?.[0]?.delta?.content;
  if (content) process.stdout.write(content);

  if (chunk.sovrant?.event === "tool_use") {
    console.log(`Running: ${chunk.sovrant.tool_name}`);
  }

  if (chunk.usage) {
    console.log(`Total tokens: ${chunk.usage.total_tokens}`);
  }
}
```

### Webhooks

Send messages through the webhook endpoint (used by Slack, Teams, Discord integrations):

```ts
const response = await client.webhook({
  source: "slack",
  user_id: "U12345",
  message: "What's the status of the deployment?",
  model: "gpt-4o-mini",         // Optional — override model
  thread_id: "thread-abc",      // Optional — for threaded conversations
  callback_url: "https://...",  // Optional — async mode (202 + callback)
});

console.log(response.text);
console.log(response.tool_calls);  // tools invoked during the turn
console.log(response.errors);      // any errors that occurred
```

### Configuration

```ts
// Read current config
const config = await client.getConfig();
// { model, llm_base_url, permission_mode, pinned_provider? }

// Update config (partial — only send fields you want to change)
const updated = await client.updateConfig({ model: "gpt-4o-mini" });
```

### Status and Models

```ts
// Server status — provider health, session pool, routing info
const status = await client.getStatus();
// { providers, active_model, permission_mode, pinned_provider, active_sessions, max_sessions, session_ttl_seconds }

// Available models
const models = await client.getModels();
```

### Session Management

```ts
// List all sessions
const sessionIds = await client.listSessions();

// Get session details (history, token totals)
const session = await client.getSession("session-abc");

// Delete a session (evicts from pool + removes from database)
await client.deleteSession("session-abc");

// Export session as markdown
const markdown = await client.exportSession("session-abc");

// Session-level config overrides
const config = await client.getSessionConfig("session-abc");
await client.updateSessionConfig("session-abc", { model: "gpt-4o-mini" });
```

### Users

```ts
// Current user
const me = await client.getMe();

// API tokens
const { token, plaintext } = await client.issueToken({ name: "my-app" });
const { tokens } = await client.listTokens();
await client.revokeToken(tokenId);

// Admin: user CRUD
const user = await client.createUser({ username: "alice" });
const { users } = await client.listUsers({ role: "admin" });
await client.updateUser(userId, { role: "admin" });
await client.deactivateUser(userId);
```

### Workspaces

```ts
const { workspaces } = await client.listWorkspaces();
const ws = await client.createWorkspace({ name: "My Team", slug: "my-team" });
const { members } = await client.listWorkspaceMembers(wsId);
await client.addWorkspaceMember(wsId, { user_id: userId, role: "editor" });

// Invites
const invite = await client.createWorkspaceInvite(wsId, { email: "bob@co.com" });
await client.acceptWorkspaceInvite(inviteToken);

// Config, usage, memory
const config = await client.getWorkspaceConfig(wsId);
const usage = await client.getWorkspaceUsage(wsId);
const { memory } = await client.listWorkspaceMemory(wsId, "project");
await client.saveWorkspaceMemory(wsId, { layer: "project", content: "..." });
```

### Projects

```ts
const { projects } = await client.listProjects(wsId);
const project = await client.createProject(wsId, { name: "API", slug: "api" });
await client.archiveProject(projectId);
await client.unarchiveProject(projectId);

// Members, config, sessions, usage, memory
const { members } = await client.listProjectMembers(projectId);
await client.addProjectMember(projectId, { user_id: userId });
```

### Teams and Runs

```ts
// Lifecycle
const team = await client.createTeam({ name: "reviewers" });
const { teams } = await client.listTeams({ workspace_id: wsId });
const detail = await client.getTeam(teamId);                    // returns { team, members }
await client.deleteTeam(teamId);

// Members
await client.addTeamMember(teamId, { name: "alice", role: "reviewer" });
const { members } = await client.listTeamMembers(teamId);

// Phase 78 Path 2 — per-team run profile (PATCH-style; omitted fields keep current value)
await client.updateTeamProfile(teamId, {
  run_mode: "parallel",
  max_concurrent: 4,
  file_locks_enabled: true,
  quality_gate_enabled: true,
  quality_gate_threshold: 8,
  decomposition_mode: "roleAware",
});

// Run
const result = await client.runTeam(teamId, { goal: "Review auth module" });

// Runs ledger
const run = await client.getRun(runId);
const { runs } = await client.listRuns({ status: "completed" });
```

> **Note:** Field names use `snake_case` on the wire to match the server. `run_mode` accepts `sequential` / `parallel` / `swarm`; `decomposition_mode` accepts `off` / `roleAware` / `open`. Invalid values return 400.

### Missions

```ts
const mission = await client.createMission({ goal: "Migrate to v2 API" });
await client.runMission(missionId);
const { events } = await client.getMissionEvents(missionId);
const exported = await client.exportMission(missionId, "markdown");
```

### Swarm

```ts
const result = await client.getSwarm(swarmId);
const events = await client.getSwarmEvents(swarmId);
const sessions = await client.listSwarmSessions({ workspace_id: wsId });
```

### Engine

```ts
const trace = await client.getEngineTrace(runtimeRunId);
const { runtime_run_ids } = await client.listInFlightRuns();
const { recovered } = await client.recoverEngineRuns();
```

### Evals

```ts
const suites = await client.listEvals();
const result = await client.runEval({ suite_name: "regression" });
const history = await client.getEvalHistory("regression");
```

### Artifacts

```ts
const { artifacts } = await client.listArtifacts({ workspace_id: wsId });
await client.deleteArtifacts(runId);
```

### Registries (Tools, Skills, Agent Templates)

```ts
const { tools } = await client.listTools();
const tool = await client.getTool("Read");

const { skills } = await client.listSkills();
const skill = await client.getSkill("code-review");

const { templates } = await client.listAgentTemplates();
const template = await client.getAgentTemplate("security-auditor");
```

### Command Center (Phase 89/90)

```ts
// Aggregated in-flight activity for the current user. Powers the /command page in Web and Desktop.
const state = await client.getCommandCenterState();
// { active_missions, active_team_runs, active_agent_runs, active_sessions, rows, generated_at }

// Optional: scope to a specific owner (admin tokens only).
const peerState = await client.getCommandCenterState({ owner_user_id: "user-123" });
```

The cockpit polls every 30 seconds. Each row carries a `detail_route` pointing at an existing detail surface. Private records are masked (`title` and `preview` are null) — their row is still returned so admins see activity counts are accurate.

### User Dashboard (Phase 98)

```ts
// Cross-workspace personal activity view — own records plus teammates' public records.
const dashboard = await client.getUserDashboardState();
// { active_missions, active_team_runs, active_agent_runs, active_sessions, rows, generated_at }
// Each row includes is_private: boolean and owner_username: string
```

Reached via the 👤 rail nav icon (`/dashboard` on Web, `UserDashboardView` on Desktop). Polls every 30 seconds. Other users' private records are **never returned** — the server excludes them entirely (no masked placeholder). Own private records are returned normally so users can manage their own privacy state.

### Usage Tracking

```ts
// Per-session token usage summary
const usage = await client.getUsage();

// Per-user usage (admin or self)
const userUsage = await client.getUserUsage(userId, { from: "2026-04-01" });
```

### Health Check

```ts
// Unauthenticated — safe to call from load balancers / readiness probes
const { status } = await client.health();
```

---

## React Integration

The SDK includes a `useChat` hook for React apps with built-in streaming, state management, and tool event callbacks.

> **Where to use this hook:** In internal tools, admin dashboards, or authenticated apps where you control who can access the token. For public-facing apps, run the SDK on your server and stream results to the browser via your own API — do not ship a long-lived `svt_*` token in a public bundle.

### Install

```bash
npm install @sovrant/sdk react
```

### Import

```ts
import { useChat } from "@sovrant/sdk/react";
```

### Usage — internal tool (token obtained from your auth layer)

```tsx
// Token comes from your authenticated session — never from a hardcoded string
function Chat({ sovrantToken }: { sovrantToken: string }) {
  const { messages, send, isStreaming, error, clear } = useChat({
    baseUrl: "https://sovrant.internal",
    token: sovrantToken,
    model: "gpt-4o",
    sessionId: `user:${currentUserId}`,
    onToolUse: (event) => console.log(`Tool: ${event.tool_name}`),
    onToolResult: (event) => console.log(`Done: ${event.tool_name}`),
    onError: (err) => console.error(err),
  });

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const input = e.currentTarget.elements.namedItem("msg") as HTMLInputElement;
    send(input.value);
    input.value = "";
  };

  return (
    <div>
      {messages.map((m) => (
        <div key={m.id}>
          <strong>{m.role}:</strong> {m.content}
          {m.toolEvents?.map((te, i) => (
            <span key={i}> [{te.tool_name}]</span>
          ))}
          {m.usage && <small> ({m.usage.total_tokens} tokens)</small>}
        </div>
      ))}
      {error && <div style={{ color: "red" }}>{error.message}</div>}
      <form onSubmit={handleSubmit}>
        <input name="msg" disabled={isStreaming} />
        <button disabled={isStreaming}>Send</button>
      </form>
      <button onClick={clear}>Clear</button>
    </div>
  );
}
```

### Hook return values

| Property | Type | Description |
|----------|------|-------------|
| `messages` | `ChatEntry[]` | All messages in the conversation |
| `isStreaming` | `boolean` | Whether a response is currently streaming |
| `send` | `(message: string) => Promise<void>` | Send a message and stream the response |
| `clear` | `() => void` | Clear all messages and errors |
| `error` | `Error \| null` | The last error that occurred |

### ChatEntry shape

```ts
interface ChatEntry {
  id: string;
  role: "user" | "assistant";
  content: string;
  toolEvents?: SovrantEvent[];   // tool_use / tool_result events
  usage?: UsageInfo;             // token counts (on final assistant message)
  createdAt: Date;
}
```

---

## SSE Parser (standalone)

If you need to parse SSE streams outside of `SovrantClient` (e.g., a custom transport layer), the SDK exports the parser directly:

```ts
import { parseSSEStream } from "@sovrant/sdk";

const response = await fetch("/v1/chat/completions", { /* ... */ });

for await (const chunk of parseSSEStream(response)) {
  console.log(chunk.choices?.[0]?.delta?.content);
}
```

The parser includes built-in security hardening:

- **Buffer size limit** — throws if the SSE buffer exceeds 1 MB (protects against malicious/misbehaving servers)
- **Prototype pollution protection** — strips `__proto__`, `constructor`, and `prototype` keys from parsed JSON
- **Graceful degradation** — silently skips malformed JSON chunks without crashing
- **Proper cleanup** — always releases the reader lock, even on errors

---

## Security Best Practices

The SDK enforces several security measures automatically. Here's what it does and what you should do on your side.

### What the SDK handles automatically

| Protection | Details |
|------------|---------|
| **URL protocol validation** | Only `http:` and `https:` base URLs accepted. `javascript:`, `file:`, `data:`, `ftp:` are rejected at construction time. |
| **Token validation** | Empty or non-string tokens are rejected at construction time. |
| **Token redaction** | `JSON.stringify(client)` outputs `"[REDACTED]"` for both `token` and `llmApiKey`. Safe to log the client object. |
| **Bearer token in headers only** | Token is sent via `Authorization: Bearer` header, never in URLs or request bodies. |
| **Path traversal prevention** | Session IDs are `encodeURIComponent()`-encoded in all URL paths. |
| **Query injection prevention** | Export format parameters are validated against an allow-list (`markdown`, `json`) and URI-encoded. |
| **Content-Type precision** | `Content-Type: application/json` is only sent when the request has a body, avoiding proxy confusion on GET/DELETE. |
| **Prototype pollution protection** | SSE JSON parsing strips `__proto__`, `constructor`, `prototype` keys. |
| **Buffer overflow protection** | SSE buffer is capped at 1 MB. Throws a descriptive error if exceeded. |
| **Automatic retries** | 429 (rate limited) and 5xx (server error) responses are retried with exponential backoff (1s, 2s, 4s). |
| **Typed errors** | `SovrantApiError` includes `status`, `body` (truncated to 256 chars in messages), `url` — but never the token. |

### What you must do

#### 1. Never expose an svt_* token to a public browser bundle

A user's `svt_*` token is a **secret** — treat it like a database password. It grants full access to that user's sessions, workspaces, and agent runs (admin tokens grant cross-user access).

For public-facing browser apps, use one of these approaches:

**Option A — Proxy (no token in browser):**
Your backend proxy holds the `svt_*` token and the browser never sees it. See the [Proxy Setup Reference](#proxy-setup-reference) below.

**Option B — Authenticated internal app:**
If all users are trusted (employees, internal tool), authenticate them first (SSO / login), then mint a per-user `svt_*` token via `POST /v1/users/me/tokens` and provide it only to authenticated sessions — not in the bundle, not in source code.

The SDK's `toJSON()` redaction protects against accidentally logging the token, but it **cannot** stop you from shipping it in a JavaScript bundle. If the token is in `new SovrantClient({ token: "svt_..." })` in your frontend code, users can find it.

#### 2. Store tokens in environment variables

Never hardcode tokens in source code or configuration files committed to git.

| Platform | Where to set |
|----------|-------------|
| **Node.js** | `.env` file (in `.gitignore`) + `dotenv` package |
| **Replit** | Secrets panel |
| **Docker** | `--env-file` or orchestrator secrets |
| **CI/CD** | Pipeline secrets (GitHub Actions, GitLab CI) |
| **Kubernetes** | `Secret` resource, mounted as env var |

#### 3. Use HTTPS in production

Always use `https://` base URLs in production. The SDK rejects non-HTTP(S) protocols at construction, but it cannot enforce TLS — that is on your deployment.

Terminate TLS at your load balancer or nginx reverse proxy. The connection between nginx and Sovrant on localhost can remain plain HTTP.

#### 4. Validate and sanitize user input before sending

The SDK sends messages as-is. If your app collects user input from forms or URLs, sanitize it before passing to `client.chat()` or `client.stream()`:

```ts
function sanitize(input: string): string {
  return input.trim().slice(0, 10_000); // length limit
}

await client.chat(sanitize(userInput));
```

#### 5. Handle errors without leaking internals

```ts
import { SovrantApiError } from "@sovrant/sdk";

try {
  await client.chat("Hello");
} catch (err) {
  if (err instanceof SovrantApiError) {
    // Do NOT forward err.body to the browser — it may contain internal details
    if (err.status === 401) respondWithError("Authentication failed");
    else if (err.status === 429) respondWithError("Rate limited — try again shortly");
    else respondWithError("Something went wrong");
  } else {
    respondWithError("Network error");
  }
}
```

#### 6. Scope sessions per user

Use separate session IDs per user and per conversation to prevent cross-contamination of history:

```ts
const client = new SovrantClient({
  baseUrl: "https://sovrant.internal",
  token: process.env.SOVRANT_API_TOKEN!,
  sessionId: `user:${userId}:conv:${conversationId}`,
});
```

#### 7. Monitor token usage

Track consumption to detect anomalies or runaway costs:

```ts
const usage = await client.getUsage();
// Returns per-session token totals — alert on unexpected spikes
```

---

## Proxy Setup Reference

For public-facing apps, the proxy holds the `svt_*` token server-side and the browser never touches it.

### Express (Node.js)

```js
import express from "express";
import { createProxyMiddleware } from "http-proxy-middleware";

const app = express();

// Your own auth middleware goes here — validate the user's session
// before forwarding to Sovrant
app.use("/v1", requireAuth, createProxyMiddleware({
  target: process.env.SOVRANT_URL ?? "http://127.0.0.1:5200",
  changeOrigin: true,
  on: {
    proxyReq(proxyReq) {
      // Inject the secret token — the browser never sees this
      proxyReq.setHeader("Authorization", `Bearer ${process.env.SOVRANT_API_TOKEN}`);
      // Remove any Authorization header the browser sent — don't forward client creds
      proxyReq.removeHeader("x-forwarded-authorization");
    },
  },
}));

app.listen(3000);
```

### nginx

Store the token as a variable (not hardcoded in the config file). One approach: load it from a separate secrets file:

```nginx
# /etc/nginx/sovrant_secrets  — not in version control, chmod 600
# set $sovrant_token "svt_...";

server {
    location /v1/ {
        include /etc/nginx/sovrant_secrets;

        proxy_pass http://127.0.0.1:5200;
        proxy_set_header Authorization "Bearer $sovrant_token";
        proxy_set_header Host $host;

        # Required for SSE streaming
        proxy_buffering off;
        proxy_cache off;
        proxy_read_timeout 300s;
        proxy_set_header Connection "";
        chunked_transfer_encoding on;
    }
}
```

Alternatively, pass the token via an environment variable using nginx's `env` directive and the `ngx_http_perl_module`, or use a secrets manager sidecar to write the include file at runtime.

### Launching Sovrant.Server from Node.js

If your Node process manages the server lifecycle:

```js
import { spawn } from "child_process";

function startSovrant() {
  const proc = spawn("dotnet", ["run", "--project", "src/Sovrant.Server", "--no-build"], {
    env: {
      ...process.env,           // Inherit LLM_API_KEY, SOVRANT_API_TOKEN, etc. from the environment
    },
    stdio: "inherit",
  });
  proc.on("exit", (code) => console.error(`sovrant-server exited with ${code}`));
  return proc;
}

const server = startSovrant();
process.on("exit", () => server.kill());
```

Wait for the server to be ready before routing traffic:

```js
async function waitForServer(url, timeout = 15_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    try {
      const r = await fetch(`${url}/health`);
      if (r.ok) return;
    } catch { /* not ready yet */ }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error("Sovrant server did not start in time");
}

await waitForServer("http://127.0.0.1:5200");
```

---

## Agentic Events (Phase 59)

Phase 59 added three new `RuntimeEvent` types that flow through the SSE `sovrant` extension field. Frontends should handle these for a complete agentic UX:

### Clarification Needed

When the user's intent is ambiguous (e.g. single-word input like "test"), the engine emits a clarification request instead of guessing. The SSE chunk carries `sovrant.clarification`:

```ts
await client.stream("test", {
  onSovrantEvent: (event) => {
    if (event.clarification) {
      // Show to user: "Did you mean to run tests, create a test file, or just chatting?"
      showClarificationPrompt(event.clarification);
    }
  },
});
```

### Plan Presented

Before executing a multi-step plan, the engine presents the plan for optional approval. The SSE chunk carries `sovrant.plan_id`, `sovrant.formatted_plan`, and `sovrant.requires_approval`:

```ts
if (event.plan_id && event.formatted_plan) {
  // Show numbered step list with destructive warnings
  showPlanUI(event.formatted_plan);
  if (event.requires_approval) {
    // Block execution until user approves/rejects
    const approved = await promptUserApproval(event.plan_id);
    // Send approval back via session config or follow-up message
  }
}
```

### Step Progress

During plan execution, the engine emits progress updates per step. The SSE chunk carries `sovrant.step_current`, `sovrant.step_total`, `sovrant.step_intent`, and `sovrant.step_status`:

```ts
if (event.step_current != null) {
  updateProgressBar(event.step_current, event.step_total);
  showStepLabel(`Step ${event.step_current}/${event.step_total}: ${event.step_intent} [${event.step_status}]`);
}
```

---

## Tool Events

When the engine calls a tool (e.g. `Bash`, `Read`, `WebSearch`) during a streaming response, the SSE chunk includes a `sovrant` extension field. The SDK surfaces these via callbacks:

```ts
await client.stream("Find and fix the bug in auth.ts", {
  onText: (chunk) => appendToUI(chunk),
  onToolUse: (event) => {
    // event: { event: "tool_use", tool_name: "Read", tool_use_id: "tu_abc", is_error?: false }
    showToolIndicator(event.tool_name);
  },
  onToolResult: (event) => {
    // event: { event: "tool_result", tool_name: "Read", tool_use_id: "tu_abc", is_error?: false }
    hideToolIndicator();
  },
});
```

Using the React hook:

```tsx
// Token obtained from your authenticated session, not hardcoded
const { messages } = useChat({
  baseUrl: "https://sovrant.internal",
  token: sessionToken,
  onToolUse: (event) => console.log(`Running: ${event.tool_name}`),
  onToolResult: (event) => console.log(`Done: ${event.tool_name}`),
});

// Each assistant message includes a toolEvents array
messages
  .filter((m) => m.role === "assistant" && m.toolEvents?.length)
  .forEach((m) => console.log(`${m.toolEvents!.length} tools used`));
```

---

## TypeScript Types

The SDK exports all types for full type safety. 75+ interfaces covering every server endpoint:

```ts
import type {
  // Core chat
  SovrantClientOptions, ChatMessage, ChatCompletionRequest,
  ChatCompletionResponse, ChatCompletionChunk, ChunkChoice,
  ResponseChoice, SovrantEvent, StreamCallbacks, ChatCallOptions,
  UsageInfo, ProviderStatus, StatusResponse, ServerConfig,
  ModelsResponse, ModelInfo,
  // Webhooks
  WebhookRequest, WebhookResponse, WebhookToolCall,
  // Sessions
  SessionDetail, SessionMessage, SessionListResponse,
  SessionConfig, SessionConfigUpdate, UsageSummary,
  // Users
  UserProfile, CreateUserRequest, UpdateUserRequest, UserListFilter,
  ApiToken, IssueTokenRequest, IssueTokenResponse,
  // Workspaces
  Workspace, CreateWorkspaceRequest, UpdateWorkspaceRequest,
  WorkspaceMember, AddWorkspaceMemberRequest,
  WorkspaceInvite, CreateInviteRequest,
  WorkspaceMemoryEntry, SaveMemoryRequest,
  // Projects
  Project, CreateProjectRequest, UpdateProjectRequest,
  ProjectMember, AddProjectMemberRequest,
  // Teams
  Team, CreateTeamRequest, TeamMember, AddTeamMemberRequest,
  TeamRunRequest, TeamRunResponse, AgentRun, AgentRunFilter,
  // Missions
  Mission, CreateMissionRequest, MissionEvent,
  // Swarm
  SwarmRunRequest, SwarmResult,
  // Engine
  RuntimeTraceEntry,
  // Evals
  EvalSuite, EvalRunRequest, EvalRunResponse, EvalResultDetail,
  // Artifacts
  ArtifactEntry, ArtifactScope,
  // Registries
  ToolDefinition, SkillSummary, SkillDetail,
  AgentTemplateSummary, AgentTemplateDetail,
} from "@sovrant/sdk";
```

---

## Remote Mode — Dual-Mode Web Frontend (Phase 61)

`Sovrant.Web` is the official Blazor Server frontend. The default landing page after first-run setup is the Command Center cockpit (`/command`) — the first-run wizard at `/setup` redirects there on completion. The User Dashboard (`/dashboard`) is a separate personal view reached via the 👤 rail nav icon. `Sovrant.Desktop` (Avalonia) follows the same pattern with `CommandCenterView` as the default startup view and `UserDashboardView` accessible from the rail nav.

`Sovrant.Web` supports two runtime modes, controlled by the `SOVRANT_RUNTIME_MODE` environment variable:

### Embedded Mode (default)

```
SOVRANT_RUNTIME_MODE=embedded
```

The agentic loop runs in-process via `AddSovrantRuntime()`. The web app is fully self-contained — it owns its own SQLite database, session pool, tool registry, and artifact store. No external server is needed.

### Remote Mode

```
SOVRANT_RUNTIME_MODE=remote
SOVRANT_SERVER_URL=http://localhost:5200
SOVRANT_API_TOKEN=your-token
```

The web app connects to an external `Sovrant.Server` instance. All runtime operations are delegated over SignalR (streaming turns) and REST (sessions, tools, artifacts).

### How it works

All Blazor components depend on interfaces (`IRuntimeSessionPool`, `ISessionStore`, `IToolRegistry`, `IArtifactStore`, `IToolConfirmationHandler`), so switching between modes is purely a DI registration concern — no component code changes.

| Interface | Embedded impl | Remote impl |
|---|---|---|
| `IRuntimeSessionPool` | `RuntimeSessionPool` (in-process) | `RemoteRuntimeSessionPool` (SignalR) |
| `ISessionStore` | `SqliteSessionStore` | `RemoteSessionStore` (REST) |
| `IToolRegistry` | `ToolRegistry` (local) | `RemoteToolRegistry` (REST) |
| `IArtifactStore` | `FileArtifactStore` | `RemoteArtifactStore` (REST) |
| `IToolConfirmationHandler` | `ToolConfirmationHandler` | `RemoteToolConfirmationHandler` (SignalR) |

### SignalR Streaming

In remote mode, `SignalRStreamingClient` manages the hub connection to `/hubs/chat`. Features:

- **Automatic reconnection** with exponential backoff (configurable base interval and max attempts)
- **Connection state tracking** via `RemoteConnectionState` (Connected/Reconnecting/Disconnected)
- **Tool confirmation round-trips** — `ConfirmToolAsync()` and `DenyToolAsync()` resolve server-side `TaskCompletionSource<bool>` instances
- **Authentication** — bearer token sent via `?access_token=` query string (standard SignalR WebSocket pattern)

### When to use each mode

| Scenario | Recommended mode |
|---|---|
| Single-user local development | Embedded |
| Desktop app (Avalonia) | Embedded |
| Team sharing one server | Remote — point multiple web frontends at a single `Sovrant.Server` |
| Cloud deployment | Remote — web frontend on one node, server on another |
| CI/CD or automated workflows | Remote — headless server, API access only |

---

## Migration from Raw Fetch

If you previously wrote raw `fetch` + SSE parsing code, here's how to migrate:

| Before (raw fetch) | After (SDK) |
|---|---|
| `fetch("/v1/chat/completions", { method: "POST", headers: { Authorization: "Bearer ..." }, body: ... })` | `client.chat("message")` |
| Custom SSE parser with `ReadableStream` + `TextDecoder` | `client.stream("message", { onText })` or `parseSSEStream(response)` |
| `JSON.parse(data)` on SSE lines | SDK's `safeJsonParse` (prototype pollution safe) |
| Manual `encodeURIComponent` on session IDs | Handled automatically |
| Custom retry logic for 429/5xx | Built-in: 3 retries, exponential backoff |
| Manual `Authorization` header on every request | Set once in constructor |
| Roll-your-own React state for streaming | `useChat()` hook |
