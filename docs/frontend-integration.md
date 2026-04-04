# Frontend Integration

This guide covers connecting a Node.js / Express frontend (e.g. hosted on Replit) to `Sovrant.Server`.

---

## Architecture

```
Browser  ──►  Node.js proxy (Replit)  ──►  Sovrant.Server (localhost:5200)
                 injects bearer token            wraps the engine
```

The proxy runs on the same machine as the server. It injects the `SOVRANT_TOKEN` bearer token server-side so the credential never reaches the browser.

---

## Node.js Proxy Setup

### Install

```bash
npm i http-proxy-middleware
```

### Proxy route

```js
// proxy.js
import { createProxyMiddleware } from 'http-proxy-middleware';

const SOVRANT_URL   = process.env.SOVRANT_URL   ?? 'http://127.0.0.1:5200';
const SOVRANT_TOKEN = process.env.SOVRANT_TOKEN ?? '';

export const sovrantProxy = createProxyMiddleware({
  target: SOVRANT_URL,
  changeOrigin: true,
  on: {
    proxyReq(proxyReq) {
      proxyReq.setHeader('Authorization', `Bearer ${SOVRANT_TOKEN}`);
    },
  },
});
```

### Mount in Express

```js
import express from 'express';
import { sovrantProxy } from './proxy.js';

const app = express();

// All /v1/* calls are proxied to the sovrant server
app.use('/v1', sovrantProxy);

app.listen(3000);
```

### Replit secrets

Set these in the Replit **Secrets** panel (not in code):

| Key | Example value |
|---|---|
| `SOVRANT_URL` | `http://127.0.0.1:5200` |
| `SOVRANT_TOKEN` | `my-secret-token` (must match server) |

---

## Calling the API from the Browser

The browser calls the Node proxy on the same origin — no CORS issues and no credentials in the browser.

### Streaming chat (fetch SSE)

```js
async function* streamChat(messages, sessionId) {
  const res = await fetch('/v1/chat/completions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      model: 'gpt-4o',
      messages,
      stream: true,
      session_id: sessionId,   // optional — resumes saved session
    }),
  });

  const reader = res.body.getReader();
  const decoder = new TextDecoder();

  for await (const chunk of readLines(reader, decoder)) {
    if (chunk === '[DONE]') return;
    const data = JSON.parse(chunk);
    const delta = data.choices?.[0]?.delta?.content;
    if (delta) yield delta;
  }
}

// Helper — splits SSE stream into `data: ...` payloads
async function* readLines(reader, decoder) {
  let buf = '';
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    buf += decoder.decode(value, { stream: true });
    const lines = buf.split('\n');
    buf = lines.pop();
    for (const line of lines) {
      if (line.startsWith('data: ')) yield line.slice(6).trim();
    }
  }
}
```

### Non-streaming chat

```js
const res = await fetch('/v1/chat/completions', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    messages: [{ role: 'user', content: 'Hello' }],
    stream: false,
  }),
});
const data = await res.json();
console.log(data.choices[0].message.content);
```

### Updating config

```js
await fetch('/v1/config', {
  method: 'PUT',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ model: 'gpt-4o-mini' }),
});
```

### Listing sessions

```js
const { sessions } = await fetch('/v1/sessions').then(r => r.json());
```

---

## Launching the Server from Node (optional)

If you want Node to manage the server process lifecycle on Replit:

```js
import { spawn } from 'child_process';

function startSovrant() {
  const proc = spawn('dotnet', ['run', '--project', 'src/Sovrant.Server', '--no-build'], {
    env: {
      ...process.env,
      LLM_API_KEY:   process.env.LLM_API_KEY,
      SOVRANT_TOKEN: process.env.SOVRANT_TOKEN,
    },
    stdio: 'inherit',
  });
  proc.on('exit', code => console.error(`sovrant-server exited with ${code}`));
  return proc;
}

const server = startSovrant();
process.on('exit', () => server.kill());
```

The Node app should wait for `/health` to return `200` before routing traffic:

```js
async function waitForServer(url, timeout = 15_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    try {
      const r = await fetch(`${url}/health`);
      if (r.ok) return;
    } catch { /* not ready yet */ }
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error('Sovrant server did not start in time');
}

await waitForServer('http://127.0.0.1:5200');
```

---

## Tool Events in the SSE Stream

When the engine calls a tool (e.g. `Bash`, `Read`) the SSE chunk includes a `sovrant` extension field:

```json
{
  "choices": [{ "delta": {}, "index": 0 }],
  "sovrant": {
    "event": "tool_use",
    "tool_name": "Bash",
    "tool_use_id": "tu_abc123",
    "is_error": false
  }
}
```

Use this to show a "thinking / running tool" indicator in the UI:

```js
if (data.sovrant?.event === 'tool_use') {
  showToolIndicator(data.sovrant.tool_name);
}
if (data.sovrant?.event === 'tool_result') {
  hideToolIndicator();
}
```
