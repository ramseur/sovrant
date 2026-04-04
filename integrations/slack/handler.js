#!/usr/bin/env node
/**
 * Sovrant Slack Bot — minimal event handler.
 *
 * Listens for @mentions and direct messages in Slack, forwards them to the
 * Sovrant webhook endpoint, and posts the response back to the Slack thread.
 *
 * Environment variables:
 *   SLACK_BOT_TOKEN      — xoxb-... Bot User OAuth Token
 *   SLACK_APP_TOKEN      — xapp-... App-Level Token (for Socket Mode)
 *   SOVRANT_URL          — Base URL of the Sovrant server (default: http://localhost:5200)
 *   SOVRANT_TOKEN        — Bearer token for the Sovrant server
 *   SOVRANT_MODEL        — Optional model override
 *
 * Install: npm install @slack/bolt
 * Run:     node handler.js
 */

const { App } = require("@slack/bolt");

const SOVRANT_URL = process.env.SOVRANT_URL || "http://localhost:5200";
const SOVRANT_TOKEN = process.env.SOVRANT_TOKEN || "";
const SOVRANT_MODEL = process.env.SOVRANT_MODEL || undefined;

const app = new App({
  token: process.env.SLACK_BOT_TOKEN,
  appToken: process.env.SLACK_APP_TOKEN,
  socketMode: true,
});

/**
 * Forward a Slack message to the Sovrant webhook endpoint.
 */
async function askSovrant(userId, message, threadId) {
  const body = {
    source: "slack",
    user_id: userId,
    message,
    model: SOVRANT_MODEL,
    thread_id: threadId,
  };

  const res = await fetch(`${SOVRANT_URL}/v1/webhook`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${SOVRANT_TOKEN}`,
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Sovrant returned ${res.status}: ${text}`);
  }

  return res.json();
}

// ── @mention in channels ──────────────────────────────────────────────────────
app.event("app_mention", async ({ event, say }) => {
  // Strip the bot mention from the message text.
  const text = event.text.replace(/<@[^>]+>/g, "").trim();
  if (!text) return;

  try {
    const result = await askSovrant(event.user, text, event.thread_ts || event.ts);
    await say({ text: result.text, thread_ts: event.thread_ts || event.ts });
  } catch (err) {
    console.error("Sovrant error:", err.message);
    await say({
      text: `Sorry, I encountered an error: ${err.message}`,
      thread_ts: event.thread_ts || event.ts,
    });
  }
});

// ── Direct messages ───────────────────────────────────────────────────────────
app.event("message", async ({ event, say }) => {
  // Only handle DMs (im), ignore bot messages and threaded replies.
  if (event.channel_type !== "im" || event.bot_id || event.subtype) return;

  try {
    const result = await askSovrant(event.user, event.text, event.ts);
    await say({ text: result.text });
  } catch (err) {
    console.error("Sovrant error:", err.message);
    await say({ text: `Sorry, I encountered an error: ${err.message}` });
  }
});

(async () => {
  await app.start();
  console.log("Sovrant Slack bot is running (Socket Mode).");
})();
