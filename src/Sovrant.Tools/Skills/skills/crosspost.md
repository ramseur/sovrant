---
name: crosspost
description: Adapts content across platforms with per-platform rules
trigger: /crosspost
tools: [Read, Write]
---

# Crosspost

Adapt a single piece of content for multiple platforms while respecting each platform's norms.

## Steps
1. **Read source content** — understand the core message and audience
2. **Adapt per platform** — apply platform-specific constraints:
   - **X** — 280 chars per tweet, thread if needed, hashtags, no links in first tweet
   - **LinkedIn** — 200-300 words, professional tone, line breaks for readability
   - **Threads** — casual, conversational, can be longer than X
   - **Mastodon** — 500 chars, CW tags if relevant, no tracking links
   - **Blog** — full-length, SEO-optimised headers, internal links
3. **Verify** — each version must make sense standalone

## Rules
- Never just truncate — rewrite for each platform's native format
- Match the conventions of each platform's community
- Vary hooks across platforms
- Preserve the author's voice (if a voice profile exists, use it)
