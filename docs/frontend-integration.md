# Frontend Integration

This guide covers connecting any frontend — browser, Node.js, React, or server-side — to `Sovrant.Server` using the official **@sovrant/sdk** TypeScript SDK.

> **Use the SDK.** It handles authentication, streaming, retries, session management, and security hardening so you don't have to write (or maintain) that code yourself.

---

## Quick Start

### Install

```bash
npm install @sovrant/sdk
```

### Basic usage

```ts
import { SovrantClient } from "@sovrant/sdk";

const client = new SovrantClient({
  baseUrl: "http://localhost:5200",
  token: process.env.SOVRANT_TOKEN!,
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

### Browser apps (recommended: proxy pattern)

Never expose `SOVRANT_TOKEN` to the browser. Use a backend proxy to inject the token server-side:

```
Browser  ──►  Node.js proxy  ──►  Sovrant.Server (localhost:5200)
                injects bearer         wraps the engine
                token server-side
```

The SDK is used on the **proxy** (Node.js) side. The browser talks to your proxy on the same origin — no CORS issues, no credentials in the browser.

### Server-side or internal tools

For internal dashboards, CI scripts, automation pipelines, or any server-to-server integration, use the SDK directly with the token:

```ts
const client = new SovrantClient({
  baseUrl: "https://sovrant.internal:5200",
  token: process.env.SOVRANT_TOKEN!,
});
```

---

## SDK Reference

### Constructor Options

```ts
const client = new SovrantClient({
  baseUrl: "http://localhost:5200",  // Required — only http: and https: allowed
  token: "your-token",               // Required — non-empty string
  model: "gpt-4o",                   // Optional — default model for requests
  sessionId: "session-abc",          // Optional — default session for persistent conversations
  maxRetries: 3,                     // Optional — retry count on 429/5xx (default: 3)
});
```

### Chat — Non-streaming

```ts
const { text, usage } = await client.chat("What is our burn rate?");
// text: string — assistant's response
// usage?: { prompt_tokens, completion_tokens, total_tokens }
```

Override model or session per-call:

```ts
const { text } = await client.chat("Quick question", {
  model: "gpt-4o-mini",
  sessionId: "one-off",
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

### Install

```bash
npm install @sovrant/sdk react
```

### Import

```ts
import { useChat } from "@sovrant/sdk/react";
```

### Usage

```tsx
function Chat() {
  const { messages, send, isStreaming, error, clear } = useChat({
    baseUrl: "/v1",            // Proxy path (browser) or full URL (server)
    token: "proxy-injected",   // Token (use proxy in browser apps)
    model: "gpt-4o",
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

### What the SDK handles

| Protection | Details |
|------------|---------|
| **URL protocol validation** | Only `http:` and `https:` base URLs accepted. `javascript:`, `file:`, `data:`, `ftp:` are rejected at construction time. |
| **Token validation** | Empty or non-string tokens are rejected at construction time. |
| **Token redaction** | `JSON.stringify(client)` outputs `"[REDACTED]"` for the token field. Safe to log the client object. |
| **Bearer token in headers only** | Token is sent via `Authorization: Bearer` header, never in URLs or request bodies. |
| **Path traversal prevention** | Session IDs are `encodeURIComponent()`-encoded in all URL paths. |
| **Query injection prevention** | Export format parameter is validated against an allow-list and URI-encoded. |
| **Prototype pollution protection** | SSE JSON parsing strips `__proto__`, `constructor`, `prototype` keys. |
| **Buffer overflow protection** | SSE buffer is capped at 10 MB. Throws a descriptive error if exceeded. |
| **Automatic retries** | 429 (rate limited) and 5xx (server error) responses are retried with exponential backoff (1s, 2s, 4s). |
| **Typed errors** | `SovrantApiError` includes `status`, `body`, `url` — but never the token. |

### What you should do

#### 1. Never expose tokens to the browser

Use a backend proxy (Node.js, Express, nginx) to inject the `SOVRANT_TOKEN` server-side:

```js
// proxy.js — Node.js/Express
import { createProxyMiddleware } from "http-proxy-middleware";

const SOVRANT_URL   = process.env.SOVRANT_URL   ?? "http://127.0.0.1:5200";
const SOVRANT_TOKEN = process.env.SOVRANT_TOKEN ?? "";

export const sovrantProxy = createProxyMiddleware({
  target: SOVRANT_URL,
  changeOrigin: true,
  on: {
    proxyReq(proxyReq) {
      proxyReq.setHeader("Authorization", `Bearer ${SOVRANT_TOKEN}`);
    },
  },
});
```

```js
// app.js
import express from "express";
import { sovrantProxy } from "./proxy.js";

const app = express();
app.use("/v1", sovrantProxy);
app.listen(3000);
```

#### 2. Store tokens in environment variables

Never hardcode tokens in source code. Use platform secrets management:

| Platform | Where to set |
|----------|-------------|
| **Node.js** | `.env` file (excluded from git) + `dotenv` package |
| **Replit** | Secrets panel |
| **Docker** | `--env-file` or orchestrator secrets |
| **CI/CD** | Pipeline secrets (GitHub Actions, GitLab CI) |

#### 3. Use HTTPS in production

Always use `https://` base URLs in production. The SDK rejects non-HTTP(S) protocols, but it cannot enforce TLS — that's on your deployment.

#### 4. Validate and sanitize user input before sending

The SDK sends messages as-is. If your app collects user input from forms or URLs, sanitize it before passing to `client.chat()` or `client.stream()`:

```ts
function sanitize(input: string): string {
  return input.trim().slice(0, 10_000); // length limit
}

await client.chat(sanitize(userInput));
```

#### 5. Handle errors gracefully

```ts
import { SovrantApiError } from "@sovrant/sdk";

try {
  await client.chat("Hello");
} catch (err) {
  if (err instanceof SovrantApiError) {
    if (err.status === 401) console.error("Invalid token");
    else if (err.status === 429) console.error("Rate limited — try again");
    else console.error(`API error ${err.status}: ${err.body}`);
  } else {
    console.error("Network error:", err);
  }
}
```

#### 6. Scope sessions appropriately

Use separate session IDs per user, per conversation, or per context to prevent cross-contamination:

```ts
// Per-user sessions
const client = new SovrantClient({
  baseUrl: "http://localhost:5200",
  token: process.env.SOVRANT_TOKEN!,
  sessionId: `user:${userId}`,
});

// Per-conversation sessions
const { text } = await client.chat("Continue our discussion", {
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

### Express (Node.js)

```js
import express from "express";
import { createProxyMiddleware } from "http-proxy-middleware";

const app = express();

app.use("/v1", createProxyMiddleware({
  target: process.env.SOVRANT_URL ?? "http://127.0.0.1:5200",
  changeOrigin: true,
  on: {
    proxyReq(proxyReq) {
      proxyReq.setHeader("Authorization", `Bearer ${process.env.SOVRANT_TOKEN}`);
    },
  },
}));

app.listen(3000);
```

### nginx

```nginx
location /v1/ {
    proxy_pass http://127.0.0.1:5200;
    proxy_set_header Authorization "Bearer YOUR_TOKEN";
    proxy_set_header Host $host;

    # SSE support
    proxy_buffering off;
    proxy_cache off;
    proxy_read_timeout 300s;
    proxy_set_header Connection "";
    chunked_transfer_encoding on;
}
```

### Launching the server from Node.js (optional)

If you want Node to manage the server process:

```js
import { spawn } from "child_process";

function startSovrant() {
  const proc = spawn("dotnet", ["run", "--project", "src/Sovrant.Server", "--no-build"], {
    env: {
      ...process.env,
      LLM_API_KEY: process.env.LLM_API_KEY,
      SOVRANT_TOKEN: process.env.SOVRANT_TOKEN,
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

Or using the React hook:

```tsx
const { messages } = useChat({
  baseUrl: "/v1",
  token: "t",
  onToolUse: (event) => console.log(`Running: ${event.tool_name}`),
  onToolResult: (event) => console.log(`Done: ${event.tool_name}`),
});

// Each assistant message includes toolEvents array
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
