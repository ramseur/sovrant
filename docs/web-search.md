# Web Search

Sovrant's web-search story has two layers that share a single backend selector:

1. **The `WebSearch` function tool** — what an agent calls when it explicitly
   wants search results returned to the conversation as text.
2. **Provider-native server tools** — what the LLM uses on its own when the
   active provider supports built-in search (OpenAI Responses
   `web_search_preview`, OpenRouter `plugins:[{id:"web"}]`, Anthropic
   `web_search_20250305`, and Gemini `tools:[{google_search:{}}]` once the
   native `generateContent` endpoint is wired up).

Both layers consult the same `WebSearchOptions.Backend` value, resolved once at
startup. A `/websearch <backend>` slash command override is honoured for the
current session via `SovrantConfig.WebSearchOverride`.

## Backends

| Backend | Tool result | Native injection | Required env |
|---|---|---|---|
| `auto` (default) | Brave > Firecrawl > LLM | Off when a paid key is set, otherwise on for capable models | none |
| `brave` | Brave Search only | Off | `BRAVE_API_KEY` |
| `firecrawl` | FireCrawl only | Off | `FIRECRAWL_API_KEY` |
| `native` | Guidance message — model handles search | On (warns if model lacks support) | none |
| `off` | Disabled message | Off | none |
| `searxng-future` | Behaves like `auto` (deferred backend, not yet implemented) | Same as `auto` | none |

When native injection is on, the function-tool form of `WebSearch` is
suppressed in the request so the model picks the server tool.

## Configuration precedence

Highest priority wins:

1. `/websearch <backend>` in the running session
   (`SovrantConfig.WebSearchOverride`).
2. `SOVRANT_WEB_SEARCH` environment variable.
3. `workspace_settings` DB row keyed `websearch.backend` (V018 schema). The
   Web (Blazor) `/settings` page and the Desktop Settings panel write here
   directly and fire `LiveSettingsRegistry.ReloadAll()` so the change takes
   effect on the next request without a restart (Phase 93 Bucket-B).
4. `user_preferences` DB row keyed `websearch.backend` (V020 schema) for
   per-user overrides.
5. Legacy `LLM_WEB_SEARCH=true` → `native` (with a deprecation warning).
6. Default: `auto`.

`settings.json` is no longer consulted as of Phase 93 — all on-disk JSON
config for web search migrated into SQLite. The `LegacyConfigMigrator` runs
once on first boot and lifts any pre-Phase-93 `WebSearch:Backend` value into
the DB.

## Provider matrix

The native server tool is only injected when the active model's
`SupportsNativeWebSearch` capability is `true`. The capability registry is
seeded from per-provider rules and per-deployment overrides — see
`src/Sovrant.Api/Capabilities/`.

| Provider / dialect | Wire field | Tool name |
|---|---|---|
| OpenAI Responses API | `tools[].type` | `web_search_preview` |
| OpenRouter (chat completions) | `plugins[].id` | `web` |
| Anthropic Messages | `tools[].type` | `web_search_20250305` |
| Gemini (deferred — current OpenAI shim does not surface search) | `tools[].google_search` | `{}` |

Models that don't support native search either fall back to `auto`'s paid-key
chain or, when explicitly forced to `native`, surface the request unchanged so
the failure is visible rather than silent.

## Slash command

```
/websearch                # show the active backend and key status
/websearch auto           # use the default decision chain
/websearch brave          # force Brave for this session
/websearch firecrawl      # force FireCrawl for this session
/websearch native         # delegate to the model's built-in search
/websearch off            # disable web search for this session
```

`enable` and `disable` remain as aliases for `native` and `off` for backward
compatibility with pre–Phase 70 muscle memory.
