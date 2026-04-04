# Webhooks & Chat Integrations

Sovrant exposes a generic webhook endpoint (`POST /v1/webhook`) that accepts messages from any source — Slack, Teams, Discord, or custom applications — and routes them through the agentic engine using persistent sessions.

---

## Webhook endpoint

### `POST /v1/webhook`

Accepts a message, runs an agentic turn, and returns the result either synchronously or via a callback URL.

**Request:**

```json
{
  "source": "slack",
  "user_id": "U123ABC",
  "message": "What tests are failing in the auth module?",
  "callback_url": "https://hooks.slack.com/...",
  "model": "gpt-4o-mini",
  "thread_id": "1234567890.123456"
}
```

| Field | Required | Description |
|---|---|---|
| `source` | Yes | Source identifier (e.g. `slack`, `teams`, `discord`, `custom`) |
| `user_id` | Yes | External user identifier — combined with source to derive a stable session |
| `message` | Yes | The user message to process |
| `callback_url` | No | URL to POST the result to. If omitted, the result is returned synchronously. |
| `model` | No | Model override for this request |
| `thread_id` | No | Thread/channel ID (informational — echoed in the response) |

### Session derivation

The webhook derives a Sovrant session ID as `webhook:{source}:{user_id}`. This means:
- Each user gets their own persistent conversation history
- Different sources (Slack vs Teams) for the same user get separate sessions
- History survives across requests — the agent remembers prior turns

### Synchronous mode (no `callback_url`)

The response is returned inline:

```json
{
  "success": true,
  "source": "slack",
  "user_id": "U123ABC",
  "thread_id": "1234567890.123456",
  "session_id": "webhook:slack:U123ABC",
  "text": "The auth module has 3 failing tests...",
  "tool_calls": [
    { "id": "tc_1", "tool_name": "grep", "is_error": false },
    { "id": "tc_2", "tool_name": "read", "is_error": false }
  ],
  "errors": [],
  "input_tokens": 1200,
  "output_tokens": 350
}
```

### Asynchronous mode (with `callback_url`)

Returns `202 Accepted` immediately:

```json
{ "status": "accepted", "session_id": "webhook:slack:U123ABC" }
```

The full result is POSTed to the callback URL in the background. Callback delivery failures are logged but do not affect the agent turn.

---

## Slack integration

A ready-to-use Slack bot is included at `integrations/slack/`.

### Setup

1. Create a new Slack app at [api.slack.com/apps](https://api.slack.com/apps) using the manifest at `integrations/slack/manifest.json`
2. Install the app to your workspace
3. Enable Socket Mode and generate an app-level token (`xapp-...`)
4. Copy the Bot User OAuth Token (`xoxb-...`)

### Run the bot

```bash
cd integrations/slack
npm install @slack/bolt

export SLACK_BOT_TOKEN="xoxb-..."
export SLACK_APP_TOKEN="xapp-..."
export SOVRANT_URL="http://localhost:5200"
export SOVRANT_TOKEN="your-secret-token"
export SOVRANT_MODEL="gpt-4o-mini"  # optional

node handler.js
```

### Usage

- **@mention in a channel:** `@Sovrant what's the test coverage for auth?`
- **Direct message:** just type your question

The bot forwards each message to the Sovrant webhook endpoint using Socket Mode (no public URL required). Responses are posted back to the same thread.

---

## Microsoft Teams

Use the webhook endpoint with a Teams outgoing webhook or a Bot Framework bot:

```bash
curl -X POST http://localhost:5200/v1/webhook \
  -H "Authorization: Bearer $SOVRANT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "source": "teams",
    "user_id": "user@company.com",
    "message": "Summarize recent changes to the API",
    "callback_url": "https://your-teams-bot.azurewebsites.net/api/callback"
  }'
```

The callback URL receives the full `WebhookResponse` JSON. Your Teams bot should parse the `text` field and post it back to the Teams conversation.

---

## Discord

Use a Discord bot that listens for messages and forwards them to the webhook:

```bash
curl -X POST http://localhost:5200/v1/webhook \
  -H "Authorization: Bearer $SOVRANT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "source": "discord",
    "user_id": "123456789012345678",
    "message": "Find all TODO comments in the codebase",
    "thread_id": "channel-id"
  }'
```

---

## Custom integrations

Any HTTP client can use the webhook endpoint. The `source` field is a free-form string — use it to namespace sessions by integration:

```bash
# Internal tool
curl -X POST http://localhost:5200/v1/webhook \
  -H "Authorization: Bearer $SOVRANT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "source": "internal-dashboard",
    "user_id": "engineer-42",
    "message": "Why is the build failing?"
  }'
```

---

## Security

- The webhook endpoint requires the same `SOVRANT_TOKEN` bearer auth as all other endpoints.
- `callback_url` must be an absolute HTTP or HTTPS URL — relative URLs and non-HTTP schemes are rejected.
- The Slack bot uses Socket Mode (WebSocket) — no public URL or ingress is required.
- Each `source:user_id` pair gets an isolated session — users cannot see each other's conversations.
- Rate limiting applies per session (same `SOVRANT_RATE_LIMIT_RPM` policy as chat completions).
