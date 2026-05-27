# Integration Connection Matrix

Phase 107 audit — last updated 2026-05-26.

This document is the acceptance gate for the Integrations Gallery. Every entry
is reviewed for package availability, credential field accuracy, and known
setup requirements. OAuth-required entries are flagged for Phase 101.

---

## Status legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Verified — package exists, credentials confirmed |
| ⚠️ | Works but has known caveats |
| 🔒 | OAuth required (Phase 101) |
| 🛠️ | Fixed in this phase |
| ❌ | Removed — no usable package found |

---

## Automation

| Integration | Kind | Package / Endpoint | Credential fields | Status | Notes |
|---|---|---|---|---|---|
| Composio | HTTP | `https://mcp.composio.dev/composio/{API_KEY}` | API Key → `x-api-key` header | ✅ 🛠️ | Header corrected from `Authorization` to `x-api-key` |
| n8n | HTTP | Custom endpoint | API Key → `X-N8N-API-Key` header | ✅ | Header confirmed correct |
| Zapier | HTTP | Custom endpoint (from Zapier dashboard) | Endpoint required; API key optional | ✅ 🔒 🛠️ | Old URL `actions.zapier.com/mcp/{key}/sse` removed; OAuth is recommended path; manual endpoint still works |
| Make | HTTP | Custom endpoint | API Key → `Authorization` header | ✅ | No changes needed |

---

## Platform

| Integration | Kind | Package / Endpoint | Credential fields | Status | Notes |
|---|---|---|---|---|---|
| GitHub | stdio | `npx -y @modelcontextprotocol/server-github` | `GITHUB_PERSONAL_ACCESS_TOKEN` | ⚠️ 🛠️ | Env var corrected from `GITHUB_TOKEN`; npm package deprecated but still functional; GitHub's new canonical server is Docker-based (`ghcr.io/github/github-mcp-server`) — upgrade tracked separately |
| Slack | stdio | `npx -y @modelcontextprotocol/server-slack` | `SLACK_BOT_TOKEN` | ✅ | No changes needed |
| Notion | stdio | `npx -y @notionhq/notion-mcp-server` | `NOTION_API_KEY` | ✅ | No changes needed |
| Linear | HTTP | `https://mcp.linear.app/mcp` | OAuth only | 🔒 🛠️ | Switched from broken `@linear/mcp-server` (does not exist on npm) to Linear's official remote HTTP endpoint with OAuth 2.1 |
| Stripe | stdio | `npx -y @stripe/mcp --tools=all` | `STRIPE_SECRET_KEY` | ✅ | No changes needed |
| PostgreSQL | stdio | `npx -y @modelcontextprotocol/server-postgres` | Connection string (CLI arg) | ✅ | No changes needed |
| Supabase | stdio | `npx -y @supabase/mcp-server-supabase --access-token {key}` | Access Token (headless) | ⚠️ | Manual token flow still valid for CI/headless; interactive OAuth via Supabase dashboard is the default — flagged for Phase 101 |
| Snowflake | stdio | `npx -y snowflake-mcp` | `SNOWFLAKE_PASSWORD` + account identifier | ⚠️ 🛠️ | Package name corrected (`snowflake-mcp-server` → `snowflake-mcp`); full env var set is `SNOWFLAKE_USER`, `SNOWFLAKE_PASSWORD`, `SNOWFLAKE_ACCOUNT`, `SNOWFLAKE_WAREHOUSE`, `SNOWFLAKE_DATABASE`, `SNOWFLAKE_SCHEMA` — catalog form only surfaces password and account; remaining vars require Phase 96 (MCP runtime variables) |

---

## Search

| Integration | Kind | Package / Endpoint | Credential fields | Status | Notes |
|---|---|---|---|---|---|
| Brave Search | stdio | `npx -y @modelcontextprotocol/server-brave-search` | `BRAVE_API_KEY` | ✅ | No changes needed; free tier: 2,000 queries/month |
| Exa | stdio | `npx -y exa-mcp-server` | `EXA_API_KEY` | ✅ | No changes needed |
| Tavily | stdio | `npx -y tavily-mcp` | `TAVILY_API_KEY` | ✅ | No changes needed |

---

## DXP / CMS

| Integration | Kind | Package / Endpoint | Credential fields | Status | Notes |
|---|---|---|---|---|---|
| Sitecore Community | stdio | `npx -y @antonytm/mcp-sitecore-server` | `AUTORIZATION_HEADER` (optional) + GraphQL endpoint | ✅ | Env var name `AUTORIZATION_HEADER` is intentionally misspelled — matches the package's actual env var; not a catalog error |
| Sitecore Marketer | HTTP | Endpoint from Sitecore Cloud Portal | OAuth only | 🔒 | No changes needed; OAuth flag added |
| Adobe AEM | HTTP | AEM MCP endpoint | OAuth token → `Authorization` header | 🔒 | OAuth flag added |
| Optimizely CMS | — | — | — | ❌ | Removed: `optimizely-cms-mcp` does not exist on npm; no official or widely-adopted community package found; re-add when an installable package ships |

---

## Phase 101 OAuth queue

The following entries require browser-based OAuth and are blocked on Phase 101
(OAuth 2.1 + PKCE for MCP connections):

| Integration | Auth provider |
|---|---|
| Zapier | Zapier OAuth 2.0 |
| Linear | Linear OAuth 2.1 |
| Supabase | Supabase OAuth (interactive default) |
| Sitecore Marketer | Sitecore Identity OAuth 2.0 |
| Adobe AEM | Adobe IMS OAuth 2.0 + PKCE |

---

## Open issues

| Issue | Integration | Blocked on |
|---|---|---|
| GitHub npm package deprecated | GitHub | GitHub to publish an npx-compatible wrapper for their Go binary, or we migrate to Docker-based launch |
| Snowflake missing 4 env vars | Snowflake | Phase 96 (MCP runtime variables editor) |
| Supabase OAuth path not wired | Supabase | Phase 101 |
