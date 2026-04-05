# Frontend Integration

This guide covers connecting any frontend — browser, Node.js, React, or server-side — to `Sovrant.Server` using the official **@sovrant/sdk** TypeScript SDK.

> **Use the SDK.** It handles authentication, streaming, retries, session management, and security hardening so you don't have to write (or maintain) that code yourself.

---

## How authentication works

`Sovrant.Server` uses a single bearer token (`SOVRANT_TOKEN` env var). Every request must include:

```
Authorization: Bearer <your-token>
```

If the token is missing or wrong, the server returns `401 Unauthorized`.

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
  token: process.env.SOVRANT_TOKEN!,  // Never hardcode — always from environment
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

The `SOVRANT_TOKEN` is a **server secret**. Where you put the SDK depends on whether your app is public-facing or internal.

### Public-facing browser apps — proxy pattern (required)

Never send `SOVRANT_TOKEN` to a browser. Any user can read browser network requests, local storage, or bundle source maps. Instead, put the SDK and the token on a Node.js backend and have the browser call your proxy:

```
Browser  ──►  Your Node.js proxy  ──►  Sovrant.Server :5200
              (holds SOVRANT_TOKEN,       (validates token,
               uses SDK server-side)       runs the agent)
```

The browser sends unauthenticated requests to your proxy at `/v1`. The proxy uses the SDK with the real token and streams responses back to the browser. See the [Proxy Setup Reference](#proxy-setup-reference) section.

### Internal tools and admin dashboards

If the application is protected by its own authentication layer (SSO, VPN, corporate identity provider) and the users are trusted members of your organisation, it is acceptable to issue a scoped token to authenticated sessions and use the SDK directly in the browser.

In this pattern, your backend:
1. Authenticates the user (SSO / login)
2. Issues a **session-scoped credential** for that user's browser session — this can be the `SOVRANT_TOKEN` itself, or a short-lived proxy token that your backend validates
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
  token: process.env.SOVRANT_TOKEN!,
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
  ──[SOVRANT_TOKEN + x_api_key in body]──►  Sovrant.Server
                                              ──[x_api_key]──►  OpenAI / Gemini / etc.
```

- `SOVRANT_TOKEN` still authenticates the client to your Sovrant server
- `x_api_key` and `x_base_url` travel in the **JSON request body**, encrypted by HTTPS
- The server uses them for that LLM call only — they are never logged, stored, or included in error responses
- Each team's session history is isolated by a composite key (`session_id + provider`)

### Set at client construction (same key for all calls)

```ts
// One client per team — constructed with their credentials
const teamClient = new SovrantClient({
  baseUrl: "https://sovrant.yourcompany.com",
  token: process.env.SOVRANT_TOKEN!,     // Your server's bearer token
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
| **In transit** | Sent in the HTTPS-encrypted request body, not in URLs or headers |
| **On the server** | Used only for that LLM call. Never logged, never persisted, not in error bodies. |
| **In the SDK** | `toJSON()` / `JSON.stringify()` redacts `llmApiKey` as `"[REDACTED]"` |
| **Session isolation** | Server keys sessions by `{session_id}::{provider}` so team A never sees team B's history |

### What to watch for

- **HTTPS is required.** The LLM key travels in the request body. HTTP exposes it in plaintext — always use `https://` in production (the SDK rejects non-HTTP(S) base URLs at construction).
- **Don't hardcode keys.** Even with redaction, LLM keys should come from your auth/secrets layer, not from JavaScript source files.
- **The key leaves the browser.** If you use this in a public browser app, the user's key goes to your Sovrant server over HTTPS. That is expected and appropriate for a bring-your-own-key model, but make it clear to users in your terms of service.

---

## SDK Reference

### Constructor Options

```ts
const client = new SovrantClient({
  baseUrl: "http://localhost:5200",  // Required — only http: and https: allowed
  token: "your-token",               // Required — SOVRANT_TOKEN bearer token
  model: "gpt-4o",                   // Optional — default model for requests
  sessionId: "session-abc",          // Optional — default session for persistent conversations
  maxRetries: 3,                     // Optional — retry count on 429/5xx (default: 3)

  // Multi-tenant: supply each team's own LLM credentials.
  // Sent in the request body (never a URL or header), HTTPS-encrypted in transit.
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
// Provider health and routing scores
const providers = await client.getStatus();
// [{ name, healthy, latency_ms, request_count, error_count, score }]

// Available models
const models = await client.getModels();
```

### Session Management

```ts
// List all sessions
const sessionIds = await client.listSessions();

// Get session details (history, token totals)
const session = await client.getSession("session-abc");

// Delete a session (evicts from pool + deletes JSONL)
await client.deleteSession("session-abc");

// Export session as markdown
const markdown = await client.exportSession("session-abc");
```

### Usage Tracking

```ts
// Per-session token usage summary
const usage = await client.getUsage();
```

### Health Check

```ts
// Unauthenticated — safe to call from load balancers / readiness probes
const { status } = await client.health();
```

---

## React Integration

The SDK includes a `useChat` hook for React apps with built-in streaming, state management, and tool event callbacks.

> **Where to use this hook:** In internal tools, admin dashboards, or authenticated apps where you control who can access the token. For public-facing apps, run the SDK on your server and stream results to the browser via your own API — do not use this hook with the real `SOVRANT_TOKEN` in a public bundle.

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

- **Buffer size limit** — throws if the SSE buffer exceeds 10 MB (protects against malicious/misbehaving servers)
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
| **Query injection prevention** | Export format parameter is validated against an allow-list and URI-encoded. |
| **Prototype pollution protection** | SSE JSON parsing strips `__proto__`, `constructor`, `prototype` keys. |
| **Buffer overflow protection** | SSE buffer is capped at 10 MB. Throws a descriptive error if exceeded. |
| **Automatic retries** | 429 (rate limited) and 5xx (server error) responses are retried with exponential backoff (1s, 2s, 4s). |
| **Typed errors** | `SovrantApiError` includes `status`, `body`, `url` — but never the token. |

### What you must do

#### 1. Never expose SOVRANT_TOKEN to a public browser bundle

`SOVRANT_TOKEN` is a **server secret** — treat it like a database password. It grants full access to the agent, all sessions, and all config endpoints.

For public-facing browser apps, use one of these approaches:

**Option A — Proxy (no token in browser):**
Your backend proxy holds `SOVRANT_TOKEN` and the browser never sees it. See the [Proxy Setup Reference](#proxy-setup-reference) below.

**Option B — Authenticated internal app:**
If all users are trusted (employees, internal tool), authenticate them first (SSO / login), then provide the token only to authenticated sessions — not in the bundle, not in source code.

The SDK's `toJSON()` redaction protects against accidentally logging the token, but it **cannot** stop you from shipping it in a JavaScript bundle. If the token is in `new SovrantClient({ token: "sk-..." })` in your frontend code, users can find it.

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
  token: process.env.SOVRANT_TOKEN!,
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

For public-facing apps, the proxy holds `SOVRANT_TOKEN` server-side and the browser never touches it.

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
      // Inject the server secret — the browser never sees this
      proxyReq.setHeader("Authorization", `Bearer ${process.env.SOVRANT_TOKEN}`);
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
# set $sovrant_token "your-secret-token-here";

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
      ...process.env,           // Inherit LLM_API_KEY, SOVRANT_TOKEN, etc. from the environment
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

The SDK exports all types for full type safety:

```ts
import type {
  SovrantClientOptions,
  ChatMessage,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatCompletionChunk,
  ChunkChoice,
  ResponseChoice,
  SovrantEvent,
  StreamCallbacks,
  UsageInfo,
  ProviderStatus,
  ServerConfig,
  WebhookRequest,
  WebhookResponse,
  WebhookToolCall,
} from "@sovrant/sdk";
```

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
