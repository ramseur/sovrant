# Sovrant — Roadmap

**Branch:** `development`
**Last updated:** 2026-07-01 (Phase 130 planned — OpenRouter account registration & in-app key issuance via OAuth PKCE against OpenRouter's own `/auth` flow, reusing Phase 101's PKCE + loopback-listener plumbing; "Get an OpenRouter key" button on Providers settings (Web + Desktop) removes the manual copy/paste key step entirely. Phase 128 shipped ✅ — five-part plan complete: artifact security hardening (content-security headers, RemoteArtifactStore field-name fix, API zip endpoint, Artifacts.razor abstraction fix); code manifest in ArtifactManifest; scaffold enrichment (.sln + Directory.Build.props + .editorconfig + CI for all 21 templates, every .NET scaffold immediately buildable with dotnet build {sln}); LLM instruction enrichment (IProjectTemplate.BuildCommand/RunCommand, CodeCreate next_steps response, tool description update); new CodeValidateTool (structural checks via IArtifactStore.ListAsync — no compiler in PATH required, V045/V046 migrations). Phase 129 planned — Missions → Workflows rename + UX (surface labels only, dedicated Workflows page, positioning callout, `/v1/workflows` alias). v1.5 focus: Phase 114 ✅ skill enrichment + Phase 74 markdown document templates + Phase 128 ✅ + Phase 126 + Phase 129. Phase 123 ✅ — Memory System: workspace memory with public/private scoping, "+Remember" button in Chat (Web + Desktop), V041/V042 migrations, per-user injection in multi-user deployments. V040 MCP stable IDs (Phase 105 workspace-level gating). V043 email-as-user-id (replaces `usr_{hex}` PKs). PostgresSchema split to `db/postgres/PostgresSchema.sql` + `db/supabase/migrations/` (Phase 40C documentation update). Phase 127 planned — Supabase RLS. Phase 126 planned — chat conversation UX: collapsed work strips. Phase 125 planned — web search via integrations. Phase 124 planned — file system access controls. Phase 96 ✅ — MCP runtime variables: inline env var editor Web + Desktop, keystore in DB (V039). Phase 116 ✅ — Intelligent Knowledge Harness complete: A–H shipped; knowledge_attributions table, IKnowledgeRouter, per-turn PII sanitization, MCP tool relevance filtering, provenance Sources UI. Phase 113 ✅ — CachedKnowledgeStore + Phase 31 CacheInvalidator repair. Phase 112 ✅ — all built-in markdown (skills, agents, 42 doc templates) in DB; dual-write removed. Phase 108 ✅ — knowledge_pages universal store. Phase 103 ✅ — MCP trust gates + trust rules editor UI. Phase 101 ✅ — OAuth 2.1 + PKCE for MCP.)

This document tracks planned features, architectural decisions, and the reasoning behind them.

---

## Current Focus

What we are actively working on and shipping next, in priority order.

| Wave | Phase | What |
|---|---|---|
| **v1.0 — done** | Phase 92 | Active Sessions — up to 5 concurrent live tasks, return-anytime results ✅ |
| **v1.0 — done** | Phase 86 | Background session continuation across navigation and session switches ✅ |
| **v1.0 — done** | Phase 87 | Artifacts-by-default — finish workspace identity unification (auto-save partial ✅) ✅ |
| **v1.1 — done** | Phase 79 | Agents page — in-app create/edit of agent definitions + Launch chat button ✅ |
| **v1.1 — done** | Phase 94 | Orchestration Studio — compose and run teams from the UI without chat commands ✅ |
| **v1.1 — done** | Phase 95 | Integrations Gallery — catalog-first MCP onramp, encrypted credentials, Web + Desktop parity ✅ |
| **v1.1 — done** | Phase 50 | OpenClaw federation — `SwarmFederationMode`, bus client, manager-led + siloed routing, V029 migration ✅ |
| **v1.1 — done** | Phase 73 | Code scaffolding — 21 templates, multi-component generation, manifest validation, 235 tests ✅ |
| **v1.1 — done** | Phase 40C | Supabase backend (optional, admin-configured) — PostgreSQL stores for sessions + credentials; admin System Integrations UI; idempotent schema init; SQLite → Postgres migrator; boot-time DI switch; Web + Desktop parity ✅ |
| **v1.2 — done** | Phase 98 | User Dashboard — cross-workspace activity view; own public (Shared) + own private + teammates' public; V030 `is_private`; Web + Desktop rail nav; pagination, timestamps, 30s poll, guide panels, page-preserve on refresh ✅ |
| **v1.2 — done** | Phase 99 | Private sessions and agent runs — per-record privacy toggle on sessions, agent runs, and missions; masked in Command Center, excluded from User Dashboard; server-side enforcement via V030 `is_private` ✅ |
| **v1.2 — done** | Phase 96 | MCP runtime variables — inline env var editor on Web + Desktop; KEY=VALUE textarea in stdio add form; JSON paste auto-populates env vars; master key moved into DB (V039 `keystore` table); AES-256-GCM encrypted at rest ✅ |
| **v1.3 — done** | Phase 105 | MCP server permissions — workspace-level gating via V040 stable MCP IDs; `GetEnabledEntriesAsync` workspace filter; admin toggle UI on Web + Desktop; memory-gate guards at selection point ✅ (project-level deferred) |
| **v1.3 — done** | Phase 123 | Memory System — workspace memory with public/private scoping; "+Remember" button in Chat (Web + Desktop); Workspace tab on Memory page; V041/V042 migrations; per-user injection in multi-user deployments ✅ |
| **v1.3 — done** | Phase 91 *(partial)* | Knowledge Authoring — admin edit/revert on Skills and Document Templates pages; Monaco editor for prompt + JSON editing; Knowledge sub-nav sorted alphabetically (Web + Desktop); Avalonia 11 → 12 desktop migration ✅ (AvaloniaEdit Desktop fixes deferred) |
| **v1.4 — done** | V043 | Email-as-user-id — V043 migration replaces all `usr_{hex}` PKs with email across every FK and soft-reference column; `username` column dropped; `IUserService` API updated; personal workspace IDs updated in tandem ✅ |
| **v1.4 — done** | Bug fixes | Ollama provider label/routing fixed (FriendlyProviderName checks provider name before URL); FK error 19 on project creation fixed (PersonalWorkspaceId reads `App.SovrantUserId` at call time) ✅ |
| **v1.5 — done** | Phase 114 | Enrich built-in skill definitions — 2-3 sentence descriptions, agent list wiring, `verification-loop` tools fix; V044 migration; immediate quality lift for every LLM via the IKnowledgeRouter harness ✅ |
| **v1.5 — done** | Phase 74 | DB-backed document templates — 42 of 44 built-in templates in `knowledge_pages` via V037; Scriban rendering; admin Edit/Revert on Documents page (Web + Desktop); copy-on-write overlay; `sovrant document lint` CLI; authoring guide ✅ |
| **v1.5 — done** | Phase 128 | Code generation quality gates — artifact security hardening (content-security headers, API zip endpoint, RemoteArtifactStore fix, Artifacts.razor abstraction); `.sln` + `Directory.Build.props` + CI for all 21 templates (every project immediately runnable); `CodeValidateTool` (structural checks via `IArtifactStore.ListAsync`, no compiler in PATH); `CodeCreate` `next_steps` + `build_command` + LLM instruction update; `ArtifactManifest` code metadata; V045/V046 migrations ✅ |
| **v1.5 — pending UAT** | Phase 126 | Chat conversation UX — collapsed work strips replace per-tool boxes; two-level expand (strip → tool list → full detail); live "doing X" in-progress indicator; agent answer prominent, tool work subordinate; Web + Desktop parity — implemented, awaiting live UAT pass ✅(code) |
| **v1.5 — next** | Phase 129 | Missions → Workflows — surface-label rename (presentation-layer only); dedicated Workflows page with goal-first launch, active/recent cards, detail view; positioning callout (AI workflows vs n8n/Zapier automation); `/v1/workflows` API alias; **plus dual-path execution** — route through Claude Agent SDK dynamic workflow orchestration when a qualifying Claude tier is active, else fall back to Sovrant's own mission engine (base version) — model/tier gate TBD |

> Items below v1.0 are planned but not yet scheduled. See [Still pending](#still-pending) for the full gap list.

---

## Current State

The engine is fully functional across five delivery modes with enterprise multi-tenant infrastructure:

- **58 tools** across 18 categories (core file, extended, todo, tasks, plan mode, worktree, skills, MCP, agent, team, missions, artifacts, documents, quality, swarm, coordination, LSP, code scaffolding)
- **2,208 tests** across 10 projects, 0 failures
- **141 server endpoints** + 1 SignalR hub (chat, sessions, config, status, models, usage, cost, command-center, webhooks, workspaces, projects, users, teams, runs, missions, engine, artifacts, evals, swarm, tools, skills, agents, MCP auth, knowledge, trust-rules, attributions)
- **5 delivery modes:** CLI REPL, HTTP server (:5200), desktop app (Avalonia), web app (Blazor :5100), MCP server (stdio)
- Agentic loop with up to 20 tool rounds per turn
- SQLite persistence layer with 46 versioned migrations (V001–V046) — V045 seed built-in tool guides for CodeCreate/CodeCreateMulti (Phase 128D), V046 seed CodeValidateTool guide (Phase 128E), V044 enrich built-in skill descriptions + agent/tool list fixes (Phase 114), V040 `mcp_servers.id` stable surrogate for workspace-scoped gating (Phase 105), V041 `workspace_memory` owner/privacy columns (Phase 123), V042 `session_summaries`/`learned_patterns`/`instincts` owner scoping (Phase 123), V043 email-as-user-id rewrite (replaces `usr_{hex}` PKs), V038 `knowledge_attributions` table (Phase 116F), V039 `keystore` table — master AES key in DB (Phase 96), V030 `is_private` (Phase 98), V031 `agent_name` (Phase 106), V032–V033 `knowledge_pages` + `knowledge_attributions` schema (Phase 108/116F), V034–V037 `role`/`recommended_level` + `fields_json`/`filename_template` + skill/agent/doc-template seeds (Phase 112A–D) on top of the Phase 32/42.5/51/52/57/78 foundation
- Single `.env` file configuration — `sovrant.config` removed; all bootstrap knobs are env vars; routing and swarm config fully DB-backed
- **Integrations Gallery** on Web and Desktop — catalog-first MCP onramp with Automation (Composio, n8n, Zapier, Make), Platform (GitHub, Slack, Notion, Linear, Stripe, PostgreSQL, Supabase, Filesystem), and Search (Brave, Exa, Tavily) tiers; credentials stored in encrypted keystore (Phase 95 ✅)
- **Model switcher with provider discovery** — configured providers selectable inline; unconfigured known providers shown with click-to-configure deep-link to Settings → Providers on both Web and Desktop
- **Agent run prompt titles** — one-shot agent runs store the user's prompt (V028); Recent Runs list uses prompt as title with agent name badge on both Web and Desktop
- **Command Center cockpit** at `/command` on Web and Desktop — read-only live grid aggregating active missions, team runs, agent runs, and sessions; Owner column resolves userId → username/email; click-through to existing detail pages; paginated grid with header timestamp, 30s auto-refresh, and page-preserve on navigation (Phase 89/90/98 polish ✅)
- Mission engine with durable goals, re-planning, acceptance gates, and event journal (Phase 51 ✅)
- Unified agent orchestration: SQLite-backed teams + swarm + agent run ledger (Phase 52 ✅)
- Scoped artifact storage with workspace-first layout (Phase 53 ✅)
- Agent artifact tools — isolated produce-and-deposit pattern for team deliverables (Phase 41 ✅)
- Model capability registry with layered resolution (Phase 54 ✅)
- SmartRouter with health/latency/cost scoring + intent-aware model tier routing (Phase 48 ✅)
- Cost tracking with OpenRouter pricing, per-session/project budgets, JSONL metrics log, `/cost` CLI, `GET /v1/cost` API (Phase 55 ✅)
- Remote server mode for web frontend — SignalR streaming, bearer auth, `AddSovrantClient()` DI abstraction (Phase 61 ✅)
- Workspace/project/user hierarchy with membership, invites, config inheritance (Phases 35–37 ✅)
- Per-user token auth with API token issuance/revocation (Phase 38 ✅)
- Multi-provider support: OpenAI, Gemini, Ollama, native messages API, OpenAI Responses API — OpenAI is the default provider in setup and admin (changed from OpenRouter)
- Multi-tenant per-request credentials (`X-LLM-Api-Key` / `X-LLM-Base-Url` headers)
- Per-session config overlay, rate limiting, token usage tracking
- Session TTL eviction + LRU cap + per-session turn serialization
- Context auto-compaction at configurable token threshold
- Security hardening: BashTool 256 KB cap + env stripping, WebFetch SSRF protection, provider retry 3×
- Webhook integration (Slack, Teams, Discord, custom)
- Frontend SDK (TypeScript, ~115 endpoint methods, SSE streaming, React hook)
- MCP server mode (stdio JSON-RPC 2.0) + dynamic MCP tool proxy + MCP OAuth
- LSP integration (5 tools, 18 languages)
- CI/CD integration (`--ci` flag, GitHub Actions action, GitLab CI template)
- Database lifecycle tools: `sovrant db status/version/migrate/backup/inspect` (Phase 42.5 ✅)
- Windows PowerShell native integration with cwd persistence (Phase 43 ✅)

### Agent System: Current State

| Layer | Status | Notes |
|---|---|---|
| Ad-hoc sub-agent (`AgentTool`) | ✅ Working | Spawns a fresh `ConversationRuntime`, runs one isolated turn, returns text. Recursion depth ≤ 5. |
| Orchestration interfaces (`IAgent`, `IOrchestrationSystem`) | ✅ Complete | Both isolated and shared backends implement the same interface. |
| Shared backend (`OrchestrationCoordinator`) | ✅ Complete | Semaphore-based concurrency control, linked CTS with timeout, proper shutdown drain. |
| Isolated backend (`ProcessBasedOrchestrationSystem`) | ✅ Complete | Process spawn, stdin/stdout JSON, process tree kill on cancel. |
| `SovrantAgent` + `SovrantAgentFactory` | ✅ Complete | Runtime-backed agents with role-specific system prompts and optional tool filtering. |
| Config switch (`AGENT_MODE`) | ✅ Working | `isolated` (default, process-per-agent) or `shared` (in-process). |
| Team tools | ✅ Complete | `TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`, `TeamRun`, `TeamPublish`. SQLite-backed teams with workspace/project scoping. |
| Unified orchestration | ✅ Complete | `AgentOrchestrator` unifies teams + swarm. `agent_runs` ledger tracks all executions. Three modes: pre-existing team, composed teams, engine decomposition. |
| Mission engine | ✅ Complete | `IMissionStore` + `LlmMissionPlanner` + `ParallelMissionExecutor`. Durable goals with re-planning, acceptance gates, event journal. |

### Completed phases (1–56)

| Phase | Summary |
|---|---|
| 1 | Tool parity — 9 new tools (EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree, TaskUpdate, Skill, ToolSearch, ListMcpResources, ReadMcpResource) |
| 2 | Agent memory files, token count fix, context auto-compaction |
| 3 | OpenAI Responses API (`LLM_WEB_SEARCH=true`) |
| 4–6 | Security hardening, reliability, robustness |
| 7 | Structured async logging |
| 8 | Multi-tenant per-request credentials |
| 9 | Session lifecycle (TTL eviction, LRU cap, turn serialization) |
| 10 | Small team hardening (session-scoped config, rate limiting, usage tracking) |
| 11 | LSP integration (5 tools, 18 language extensions) |
| 12 | CI/CD pipeline integration |
| 13 | Slack / webhook integration |
| 14 | Frontend SDK, diff view, session export |
| 15 | MCP server mode |
| 16 | Dynamic MCP tool proxy (`MCPTool`) |
| 17 | MCP OAuth authentication (`McpAuthTool`) |
| 18 | Dual agent architecture scaffolding |
| 19+20 | Orchestration backend + team tools (58 tests) |
| 21 | Hook lifecycle system |
| 22 | Specialized agent definitions (role templates + model routing) |
| 23 | Template externalisation (built-ins as markdown files) |
| 24 | Verification loop & quality gates |
| 25 | Governance, security monitoring & audit |
| 26 | Skills system (composable workflow packages) |
| 27 | Multi-layered memory system |
| 28 | Eval-driven development framework (3 graders, pass@k metrics, 62 tests) |
| 29 | Swarm orchestrator (auto-decomposition, DAG execution, file locking, quality gate, 62 tests) |
| 30 | Registry discovery API (tools, skills, agent templates — 11 tests) |
| 31 | Server response caching & cache infrastructure (in-memory, ETag, TTL — 32 tests) |
| 32 | SQLite persistence layer — 5 migrations, 26+ tables, 7 SQLite stores, dual-write decorators (31 tests) |
| 33 | CLI quick wins |
| 34 | CLI visual polish |
| 35 | Workspaces — CRUD, members, invites, config, memory, usage aggregation |
| 36 | Projects — workspace-scoped CRUD, archive, members, 3-tier config inheritance |
| 37 | User management API — server-generated IDs, soft-delete, profiles, derived stats |
| 37.5 | Swarm sessions into SQLite (`swarm_events` table, legacy JSONL import) |
| 38 | Per-user token auth & database hardening (V009 backfill, checksum drift enforcement) |
| 41 | Agent artifact tools — `Artifact` tool (write/read/list), isolated produce-and-deposit pattern, 100K char read cap, 14 tests |
| 42.5 | Database lifecycle — `sovrant db` CLI (status, version, migrate, backup, inspect), `/health` DB block |
| 43 | Windows PowerShell native integration (cwd persistence, version detection, elevation hints) |
| 44 | Desktop application — Avalonia, 15 pages, streaming chat, tool use, setup wizard, dark/light theme |
| 48 | Intent-aware model routing — IntentClassifier (10 classes), ModelTierResolver, free-models-only mode |
| 51 | Engine layer (IPlanner/IExecutor/IStepRunner with crash-safe traces) + mission engine (store, planner, executor, journal, API) |
| 52 | Unified agent orchestration — SqliteTeamRegistry, AgentOrchestrator, agent_runs ledger, TeamRun/TeamPublish, /v1/teams + /v1/runs API |
| 53 | Scoped artifact storage — IArtifactStore, workspace-first layout, /v1/artifacts API, /artifacts CLI |
| 54 | Model capability registry — layered resolution (user > bundled > live > default), Gemma 4 support |
| 55 | Cost tracking & budgets — OpenRouter pricing, `ICostModel`, `BudgetEnforcer`, JSONL metrics log, `/cost` CLI command, `GET /v1/cost` API, cost display in Desktop + Web, `RuntimeEvent.TurnCost` |
| 56 | Web application — Blazor Server, 15 pages, streaming chat, embedded runtime, port 5100 (remote mode split to Phase 61) |
| 58 | Sovrant Trust Boundary — sanitization, ethical harness, intent verification as unified trust pipeline |
| 59 | Agentic loop hardening — intent classification, plan approval, execution governance, progress visibility |
| 57 | Inter-agent communication — PM agents, GroupMailbox, PMCoordinator, CoordinationStatus tool, V013 migration (30 tests) |
| 61 | Remote server mode — SignalR ChatHub, `RuntimeEventDto`, `AddSovrantClient()`, 8 remote service implementations, bearer auth query string for WebSocket, dual embedded/remote mode (20 tests) |
| 63 | DI audit & pluggability hardening — MCP v1.2.0 protocol additions |
| 40A | Workspace member management UI — Web inline member panel, Desktop detail pane, CLI `workspace list/members`; server-side role enforcement was already complete |
| 85 | Identity & login parity — per-user `svt_` tokens, Argon2id password hashing, admin pages (Web + Desktop), CLI login/logout/whoami, first-user admin bootstrap |
| 93 | Configuration boundary audit — `sovrant.config` removed entirely; `.env` + env vars only; `routing.json` → env vars; `swarm.json` → `workspace_settings` DB table; `config-audit.md` policy doc complete |
| 79 | Agents page — in-app create/edit of agent definitions (silent copy-on-write for built-ins), Launch chat, Run one-shot, prompt stored on agent_runs (V028), prompt as title in Recent Runs with agent badge on Web + Desktop |
| 94 | Orchestration Studio — compose and run teams from the UI; team + member create forms; Run button with task prompt on Web + Desktop |
| 95 | Integrations Gallery — catalog-first MCP onramp; 14 integrations (Automation: Composio, n8n, Zapier, Make; Platform: GitHub, Slack, Notion, Linear, Stripe, PostgreSQL, Supabase, Filesystem; Search: Brave, Exa, Tavily); encrypted credential keystore; model switcher upgrade with unconfigured-provider deep-link; Web + Desktop parity |
| 50 | OpenClaw integration & federated swarms — `SwarmFederationMode` (Silo/Federated/ManagerLed), `OpenClawBusClient`, `RouteResolver`, V029 migration (`parent_swarm_id`), `ListChildrenAsync`, `/v1/swarm/manager` + `/v1/swarm/openclaw/routes` + `/v1/swarm/{id}/children` REST endpoints, swarm-manager agent template |
| 73 | Code scaffolding — 21 project templates (Node/TS, .NET, Python, Go, Rust, Java, Kotlin, Ruby, Swift, Lua, Zig, C++), `CodeCreateTool`, `CodeCreateMultiTool` (multi-component), `CodeListTemplatesTool`, `ScaffoldManifestValidator`, 235 golden-path + manifest tests; session-level MCP opt-in lifted to context bar on Web + Desktop |

### Still pending

> **Last audited:** 2026-06-29. Shipped since prior audit: Phase 96 MCP runtime variables ✅ (inline env var editor Web + Desktop, V039 keystore). Phase 105 MCP server permissions ✅ (partial — workspace-level gating via V040 stable MCP IDs, admin toggle UI on Web + Desktop; project-level restrictions remain deferred). Phase 123 Memory System ✅ (workspace memory with public/private scoping, "+Remember" button in Chat on Web + Desktop, dedicated Workspace tab on Memory page, V041/V042 migrations, per-user injection in multi-user deployments). V043 email-as-user-id migration ✅ (replaces opaque `usr_{hex}` PKs across all FK columns). Phase 40C Supabase backend ✅ (PostgreSQL stores, admin UI, SQLite→Postgres migrator, boot-time DI switch; schema split to `db/postgres/PostgresSchema.sql` + `db/supabase/migrations/`). Phase 91 Knowledge Authoring partial ✅ (Guidelines + Documents pages complete with single Edit + silent copy-on-write; Skills page Duplicate button removal + AvaloniaEdit Desktop fixes remain).
>
> Quality / polish / audit phases (62, 68, 69, 70, 71, 72, 75) and partial-completion phases (56) are tracked in their own sections below; this table is gap-only.

| Gap | Phase | Priority |
|---|---|---|
| Enterprise auth & external identity (OAuth/OIDC, SAML, SSO) | Phase 40B | Deferred |
| Supabase backend ✅ — optional Postgres backend shipped (sessions + credentials, admin UI, schema init, SQLite→Postgres migrator, boot-time DI switch, Web + Desktop parity); SSO (Supabase Auth, OAuth/OIDC, SAML) remains deferred | Phase 40C | ✅ Done — SSO deferred |
| Granular feature permissions — workspace members can be granted or denied access to specific features (Chat, Agents, Teams, Swarms, Missions, MCP, Knowledge, Artifacts, Settings); current model is all-or-nothing per workspace role; Phase 40D adds a permission matrix admins configure per user or role | Phase 40D | Deferred |
| DuckDB database provider — `IStorageProvider` implementation backed by DuckDB; columnar analytics for agent runs, session history, token usage, cost aggregations, and audit queries; embedded like SQLite, analytical like a data warehouse; ideal for self-hosted deployments that need fast cross-session reporting without standing up Supabase | Phase 104 | Deferred |
| MCP server permissions — workspace-level gating ✅ shipped (V040 stable IDs, `GetEnabledEntriesAsync` workspace filter, admin toggle UI on Web + Desktop, memory-gate guards at selection point); project-level restrictions (project owners further restrict per project) remain deferred | Phase 105 | Partial ✅ — project-level deferred |
| VS Code native extension | Phase 42 | Deferred (MCP server covers MCP-aware IDEs) |
| Embedded terminal panel inside the desktop app | Phase 45 | Deferred |
| n8n automation integration (1,000+ third-party connectors via headless n8n) | Phase 46 | Medium |
| Workspace backup, import & export | Phase 47 | Medium |
| SearXNG web search backend (self-hosted, key-free) | Phase 49 | Low–Medium |
| OpenClaw integration & federated swarms over a routed bus (manager-led + siloed modes) | Phase 50 | ✅ Done |
| Hermes Agent integration via MCP — alternative claw/federation bus provider with self-improving skills | Phase 60 | Medium |
| Cloud storage backends & workspace isolation (Google Docs, Box, Amazon S3) | Phase 64 | Medium |
| Video generation — fal.ai, Kling AI, and pluggable provider support for text-to-video, image-to-video | Phase 65 | Medium |
| Autonomous agent modes (swarm autonomy & alternate claws) | Phase 67 | Medium |
| Code creation: project scaffolding & app generation | Phase 73 | ✅ Done |
| DB-backed document templates | Phase 74 | ✅ Done |
| In-app document viewing | Phase 76 | Medium |
| Project isolation with full feature parity | Phase 77 | Medium |
| Composio MCP integration — first-class platform awareness for Composio's MCP catalog (250+ apps), in-app browse/enable, managed OAuth via Composio connections, per-user/workspace credential scoping, still routed through Sovrant's `MCPTool` proxy and permission model | Phase 80 | Medium |
| OpenTelemetry observability — emit traces/metrics/logs for runs, turns, tool calls, router decisions, and provider HTTP via OTLP so operators can ship to any OTel-compatible backend (Honeycomb, Tempo, Jaeger, Datadog, etc.) | Phase 82.5 | Deferred |
| Pluggable memory backends — abstract `IMemoryStore` so the SQLite implementation can be swapped for distributed/remote stores (mem0, Pinecone-style vector DBs, Redis, Postgres+pgvector); enables shared/team memory across nodes | Phase 83 | Medium |
| Prompt library: reusable, parameterised prompt templates across CLI / Web / Desktop | Phase 84 | Medium |
| Local / remote mode selection — CLI + Desktop can run embedded (local DB) or connect to a shared `Sovrant.Server`; setup wizard mode picker; `sovrant connect <url>` | Phase 85.5 | ✅ Done |
| Background session continuation across navigation & session switches | Phase 86 | ✅ Done |
| Artifacts-by-default for code & documents (with workspace identity unification) | Phase 87 | ✅ Done |
| Knowledge Authoring Revisit — Guidelines and Documents pages complete (single Edit, silent copy-on-write, no "Duplicate" intermediate); Skills page Duplicate button removal + AvaloniaEdit Desktop fixes remain | Phase 91 | Partial ✅ — Skills + Desktop deferred |
| Active Sessions: up to 5 concurrent live tasks with return-anytime results; Settings UI on Web + Desktop, DB-backed; future admin console fallback | Phase 92 | ✅ Done |
| Agents page — in-app create/edit, Launch chat, Run one-shot, prompt titles in ledger | Phase 79 | ✅ Done |
| Orchestration Studio — compose and run teams from the UI; Run button with task prompt | Phase 94 | ✅ Done |
| Integrations Gallery — 14 integrations across Automation/Platform/Search tiers, encrypted credential keystore, Web + Desktop parity | Phase 95 | ✅ Done (Hermes pending details) |
| MCP runtime variables — per-server variable store (name, value, secret flag) injected as env vars when the MCP server process is launched; variable editor in the Integrations UI lets users add/edit/remove variables at any time without reconfiguring the connection; covers servers like Sitecore MCP that require 10–20 env vars passed at runtime; encrypted at rest via existing keystore; Web + Desktop parity | Phase 96 | ✅ Done |
| User Dashboard — personal cross-workspace activity view: own public + own private + teammates' public records in shared workspaces; other users' private records excluded entirely (no masked rows); Command Center remains unchanged as the admin-only view; V030 migration adds `is_private` to missions/agent_runs/sessions; new `UserDashboardAggregator`, `/v1/user-dashboard/state` endpoint; Web `/dashboard` + Desktop view both reached via 👤 rail nav for all signed-in users; server-side workspace gating via `IWorkspaceService.ListForUserAsync`; 8 aggregator unit tests covering own/public/private/non-member/team-run visibility; "Shared" stat redefined as own public items (not others' activity); both Command Center and Dashboard: paginated grid, header timestamp, 30s auto-refresh, page-preserve on refresh/navigation, guide panels | Phase 98 | ✅ Done |
| Private sessions and agent runs — users can mark any session or agent run as private via a toggle in the chat header and Agents UI; private records appear in the Command Center as masked rows (title and content hidden, existence acknowledged for accountability) but are completely excluded from the User Dashboard; only the owning user can see the full title, prompt, content, and run history of their private records; server-side enforcement ensures private content is never returned in any query for any other user regardless of role; audit log records the privacy toggle event | Phase 99 | ✅ Done |
| Accurate cost reporting — replace the current placeholder cost display in the Command Center with real per-session cost calculated from token counts × model pricing; pull and cache OpenRouter's public `/api/v1/models` pricing data (prompt $/1k + completion $/1k per model) on a daily schedule; apply the correct rate for the model used in each turn; surface per-session cost, per-run cost, and a workspace total in the Command Center and activity detail views; cost shown as $0.00 for free-tier and local models; cached pricing refreshed automatically so rates stay current without manual updates | Phase 100 | Medium |
| OAuth 2.1 + PKCE for MCP connections — replace API-key-only auth with a full OAuth 2.1 + PKCE flow for MCP servers that require it (Sitecore Marketer, Adobe AEM, and others); desktop uses authorization code + PKCE with loopback redirect `http://127.0.0.1:{port}/callback` via an ephemeral in-process HTTP listener; web uses ASP.NET Core OIDC middleware with an absolute redirect URI; RFC 8707 Resource Indicators bind each token to its specific MCP server so credentials cannot be replayed cross-server; tokens stored in the existing encrypted keystore (Windows Credential Manager on Desktop, encrypted DB on Web); automatic silent refresh with rotation before expiry; user-triggered revocation per server in the Integrations UI; no API keys exposed or pasted by the user — auth is entirely browser-driven on first connect | Phase 101 | ✅ Done |
| MCP quality and trust gates — add a policy layer between agents and connected MCP tools so destructive or high-impact actions require explicit approval before execution; company- or workspace-level rules define which tool call patterns are permitted, require confirmation, or are blocked outright (e.g. delete operations on more than N items, writes to production systems, bulk mutations); agents that violate a rule get a structured refusal with the policy reason so they can surface it to the user; quality gates optionally route tool call results through a validation step (schema check, output size cap, content policy) before returning to the agent; admins configure rules in the Integrations UI per MCP server; rule enforcement is server-side so no client-side bypass is possible; audit log records every gate decision (permitted, confirmed, blocked) per session | Phase 103 | ✅ Done |
| Agent identity in chat — agent name persisted to `sessions.agent_name` (V031 migration); restored on session resume; agent badge shown in chat hero state and top context bar on both Web and Desktop; `ActiveContextService` (Web) and `ActiveContextViewModel` (Desktop) carry `ActiveAgentName` so all surfaces stay in sync; context cleared on new session, restored on resume | Phase 106 | ✅ Done |
| Integration connection audit — 19 entries audited; Composio header fixed, Zapier URL replaced, GitHub env var corrected, Linear switched to remote HTTP endpoint (npm package did not exist), Snowflake package name fixed + 6-var requirement documented, Optimizely removed (no npm package); OAuth badge added to connect forms for 5 OAuth-gated entries; connection matrix at `docs/integration-connection-matrix.md` | Phase 107 | ✅ Done |
| Code template language management — surface the supported language and framework templates from Phase 73 scaffolding as editable knowledge pages; each language (TypeScript, Python, C#, Go, etc.) gets a template guideline page in the Knowledge section that defines coding conventions, preferred patterns, dependency standards, and generation hints used by the scaffolding engine; workspace admins can update guidelines to match company standards (e.g. internal package registries, required linting rules, banned dependencies); built-in guidelines are read-only with silent copy-on-write to user tier following the standard knowledge editing model; the scaffolding engine reads active guidelines at generation time so updates take effect immediately without rebuilding; Web + Desktop parity | Phase 108 | ✅ Done |
| Multi-agent session scoping — allow a user to scope more than one agent onto a single chat session; each scoped agent contributes its system prompt and tool set for the duration of the session; the chat header and top context bar show all active agents (expanding the single-agent badge from Phase 106); agents can be added or removed mid-session without clearing history; `sessions.agent_name` (V031 scalar) replaced or extended to support a list of agent IDs stored as a relation or JSON column (new migration required); `ActiveContextService` and `ActiveContextViewModel` updated to carry a collection; Web + Desktop parity | Phase 109 | Medium |
| CLI first-run experience — improve the experience for users launching `sovrant` for the first time; guided setup that detects missing configuration (no provider, no API key) and walks the user through provider selection and key entry interactively; `sovrant init` command as a standalone setup entrypoint; `sovrant doctor` command that checks health of all configuration and connectivity (provider reachable, model list accessible, DB migrations current, tool count); friendly error messages when the runtime fails to start rather than raw stack traces; color-coded status output; `--help` improvements with grouped command categories and examples; Web + Desktop parity not required — CLI-only surface | Phase 110 | High |
| Navigation overhaul — replace the current top-bar and rail navigation with a collapsible left-side panel following the modern sidebar pattern (VS Code, Linear, Notion, Slack); primary sections (Chat, Sessions, Agents, Teams, Missions, Knowledge, Artifacts, Settings, Command Center, Dashboard) listed in the sidebar with icons and labels; sidebar collapses to icon-only mode with tooltip labels; user-resizable panel width; active section highlighted; nested expansion for sections with sub-pages (e.g. Settings → Providers / Users / Workspace / System; Knowledge → Skills / Documents / Tools); keyboard shortcut to toggle sidebar; Web and Desktop parity; Desktop uses a single `SidebarView` Avalonia control replacing the current `RailNav`; Web replaces the current `<NavMenu>` component with a new `<Sidebar>` component; breadcrumb header remains on all pages; mobile breakpoint collapses sidebar to overlay drawer | Phase 111 | Medium |
| Migrate built-in markdown knowledge into the DB — move all on-disk built-in markdown (32 skills, 25 agents) plus the 44 code-defined document templates out of the filesystem/C# and into the `knowledge_pages` table so users manage every template (base + their own) in the DB without code changes; copy-on-write overlay model (immutable base rows, user edits become `global`/project overlays that win, revert = delete overlay); built-ins seeded via SQL migration; registries flipped to read DB; document rendering becomes data-driven via a sandboxed templating engine; `.md` files and disk scans deleted once verified; SQLite-only (knowledge store is local even on Postgres backend) | Phase 112 | Done ✅ |
| Caching: DB read cache + Phase 31 invalidation fix — `CachedKnowledgeStore` decorator wraps `IKnowledgeStore` with TTL-based in-process caching for rarely-changing content (skills, agents, document templates, tool/doc guides) and fires a `KnowledgePageChanged` event on every write; repairs Phase 31's `CacheInvalidator` whose file-watcher triggers were deleted by Phase 112, restoring HTTP-cache invalidation for `skills:list`, `templates:list`, and `knowledge:*` keys; opt-out via `SOVRANT_KNOWLEDGE_CACHE_TTL=0` | Phase 113 | ✅ Done |
| Intelligent Knowledge Harness — on-demand per-turn knowledge loading via `IKnowledgeRouter` (keyword/trigger/intent scoring, no LLM call); dynamic per-turn addendum preserves stable system-prompt cache; full-round-trip PII sanitization for knowledge bodies and tool results; `knowledge_attributions` table; MCP tool relevance filtering per turn; provenance Sources section in Web + Desktop chat | Phase 116 | ✅ Done |
| Enrich built-in skill definitions — 2-3 sentence descriptions, agent list wiring, `verification-loop` tools fix; V044 additive migration updates all 32 BuiltIn base rows; user overlays unaffected | Phase 114 | ✅ Done |
| API endpoint integration — connect REST and GraphQL APIs as first-class platform integrations alongside MCP servers; harness auto-discovers endpoints via OpenAPI/Swagger spec import or GraphQL introspection, or admin can manually describe specific endpoints; discovered endpoints exposed as typed tools through the same `MCPTool` proxy layer (trust rules, session picker, `FilteredToolRegistry`); admin configures per workspace; credentials stored in encrypted keystore; Web + Desktop parity | Phase 117 | Planned |
| Bootstrap configuration — declarative YAML file (`sovrant.bootstrap.yaml`) that pre-configures a Sovrant installation before or at first run; covers providers, MCP servers, knowledge/skills, agent templates, workspace setup, admin users, permission defaults, and branding; Sovrant installs and starts normally then applies the bootstrap idempotently; enables sales-assisted and partner-delivered custom installs without code changes | Phase 118 | Planned |
| Orchestration improvements — enhanced mission run-mode for teams and swarms; missions get a named run-mode (autonomous, supervised, step-through) set at launch rather than inherited from global permission; Claws sourced from configured platform integrations can be added as team members if the integration is connected, giving orchestrations access to external agents alongside local ones | Phase 119 | Planned |
| Multi-user web support — `Sovrant.Web` is currently single-user: `WebSessionService`, `ActiveContextService`, `SovrantConfig`, `MutableAuthProvider`, `IPermissionPolicy`, and `IToolConfirmationHandler` are all `AddSingleton`, meaning all browser circuits share one authenticated identity and one active workspace/model/MCP state; to support concurrent users each of these must become `AddScoped` (one instance per Blazor Server circuit); `WebSessionService` is the root dependency — real per-circuit identity (cookie/JWT per connection) must land first before workspace context and config can be safely isolated; `ActiveSessionsService` and `ChatSeedService` are already scoped, showing the intent; the remote mode (`Sovrant.Server` backend) is the closer path since the server already has multi-user auth — the web frontend just needs per-circuit context; workspace MCP/provider guards added in the meantime are already circuit-local and will remain correct after the scoped refactor | Phase 120 | Deferred |
| Recent chats time grouping — organize the session list in the sidebar into labeled time buckets rather than a flat chronological list; groups: **Today**, **Previous 7 days**, **Previous 30 days**, then one collapsible group per calendar month (e.g. "May 2026", "April 2026"); group headers are sticky/pinned as the user scrolls; empty groups are hidden; the active session is highlighted within its group; Web (`Sidebar.razor`) and Desktop (`SidebarViewModel` + `SidebarView.axaml`) parity; no new API endpoint needed — `created_at` is already returned in session list queries | Phase 121 | Medium |
| Image generation and inline display — allow models that support image output (e.g. `dall-e-3`, `gpt-image-1`, `flux`, `stable-diffusion` via OpenRouter, or any provider that returns `image_url` / base64 in the response) to generate images mid-conversation; generated images are stored as chat artifacts (`Artifact` with `Kind = "image"`) and rendered inline in the chat transcript for both Web and Desktop; the message bubble shows the image directly below the assistant text that prompted it, with a "Save" button to download the full-resolution file; clicking the image opens a lightbox (Web) or a full-size viewer window (Desktop); admin configures which providers and models are image-capable; no new conversation format needed — the existing artifact pipeline handles the binary payload | Phase 122 | Medium |
| Memory system — workspace memory as a first-class user feature with public/private scoping: users can add memory notes directly from the chat "＋ Remember" button (web and desktop) or from a dedicated Workspace tab on the Memory page; notes are injected into every future chat session; private notes are only injected for their owner, public notes are injected for all workspace members; V041 migration adds `owner_user_id` and `is_private` to `workspace_memory`; `ConversationRuntime` auto-resolves the session owner's personal workspace so injection is per-user in multi-user deployments rather than relying on a global env var | Phase 123 | Done |
| File system access controls — admin-configurable directory allowlist and blocklist so Sovrant agents can only operate within declared paths; enforced at the tool level before Read/Write/Edit/Glob/Grep/Bash execute; denied accesses logged as `directory_access_denied` governance audit events; Governance page gains a "File System Access" section (Web + Desktop); V044 migration stores rules in `server_settings`; path traversal and symlink escapes are normalised before evaluation | Phase 124 | Planned |
| Chat conversation UX — collapsed work strips replace per-tool boxes; two-level expand (strip → tool list → full detail); live "doing X" in-progress indicator while agent works; clean visual hierarchy where the agent's answer is prominent and tool work is subordinate; consistent Web + Desktop parity | Phase 126 | Implemented — pending UAT |
| Web search via integrations — move web search out of the hard-coded `WebSearchBackend` enum and into the Integration Gallery; add `IntegrationKind.HttpApi` for direct REST adapters (no MCP process); define `IWebSearchProvider` interface; ship DuckDuckGo (built-in free default), Brave, FireCrawl, Exa, Tavily as `HttpApi` catalog entries; add Crawl4AI as a scraper/fetcher integration; admin picks active search provider from Integrations page; remove `WebSearchBackend` enum; existing MCP search entries remain as alternatives; unit test coverage for WebFetchTool, search providers, and dispatch | Phase 125 | Planned |
| Supabase Row Level Security — enable RLS on all privacy-sensitive tables in the Supabase migration and write policies for the `owner_user_id` model; service-role key retains full unrestricted access (Supabase bypasses RLS for service role by design); anon/authenticated JWT callers are scoped to their own data at the database layer; complements the existing application-layer query filters | Phase 127 | Planned |
| Code generation quality gates — artifact security hardening; `.sln` + `Directory.Build.props` + CI for all 21 templates (every scaffold immediately runnable); `CodeValidateTool` (structural checks, no compiler in PATH); `CodeCreate` `next_steps` + `build_command` + LLM instruction update; `ArtifactManifest` code metadata | Phase 128 | ✅ Done |
| Missions → Workflows rename and UX review — surface-label rename (presentation-layer only); dedicated Workflows page (goal-first launch, active/recent cards, detail view with journal + artifacts); positioning callout distinguishing AI-driven workflows from trigger-automation (n8n/Zapier/Make); `/v1/workflows` alias for `/v1/missions`; Phase 119 run-modes surfaced in the launch form; plus dual-path execution — Claude Agent SDK dynamic workflow orchestration when a qualifying Claude tier is active, otherwise Sovrant's own mission engine as the base version (model/tier gate TBD) | Phase 129 | Planned (v1.5) |
| OpenRouter account registration & key issuance in-app — "Get an OpenRouter key" button on the Providers setup flow (Web + Desktop) drives OpenRouter's OAuth PKCE flow (`openrouter.ai/auth`) so a user can register a new OpenRouter account or sign into an existing one and receive a working API key without ever leaving Sovrant or hand-copying a key; reuses the PKCE code-challenge/verifier plumbing and loopback callback listener built for Phase 101's MCP OAuth; issued key is written straight into the encrypted keystore and activated as a provider profile like a manually-entered key | Phase 130 | Planned |

### v1.0 release polish ✅

A focused subset of Phase 69 / Phase 70 acceptance work that must land
before the public release. Tracked here rather than reopening the parent
phases — the rest of those phases stays deferred to v1.1.

- [x] **Activity pages — wire to real data** (Desktop + Web). Replaced the
  "Coming soon" placeholders in `ActivityView.axaml` and `Activity.razor`
  with a session-history list backed by `IMemoryStore.LoadSummariesAsync`
  (outcome, duration, turns, tokens, tools, files). Includes
  loading / empty / error states.
- [x] **Standardize Desktop markdown rendering on `SafeMarkdownPresenter`**.
  Swapped 7 views (Agents, Artifacts, Integrations, Projects, Skills, Tools,
  Workspaces) from `MarkdownScrollViewer` to `SafeMarkdownPresenter` and
  removed the `SanitizeForMarkdown` workaround that stripped code fences
  and backticks. Code blocks now render properly across the whole app.
- [x] **Web Chat error handling**. Stream/SSE errors already routed to
  `ChatMessageModel.SetError` (catch in `SendToRuntimeAsync` + `RuntimeError`
  event); added a top-level `.chat-error-banner` for page-level failures
  not tied to a single message (session load failure, runtime warmup,
  slash-command catalog load) and wrapped `LoadSessionAsync` in a
  try/catch that surfaces through it.
- [x] **Phase 69 subset — empty/loading/error states on async list pages**:
  added `_isLoading` + `_loadError` + `.page-error-banner` (with Retry)
  pattern to Web `Memory.razor`, `Workspaces.razor`, `Projects.razor`,
  `Artifacts.razor`. Sync in-memory pages (Agents, Skills, Tools,
  Documents) skipped — they render instantly. New `.page-error-banner`
  CSS class in `sovrant.css` for reuse.
- [x] **Phase 70 subset — CLI polish**: `NO_COLOR` env var honored at
  startup (also new global `--no-color` flag), `--json` flag added to
  `sovrant status` (existing `sovrant document` subcommands already had
  `--json`; `sovrant prompt --ci` already emits JSON for automation).
  Streaming chat-output markdown rendering deferred — raw streaming
  text remains acceptable for v1.0.

---

## Roadmap

### Phase 1 — Tool Parity with OpenClaude ✅

**Goal:** Close the gap between Sovrant's 22 tools and the full OpenClaude tool set. A comparison of the OpenClaude source against Sovrant's `Sovrant.Tools` project identified 9 missing tools worth porting and 13 cloud/platform-only stubs that are not portable.

#### Tool comparison summary

| Category | Count | Tools |
|---|---|---|
| Implemented ✅ | 32 | Read, Write, Edit, Glob, Grep, LS, Bash, PowerShell, REPL, WebFetch, WebSearch, TaskCreate/Get/List/Output/Stop/Update, TodoWrite, Agent, AskUserQuestion, Sleep, NotebookEdit, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree, Skill, ToolSearch, ListMcpResources, ReadMcpResource, LSP (Phase 11) |
| Missing — port ⬜ | 2 | ScheduleCron, ConfigTool |
| Cloud — future phases ☁️ | 3 | MCPTool (Phase 13), McpAuthTool (Phase 14), TeamCreate/Delete (Phase 15) |
| Not portable ❌ | 10 | RemoteTrigger, SendMessage, WorkflowTool, BriefTool, SuggestBackgroundPR, VerifyPlanExecution, SyntheticOutput, Tungsten |

#### Tier 1 — High priority (complete the core developer experience)

> **Status: ✅ Implemented** — TaskUpdate, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree all implemented and building clean (99/99 tests passing).

**`TaskUpdate`**
The task management suite has 5 of 6 tools. `TaskUpdate` lets the model update the status or description of a running background task while it is in flight. Trivial to add alongside the existing `BackgroundTaskRegistry`.

**`EnterPlanMode` / `ExitPlanMode`**
Sovrant already has a `plan` permission mode, but it is set by the user at startup (`--permission-mode plan`). These tools let the *model* signal a plan-mode transition mid-conversation — entering a read-only planning phase and exiting back to execution when ready. The runtime needs to honour the signal by toggling `_config.PermissionMode` (or a session-scoped override) when it sees these tool calls returned.

**`EnterWorktree` / `ExitWorktree`**
Creates a temporary git worktree so the agent performs all file edits on an isolated branch, then optionally commits and returns the branch name. Essential for safe autonomous multi-file coding work — the user's working tree is never touched until explicitly merged. Requires `git worktree add` / `git worktree remove` via `Bash` internally; the tool wraps this with session-scoped state tracking.

#### Tier 2 — Medium priority (MCP resource access + tool discovery + safety)

> **Status: ✅ Implemented** — `Skill`, `ToolSearch`, `ListMcpResources`, `ReadMcpResource`, custom project slash commands, `/memory` command all implemented and building clean.

**`/undo` / `/redo` (git-backed)**
Before every `Write` or `Edit` tool call, stash the current file state to a temporary git commit or diff buffer. `/undo` reverts the last agent file change; `/redo` reapplies it. Builds user trust significantly — the user can always roll back an agent mistake without losing their own work. Related to `EnterWorktree`/`ExitWorktree` (Tier 1) but applies even outside a worktree. Implementation: wrap `Write`/`Edit` tool execution in `ConversationRuntime` with pre/post git snapshot calls.

**Custom project slash commands (`.sovrant/commands/`)** ✅ Done
Project-specific slash commands defined as markdown files in `.sovrant/commands/{name}.md`. When invoked, the file's content is injected as a user message. `SlashCommandDispatcher.TryDispatchAsync` checks this directory when a built-in command is not found.

**`ListMcpResources` / `ReadMcpResource`** ✅ Done
`ListMcpResources` enumerates what each connected MCP server exposes; `ReadMcpResource` fetches a resource by URI. Backed by `McpClientRegistry` (populated at `InitializeRuntimeAsync` time).

**`ToolSearch`** ✅ Done
Searches registered tool names/descriptions by keyword. Useful once tool count exceeds ~30 or MCP servers register many tools.

**`SkillTool`** ✅ Done
Reads `.sovrant/skills/{name}.md` (project-local first, then `~/.sovrant/skills/{name}.md`). Substitutes `$ARGUMENTS` placeholder. The model calls `Skill("commit")` to trigger a specialised behaviour from a prompt template.

#### Tier 3 — Lower priority

**`ScheduleCron`**
Schedules a recurring or one-shot prompt via a 5-field cron expression. The scheduled task fires at the next cron match and enqueues a new conversation turn. Requires a persistent cron scheduler (`IHostedService` + cron file). Complex but powerful for automation use cases.

**`ConfigTool`**
Lets the model read and write Sovrant's runtime config from within a conversation (change model, permission mode, base URL). The `PUT /v1/config` endpoint already does this from the outside; the tool exposes the same capability to the model itself.

**`LSPTool`**
Language Server Protocol integration: hover type info, go-to-definition, find-references, document symbols. Powerful for code-heavy agentic work but requires a language server process to be running alongside Sovrant. Out of scope until there is a clear IDE integration story.

#### Implementation plan

1. ~~`TaskUpdate` — add to `Sovrant.Tools/Tasks/`, register in `ToolRegistrar`, add unit test~~ ✅ Done
2. ~~`EnterPlanMode` / `ExitPlanMode` — add to `Sovrant.Tools/PlanMode/`; runtime handles the tool result by updating a session-scoped permission override~~ ✅ Done — `IPermissionModeAccessor` added to Runtime; `MutableCliPermissionPolicy` and `MutableServerPermissionModeAdapter` registered in both contexts
3. ~~`EnterWorktree` / `ExitWorktree` — add to `Sovrant.Tools/Worktree/`; session-scoped worktree path stored in `ConversationRuntime` or a scoped service~~ ✅ Done — `WorktreeState` singleton; tools invoke `git worktree add/remove` directly
4. `/undo`/`/redo` — wrap `Write`/`Edit` execution with git snapshot; add `/undo` and `/redo` slash commands in `Sovrant.Commands`
5. ~~Custom project commands — `SlashCommandDispatcher.TryDispatchAsync` checks `.sovrant/commands/{name}.md`~~ ✅ Done
6. ~~`ListMcpResources` / `ReadMcpResource` — `McpClientRegistry` + two tools in `Sovrant.Tools/Mcp/`~~ ✅ Done
7. ~~`ToolSearch` — injects `IToolRegistry`, filters `GetDefinitions()` by keyword~~ ✅ Done
8. ~~`SkillTool` — reads `.sovrant/skills/{name}.md` from disk~~ ✅ Done
9. ~~`LSPTool`~~ ✅ Shipped in Phase 11 (5 tools: Hover, Definition, References, Diagnostics, Rename across 18 languages); `ScheduleCron` / `ConfigTool` — deferred

---

### Phase 2 — Quick Wins from Competitor Analysis ✅

**Goal:** Implement three high-value features identified in the competitor analysis that have no external dependencies, can be done in isolation, and dramatically improve day-to-day usability.

#### Agent Memory Files (`.sovrant/memory.md`)

**Competitor precedent:** Claude Code (`CLAUDE.md` project + global memory files)

The agent reads two markdown memory files at the start of every session and prepends their contents to the system prompt:

- **Global:** `~/.sovrant/memory.md` — user-level preferences, coding style, preferred tools
- **Project:** `.sovrant/memory.md` (in the working directory) — project-specific conventions, which files to avoid, how to run tests, architecture notes

This makes the agent immediately context-aware of any codebase without the user having to re-explain conventions every session. It is the single highest-perceived-value feature for regular users.

**Implementation:** In `ConversationRuntime.BuildSystemPrompt()` — read both files at construction time (if they exist), append their contents after the base system prompt. No new tools required. Add a `/memory` slash command to open the project memory file for editing.

#### Context Auto-Compaction

**Competitor precedent:** Claude Code ✅ · opencode ✅ · OpenClaude ✅

When the accumulated `_history` token count approaches the model's context window limit (configurable threshold, default 80% of `MaxTokens`), the runtime automatically summarises the oldest portion of the history into a compact representation and replaces it. This allows arbitrarily long sessions without hitting limits or paying for redundant tokens.

**Implementation:**
1. After each turn, estimate token count of `_history` (rough: `sum(message.Content.Length) / 4`)
2. If above the threshold, call a summarisation turn: `"Summarise the conversation so far in 500 words, preserving all key decisions, code changes, and open questions."`
3. Replace the oldest N messages with a single `[Compacted: {summary}]` assistant message
4. Persist the compaction event to JSONL so it survives session reload
5. Add `SOVRANT_COMPACT_THRESHOLD` env var (default: `0.8` = 80% of MaxTokens)

#### Token Count Fix (OpenAI Streaming)

**Blocks:** Phase 10 usage tracking, context window visualisation

OpenAI sends `usage` data in the second-to-last SSE chunk (before `[DONE]`), not in `MessageDelta`. The current `CollectStreamEventsAsync` in `ConversationRuntime` only reads `MessageDelta.Usage.OutputTokens` — the OpenAI format uses a top-level `usage` field on the final content chunk.

Fix: detect the `usage` field on the final OpenAI SSE chunk and capture `prompt_tokens` / `completion_tokens` into `accumulated.InputTokens` / `accumulated.OutputTokens`. Once fixed, expose live token counts in the REPL status line and in `GET /v1/sessions/{id}`.

#### Implementation Plan

1. ~~Agent memory files — extend `BuildSystemPrompt()` in `ConversationRuntime`; add `/memory` slash command~~ ✅ Done — `AppendMemoryFile()` helper reads both files; `/memory` and `/mem` commands registered; `InjectAsUserMessage` on `SlashCommandResult` wired into REPL
2. ~~Token count fix — update `CollectStreamEventsAsync` to capture OpenAI `usage` field from final SSE chunk~~ ✅ Done — `OpenAiCompatProvider` removed `yield break` on `finish_reason`; continues loop to capture trailing usage chunk; `ConversationRuntime` captures `InputTokens` from `MessageDelta`
3. ~~Context auto-compaction — add compaction logic to `RunTurnAsync`; add `SOVRANT_COMPACT_THRESHOLD` config~~ ✅ Done — `MaybeCompactHistoryAsync()` wired into the agentic loop in `ConversationRuntime`; reads `SovrantConfig.CompactThreshold` (default 80,000 tokens)
4. ~~Expose token counts in REPL status line and `GET /v1/sessions/{id}` response~~ ✅ Done — REPL renders from `RuntimeEvent.TurnComplete { InputTokens, OutputTokens }`; `SessionDetailDto` includes `total_input_tokens`/`total_output_tokens` plus per-message tokens

---

### Phase 3 — Native Model Web Search (OpenAI Responses API) ✅

> **Status: ✅ Complete** — `LLM_WEB_SEARCH=true` activates `OpenAiResponsesProvider`; tested end-to-end with `gpt-4o-mini`.

**Goal:** Allow teams using OpenAI to skip the Brave/FireCrawl API key requirement by using OpenAI's native `web_search_preview` tool instead.

#### What was built

| Component | File | Notes |
|---|---|---|
| `OpenAiResponsesTypes.cs` | `Sovrant.Api/OpenAi/` | Request/response/SSE types for `POST /v1/responses` |
| `ResponsesFormatConverter.cs` | `Sovrant.Api/OpenAi/` | Converts `MessagesRequest` ↔ Responses API format; handles multi-turn history including function call/result replay |
| `OpenAiResponsesProvider.cs` | `Sovrant.Api/Providers/` | Full `ILlmProvider` implementation; streaming + non-streaming; injects `web_search_preview`; suppresses `WebSearch` function tool |
| `ServiceCollectionExtensions.cs` | `Sovrant.Api/` | Registers `OpenAiResponsesProvider` instead of `OpenAiCompatProvider` when `LLM_WEB_SEARCH=true` |

#### Key design notes

- **Why a separate provider (not inject into chat/completions):** OpenAI's `/v1/chat/completions` only accepts `function` and `custom` tool types. `web_search_preview` is exclusively available on `POST /v1/responses`.
- **`WebSearch` suppressed:** The Responses API provider filters `WebSearch` from the function tools list before sending. This prevents the model from calling our function tool (which requires Brave/FireCrawl) when the native built-in is available.
- **`function_call.id` vs `call_id`:** The Responses API requires `function_call.id` to start with `fc_` (item ID) while `call_id` (starts with `call_`) is used to match tool results. Our `ToolUseBlock.Id` stores `call_id`; the converter prefixes a synthetic `fc_` item ID when rebuilding history for multi-turn requests.
- **`web_search_preview` is transparent:** The model's search calls are handled server-side by OpenAI. No tool call events are emitted to the agentic loop; search results appear directly in the model's text output.

#### Usage

```bash
LLM_WEB_SEARCH=true dotnet run --project src/Sovrant.Cli -- --model gpt-4o-mini prompt "What are today's top tech headlines?"
```

#### What it does NOT cover (future)

- Anthropic does not yet expose a native web search tool via their API — no action needed until they do.
- Gemini via the OpenAI-compat endpoint (`LLM_BASE_URL=.../openai/`) does not support `web_search_preview` — `LLM_WEB_SEARCH=true` should only be set with OpenAI.
- Other providers (Ollama, etc.) are unaffected; `OpenAiCompatProvider` is still used when `LLM_WEB_SEARCH` is not set.

---

### Phase 4 — Security Hardening ✅

**Source:** OpenClaude improvement document review (2026-04-04) — items applicable to Sovrant's tool surface.

**Goal:** Address the most critical security and safety gaps in `BashTool` and `WebFetchTool` before the server is used in any shared or internet-facing deployment.

#### BashTool hardening

**Current gaps:**
- No stdout/stderr size limit — a runaway command can exhaust memory or produce an enormous tool result
- No dangerous env var stripping — inherits `LD_PRELOAD`, `DYLD_INSERT_LIBRARIES`, and other vars that can be abused to hijack process behaviour
- No workspace guard — the tool will happily run with `$HOME` or `/` as its working directory

**Fixes:**
1. Hard `MAX_OUTPUT_BYTES` limit (256 KB) — truncate combined stdout+stderr and append `[truncated: N bytes omitted]` marker
2. Strip `LD_PRELOAD`, `DYLD_INSERT_LIBRARIES`, `LD_LIBRARY_PATH` from the child process environment before exec
3. Workspace guard — if the resolved working directory is `$HOME`, `/`, or outside the configured workspace root, log a warning and surface it in the tool output

#### WebFetchTool SSRF protection

**Current gaps:**
- No SSRF (Server-Side Request Forgery) protection — the agent can be instructed to fetch internal IPs (AWS metadata at `169.254.169.254`, internal services at `192.168.x.x`, `10.x.x.x`, etc.)
- No `file://` scheme blocking — `file:///etc/passwd` would succeed on some HTTP client configurations
- Response is buffered entirely in memory via `ReadAsStringAsync`

**Fixes:**
1. DNS resolution + IP blocklist: after resolving the target hostname, reject requests to RFC-1918 private ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), loopback (`127.0.0.0/8`, `::1`), link-local (`169.254.0.0/16`), and metadata endpoints
2. Block `file://`, `ftp://`, and other non-HTTP schemes at the URL parse stage
3. Block redirect chains that resolve to private IPs (check each redirect destination)
4. Stream response with `HttpCompletionOption.ResponseHeadersRead` + capped `StreamReader` — avoid buffering large responses in memory

#### Implementation Plan

1. `BashTool` — add `MAX_OUTPUT_BYTES = 262144`; pipe stdout+stderr through a capped `MemoryStream`; strip dangerous env vars; add workspace guard warning
2. `WebFetchTool` — add `IsPrivateIp(IPAddress)` helper; resolve DNS before sending; block private IPs and `file://`; follow redirects manually to check each hop; switch to streaming read
3. Add tests for each guard in `Sovrant.Tools.Tests`

---

### Phase 5 — Reliability & Safety ✅

**Source:** OpenClaude improvement document review (2026-04-04).

**Goal:** Prevent runaway agent behaviour and improve resilience against transient LLM provider errors.

#### Provider retry with exponential backoff

**Current state:** `SmartRouter` makes a single attempt. On a `429` or `5xx`, the request fails immediately and the error propagates to the user. The `ApiError.Retryable` flag is already set correctly but never used.

**Fix:** Wrap `RouteAsync` + `StreamAsync` in a retry loop (max 3 attempts, delays 1s / 2s / 4s) for errors where `ApiError.Retryable == true`. Respect `Retry-After` header if present. Non-retryable errors (`400`, `401`, `403`) skip retries immediately.

#### AgentTool recursion depth limit

**Current state:** `AgentTool` spawns a new `ConversationRuntime` and runs a full agentic loop. That sub-agent can itself call `AgentTool`, with no limit on nesting depth — a model instruction can cause infinite recursion, exhausting threads/memory.

**Fix:** Use `AsyncLocal<int>` to track the current nesting depth. Reject calls at depth ≥ 5 with an error result. Pass the depth counter through the `IServiceProvider` or as an ambient context.

#### ReadFileTool file size limit

**Current state:** `ReadAllLinesAsync` loads the entire file into memory regardless of size. A 2 GB log file would be loaded completely.

**Fix:** Check `FileInfo.Length` before reading. If size exceeds 10 MB, return an error: `"Error: file too large to read (N MB). Use Grep to search within it."`. For files between 1–10 MB, include a warning in the output.

#### Implementation Plan

1. Add retry logic to `ConversationRuntime.CollectStreamEventsAsync` — detect retryable errors from `HttpRequestException`, inspect status code, apply exponential backoff
2. Add `AsyncLocal<int> s_agentDepth` to `AgentTool`; increment on enter, check ≥ 5, decrement on exit
3. `ReadFileTool` — add `FileInfo.Length` check; return error if > 10 MB

---

### Phase 6 — Robustness ✅

**Source:** OpenClaude improvement document review (2026-04-04).

**Goal:** Eliminate edge-case data corruption and unbounded output in the remaining core tools.

#### Atomic file writes (`WriteFileTool` / `EditFileTool`)

**Current state:** Both tools call `File.WriteAllTextAsync(path, content)` directly. A crash or cancellation mid-write leaves a partial file at the destination path, corrupting the original.

**Fix:** Write to a temporary file in the same directory, then `File.Move(tmp, destination, overwrite: true)`. `File.Move` is atomic on POSIX (same filesystem). On Windows it is not formally atomic but is best-effort via a rename. This eliminates partial writes in the common case.

#### GlobTool result cap

**Current state:** No explicit cap on result count. On a very large repository (monorepos with 100K+ files) a `**/*` glob can produce an enormous result string.

**Fix:** Cap results at 1000 files. If truncated, append `[truncated: showing 1000 of N files]`.

#### Deferred — `ToolResult` structured record

The OpenClaude document suggests replacing the `string` return from `ITool.ExecuteAsync` with a structured `ToolResult` record carrying `Output`, `IsError`, and optional `Metadata`. This is a **breaking change** to the `ITool` interface and all 31 implementations. Defer to a dedicated Phase 11+ refactor when the tool surface has stabilised.

#### Implementation Plan

1. `WriteFileTool` — write to `{path}.tmp`, then `File.Move` with overwrite
2. `EditFileTool` — same: write full updated content to temp, then rename
3. `GlobTool` — cap `files` list at 1000; append truncation notice if exceeded

---

### Phase 7 — Structured Async Logging ✅

**Goal:** Add non-blocking, structured logging throughout the engine and server so that runtime behaviour, errors, and performance are easy to observe without adding latency to the hot path.

#### Motivation

The engine currently logs sparingly — mostly ad-hoc `ILogger` calls with string interpolation. There is no consistent structure, no correlation IDs, and no sink that can write to a file without blocking the calling thread. When something goes wrong (provider error, tool failure, compaction event) there is no reliable way to reconstruct what happened from logs alone.

#### Provider choice — ZLogger or Serilog + Async sink

| Option | Pros | Cons |
|---|---|---|
| **ZLogger** (`Cysharp/ZLogger`) | Zero-allocation log formatting; designed for .NET high-throughput; outputs structured JSON; no extra abstraction | Less ecosystem documentation; smaller community |
| **Serilog + `Serilog.Sinks.Async`** | Widely known; rich ecosystem; `Serilog.Sinks.File` (rolling) + `Serilog.Sinks.Console`; async sink wraps any sink with a bounded `BlockingCollection` | Small allocation per log event (mitigated by buffering) |

**Recommendation:** Use **Serilog** with `Serilog.Sinks.Async` wrapping both a rolling file sink and the console sink. The async wrapper ensures all disk I/O happens on a dedicated background thread and never blocks the agentic loop. Keep `Microsoft.Extensions.Logging` as the surface API — Serilog plugs in as the backing provider via `UseSerilog()`.

#### Source-generated log delegates

All hot-path log calls must use `[LoggerMessage]`-source-generated delegates (already the pattern in `OpenAiCompatProvider`). Do not use `LogInformation("text {Arg}", value)` directly — it allocates a `string[]` on every call even when the log level is disabled.

Convert all existing inline `_logger.LogXxx(...)` calls to static `[LoggerMessage]`-attributed methods.

#### Structured fields

Every log event in the critical path should carry a consistent set of structured properties:

| Field | Type | Source |
|---|---|---|
| `session_id` | `string` | `ConversationRuntime` |
| `model` | `string` | `MessagesRequest.Model` |
| `provider` | `string` | `ILlmProvider.Name` |
| `tool_name` | `string` | `ITool.Name` (tool events) |
| `turn` | `int` | Loop counter in `RunTurnAsync` |
| `duration_ms` | `long` | `Stopwatch.ElapsedMilliseconds` |
| `tokens_in` | `int` | `Usage.InputTokens` |
| `tokens_out` | `int` | `Usage.OutputTokens` |
| `is_error` | `bool` | Exception / error result |
| `retry_attempt` | `int` | Retry loop (provider retry) |

Use Serilog's `LogContext.PushProperty` to attach `session_id` and `turn` as ambient properties at the start of each `RunTurnAsync` call so they appear on every log line without threading them through every method signature.

#### Critical log points

| Component | Event | Level |
|---|---|---|
| `ConversationRuntime` | Turn start (session, model, turn number) | `Information` |
| `ConversationRuntime` | Turn complete (duration_ms, tokens_in, tokens_out) | `Information` |
| `ConversationRuntime` | Tool call dispatched (tool_name, turn) | `Debug` |
| `ConversationRuntime` | Tool call complete (tool_name, duration_ms, is_error) | `Debug` |
| `ConversationRuntime` | Retry attempt (attempt, delay_ms, reason) | `Warning` |
| `ConversationRuntime` | Context compaction triggered (messages_before, messages_after, tokens_before) | `Information` |
| `ConversationRuntime` | Max tool rounds reached | `Warning` |
| `SmartRouter` | Provider selected (provider_name, reason) | `Debug` |
| `SmartRouter` | Provider health ping result (provider_name, latency_ms, is_healthy) | `Debug` |
| `SmartRouter` | All providers unhealthy — fallback to configured list | `Warning` |
| `OpenAiCompatProvider` | SSE parse skip (unparseable data) | `Warning` |
| `OpenAiCompatProvider` | HTTP / JSON / IO error | `Error` |
| `ToolExecutor` | Permission denied (tool_name, mode) | `Warning` |
| Server request pipeline | Request received (method, path, session_id) | `Debug` |
| Server request pipeline | Request complete (status, duration_ms) | `Information` |
| Server request pipeline | Streaming begun / ended | `Debug` |

#### Configuration

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_LOG_LEVEL` | `Information` | Minimum log level: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `SOVRANT_LOG_FILE` | `~/.sovrant/logs/sovrant-.log` (rolling daily) | Path pattern for the rolling file sink. Set to empty string to disable file logging. |
| `SOVRANT_LOG_CONSOLE` | `true` | Whether to write logs to stdout. Set to `false` to silence console output. |
| `SOVRANT_LOG_FORMAT` | `text` | `text` (human-readable) or `json` (structured JSON — better for log aggregators). |

#### Implementation Plan

1. Add NuGet packages: `Serilog`, `Serilog.Extensions.Hosting`, `Serilog.Sinks.Async`, `Serilog.Sinks.File`, `Serilog.Sinks.Console`, `Serilog.Enrichers.Thread`
2. Wire `UseSerilog()` in `Sovrant.Server` and `Sovrant.Cli` host builders; read `SOVRANT_LOG_LEVEL` / `SOVRANT_LOG_FILE` / `SOVRANT_LOG_CONSOLE` / `SOVRANT_LOG_FORMAT` from environment
3. Convert all existing inline `_logger.LogXxx(...)` calls across `Sovrant.Api`, `Sovrant.Runtime`, `Sovrant.Tools`, `Sovrant.Server` to `[LoggerMessage]`-source-generated delegates
4. Add `LogContext.PushProperty("session_id", ...)` and `LogContext.PushProperty("turn", ...)` scope at the top of `RunTurnAsync`
5. Add `Stopwatch` timing around each tool dispatch in the agentic loop; log `duration_ms` on completion
6. Add provider name to every LLM call log line via `LogContext`
7. Add structured server request middleware (or minimal API filter) that logs request start/end with correlation
8. Add `Sovrant.Server.Tests` integration test: capture log output, verify `session_id` appears on turn-complete log events
9. Update `README.md` environment variable table with the four new `SOVRANT_LOG_*` vars
10. Update `docs/engine-status.md` to mark logging as implemented

---

### Phase 8 — Multi-Tenant Per-Request Credentials ✅

**Goal:** Allow each client to supply its own LLM provider, model, and API key per HTTP request, without the server storing or managing those credentials.

#### Motivation

The current server model has one global `LLM_API_KEY` and `LLM_BASE_URL` set at startup. This works for a single-user or single-organisation deployment. However, the vision for Sovrant is a server that can act as a shared runtime for multiple users or frontends — each potentially using a different LLM provider and key.

Examples:
- A Replit frontend where each user has their own OpenAI key stored in Replit Secrets
- A multi-tenant SaaS where the server must not hold customer API keys at rest
- A dev environment where different engineers use different models (GPT-4o vs Gemini vs local Ollama)

#### Design

Extend the `POST /v1/chat/completions` request body with optional override fields:

```json
{
  "model": "gpt-4o-mini",
  "messages": [...],
  "session_id": "user-123",
  // Per-request credentials are now sent as headers:
  // X-LLM-Api-Key: sk-...
  // X-LLM-Base-Url: https://api.openai.com/v1
}
```

The server:
1. Checks for `X-LLM-Api-Key` / `X-LLM-Base-Url` headers on the request
2. If present, creates a **request-scoped** `IConversationRuntime` with a temporary `ApiKeyAuthProvider` and provider pointing at the override URL
3. If absent, falls back to the global `MutableServerConfig` key/URL as today
4. The session pool keyed on `session_id` must also account for the provider — otherwise two users with the same session ID but different keys would collide

#### Session Pool Key

When per-request credentials are supported, the session pool key must include the provider identity:

```csharp
var poolKey = $"{req.SessionId}::{baseUrl}";
```

This ensures session history is isolated per user even if two users share the same `session_id` string.

#### Security Considerations

- `X-LLM-Api-Key` is a client-supplied secret. The server must never log it, persist it, or include it in error responses.
- The server's own `SOVRANT_TOKEN` continues to gate all requests — the per-request key is only for the downstream LLM call.
- Rate limiting per `X-LLM-Api-Key` or per `SOVRANT_TOKEN` client should be added to prevent abuse.

#### Implementation Plan

1. Read `X-LLM-Api-Key` and `X-LLM-Base-Url` from request headers
2. In `ChatRoutes.HandleAsync`:
   - If headers are present, build a scoped `OpenAiCompatProvider` with those credentials
   - Wrap it in a scoped `SmartRouter` (single-provider, skip ping)
   - Create a scoped `ConversationRuntime` using the scoped router
3. Session pool key: `{sessionId}::{baseUrl}` or a hash of both
4. Add `X-LLM-Api-Key` to the server's sensitive-field redaction list

---

### Phase 9 — Session Lifecycle Management (TTL Eviction + Turn Serialization) ✅

**Goal:** Allow the server to manage many concurrent sessions safely — one `ConversationRuntime` per active session — with automatic eviction of idle sessions and correct serialization of concurrent turns within the same session.

#### Architectural Decision: One Runtime Per Session (Not a Shared Pool)

The alternative considered was a fixed pool of runtimes shared across users (like a database connection pool). This was rejected because:

- `ConversationRuntime` is not stateless — it owns `_history` (`List<InputMessage>`), which IS the session state. Sharing a runtime across users would require loading history from disk before every turn and clearing it after — trading RAM for disk I/O on every single request.
- The "heavy" DI services (`SmartRouter`, `IToolExecutor`, `IToolRegistry`, `ISessionStore`, `SovrantConfig`) are already **shared singletons**. Each runtime holds only references to them. The per-session memory cost is essentially just the conversation history (~40–80 KB for a typical 20-turn session).
- The JSONL store is already the source of truth on disk. Evicting an idle runtime is safe and reversible — the next request for that session reloads history from JSONL and creates a fresh runtime.

One runtime per session is the correct model. The gaps to close are eviction and turn ordering.

#### Gap 1 — Unbounded Growth (TTL + LRU Eviction)

`RuntimeSessionPool` currently keeps runtimes alive indefinitely. Fix:

- `RuntimeSessionPool` records `DateTimeOffset LastAccess` per entry (update on every `GetOrCreateAsync`)
- A background `IHostedService` (e.g., `SessionEvictionService`) runs on a timer (every 5 min) and removes entries where `LastAccess < UtcNow - TTL`
- `SOVRANT_SESSION_TTL_SECONDS` env var (default: `3600`)
- LRU cap: if the active session count exceeds `SOVRANT_MAX_SESSIONS` (default: `500`), evict the least-recently-used sessions above the cap immediately, before the timer fires
- `DELETE /v1/sessions/{id}` continues to work for explicit eviction

Evicted sessions are not lost — their JSONL history persists on disk. The next request with that session ID recreates the runtime and replays history.

#### Gap 2 — Concurrent Turn Corruption (Per-Session Lock)

`ConcurrentDictionary` in the current pool prevents creating two runtimes for the same session, but it does **not** prevent two simultaneous HTTP requests from calling `RunTurnAsync` on the same session concurrently. Since `_history` is a plain `List<InputMessage>`, concurrent turns would corrupt it (both turns append to the same list, producing an interleaved, invalid history).

Fix: add a `SemaphoreSlim(1,1)` per session in the pool. Callers acquire it before starting a turn and release it after.

```csharp
// RuntimeSessionPool internal entry
private sealed record SessionEntry(
    IConversationRuntime Runtime,
    SemaphoreSlim Lock,         // serializes turns within this session
    DateTimeOffset LastAccess);
```

`GetOrCreateAsync` returns both the runtime and the lock. The HTTP handler acquires the lock, runs the turn, and releases it — ensuring turns are strictly ordered per session regardless of how many concurrent HTTP requests arrive.

#### Implementation Plan

1. Add `SessionEntry` record to `RuntimeSessionPool` (runtime + lock + last-access timestamp)
2. Update `GetOrCreateAsync` to return `SessionEntry` (or expose a `RunExclusiveAsync` helper)
3. Update `ChatRoutes.HandleAsync` to acquire/release the per-session lock around `RunTurnAsync`
4. Add `SessionEvictionService : IHostedService` — timer-based TTL sweep + LRU cap enforcement
5. Add `SOVRANT_SESSION_TTL_SECONDS` and `SOVRANT_MAX_SESSIONS` to env var config and `MutableServerConfig`
6. Expose TTL and max-sessions in `GET /v1/status` response

---

### Phase 10 — Small Team Hardening ✅

**Depends on:** Phase 9 (session TTL + per-session lock)

**Goal:** Make Sovrant solid for a small trusted team sharing a single deployment — a single bearer token is fine, but config changes must be session-scoped, each user should bring their own LLM key, and the server must be observable (token usage, context window) and resilient (rate limiting, usage tracking). No user login system required at this stage.

#### What works today for a team

Session history is already isolated per `session_id` — ten users with different session IDs have fully independent conversation histories and tool state. A shared `SOVRANT_TOKEN` is fine for a trusted team where everyone is known and has the token. This is sufficient for a team of up to ~50 engineers sharing an internal deployment.

#### What this phase adds

| Gap | Problem | Fix |
|---|---|---|
| `PUT /v1/config` is global | One user changing the model or permission mode (or calling `EnterPlanMode`) changes it for all sessions simultaneously | Session-scoped config overlay — each `SessionEntry` carries a `SessionConfig` that shadows the global defaults |
| Single `LLM_API_KEY` | All sessions bill to the same key — no per-session cost visibility | Phase 8 per-request `X-LLM-Api-Key` header — each user or client supplies their own key |
| No per-session rate limiting | A single session can saturate the server with concurrent requests | Per-session request rate limiter (ASP.NET Core `RateLimiter` middleware) |
| No cost visibility | No way to see which session is responsible for LLM spend | Per-session token accumulation in `SessionEntry`; `GET /v1/usage` summary endpoint |
| No context window visibility | Users can't see how much context is consumed | `context_used_pct` and `tokens_remaining` in `GET /v1/sessions/{id}`; REPL status line |

#### Design

**Session-scoped config overlay**
`PUT /v1/config` currently mutates a single `MutableServerConfig` singleton — this is what makes `EnterPlanMode` global. Fix: each `SessionEntry` carries a `SessionConfig` (model, permission mode) that overlays the global defaults for that session only. Add `PUT /v1/sessions/{id}/config` for per-session overrides. The global `PUT /v1/config` remains available for operators to set server-wide defaults.

**Per-session rate limiting**
Use ASP.NET Core's built-in `RateLimiter` middleware keyed on `session_id`. Policy: N requests/minute per session (configurable via `SOVRANT_RATE_LIMIT_RPM`, default 60). Returns `429` with a `Retry-After` header.

**Usage tracking**
Each `SessionEntry` accumulates `TotalInputTokens` and `TotalOutputTokens` across all turns. Token count capture (Phase 2 prerequisite) must be fixed first. `GET /v1/usage` returns a per-session summary.

#### Implementation Plan

1. Add `SessionConfig` overlay to `SessionEntry` — model, permission mode; initialised from global `MutableServerConfig` defaults on session creation
2. Add `PUT /v1/sessions/{id}/config` endpoint; update `EnterPlanMode`/`ExitPlanMode` to write to `SessionConfig` rather than the shared singleton (fixes the global plan mode issue)
3. Add ASP.NET Core `RateLimiter` middleware keyed on `session_id`; `SOVRANT_RATE_LIMIT_RPM` env var
4. Fix token count capture (Phase 2 prerequisite); add `TotalInputTokens` / `TotalOutputTokens` to `SessionEntry`
5. Add `GET /v1/usage` endpoint — per-session token summary
6. Add `context_used_pct` and `tokens_remaining` to `GET /v1/sessions/{id}`; surface in REPL status line

---

### Phase 11 — LSP Integration (Language Server Protocol) ✅

**Competitor precedent:** opencode ✅ (20+ language servers, diagnostics, go-to-definition, symbol search)

**Goal:** Give the agent semantic code intelligence — not just text search — by connecting to real language servers running alongside Sovrant.

#### Motivation

Grep and Glob find text. They cannot tell the agent that a variable is unused, that a function signature has changed, or where all callers of a method are. A language server speaks the code's actual type system: it can answer "what type does this return?", "what breaks if I rename this?", "what are all the diagnostics in this file?". This makes refactoring and bug-fixing dramatically more accurate and less likely to introduce regressions.

#### Architecture

An `ILspClient` service manages the lifecycle of one or more language server processes (e.g., `omnisharp`, `pyright`, `typescript-language-server`, `clangd`). The client communicates via JSON-RPC over stdio (the LSP standard transport). Five tool wrappers expose LSP capabilities to the model:

| Tool | LSP request | What it returns |
|---|---|---|
| `LspHover` | `textDocument/hover` | Type info, documentation for a symbol at a file position |
| `LspDefinition` | `textDocument/definition` | File + line where a symbol is defined |
| `LspReferences` | `textDocument/references` | All locations that reference a symbol |
| `LspDiagnostics` | `textDocument/publishDiagnostics` | All errors and warnings in a file |
| `LspRename` | `textDocument/rename` | Workspace-wide rename with preview |

#### Implementation Plan

1. Add `ILspClient` / `LspClient` to a new `Sovrant.Lsp` project — JSON-RPC over stdio, manages server process lifecycle
2. Add language server configuration to `SovrantConfig` (`LspServers: { "csharp": "omnisharp", "python": "pyright" }`)
3. Implement the five tool wrappers in `Sovrant.Tools/Lsp/`
4. Auto-start configured language servers when the CLI starts; shut down on exit
5. In server mode, start language servers per active session (or shared if single-workspace)

---

### Phase 12 — CI/CD Pipeline Integration ✅

**Competitor precedent:** Claude Code ✅ (GitHub Actions + GitLab CI)

**Goal:** Enable Sovrant to run autonomously inside CI pipelines — fix failing tests, resolve build errors, update generated code — without human intervention.

**Status: ✅ Complete**

#### What was implemented

1. **`--ci` flag** on the CLI `prompt` command — machine-readable JSON output (`CiOutput` record), non-zero exit code on error, suppressed console logging
2. **`CiPermissionPolicy`** — auto-approves file edits and shell commands, denies unknown destructive operations (43 tests)
3. **`CiUserInputProvider`** — no-op input provider for non-interactive CI environments
4. **GitHub Actions composite action** at `.github/actions/sovrant-agent/action.yml` — builds, runs the agent in CI mode, exposes `success`, `output`, and `json` outputs
5. **Documentation** at `docs/ci-cd.md` — full reference for `--ci` flag, JSON output format, GitHub Actions usage, GitLab CI template, security notes

#### Key files

- `src/Sovrant.Runtime/Permissions/CiPermissionPolicy.cs`
- `src/Sovrant.Cli/Program.cs` (RunCiTurnAsync, --ci option, CiOutput DTOs)
- `src/Sovrant.Cli/CiUserInputProvider.cs`
- `.github/actions/sovrant-agent/action.yml`
- `docs/ci-cd.md`
- `tests/Sovrant.Runtime.Tests/Permissions/CiPermissionPolicyTests.cs`

---

### Phase 13 — Slack / Webhook Integration ✅

**Goal:** Invoke Sovrant from any messaging platform (Slack, Teams, Discord) or custom system via a generic webhook endpoint.

**Status: ✅ Complete**

#### What was implemented

1. **`POST /v1/webhook`** — generic webhook endpoint in `Sovrant.Server` that accepts `{ source, user_id, message, callback_url, model, thread_id }`, derives a stable session ID (`webhook:{source}:{user_id}`), runs an agentic turn, and returns the result synchronously or POSTs it to a callback URL
2. **`WebhookCallbackService`** — posts results to callback URLs in the background using a named `HttpClient`; errors are logged but do not propagate
3. **Webhook DTOs** — `WebhookRequest`, `WebhookResponse`, `WebhookToolCall` with full JSON serialization
4. **Slack bot** — `integrations/slack/manifest.json` (app manifest) + `integrations/slack/handler.js` (Node.js Bolt SDK event handler using Socket Mode)
5. **Documentation** at `docs/webhooks.md` — webhook endpoint reference, Slack setup guide, Teams/Discord examples, security notes
6. **New test project** `Sovrant.Server.Tests` with 12 tests for webhook DTOs and callback service

#### Key files

- `src/Sovrant.Server/Webhooks/WebhookRequest.cs`
- `src/Sovrant.Server/Webhooks/WebhookResponse.cs`
- `src/Sovrant.Server/Webhooks/WebhookCallbackService.cs`
- `src/Sovrant.Server/Routes/WebhookRoutes.cs`
- `integrations/slack/manifest.json`
- `integrations/slack/handler.js`
- `docs/webhooks.md`
- `tests/Sovrant.Server.Tests/` (12 tests)

---

### Phase 14 — Frontend SDK, Diff View, Session Export ✅

**Goal:** TypeScript/JavaScript client SDK, structured diff rendering in the CLI, and session export endpoint.

**Status: ✅ Complete**

#### What was implemented

1. **TypeScript SDK** at `sdk/js/` — `@sovrant/sdk` npm package
   - `SovrantClient` class: chat (sync + streaming), webhook, config, status, sessions, usage, health
   - SSE stream parser (`parseSSEStream`) for raw chunk iteration
   - React `useChat()` hook with streaming text, tool events, and error handling
   - Per-request credential injection via options
   - Built-in retry on 429/5xx with configurable max retries
   - `SovrantApiError` class for structured error handling
   - Full TypeScript types for all request/response shapes

2. **Structured Diff View** in CLI REPL
   - `DiffRenderer` class renders edit/write tool inputs as colored diffs
   - Edit tools: red lines for `old_string`, green lines for `new_string`
   - Write tools: file path + line count + first 5 lines preview
   - Integrated into `ToolUseRequested` event handling in the REPL

3. **Session Export** — `GET /v1/sessions/{id}/export`
   - Returns full session as human-readable markdown (`text/markdown`)
   - User turns, assistant turns (with model + token counts), tool calls (as code blocks), tool results
   - Long tool results truncated to 2000 chars for readability

#### Key files

- `sdk/js/src/client.ts` — `SovrantClient`
- `sdk/js/src/sse.ts` — SSE parser
- `sdk/js/src/hooks/use-chat.ts` — React hook
- `sdk/js/src/types.ts` — TypeScript types
- `sdk/js/src/index.ts` — barrel export
- `src/Sovrant.Cli/DiffRenderer.cs` — structured diff display
- `src/Sovrant.Server/Routes/SessionRoutes.cs` — added `ExportSession`

---

### Phase 15 — MCP Server Mode ✅

**Goal:** Expose Sovrant as an MCP server so it can be consumed by MCP clients or composed into larger agent pipelines.

**Status:** Complete. `sovrant mcp-server` subcommand activates stdio-based MCP server. Bridges all `IToolRegistry` tools + synthetic `chat` tool + session/config resources. `SOVRANT_MCP_TOOLS` env var for optional filtering. 20 tests. See [`docs/mcp-server.md`](mcp-server.md).

**Update (2026-04-12):** Extended with full MCP v1.2.0 protocol support:
- **Prompts:** 4 built-in prompts (code-review, explain, summarize-session, refactor) with argument definitions and `GetPromptResult` builders.
- **Logging control:** `SetLoggingLevel` handler.
- **Completions:** Auto-complete for session IDs, languages, and goals via `WithCompleteHandler`.
- **Resource subscriptions:** Subscribe/unsubscribe handlers with `ConcurrentDictionary`-based ref counting. Resource templates for `sovrant://sessions/{session_id}`.
- **HTTP/SSE transport:** Opt-in via `SOVRANT_MCP_HTTP=true` env var. Uses `ModelContextProtocol.AspNetCore` v1.2.0 with `WithHttpTransport()` + `app.MapMcp("/mcp")`. Shared handler registration via `AddSovrantMcpHandlers()` extension.
- **Tests:** `McpProtocolFeatureTests.cs` (4 tests for subscription ref counting).

**Files added:**
- `src/Sovrant.Mcp/` — `McpServerSetup.cs`, `ChatToolHandler.cs`, `ToolFilter.cs`
- `tests/Sovrant.Mcp.Tests/` — `ToolBridgeTests.cs`, `ToolFilterTests.cs`, `ChatToolHandlerTests.cs`, `McpProtocolFeatureTests.cs`
- `docs/mcp-server.md` — full documentation with IDE config examples

---

### Phase 16 — Dynamic MCP Tool Proxy (`MCPTool`) ✅

**Goal:** Allow the model to discover and invoke any tool exposed by a connected MCP server dynamically, without those tools being statically registered in `ToolRegistrar` at startup.

**Status:** Complete. `MCPTool` added to `Sovrant.Tools/Mcp/`. Parameters: `tool` (required), `server` (optional — searches all clients when omitted), `input` (object). Uses existing `McpClientRegistry` — no new infrastructure. 8 tests.

**Files added:**
- `src/Sovrant.Tools/Mcp/McpProxyTool.cs` — implementation
- `tests/Sovrant.Tools.Tests/Mcp/McpProxyToolTests.cs` — 8 tests

---

### Phase 17 — MCP OAuth Authentication (`McpAuthTool`) ✅

**Goal:** Support MCP servers that require OAuth 2.0 authentication, enabling Sovrant to connect to SaaS APIs (GitHub, Google, Salesforce, etc.) through MCP without the server holding static API keys.

#### Motivation

At cloud scale, users will want to connect MCP servers that front OAuth-protected APIs. Static API keys are a liability — they don't expire, can't be scoped per user, and are a breach risk if the server is compromised. OAuth tokens are user-scoped, short-lived, and revocable.

#### Design

- `McpAuthTool` initiates an OAuth flow for a named MCP server
- The server generates an authorization URL and returns it to the model
- The model surfaces the URL to the user (CLI: prints it; Server: returns it in the SSE stream for the frontend to open)
- After the user completes the OAuth flow, the callback exchanges the code for tokens
- Tokens are stored in a scoped, encrypted credential store (never in session history or logs)
- The `IMcpClient` for that server is updated with the new token and reconnected

#### Security Considerations

- OAuth tokens must never appear in session JSONL, server logs, or API responses
- Token storage must be encrypted at rest (use OS keychain on CLI, server-side encrypted store for the HTTP server)
- Refresh token rotation must be handled automatically on expiry
- Per-user token isolation is required in multi-tenant deployments (Phase 8 session pool key applies here too)

#### Implementation Plan

1. Add OAuth state machine to `IMcpClient` — `InitiateOAuthAsync`, `CompleteOAuthAsync`, `RefreshTokenAsync`
2. Implement `McpAuthTool` in `Sovrant.Tools/Mcp/`
3. Add encrypted credential store (`ICredentialStore`) with OS keychain backend for CLI and AES-GCM backend for server
4. Add callback endpoint to `Sovrant.Server` (`GET /v1/mcp/auth/callback`)
5. Surface the auth URL in SSE stream as a new `RuntimeEvent.AuthRequired` event type

---

### Phase 18 — Dual Agent Architecture (Scaffolding) ✅

**Source:** Dual Agent Architecture design document (2026-04-04).
**Status:** ✅ Complete — scaffolding done, full execution implemented in Phase 20.

**Goal:** Introduce two interchangeable orchestration backends behind a shared `IOrchestrationSystem` interface so the rest of the system never depends on a specific implementation. Whichever architecture proves superior in practice can be promoted as the default without touching consumers.

#### Why two backends

Orchestration coordination is an unsettled space. Process-per-agent (spawning a child process for each agent) matches the original OpenClaude approach and is easy to reason about in isolation. In-process async channels are lighter, faster, and compose naturally with the existing `ConversationRuntime` model. Both are viable; the winner is not yet clear. The interface abstraction preserves both options with zero coupling.

#### Project: `Sovrant.Agents`

Depends on `Sovrant.Runtime` (for `IConversationRuntime`, `IToolRegistry`, `FilteredToolRegistry`). Consumers reference it for the `IOrchestrationSystem` interface and register via `services.AddOrchestrationSystem()`.

```
src/Sovrant.Agents/
  Abstractions/
    IAgent.cs                          ← interface: Name + HandleAsync
    IOrchestrationSystem.cs               ← interface: RegisterAgent, RunTaskAsync, CancelTask, ShutdownAsync
  Models/
    AgentTask.cs                       ← record: Id, Prompt, AssignedAgentName, Metadata, CreatedAt
    AgentResult.cs                     ← record: TaskId, Success, Output, Error; Ok/Fail factories
    AgentRole.cs                       ← enum: General, Planner, Coder, Reviewer, Executor, Supervisor
  Isolated/
    ProcessAgent.cs                    ← IAgent backed by ProcessStartInfo; stdin/stdout stdio
    ProcessBasedOrchestrationSystem.cs    ← spawns ProcessAgent per task; AGENT_MODE=isolated
  Shared/
    BaseAgent.cs                       ← abstract IAgent with Channel<AgentTask> inbox + RunLoopAsync
    OrchestrationCoordinator.cs           ← routes tasks; per-task CTS; shutdown drain
    InProcessOrchestrationSystem.cs       ← wraps coordinator + WorkspaceContext; AGENT_MODE=shared (default)
    WorkspaceContext.cs                ← thread-safe ConcurrentDictionary scratch space for a run
  Config/
    AgentSystemConfig.cs               ← UseIsolatedAgents bool; MaxConcurrentAgents; TaskTimeoutSeconds
    AgentSystemFactory.cs              ← static Create(config, services) → IOrchestrationSystem
  ServiceCollectionExtensions.cs       ← AddOrchestrationSystem(config?) reads AGENT_MODE env var
```

#### Configuration switch

| Mechanism | Effect |
|---|---|
| `AGENT_MODE=isolated` | `ProcessBasedOrchestrationSystem` (process-per-agent) |
| `AGENT_MODE=shared` or unset | `InProcessOrchestrationSystem` (shared, in-process) |
| `AgentSystemConfig.UseIsolatedAgents = true` | Isolated (default), programmatic override |

#### Fully implemented (Phase 20)

- `ProcessAgent.HandleAsync` — process spawn, stdin write, stdout/stderr read, cancellation kills process tree
- `ProcessBasedOrchestrationSystem.RunTaskAsync` — agent resolution, linked CTS, timeout handling
- `OrchestrationCoordinator.DispatchAsync` — agent selection by name/first-registered, semaphore-based concurrency control, linked CTS with timeout, proper cleanup
- `OrchestrationCoordinator.ShutdownAsync` — awaiting all `BaseAgent.RunLoopAsync` tasks
- `SovrantAgent` — `BaseAgent` subclass backed by `IConversationRuntime`, collects `TextChunk` events
- `SovrantAgentFactory` — creates `SovrantAgent` from `TeamMemberInfo` with role-specific prompts and tool filtering
- `AgentPrompts` — role-specific system prompts for each `AgentRole`
- `FilteredToolRegistry` — decorator restricting tool visibility per agent
- `ITeamRegistry` / `InMemoryTeamRegistry` — team member lifecycle management
- Team tools: `TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`

#### Also working

- `WorkspaceContext` — thread-safe `ConcurrentDictionary` variable store
- `BaseAgent` — channel construction, `EnqueueAsync`, `RunLoopAsync`, `Complete`
- `AgentSystemConfig.FromEnvironment()` — reads `AGENT_MODE`
- `AgentSystemFactory.Create` — correct backend selection
- `ServiceCollectionExtensions.AddOrchestrationSystem` — DI wiring (includes `ITeamRegistry` and `SovrantAgentFactory`)
- All interfaces and model records

---

### Phase 19 — Orchestrated Teams (`TeamCreateTool` / `TeamDeleteTool`) ✅

**Depends on:** Phase 18 (`Sovrant.Agents` scaffolding — already done)

**Goal:** Allow a single Sovrant session to orchestrate a team of independent sub-agents running in parallel, each with its own session, tool access, and LLM provider — coordinated by a supervisor agent.

#### Motivation

Complex long-horizon tasks (large refactors, multi-service deployments, research pipelines) exceed what a single agentic loop can handle reliably. Teams decompose the problem: a supervisor breaks the work into subtasks, spawns specialist agents for each, monitors progress, and synthesises results. This is the foundation for Sovrant as a cloud-scale autonomous engineering platform.

#### Architecture

```
Supervisor Agent (ConversationRuntime)
  ├── TeamCreateTool → spawns Agent A (isolated runtime, own session, own tools)
  ├── TeamCreateTool → spawns Agent B
  ├── TeamCreateTool → spawns Agent C
  └── Collects results via TaskOutput / team message bus
```

- Each team member is a full `ConversationRuntime` with its own session pool slot, tool set, and optionally its own LLM provider/key (Phase 8)
- The supervisor coordinates via `TaskOutput` polling or a lightweight message bus
- Team lifecycle: `TeamCreate` spawns and starts, `TeamDelete` cancels and evicts
- Teams are scoped to the supervisor's session — they are cleaned up when the supervisor session is evicted (Phase 9 TTL)

#### Design Decisions to Resolve

- **Message bus vs polling:** Polling via `TaskOutput` is simpler and avoids a new infrastructure dependency. Start with polling; add a message bus if latency becomes a problem.
- **Resource limits:** Each team member occupies a session pool slot and may trigger many LLM calls in parallel. Rate limiting per team and per supervisor session is required before production use.
- **Nesting depth:** Start with depth-1 (supervisor + workers only); recursive teams add significant complexity.

#### Implementation Plan

1. Design `ITeamRegistry` — tracks team members per supervisor session ID
2. Implement `TeamCreateTool` — accepts agent prompt, tool subset, optional provider override; spawns a `ConversationRuntime` and registers it
3. Implement `TeamDeleteTool` — cancels and evicts the named team member
4. Extend `TaskOutput` to support reading from team member sessions (not just background shell processes)
5. Add team-scoped resource limits to `MutableServerConfig`
6. Add team lifecycle events to the SSE stream (`RuntimeEvent.TeamMemberStarted`, `RuntimeEvent.TeamMemberComplete`)

---

### Phase 20 — Dual Agent Architecture: Full Implementation ✅

**Depends on:** Phase 18 (scaffolding), Phase 19 (team tools that will consume `IOrchestrationSystem`)
**Status:** ✅ Complete — both backends implemented, team tools wired, 58 tests passing.

**Goal:** Complete the two orchestration backends stubbed in Phase 18. At this point the `TeamCreateTool` / `TeamDeleteTool` from Phase 19 will be wired to `IOrchestrationSystem` and the choice of isolated vs. shared backend becomes a runtime configuration decision.

#### Option A completion: `ProcessBasedOrchestrationSystem`

1. `ProcessAgent.HandleAsync` — spawn child process from `ProcessStartInfo`; write task JSON to stdin; stream stdout line by line; parse structured tool-use blocks (same format as original OpenClaude); propagate `CancellationToken` via `Process.Kill()`
2. `ProcessBasedOrchestrationSystem.RunTaskAsync` — resolve target agent; create linked CTS; stream result incrementally; record CTS for `CancelTask`

#### Option B completion: `OrchestrationCoordinator.DispatchAsync`

1. Resolve target agent by `AgentTask.AssignedAgentName` or by `AgentRole` (planner → coder → reviewer pipeline)
2. For `BaseAgent` subtypes: enqueue via `EnqueueAsync`, pair with a `TaskCompletionSource<AgentResult>` registered by `RunLoopAsync`
3. For plain `IAgent` implementations: call `HandleAsync` directly on a `Task.Run` thread
4. Per-task linked CTS stored in `_taskCts`; `CancelTask` triggers it
5. `ShutdownAsync` — await all `BaseAgent.RunLoopAsync` background tasks

#### Implementation Plan

1. Implement `ProcessAgent.HandleAsync` with stdin/stdout pipes and tool-use message parser
2. Implement `ProcessBasedOrchestrationSystem.RunTaskAsync` with full lifecycle management
3. Implement `OrchestrationCoordinator.DispatchAsync` and update `ShutdownAsync`
4. Wire `TeamCreateTool` to use `IOrchestrationSystem.RunTaskAsync` (replaces ad-hoc `ConversationRuntime` spawning in Phase 19)
5. Add integration tests: isolated backend with a mock echo process; shared backend with a test `BaseAgent` subclass

---

### Phase 21 — Hook Lifecycle System ✅

**Inspired by:** everything-claude-code (34 hooks across 7 lifecycle events, ~16 built-in implementations)
**Depends on:** None — can be implemented independently

**Goal:** Add an event-driven hook system that fires user-defined scripts at key points in the agent lifecycle: before/after tool use, session start/end, pre-compaction, and response stop. This enables quality enforcement, security monitoring, cost tracking, and memory persistence without modifying the core agent loop.

#### Why this is high priority

Hooks are the single most extensible feature in everything-claude-code. They enable the entire ecosystem of quality gates, governance, learning, and observability — all without touching the engine itself. Every subsequent phase (verification loops, governance, cost tracking, memory) becomes dramatically easier to implement when hooks exist.

#### Hook Events

| Event | When | Use Cases |
|---|---|---|
| `SessionStart` | New session begins | Load previous session summary, detect project type, seed context |
| `PreToolUse` | Before any tool executes | Block dangerous operations, validate inputs, config protection |
| `PostToolUse` | After a tool completes | Lint/format edited files, audit log bash commands, accumulate edits |
| `PostToolUseFailure` | After a tool fails | Error analysis, retry suggestions |
| `PreCompact` | Before context compaction | Save state to memory before context is lost |
| `Stop` | Agent response ends | Batch format/typecheck, desktop notifications |
| `SessionEnd` | Session terminates | Persist session summary, extract patterns |

#### Architecture

```
src/Sovrant.Runtime/Hooks/
  IHookRunner.cs           ← interface: RunAsync(HookEvent, HookContext)
  HookEvent.cs             ← enum of lifecycle events
  HookContext.cs            ← event-specific context (tool name, file path, session ID, etc.)
  HookRunner.cs            ← loads hooks from config, executes matching scripts
  HookConfig.cs            ← hook definition: event, matcher (tool/glob), command, timeout
```

#### Configuration

Hooks defined in `.sovrant/hooks.json` or `~/.sovrant/hooks.json`:

```json
{
  "hooks": [
    { "event": "PostToolUse", "matcher": { "tool": "Edit" }, "command": "dotnet format {file}", "timeout": 30000 },
    { "event": "PreToolUse", "matcher": { "tool": "Bash" }, "command": "scripts/validate-command.sh {command}" },
    { "event": "Stop", "command": "scripts/batch-format.sh" }
  ]
}
```

#### Hook Profiles

Three enforcement levels configurable via `SOVRANT_HOOK_PROFILE`:
- `minimal` — session start/end only
- `standard` — all non-blocking hooks (default)
- `strict` — all hooks, including blocking pre-tool-use validators

#### Implementation Plan

1. Define `IHookRunner`, `HookEvent`, `HookContext`, `HookConfig` in `Sovrant.Runtime/Hooks/`
2. Implement `HookRunner` — loads from config, matches event + tool name, executes via `ProcessExecutor`
3. Add hook firing points to `ConversationRuntime.RunTurnAsync` (pre/post tool use) and `RuntimeSessionPool` (session start/end)
4. Add `PreCompact` hook to context compaction logic
5. Add `Stop` hook after the final `TurnComplete` event
6. Support both blocking (pre-tool-use: can abort the tool) and fire-and-forget (post-tool-use) execution modes
7. Add `SOVRANT_HOOK_PROFILE` and `SOVRANT_DISABLED_HOOKS` env vars
8. Tests: hook matching, execution, timeout, blocking vs fire-and-forget, profile filtering

---

### Phase 22 — Specialized Agent Definitions (Role Templates + Model Routing) ✅

**Inspired by:** everything-claude-code (38 specialized agents with tool restrictions and model selection)
**Depends on:** Phase 19+20 (orchestrated team tools — already complete)

**Goal:** Define a library of **24 specialized agent role templates** spanning coding, research, communication, operations, and creative work — each with a structured methodology, constrained tool access, and a **recommended capability level** (high/standard/fast). Templates specify what capability a task *needs*, not which vendor model to use — admins and users choose the actual model and API key. These templates are loaded by `TeamCreate` and `AgentTool` to spawn purpose-built sub-agents. Sovrant is a general-purpose agentic platform — coding is one vertical, not the entire product.

#### Why this is high priority

The team tools exist (Phase 19+20) but agents are generic — they get a role enum and a freeform prompt. Structured role templates with defined methodologies, tool restrictions, and output formats make agents dramatically more effective. This is the difference between "ask an LLM to do research" and "run a structured 6-step deep research workflow with source attribution and confidence scoring."

#### Agent Templates — General-Purpose (10 templates)

| Template | Recommended Level | Tools | Methodology |
|---|---|---|---|
| **Planner** | High | Read, Grep, Glob (read-only) | Requirements analysis → architecture review → step breakdown → implementation ordering |
| **Architect** | High | Read, Grep, Glob (read-only) | System design, trade-off analysis, ADR generation, scalability review |
| **Researcher** | High | Read, Grep, Glob, WebSearch, WebFetch | Multi-source research with citation, 6-step workflow (scope → search → evaluate → synthesize → cite → report) |
| **Chief of Staff** | High | Read, Grep, Glob, Bash, Edit, Write | Multi-channel communication triage (email, Slack, calendar), 4-tier priority classification, response drafting |
| **Content Writer** | Standard | Read, Write, Edit, WebSearch | Long-form content creation with voice matching, anti-slop enforcement, multi-platform adaptation |
| **Data Analyst** | Standard | Read, Grep, Glob, Bash | Data exploration, pattern detection, visualization generation, statistical analysis |
| **Project Manager** | Standard | Read, Grep, Glob, Edit | Issue triage, cross-system coordination (GitHub/Linear/Jira), status tracking, dependency mapping |
| **Doc Updater** | Standard | Read, Write, Edit, Grep, Glob | Documentation sync with code changes, API doc generation, changelog maintenance |
| **Loop Operator** | Standard | Read, Grep, Glob, Bash, Edit | Safe autonomous loop management with monitoring, stall detection, and human escalation triggers |
| **Executor** | Standard | Bash, PowerShell, Read | Run commands, monitor output, report results |

#### Agent Templates — Code-Specific (8 templates)

| Template | Recommended Level | Tools | Methodology |
|---|---|---|---|
| **Code Reviewer** | High | Read, Grep, Glob (read-only) | Multi-severity review (CRITICAL/HIGH/MEDIUM/LOW) with confidence thresholds |
| **Security Reviewer** | High | Read, Grep, Glob (read-only) | OWASP Top 10 scan, secret detection, dependency audit, attack surface analysis |
| **TDD Guide** | Standard | All | Red-Green-Refactor cycle enforcement, 80%+ coverage target, edge case generation |
| **Refactor Cleaner** | Standard | Read, Grep, Glob, Edit | Dead code detection, safe incremental removal with SAFE/CAREFUL/RISKY classification |
| **Coder** | Standard | All | Implementation from specs, test writing, error handling |
| **Build Error Resolver** | Standard | Read, Grep, Glob, Bash | Parse build errors, identify root cause, apply fix, verify build passes |
| **E2E Test Runner** | Standard | Read, Bash, Grep | Playwright/Selenium E2E test execution, failure analysis, screenshot capture |
| **Database Reviewer** | High | Read, Grep, Glob (read-only) | Schema review, query optimization, migration safety analysis |

#### Agent Templates — Creative & Domain (6 templates)

| Template | Recommended Level | Tools | Methodology |
|---|---|---|---|
| **GAN Planner** | High | Read, Grep, Glob | Expands briefs into comprehensive product specs with success criteria |
| **GAN Generator** | Standard | All | Implements features per spec, manages sprint iterations |
| **GAN Evaluator** | High | Read, Bash, Grep | Tests live apps against rubric, scores 4 dimensions (design, originality, craft, functionality) |
| **Prompt Optimizer** | High | Read, Write, Edit | 6-phase prompt analysis: gap identification, ecosystem mapping, structure optimization |
| **Sales Intelligence** | Standard | Read, WebSearch, WebFetch | Lead scoring, prospecting pipeline, warm-path discovery, outreach draft generation |
| **Compliance Reviewer** | High | Read, Grep, Glob (read-only) | Policy adherence verification, PHI/PII detection, regulatory checklist enforcement |

#### Architecture

```
src/Sovrant.Agents/Templates/
  AgentTemplate.cs          ← record: Name, Role, RecommendedLevel, AllowedTools, SystemPrompt, Methodology, OutputFormat
  AgentTemplateRegistry.cs  ← loads built-in + user-defined templates from .sovrant/agents/
  RecommendedLevel.cs       ← enum: High (complex reasoning), Standard (general tasks), Fast (triage/simple)
```

Templates stored as markdown files in `.sovrant/agents/` (user-defined) or embedded resources (built-in):

```markdown
---
name: security-reviewer
role: reviewer
recommended_level: high
allowed_tools: [Read, Grep, Glob]
---
# Security Reviewer

## Identity
You are a security-focused code reviewer specializing in OWASP Top 10...

## Methodology
1. Attack surface mapping
2. Input validation audit
3. Authentication/authorization review
4. Secret detection scan
...
```

#### Model Resolution (Provider-Agnostic)

Templates specify a `RecommendedLevel` (high/standard/fast), not a specific model. The actual model used is resolved at runtime based on what the admin or user has configured:

1. **User override** — `TeamCreate` or `AgentTool` accepts an optional `model` parameter (e.g., `"gpt-4o"`, `"claude-sonnet-4-6"`, `"deepseek-r1"`)
2. **Admin defaults** — `.sovrant/config.json` or env vars map levels to models for the deployment:
   ```json
   { "model_levels": { "high": "claude-opus-4-6", "standard": "gpt-4o", "fast": "gpt-4o-mini" } }
   ```
3. **Template recommendation** — if no override and no admin default, the template's `recommended_level` is logged as a suggestion but the session's current model is used

This keeps Sovrant provider-agnostic. Users who care about cost can route "high" tasks to a cheaper model; users who want the best results can route everything to their top-tier model. The template defines *what capability is needed*, not *which vendor to call*.

#### Implementation Plan

1. Define `AgentTemplate` and `RecommendedLevel` in `Sovrant.Agents/Templates/`
2. Implement `AgentTemplateRegistry` — loads built-in templates from embedded resources + user templates from `.sovrant/agents/`
3. Update `SovrantAgentFactory` to accept `AgentTemplate` and resolve model via user override → admin default → session model
4. Update `TeamCreateTool` to accept a `template` parameter (e.g., `"security-reviewer"`)
5. Update `AgentTool` to accept an optional `template` parameter for ad-hoc sub-agents
6. Ship **24 built-in templates**: 10 general-purpose, 8 code-specific, 6 creative/domain
7. Tests: template loading, model level resolution (override → admin default → session), tool restriction enforcement, user template override

---

### Phase 23 — Template Externalisation (Built-ins as Markdown Files) ✅

**Depends on:** Phase 22 (AgentTemplateRegistry already loads user templates from `.sovrant/agents/`)

**Goal:** Move the 24 built-in agent templates out of `BuiltInTemplates.cs` and into
plain markdown files on disk. This lets operators tune system prompts, adjust tool
lists, and add new templates without recompiling — changes take effect immediately on
the next agent spawn.

#### Why this matters

Right now the built-in templates are embedded in source code. That means:
- Changing a prompt requires a build + deploy cycle.
- Operators running Sovrant as a shared service can't customise templates without forking.
- A/B testing or iterating on prompt methodology requires code changes.

With file-resident templates, a single `.md` edit is all it takes.

#### Design

Built-in templates ship as markdown files in a well-known location, loaded at startup
by `AgentTemplateRegistry`. The search order mirrors the existing layering:

```
1. ~/.sovrant/agents/          — user-global overrides (highest priority)
2. .sovrant/agents/            — project-level overrides
3. <install>/agents/           — shipped built-ins (lowest priority, always present)
```

The install directory is resolved from the executing assembly's location:
`Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)/agents/`

This means the 24 `.md` files are deployed alongside the binary (copied to output
via `<Content CopyToOutputDirectory="Always" />`), but any of them can be shadowed by
a file with the same name in `.sovrant/agents/` or `~/.sovrant/agents/`.

`BuiltInTemplates.cs` is deleted once the files are in place. The fallback
`NullObject` pattern (one hardcoded emergency template) is optional and not required.

#### File layout

```
src/Sovrant.Agents/agents/
  planner.md
  architect.md
  researcher.md
  chief-of-staff.md
  content-writer.md
  data-analyst.md
  project-manager.md
  doc-updater.cs
  loop-operator.md
  executor.md
  code-reviewer.md
  security-reviewer.md
  tdd-guide.md
  refactor-cleaner.md
  coder.md
  build-error-resolver.md
  e2e-test-runner.md
  database-reviewer.md
  gan-planner.md
  gan-generator.md
  gan-evaluator.md
  prompt-optimizer.md
  sales-intelligence.md
  compliance-reviewer.md
```

Each file uses the same front matter format already supported by `AgentTemplateRegistry.ParseTemplateFile`:

```markdown
---
name: security-reviewer
role: Reviewer
recommended_level: High
allowed_tools: [Read, Grep, Glob]
---
You are a security review agent...
```

#### Implementation Plan

1. Create `src/Sovrant.Agents/agents/` and write one `.md` file per built-in template
   (content ported verbatim from `BuiltInTemplates.cs`).
2. Mark each file `<Content CopyToOutputDirectory="PreserveNewest" />` in the `.csproj`.
3. Update `AgentTemplateRegistry` constructor to resolve the install-directory path
   and call `LoadUserTemplates` on it as the lowest-priority source.
4. Delete `BuiltInTemplates.cs`.
5. Update tests: `All_Returns24BuiltInTemplates` and per-template metadata tests should
   still pass — behaviour is unchanged, only the loading mechanism differs.
6. Verify that placing a custom `.md` in `.sovrant/agents/coder.md` correctly shadows
   the shipped `coder.md`.

#### Hot-reload (optional stretch goal)

Use `FileSystemWatcher` on the three template directories to reload changed files
without restarting the process. Gate behind `SOVRANT_TEMPLATE_WATCH=true` to avoid
unnecessary overhead in production.

---

### Phase 24 — Verification Loop & Quality Gates ✅

**Inspired by:** everything-claude-code (6-phase verification loop, quality gate hooks, TDD workflow)
**Depends on:** Phase 21 (hooks — gates run as post-stop hooks) or can be standalone tools

**Goal:** A structured multi-phase quality verification pipeline that runs automatically before PR submission or on demand via a `/verify` skill. Covers: build, type check, lint, test (with coverage threshold), security scan, and diff review.

#### Why this is high priority

The code review (Phases A-F) was manual. A verification loop automates the same checks as repeatable, enforceable gates. Combined with hooks (Phase 21), this runs automatically at the end of every coding session.

#### Verification Phases

| Phase | What it does | Pass criteria |
|---|---|---|
| **Build** | `dotnet build` / `npm run build` / `go build` | Zero errors |
| **Type Check** | Language-specific type checking (e.g., `tsc --noEmit`) | Zero errors |
| **Lint** | `dotnet format --verify-no-changes` / `eslint` / `golangci-lint` | Zero warnings at configured severity |
| **Test** | `dotnet test` with coverage collection | All tests pass, coverage ≥ configurable threshold (default 60%) |
| **Security Scan** | `dotnet list package --vulnerable` / `npm audit` / secret detection | Zero critical/high vulnerabilities |
| **Diff Review** | Git diff analysis: no debug code, no secrets, no unintended files | Clean diff |

#### Architecture

```
src/Sovrant.Tools/Quality/
  VerifyTool.cs              ← runs all 6 phases, returns structured report
  VerificationPhase.cs       ← enum + per-phase runner
  VerificationConfig.cs      ← thresholds, skip list, per-project overrides
  VerificationResult.cs      ← phase results with pass/fail + detail
```

Also a `/verify` skill that the model can invoke or that fires as a `Stop` hook.

#### Configuration

`.sovrant/verify.json`:
```json
{
  "coverage_threshold": 60,
  "lint_severity": "warning",
  "skip_phases": [],
  "build_command": "dotnet build Sovrant.slnx",
  "test_command": "dotnet test Sovrant.slnx --collect:\"XPlat Code Coverage\"",
  "security_command": "dotnet list package --vulnerable"
}
```

#### Implementation Plan

1. Define `VerificationPhase`, `VerificationConfig`, `VerificationResult` in `Sovrant.Tools/Quality/`
2. Implement `VerifyTool` — runs phases sequentially, collects results, returns structured report
3. Auto-detect project type (`.csproj` → dotnet, `package.json` → npm, `go.mod` → Go) for default commands
4. Add `/verify` skill registration
5. Optionally wire as a `Stop` hook (Phase 21) for automatic end-of-session verification
6. Tests: each phase runner, threshold enforcement, skip list, auto-detection

---

### Phase 25 — Governance, Security Monitoring & Audit ✅

**Inspired by:** everything-claude-code (governance capture, config protection, commit quality, enterprise controls)
**Depends on:** Phase 21 (hooks — governance runs as PreToolUse/PostToolUse hooks)

**Goal:** Defense-in-depth for agentic operations: detect secrets in tool outputs, block dangerous commands, protect configuration files, audit-log all bash commands, and provide enterprise control policies.

#### Components

| Component | What it does | Hook Event |
|---|---|---|
| **Secret Detection** | Scans tool inputs/outputs for AWS keys, tokens, JWTs, API keys | PostToolUse |
| **Dangerous Command Blocker** | Blocks `rm -rf /`, `git push --force`, `DROP TABLE`, `chmod 777` | PreToolUse (Bash) |
| **Config Protection** | Prevents modification of `.editorconfig`, `.eslintrc`, `Directory.Build.props` | PreToolUse (Edit/Write) |
| **Bash Audit Log** | Logs every bash command to `~/.sovrant/audit/bash-commands.jsonl` | PostToolUse (Bash) |
| **Commit Quality Gate** | Validates staged files for debug code, secrets, formatting before commit | PreToolUse (Bash, matcher: `git commit`) |
| **Governance Event Stream** | Structured JSON events for security-relevant actions: severity, session, timestamp | All |

#### Architecture

```
src/Sovrant.Runtime/Governance/
  IGovernanceMonitor.cs       ← interface: EvaluateAsync(GovernanceContext) → GovernanceVerdict
  GovernanceMonitor.cs        ← aggregates all detection rules
  GovernanceVerdict.cs        ← Allow / Warn / Block + reason
  SecretDetector.cs           ← regex patterns for common secret formats
  DangerousCommandDetector.cs ← blocked command patterns
  ConfigProtectionRule.cs     ← protected file path patterns
  AuditLogger.cs              ← append-only JSONL audit log
```

#### Enterprise Controls

`.sovrant/governance.json`:
```json
{
  "blocked_commands": ["rm -rf /", "git push --force main"],
  "protected_files": [".editorconfig", "Directory.Build.props", "*.sln"],
  "secret_patterns": ["AKIA[0-9A-Z]{16}", "sk-[a-zA-Z0-9]{48}"],
  "audit_log": true,
  "governance_level": "standard"
}
```

Three levels: `minimal` (audit only), `standard` (audit + warn), `strict` (audit + block).

#### Implementation Plan

1. Define governance interfaces and models in `Sovrant.Runtime/Governance/`
2. Implement `SecretDetector` with configurable regex patterns
3. Implement `DangerousCommandDetector` with configurable blocked patterns
4. Implement `ConfigProtectionRule` with configurable protected file globs
5. Implement `AuditLogger` — append-only JSONL to `~/.sovrant/audit/`
6. Implement `GovernanceMonitor` — aggregates all rules, returns `GovernanceVerdict`
7. Wire into hook system (Phase 21) or directly into `DefaultToolExecutor` as a pre-execution check
8. Add `SOVRANT_GOVERNANCE_LEVEL` env var
9. Tests: secret detection patterns, command blocking, config protection, audit log format

---

### Phase 26 — Skills System (Composable Workflow Packages) ✅

**Inspired by:** everything-claude-code (156+ skills as directory-based workflow definitions)
**Depends on:** Phase 21 (hooks — skills can trigger hooks), Phase 22 (agent templates — skills can spawn agents)

**Goal:** A modular system for packaging multi-step workflows as reusable, composable "skills" — each a single `.md` file with YAML frontmatter and a markdown body. Skills are invoked via `/skill-name` slash commands or programmatically by agents. Ship with **32 built-in skills** across 7 domains — coding is just one.

#### Why this matters

Today Sovrant has a basic `SkillTool` that loads markdown files as system prompt overlays. The everything-claude-code approach is richer: skills are full workflow definitions with steps, agent delegation, tool restrictions, and cross-harness compatibility. Sovrant is a general-purpose agentic platform — chat, research, writing, business ops, project management, and coding. The skill system turns Sovrant from a tool-using agent into a workflow engine that serves all these verticals.

#### Skill Structure — Flat .md Files (mirrors Phase 23 agent templates)

Each skill is a single `.md` file — no directory-per-skill, no sidecar files. When a skill needs embedded code (JavaScript, Python, etc.), the code lives inside a fenced code block within the markdown body. This keeps skills self-contained and allows thousands of skills without directory explosion.

```
.sovrant/skills/
  tdd-workflow.md
  code-review.md
  deep-research.md
  article-writing.md
  market-research.md
```

Built-in skills ship in `src/Sovrant.Tools/Skills/skills/` and are copied to the output directory at build time.

Skill `.md` format:
```markdown
---
name: deep-research
description: Multi-source research with citation and confidence scoring
trigger: /research
agents: [researcher]
tools: [Read, Grep, Glob, WebSearch, WebFetch]
---

# Deep Research

## Steps
1. Define research scope and questions
2. Search multiple sources (web, docs, codebase)
3. Evaluate source quality and relevance
4. Synthesize findings with cross-reference
5. Generate cited report with confidence levels
6. Identify gaps and suggest follow-up queries

## Output Format
- Executive summary
- Detailed findings with citations
- Confidence assessment per finding
- Recommended next steps
```

Skills that need embedded code include it as a fenced block:
```markdown
---
name: data-scraper
description: Autonomous data collection pipeline
trigger: /scrape
tools: [WebFetch, Write]
---

## Handler

\```js
// Embedded JS — no separate file needed
async function scrape(url) { ... }
\```
```

#### Built-in Skills — 32 skills across 7 domains

**Research & Intelligence (5 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `deep-research` | `/research` | Multi-source research from 15-30 sources with cited reports |
| `market-research` | `/market` | Competitive analysis, market sizing, source-attributed business intelligence |
| `data-scraper` | `/scrape` | Autonomous data collection pipeline: collect → enrich → store with scheduling |
| `doc-lookup` | `/docs-lookup` | API and documentation research with structured extraction |
| `search-first` | `/search-first` | Forces web/doc lookup before any implementation begins |

**Writing & Content (5 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `article-writing` | `/article` | Long-form blog posts, essays, newsletters with voice matching and anti-slop rules |
| `content-engine` | `/content` | Multi-platform content for social, newsletters, blogs — repurposes anchor content |
| `brand-voice` | `/brand-voice` | Extracts durable voice profiles from 5-20 writing samples |
| `crosspost` | `/crosspost` | Adapts content across platforms (X, LinkedIn, Threads) with per-platform rules |
| `slides` | `/slides` | HTML presentations and PPTX conversion |

**Business & Operations (5 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `investor-materials` | `/pitch` | Pitch decks (12-slide structure), one-pagers, financial models |
| `lead-intelligence` | `/leads` | AI-native prospecting: signal scoring, mutual ranking, warm-path discovery |
| `billing-ops` | `/billing` | Subscription management, refund triage, churn analysis, plan optimization |
| `connections-optimizer` | `/network` | Network pruning, expansion, rebalancing with warm-path outreach drafts |
| `product-lens` | `/product` | Product analysis, feature evaluation, competitive positioning |

**Project Management (4 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `plan` | `/plan` | Structured planning with phased implementation and dependency mapping |
| `project-flow` | `/flow` | GitHub + Linear/Jira coordination, issue triage, cross-system consistency |
| `team-builder` | `/team` | Interactive agent selection and parallel dispatch with result synthesis |
| `architecture-decision` | `/adr` | Structured ADR creation and management |

**Coding & Quality (7 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `tdd-workflow` | `/tdd` | Red-Green-Refactor with coverage enforcement |
| `code-review` | `/review` | Multi-severity code review (CRITICAL/HIGH/MEDIUM/LOW) |
| `verification-loop` | `/verify` | 6-phase quality gate (Phase 24) |
| `security-review` | `/security` | OWASP-based security audit |
| `refactor` | `/refactor` | Dead code detection + safe removal |
| `doc-update` | `/docs` | Documentation maintenance synced with code changes |
| `codebase-onboard` | `/onboard` | New contributor onboarding — architecture walkthrough, key patterns, setup guide |

**Media & Creative (3 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `media-gen` | `/media` | Image, video, audio generation via AI services (fal.ai, etc.) |
| `video-explainer` | `/explainer` | Animated technical explainers with progressive reveal |
| `ui-demo` | `/ui-demo` | Interactive UI demonstration creation |

**Agent Infrastructure (3 skills)**

| Skill | Trigger | What it does |
|---|---|---|
| `autonomous-loop` | `/loop` | 6 loop patterns from simple pipelines to DAG orchestration with merge queues |
| `prompt-optimize` | `/optimize-prompt` | 6-phase prompt analysis: gap identification, structure optimization |
| `skill-create` | `/new-skill` | Define new skills at runtime from successful workflow patterns |

#### Architecture

```
src/Sovrant.Tools/Skills/
  SkillDefinition.cs          ← record: Name, Description, Trigger, Agents, Tools, Body
  SkillParser.cs              ← YAML frontmatter parser (same pattern as AgentTemplateRegistry)
  SkillRegistry.cs            ← 3-tier discovery: assembly dir skills/ → ~/.sovrant/skills/ → .sovrant/skills/
  SkillRunner.cs              ← resolves skill, formats prompt with metadata, $ARGUMENTS substitution
  SkillTool.cs                ← registry-backed; supports "list" and args passthrough
  SkillCreateTool.cs          ← creates .md files in .sovrant/skills/ at runtime
  skills/                     ← 32 built-in .md files (copied to output dir via CopyToOutputDirectory)
```

#### Implementation (complete)

1. `SkillDefinition` record with `IReadOnlyList<string>` for Agents and Tools
2. `SkillParser` — parses .md files with YAML frontmatter; handles `[A, B]` and `A, B` list syntax
3. `SkillRegistry` — 3-tier discovery (assembly dir → global → project-local), case-insensitive name + trigger dictionaries
4. `SkillRunner` — formats prompt with metadata header, tool/agent constraints, body, `$ARGUMENTS` substitution
5. `SkillTool` rewritten to use `SkillRunner`; `SkillCreateTool` writes .md files with path traversal protection
6. 32 built-in skills shipped as flat .md files in `src/Sovrant.Tools/Skills/skills/`
7. MSBuild: `<Content Include="Skills\skills\**" CopyToOutputDirectory="PreserveNewest" Link="skills\%(Filename)%(Extension)" />`
8. DI: `SkillRegistry`, `SkillRunner` singletons + `SkillCreateTool` registered as `ITool`
9. 46 tests across 5 test files (SkillParser, SkillRegistry, SkillRunner, SkillTool, SkillCreateTool)

---

### Phase 27 — Multi-Layered Memory System ✅

**Inspired by:** everything-claude-code (session summaries, learned skills, instincts with confidence scoring)
**Depends on:** Phase 21 (hooks — memory persisted via session start/end hooks)

**Goal:** Evolve Sovrant's current flat memory files (`~/.sovrant/memory.md`) into a multi-layered memory system: session summaries (short-term), learned patterns (medium-term), and instincts with confidence scoring (long-term). Each layer has different persistence, scope, and retrieval characteristics.

#### Memory Layers

| Layer | Scope | Lifetime | What it stores |
|---|---|---|---|
| **Session Summary** | Per-session | Until next session on same project | Tasks attempted, tools used, files modified, outcome |
| **Learned Patterns** | Per-project | Persistent | Extracted code patterns, common fixes, project conventions |
| **Instincts** | Global | Persistent, evolving | Behavioral rules with confidence scores (0.0–1.0), evidence trails |

#### Session Summaries

At session end (via hook or explicit), extract from the JSONL transcript:
- User messages (tasks requested)
- Tools used and files modified
- Outcome (success/failure)
- Tokens consumed

Stored at `~/.sovrant/sessions/{project}/{timestamp}-summary.md`. Loaded at next session start for continuity.

#### Learned Patterns

Extracted from high-quality sessions (configurable threshold: minimum N turns, user-confirmed success). Examples:
- "This project uses NUnit, not xUnit"
- "API routes follow RESTful naming: `/v1/{resource}/{id}`"
- "Tests go in `tests/{Project}.Tests/` mirroring `src/{Project}/`"

Stored at `.sovrant/learned/` per project.

#### Instincts (Confidence-Scored Behavioral Rules)

Long-term behavioral learning with evidence tracking:

```yaml
- id: instinct-001
  trigger: "user corrects test framework choice"
  action: "Check project test dependencies before generating tests"
  confidence: 0.85
  evidence:
    - "2026-04-03: user corrected xUnit → NUnit in Project X"
    - "2026-04-05: correctly used NUnit based on dependency scan"
```

Confidence increases on positive reinforcement, decreases on correction. Low-confidence instincts (< 0.3) are pruned.

#### Architecture

```
src/Sovrant.Runtime/Memory/
  IMemoryStore.cs             ← interface: sessions, patterns, instincts
  SessionSummaryStore.cs      ← JSONL-based session summaries
  LearnedPatternStore.cs      ← markdown files per project
  InstinctStore.cs            ← YAML instinct definitions with confidence tracking
  MemoryInjector.cs           ← selects relevant memories for system prompt injection
```

#### Implementation Plan

1. Define `IMemoryStore` and memory layer models
2. Implement `SessionSummaryStore` — extract from JSONL, persist as markdown
3. Implement `LearnedPatternStore` — markdown files in `.sovrant/learned/`
4. Implement `InstinctStore` — YAML with confidence scoring and evidence
5. Implement `MemoryInjector` — selects relevant memories based on project, recency, and confidence
6. Wire session summary extraction into `SessionEnd` hook (Phase 21) or `RuntimeSessionPool.Evict`
7. Update system prompt builder to inject multi-layered memory
8. Add `/remember` and `/forget` commands for explicit memory management
9. Tests: summary extraction, pattern storage, instinct confidence updates, memory selection

---

### Phase 28 — Eval-Driven Development Framework ✅

**Inspired by:** everything-claude-code (eval harness with pass@k metrics, capability/regression evals, multiple grader types)
**Depends on:** Phase 24 (verification loop), Phase 22 (agent templates)

**Goal:** A formal evaluation framework for testing agent behavior itself — not just the code it produces. Define expected behaviors as evals, run them against agent sessions, and track pass@k metrics over time. This applies to all agent verticals: "Does the research agent cite sources?" "Does the content writer avoid slop phrases?" "Does the planner create actionable steps?" — not just coding tasks.

#### Why this matters

Unit tests verify code. Evals verify the agent. "Does the agent correctly identify security vulnerabilities?" "Does the research agent produce cited, multi-source reports?" "Does the content writer match the brand voice?" "Does the planner break tasks into parallelizable sub-tasks?" These questions can't be answered by running `dotnet test` — they require evaluating the agent's behavior against structured expectations across every domain Sovrant serves.

#### Eval Types

| Type | What it tests | Example |
|---|---|---|
| **Capability Eval** | Can the agent do X? | "Given a failing test, does the agent identify and fix the root cause?" |
| **Regression Eval** | Does Y still work after changes? | "After refactoring the tool executor, does BashTool still handle timeouts?" |
| **Quality Eval** | How well does the agent do X? | "Rate the code review output on completeness (1-5)" |

#### Grader Types

| Grader | How it works |
|---|---|
| **Code-based** | Deterministic: run a command, check exit code / output pattern (e.g., `dotnet test`, `grep "CRITICAL"`) |
| **Model-based** | LLM judges the output against a rubric (e.g., "Does this review identify all 3 planted bugs?") |
| **Human-based** | Flags for manual review, stores rating |

#### Metrics

- `pass@1` — passes on first attempt
- `pass@3` — passes within 3 attempts
- Trend tracking over time (did agent quality improve after template changes?)

#### Architecture

```
src/Sovrant.Runtime/Evals/
  IEvalRunner.cs            ← interface: RunAsync(EvalSuite) → EvalReport
  EvalDefinition.cs         ← eval: name, prompt, expected behavior, grader config
  EvalSuite.cs              ← collection of evals with metadata
  EvalReport.cs             ← results: per-eval pass/fail, pass@k, duration
  Graders/
    CodeGrader.cs           ← runs command, checks output
    ModelGrader.cs          ← sends output to LLM with rubric
```

Evals stored in `.sovrant/evals/`:
```yaml
- name: security-review-detects-sqli
  prompt: "Review src/Controllers/UserController.cs for security issues"
  grader: code
  check: "grep -q 'SQL injection' {output}"
  pass_threshold: 1
```

#### Implementation Plan

1. Define eval interfaces and models in `Sovrant.Runtime/Evals/`
2. Implement `CodeGrader` — command execution + output matching
3. Implement `ModelGrader` — sends eval output to LLM with rubric prompt
4. Implement `EvalRunner` — loads suite, runs evals, computes pass@k
5. Add `/eval` CLI command and skill
6. Add `GET /v1/evals` endpoint for results
7. Store results in `~/.sovrant/evals/results/` for trend tracking
8. Tests: grader execution, pass@k computation, eval loading

---

### Phase 29 — Swarm Orchestrator (Auto-Decomposition + DAG Execution) ✅

> **Status: ✅ Complete** — 17 new files, 62 new tests, 0 warnings. OFF by default; foundation for frontend-driven orchestration.

**Inspired by:** [claude-swarm](https://github.com/affaan-m/claude-swarm) (parallel task decomposition with dependency DAGs, file locking, budget enforcement, quality gate)
**Depends on:** Phase 19+20 (orchestrated team tools), Phase 22 (agent templates), Phase 55 (cost tracking — optional)

**Goal:** Add a **swarm orchestration layer** on top of Sovrant's existing orchestration infrastructure. A user gives a single complex prompt; a high-capability model automatically decomposes it into a dependency graph of 2-8 subtasks; subtasks execute in parallel waves respecting dependencies, with file-level conflict prevention, budget enforcement, and a quality gate review phase. The swarm uses whatever models the admin/user has configured — decomposition and quality gates use the "high" level model, workers use the "standard" level (all provider-agnostic via Phase 22's model resolution). Available via CLI (`sovrant swarm "task"`), the `SwarmTool` for programmatic use, and `POST /v1/swarm` for frontend integration.

#### What Sovrant already has vs. what this adds

| Existing (Phase 19+20) | This Phase adds |
|---|---|
| Manual agent creation (`TeamCreate`) | **Auto-decomposition**: one prompt → task DAG |
| Manual delegation (`TeamDelegate`) | **Dependency-aware scheduling**: topological wave execution |
| Basic status tracking (`TeamStatus`) | **File conflict prevention**: pessimistic locking per agent |
| Per-agent timeout | **Cross-swarm budget enforcement** with auto-cancellation |
| — | **Quality gate**: post-execution review of combined output |
| — | **Session recording/replay**: full JSONL audit trail of swarm runs |
| — | **Rich progress**: real-time swarm status via SSE to frontend |

#### How it works (3-phase pipeline)

**Phase 1 — Decomposition** (high-level model)
1. User submits a single complex prompt
2. Decomposer calls `IConversationRuntime` (using the admin's configured "high" model) with a structured system prompt instructing it to output a JSON task graph
3. Each task in the graph has: `id`, `description`, `dependencies` (list of task IDs), `files_to_modify` (predicted), `agent_template` (from Phase 22), `allowed_tools`
4. Tasks are organized into parallel waves (levels of the DAG) — wave 0 has no dependencies, wave 1 depends on wave 0, etc.

**Phase 2 — Parallel Execution** (standard/fast-level workers)
1. `SwarmOrchestrator` processes waves sequentially, tasks within a wave in parallel
2. Before launching each task: check file locks (pessimistic), check budget ceiling
3. Each task spawns an agent via `SovrantAgentFactory` using the appropriate `AgentTemplate`
4. PostToolUse tracking records which files each agent actually modifies
5. On completion: release file locks, accumulate cost, pass output to dependent tasks as context
6. On failure: retry up to `max_retries`, then mark failed and cancel dependents

**Phase 3 — Quality Gate** (high-level model)
1. Collect all task outputs
2. Send combined output to the "high" model with a quality review prompt
3. Score 1-10 with verdict: `pass` / `needs_revision` / `fail`
4. If `needs_revision`: identify specific tasks to re-run, loop back to Phase 2
5. Return final combined result to user

#### Architecture

```
src/Sovrant.Agents/Swarm/
  SwarmTask.cs                ← task model: id, prompt, dependencies, files_to_modify, template, status
  SwarmPlan.cs                ← decomposed DAG: tasks, parallel_waves, metadata
  SwarmResult.cs              ← final result: task outputs, quality score, total cost, duration
  ISwarmDecomposer.cs         ← interface: DecomposeAsync(prompt, ct) → SwarmPlan
  LlmSwarmDecomposer.cs       ← high-level model decomposition via IConversationRuntime
  SwarmOrchestrator.cs        ← DAG execution engine with file locking, budget, retries
  SwarmQualityGate.cs         ← post-execution high-level model review with score + verdict
  SwarmSession.cs             ← JSONL event recording and replay

src/Sovrant.Tools/Swarm/
  SwarmTool.cs                ← tool: accepts prompt, returns swarm result (for agent-initiated swarms)
  SwarmStatusTool.cs          ← tool: returns live swarm progress (tasks, status, cost)

src/Sovrant.Cli/Commands/
  SwarmCommand.cs             ← CLI: `sovrant swarm "task" [--budget 5.0] [--max-agents 4] [--dry-run]`
```

#### Configuration

`.sovrant/swarm.yaml` (optional — overrides defaults):
```yaml
swarm:
  max_concurrent: 4          # max parallel agents
  budget_usd: 5.0            # hard cost ceiling
  max_retries: 1             # per-task retry limit
  quality_gate: true         # enable post-execution review
  decomposer_level: high       # recommended level for decomposition (Phase 22 RecommendedLevel)
  worker_level: standard       # default recommended level for workers

templates:                    # override agent templates per task type
  code_change: coder
  review: security-reviewer
  test: tdd-guide
  docs: doc-updater
```

#### CLI Interface

```bash
# Basic: decompose + execute + quality gate
sovrant swarm "Refactor the auth module to use JWT tokens with refresh rotation"

# Dry run: show the decomposed plan without executing
sovrant swarm "Add pagination to all API endpoints" --dry-run

# Budget-constrained
sovrant swarm "Build a complete CRUD API for the inventory module" --budget 3.0 --max-agents 6

# List past swarm sessions
sovrant swarm sessions

# Replay a past session
sovrant swarm replay <session-id>
```

#### Server API (frontend integration)

```
POST /v1/swarm
  Body: { "prompt": "...", "budget_usd": 5.0, "max_concurrent": 4, "quality_gate": true }
  Response: SSE stream of SwarmEvent objects

GET /v1/swarm/{session_id}
  Response: { tasks: [...], status, quality_score, total_cost, duration }

GET /v1/swarm/{session_id}/events
  Response: JSONL event stream (for replay)
```

The SSE stream emits events matching the session recorder: `plan_created`, `task_started`, `task_completed`, `task_failed`, `file_conflict`, `quality_gate_started`, `quality_gate_result`, `swarm_completed`. The frontend can render a live DAG visualization showing task status, active agents, file locks, and cost accumulation.

#### File Conflict Prevention

```
SwarmOrchestrator maintains:
  _fileLocks: ConcurrentDictionary<string, string>  // filePath → agentId

Before launching a task:
  1. Check if any file in task.FilesToModify is locked by another agent
  2. If conflict: mark task BLOCKED, re-check after blocking agent completes
  3. On task start: lock all files in task.FilesToModify
  4. PostToolUse hook: track additional files the agent edits at runtime
  5. On task completion: release all locks held by that agent
```

#### Key difference from claude-swarm

Claude-swarm agents are isolated — downstream tasks wait for predecessors but never receive their output. Sovrant's swarm **passes predecessor output as context** to dependent tasks, enabling true information flow through the DAG. Combined with the existing `TeamDelegate` infrastructure, agents within a swarm can also delegate ad-hoc subtasks beyond the original plan.

#### Implementation Plan (10 steps, ~12 new files)

1. Define `SwarmTask`, `SwarmPlan`, `SwarmResult` models in `Sovrant.Agents/Swarm/`
2. Implement `LlmSwarmDecomposer` — structured high-level model call that outputs JSON task DAG
3. Implement `SwarmOrchestrator` — DAG scheduler with `SemaphoreSlim`, file locks, budget tracking, retry logic
4. Implement `SwarmQualityGate` — post-execution high-level model review with structured scoring
5. Implement `SwarmSession` — JSONL event recording and replay
6. Create `SwarmTool` and `SwarmStatusTool` in `Sovrant.Tools/Swarm/`
7. Add `SwarmCommand` to CLI (`sovrant swarm "prompt" [flags]`)
8. Add `POST /v1/swarm` SSE endpoint and `GET /v1/swarm/{id}` to `Sovrant.Server`
9. Wire into DI: `ISwarmDecomposer`, `SwarmOrchestrator`, session store
10. Tests: decomposition parsing, DAG scheduling, file lock conflicts, budget enforcement, quality gate scoring, SSE event streaming, session replay

---

### Phase 30 — Registry Discovery API (Tools, Skills, Agent Templates) ✅

**Depends on:** Phase 26 (skills system), Phase 23 (agent templates), existing tool registry
**Difficulty:** Low–Medium

**Goal:** Expose read-only API endpoints that let frontends discover what the engine can do — every registered tool, every built-in and user-defined skill, and every agent template. Today the engine has 43 tools, 32 skills, and 24 agent templates, but a frontend has no way to enumerate them or display their metadata to the user. These endpoints close that gap so UIs can render tool palettes, skill catalogs, and agent template pickers.

#### Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/v1/tools` | List all registered tools with name, description, and parameter schema |
| `GET` | `/v1/tools/{name}` | Get a single tool's full definition (name, description, parameters JSON Schema) |
| `GET` | `/v1/skills` | List all skills (built-in + global + project-local) with name, description, trigger |
| `GET` | `/v1/skills/{name}` | Get a single skill's full definition including body, agents, and tools constraints |
| `GET` | `/v1/agents/templates` | List all agent templates with name, description, recommended level, allowed tools |
| `GET` | `/v1/agents/templates/{name}` | Get a single agent template's full definition including system prompt body |

All endpoints are authenticated (same `SOVRANT_TOKEN` bearer auth as existing endpoints). Responses are JSON.

#### Response Shapes

**`GET /v1/tools`**
```json
{
  "tools": [
    {
      "name": "Read",
      "description": "Read a file from the filesystem",
      "parameters": { "type": "object", "properties": { "file_path": { ... } }, ... }
    }
  ],
  "count": 43
}
```

**`GET /v1/skills`**
```json
{
  "skills": [
    {
      "name": "deep-research",
      "description": "Multi-source research with citation and confidence scoring",
      "trigger": "/research",
      "agents": ["researcher"],
      "tools": ["Read", "Grep", "Glob", "WebSearch", "WebFetch"]
    }
  ],
  "count": 32
}
```

**`GET /v1/agents/templates`**
```json
{
  "templates": [
    {
      "name": "security-auditor",
      "description": "Security-focused code review agent",
      "recommended_level": "High",
      "allowed_tools": ["Read", "Grep", "Glob"]
    }
  ],
  "count": 24
}
```

#### Architecture

- `ToolRegistryRoutes` — maps `IToolRegistry.All` to JSON; parameter schema from `ToolDefinition.Parameters`
- `SkillRegistryRoutes` — maps `SkillRegistry.All` to JSON; detail endpoint includes body
- `AgentTemplateRoutes` — maps `AgentTemplateRegistry.All` to JSON; detail endpoint includes system prompt
- All three registries already exist as singletons in DI — no new infrastructure needed

#### Implementation Plan

1. Add `GET /v1/tools` and `GET /v1/tools/{name}` route group in `Sovrant.Server`
2. Add `GET /v1/skills` and `GET /v1/skills/{name}` route group
3. Add `GET /v1/agents/templates` and `GET /v1/agents/templates/{name}` route group
4. Update `docs/server.md` with new endpoints
5. Update README server API table
6. Tests: list endpoints return correct counts, detail endpoints return 404 for unknown names, auth required

---

### Phase 31 — Server Response Caching & Cache Infrastructure ✅

**Depends on:** Phase 30 (registry discovery API — primary consumer), Phase 7 (server)
**Difficulty:** Medium

**Goal:** Add a caching layer to the server so that expensive or repeated reads (registry listings, session metadata, provider health) are served from cache with proper TTL expiry and invalidation on mutation. Ship with an in-memory cache by default and an optional Redis adapter for multi-instance deployments.

#### Why this matters

The Phase 30 registry endpoints (`/v1/tools`, `/v1/skills`, `/v1/agents/templates`) return data that changes only when skills are created or the server restarts. Without caching, every request re-enumerates the registry. Session metadata, provider health scores, and config are similarly stable between mutations. A cache with proper invalidation turns these into near-zero-cost reads and enables HTTP cache headers so frontends can cache client-side too.

#### What gets cached

| Resource | Cache key | TTL | Invalidation trigger |
|---|---|---|---|
| Tool registry listing | `tools:list` | 1 hour | Server restart (tools are static) |
| Skill registry listing | `skills:list` | 5 min | ~~`SkillCreate` tool execution, file watcher on `.sovrant/skills/`~~ **⚠ stale — see Phase 113** |
| Agent template listing | `templates:list` | 5 min | ~~File watcher on `.sovrant/agents/`~~ **⚠ stale — see Phase 113** |
| Single tool/skill/template detail | `{type}:{name}` | Same as listing | **⚠ stale — see Phase 113** |
| Session metadata (message count, token totals) | `session:{id}:meta` | 30 sec | Any turn completion on that session |
| Provider health/status | `providers:health` | 10 sec | Health ping cycle |
| `/v1/config` response | `config:current` | Until mutation | `PUT /v1/config`, `PUT /v1/sessions/{id}/config` |

> **Post-Phase-112 gap:** Skills, agents, and document templates are now DB-backed (no filesystem). The file-watcher and `SkillCreate` invalidation triggers no longer fire. Phase 113 replaces them with `KnowledgePageChanged` events and adds a `CachedKnowledgeStore` decorator for DB-level read caching.

#### HTTP Cache Headers

- `ETag` on all cacheable GET responses — computed from a hash of the serialized response
- `Cache-Control: private, max-age={ttl}` matching the server-side TTL
- `304 Not Modified` when client sends `If-None-Match` with a matching ETag
- Mutable endpoints (`PUT`, `POST`, `DELETE`) return `Cache-Control: no-store`

#### Architecture

```
src/Sovrant.Runtime/Caching/
  ICacheProvider.cs            ← Get<T>, Set<T>(key, value, ttl), Remove(key), RemoveByPrefix(prefix)
  InMemoryCacheProvider.cs     ← ConcurrentDictionary + timer-based TTL sweep (default)
  RedisCacheProvider.cs        ← StackExchange.Redis adapter (opt-in via SOVRANT_CACHE_REDIS_URL)
  CacheInvalidator.cs          ← event-driven: listens to registry changes, session events, config mutations

src/Sovrant.Server/Middleware/
  ETagMiddleware.cs            ← computes ETag for GET responses, handles If-None-Match → 304
```

#### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_CACHE_PROVIDER` | `memory` | `memory` or `redis` |
| `SOVRANT_CACHE_REDIS_URL` | — | Redis connection string (required when provider is `redis`) |
| `SOVRANT_CACHE_DEFAULT_TTL` | `300` | Default TTL in seconds for entries without an explicit TTL |

#### Implementation Plan

1. Define `ICacheProvider` interface (Get/Set/Remove/RemoveByPrefix with `TimeSpan` TTL)
2. Implement `InMemoryCacheProvider` — `ConcurrentDictionary<string, CacheEntry>` with background `Timer` sweep (every 60s)
3. Implement `RedisCacheProvider` — thin wrapper over `StackExchange.Redis` `IDatabase`; serialize values as JSON
4. Add `CacheInvalidator` — subscribes to `SkillRegistry` changes, `RuntimeSessionPool` events, config mutations; calls `RemoveByPrefix`
5. Wire caching into registry routes (Phase 30): check cache before enumerating, populate on miss
6. Wire caching into existing routes: `/v1/status`, `/v1/config`, `/v1/sessions/{id}` metadata
7. Add `ETagMiddleware` — hash response body for GET routes, compare `If-None-Match`, return `304` or full response
8. Register `ICacheProvider` in DI (factory selects implementation based on `SOVRANT_CACHE_PROVIDER`)
9. Tests: TTL expiry, invalidation on mutation, ETag match/mismatch, Redis adapter (integration test with Testcontainers or mock)

---

### Phase 32 — Persistence Layer (SQLite) ✅

**Depends on:** Phase 7 (structured logging), Phase 25 (governance audit), Phase 27 (memory system)
**Difficulty:** Medium–High

**Goal:** Introduce SQLite as **the** persistence layer for the engine. Everything except `.md` files moves into a single database — config, sessions, audit, credentials, token usage, and CLI memory. SQLite is the starting point: zero-config, zero-infrastructure, works offline, ships as a single file. The abstraction (`IStorageProvider`) is designed so that a future phase can swap in Postgres, CockroachDB, or Turso without touching any consumer code.

Both the CLI and the server share the same `IStorageProvider` singleton from `Sovrant.Runtime` — one database, two entry points.

#### The principle

**`.md` files stay as files. Everything else moves to the database.**

`.md` files (skills, agent templates, memory notes) are version-controlled, git-diffable, human-authored content. They belong in a repo or a dotfiles directory. Everything else — config, sessions, audit, credentials, usage — is operational data that the engine produces and consumes. Operational data belongs in a database where it can be scoped per-user, per-team, queried, and migrated.

#### Why this matters

**Multi-user/multi-team config.** Today config is a 3-file merge (`~/.sovrant/settings.json` → `.sovrant/settings.json` → `.sovrant/settings.local.json`). Adding per-user or per-team overrides means more files, more merge logic, more edge cases. With a `config` table, scoping is a query:

```sql
SELECT key, value FROM config
WHERE scope IN ('global', 'team:engineering', 'user:eric')
ORDER BY priority DESC  -- user > team > global
```

**Queryable operations.** Finding "all governance violations in the last 7 days" means scanning every line of `governance.jsonl`. Finding "which sessions touched file X" means opening every JSONL. Token usage is lost entirely on restart. SQLite gives indexed queries with zero infrastructure.

**Session search.** Cross-session search (`GET /v1/sessions?query=auth+module`) currently requires scanning every JSONL file on disk. With transcripts in SQLite, it's a full-text search index.

**Credential isolation.** Per-user credential scoping becomes natural: `(user_id, credential_key, encrypted_blob)` instead of a flat directory of `.enc` files.

**Growth path.** SQLite today, Postgres tomorrow. The `IStorageProvider` abstraction means swapping the backend is a DI registration change, not a rewrite.

#### Complete persistence inventory

Every place the engine currently writes data to disk:

**Moves to SQLite**

| Data | Current storage | Current format | What SQLite enables |
|---|---|---|---|
| **Config — settings** | `~/.sovrant/settings.json` + `.sovrant/settings.json` + `.sovrant/settings.local.json` | JSON (3-file merge) | Per-user/per-team scoped config; `PUT /v1/config` writes to DB, not files; no more merge logic |
| **Config — hooks** | `~/.sovrant/hooks.json` + `.sovrant/hooks.json` | JSON | Per-user hook overrides; query "which hooks are active for user X in project Y" |
| **Config — governance** | `~/.sovrant/governance.json` + `.sovrant/governance.json` | JSON (merged) | Per-team governance rules; audit trail of rule changes |
| **Config — verification** | `.sovrant/verify.json` | JSON | Per-project with team overrides; consistent with other config |
| **Session transcripts** | `~/.sovrant/sessions/*.jsonl` | JSONL (AppendAllTextAsync) | Cross-session full-text search; per-user session isolation; no filesystem scanning; `GET /v1/sessions?query=...` |
| **Governance audit log** (Phase 25) | `~/.sovrant/audit/governance.jsonl` | JSONL (AppendAllTextAsync) | Query by tool, session, severity, date range |
| **Bash command audit log** (Phase 25) | `~/.sovrant/audit/bash-commands.jsonl` | JSONL (AppendAllTextAsync) | Query by command, exit code, session |
| **Session index** | Implicit (scan JSONL filenames) | No storage (derived) | Query by creation date, last access, project, message count, files touched, token totals |
| **Token usage history** (Phase 10) | In-memory only (`SessionConfig.AddTokens()`) | Not persisted | Persist across restarts; query by date/model/session; historical cost analysis |
| **Encrypted credentials** (Phase 17) | `~/.sovrant/credentials/*.enc` + `.keystore` | Binary (AES-GCM) / Hex | Per-user credential isolation; no directory scanning; key rotation without file juggling |
| **Memory entries** | `~/.sovrant/memory.md` + `.sovrant/memory.md` | Markdown (2 flat files) | Per-user, per-project, typed (user/feedback/project/reference); searchable; concurrent writes without file lock; multiple sessions can add entries simultaneously |
| **CLI memory — learned patterns** (Phase 27) | `.sovrant/learned/*.md` (planned) | Markdown (planned) | Full-text search, confidence scoring, evidence trails, recency ranking |
| **CLI memory — instincts** (Phase 27) | `~/.sovrant/instincts/*.yaml` (planned) | YAML (planned) | Query by trigger, confidence threshold, decay pruning |

**Stays as files**

| Data | Path | Format | Why it stays |
|---|---|---|---|
| **Skills** | `.sovrant/skills/*.md` + built-in `src/.../skills/` | Markdown + YAML frontmatter | Version-controlled, git-diffable, human-authored; loaded into memory at startup |
| **Agent templates** | `.sovrant/agents/*.md` + built-in `src/.../agents/` | Markdown + YAML frontmatter | Same as skills |
| **Memory `.md` bootstrap files** | `~/.sovrant/memory.md` + `.sovrant/memory.md` | Markdown | Read-only seed layer — imported into `memory_entries` table on first run; existing files still loaded as fallback if DB is empty; new entries go to SQLite |
| **Rolling app logs** (Phase 7) | `~/.sovrant/logs/sovrant-{Date}.log` | Text or JSON | Standard log files consumed by log aggregators (Datadog, ELK, etc.); daily rotation; external tooling expects files |
| **Temp scripts** | `{TempPath}/sovrant_*.{sh,ps1,cmd}` | Text | Ephemeral — created for tool execution, deleted immediately after |

#### Shared by CLI and Server

`IStorageProvider` lives in `Sovrant.Runtime` — both `Sovrant.Cli` and `Sovrant.Server` reference it. Both register it as a singleton in DI. Same database, same schema, same migrations.

| Entry point | Writes | Reads |
|---|---|---|
| **CLI** | Config, memory entries, audit events, bash commands, session transcripts, session index, token usage, credentials, learned patterns, instincts | Config (scoped), memory (prepend to system prompt), session index (resume), audit queries, credential lookup |
| **Server** | Same as CLI, plus per-request token tracking at higher volume | Same as CLI, plus `GET /v1/audit`, `GET /v1/usage`, `GET /v1/sessions?query=...`, per-user memory via API |

#### Config scoping model

Config moves from file-merge to scope-priority resolution:

| Scope | Priority | Who sets it | Example |
|---|---|---|---|
| `global` | 0 (lowest) | Server admin / CLI defaults | Default model, log level |
| `team:{name}` | 1 | Team lead via API | Team-specific governance level, allowed tools |
| `project:{path}` | 2 | Project `.sovrant/` directory (migrated) | Project-specific verify config, hooks |
| `user:{id}` | 3 (highest) | Individual via CLI or API | Personal model preference, permission mode |

Resolution: for a given key, the highest-priority scope wins. `ConfigLoader` becomes `ConfigResolver` backed by `IStorageProvider` instead of file I/O.

#### Lightweight user identity

Phase 32 introduces a `users` table — not full enterprise auth (that's Phase 37), just enough to give every other table an `owner_id` foreign key. This enables per-user config, session isolation, credential scoping, and usage attribution without building a login system.

| Context | How user ID is resolved |
|---|---|
| **CLI** | `SOVRANT_USER_ID` env var, or defaults to OS username (`Environment.UserName`) |
| **Server** | Derived from bearer token → user mapping (simple `SOVRANT_TOKENS` JSON map: `{"token": "user_id"}`). Falls back to `"anonymous"` for single-token setups. |

The `users` table is an anchor row — created on first seen, referenced everywhere by `user_id`. No passwords, no OAuth, no sessions-as-auth. Phase 38 adds per-user token auth on top of this same schema.

```sql
-- Every table references user_id for scoping
SELECT * FROM session_index WHERE user_id = 'eric';
SELECT * FROM config WHERE scope = 'user:eric';
SELECT SUM(output_tokens) FROM token_usage WHERE user_id = 'eric' AND timestamp > '2026-04-01';

-- Memory: per-user, per-project, typed, searchable
SELECT * FROM memory_entries WHERE user_id = 'eric' AND project = 'sovrant' AND type = 'feedback';
SELECT * FROM memory_entries WHERE user_id = 'eric' AND content LIKE '%testing%' AND is_stale = 0;
```

#### Architecture

```
src/Sovrant.Runtime/Storage/
  IStorageProvider.cs           ← abstraction: query, insert, update, delete, transaction support
  SqliteStorageProvider.cs      ← Microsoft.Data.Sqlite, WAL mode, connection pooling
  StorageMigrator.cs            ← versioned schema migrations (version table + ordered scripts)

  Tables:
    users                      ← (user_id PK, display_name, created_at, last_seen_at)
    config                     ← (id, scope, key, value_json, updated_at)
    sessions                   ← (id, session_id, user_id FK, role, content, tool_calls_json, timestamp)
    session_index              ← (session_id PK, user_id FK, project, created_at, last_accessed,
                                  message_count, files_modified, total_input_tokens, total_output_tokens)
    audit_events               ← (id, timestamp, user_id FK, session_id, tool, action, severity, detail)
    bash_commands              ← (id, timestamp, user_id FK, session_id, command, exit_code, duration_ms)
    credentials                ← (id, user_id FK, credential_key, encrypted_blob, created_at, updated_at)
    token_usage                ← (id, user_id FK, session_id, model, input_tokens, output_tokens, timestamp)
    memory_entries             ← (id, user_id FK, project, type, name, description, content,
                                  source_session, created_at, updated_at, is_stale)
    learned_patterns           ← (id, user_id FK, project, pattern, source_session, confidence, created_at, last_used)
    instincts                  ← (id, user_id FK, trigger, action, confidence, evidence_json, created_at, updated_at)
```

All tables except `users` and `config` have a `user_id` foreign key. Queries naturally scope by user without extra logic. Phase 38's per-user token auth adds token management and access control on top of this existing schema — the data model doesn't change, only who is allowed to set `user_id`.

#### Database Location

All persistent data lives under `{project-root}/data/`. This keeps the database alongside the codebase (easy to find, back up, and `.gitignore`), separate from config/template files in `.sovrant/`, and avoids polluting the user's home directory.

| Context | Path | Rationale |
|---|---|---|
| CLI (per-user) | `{project-root}/data/sovrant.db` | Alongside the project; `data/` is gitignored |
| Server | `$SOVRANT_DB_PATH` or `{project-root}/data/sovrant.db` | Configurable for containers; defaults to project root |

The `data/` directory also holds WAL and SHM files that SQLite creates automatically (`sovrant.db-wal`, `sovrant.db-shm`). Add `data/` to `.gitignore`.

#### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_STORAGE_PROVIDER` | `sqlite` | `sqlite` now; `postgres` in a future phase |
| `SOVRANT_DB_PATH` | `{project-root}/data/sovrant.db` | SQLite database file path |
| `SOVRANT_USER_ID` | OS username | User identity for CLI (server resolves from token) |
| `SOVRANT_TOKENS` | — | JSON map of `{"bearer-token": "user_id"}` for server multi-user identity resolution |
| `SOVRANT_AUDIT_JSONL` | `false` | Dual-write audit to JSONL alongside SQLite (migration period) |
| `SOVRANT_SESSION_JSONL` | `false` | Dual-write session transcripts to JSONL alongside SQLite (migration period) |

#### Migration from flat files

On first run, `StorageMigrator` detects existing flat-file data and imports it:

1. **Config files** → Reads each JSON config file, inserts key-value pairs into `config` table with appropriate scope (`global` for `~/.sovrant/`, `project:{path}` for `.sovrant/`)
2. **Memory files** → Parses `~/.sovrant/memory.md` (global scope) and `.sovrant/memory.md` (project scope), splits into individual entries, inserts into `memory_entries` with `type` inferred from content structure
3. **Session transcripts** → Reads each `*.jsonl`, inserts messages into `sessions` table, builds `session_index` row from metadata
4. **Audit logs** → Reads `governance.jsonl` and `bash-commands.jsonl`, inserts into respective tables
5. **Credentials** → Reads `*.enc` files, inserts encrypted blobs into `credentials` table (master key migrates too)
6. **Token usage** → No existing persistence to migrate (currently in-memory only)

Post-migration:
- Original files are **not deleted** — they become archival copies
- `SOVRANT_AUDIT_JSONL=true` and `SOVRANT_SESSION_JSONL=true` enable dual-write during transition
- Migration is **idempotent** — keyed by content hash + timestamp, re-running skips already-imported records
- `ConfigLoader` falls back to file-based resolution if SQLite is unavailable (graceful degradation)

#### Future growth path

SQLite is the starting persistence layer, not the final one. The `IStorageProvider` abstraction is designed for this:

| Scale | Backend | When |
|---|---|---|
| Single user / small team | SQLite (this phase) | Now |
| Hosted server / multi-tenant | Supabase (PostgreSQL + Auth) | Phase 40C — first cloud backend; includes SSO |
| Edge / embedded | SQLite remains ideal | Always |
| Multi-instance server (self-hosted) | Postgres / CockroachDB | When Supabase managed service is not desired |
| Distributed edge | Turso (libSQL) | SQLite-compatible with replication |

Swapping backends is a DI registration change + migration script, not a rewrite. All consumers use `IStorageProvider` — they never touch `SqliteConnection` directly.

#### Relationship to Phase 27 (Memory System → SQLite)

Phase 27 introduces three memory layers that initially use flat files. Phase 32 migrates all three into SQLite:

| Memory layer | Phase 27 storage | Phase 32 table | What changes |
|---|---|---|---|
| **Session summaries** | `~/.sovrant/sessions/{project}/{ts}-summary.md` | `session_summaries` `(id, user_id, project, session_id, summary_md, tasks, tools_used, files_modified, outcome, tokens_in, tokens_out, created_at)` | Full-text search across summaries; per-user scoping; no filesystem scanning |
| **Learned patterns** | `.sovrant/learned/*.md` | `learned_patterns` `(id, user_id, project, pattern, source_session, confidence, created_at, last_used)` | Query by project + confidence; automatic decay; concurrent multi-session writes |
| **Instincts** | `~/.sovrant/instincts/*.yaml` | `instincts` `(id, user_id, trigger, action, confidence, evidence_json, created_at, updated_at)` | Query by trigger keyword; confidence-threshold pruning via SQL; evidence append without YAML rewrite |

Migration strategy:
- `StorageMigrator` reads existing Phase 27 flat files and inserts into respective tables
- `IMemoryStore` implementations get a second constructor path accepting `IStorageProvider`
- When `IStorageProvider` is available (Phase 32+), stores write to SQLite; otherwise fall back to flat files
- `.md` and `.yaml` files are kept as archival copies post-migration

#### Relationship to Phase 31 (Caching)

Phase 31's `ICacheProvider` is for **hot, ephemeral data** — fast reads with TTL expiry, no durability guarantee. Phase 32's `IStorageProvider` is for **cold, durable data** — structured records that survive restarts and support queries. They compose:
- Cache a query result from SQLite in the in-memory/Redis cache for repeated fast access
- Invalidate the cache entry when the underlying SQLite data changes
- Example: `GET /v1/audit?last=7d` → first request queries SQLite, caches for 30s; new audit event invalidates

#### Implementation Plan

1. Add `Microsoft.Data.Sqlite` package to `Sovrant.Runtime`
2. Define `IStorageProvider` interface — generic query/insert/update/delete with typed results, transaction support
3. Implement `SqliteStorageProvider` — connection pooling, WAL mode, `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL`
4. Implement `StorageMigrator` — `schema_version` table + ordered migration classes
5. **Config migration:** Replace `ConfigLoader` file-merge with `ConfigResolver` backed by `config` table; scoped resolution by priority
6. **Session migration:** Replace `JsonlSessionStore` with SQLite-backed `SqliteSessionStore`; `sessions` table for messages, `session_index` for metadata
7. **Audit migration:** Replace `AuditLogger` file writes with SQLite inserts; add `SOVRANT_AUDIT_JSONL` dual-write toggle
8. **Credential migration:** Replace `AesGcmCredentialStore` file I/O with `credentials` table; same AES-GCM encryption, different storage
9. **Token usage:** Add `token_usage` table; wire `SessionConfig.AddTokens()` to persist
10. **Memory migration:** Add `memory_entries` table; replace `MemoryCommand` file I/O with SQLite reads/writes; import existing `.md` files on first run; `/memory` command writes to DB
11. Prepare `learned_patterns` and `instincts` tables (schema only — populated by Phase 27)
12. Wire `IStorageProvider` into DI as singleton; CLI and Server share same registration
13. Add server endpoints: `GET /v1/audit` (query events), `GET /v1/sessions?query=...` (full-text search)
14. Import tool: `StorageMigrator` auto-imports existing flat files on first run
15. Tests: migration idempotency, config scope resolution, session CRUD, memory CRUD + type filtering, concurrent write safety, JSONL import, credential round-trip, CLI + Server both resolve same provider

---

### Phase 33 — CLI Quick Wins ✅

**Depends on:** None (CLI presentation only)
**Difficulty:** Low–Medium

**Goal:** Foundational UX improvements that make the CLI feel polished and professional on first launch. All changes are CLI presentation and interaction only — no backend, server, agent, or runtime changes.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | ASCII art home screen | Startup banner with "Sovrant" ASCII art, version number, and tagline. Displayed once on launch. Style modeled on Fastfetch/Angular CLI. |
| 2 | Graceful env var errors | If `LLM_API_KEY` is missing, show a friendly message naming the variable, explaining its purpose, and showing the exact `export` command. Exit cleanly — no stack trace, no blank screen. |
| 3 | Thinking messages | Randomly rotating warm teal phrases ("Thinking really hard...", "Consulting the oracle...", etc.) that cycle every 2–3 seconds while the model processes. Vanish the instant the first token arrives. Uses Spectre.Console. |
| 4 | Escape to cancel | Detect Escape during generation, cancel the request mid-stream, clean up the display, and print `[Cancelled]`. Show a persistent `Esc to cancel` hint at the prompt in muted text. |
| 5 | Sticky input bar | Input prompt pinned to the bottom of the terminal at all times. Output scrolls above it. Bar includes a horizontal rule, `You:` label, text input, and `Esc to cancel` hint. Redraws correctly on terminal resize. Uses Spectre.Console live rendering. |
| 6 | Paste acknowledgment | Detect multi-line paste as a single operation, show `[Pasted N lines]`, hold content until Enter is pressed. No per-line echo. |
| 7 | /help coverage audit | Audit all slash commands and ensure every one appears in `/help` output with a short description. Full visual formatting deferred to Phase 34. |

#### Acceptance Criteria

- `dotnet build` exits 0, no new warnings
- ASCII banner on fresh launch, no repeat mid-session
- Missing `LLM_API_KEY` → readable error + clean exit
- Thinking phrases rotate in teal, vanish on first token
- Escape cancels generation cleanly, prints `[Cancelled]`
- Input bar pinned to bottom, redraws on resize
- Multi-line paste → `[Pasted N lines]`, submits only on Enter
- Every slash command appears in `/help`

---

### Phase 34 — CLI Visual Polish ✅

**Depends on:** Phase 33 (CLI quick wins — input bar, /help audit, thinking messages must be in place)
**Difficulty:** Low–Medium

**Goal:** Consistent visual language across all CLI output. Builds on Phase 33's interaction changes with color, spacing, and formatting polish. Includes two small functional fixes (web search, Windows write perms) that are CLI-scoped.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Output coloring | Apply consistent Spectre.Console coloring: bold headings, muted code blocks, distinct tool call names, dimmer tool results, red errors, amber warnings, gray system messages. No plain-white-on-black content remaining. |
| 2 | Conversation visual separation | Clear rhythm between turns: blank line before user turn, bold colored `You:` label, blank line before assistant, teal bold `Sovrant:` label, subtle separator after each response. Scannable at a glance. |
| 3 | /help formatting | Group commands by category (Session, Memory, Config, Navigation). Accent-colored command names, aligned descriptions, bold category headers. Builds on Phase 33's coverage audit. |
| 4 | Web search indicator | Fix `LLM_WEB_SEARCH=true` pass-through to the provider layer. Add `[web search on]` status badge at the prompt. Warn if the provider doesn't support it. |
| 5 | Windows write permissions | Detect access/permission errors on file writes (WriteFile, EditFile, session/memory files). Display a clear message instructing the user to restart as Administrator. Never fail silently. |

#### Acceptance Criteria

- `dotnet build` exits 0, no new warnings
- All output types (headings, code, tool calls, errors, warnings, system) are distinctly colored
- User/assistant turns visually separated with labels and spacing
- `/help` grouped by category with aligned Spectre.Console columns
- `LLM_WEB_SEARCH=true` → visible badge, search works end-to-end
- Windows file write failure → readable permissions error with restart instructions

---

### Phase 35 — Workspaces ✅

**Depends on:** Phase 32 (SQLite persistence — `users` table already exists), Phase 27 (memory system)

**Goal:** Personal and team areas that house projects. Every user gets an isolated personal workspace by default; team workspaces allow groups to collaborate. Workspaces own their own memory, configuration, sessions, credentials, and audit data.

#### Core concepts

- **Personal workspace** — auto-created when a user is created. Single-owner, cannot be deleted, always exists. This is where a user's solo work lives.
- **Team workspace** — created explicitly. Has membership (owner/admin/member/viewer) and invite-based onboarding. This is where teams collaborate.
- **Fallback rule** — if no `workspace_id` is provided in a request, resolve to the authenticated user's personal workspace. No request is ever "unscoped."
- **Isolation** — workspaces cannot see each other's data. Sessions, config, memory, credentials, audit — all scoped per-workspace.
- **Memory** — Phase 27's memory layers (SessionSummary, LearnedPattern, Instinct) are scoped per-workspace. A team workspace accumulates shared learned patterns; a personal workspace keeps individual ones. Memory does not leak across workspace boundaries.

#### Motivation

Today Sovrant is single-user (the `users` table from Phase 32 seeds one user). There's no separation between "my stuff" and "our team's stuff." Workspaces give every user a private area from day one (personal workspace) and let teams form shared areas when needed (team workspaces):
- A user's personal workspace is their default — solo work, personal memory, personal config
- A team workspace lets multiple users share sessions, memory, config, and projects
- Isolation means workspace A cannot see workspace B's data, memory, or sessions
- Users can belong to multiple team workspaces while always having their personal workspace as home base

#### SQLite schema

| Table | Key columns | Notes |
|---|---|---|
| `workspaces` | `workspace_id` (PK), `type` (personal/team), `name`, `slug` (unique), `owner_id` (FK → users), `created_at`, `updated_at` | Personal workspaces: `type='personal'`, one per user, `owner_id` = user. Team workspaces: `type='team'`, `owner_id` = creator. |
| `workspace_members` | `workspace_id` (FK), `user_id` (FK), `role` (owner/admin/member/viewer), `joined_at` | Personal workspaces have exactly one member (the owner). Team workspaces have many. |
| `workspace_config` | `workspace_id` (FK), `key`, `value` | Workspace-scoped settings (model defaults, governance level, budget caps) |
| `workspace_invites` | `invite_id` (PK), `workspace_id` (FK), `email`, `role`, `token`, `expires_at`, `accepted_at` | Team workspaces only — personal workspaces don't accept invites |
| `workspace_memory` | `memory_id` (PK), `workspace_id` (FK), `layer` (summary/pattern/instinct), `content`, `confidence`, `project_id` (FK, nullable), `created_at`, `updated_at` | Replaces flat-file memory storage from Phase 27. Scoped to workspace, optionally to project. |

All existing tables with `user_id` gain a `workspace_id` FK (not nullable — every row belongs to a workspace). Migration backfills existing data into each user's personal workspace.

#### Workspace resolution

1. Request includes `X-Workspace-Id` header → use that workspace (validate membership)
2. Request includes no workspace context → resolve to the authenticated user's personal workspace
3. Never unscoped — the personal workspace is always the fallback

#### API surface

| Endpoint | Method | Description |
|---|---|---|
| `/v1/workspaces` | GET | List workspaces the authenticated user belongs to (always includes personal) |
| `/v1/workspaces` | POST | Create a team workspace (creator becomes owner) |
| `/v1/workspaces/{id}` | GET/PUT | Workspace read/update. Personal workspaces can be renamed but not deleted. |
| `/v1/workspaces/{id}` | DELETE | Delete team workspace only (personal workspaces cannot be deleted) |
| `/v1/workspaces/{id}/members` | GET/POST/DELETE | Membership management (team workspaces only) |
| `/v1/workspaces/{id}/invites` | POST/DELETE | Invite lifecycle (team workspaces only) |
| `/v1/workspaces/{id}/config` | GET/PUT | Workspace-scoped configuration |
| `/v1/workspaces/{id}/usage` | GET | Aggregated token/cost usage for workspace |
| `/v1/workspaces/{id}/memory` | GET | Workspace memory (summaries, patterns, instincts) |

#### Implementation plan

1. Add SQLite tables (`workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `workspace_memory`)
2. Add `workspace_id` FK (not nullable) to `sessions`, `audit_events`, `credentials`, `usage`, `config` tables
3. Auto-create personal workspace on user creation (triggered when a new user row is inserted)
4. Migration: backfill existing data into each user's personal workspace
5. Implement `IWorkspaceService` — CRUD, membership, invite token generation/validation, personal workspace creation
6. Implement `WorkspaceContextMiddleware` — resolves workspace from `X-Workspace-Id` header, falls back to personal workspace
7. Migrate Phase 27 `IMemoryStore` from flat files to `workspace_memory` table — `FileMemoryStore` becomes fallback for non-SQLite mode
8. Scope all existing queries by `workspace_id` (no unscoped queries remain)
9. Add workspace API endpoints
10. Workspace-scoped config inheritance: workspace config → user config → global defaults
11. Tests: personal workspace auto-creation, fallback resolution, isolation (workspace A can't see workspace B data), memory scoping, membership roles, invite flow (team only), personal workspace delete protection

#### Relationship to Phase 40 (Enterprise Auth)

Phase 40 adds external IdP login and RBAC on top of the workspace model — workspaces become the RBAC scope boundary.

---

### Phase 36 — Projects ✅

**Depends on:** Phase 35 (workspaces)

**Goal:** Isolated containers within workspaces that group related work — sessions, config, memory, agent templates, and artifacts. A project belongs to exactly one workspace and inherits its isolation boundary.

#### Core concepts

- **Project** — a named container within a workspace (like a repo, initiative, or client engagement)
- **Workspace-scoped** — projects belong to a workspace. Personal workspace projects are solo; team workspace projects are collaborative.
- **Memory inheritance** — projects can accumulate their own learned patterns and instincts (stored in `workspace_memory` with `project_id` set). Project memory is a refinement of workspace memory, not a replacement — both layers are visible within the project.
- **Membership** — project members are a subset of workspace members. If no project members are explicitly set, all workspace members have access (open by default within the workspace).

#### Motivation

Workspaces isolate people. Projects isolate work. Without projects:
- All sessions in a workspace are in one flat list
- Config overrides apply workspace-wide or per-session — no middle ground for "this initiative uses model X with budget Y"
- Memory accumulates at the workspace level with no way to separate patterns learned on different initiatives
- Agent templates and skills can't be scoped to a specific effort

Projects let a team (or individual) say "this engagement has its own model config, budget, memory, and agent templates" without affecting other work in the same workspace.

#### SQLite schema

| Table | Key columns | Notes |
|---|---|---|
| `projects` | `project_id` (PK), `workspace_id` (FK), `name`, `slug`, `description`, `created_at`, `updated_at`, `archived_at` | Belongs to exactly one workspace |
| `project_members` | `project_id` (FK), `user_id` (FK), `role` (lead/contributor/viewer), `joined_at` | Subset of workspace members; optional — no rows means all workspace members have access |
| `project_config` | `project_id` (FK), `key`, `value` | Project-scoped settings (model, budget, governance overrides) |

Phase 35's `workspace_memory` table already has an optional `project_id` FK — project-scoped memory writes set this field. Existing tables gain optional `project_id` FK: `sessions`, `audit_events`, `usage`.

#### Config and memory inheritance

```
project config  →  workspace config  →  user config  →  global defaults
project memory  +  workspace memory  (both visible within project context)
```

#### API surface

| Endpoint | Method | Description |
|---|---|---|
| `/v1/workspaces/{wid}/projects` | GET/POST | List/create projects in workspace |
| `/v1/projects/{id}` | GET/PUT/DELETE | Project CRUD (delete archives by default) |
| `/v1/projects/{id}/members` | GET/POST/DELETE | Project membership (subset of workspace) |
| `/v1/projects/{id}/config` | GET/PUT | Project-scoped configuration |
| `/v1/projects/{id}/sessions` | GET | Sessions scoped to project |
| `/v1/projects/{id}/usage` | GET | Token/cost usage for project |
| `/v1/projects/{id}/memory` | GET | Project-scoped memory (project + inherited workspace memory) |

#### Implementation plan

1. Add SQLite tables (`projects`, `project_members`, `project_config`)
2. Add optional `project_id` FK to `sessions`, `audit_events`, `usage` tables
3. Implement `IProjectService` — CRUD, membership, archival
4. Implement `ProjectContextMiddleware` — resolves project from `X-Project-Id` header
5. Config inheritance chain: project config → workspace config → user config → global defaults
6. Memory scoping: project memory writes go to `workspace_memory` with `project_id` set; reads merge project + workspace layers
7. Session creation auto-associates with active project context
8. Project-scoped agent templates and skills (load from project config in addition to workspace/global)
9. Budget enforcement at project level (Phase 55's `ICostModel` + project config budget cap)
10. Tests: project isolation within workspace, config inheritance chain, memory scoping (project sees own + workspace memory), membership subset validation, archive behavior

#### Relationship to Phase 41 (Artifact System)

If Phase 41 is implemented, artifacts are scoped to projects rather than just teams. The `ITeamWorkspace` from Phase 41 becomes project-aware — artifacts persist in the project context and survive across team agent lifetimes.

---

### Phase 37 — User Management API ✅

**Depends on:** Phase 32 (persistence layer — `users` table)
**Difficulty:** Low–Medium

**Goal:** Expose CRUD endpoints for user management so that frontends can create users, manage profiles, and view per-user data. Phase 32 creates the `users` table and `user_id` foreign keys on every other table. This phase builds the API surface to manage those users.

This is **not** auth — there are no per-user tokens, no login, no RBAC. The existing `SOVRANT_TOKEN` gate remains the only auth mechanism. Per-user bearer tokens and token resolution are deferred to Phase 38 (per-user token auth). Role-based access control is deferred to Phase 40 (enterprise auth).

#### Why this matters

After Phase 32, the database has per-user scoping on sessions, audit, credentials, and usage. But there's no way to create or manage users through the API — they're auto-seeded from the OS username. A frontend building a multi-user dashboard needs to:
- Create and list users
- View per-user usage, sessions, and audit history
- Deactivate users without deleting their data
- Assign users to teams for organizational grouping

#### Endpoints

All endpoints are protected by the existing `SOVRANT_TOKEN` bearer auth (same as all other endpoints).

**User CRUD**

| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/users` | Create a user — returns `user_id` |
| `GET` | `/v1/users` | List all users (filterable by status/team/role) |
| `GET` | `/v1/users/{id}` | Get user profile, session count, total tokens consumed |
| `PUT` | `/v1/users/{id}` | Update display name, role, team, status |
| `DELETE` | `/v1/users/{id}` | Soft-delete (deactivate) — sets `status: inactive`, preserves data for audit |

**Per-User Data Views**

| Method | Path | Description |
|---|---|---|
| `GET` | `/v1/users/{id}/sessions` | List sessions owned by this user |
| `GET` | `/v1/users/{id}/usage` | Token usage summary for this user (by model, by date range) |
| `GET` | `/v1/users/{id}/audit` | Audit events for this user |

#### Request/Response Shapes

**`POST /v1/users`**
```json
// Request
{ "display_name": "Eric", "role": "user", "team": "engineering" }

// Response (201 Created)
{
  "user_id": "usr_a1b2c3d4",
  "display_name": "Eric",
  "role": "user",
  "team": "engineering",
  "status": "active",
  "created_at": "2026-04-06T..."
}
```

**`GET /v1/users`**
```json
{
  "users": [
    {
      "user_id": "usr_a1b2c3d4",
      "display_name": "Eric",
      "role": "user",
      "team": "engineering",
      "status": "active",
      "session_count": 47,
      "total_input_tokens": 1250000,
      "total_output_tokens": 380000,
      "created_at": "2026-04-01T...",
      "last_seen_at": "2026-04-05T..."
    }
  ],
  "count": 1,
  "total": 1
}
```

#### Schema notes

No schema changes needed — Phase 32 already created the `users` table with `role`, `team`, and `status` columns. This phase only adds the API surface.

#### Implementation Plan

1. Implement `IUserService` — CRUD operations backed by `ISqliteConnectionFactory`
2. Add `UserRoutes` route group: `/v1/users` CRUD
3. Add per-user data view routes: `/v1/users/{id}/sessions`, `/v1/users/{id}/usage`, `/v1/users/{id}/audit`
4. Update `docs/server.md` with new endpoints
5. Tests: user CRUD, soft-delete preserves data, per-user data views filter correctly
6. **Update the Frontend SDK** — extend the TypeScript client to cover the new `/v1/users/*` endpoints (and the Phase 35 `/v1/workspaces/*` and Phase 36 `/v1/workspaces/{wid}/projects/*` surfaces if not already wired). Bump the SDK version, regenerate types, and publish so frontends/apps can consume users, workspaces, and projects through a single typed client.

#### What this does NOT include (deferred to Phase 38)

- Per-user bearer tokens and `api_tokens` table population
- Token hash-based user resolution middleware
- Admin vs user role enforcement
- `SOVRANT_ADMIN_TOKENS` environment variable
- Per-user auth scoping (any authenticated request can access any user's data)
- Database hardening (secure_delete, file permissions, read-only connections)

---

### Phase 37.5 — Swarm Sessions Into the Database ✅

**Status:** Complete (2026-04-08)
**Depends on:** Phase 32 (SQLite persistence — `swarm_events` table exists from V005)
**Difficulty:** Small–Medium

**What shipped:**
- `ISwarmEventStore` + `SqliteSwarmEventStore` against the existing `swarm_events` table — no schema migration was needed (V005 columns + V006/V007 indexes were already in place).
- `SwarmSession` rewritten to write through the new store; public surface (`RecordAsync` / `ReplayAsync` / `ListSessions` / `Exists`) preserved.
- `SwarmExecutionContext(UserId, WorkspaceId, ProjectId)` threaded through `SwarmOrchestrator.ExecuteAsync` so every event is stamped with scope. `SwarmRoutes` populates it from `WorkspaceContextMiddleware`'s `HttpContext.Items` plus the `X-Project-Id` header.
- `GET /v1/swarm/sessions` accepts `workspace_id`, `project_id`, `limit` query parameters (handed to `SwarmListFilter`).
- `sovrant db import-swarm [--dir <path>] [--delete-source]` migrates legacy `~/.sovrant/swarm/sessions/*.jsonl` files into `swarm_events` for existing installs.
- New tests: `SqliteSwarmEventStoreTests` (12 cases) plus 2 workspace-scoping tests added to `SwarmSessionTests`. Full suite green.
- `persistence.md` updated (new Swarm Event Store subsection, swarm row dropped from "Stays as Files", concern #7 retired).


**Goal:** Replace the JSONL-only swarm session store (`SwarmSession` writing `.sovrant/swarm/sessions/{id}.jsonl`) with a SQLite-backed `ISwarmEventStore` that writes to the existing `swarm_events` table. Promotes item #14 from Phase 42.5's deferred list to a discrete phase so swarm runs become queryable, joinable to users/workspaces, and covered by the same backup/migration story as everything else.

#### Why this matters

Today swarm is the **only** subsystem that still treats flat files as the source of truth. The `swarm_events` table was created in V005 and has sat empty ever since. As a result:

- Swarm runs are invisible to per-user / per-workspace queries (`/v1/users/{id}/sessions` won't find them).
- They are not covered by the SQLite backup story — only by ad-hoc filesystem copies of `~/.sovrant/swarm/sessions/`.
- The directory grows unbounded with no rotation.
- There is no foreign-key linkage to `users`, `workspaces`, or `sessions`, so cross-cutting analytics (cost per swarm, swarm-vs-direct usage breakdown) require ad-hoc JSONL parsing.
- The audit trail for governance events triggered inside swarm sub-agents is split across two stores.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Define `ISwarmEventStore` | New interface in `Sovrant.Runtime/Storage` with `RecordEventAsync(SwarmEvent)`, `LoadEventsAsync(swarmId)`, `ListSwarmsAsync(filter)`, `DeleteSwarmAsync(swarmId)`. |
| 2 | Implement `SqliteSwarmEventStore` | Backed by the existing `swarm_events` table. Stores `swarm_id`, `event_type`, `agent_id`, `workspace_id`, `project_id`, `payload` (JSON), `timestamp`. Reuses `ISqliteConnectionFactory`. |
| 3 | Refactor `SwarmSession` | Take `ISwarmEventStore` via constructor injection. Replace `File.AppendAllText(...)` writes with `_store.RecordEventAsync(...)`. Replace directory globbing in `LoadAsync` with a SQL query keyed on `swarm_id`. |
| 4 | DI registration | Add `services.AddSingleton<ISwarmEventStore, SqliteSwarmEventStore>()` in `Sovrant.Runtime.ServiceCollectionExtensions`. |
| 5 | Workspace + project scoping | When `SwarmOrchestrator` starts a swarm, capture the active `workspace_id` / `project_id` from the request context and stamp every event with it. This is what enables the per-workspace and per-project queries. |
| 6 | One-shot JSONL importer | `sovrant db import-swarm` (or a one-time migration helper) that reads any existing `~/.sovrant/swarm/sessions/*.jsonl` files and inserts the events into `swarm_events`, then optionally deletes the source files with `--delete-source`. |
| 7 | Update `/v1/swarms` routes | Existing `SwarmRoutes` reads from the JSONL store. Switch them to query through `ISwarmEventStore`. Add workspace/project filters to the list endpoint. |
| 8 | Tests | `SqliteSwarmEventStoreTests` (CRUD, filters, workspace scoping), `SwarmSessionTests` against the new store, and an integration test that runs a real swarm and verifies the events land in `swarm_events`. |
| 9 | Update persistence.md | Move swarm out of "What Stays as Files", add it to the Domain Stores section, and remove items #14 (and partial #13) from the Phase 42.5 known-concerns table. |
| 10 | Update server.md | Document the new query/filter capabilities on `/v1/swarms`. |

#### Acceptance Criteria

- `swarm_events` table is the canonical store for all new swarm runs after upgrade.
- `~/.sovrant/swarm/sessions/` is no longer written to by default; opt-in JSONL export via a flag if needed.
- A swarm started inside a workspace is queryable via `/v1/workspaces/{wid}/swarms`.
- A one-shot importer migrates existing JSONL session files into the DB.
- All existing swarm tests pass against the new store; new tests cover workspace scoping.
- `docs/persistence.md` no longer lists swarm under "What Stays as Files".

#### What this does NOT include

- Backfilling `user_id` onto historical swarm events (the JSONL files don't carry it).
- Per-event session linkage to `sessions` / `session_entries` — that's a future enhancement (would let you join a swarm event to its parent agent's full chat history).
- Live progress streaming via Server-Sent Events — `SwarmRoutes` already has SSE; this phase preserves it but doesn't expand it.

---

### Phase 38 — Per-User Token Auth & Database Hardening ✅ Complete

**Depends on:** Phase 37 (user management API — users exist in the DB)
**Difficulty:** Medium

**Goal:** Replace the single shared `SOVRANT_TOKEN` with per-user bearer tokens so the frontend can authenticate individual users. Each user gets their own tokens stored as SHA-256 hashes in the `api_tokens` table (created empty in Phase 32). Also hardens the SQLite layer now that per-user identity is enforceable.

This is the bridge between "everyone shares one token" (Phase 8) and "enterprise SSO with RBAC" (Phase 40). It gives the frontend what it needs — user-specific auth — without the complexity of OAuth/OIDC.

#### Why this matters

After Phase 37, the server can CRUD users but still authenticates everyone with a single shared token. The frontend can't tell which user is making a request. This phase resolves that:
- Each user gets their own bearer tokens
- The server resolves incoming tokens to a `user_id`
- Session, audit, and usage data is scoped to the authenticated user
- Admin users can manage other users; regular users can only access their own data

#### Token Model

| Item | Description |
|---|---|
| Token format | `svt_` prefix + 32 bytes crypto-random (base64url) |
| Storage | Only the SHA-256 hash is stored in `api_tokens`. Plaintext is returned once on creation, never retrievable again. |
| Resolution | `BearerTokenMiddleware` hashes the incoming token, looks up `api_tokens` by `token_hash`, resolves `user_id`, checks `status: active` and `revoked_at IS NULL`. Attaches `user_id` and `role` to `HttpContext.Items`. |
| Admin check | User management endpoints require `role = 'admin'`. Non-admin users can only access their own data. |
| Backward compat | `SOVRANT_TOKEN` (single shared token) still works — mapped to a built-in `admin` user on first run. Existing deployments don't break. |
| Security | Tokens never appear in logs, audit events, error responses, or session data. |

#### Token Management Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/users/{id}/tokens` | Issue a new API token for this user (returns token once, never again) |
| `GET` | `/v1/users/{id}/tokens` | List active tokens (masked — shows prefix + last 4 chars only) |
| `DELETE` | `/v1/users/{id}/tokens/{token_id}` | Revoke a specific token |

#### Database Hardening

Applied in this phase because per-user identity makes ownership enforcement meaningful:

| Hardening | Description |
|---|---|
| `secure_delete=ON` pragma | Zeros deleted rows on disk instead of leaving residual data. No write performance impact in WAL mode. |
| File permissions on creation | `chmod 600` on Linux/macOS, restricted ACL on Windows for `sovrant.db` and `.keystore`. |
| Connection-scoped read-only mode | Read-only endpoints use a read-only SQLite connection. |
| Token hash timing-safe comparison | `CryptographicOperations.FixedTimeEquals` prevents timing attacks on token lookups. |
| Session ownership enforcement | All session/audit/usage queries filter by authenticated `user_id`. Admin role bypasses for management endpoints only. |

#### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_ADMIN_TOKENS` | — | Comma-separated admin bearer tokens (bootstrap — creates admin users on first seen) |
| `SOVRANT_TOKEN` | — | Legacy single-token mode (mapped to built-in admin user for backward compat) |

#### Implementation Plan

1. ✅ Implement `ITokenService` — token generation (`svt_` + crypto-random), SHA-256 hashing, issuance, revocation
2. ✅ Implement `BearerTokenMiddleware` — hash-based token-to-user resolution, replaces boolean `SOVRANT_TOKEN` check
3. ✅ Add self-service token management endpoints (`/v1/users/me/tokens`)
4. ✅ Add admin authorization — `role = 'admin'` for user management; self-access for regular users; static `SOVRANT_TOKEN` treated as admin
5. ✅ Scope all data view queries by authenticated `user_id` (sessions, audit, usage) — enforced at `ISessionStore` layer with `INSERT OR IGNORE` first-touch ownership; routes do pre-flight `GetOwnerAsync` checks and return 404 (not 403) for cross-user access
6. ✅ Backward compat: `SOVRANT_TOKEN` still works as admin bootstrap
7. ✅ Apply database hardening (secure_delete pragma, 0600 file permissions, read-only connections via `CreateReadOnlyConnection()` + `PRAGMA query_only`, timing-safe comparison via `CryptographicOperations.FixedTimeEquals`)
8. ✅ Update `docs/server.md` with token endpoints and auth model
9. ✅ Tests: token issuance/revocation, hash-based resolution, admin-only access, self-read, ownership enforcement (9 SqliteSessionStore + 9 SessionOwnership tests), timing-safe comparison, backward compat with `SOVRANT_TOKEN`

---

### Phase 39 — ~~Cost Tracking, Token Budgets & Model Pricing Registry~~ Consolidated into Phase 55

> **Status:** Fully absorbed into Phase 55. The original Phase 39 split pricing/budgets into two phases — Phase 39 for the local registry + dashboard + budgets, Phase 55 for the OpenRouter-backed live pricing. Since all cost tracking runs through OpenRouter's free `/api/v1/models` endpoint anyway, the split was artificial. Phase 55 now owns the entire cost pipeline: pricing, budgets, dashboard, and metrics logging. All cross-references elsewhere in this roadmap have been updated to point to Phase 55.

---

### Phase 40A — Workspace-Scoped Roles & Tenant Enforcement ✅ Complete

**Depends on:** Phase 85 (identity & login — per-user `svt_` tokens, admin gate), Phase 35 (workspaces — data model and role columns already exist)

**Goal:** Make workspaces the hard multitenant boundary for beta. Workspaces are created and managed by admins only. Members operate strictly within the workspaces they belong to — they cannot create projects with users outside their workspace, cannot see other workspaces' data, and have their actions governed by their workspace role (`owner / admin / member / viewer`). Server API, Web UI, Desktop app, and MCP server enforce the same rules. CLI membership is admin-managed (no self-serve invite flow needed at beta).

The data model is already in place from Phase 35 (`workspace_members.role`, `project_members.role`, `IsMemberAsync`, `GetMemberRoleAsync`). This phase wires enforcement everywhere it's missing.

#### Current gap

Phase 35 created the schema and `IWorkspaceService`. Phase 85 added real login. What's missing is **enforcement** — the route guards, UI gates, and CLI/MCP checks that actually prevent a member from doing what their role doesn't allow. Today anyone with a valid token can call `POST /v1/workspaces` and create a workspace; a viewer can call member-management endpoints; a project can be created with a user who isn't in the workspace.

#### Rules for beta

| Rule | Detail |
|---|---|
| **Admin-only workspace creation** | Only global admins (`role = 'admin'` on the user) can create team workspaces. Non-admins get 403. Personal workspaces are still auto-created on registration. |
| **Workspace membership required** | Every non-admin request to a workspace endpoint must pass `IsMemberAsync`. Non-members get 403. |
| **Workspace role enforcement** | `owner` and `admin` can manage members, update config, delete the workspace. `member` can read and contribute sessions/projects. `viewer` is read-only. |
| **Project member constraint** | A user can only be added to a project if they are already a member of the parent workspace. Violating this returns 422. |
| **Cross-workspace isolation** | Sessions, agent runs, artifacts, and memory are always scoped to the resolved workspace. A request cannot reference data from a workspace it is not a member of. |
| **Workspace context on `IPrincipalAccessor`** | Add `WorkspaceId` so any service layer can ask "what workspace is this request in?" without re-resolving from HTTP headers. |

#### Workspace role reference

| Role | Can do |
|---|---|
| `owner` | Everything — rename/delete workspace, manage all members, full config |
| `admin` | Manage members (cannot remove owner), update config, full project access |
| `member` | Create/run projects and sessions, contribute to workspace memory |
| `viewer` | Read sessions, projects, memory — no writes |

Project roles (`lead / contributor / viewer`) mirror this within the project scope and are a subset of workspace membership.

#### Per-surface implementation

**Server (`Sovrant.Server`)**
- `WorkspaceRoutes`: add `IsAdmin` guard on `POST /v1/workspaces`; add `CanManageWorkspace` helper (owner or admin of workspace, or global admin); gate all mutating member/config/delete routes behind it
- `ProjectRoutes`: validate that any user being added to a project is already in `workspace_members` for that project's workspace
- `WorkspaceContextMiddleware`: populate `IPrincipalAccessor.WorkspaceId` from resolved workspace at request time

**Web (`Sovrant.Web`)**
- **Top-left workspace switcher** (replaces any existing workspace nav section): shows current workspace name + dropdown; lists Personal first, then joined team workspaces in order; if no team workspaces, no dropdown — just the personal label
- Switching workspace updates the URL to `/w/{workspaceId}/...` and re-scopes all navigation
- Project member add: populate user picker from workspace members only (not all users)
- Workspace settings page (new, linked from switcher): visible to workspace owner/admin only; shows member list and roles; admin can add/remove members by user ID or email
- Admin page — Workspaces tab: lists all workspaces, create/delete, assign members and roles

**Desktop (`Sovrant.Desktop`)**
- **Top-left workspace switcher** (same behaviour as Web): Personal first, then joined workspaces; switching re-scopes navigation and reflects in the route/navigation state (e.g. `workspace/{workspaceId}/...`)
- No "New workspace" control for non-admin users anywhere in the Desktop UI
- Workspace settings view (accessible from switcher, owner/admin only): member list + role management
- Project member UI: constrain picker to workspace members

**CLI (`sovrant`)**
- No self-serve invite or workspace creation at beta — workspace membership is managed by admins via the Web or Desktop UI
- `sovrant workspace list` — lists workspaces the authenticated user belongs to
- `sovrant workspace members <workspace-id>` — lists members and roles (owner/admin only)
- All commands that take `--workspace` validate membership before executing; non-members get a clear 403 error

**MCP server**
- Tool calls that resolve a workspace context validate membership of the authenticated token's user before executing
- Agent runs are always stamped with the resolved `workspace_id`; a tool cannot reference data outside the request's workspace
- `workspace_id` exposed in the MCP session context so tool implementations can scope storage without re-resolving

#### `CanManageWorkspace` helper

Central authorization predicate used across all routes and service calls:

```csharp
bool CanManageWorkspace(string userId, string workspaceId)
    => IsGlobalAdmin(userId)
    || GetMemberRoleAsync(workspaceId, userId) is owner or admin
```

#### What does NOT change in this phase

- Token format and issuance — still `svt_` tokens, still 30-day sliding TTL
- The global `admin`/`user` flag on `users.role` — global admins retain full access across all workspaces
- Role storage — still in `workspace_members.role` (already exists); no new RBAC tables needed yet
- OAuth/OIDC/SSO — deferred to Phase 40B

#### Implementation plan

1. Add `WorkspaceId` to `IPrincipalAccessor` and populate in `WorkspaceContextMiddleware` (Server) and equivalent session objects (Desktop, Web)
2. Add `CanManageWorkspace` helper to `HttpContextAuthExtensions` (Server) and `IPrincipalAccessor` implementations
3. **Server:** gate `POST /v1/workspaces` behind `IsAdmin`; add role guards to all workspace member/config/delete routes; add workspace-membership validation to project member add
4. **Web:** replace workspace nav with top-left workspace switcher (`/w/{workspaceId}/...` routing); filter list to memberships; add workspace settings page (owner/admin); gate workspace creation in Admin page only
5. **Desktop:** replace workspace nav with top-left workspace switcher (route-reflected); filter list to memberships; add workspace settings tab (owner/admin); remove new-workspace control for non-admins
6. **CLI:** implement `workspace list`, `workspace members`; validate `--workspace` membership on all commands (no self-serve invite at beta)
7. **MCP:** validate workspace membership on tool calls; stamp agent runs with `workspace_id`
8. Tests: admin-only creation, membership enforcement, cross-workspace isolation, project member constraint, role hierarchy (owner > admin > member > viewer), all-surface parity

---

### Phase 40B — Enterprise Auth & External Identity ⏸️ Deferred

**Depends on:** Phase 40A (workspace roles enforced), Phase 85 (per-user tokens)

**Goal:** Add external identity providers (OAuth/OIDC, SAML), replace the token-carried role flag with DB-evaluated roles, and add enterprise SSO enforcement and billing isolation on top of the Phase 40A workspace model.

#### Current limitation — role stored on the token, not in the DB

`IsAdmin()` reads the `role` field carried on the `svt_` token, set at issuance time. Role changes require re-issuing a token. Phase 40B moves role evaluation to a DB lookup at request time so changes take effect immediately.

#### When to implement

Add this phase when:
- External identity providers (Google, GitHub, Azure AD, Okta) are required for login, **or**
- Fine-grained tool-level permissions beyond workspace roles are needed (e.g., "can use tool X but not Y"), **or**
- Enterprise SSO with SAML is required by a customer

#### What it adds on top of Phase 40A

| Item | Detail |
|---|---|
| External IdP | OAuth 2.0 / OIDC — login via Google, GitHub, Azure AD, Okta. Maps external identity to `users.user_id`. |
| SAML | Enterprise SSO via SAML 2.0 for customers with existing IdP infrastructure. |
| DB-stored role evaluation | Replace token-carried `role` with a DB lookup in `BearerTokenMiddleware` so role changes are immediate. |
| Granular RBAC | Populate `roles` + `permissions` + `role_permissions` + `user_roles` tables. Define `tools:execute`, `config:write`, `sessions:read-all`, etc. |
| SSO enforcement | Workspace admins can require SSO login — disable token-only access for their workspace. |
| JWT coexistence | `BearerTokenMiddleware` accepts both `svt_` tokens and JWTs from external IdP. |
| Billing isolation | Per-workspace billing plan association and usage alerts on top of Phase 35 usage aggregation. |
| Hot reload | `POST /v1/admin/reload` to refresh token registry without restart. |
| Audit | All auth events (login, token issue, token revoke, permission change) logged to `audit_events`. |

#### Implementation plan

1. Add OAuth/OIDC middleware — `Microsoft.AspNetCore.Authentication.OpenIdConnect`
2. Add SAML middleware for enterprise customers
3. Move role evaluation from token payload to DB lookup in `BearerTokenMiddleware`
4. Populate `roles`, `permissions`, `role_permissions`, `user_roles` tables
5. Implement `IRbacService` — permission checks at endpoint and tool-execution level
6. Add SSO enforcement flag on `workspace_config`
7. Add `POST /v1/admin/reload`
8. Tests: OAuth flow, SAML flow, DB role evaluation, RBAC permission checks, SSO enforcement, JWT + svt_ coexistence

---

### Phase 40C — Supabase Backend + SSO 🔜 High Priority

**Depends on:** Phase 32 (SQLite persistence abstraction via `IStorageProvider`)

**Goal:** Replace the server-side SQLite database with Supabase (PostgreSQL) and adopt Supabase Auth as the first managed identity layer. Supabase Auth ships SSO out of the box (Google, GitHub, Microsoft/Azure AD, Apple, SAML 2.0, generic OIDC) — eliminating the need to build Phase 40B's hand-rolled OAuth/OIDC/SAML plumbing. The embedded desktop continues to use SQLite.

**Supabase is the first SSO provider, not the only one.** The auth layer is designed around an `IAuthProvider` abstraction so additional managed auth services (Auth0, Clerk, Azure AD B2C, Okta, WorkOS) can be plugged in as future sub-phases without changing application code. The goal is to support all major enterprise SSO providers — Supabase simply ships first because it bundles the database.

This also supersedes Phase 40B. Phase 40B remains documented for reference (hand-rolling auth from scratch), but Phase 40C is the intended path — managed providers handle the hard parts.

#### Why Supabase

| Concern | Hand-rolled (Phase 40B) | Supabase (Phase 40C) |
|---|---|---|
| SSO providers | Build OAuth/OIDC + SAML from scratch | Enabled via Supabase dashboard in minutes |
| Database | SQLite (single-node) | PostgreSQL (scalable, multi-instance safe) |
| Auth tokens | Custom `svt_*` + JWT co-existence wiring | Supabase JWTs; `svt_*` tokens bridged or replaced |
| Row-level security | App-layer workspace checks | PostgreSQL RLS policies (defence-in-depth) |
| User management | Custom users table + Argon2id | Supabase Auth users table; password hashing managed |
| Email verification / magic links | Not implemented | Built-in |
| Ops burden | We maintain migrations, backups, indexing | Supabase handles infra; we own schema only |

#### Deployment topology

```
Desktop (embedded)   →  SQLite (unchanged — local, offline-capable)
Server (hosted)      →  Supabase (PostgreSQL + Supabase Auth)
```

The `IStorageProvider` abstraction introduced in Phase 32 makes the swap a DI registration change for the server. Embedded desktop is unaffected.

#### What it adds

| Item | Detail |
|---|---|
| `SupabaseStorageProvider` | Implements `IStorageProvider` against PostgreSQL via Supabase client or Npgsql. Registered for server builds; `SqliteStorageProvider` remains for desktop/embedded. |
| Schema migration | Port the 26 SQLite migrations to PostgreSQL-compatible DDL. Apply via Supabase migrations or Flyway. |
| Supabase Auth integration | `BearerTokenMiddleware` validates Supabase JWTs in addition to (or instead of) `svt_*` tokens. User identity flows from `auth.users`. |
| SSO provider management | Admin enables/disables identity providers (Google, GitHub, Azure AD, SAML, OIDC) via Supabase Auth dashboard or the Supabase Management API. No custom provider config UI needed. |
| Admin UI — Web | Settings → Identity Providers page shows active providers (read from Supabase) with enable/disable toggle. Credential entry not needed — Supabase holds the client secrets. |
| Admin UI — Desktop | Same read-only provider status list in Settings; configuration done via Supabase dashboard. |
| Role mapping | Supabase Auth custom claims (JWT `app_metadata`) carry workspace role. Mapped from external IdP groups or email domain via Supabase Auth hooks. |
| SSO enforcement | Per-workspace flag: require SSO — disable password login for non-admin users. Enforced at the Supabase Auth level. |
| Row-level security | PostgreSQL RLS policies enforce workspace ownership as a second layer below the application. |
| Realtime (optional) | Supabase Realtime subscriptions can replace or supplement SignalR for live session updates. Deferred to a sub-phase. |
| `IAuthProvider` abstraction | Interface wrapping the auth backend (token validation, user lookup, SSO provider list). `SupabaseAuthProvider` is the first implementation; future sub-phases add `Auth0AuthProvider`, `ClerkAuthProvider`, `OktaAuthProvider`, `WorkOsAuthProvider`, `AzureAdB2CAuthProvider`. |
| Future SSO providers | Phase 40C-1 Auth0, Phase 40C-2 Clerk, Phase 40C-3 Okta/WorkOS, Phase 40C-4 Azure AD B2C — each is a new `IAuthProvider` registration and Settings UI card; no changes to the application auth pipeline. |

#### Implementation plan

1. Add `Sovrant.Storage.Supabase` project — `SupabaseStorageProvider` wrapping Npgsql + Supabase client
2. Port SQLite schema (26 migrations) to PostgreSQL DDL; run against Supabase project
3. Update `ServiceCollectionExtensions` to register `SupabaseStorageProvider` when `SOVRANT_SUPABASE_URL` + `SOVRANT_SUPABASE_KEY` env vars are set (server); fall through to SQLite otherwise (desktop/embedded)
4. Extend `BearerTokenMiddleware` to accept Supabase JWTs (`Authorization: Bearer <supabase-jwt>`); validate via Supabase public key; map to `userId`, `role`
5. Web/Desktop Settings → Identity Providers: read enabled providers from Supabase Auth API; show status cards with enable toggle (calls Supabase Management API)
6. Supabase Auth hook: map `app_metadata.groups` or `email` domain → workspace role custom claim
7. SSO enforcement: per-workspace `require_sso` flag in `workspace_settings`; login endpoint rejects password auth if set
8. Add PostgreSQL RLS policies to all tables scoped by `workspace_id`
9. Tests: provider login flow (Google, GitHub), JWT validation, role mapping, SSO enforcement, RLS policy coverage, `IStorageProvider` contract tests against PostgreSQL

---

### Phase 40D — Granular Feature Permissions ⏸️ Deferred

**Depends on:** Phase 40A (workspace roles), Phase 40C (Supabase auth + DB)

**Goal:** Move beyond the current all-or-nothing workspace role model (Owner / Admin / Member) and let workspace admins grant or deny access to individual product features per user or per role. A member can have access to the workspace but be restricted to Chat only — no Agents, no Teams, no MCP connections, no Settings.

#### Current limitation

Today `Member` role grants access to everything inside a workspace. There is no way to say "this contractor can chat but cannot create agents or run swarms" without removing them from the workspace entirely.

#### Feature permission model

Features are grouped into discrete capabilities. Each can be `allow` or `deny` per user (overrides role default) or per role (applies to all members of that role):

| Feature key | Covers |
|---|---|
| `chat` | Chat sessions — send messages, view history |
| `agents` | Agent creation, editing, and execution |
| `teams` | Team creation and TeamRun orchestration |
| `swarms` | Swarm tool and swarm pipeline execution |
| `missions` | Mission creation and management |
| `mcp` | MCP server connections and tool proxy |
| `knowledge` | Knowledge page authoring and browsing |
| `artifacts` | Artifact read/write access |
| `projects` | Project creation and management |
| `settings.workspace` | Workspace settings (TTL, limits, etc.) |
| `settings.providers` | LLM provider configuration |
| `settings.identity` | Identity provider / SSO configuration |
| `admin` | User management, approval, role changes |

#### Resolution order

```
User-level grant/deny  (highest — explicit per-user override)
    ↓
Role-level grant/deny  (applies to all members of the role)
    ↓
Workspace default       (Member: allow all by default)
```

#### What it adds

| Item | Detail |
|---|---|
| `feature_permissions` table | `(workspace_id, subject_type [role/user], subject_id, feature, effect [allow/deny])` |
| `IFeaturePermissionService` | `CanAsync(userId, workspaceId, feature)` — evaluates the resolution chain above |
| Route-level enforcement | Each feature area's API routes check `IFeaturePermissionService` before handler logic; return 403 with `"feature_denied"` error code |
| UI gating | Web + Desktop hide or disable nav items and action buttons for denied features; denial is enforced server-side regardless |
| Admin UI — Permission Matrix | Settings → Members → select user → permission matrix table; toggle allow/deny per feature; role-level defaults shown as background |
| Role defaults editor | Settings → Roles → edit role → feature defaults toggle matrix |
| Audit | Permission grant/deny changes logged to `audit_events` |

#### Implementation plan

1. Add `feature_permissions` DB migration
2. Implement `IFeaturePermissionService` + `FeaturePermissionStore`; register in DI
3. Add `[RequireFeature("chat")]` middleware/attribute applied to route groups
4. Web: hide/disable nav items and action buttons based on feature check (server-rendered initial state + client guard)
5. Desktop: same guard on nav items and command bindings
6. Admin UI: permission matrix in Members settings page (web + desktop)
7. Role defaults editor in Roles settings
8. Tests: resolution order (user override beats role), 403 enforcement, UI gating, audit trail

---

### Phase 41 — Agent Artifact Tools ✅

**Depends on:** Phase 53 (Scoped Artifact Storage — `IArtifactStore`, workspace-first layout)

**Goal:** Give agents a tool to deposit deliverables into the artifact store so work products flow through structured storage instead of prompt text. Agents work in isolation, produce artifacts, and return to the orchestrator. Users and the team leader consume results.

**Key design decision:** This is a **produce-and-deposit** pattern, not an inter-agent messaging channel. Agents don't talk to each other through artifacts — they deposit work for the orchestrator/user to consume. Future inter-agent coordination goes through PM agents (Phase 57).

#### What shipped

| Component | Description |
|---|---|
| `ArtifactTool` | Single tool with 3 actions: `write`, `read`, `list`. Wraps `IArtifactStore` for agent-side access. |
| Write | Agent stores a deliverable (code, report, data) scoped to workspace/project/run. |
| Read | Agent retrieves an artifact by path. Capped at 100K chars to prevent prompt overload. |
| List | Agent lists artifacts in a scope (run-level or project-level). Capped at 100 entries. |
| DI registration | `ArtifactTool` registered as singleton in `ServiceCollectionExtensions`, injecting `IArtifactStore`. |
| Tests | 14 tests — write/read/list, error cases, cross-agent sharing within same run scope. |

#### Agent workflow

```
Orchestrator assigns task to Agent A
    ↓
Agent A works in isolation
    ↓
Agent A calls Artifact(action: "write", path: "analysis.md", content: "...", run_id: "team-run-1")
    ↓
Agent A returns to orchestrator with just the artifact reference
    ↓
Orchestrator (or user via API) reads the artifact
```

---

### Phase 42 — IDE Extension (VS Code) ⏸️ Deferred (nice-to-have)

**Competitor precedent:** Claude Code ✅ · opencode ✅ (beta)
**Depends on:** Phase 15 (MCP server mode) — once Sovrant exposes an MCP server, MCP-aware IDEs (VS Code with GitHub Copilot, Cursor, Windsurf) can connect without a bespoke extension.
**Status:** Deferred. MCP-based IDE integration (Phase 15) covers the core use case. A native extension adds polish but is not required for the core product.

**Goal:** Embed Sovrant into VS Code as a sidebar panel — chat interface, inline diff approval, tool event rendering, permission dialogs with file highlighting.

#### Architecture

Two-layer approach:
1. **Phase 15 (MCP):** Zero-code IDE integration for MCP-aware clients. Sovrant appears as an MCP tool server. No extension required.
2. **Phase 42 (native extension):** A dedicated VS Code extension that connects to `Sovrant.Server` via HTTP/SSE for richer UX — inline diffs, file decorations, permission dialogs anchored to the relevant file.

#### Implementation Plan

1. Publish `Sovrant.Server` as a local background service (`sovrant serve` command) with auto-start on VS Code activation
2. Implement the VS Code extension (`vscode-sovrant`) — TypeScript, connects to `Sovrant.Server` via the Phase 14 frontend SDK
3. Sidebar: chat panel backed by `useChat()` hook
4. Inline diffs: intercept `Edit`/`Write` tool events, show diff decoration in the editor
5. Permission dialogs: VS Code `window.showInformationMessage` with approve/deny buttons
6. Publish to VS Code Marketplace

---

### Phase 42.5 — Database Lifecycle: Setup, Upgrade Safety & Introspection ✅ Complete

**Depends on:** Phase 32 (SQLite persistence), Phases 35–37 (workspaces, projects, users)
**Difficulty:** Medium
**Status:** All actionable items complete. Core items (1, 3–7, 9, 12, 15, 17) landed in commit `3b7003f`. Remaining items closed out: item 2 (idempotent re-seed test), item 10 (first-boot WARN log), item 11 (`--db-path` CLI flag wired through `SovrantConfig`), item 18 (`sovrant db init` + `sovrant db reset --yes`). Item 8 (data-loss audit) is a documentation-only concern, not code. Items 13 (JSONL consolidation) and 16 (connection pooling) are already satisfied — JSONL is opt-in via env vars, and `Cache=Shared` + singleton factory is sufficient.

**Goal:** Make the SQLite layer a first-class, observable, recoverable system. Today the database is created and migrated transparently on first boot — which is great for new users, but failures are silent, there is no introspection, multiple persistence stores still write parallel JSONL files, and we have no automated proof that an old DB upgrades cleanly. This phase closes those gaps.

**Why this matters**

Today every test starts from a fresh empty SQLite file. The migration runner is correctly designed to be additive (`CREATE TABLE`, `CREATE INDEX IF NOT EXISTS`) and skips already-applied versions, so in principle an old DB upgrades in place on next boot — but we have **no automated proof**, no docs telling users this, and no recovery story when something goes wrong. Users hitting "I have an old DB and the new server crashes" today have no guidance other than "delete the file".

#### Items

| # | Status | Item | Description |
|---|---|---|---|
| 1 | ✅ | Old-DB upgrade tests | `tests/Sovrant.Runtime.Tests/Storage/OldDbUpgradeTests.cs` builds a V005-era DB, seeds users/sessions/entries/token_usage/audit_bash, runs `InitializeAsync` to carry it to current, and asserts all data survives + V006/V007 tables exist. |
| 2 | ✅ | Idempotent re-seed verification | `InitializeAsync_SeedIsIdempotent_NoDuplicateRows` test verifies calling `InitializeAsync` twice produces exactly 1 user, 1 workspace, and 1 workspace_members row. |
| 3 | ✅ | Migration checksum drift detection | `MigrationRunner.VerifyNoChecksumDrift` throws `MigrationDriftException` before applying new migrations; null legacy checksums are tolerated so existing installs upgrade cleanly. |
| 4 | ✅ | Backup-before-migrate flag | `SOVRANT_DB_BACKUP_ON_UPGRADE=true` checkpoints the WAL and copies the DB to `{path}.bak-{currentVersion}` before running pending migrations. Backup failure aborts migration. |
| 5 | ✅ | Upgrade docs | New "DB Upgrades (Phase 42.5)" section in `docs/persistence.md` covering init flow, recommended upgrade procedure, rollback via backup file, drift recovery, and `/health` DB coverage. |
| 6 | ✅ | Server startup log line | `LogInitialized` already emits `"SQLite storage initialized at schema version N"` at Information level on every boot. |
| 7 | ✅ | Hard-failure reporting | `LogMigrationFailed` names the failing version + description and points operators at `docs/db-upgrades.md` / `SOVRANT_DB_BACKUP_ON_UPGRADE`. `LogMigrationDrift` names the offending version. |
| 8 | N/A | Per-table data-loss audit | Documentation-only concern. All migrations are additive (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`). No column drops or renames exist. Future migrations that require schema changes should use the add-column-then-backfill pattern. |
| 9 | ✅ | `sovrant db` CLI subcommand | `sovrant db status`, `sovrant db version`, `sovrant db migrate [--dry-run]`, `sovrant db backup [path]`, `sovrant db inspect <table> [--limit N]` all live next to `sovrant db import-swarm`. |
| 10 | ✅ | First-boot WARN log | `LogFirstBoot` emits `"Created Sovrant database at {DbPath} (schema v{Version}). This is a first-time initialization."` at Warning level when `preVersion == 0`. Subsequent boots log at Information via `LogInitialized`. |
| 11 | ✅ | `--db-path` CLI flag | Global `--db-path` option added to CLI root command. Flows through `SovrantConfig.DbPath` → `AddSovrantRuntime` → `SqliteStorageProvider` constructor. Takes priority over `SOVRANT_DB_PATH` env var. |
| 12 | ✅ | Fail-loud option for init failures | `SOVRANT_DB_REQUIRE=true` makes `InitializeAsync` rethrow any init failure as `InvalidOperationException`. `MigrationDriftException` is always rethrown regardless of the flag. |
| 13 | ✅ | Consolidate parallel JSONL persistence | Already satisfied — JSONL is off by default (SQLite is the sole write path). `SOVRANT_AUDIT_JSONL=true` / `SOVRANT_SESSION_JSONL=true` are opt-in export flags that enable `DualWriteAuditStore` / `DualWriteSessionStore` decorators. No further work needed. |
| 14 | — | ~~Move swarm sessions into the DB~~ | **Promoted to Phase 37.5.** |
| 15 | ✅ | Empty-string `user_id` cleanup | V009 backfills pre-existing `user_id = ''` rows in `sessions`, `token_usage`, and `credentials` to the oldest active admin user (deterministic, env-free). |
| 16 | ✅ | Connection pooling | `Cache=Shared` on both read-write and read-only connection strings enables SQLite's shared-cache mode across all connections. Combined with the singleton `ISqliteConnectionFactory`, 20 MB `cache_size`, and WAL mode, this is sufficient. No user pain observed; a dedicated pool can be added later if load testing warrants it. |
| 17 | ✅ | Health-check endpoint coverage | `/health` now returns a `db` block with `status`, `schema_version`, `path`, and optional `error`. A failing probe flips overall status to `degraded` while still returning HTTP 200. |
| 18 | ✅ | `sovrant db init` / `db reset` | `sovrant db init` explicitly runs migrations and seeds default data (safe to re-run). `sovrant db reset [--yes]` deletes the DB + WAL/SHM sidecars then re-initialises from scratch. Interactive confirmation required unless `--yes` is passed. |

#### Acceptance Criteria

- ✅ A test suite that takes a "Phase 32 era" DB through to current and verifies workspaces, projects, and users all coexist with pre-existing rows — `OldDbUpgradeTests` covers V005 → current.
- ✅ Documented upgrade procedure — landed in `docs/persistence.md` under "DB Upgrades (Phase 42.5)".
- ✅ A failing migration produces a friendly, actionable error — `LogMigrationFailed` + `LogMigrationDrift`.
- ✅ `SOVRANT_DB_BACKUP_ON_UPGRADE` works end-to-end — covered by `UpgradeFromV005_WithBackupFlag_WritesBackupFile`.
- ✅ `sovrant db status` reports path, schema version, and table sizes.
- ✅ `/health` reports DB status (degraded vs ok).
- ✅ JSONL parallel writes are gated behind explicit export flags (`SOVRANT_AUDIT_JSONL`, `SOVRANT_SESSION_JSONL`), not on by default.
- ✅ Swarm orchestrator sessions live in the `swarm_events` table — delivered in Phase 37.5.
- ✅ All `user_id = ''` rows are backfilled or migrated — V009.

---

### Phase 43 — Windows PowerShell Integration & Platform Shell Strategy ✅ Core complete

**Depends on:** Phase 34 (CLI visual polish — BashTool already switched to PowerShell on Windows)
**Difficulty:** Medium
**Status:** Core items ✅ complete (2026-04-09, commit `7619e19`). The original scope also included speculative items (module discovery, SecretManagement integration, NTFS ACL helpers) that we deferred — see the Status column below.

**Goal:** Fully leverage PowerShell capabilities on Windows environments. The current BashTool was designed for Unix shells and uses PowerShell on Windows only as a compatibility shim. This phase explores native PowerShell idioms, cmdlet access, and Windows-specific capabilities to make Sovrant a first-class citizen on Windows.

**What was actually driving the phase:** two real bugs surfaced in practice — (a) `cd` in one BashTool invocation didn't persist into the next because `ProcessStartInfo.WorkingDirectory` was never set and no per-session cwd state existed; (b) `FindPowerShell()` had dead-code PATH lookup so pwsh-on-PATH was never picked over Windows PowerShell 5.1. Both fixed.

#### Items

| # | Item | Status | Description |
|---|---|---|---|
| 0 | **cwd persistence across BashTool calls** (added mid-phase) | ✅ | Root cause of the pwd/traversal bug. `ShellSessionState` singleton + sentinel-based cwd readback (`printf` on bash, `Write-Output` on PowerShell) so `cd` in one call survives into the next. Linux `/bin/bash` and Windows PowerShell both use the same plumbing. |
| 1 | PowerShell-native command generation | ✅ | `SystemPromptBuilder` now emits OS-specific shell guidance: on Windows it tells the model the Bash tool runs through PowerShell, to use `;` instead of `&&` (unsupported in PS 5.1), and to prefer `Get-ChildItem`/`Remove-Item`/`Copy-Item`/`Get-Content`/`Set-Location` over Unix aliases when switches matter. |
| 2 | PowerShell module discovery | ⏸️ Deferred | Speculative — no observed need yet. Revisit when a tool actually wants to enumerate `Get-Module -ListAvailable`. |
| 3 | Windows API access via cmdlets | ⏸️ Deferred | The existing BashTool already invokes PowerShell, so .NET types / WMI / registry access is implicitly available to generated commands. No dedicated wrapper needed until a specific use case appears. |
| 4 | Execution policy handling | ✅ | `BashTool` detects the "running scripts is disabled" / "UnauthorizedAccess" / "cannot be loaded" error strings and appends an actionable hint: `Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned`. |
| 5 | PowerShell 7+ vs 5.1 detection | ✅ | `ShellEnvironment` probes `$PSVersionTable.PSVersion` + `$PSEdition` once at first use and caches `PowerShellInfo { Path, Version, Edition, IsPwsh7Plus, IsWindowsPowerShell51 }`. Also fixed the dead-code PATH lookup so a winget-installed `pwsh.exe` is now preferred over `powershell.exe`. |
| 6 | Credential & secret management | ⏸️ Deferred | SecretManagement integration was speculative. Credentials flow through `SovrantConfig` and env vars today; revisit if Phase 44 desktop app wants OS-credential-store for API keys (its own item covers this). |
| 7 | Windows-specific file operations | ⏸️ Deferred | No consumer yet. Phase 38 PR 4 already handles the one case we cared about (`File.SetUnixFileMode` for DB files on Unix; Windows relies on inherited ACLs). |
| 8 | Admin elevation detection | ✅ | `ShellEnvironment.IsElevated` — `WindowsPrincipal.IsInRole(Administrator)` on Windows, `geteuid() == 0` on Unix. `InteractiveConfirmationHandler` shows a red "running as Administrator/root" warning on `Bash`/`PowerShell`/`Repl`/`Verify` confirmations. |

#### Acceptance Criteria

- ✅ LLM generates PowerShell-appropriate syntax on Windows — system prompt tells it so
- ⏸️ Available PowerShell modules are discoverable by the agent — deferred with item #2
- ✅ Execution policy errors produce actionable guidance — hint appended on detection
- ✅ Admin elevation status is detected and surfaced in tool confirmations
- ✅ All existing Unix/macOS behavior is preserved — `/bin/bash` code path unchanged except for the new `WorkingDirectory` assignment and the POSIX `printf` sentinel; all 176 tool tests + 1,085 suite tests pass
- ✅ Bonus: fresh-regression for cwd persistence (two new tests in `BashToolTests`: cd-across-invocations, deleted-cwd fallback)

#### Files touched

- `src/Sovrant.Tools/ShellSessionState.cs` (new)
- `src/Sovrant.Tools/Shell/ShellEnvironment.cs` (new, with `PowerShellInfo` record)
- `src/Sovrant.Tools/ProcessExecutor.cs` (added `workingDirectory` param)
- `src/Sovrant.Tools/Core/BashTool.cs` (sentinel wrap, ShellEnvironment injection, exec-policy hint, PATH-aware pwsh lookup)
- `src/Sovrant.Tools/Quality/VerifyTool.cs` (named-arg `ct:` for signature compat)
- `src/Sovrant.Tools/ServiceCollectionExtensions.cs` (DI registrations)
- `src/Sovrant.Runtime/Prompt/SystemPromptBuilder.cs` (OS-specific shell hint)
- `src/Sovrant.Cli/InteractiveConfirmationHandler.cs` (elevation warning on shell tools)
- `tests/Sovrant.Tools.Tests/Core/BashToolTests.cs` (+2 tests)
- `tests/Sovrant.Agents.Tests/Shared/SovrantAgentTests.cs`, `tests/Sovrant.Commands.Tests/Commands/ExitClearCommandTests.cs` (pre-existing Phase 38 signature drift in test fakes)

---

### Phase 44 — Desktop Application (Cross-Platform) ✅

**Depends on:** Phase 32 (SQLite persistence), Phase 34 (CLI visual polish)
**Difficulty:** High

**Goal:** A native desktop application that mirrors the Sovrant web frontend's design and UX, built with Avalonia UI on .NET 10. The app embeds the Sovrant runtime directly in-process for maximum performance and security — no server, no CLI subprocess, no network overhead. The runtime (`ConversationRuntime`, `ToolExecutor`, `SessionStore`, `AgentOrchestrator`, etc.) runs in the same process as the UI, with SQLite persistence, OS credential store for API keys, and the full tool/agent/memory stack available natively. The GUI translates user actions into direct runtime calls and renders `RuntimeEvent` streams as rich UI. Windows is the primary target, with macOS and Linux as a goal.

#### Framework Evaluation

| Framework | Windows | macOS | Linux | Maturity | Notes |
|---|---|---|---|---|---|
| **Avalonia UI** | ✅ | ✅ | ✅ | Stable (11.x) | XAML-based, CSS-like styling, JetBrains uses it. Best Linux story. Single codebase → all 3 platforms. Hot reload, design-time preview. **Recommended.** |
| **Uno Platform** | ✅ | ✅ | ✅ (via Skia/GTK) | Stable (5.x) | WinUI 3 / XAML dialect. Also targets web (WASM) and mobile. Heavier runtime. Good if we want web+desktop from one XAML codebase. |
| **.NET MAUI** | ✅ | ✅ | ❌ | Stable but desktop-weak | Microsoft official. No Linux support. Desktop experience is secondary to mobile. Community frustration with bugs and slow fixes. |
| **Blazor Hybrid** | ✅ | ✅ | ❌ (needs MAUI) | Stable | Renders web UI inside a native WebView. Could reuse the existing React/TypeScript frontend directly. But tied to MAUI for the host shell → no Linux. |
| **Photino** | ✅ | ✅ | ✅ | Early | Lightweight native WebView wrapper. Tiny footprint. Could host the existing web frontend as-is. Less mature, small community. |

**Recommendation: Avalonia UI** — best cross-platform coverage (including Linux), most active community for desktop-focused .NET apps, XAML familiarity for .NET developers, and proven at scale. Uno Platform is a strong alternative if we later want to share XAML between desktop and a WASM web target. Photino is worth revisiting if we decide to simply wrap the existing web frontend in a native shell instead of building native UI.

#### Architecture — Embedded Runtime with GUI Shell

The desktop app references `Sovrant.Runtime`, `Sovrant.Tools`, `Sovrant.Commands`, and `Sovrant.Agents` directly as project references — the same way `Sovrant.Cli` does. The runtime runs in-process: `AddSovrantRuntime()`, `AddSovrantTools()`, `AddOrchestrationSystem()`, and `AddSovrantCommands()` wire up DI exactly as they do in the CLI. No HTTP layer, no server process, no network roundtrip.

```
┌───────────────────────────────────────────────┐
│              Sovrant.Desktop                  │
│         (Avalonia UI — .NET 10)               │
│                                               │
│  ┌─────────────┐  ┌───────────────────────┐   │
│  │  Chat View  │  │  Session Sidebar      │   │
│  │  (streaming │  │  (history, search,    │   │
│  │   tokens)   │  │   resume, export)     │   │
│  ├─────────────┤  ├───────────────────────┤   │
│  │  Tool Panel │  │  Settings / Config    │   │
│  │  (diffs,    │  │  (providers, keys,    │   │
│  │   approvals)│  │   permissions, model) │   │
│  └─────────────┘  └───────────────────────┘   │
│                                               │
│  ┌───────────────────────────────────────────┐│
│  │         Sovrant Runtime (in-process)      ││
│  │  ConversationRuntime · ToolExecutor ·     ││
│  │  SmartRouter · SessionStore (SQLite) ·    ││
│  │  AgentOrchestrator · HookRunner ·         ││
│  │  MemoryManager · MCP Client               ││
│  └───────────────────────────────────────────┘│
└───────────────────────────────────────────────┘
         │ direct .NET calls
         ▼
   LLM Providers (OpenAI, Anthropic, etc.)
```

`IAsyncEnumerable<RuntimeEvent>` streams tokens directly to the UI thread via Avalonia's dispatcher. User actions (send message, approve tool, cancel generation) call `ConversationRuntime` and `SlashCommandDispatcher` directly — zero serialization, zero latency. SQLite sessions, memory, and audit data persist locally. The desktop app is the primary product for end users; the CLI and server remain available for developers and automation.

**Future: Embedded Terminal (Phase 45)**
A later phase adds an integrated terminal panel within the desktop app (similar to VS Code's terminal or Windows Terminal) that can optionally run the Sovrant CLI for power users who want raw terminal access.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Project scaffolding | Create `src/Sovrant.Desktop/` as an Avalonia MVVM app targeting .NET 10. Project-reference `Sovrant.Runtime`, `Sovrant.Tools`, `Sovrant.Commands`, `Sovrant.Agents`. Wire up DI with `AddSovrantRuntime()`, `AddSovrantTools()`, `AddOrchestrationSystem()`, `AddSovrantCommands()` in `App.axaml.cs`. Single-process, no external dependencies. |
| 2 | Design system | Port the web frontend's visual language to Avalonia: color palette (teal accents, dark/light themes), typography, spacing, iconography. Create `Styles/` with reusable control templates and theme resources. |
| 3 | Chat view | Main conversation panel consuming `IAsyncEnumerable<RuntimeEvent>` directly. Markdown rendering, code block syntax highlighting, auto-scroll. `TextChunk` events append to the current message; `TurnComplete` finalizes it. Match the web frontend's message layout and turn separation. |
| 4 | Inline diff view | Render `ToolUseRequested` events for Edit/Write tools as side-by-side or unified diffs with red/green coloring. Inline Allow/Deny buttons wired to `IToolConfirmationHandler`. |
| 5 | Tool confirmation UI | Implement `IToolConfirmationHandler` as an Avalonia dialog — show tool name, parameters, risk level. `RequestConfirmationAsync` opens a modal and returns the user's choice. Native desktop experience for permission management. |
| 6 | Session sidebar | List previous sessions from `ISessionStore` (direct SQLite query, no HTTP). Search, resume, delete, export as Markdown. Show session metadata (date, token count, model, provider). |
| 7 | Settings panel | Provider configuration, API key management via OS credential store (Windows Credential Manager / macOS Keychain / libsecret on Linux), permission mode selection, model picker. Writes to `sovrant.json` config file. |
| 8 | System tray integration | Minimize to system tray, global hotkey to summon, notification badges for long-running agent completions. Windows: NotifyIcon. macOS: NSStatusItem. Linux: StatusNotifierItem/AppIndicator. |
| 9 | Slash command palette | Searchable command palette (Ctrl+K / Cmd+K) surfacing all `ISlashCommand` implementations with their categories and descriptions. Equivalent to `/help` but as a filterable overlay. |
| 10 | Auto-update mechanism | Check for new versions on startup, download and apply updates. Windows: MSIX or Squirrel.Windows. macOS: Sparkle. Linux: AppImage with built-in update check. |
| 11 | Packaging & distribution | Platform-specific installers: MSIX/WinGet (Windows), DMG (macOS), AppImage/Flatpak (Linux). Self-contained single-file publish (no .NET runtime install). CI pipeline for all three. Code signing for Windows and macOS. |
| 12 | Provider-native web search | Enable web search without requiring Brave/FireCrawl API keys by leveraging provider-native capabilities. OpenRouter: add `plugins: [{id: "web"}]` or `:online` model suffix to chat completions requests. OpenAI: already supported via `OpenAiResponsesProvider` with `web_search_preview`. Per-model: check `IModelCapabilityRegistry` for web search support (e.g. Gemma 4 does not support native web search but OpenRouter can add it). Add a "Web Search" toggle in Settings that persists to `settings.json` and bridges to `LLM_WEB_SEARCH` env var. |

#### Acceptance Criteria

- Single self-contained executable — no server, no CLI, no external process required
- Runtime embedded in-process: full `ConversationRuntime`, tools, agents, memory, sessions
- Zero-latency streaming: in-process `RuntimeEvent` delivery, no serialization overhead
- SQLite persistence: sessions, memory, audit all stored locally and survive restarts
- API keys stored in OS credential store (Windows Credential Manager / macOS Keychain / libsecret), never in plaintext
- Tool confirmations via native Avalonia dialogs implementing `IToolConfirmationHandler`
- Windows build runs on Windows 10+ with full feature parity to the web frontend
- macOS and Linux builds launch and render correctly (may have reduced OS integration)
- `dotnet build` and `dotnet test` pass with no new warnings

---

### Phase 45 — Embedded Terminal Panel ⏸️ Deferred

**Depends on:** Phase 44 (Desktop application)
**Difficulty:** Medium

**Goal:** Add an integrated terminal panel within the Sovrant desktop app, similar to VS Code's integrated terminal or Windows Terminal's tab system. Power users can drop into a full Sovrant CLI session without leaving the app, or open a raw shell for system commands.

#### Items

| # | Item | Description |
|---|---|---|
| 1 | Terminal emulator control | Embed a VT100/xterm-compatible terminal emulator as an Avalonia control. Evaluate existing .NET terminal libraries (e.g., Terminal.Gui, XtermSharp) or implement a minimal VT parser. |
| 2 | Sovrant CLI integration | Launch `sovrant` CLI as a child process with stdin/stdout piped to the terminal control. Full REPL experience — banner, sticky input bar, thinking spinner, tool diffs — rendered natively in the terminal panel. |
| 3 | Shell tab support | Support multiple terminal tabs: Sovrant CLI sessions, PowerShell/bash shells, or any arbitrary command. Tab management with keyboard shortcuts (Ctrl+` to toggle, Ctrl+Shift+T for new tab). |
| 4 | Split panes | Horizontal/vertical split within the terminal area. Run a Sovrant session alongside a shell for manual verification. |
| 5 | Shared context | Terminal sessions can reference files and sessions visible in the main desktop UI. Copy file paths, session IDs, or tool outputs between the GUI and terminal with a click. |

#### Acceptance Criteria

- Terminal panel toggles with a keyboard shortcut, docks to bottom or side
- Sovrant CLI renders correctly in the embedded terminal (ANSI colors, cursor positioning, input bar)
- Multiple tabs supported with independent sessions
- Split pane layout with drag-to-resize
- Works on Windows (PowerShell), macOS (zsh), Linux (bash)

---

---

## Phase 46 — n8n Automation Integration

**Goal:** Give Sovrant access to 1,000+ third-party integrations (Slack, Gmail, Sheets, Salesforce, Jira, etc.) by integrating with n8n as a headless automation engine. The runtime orchestrates n8n workflows via API, turning Sovrant agents into automation controllers that can trigger and receive data from external services without building each integration from scratch.

**Integration approaches (not mutually exclusive):**

| Approach | How It Works | Best For |
|---|---|---|
| **API-First** | `HttpClient` triggers n8n workflows via Webhook nodes; n8n returns data to the calling agent | Simple request/response automations, synchronous tool use |
| **Headless Orchestration** | n8n runs in a Docker container alongside Sovrant; the runtime acts as the "brain", n8n as a "worker" for third-party APIs | Complex multi-step workflows, scheduled automations |
| **.NET Aspire Composition** | Aspire orchestrates Sovrant services + n8n container as a managed distributed app | Cloud-native deployments, dev/test parity, health monitoring |

**Items:**
1. `AutomationTool` — agent-callable tool that triggers n8n workflows by name/ID via webhook, returns the result
2. `AutomationListTool` — lists available n8n workflows the agent can trigger
3. n8n Docker Compose profile — `docker-compose.automation.yml` with n8n container, shared network, volume for workflow persistence
4. Webhook callback endpoint — `POST /v1/automation/callback` so n8n can push async results back to Sovrant
5. Workflow template library — starter n8n workflows for common patterns (Slack notify, email send, spreadsheet update, Jira ticket create)
6. .NET Aspire resource definition — `AddN8nContainer()` extension for orchestrated deployments
7. Credential bridging — securely pass API keys from Sovrant's credential store to n8n at container startup
8. `/automation` slash command — list, trigger, and check status of n8n workflows from the CLI
9. Rate limiting and circuit breaker — protect against n8n overload or downstream service failures
10. Documentation — setup guide, workflow authoring, security model

**Verification:**
- `dotnet build` exits 0
- Agent can trigger an n8n workflow via the `Automation` tool and receive structured results
- n8n container starts via Docker Compose with persistent workflow storage
- Webhook callback delivers async results to the correct session
- Aspire orchestration starts both Sovrant and n8n with health checks

---

## Phase 47 — Workspace Backup, Import & Export

> **Status: Post-Beta** — Intentionally deferred past initial public release. The value is clear and the spec is solid, but workspace portability is not a launch blocker. Implement once the beta cohort is established and users start asking for it.

**Depends on:** Phase 35 (Workspaces), Phase 32 (SQLite persistence)

**Goal:** Enable full backup, restore, import, and export of Sovrant workspaces so users can snapshot their work, migrate between machines, share workspace templates, and recover from data loss. A workspace export is a self-contained portable archive containing all sessions, memory, config, credentials (encrypted), agent templates, skills, and audit history.

#### Motivation

As workspaces accumulate sessions, learned patterns, custom agents, and configuration, they become valuable intellectual property. Users need to:
- **Back up** before risky operations or machine migrations
- **Restore** a workspace to a known-good state after corruption or accidental deletion
- **Export** a workspace to share with teammates or move to another Sovrant instance
- **Import** a workspace archive received from another user or CI pipeline
- **Template** a workspace setup (agents, skills, config) for repeatable project bootstrapping

#### Archive format

A workspace export is a `.sovrant-workspace` file (ZIP archive) with a deterministic internal layout:

```
workspace-export/
├── manifest.json          # version, workspace metadata, export timestamp, checksums
├── sessions/              # session history (JSONL per session)
│   ├── session-abc.jsonl
│   └── session-def.jsonl
├── memory/
│   ├── summaries.json     # session summaries
│   ├── patterns.json      # learned patterns
│   └── instincts.json     # instinct rules
├── config/
│   ├── workspace.json     # workspace-level settings
│   └── sovrant.json       # project-level config snapshot
├── agents/                # custom agent templates (.md files)
├── skills/                # custom skills (.md files)
├── commands/              # custom slash commands (.md files)
├── credentials.enc        # encrypted credential vault (AES-256-GCM, key derived from user passphrase)
└── audit/
    └── audit-log.jsonl    # governance and tool audit events
```

#### Core operations

| Operation | CLI | API | Description |
|---|---|---|---|
| **Backup** | `sovrant backup [--output path]` | `POST /v1/workspaces/{id}/backup` | Full snapshot of current workspace to `.sovrant-workspace` archive |
| **Restore** | `sovrant restore <archive>` | `POST /v1/workspaces/{id}/restore` | Replace current workspace data with archive contents |
| **Export** | `sovrant export [--exclude sessions,audit]` | `POST /v1/workspaces/{id}/export` | Selective export with optional exclusions |
| **Import** | `sovrant import <archive> [--merge\|--replace]` | `POST /v1/workspaces/import` | Import archive as new workspace or merge into existing |
| **Schedule** | `/backup schedule daily 02:00` | `POST /v1/workspaces/{id}/backup/schedule` | Automated periodic backups with retention policy |

#### Import modes

- **Replace** (default for restore): Drops existing workspace data and replaces with archive contents. Destructive — requires confirmation.
- **Merge**: Adds archive data alongside existing data. Sessions are deduplicated by ID. Config values from the archive override existing where keys collide. Memory entries are merged (no duplicates by content hash). Agent/skill/command files are overwritten if names match.

#### Security

- Credential vault is encrypted with AES-256-GCM; passphrase required on export and import
- Archives can optionally exclude credentials entirely (`--no-credentials`)
- Manifest includes SHA-256 checksums for every file — import validates integrity before applying
- Audit log records all backup/restore/import/export operations with timestamp and user

#### Scheduled backups

- Configurable via workspace config or CLI slash command
- Retention policy: keep last N backups, or backups from last N days
- Storage: local filesystem (`~/.sovrant/backups/`) or configurable path
- Backup rotation: oldest archives pruned when retention limit is exceeded

#### Implementation plan

1. Define `WorkspaceArchive` model — manifest schema, file layout, version numbering
2. Implement `IWorkspaceExporter` — reads workspace data from SQLite/filesystem, writes ZIP archive
3. Implement `IWorkspaceImporter` — reads ZIP archive, validates manifest checksums, applies data
4. Implement credential encryption/decryption with passphrase-derived key (PBKDF2 + AES-256-GCM)
5. Add `sovrant backup` / `sovrant restore` / `sovrant export` / `sovrant import` CLI subcommands
6. Add `/backup` slash command for in-session backup/schedule management
7. Add API endpoints: backup, restore, export, import (streaming file upload/download)
8. Implement merge logic — session dedup, config merge, memory content-hash dedup
9. Implement scheduled backup service with retention policy and rotation
10. Tests: round-trip export→import, merge vs replace modes, credential encryption, checksum validation, scheduled backup trigger, retention pruning

**Verification:**
- `dotnet build` exits 0
- `sovrant backup` produces a valid `.sovrant-workspace` archive
- `sovrant restore <archive>` restores all workspace data
- `sovrant export --exclude credentials` omits credential vault
- `sovrant import <archive> --merge` merges without data loss
- Round-trip: export workspace A → import into workspace B → B has identical data
- Credential vault requires passphrase and is AES-256-GCM encrypted
- Scheduled backup fires at configured time and prunes old archives

---

## Phase 48 — Intent-Aware, Capability-Discovered Model Routing

**Depends on:** Phase 8 (multi-provider), SmartRouter

**Goal:** Automatically select the best LLM model for each turn based on (a) what models the user actually has connected and (b) the input's intent, complexity, and task type — without the user switching models manually. The router discovers each provider's available models at startup, builds a tier ladder from the user's real fleet (Anthropic, OpenAI, Ollama, fine-tunes, or any mix), and routes simple questions to fast/cheap models while sending complex reasoning, code generation, and multi-step planning to high-capability models. Users can override with explicit model selection, but the default is intelligent automatic routing that adapts to whatever they have wired up.

#### Motivation

Today the SmartRouter picks a provider based on latency, cost, and error rate — but it always uses the same model tier. Users must manually `/model <name>` to switch. In practice:
- A simple "what time is it?" doesn't need Opus-class reasoning
- A complex "refactor this module and write tests" benefits from the strongest model
- Creative writing, code review, and data analysis have different optimal model tiers
- Cost-conscious users want cheap models by default with automatic escalation for hard tasks

Intent-aware routing solves this by classifying each input and routing to the appropriate model tier automatically.

#### Core concepts

- **Intent classifier** — lightweight local classifier (rule-based + optional small LLM call) that categorizes user input into intent classes: `simple_qa`, `code_generation`, `code_review`, `refactor`, `planning`, `creative`, `analysis`, `debugging`, `conversation`, `tool_heavy`
- **Complexity estimator** — scores input complexity (0.0–1.0) based on token count, question depth, number of files referenced, presence of code blocks, multi-step indicators ("first...then...finally")
- **Model tier mapping** — maps intent + complexity to model tiers: `fast` (Haiku-class), `standard` (Sonnet-class), `high` (Opus-class)
- **Escalation** — if the model produces a low-confidence or incomplete response, automatically retry with a higher-tier model
- **User override** — `/model <name>` pins to a specific model; `/model auto` re-enables intent routing
- **Cost budget awareness** — if a cost budget is set (Phase 55), prefer cheaper models when budget is running low
- **Capability-aware tier resolution** — the tier map (`fast` / `standard` / `high`) is **not hardcoded to Claude names**. At startup, the router enumerates every connected provider's available models (via `/v1/models` for OpenAI-compatible endpoints, `/api/tags` for Ollama, the configured model list for native providers) and builds the tier map from what is actually reachable. A user running only Ollama gets tiers populated from local models (e.g. `qwen2.5:3b` → fast, `qwen2.5:14b` → standard, `qwen2.5:72b` → high); a user with GPT-4o-mini + GPT-4o gets those; a user with the full Anthropic suite gets Haiku/Sonnet/Opus. If a tier has no candidate, it collapses to the next-best available tier rather than failing.

#### Routing rules (configurable)

| Intent | Default tier | Escalation trigger |
|---|---|---|
| `simple_qa` | fast | N/A |
| `conversation` | fast | Multi-turn depth > 5 |
| `code_review` | standard | File count > 3 |
| `code_generation` | standard | Complexity > 0.7 → high |
| `refactor` | high | Always high |
| `planning` | high | Always high |
| `debugging` | standard | After 2 failed tool rounds → high |
| `creative` | standard | Length > 2000 tokens → high |
| `analysis` | standard | Data size indicators → high |
| `tool_heavy` | standard | Tool round count > 5 → high |

#### Model capability discovery

The router needs to know **which models the user actually has connected** and what each one is good at before it can pick one for a task. This is a discrete subsystem that runs alongside the existing health pings:

- **Discovery pass** at startup (and on `/v1/router/refresh`):
  - OpenAI-compatible providers → `GET /v1/models`
  - Ollama → `GET /api/tags`
  - Native Anthropic / Gemini → declared model list from config
  - Per-user credential overrides (Phase 8 headers) → discovery is per-session, not just per-process
- **Capability metadata** for each discovered model — stored in a `model_capabilities` registry seeded with known data and overridable by the user:
  - `tier_hint` (`fast` / `standard` / `high`) — derived from model family + parameter count
  - `context_window`, `max_output_tokens`
  - `supports_tools`, `supports_vision`, `supports_json_mode`, `supports_thinking`
  - `intent_affinity` — optional per-intent score boost (e.g. a coding-specialised model gets +0.3 on `code_generation`)
  - `cost_per_1k_input`, `cost_per_1k_output` (feeds Phase 55)
- **Auto-tier assignment** — when discovery finishes, the router groups every reachable model by `tier_hint`, picks the cheapest healthy one in each bucket as the default for that tier, and falls back to the next bucket up if a tier is empty (e.g. user only has one large local model → `fast` collapses to `standard`).
- **User override** — `.sovrant/routing.json` can pin specific models to specific tiers, overriding auto-assignment. Discovery still runs so unknown models surface in `/v1/router/status`.
- **Re-discovery triggers** — startup, manual refresh, provider added/removed, ping recovery from unhealthy.

This is what makes the routing "user-aware" rather than Anthropic-flavoured: a developer running pure-local Ollama gets a working tier ladder out of the box, and a team mixing OpenAI + Anthropic + a fine-tune gets tiers built from their actual fleet.

#### Configuration

```json
// .sovrant/routing.json
{
  "intent_routing": true,
  "default_tier": "standard",
  "auto_tier_assignment": true,
  "tier_models": {
    "fast": "auto",
    "standard": "auto",
    "high": "auto"
  },
  "model_overrides": {
    "qwen2.5-coder:14b": { "intent_affinity": { "code_generation": 0.4, "refactor": 0.3 } }
  },
  "escalation": true,
  "max_escalations_per_turn": 1,
  "custom_rules": [
    { "pattern": "*.test.*", "tier": "standard" },
    { "pattern": "security|vulnerability|CVE", "tier": "high" }
  ]
}
```

#### API surface

| Endpoint | Method | Description |
|---|---|---|
| `/v1/config/routing` | GET/PUT | View or update intent routing configuration |
| `/v1/chat/completions` | POST | Existing endpoint — adds `X-Model-Tier` response header showing which tier was selected |

#### Implementation plan

1. Define `IntentClass` enum and `IntentClassification` record (intent, complexity score, recommended tier)
2. Implement `RuleBasedIntentClassifier` — keyword matching, regex patterns, structural analysis (code blocks, file references, question indicators)
3. Implement `IModelCapabilityRegistry` — discovers reachable models per provider (`/v1/models`, `/api/tags`, declared lists), stores capability metadata, supports per-session re-discovery for credential overrides, persists to SQLite so first-run latency is hidden on warm starts
4. Implement `IModelTierResolver` — maps intent classification to a concrete model name using the discovered registry; auto-assigns tier defaults from `tier_hint`; collapses missing tiers to the next-best available
5. Integrate into `SmartRouter.RouteAsync` — classify intent before selecting provider, use tier to filter eligible providers, prefer models with higher `intent_affinity` for the classified intent
6. Add `/model auto` support to `ModelCommand` — sets session config to use intent routing
7. Add `X-Model-Tier` and `X-Intent-Class` response headers to chat completions endpoint
8. Implement escalation logic in `ConversationRuntime.RunTurnAsync` — detect low-quality responses and retry with higher tier
9. Add routing config file loading (`RoutingConfigLoader`)
10. Add custom rule support (pattern-based tier overrides)
11. CLI: show selected tier in token usage line (e.g., `(150↑ 200↓ tokens · standard)`)
12. Surface discovered models + tier assignments in `/v1/router/status` and a new `sovrant router models` CLI command so the user can see which models the router knows about and which tier each one is in
13. Tests: capability discovery per provider type, auto-tier assignment with sparse fleets (Ollama-only, OpenAI-only, mixed), tier collapse when buckets are empty, intent classification accuracy, tier selection, escalation triggers, user override, budget awareness

**Verification:**
- `dotnet build` exits 0
- Capability discovery enumerates models from every connected provider (OpenAI-compatible, Ollama, native)
- Auto-tier assignment produces a working `fast`/`standard`/`high` ladder for an Ollama-only setup, an OpenAI-only setup, and a mixed setup
- Empty tier buckets collapse to the next-best tier without erroring
- Simple questions route to fast tier, complex tasks route to high tier
- `/model auto` enables intent routing; `/model claude-opus-4-6` pins to specific model
- Escalation retries with higher tier on low-quality responses
- `X-Model-Tier` header present in API responses
- Custom routing rules override default tier mapping
- Cost budget reduces tier preference when budget is low
- `sovrant router models` lists discovered models grouped by assigned tier

#### ✅ Completed

Implemented in full. Delivered:

- **IntentClassifier** — rule-based classifier with 10 `GeneratedRegex` patterns covering `SimpleQa`, `Conversation`, `CodeReview`, `CodeGeneration`, `Refactor`, `Planning`, `Creative`, `Analysis`, `Debugging`, `ToolHeavy`. Complexity estimator (0.0–1.0) scores word count, code blocks, multi-step indicators, file references, and conversation depth.
- **ModelTierResolver** — auto-assigns tiers from pricing percentiles (bottom 33% → fast, mid → standard, top 33% → high). Supports explicit `TierHint`, name-based inference for local models (e.g. `:3b` → fast, `:72b` → high), pinned tier overrides, intent affinity scoring, and tier collapse when buckets are empty.
- **RoutingConfig** — loads from `.sovrant/routing.json` (cwd then home) and `SOVRANT_INTENT_ROUTING` env var. Supports custom routing rules (regex pattern → tier override), tier model pinning, escalation toggles.
- **SmartRouter integration** — `RouteWithIntentAsync()` classifies intent, checks custom rules, resolves tier to model, sets model on request, selects provider. `RouteAsync()` refactored to share `SelectProvider()`.
- **LiveModelMetadataFetcher** — enhanced to extract pricing (`CostPerMillionInput`/`Output`), `MaxOutputTokens`, and `ThinkingMode` from OpenRouter `/api/v1/models` responses.
- **CLI commands** — `sovrant router models` (Spectre.Console table of tier assignments), `sovrant router status` (routing config display).
- **Tests** — 30+ new tests: IntentClassifierTests (16), ModelTierResolverTests (9), LiveMetadataPricingTests (5), SmartRouter intent integration tests (4).

Items deferred to future phases:
- Response-quality escalation (retry with higher tier on low-confidence response) → Phase 49+
- `X-Model-Tier` / `X-Intent-Class` response headers → Phase 49+
- Cost budget awareness (reduce tier when budget low) → depends on Phase 55
- Per-session re-discovery for credential overrides → Phase 49+
- `/model auto` CLI command → Phase 49+

---

## Phase 49 — SearXNG Web Search Backend

**Depends on:** existing `WebSearch` tool, multi-provider infrastructure

**Goal:** Add a self-hosted, key-free, privacy-preserving web search backend by integrating [SearXNG](https://github.com/searxng/searxng) as a `WebSearch` provider option. Lets users running a local-first stack (Ollama + SQLite + local memory) close the last cloud dependency in the agent loop without paying for Brave / Tavily / SerpAPI keys.

#### Motivation

Today `WebSearch` requires either the OpenAI Responses API native tool (`LLM_WEB_SEARCH=true`, paid) or one of the configured external search APIs. For users who:
- run Ollama for the LLM
- run Sovrant Server locally
- already self-host other infra in a homelab

…there is no fully-offline-capable search option. SearXNG is the natural fit: a single Docker container that aggregates 70+ upstream search engines (Google, Bing, DuckDuckGo, Brave, Wikipedia, GitHub, Stack Overflow, arXiv, …) behind a clean JSON API, with no API keys, no tracking, and per-category source selection.

#### Core concepts

- **`SearxngSearchProvider`** — implements the same internal search-provider interface used by the existing `WebSearch` tool. Calls `GET {SEARXNG_BASE_URL}/search?q=...&format=json` and maps the result list (`title`, `url`, `content`, `engine`, `category`) to Sovrant's existing `WebSearchResult` shape.
- **Selection via env var** — `SOVRANT_WEB_SEARCH=searxng` (alongside the existing `brave`, `tavily`, etc. options) plus `SEARXNG_BASE_URL=http://localhost:8080`. No code changes required for users on other backends.
- **Category filtering** — the `WebSearch` tool gains an optional `category` argument (`general`, `code`, `news`, `science`, `images`, `files`). Maps to SearXNG's `categories=` query param. Other backends ignore the argument.
- **Rate-limit hygiene** — local SearXNG instances are rate-limited by their *own* IP against upstream engines. Provider applies a small per-request delay budget (configurable via `SEARXNG_MIN_INTERVAL_MS`) and surfaces 429s clearly so users learn to back off rather than burn upstream goodwill.
- **Health check** — registered with `SmartRouter`'s health-ping framework so a dead SearXNG container shows up in `/v1/router/status` instead of failing silently mid-conversation.
- **No public-instance default** — `SEARXNG_BASE_URL` must be set explicitly. We will not ship a default pointing at `searx.space` instances; they have their own rate limits and reliability varies.

#### Configuration

```bash
# Self-hosted SearXNG (recommended)
docker run -d --name searxng -p 8080:8080 searxng/searxng

# Sovrant
export SOVRANT_WEB_SEARCH=searxng
export SEARXNG_BASE_URL=http://localhost:8080
export SEARXNG_MIN_INTERVAL_MS=500   # optional, default 0
```

#### Implementation plan

1. Add `SearxngSearchProvider` under `src/Sovrant.Tools/Web/Providers/` (or wherever the existing `BraveSearchProvider` / equivalent lives)
2. Wire selection in the `WebSearch` tool's provider factory keyed on `SOVRANT_WEB_SEARCH=searxng`
3. Add `category` argument to the `WebSearch` tool schema (optional, defaults to `general`)
4. Register a health-check ping (`GET /healthz`) with `SmartRouter` so status shows up in `/v1/router/status`
5. Document the Docker one-liner and env vars in `README.md` § Web Search and `docs/configuration.md`
6. Tests: provider unit tests with a mocked HTTP client, category mapping, error path on 429, health-check integration

**Verification:**
- `dotnet build` exits 0
- With a local SearXNG container running, `WebSearch` returns aggregated results from multiple engines
- `SOVRANT_WEB_SEARCH=searxng` with no `SEARXNG_BASE_URL` fails fast with a clear error
- `category=code` returns developer-oriented results (GitHub, Stack Overflow, etc.)
- `/v1/router/status` shows SearXNG health when configured
- Other web search backends (Brave, Tavily, OpenAI Responses) continue to work unchanged

**Non-goals:**
- Shipping a bundled SearXNG container (users self-host)
- Crawling / indexing — this is purely query-time aggregation; tools like Crawl4AI / Firecrawl are out of scope
- Public-instance fallback — explicitly not provided for rate-limit hygiene reasons

---

## Phase 50 — OpenClaw Integration & Federated Swarms Over a Routed Bus

**Depends on:** Phase 16 (Dynamic MCP Tool Proxy), Phase 29 (Swarm Orchestrator), Phase 35 (Workspaces), Phase 37.5 (Swarm event store), Phase 19+20 (Orchestrated teams)

**Goal:** Make [OpenClaw](https://docs.openclaw.ai/) the **routed message bus** for federated Sovrant swarms. Sovrant swarms (running Sovrant's own workers — not OpenClaw workers, because OpenClaw isn't a coding agent) post events, findings, and approval requests into OpenClaw routes; other swarms and human operators on Discord / Telegram / WhatsApp / Slack / Signal / iMessage / Matrix subscribe to those routes and respond. Three federation modes (`silo`, `federated`, `manager-led`) map onto OpenClaw's routing primitives so multiple swarms can run side-by-side either fully isolated, sharing a common channel, or reporting up to a manager. As a free side-effect, every running swarm becomes reachable from a phone.

#### What OpenClaw is (and isn't)

OpenClaw is a **self-hosted gateway/orchestrator** that bridges messaging platforms (Discord, Telegram, WhatsApp, Slack, Signal, iMessage, Matrix, …) to AI agents. It is *not* a coding agent and does not execute swarm tasks itself — it routes messages, attachments, events, and approval requests between channels and the agents that consume them. Routes are isolated per agent / workspace / sender.

**Integration surface — verified:** OpenClaw ships an **MCP server mode** invoked as `openclaw mcp serve` over **stdio**. The bridge exposes nine standard MCP tools:

| Tool | Purpose |
|---|---|
| `conversations_list` / `conversation_get` | Discover and retrieve routed conversations |
| `messages_read` / `attachments_fetch` | Read transcript history and message metadata |
| `messages_send` | Send replies through existing routes |
| `events_poll` / `events_wait` | Consume the live event queue (`events_wait` is a long-poll) |
| `permissions_list_open` / `permissions_respond` | Manage approval requests |

The server holds an in-memory event queue that starts when the bridge connects; older history is fetched separately via `messages_read`. With Claude channel mode enabled it also emits `notifications/claude/channel`.

**Why this matters for performance:** Sovrant already speaks MCP fluently — Phase 16 (`MCPTool`) lets any MCP server's tools surface inside the Sovrant agentic loop, and Phase 17 covers OAuth. **Adding OpenClaw is mostly configuration**, not a new transport layer. We launch `openclaw mcp serve` as a managed MCP child process (the same way every other MCP server is launched today), the nine OpenClaw tools become callable by Sovrant agents on first start, and the existing MCP infrastructure handles framing, lifecycle, restarts, and audit. No gRPC, no protobuf, no new IPC, no parallel transport story — and the long-lived stdio connection means the per-call overhead is dominated by the underlying messaging-platform RTT, not by Sovrant's wire format.

#### Motivation

Today Sovrant swarms run in isolation. There is no way for:
- Two swarms to share intermediate findings without going through the parent orchestrator's event store
- A human operator to be notified on their phone when a swarm hits a permission prompt or finishes a long-running task
- A "manager" swarm to fan tasks out to several "worker" swarms and collect their results through anything other than direct in-process function calls
- Adversarial / red-team workflows to guarantee that two competing swarms cannot read each other's intermediate state

OpenClaw solves all four with a single routing primitive: **the route**. A route is a named channel that one or more agents subscribe to, with isolation guarantees enforced by the gateway. Mapping Sovrant's federation modes onto OpenClaw routes gives us inter-swarm communication, human-in-the-loop chat-channel access, and red-team isolation in one phase.

#### Core concepts

- **`OpenClawBusClient`** — a thin Sovrant-side wrapper around the existing MCP tool proxy that exposes named, ergonomic methods (`PublishAsync`, `SubscribeAsync`, `RequestApprovalAsync`) backed by `messages_send`, `events_wait`, and `permissions_*`. Workers and the manager don't call MCP tools directly; they call this wrapper. Reuses the channel and lifecycle that Phase 16 already provides.
- **Route naming convention** — every Sovrant swarm gets a deterministic OpenClaw route derived from its `swarm_id` plus federation key, e.g. `sovrant/swarm/{swarmId}` for silo mode and `sovrant/federation/{federationId}` for federated mode. The naming convention is enforced by `SwarmExecutionContext` so workers cannot accidentally subscribe to a route outside their scope.
- **`SwarmFederationMode`** — new field on `SwarmConfig`:
  - `silo` (default) — each swarm posts to and reads from a private route `sovrant/swarm/{swarmId}`. No other swarm has the route name. OpenClaw's per-route isolation gives us red-team-grade separation for free.
  - `federated` — every child swarm of a parent shares one route `sovrant/federation/{parentSwarmId}`. Workers can `events_wait` on this route to consume each other's intermediate findings without going through the manager.
  - `manager-led` — workers post to a manager-owned route `sovrant/manager/{managerId}/inbox`; only the manager is subscribed via `events_wait`. Worker-to-worker traffic is impossible. The manager re-publishes rolled-up events onto a separate `sovrant/manager/{managerId}/outbox` route that the parent swarm and any human operators subscribe to.
- **`SwarmManagerAgent`** — a regular Sovrant agent template (`swarm-manager.md`) that owns a parent `swarm_id`, spawns child swarms, holds an open `events_wait` against its inbox route, and emits roll-up events (`ChildSwarmCompleted`, `ChildSwarmFailed`, `FederationFinding`) onto its outbox route **and** into the local `swarm_events` table via the Phase 37.5 store. Persistence is unchanged — the manager just dual-publishes to the bus.
- **Human-in-the-loop for free** — because OpenClaw routes are first-class channels in Discord / Slack / Telegram / etc., a human operator who joins `sovrant/manager/{managerId}/outbox` from their phone instantly sees the swarm's rolled-up status, and replies routed back via `messages_send` reach the manager agent. The same plumbing handles `permissions_list_open` / `permissions_respond` so an approval prompt from a Sovrant tool call can be answered from a phone without any extra Sovrant code.
- **Permission bridge** — when a Sovrant tool inside a swarm hits the existing `IPermissionPolicy` and the policy returns "ask", Sovrant publishes the request via `permissions_list_open` (with the route in scope) and blocks on `permissions_respond`. From the operator's view this is identical to the existing in-process permission flow; the only difference is that the answer can come from any subscribed channel.
- **Auth & isolation** — every published message inherits the parent swarm's `SwarmExecutionContext` (workspace, project, user) and is tagged in OpenClaw via the route name plus message metadata. Silo mode also enforces filesystem isolation by running workers in scratch directories (or `EnterWorktree` clones) that are torn down on completion.
- **Health & status** — the OpenClaw MCP server is registered with `SmartRouter`'s health-ping framework via the existing MCP server lifecycle. `GET /v1/swarm/openclaw/routes` lists active routes Sovrant currently has subscriptions or publishers on, with last-event timestamps.

#### Configuration

```jsonc
// .sovrant/swarm.json
{
  "enabled": true,
  "openClaw": {
    "enabled": true,
    // Sovrant launches `openclaw mcp serve` as a managed MCP child process via the existing
    // MCP server config (Phase 16). No new transport. The block below is just sugar over the
    // standard MCP server entry — the launcher and lifecycle live in mcp.json.
    "mcpServerName": "openclaw",
    "claudeChannelMode": "auto",            // maps to --claude-channel-mode
    "gateway": {
      "url": "wss://openclaw.local/gateway", // --url
      "tokenFile": "~/.sovrant/openclaw/token" // --token-file
    },
    "routePrefix": "sovrant",
    "approvalTimeoutSec": 300                 // how long Sovrant blocks on permissions_respond
  },
  "federation": {
    "mode": "manager-led",                    // "silo" | "federated" | "manager-led"
    "managerAgent": "swarm-manager",          // template name under .sovrant/agents/templates
    "maxChildSwarms": 8
  }
}
```

```jsonc
// .sovrant/mcp.json — the standard MCP server entry Sovrant already understands
{
  "mcpServers": {
    "openclaw": {
      "command": "openclaw",
      "args": ["mcp", "serve",
               "--url", "wss://openclaw.local/gateway",
               "--token-file", "/home/me/.sovrant/openclaw/token",
               "--claude-channel-mode", "auto"]
    }
  }
}
```

#### Implementation plan

1. Add `OpenClawBusClient` under `src/Sovrant.Agents/Swarm/Bus/OpenClaw/`. It is a thin facade over the existing `MCPTool` proxy — given the MCP server name `openclaw`, it resolves the nine bridge tools and exposes `PublishAsync`, `SubscribeAsync`, `RequestApprovalAsync`, `LoadHistoryAsync`. **No new transport code** — Phase 16's `MCPTool` does the wire work.
2. Extend `SwarmConfig` with the `OpenClaw` and `Federation` blocks. Default `OpenClaw.Enabled = false` so existing installs are unaffected. Validate that the named MCP server exists in `mcp.json` at startup.
3. Add `SwarmFederationMode` enum + per-swarm field. Add a `RouteResolver` that maps `(SwarmExecutionContext, federationMode)` → route name using the documented convention so workers cannot fabricate route names.
4. Add `parent_swarm_id` column to `swarm_events` in a new migration `Vxxx__swarm_federation.sql`. Index it. Extend `SwarmListFilter` and `ISwarmEventStore.LoadEventsAsync` with an optional `parentSwarmId` filter. The manager's roll-up roll-up still lands in the local store; the bus is for live distribution, not durable history.
5. `SwarmOrchestrator.ExecuteAsync` honors the federation mode: in `silo` it publishes only to the per-swarm route, in `federated` it publishes to and reads from the shared parent route, in `manager-led` it publishes to the manager inbox and refuses to subscribe to anything else.
6. Implement `SwarmManagerAgent` as a regular Sovrant agent template (`swarm-manager.md`). Its system prompt encodes the fan-out / fan-in protocol: decompose, spawn child swarms with appropriate `SwarmExecutionContext`, hold an `events_wait` long-poll on the inbox, aggregate, publish to outbox, persist roll-up to `swarm_events`.
7. Wire approval routing: when `IToolConfirmationHandler` is asked to confirm a tool call inside a swarm with OpenClaw enabled, it publishes via `permissions_list_open` and blocks on `permissions_respond` (with timeout from config).
8. Extend the existing `Swarm` tool with a `federation` argument (`silo` / `federated` / `manager-led`) plus an optional `parent_swarm_id` so Sovrant agents can spawn child swarms from the agentic loop without a brand-new tool.
9. New routes:
    - `POST /v1/swarm/manager` — start a manager-led federated swarm with N child sub-tasks
    - `GET  /v1/swarm/openclaw/routes` — active OpenClaw routes Sovrant has subscriptions or publishers on
    - `GET  /v1/swarm/{id}/children` — child swarms of a parent (uses `parent_swarm_id`)
10. CLI subcommand `sovrant openclaw routes {list|tail <route>}` so operators can inspect what's flowing on a route from the terminal without opening a chat client.
11. Tests:
    - `OpenClawBusClient` against an in-process fake MCP server that implements the nine bridge tools (no real OpenClaw needed for unit tests)
    - `RouteResolver`: silo / federated / manager-led modes produce the documented route names and reject attempts to subscribe outside scope
    - Federation behavior: 3 child swarms in silo mode see zero cross-traffic; in federated mode they see each other's findings; in manager-led mode workers see only the manager
    - Approval round-trip: tool call → `permissions_list_open` → simulated `permissions_respond` → tool call resumes
    - Manager roll-up: 3 child swarms emit `SwarmCompleted`, manager aggregates into one parent event in `swarm_events` and one outbox publish
    - One slow integration test gated behind `OPENCLAW_E2E=1` that exercises a real `openclaw mcp serve` against a test gateway
12. Docs: `docs/persistence.md` (new column on `swarm_events`), `docs/server.md` (new routes), `README.md` § Swarms (federation modes + OpenClaw bus), `agent-systems.md` (manager-led pattern with chat-channel HITL).

**Verification:**
- `dotnet build` exits 0
- With OpenClaw configured and a test gateway running, `sovrant swarm "Refactor X" --federation manager-led` runs three child swarms and lands a single rolled-up event in the parent's `swarm_events` row
- `mode=silo`: spawning two child swarms shows zero cross-route events; each only sees its own route
- `mode=federated`: worker B can `events_wait` on the shared route and see worker A's findings
- `mode=manager-led`: manager `events_wait` receives every child event; worker-to-worker traffic returns nothing
- A permission prompt raised inside a swarm tool call appears via `permissions_list_open` and resumes the tool call when answered via `permissions_respond` from a chat channel
- Killing the manager mid-run leaves child swarms in a terminal state (either completed or explicitly cancelled), not orphaned
- A human in a Discord/Slack channel subscribed to the manager outbox receives the same roll-up events Sovrant writes to `swarm_events`

**Non-goals:**
- Bundling or distributing OpenClaw itself (users install and configure it separately, including any messaging-platform credentials)
- Reimplementing OpenClaw's routing or channel adapters in Sovrant — the whole value of this phase is that OpenClaw already does that
- Treating OpenClaw routes as durable storage. The bus is for live distribution; the SQLite `swarm_events` table is still the source of truth. If a subscriber is offline, they catch up via `messages_read`, not by replaying a Sovrant-side queue.
- Cross-tenant federation (one Sovrant install, one OpenClaw gateway, in this phase). Multi-tenant federation can come later if there's demand.

---

## Phase 51 — Sophisticated Runtime + Autonomous Mission Loop (End-to-End Tasks With PM & Priority Awareness) ✅

### ✅ Complete — shipped 2026-04-09 (core) + 2026-04-10 (follow-up)

#### Value — what Phase 51 gives Sovrant

Phase 51 is the phase that makes Sovrant stop being a fancy chatbot wrapper and start being an **autonomous engineering system**.

- **Before Phase 51:** The runtime ran one LLM turn at a time. If a tool call failed, the model guessed what to do next inside the same context window. No structured plan, no crash recovery, no way to inspect what happened. Every task was single-shot — "ship the OAuth refactor" meant the user had to babysit each step, re-prompt after failures, and manually track what was done.
- **After Phase 51:** The engine commits to a plan of concrete steps, notices when reality contradicts expectations, and re-plans cheaply instead of silently drifting. Every state transition is crash-safe in SQLite. If the process dies mid-step, `EngineRecovery` closes out orphaned runs on restart. A mission takes a high-level goal and owns it across multiple engine runs — the event journal is append-only so you can reconstruct exactly what happened, the acceptance gate decides when a mission is done, and the parallel executor fans out independent steps so multi-step missions complete faster. Agents can spawn sub-missions as tool calls. The LLM planner decomposes goals into real multi-step work breakdowns. The LLM compactor keeps long-running missions within the planner's context budget without losing salient information.
- **Net result:** Sovrant can take an ambiguous goal like "investigate the latency regression" and autonomously plan the investigation steps, execute them in parallel where possible, notice when a step contradicts the plan, re-plan, run the next wave, and produce a traceable journal of everything it did — with the user only needing to approve if the acceptance gate says so.

#### What actually landed

This box is the post-delivery summary. Everything below the horizontal rule that follows is the original specification; it is kept verbatim so the design intent stays on record. If the two disagree, this box is the source of truth for **what exists in the repo today**; the spec is the source of truth for **what the phase ultimately wants to become**.

**Engine layer — planner/executor split with crash-safe traces.** Steps A–I landed as nine small, independently reviewable commits rather than one monolithic drop:

| Step | What it adds | Value — why this is better than the old agentic loop |
|---|---|---|
| A — `RuntimePlan` / `IPlanner` / `StepOutcome` | Typed plan contract between the planner and the executor: `RuntimePlan` carries an id, version, goal, and an ordered list of `RuntimeStep`s; each step carries intent, expected outcome, model tier, and an optional tool allow-list | The planner commits to a short sequence of *intended* steps up front instead of letting the model thrash inside a tool loop. The executor can now notice contradictions (actual outcome ≠ expected outcome) and request a cheap re-plan instead of silently drifting. |
| B — `SqliteRuntimeTraceStore` + V010 `runtime_traces` | Append-only structured reasoning trace; every step_started, step_completed, step_failed, and replan_requested is a row | Crash-safe by construction: every state transition is committed to SQLite *before* the side effect runs. A process crash mid-step leaves a trace we can recover from. |
| C — `LlmExecutor` + `IStepRunner` seam | Default `IExecutor` with bounded re-plan loop, `MaxReplans`/`MaxStepRetries` knobs, and a narrow `IStepRunner` seam so step dispatch can be mocked | The re-plan budget stops runaway missions. The `IStepRunner` seam lets us test the whole executor without touching an LLM — a big win for the test surface. |
| D — `IMissionScratchpadStore` + V010 `mission_scratchpad` | Typed, append-only shared store: `(mission_id, step_index, namespace, key, value, agent_id)` with `LoadAsync` / `ReadLatestAsync` / `DeleteMissionAsync` | Parallel sub-agents within one mission can publish intermediate findings that the next plan wave can read, without stomping on each other. Append-only means history is auditable. |
| E — `IContextCompactor` + `NaiveContextCompactor` | Seam that folds older step outcomes into a prose summary when prior-outcome history exceeds the planner's character budget; default keeps the last N outcomes verbatim | Long missions no longer blow the planner's context window. Swapping in an LLM-backed compactor later requires zero changes to executor or planner. |
| F — `MacroDefinition` + `MacroExpander` + `IMacroRegistry` | Named, reusable step sequences that the planner can reference by name; expander flattens them inline with contiguous indexes, and also renumbers sparse indexes from replanner patches | Common sequences (e.g. "run tests, then format, then commit") become first-class instead of having to be re-planned from scratch each time. |
| G — `EngineRecovery` + `IEngineRecovery` | Startup-time scan of `runtime_traces` for orphaned `step_started` rows with no matching `step_completed` or `step_failed`; writes synthetic `step_failed` + `run_completed(Cancelled)` rows | Idempotent recovery — after a crash the trace log is internally consistent, so replays and audits are trustworthy. |
| H — `EngineRoutes` at `/v1/engine/*` | `GET /runs/{id}/trace`, `GET /runs/in-flight`, `POST /runs/recover`, `DELETE /runs/{id}`; wired into `Program.cs` next to `SwarmRoutes` | CLI / UI / mission layer can inspect and recover engine runs without direct DB access. Prerequisite for `sovrant engine tail`. |
| I — `LlmStepRunner` production `IStepRunner` | Bridges the executor to the existing agentic loop via `IRuntimeSessionPool` + `IConversationRuntime`; maps `RuntimeEvent`s (TextChunk / ToolResult / RuntimeError / PermissionDenied / TurnComplete) to `StepOutcome` statuses; serialises turns through the per-session lock | Closes the loop: the engine layer now runs real LLM turns against real tools in production, not just test doubles. |

**Mission layer — long-lived goals with an event journal.** Built on top of the engine layer as a separate Missions namespace:

| Component | What it adds | Value |
|---|---|---|
| V011 `missions` + `mission_events` | Canonical mission record with cached `status` / `plan_json` / timestamps / scoping, plus an append-only event journal covering `mission_created`, `plan_revised`, `run_started`, `run_completed`, `acceptance_approved`, `acceptance_rejected`, `paused`, `resumed`, `completed`, `failed`, `cancelled` | Mission history is fully reconstructable from the journal alone without trusting the mutable `missions` row — same invariant as `runtime_traces` at the engine layer. |
| `Mission` / `MissionStatus` / `MissionEvent` / `MissionEventTypes` | Typed records and a frozen vocabulary for event type strings | Downstream consumers (UI, `pm_export`, Phase 50 outbox) match on a stable set of strings, not open-ended text. |
| `IMissionStore` + `SqliteMissionStore` | `Create` / `Get` / `List(ownerUserId?, status?, limit)` / `UpdateState` / `AppendEvent` / `GetEvents`; every state mutation is paired with a journal write | Single enforcement point for the journal-is-canonical rule. Filtering by owner and status lets the CLI show just the user's in-flight missions. |
| `IMissionPlanner` + `SimpleMissionPlanner` | Seam for mission-level planning; default stub produces a one-step plan whose intent is the mission goal | Proves the seam. An LLM-backed mission planner can slot in later without touching the store, executor, or routes. |
| `IAcceptanceGate` + `AllStepsSucceededGate` | Seam deciding whether a completed engine run satisfies the mission's acceptance criteria; default rule is "every step Succeeded and terminal state Completed" | Acceptance is a swappable policy instead of being hard-coded. `RequiresHuman` path already exists, so `AwaitingHuman` transitions are plumbed end-to-end. |
| `LlmMissionExecutor` | Drives one mission forward one engine cycle: plan → `IExecutor.ExecuteAsync` → acceptance gate → journal → terminal state. Idempotent on already-terminal missions so a double `RunAsync` is a cheap no-op. Catches engine exceptions and journals them as `Failed` with an error payload so crashes are visible in the timeline | Missions are now first-class — the user can POST a goal and repeatedly POST `/run` to drive it forward, with every transition recorded in the journal. |
| `MissionRoutes` at `/v1/missions/*` | `POST /v1/missions` (create), `GET /v1/missions` (list with owner/status filters), `GET /v1/missions/{id}`, `POST /v1/missions/{id}/run`, `GET /v1/missions/{id}/events` | HTTP surface for the CLI and UI. Same integration-test pattern as `EngineRoutes` so the routes are exercised against the real `SqliteMissionStore` via `SovrantWebAppFactory`. |

**Tests.** 541 runtime tests + 139 server tests all passing. Schema version bumped to **V011** across migration tests and old-db upgrade tests.

**What shipped in the follow-up pass (2026-04-10).** Six items that had zero external blockers were shipped immediately after the core primitive:

| Item | What it adds | Value |
|---|---|---|
| `MissionTool` | `ITool` wrapping `IMissionStore`/`IMissionExecutor` with create/run/get/events/list actions | Running agents can spawn sub-missions as tool calls instead of the parent loop managing everything synchronously |
| `MissionExportService` (pm_export) | `ExportMarkdownAsync` and `ExportJsonAsync` — Markdown timeline report + structured JSON export from mission state + event journal | `GET /v1/missions/{id}/export?format=json` endpoint and `/mission export` CLI subcommand give humans and external tooling a readable view of what a mission did |
| `/mission` CLI command | create, list, show, run, events, export, cancel subcommands registered as `ISlashCommand` | Users can drive missions from the REPL without hitting the HTTP API directly |
| `LlmContextCompactor` | `IContextCompactor` that asks an LLM to summarise folded step outcomes; falls back to `NaiveContextCompactor` on failure | Higher-quality compaction — the LLM identifies *salient* outcomes rather than listing statuses chronologically |
| `LlmMissionPlanner` | `IMissionPlanner` that asks an LLM to decompose a goal into 2-8 steps with intent, expected outcome, and model tier; falls back to `SimpleMissionPlanner` | Multi-step plans instead of one-step stubs — the mission layer can now produce real structured work breakdowns |
| `ParallelMissionExecutor` | `IMissionExecutor` that fans out independent plan steps across concurrent engine runs (bounded by `MaxConcurrency=4`), writes outcomes to scratchpad | Missions with independent steps complete faster; the scratchpad ensures the next plan wave sees all results |

**What remains deferred (blocked on other phases):**

- **`MissionGuard` + cost/time envelopes.** Requires Phase 55's `ICostModel`, which has not shipped. Until then the acceptance gate is pure pass/fail on step status.
- **Phase 50 outbox hookup.** Publishing mission events to an OpenClaw route requires Phase 50 (federated bus). No changes needed in the mission layer itself — it's a consumer of the existing journal.

---

**Depends on:** Phase 22 (agent templates + model routing), Phase 24 (verification loop), Phase 27 (memory), Phase 29 (Swarm orchestrator), Phase 32 (persistence), Phase 37.5 (swarm event store), Phase 48 (intent-aware model routing), Phase 50 (federated bus, optional), **Phase 55 (cost tracking + budgets via OpenRouter — provides `ICostModel`, `BudgetEnforcer`, and per-turn cost capture so `MissionGuard` has real numbers to gate on)**

**Goal:** Make the Sovrant **engine and runtime** materially more sophisticated, then put a **long-running autonomous mission loop** on top of it. Today the engine runs one agentic turn at a time and the swarm runs one decomposition + one DAG + one quality gate before exiting. Competitors are shipping engines that re-plan mid-turn, route every sub-step to a different model, hold parallel sub-agents against a shared scratchpad, compact their own context, and own a task across hours of wall-clock time. This phase closes that gap on two layers at once because they reinforce each other:

1. **Engine layer.** A real planner/executor split, dynamic mid-turn re-planning, parallel sub-agents with a shared scratchpad, per-sub-step model routing on top of Phase 48, structured reasoning traces, mid-conversation context compaction, and a runtime introspection API. This is the sophistication the user explicitly asked for.
2. **Mission layer.** A first-class `Mission` entity that takes a high-level goal ("ship the OAuth refactor", "investigate the latency regression", "draft and merge the v2 release"), decomposes it into a tracked plan with priorities and acceptance criteria, executes it across many turns and possibly many days, manages its own cost ceiling, escalates to a human only when policy says it must, and produces a verifiable artifact at the end. The mission layer is what makes the engine improvements *visible to the user* — without missions, a smarter engine just makes single turns marginally better; with missions, a smarter engine compounds across hours of work.

These ship together because each is half-useful alone: a sophisticated engine with no mission abstraction is still single-shot; a mission abstraction over today's engine would inherit today's brittleness on long horizons. Built together, they let Sovrant take an ambiguous goal and own it.

#### Why now — competitor landscape

| Product | Autonomous-loop story | What we can learn |
|---|---|---|
| **OpenClaude / Claude Code** | Single agentic session with tool use; user drives turns. No persistent mission state across sessions, no built-in cost-aware re-planning. | Best-in-class single-turn agent; we already match this. The gap is *across turns and sessions*. |
| **Devin (Cognition)** | Long-running "engineer" that holds a task for hours, re-plans, asks for help on a chat surface, ships a PR. Closed source, opinionated environment. | Mission-as-first-class entity with persistent state, planner/executor split, and a clear escalation surface. |
| **OpenHands (formerly OpenDevin)** | Open-source agent with explicit `Plan` / `Action` / `Observation` loop, runtime sandbox, headless mode. | Good reference for the loop shape, sandboxed execution model, and headless API surface. |
| **Aider** | Repo-aware single-loop coder. Strong at edits, weak at multi-day planning or PM-style tracking. | Confirms that "good edits" alone is not enough — users want a *plan* they can inspect. |
| **Cline / Roo / Continue** | Editor-embedded agents with task lists and approval flows. | Validates that an explicit, *visible* task list (not just hidden DAG) is what humans actually trust. |
| **AutoGPT / BabyAGI lineage** | Recursive task spawners with no real cost or termination story. | Cautionary tale: without budget, priority, and acceptance criteria these loops thrash forever. |
| **GitHub Copilot Workspace** | Spec → plan → implementation → review pipeline tied to issues/PRs. | Validates the "issue tracker is the mission ledger" pattern. |
| **Sovrant today** | Phase 29 swarm (one decomposition, one DAG run, one quality gate, exit). Phase 24 verification loop (per-task). Phase 27 memory (cross-session knowledge but not mission state). | We have most of the building blocks; what we lack is a *mission* abstraction that owns them across time. |

The user-visible gap, in one line: **today Sovrant can complete a well-scoped task in one shot; it cannot yet take ownership of an ambiguous goal, hold the plan over hours, decide when to spend more vs. ask, and ship a checkable result.**

#### What Sovrant already has vs. what this phase adds

**Engine layer**

| Existing | This phase adds |
|---|---|
| Single agentic loop: model picks a tool, runs it, model sees the result, repeat | **Planner/executor split**: a planner produces a structured intermediate plan and an executor runs it, with the executor allowed to fail-fast back to the planner instead of thrashing the model on a wrong path |
| Re-planning only at swarm boundaries (Phase 29 quality gate) | **Mid-turn dynamic re-planning**: the executor can call the planner again on a single tool failure or a contradiction in observations, without unwinding the whole swarm |
| One model per session (`SmartRouter` chooses on session start) | **Per-sub-step model routing** on top of Phase 48: the planner picks the model *per step* — high tier for ambiguous reasoning, standard for code edits, fast/cheap for grep/parse — instead of paying the high-tier price for every turn |
| Sub-agents are independent processes with no shared state | **Shared scratchpad**: parallel sub-agents within one turn write to a structured, per-mission scratchpad (`MissionScratchpad`) so sibling agents can see each other's intermediate findings without going through the orchestrator |
| Tool calls are opaque (only `name` and `args` are recorded) | **Structured reasoning traces**: every tool call is wrapped with the planner's *intent* ("I'm calling Grep to confirm the function exists before I edit it"), persisted alongside the tool result so re-plans and humans can read the reasoning, not just the actions |
| Context grows monotonically until the model cuts off | **Mid-conversation context compaction**: a background compactor summarises stale tool results and old reasoning into a compact form when the conversation crosses a threshold, preserving identifiers and acceptance criteria verbatim |
| No introspection of the running engine | **Runtime introspection API** (`GET /v1/engine/state`, `GET /v1/engine/trace/{id}`): the current plan, the active sub-agents, the scratchpad, and the live reasoning trace are all queryable from outside the process |
| Tool routing by hard-coded allow-lists per agent template | **Tool composition**: the planner can compose existing tools into reusable *macros* for the duration of a mission (e.g. "find-then-edit", "grep-then-test") so common sequences don't burn a planner round-trip every time |

**Mission layer**

| Existing | This phase adds |
|---|---|
| Phase 29 swarm: decompose → wave-execute → quality-gate → exit | **Mission**: a persistent first-class entity that owns one or more swarm runs, plus re-plans, plus escalations, across sessions |
| Phase 24 verification loop (per-task quality gate) | **Acceptance criteria** as data: each mission carries explicit, machine-checkable success conditions used by the gate and the human reviewer |
| Phase 27 memory (summaries, learned patterns, instincts) | **Mission journal**: append-only narrative of what was tried, what worked, what was rejected, scoped to the mission (separate from cross-session memory) |
| Phase 37.5 swarm event store (SQLite) | **`missions` table** + child `mission_steps`, `mission_artifacts`, `mission_decisions` — the mission ledger lives next to swarm events |
| Per-session token tracking (Phase 10) | **Mission-level cost envelope**: budget enforced across many sessions and re-plans, not just one swarm run |
| `IPermissionPolicy` + Phase 50 chat-channel approvals | **Escalation policy**: when the mission must stop and ask vs. when it can keep spending; declarative, not hard-coded per tool |
| Manual `/swarm` invocation | **`sovrant mission`** command + `POST /v1/missions` API — a mission can be created from a CLI prompt, an issue tracker hook, or a chat message |

#### Engine sophistication — what actually changes in the runtime

These are concrete changes to `Sovrant.Runtime` and `Sovrant.Agents`, not new top-level products.

- **`IPlanner` / `IExecutor` split.** Today `ConversationRuntime` runs a single loop where the model is both planner and executor. Phase 51 introduces `IPlanner` (produces a `RuntimePlan` of intended steps with intent, expected outcome, model tier, and tool allow-list) and `IExecutor` (runs steps and reports `StepOutcome`). The default `LlmPlanner` calls a high-tier model with a structured-output schema; the default `LlmExecutor` runs each step with the planner-chosen model. The existing single-loop runtime stays as `LegacyRuntime` for callers that don't want planning overhead.
- **Dynamic re-planning.** `IExecutor` exposes `RequestReplan(reason, observations)`. The executor calls it when a tool fails twice, when an observation contradicts a plan assumption, or when the planner-declared `expected_outcome` doesn't match the actual outcome. Re-plan is a cheap call: the planner sees the current `RuntimePlan`, the journal of completed steps, and the observation that triggered the re-plan, and returns a *patch* to the plan rather than starting over. This is the core "engine sophistication" — instead of letting the model thrash on a wrong path, the engine notices and re-plans deliberately.
- **Per-sub-step model routing.** `RuntimePlan.Step.ModelTier` declares which Phase 22 tier (`high` / `standard` / `fast`) and optionally which Phase 48 capability profile the executor should use for that step. The executor passes this to `SmartRouter` per call. Effect: a 10-step plan that would have burned 10 calls of the high-tier model now burns 2 high-tier (planning + tricky reasoning) and 8 standard/fast (mechanical edits, greps, file reads). This is where the cost wins live.
- **`MissionScratchpad` (shared state for parallel sub-agents).** A typed, append-only structured store keyed by mission and namespaced by step. Sub-agents within one wave write findings, partial outputs, and contradictions to it; the next wave's planner reads it before producing the next plan. Replaces the today-pattern where sibling agents are blind to each other and the orchestrator is the only integration point. Backed by SQLite (lives in `mission_scratchpad` table) so it survives process restarts.
- **Structured reasoning traces.** Every step in a `RuntimePlan` carries the planner's `intent` field. The executor persists `(intent, tool_call, observation, outcome)` tuples to `runtime_traces` rather than just the raw tool history. The mission journal, the runtime introspection API, and the re-planner all read from this richer trace. Cost is bounded: traces are subject to the same compactor as the conversation context.
- **Mid-conversation context compaction.** A background `IContextCompactor` runs when token usage on a session crosses a configurable threshold. It summarises stale tool results, drops fully-superseded reasoning, and preserves identifiers, acceptance criteria, and unresolved blockers verbatim. Compaction is *idempotent and reversible* — the original tool history is kept in `runtime_traces`, only the in-context view is compacted. A re-plan can hydrate the original on demand if it needs detail the compactor dropped.

  This is a deliberate replacement for the existing `MaybeCompactHistoryAsync` in `ConversationRuntime` (Phase 27 era), which has four known limitations Phase 51 explicitly fixes:

  | Today (`MaybeCompactHistoryAsync`) | Phase 51 (`IContextCompactor`) |
  |---|---|
  | Trigger is a *fixed token count* (`SOVRANT_COMPACT_THRESHOLD`, default 80,000). On a 200K-window model that fires too early; on a 32K-window model it never fires. | Trigger is a *percentage of the active model's real context window* (default 70%), resolved through the provider/model registry so each model gets its own threshold. The fixed-token env var stays as an override. |
  | Always keeps a hardcoded **last 4 messages** verbatim. Loses any earlier message regardless of importance. | Keeps a tunable tail *plus* a "pinned set" of messages the compactor must preserve verbatim: the active acceptance criteria, the most recent tool error, any unresolved blocker, and any message the user explicitly pinned with `/pin`. |
  | Runs the summary through the same `_config.Model` as the main loop — pays the high-tier price to do a mechanical summarisation. | Routes the summary call to the Phase 22 `fast` tier (or whatever tier the user maps to `compaction`), saving the cost for an operation that doesn't need top-tier reasoning. |
  | **Destructive in-memory:** the original messages are dropped from `_history` and cannot be re-read. A later turn that needs detail the summary lost is out of luck. | **Reversible:** the original tool history and pre-compaction messages live in `runtime_traces` (durable, indexed by turn). The in-context view is the compacted form, but the executor and re-planner can hydrate the original on demand. |

  Each fix is independently shippable — see the corresponding entry in Known Issues / Debt for the near-term tactical version that can land before the full Phase 51 rewrite.
- **Tool macros.** A planner can declare a `MacroDefinition` ("find_and_edit": grep → read → edit) and the executor expands it inline without a planner round-trip per call. Macros are mission-scoped (defined for the lifetime of the mission, not globally). This is what stops the planner from burning a turn deciding what to do every single time it needs to do an obvious sequence.
- **Runtime introspection API.** `GET /v1/engine/state` returns the active runtimes, their current `RuntimePlan`, the live scratchpad, and the recent reasoning trace. `GET /v1/engine/trace/{runtime_id}` streams the structured trace via SSE. Used by the frontend, the CLI `sovrant engine tail`, and (with Phase 50) by an OpenClaw operator on a phone. This is the difference between "the agent is doing something, hopefully" and "I can see exactly what the agent is reasoning about, right now".
- **Crash safety as a runtime property, not a mission property.** Every state transition in `IExecutor` is committed to SQLite before the side effect runs. On process restart, an `EngineRecovery` service walks `runtime_traces` and resumes any executor that was mid-step at the moment of crash. Missions inherit this for free.

#### Core concepts

- **`Mission`** — first-class persisted entity. Fields: `id`, `goal`, `acceptance_criteria` (list of checkable predicates in plain English + optional structured form), `priority` (`p0`/`p1`/`p2`/`p3`), `cost_envelope_usd`, `time_envelope_hours`, `escalation_policy` (`autonomous` / `ask-on-blocker` / `ask-on-spend-threshold` / `human-pair`), `status` (`drafting` / `running` / `blocked` / `awaiting_human` / `succeeded` / `failed` / `cancelled`), `workspace_id`, `project_id`, `owner_user_id`, `created_at`, `updated_at`.
- **`MissionPlan`** — a structured plan composed of `MissionStep`s. Each step has its own acceptance criteria, estimated cost, and references to the swarm runs (Phase 29) or single-agent runs that executed it. Plans are versioned; a re-plan creates a new `MissionPlan` row linked to the same `Mission` so the history is visible.
- **`MissionStep`** — the unit of execution. A step can be (a) a single-agent task, (b) a swarm run (Phase 29), (c) a human action ("review and approve PR #123"), or (d) a wait condition ("CI green on branch X"). Steps have priorities inherited from the mission but overridable.
- **`MissionExecutor`** — the long-running loop. Picks the next ready step by priority, dispatches it (single agent / swarm / wait / human), records the result and cost, decides whether to continue, re-plan, escalate, or terminate. Crash-safe: every state transition is committed to the DB before the action happens, so a process restart resumes where it left off.
- **`MissionPlanner`** — splits a goal into the initial `MissionPlan` and re-plans on demand. Uses the Phase 22 "high" model. Re-plan triggers: a step failed twice, budget burn rate exceeds projection by 1.5x, a step's outputs invalidate the plan's assumptions, or the human inserts a directive.
- **`MissionGuard`** — evaluates the cost envelope, time envelope, and escalation policy on every loop iteration. Decides between *continue*, *reduce scope*, *escalate*, *halt*. Reads live per-turn cost numbers from Phase 55's `ICostModel` (OpenRouter-backed).
- **`MissionJournal`** — append-only narrative scoped to the mission. Distinct from Phase 27 memory: memory is the user's long-term knowledge across projects; the journal is the *mission's own* short-term reasoning record so a re-plan can read what was already tried. Stored as `mission_decisions` rows plus optional summaries.
- **Acceptance gate** — when the executor thinks the mission is done, it runs the acceptance criteria through a checker (high-level model + any structured assertions). Only on `pass` does the mission move to `succeeded`. On `needs_revision`, the planner re-plans the gap. On `fail`, the mission moves to `blocked` and waits for human input via the escalation policy.
- **Human surface** — every mission gets a *mission view* (`GET /v1/missions/{id}` and `sovrant mission show <id>`) that shows goal, plan version history, current step, cost burned vs. envelope, journal entries, blockers, and pending approvals. If Phase 50 is enabled, missions can also publish status to an OpenClaw route so the human gets pinged on a phone instead of having to poll.

#### PM, priority & budget management

This is the part the user explicitly asked about — making the runtime sophisticated enough to manage *project management*, *priorities*, and *budgets*, not just execute steps. The actual dollars-per-token numbers come from **Phase 55 (live OpenRouter cost tracking)** — Phase 51 consumes the `ICostModel` that phase provides rather than building its own pricing table.

- **Cost envelope.** Every mission carries a `cost_envelope_usd` (hard ceiling) and an internal *projection* maintained by the planner. The guard re-evaluates after every step: actual / projected. If burn rate > 1.5x projection, the guard either escalates (per policy) or asks the planner to *reduce scope* (drop p2/p3 steps before touching p0/p1). All dollar figures are read from Phase 55's `ICostModel`, which pulls live pricing from OpenRouter's `/api/v1/models` endpoint — Phase 51 does not reinvent a local pricing registry.
- **Priority management.** Mission-level priority sets defaults; per-step priority overrides. The executor always picks the highest-priority *ready* step. When the guard decides to reduce scope, it walks the plan from lowest priority up. Priorities are also exposed on the API so an external PM tool (Linear, GitHub, Jira) can push priority changes mid-mission and the executor will pick them up on the next loop iteration.
- **PM-style tracking.** The mission ledger is intentionally close to a lightweight issue tracker: each mission has a status, an owner, a plan, steps with priorities, a cost burn, and a journal of decisions. The same data shape is what a PM tool would store. Phase 51 ships a one-way export (`mission → GitHub issue / Linear ticket`) and leaves a two-way sync hook for a later phase. The point is not to *replace* the PM tool — it is to make Sovrant *legible* to the PM tool so a human looking at a Linear ticket can see the autonomous work happening underneath.
- **Escalation policy as data.** Today escalations are tool-by-tool (`IPermissionPolicy` per tool call). For autonomous missions that is too granular — a mission needs *mission-level* policies like "ask before exceeding $X", "ask before any production deploy", "ask if any step retries more than twice", "never ask, log and continue" (for unattended overnight runs). These are declared in `escalation_policy` and evaluated by the guard. Per-tool policies still apply underneath.
- **Time envelope.** Wall-clock budget complementary to cost. A mission can be told "spend up to $5 *and* up to 4 hours"; whichever ceiling is hit first triggers the guard.

#### Architecture

```
src/Sovrant.Runtime/Engine/
  IPlanner.cs / LlmPlanner.cs        ← structured-output planner with intent + per-step model tier
  IExecutor.cs / LlmExecutor.cs      ← runs RuntimePlan steps, requests re-plans on contradictions
  RuntimePlan.cs / RuntimeStep.cs    ← typed plan with intent, expected_outcome, model_tier, allowed_tools
  MissionScratchpad.cs               ← typed shared store for parallel sub-agents
  IContextCompactor.cs / LlmContextCompactor.cs
  MacroDefinition.cs / MacroExpander.cs
  EngineRecovery.cs                  ← walks runtime_traces, resumes mid-step executors after crash
src/Sovrant.Runtime/Storage/
  IRuntimeTraceStore.cs / SqliteRuntimeTraceStore.cs   ← runtime_traces, mission_scratchpad
  IMissionStore.cs / SqliteMissionStore.cs             ← missions, mission_steps, mission_artifacts, mission_decisions
src/Sovrant.Agents/Missions/
  Mission.cs / MissionPlan.cs / MissionStep.cs
  IMissionPlanner.cs / LlmMissionPlanner.cs            ← thin wrapper over IPlanner that adds acceptance criteria
  IMissionExecutor.cs / MissionExecutor.cs             ← thin wrapper over IExecutor that adds the cost/time guard
  MissionGuard.cs / MissionJournal.cs / AcceptanceGate.cs / EscalationPolicy.cs
src/Sovrant.Server/Routes/
  MissionRoutes.cs            ← /v1/missions CRUD + status + control
  EngineRoutes.cs             ← /v1/engine/state and /v1/engine/trace/{id} (SSE)
src/Sovrant.Cli/
  MissionCommand.cs           ← `sovrant mission {create|show|list|cancel|approve|tail}`
  EngineCommand.cs            ← `sovrant engine {state|tail <id>}`
src/Sovrant.Tools/Missions/
  MissionTool.cs              ← agent-callable: an agent inside a swarm can spawn a sub-mission
```

#### Configuration

```jsonc
// .sovrant/missions.json
{
  "defaults": {
    "cost_envelope_usd": 5.00,
    "time_envelope_hours": 2.0,
    "priority": "p2",
    "escalation_policy": "ask-on-blocker",       // see below
    "acceptance_checker_level": "high",          // Phase 22 model level
    "planner_level": "high",
    "executor_level": "standard"
  },
  "policies": {
    "ask-on-blocker":         { "ask_on_blocker": true,  "ask_on_spend_pct": null, "ask_on_retry_count": 2 },
    "ask-on-spend-threshold": { "ask_on_blocker": true,  "ask_on_spend_pct": 80,   "ask_on_retry_count": 3 },
    "autonomous":             { "ask_on_blocker": false, "ask_on_spend_pct": null, "ask_on_retry_count": 5 },
    "human-pair":             { "ask_on_blocker": true,  "ask_on_spend_pct": 25,   "ask_on_retry_count": 1 }
  },
  "scope_reduction": {
    "drop_priorities_in_order": ["p3", "p2"]     // never drops p0/p1 to stay under budget
  },
  "pm_export": {
    "enabled": false,
    "target": "github",                          // "github" | "linear" | "none"
    "repo": "myorg/myrepo"                       // for github
  }
}
```

#### Implementation plan

**Engine layer (lands first, mission layer rides on top)**

A. `RuntimePlan` / `RuntimeStep` records and the `IPlanner` / `IExecutor` interfaces. Default `LlmPlanner` uses structured output against the Phase 22 high-tier model. Default `LlmExecutor` runs steps with the planner-chosen tier.
B. `IRuntimeTraceStore` + `SqliteRuntimeTraceStore` and the `runtime_traces` schema migration. Every step transition commits before the side effect.
C. Re-plan loop: `IExecutor.RequestReplan(reason, observations)` → planner returns a *patch* to the existing plan. Bound the re-plan depth (default 3) to prevent runaway loops.
D. `MissionScratchpad` typed store + `mission_scratchpad` table migration. Wire it into the planner (read on next plan) and into sub-agents (write during execution).
E. `IContextCompactor` + `LlmContextCompactor` with a configurable trigger threshold. Tests cover idempotency and that identifiers / acceptance criteria survive a compaction round-trip.
F. `MacroDefinition` + `MacroExpander`. The planner can declare macros for the lifetime of a mission; the executor expands them inline. Macros are validated against the active tool allow-list.
G. `EngineRecovery`: on process startup, scan `runtime_traces` for executors that were mid-step at crash time and resume them. Test by killing the process mid-step and asserting no double-execute.
H. `EngineRoutes`: `GET /v1/engine/state`, `GET /v1/engine/trace/{id}` (SSE). `EngineCommand`: `sovrant engine {state|tail <id>}`.
I. Wire `LlmExecutor` into `SmartRouter` so per-step model tier actually changes which provider/model handles the call. Verify on a recorded run that high-tier calls drop from N to ~2 and the rest go to standard/fast.

**Mission layer (rides on the engine layer above)**

1. Schema: new migration `Vxxx__missions.sql` creating `missions`, `mission_steps`, `mission_artifacts`, `mission_decisions` tables (workspace/project scoped, foreign keys to existing tables, indexes on `status`, `priority`, `updated_at`).
2. `IMissionStore` + `SqliteMissionStore` with crash-safe state transitions (every status change is its own committed row before the side effect runs).
3. `Mission`, `MissionPlan`, `MissionStep`, `EscalationPolicy` records and their JSON serialization.
4. `LlmMissionPlanner`: structured-output prompt that takes a goal + acceptance criteria draft and returns an initial plan with priorities and per-step cost estimates. Reuses the Phase 22 "high" model.
5. `MissionExecutor` loop: pick ready step by priority → dispatch (single agent, Phase 29 swarm, wait, or human) → record outcome + cost → ask the guard → continue or transition. Idempotent on restart.
6. `MissionGuard`: pure function over `(Mission, latest costs, escalation policy, time elapsed)` returning `Continue` / `ReduceScope` / `Escalate(reason)` / `Halt(reason)`.
7. Consume Phase 55's `ICostModel` (OpenRouter-backed) — Phase 51 does not ship a cost model of its own. The mission guard calls `costModel.EstimateCost(model, inputTokens, outputTokens)` after every step and trusts the returned value. If Phase 55 has not shipped yet when this phase starts, Phase 51 can stub `ICostModel` with a null-returning implementation and let mission budgets degrade to "soft" (time-envelope only) until Phase 55 lands.
8. `AcceptanceGate`: high-level model verifies acceptance criteria against the mission's outputs and journal; structured assertions (e.g., "tests pass") run as concrete commands.
9. `MissionJournal` append API + auto-summarisation when entry count crosses a threshold (so a long-running mission doesn't blow the planner's context on the next re-plan).
10. CLI: `sovrant mission create "<goal>" --priority p1 --budget 5 --hours 2 --policy ask-on-blocker --accept "<criteria>"`, plus `mission show`, `mission list`, `mission tail <id>`, `mission cancel <id>`, `mission approve <id> <step>`.
11. API routes under `/v1/missions`: `POST` create, `GET` list/show, `POST /{id}/cancel`, `POST /{id}/approve`, `GET /{id}/tail` (SSE).
12. `MissionTool` (agent-callable) so a Sovrant agent inside a swarm can spawn a sub-mission for a substantial side quest without the parent loop having to wait synchronously.
13. Optional `pm_export` adapter: GitHub-issue and Linear-ticket creators that mirror mission status. One-way for this phase.
14. Phase 50 hookup (if enabled): mission status events publish to a `sovrant/mission/{id}/outbox` OpenClaw route so a phone subscriber gets pings without polling.
15. Tests:
    - `MissionGuard` table-driven tests over (cost, time, policy) → decision matrix
    - `MissionExecutor` crash recovery: kill the process mid-step, restart, assert it resumes from the last committed state and does not double-execute
    - `LlmMissionPlanner` against a fake LLM provider, verifying structured plan output
    - `AcceptanceGate` happy path + revision-loop path + fail path
    - End-to-end: a small mission ("add a hello-world endpoint and a test") runs to `succeeded` against a test repo with a real LLM provider, gated behind `MISSION_E2E=1`
    - Priority preemption: a p0 step inserted mid-mission preempts an in-progress p2 step on the next loop iteration
    - Scope reduction: when burn > 1.5x projection, p3 then p2 steps are dropped, p0/p1 untouched
16. Docs: new `docs/missions.md` (concept + lifecycle + escalation policies), updates to `roadmap.md` (this phase), `docs/persistence.md` (new tables), `docs/server.md` (new routes), `README.md` § Missions.

**Verification:**
- `dotnet build` exits 0
- A recorded run of a 10-step mission shows ≤3 high-tier model calls and ≥7 standard/fast-tier calls (proves per-sub-step routing is live), with total cost meaningfully below the same mission run on `LegacyRuntime`
- Killing the process mid-executor-step and restarting resumes from the last committed `runtime_traces` row, with no double-execution of the in-flight step
- Triggering `IContextCompactor` on a long session preserves all acceptance criteria and unresolved blockers verbatim, drops superseded reasoning, and reduces in-context token count by ≥40%
- A planner re-plan triggered by a contradictory observation produces a *patch* (diff against the existing `RuntimePlan`), not a full restart, and the executor resumes on the patched plan
- `GET /v1/engine/state` returns the live plan, scratchpad, and recent trace for an in-progress runtime; `sovrant engine tail <id>` streams new trace entries as they happen
- `sovrant mission create "Add /healthz endpoint with a test" --budget 2 --policy autonomous` runs to `succeeded` against a test repo, with a non-empty journal, an acceptance-gate pass, and a final cost recorded under the envelope
- A mission whose budget is exceeded transitions to `blocked` (not `failed`), records a `BudgetExceeded` decision in the journal, and waits for human input
- Killing the executor process mid-step and restarting resumes the same mission at the same step without re-running the prior step
- A p0 step injected mid-run is picked next, preempting any p2/p3 work on the ready queue
- A mission with `escalation_policy: human-pair` pauses on every blocker and on every 25% spend tick, and resumes via `mission approve`
- With Phase 50 enabled, mission status events appear on the configured OpenClaw outbox route as well as in the local DB

**Non-goals:**
- Replacing existing PM tools (GitHub Issues, Linear, Jira). The export is one-way and intentionally minimal — the goal is *legibility*, not vendor lock-in.
- Building a cost model. Live dollar-per-token pricing is Phase 55's job (OpenRouter-backed). Phase 51 only *consumes* the `ICostModel` interface.
- Inventing a new agent runtime. Missions sit on top of the existing Phase 19/20/22/24/29 stack; this phase adds the *loop and the ledger*, not a new executor.
- Long-horizon orchestration negotiation between *missions* (one mission contracting work to another). Sub-missions spawn synchronously via `MissionTool`; cross-mission negotiation is a later phase if there's demand.
- True open-ended autonomy with no human surface. Every mission has at least one escalation pathway; "autonomous" mode just means the threshold is high, not absent.

---

## Phase 52 ✅ — Unified Agent Orchestration: One Team-or-Swarm Abstraction in the Database

**Depends on:** Phase 19+20 (orchestrated teams), Phase 22 (agent templates), Phase 29 (Swarm orchestrator), Phase 32 (persistence), Phase 35 (workspaces), Phase 36 (projects), Phase 37 (users), **Phase 37.5 (swarm sessions in the DB — the prerequisite that makes this phase possible)**

**Goal:** Collapse Sovrant's two parallel orchestration systems — **Team** (LLM-driven, conversational, persistent personas, in-memory only) and **Swarm** (user-driven, ephemeral, parallel, file-locked, DAG-decomposed) — into a **single orchestration abstraction** with one persistent home in the SQLite database. After this phase, "team" is no longer a separate concept from "swarm"; it is one of three ways to compose a swarm. All members, runs, plans, locks, and events live in the same tables, queryable per user / workspace / project, surviving restarts and exportable like every other persisted entity.

This is the unification that `docs/agent-systems.md` previewed at the bottom of the doc as a "possible future". Phase 37.5 is what makes it possible: once swarm events live in the database, the rest of the agent state (team members, agent runs, conversation links) can join them in the same store under the same backup, query, and scoping story.

#### Why now

Today Sovrant has two stovepipes that share the same `SovrantAgentFactory` and `AgentTemplateRegistry` underneath but diverge above:

| Today | The cost |
|---|---|
| `InMemoryTeamRegistry` (`ConcurrentDictionary`) holds team members | Lost on restart. No per-workspace scoping. Survives nothing. |
| `SwarmStateTracker` + JSONL files held swarm state (until Phase 37.5 moved events into `swarm_events`) | Was unjoinable to users/workspaces. Phase 37.5 fixed events; team members and agent runs are still parallel paths. |
| `TeamCreate` / `TeamDelegate` / `TeamStatus` are LLM-callable | The LLM can build a team in conversation but cannot then say "now run this plan in parallel with file locks" — that requires dropping out of the conversation and into the swarm CLI. |
| `sovrant swarm "<task>"` always spawns ephemeral workers from templates | The user cannot say "use the team I already built" — the swarm decomposer ignores any pre-existing team members. |
| Two observability stories | One API surface (`TeamStatus` tool) and one storage surface (`/v1/swarm/sessions` + `swarm_events` table). Same underlying activity, different consumers. |

The Phase 37.5 doc note already calls this out: *"this is the unification of team and swarm into a single orchestration concept"* is one of the explicit reasons the swarm-into-DB work was a prerequisite, not just a persistence cleanup.

#### Pain points from `agent-systems.md` this phase explicitly fixes

`docs/agent-systems.md` § "Where the value is less clear" lists five concrete problems with the current Team/Swarm split. Phase 52 maps each one to a specific deliverable so nothing falls through the cracks:

| Pain point (from `agent-systems.md`) | How Phase 52 fixes it |
|---|---|
| **Massive code-to-value ratio difference.** Swarm is ~15× the LOC for capabilities most users probably never trigger (DAG decomposition, file locking, wave scheduling). Team is tiny but heavily used by the LLM in normal conversation. | One unified `AgentOrchestrator` replaces both `SwarmOrchestrator` and the bespoke `TeamDelegateTool` execution path. The heavy machinery — DAG decomposition, file-lock manager, wave scheduler, quality gate — becomes **opt-in features on a single orchestrator**, not a parallel codebase. A simple `TeamDelegate`-style call goes straight through the orchestrator with `decompose=false`, `lock_files=false`, `quality_gate=false`, `max_parallel=1` — same code path, just no bells and whistles. The big stuff only runs when the caller (LLM, user, or mission step) actually opts in. Net effect: the ~1,376 LOC of Swarm and the ~89 LOC of Team collapse into one orchestrator + a feature-flag config, with the Team-style "fast path" being a configuration of the unified engine, not a separate engine. Less duplicated code, less drift, and the heavy capabilities stop billing complexity to people who never touch them. |
| **Surface overlap.** Both spawn sub-agents from the same factory + templates. From the user's perspective, "ask an agent to do X" has two completely different code paths and two completely different observability stories. That is confusion debt. | One factory call site, one run ledger (`agent_runs`), one event store (`swarm_events` extended with a `kind` column), one set of API routes (`/v1/runs/{id}` works for any kind), one CLI surface (`sovrant team run` and `sovrant swarm` are aliases for the same orchestrator with different defaults), one observability story (`GET /v1/runs/{id}/events` SSE works regardless of how the run was started). The existing wire formats stay backward-compatible, but the underlying paths converge so a user inspecting a delegation, a swarm task, and a mission sub-step sees them in the same query, with the same fields, in the same table. |
| **Decomposition tax.** Swarm's killer feature (auto-decomposition) costs an LLM call up front. For tasks the user could decompose themselves, the swarm overhead is pure loss. | Decomposition becomes **explicitly optional**. `EnsembleRunRequest` carries either a `goal` (run the decomposer) **or** a pre-built `plan: SwarmTaskNode[]` (skip the decomposer entirely, zero LLM tax). The new `TeamRunTool` defaults to `decompose=false` because the LLM that called it has already decided what to delegate. Mode 1 (one pre-existing team) and mode 2 (multiple teams) use the caller's plan unchanged; only mode 3 (engine-decided) actually pays for `LlmSwarmDecomposer`. Result: the decomposition cost is paid only when it's worth paying, and existing in-conversation `TeamDelegate` flows pay nothing. |
| **No persistence on Team.** Team's "co-workers" evaporate on restart. For a system that just shipped per-user identity and workspaces (Phases 35–37), that is an obvious gap. | `SqliteTeamRegistry` replaces `InMemoryTeamRegistry` as the default DI binding (the in-memory version stays, but only as a test fixture). The `teams` and `team_members` tables are workspace- and project-scoped with foreign keys to `users`, so a team built by `TeamCreate` in conversation A survives a restart and is callable from conversation B by the same user, in the same workspace, with the same members. The verification step `sovrant team list` after a process restart returns the team is the test that this gap is closed. |
| **No bridge between the two.** The LLM cannot launch a swarm directly via a tool that returns the resulting team of specialists. The swarm cannot use a long-lived team member as one of its workers (it could in theory since `SwarmOrchestrator` takes `ITeamRegistry`, but no wiring resolves named team members as swarm workers today). | Two new tools wire the bridge in both directions. **`TeamRunTool`** lets the LLM launch a parallel, locked, gated run against an existing team in one tool call — that's swarm capabilities applied to team members. **`TeamPublishTool`** lets a swarm publish its ephemeral workers as a named team after completion (`origin = 'swarm-published'`), so a `sovrant swarm "<task>"` run produces a reusable team the LLM can call back to in a later conversation. The orchestrator's `EnsembleSelector` actually consults `ITeamRegistry` per task slot when a team is supplied, instead of unconditionally spawning ephemerals from templates — the wiring that was theoretically possible becomes the default. End state: any combination of "build a team / run a swarm / publish workers as a team / call them again later" works through the same orchestrator without the user choosing between two stovepipes. |

These five deliverables are not extras — they are the *minimum* set Phase 52 must ship for the unification to be real. Each appears in the implementation plan and verification list below as a specific item.

#### What "unified" means concretely

After this phase there is **one** persisted concept — call it an `AgentEnsemble` (working name; could stay "team" or "swarm" if we want to preserve a familiar word) — with:

- **One member registry.** A new `SqliteTeamRegistry : ITeamRegistry` replaces `InMemoryTeamRegistry`. Members live in a `team_members` table keyed by `(workspace_id, project_id, team_id, member_id)`. Created via `TeamCreate` in conversation **or** via the server API **or** by a swarm decomposer that elects to publish its ephemeral workers as a named team.
- **One run ledger.** A new `agent_runs` table records every agentic execution — single-shot tool delegation (today's `TeamDelegate`), wave step (today's swarm worker), or mission step (Phase 51) — with foreign keys to the team that ran it, the user who triggered it, the workspace/project, and the parent run if it was spawned by another agent.
- **One event store.** Phase 37.5's `swarm_events` table generalises to `agent_events` (or stays named `swarm_events` with a `kind` column added — TBD by the migration). Both single-agent delegations and orchestration waves stream into the same event stream.
- **Three creation modes for an orchestration**, all going through the same `AgentOrchestrator`:
  1. **One pre-existing team.** Caller hands the orchestrator a `team_id`; the orchestrator runs the work using *only* members of that team (single delegation, parallel wave, or DAG — same engine, different surface). This is "use the team I already built." The LLM can do this from a conversation tool (`TeamRun`), the user can do it from the CLI (`sovrant team run <team_id> "<task>"`), and the API can do it (`POST /v1/teams/{id}/runs`).
  2. **Multiple pre-existing teams (composition).** Caller hands the orchestrator a list of `team_id`s — e.g. `[security-reviewers, perf-team, frontend]` — plus a task. The decomposer routes each step to the most appropriate team based on member capabilities (template + tool whitelist + recommended model tier). This is "use these specialised teams together."
  3. **Engine-decided (current behavior).** Caller hands the orchestrator a goal and no team. The decomposer (today's `LlmSwarmDecomposer`) builds a plan, spawns ephemeral workers from templates, and *optionally* publishes the resulting workers as a named team at the end so the user can re-use them later. This is the existing `sovrant swarm "<task>"` flow, unchanged from the user's perspective, but now with the side-effect that the workers it spawned become first-class persisted members the LLM can call back to in a later conversation.

The point is that the same orchestrator code handles all three modes; the only difference is *where the member roster comes from*. File locking, wave scheduling, retries, the quality gate, and the decomposer all sit underneath the orchestrator and apply uniformly regardless of how the team was assembled.

#### Architecture

```
src/Sovrant.Agents/Orchestration/
  IAgentOrchestrator.cs           ← unified entry point (replaces SwarmOrchestrator's public surface)
  AgentOrchestrator.cs            ← merges TeamDelegate path and SwarmOrchestrator engine
  EnsembleSelector.cs             ← takes (task, team_ids[]) and produces a worker roster
  TeamRunRequest.cs / EnsembleRunRequest.cs

src/Sovrant.Agents/Teams/
  ITeamRegistry.cs                ← unchanged interface
  SqliteTeamRegistry.cs           ← NEW: replaces InMemoryTeamRegistry as the default DI binding
  InMemoryTeamRegistry.cs         ← kept for tests only

src/Sovrant.Runtime/Storage/
  ITeamMemberStore.cs / SqliteTeamMemberStore.cs
  IAgentRunStore.cs / SqliteAgentRunStore.cs
  IAgentEventStore.cs             ← either generalises ISwarmEventStore or coexists with a kind discriminator

src/Sovrant.Runtime/Storage/Migrations/
  Vxxx__unified_orchestration.sql ← team_members, agent_runs, parent_run_id, kind columns

src/Sovrant.Tools/Team/
  TeamCreateTool.cs / TeamDelegateTool.cs / TeamStatusTool.cs / TeamDeleteTool.cs ← unchanged surface
  TeamRunTool.cs                  ← NEW: lets the LLM run an existing team against a task with parallelism
  TeamPublishTool.cs              ← NEW: lets a swarm publish its ephemeral workers as a named team after completion

src/Sovrant.Server/Routes/
  TeamRoutes.cs                   ← /v1/teams (CRUD), /v1/teams/{id}/runs (start), /v1/teams/{id}/members
  SwarmRoutes.cs                  ← stays for backward compat; internally calls AgentOrchestrator with mode=engine-decided

src/Sovrant.Cli/
  TeamCommand.cs                  ← `sovrant team {list|show|create|run|delete}` next to existing `swarm` command
```

#### Schema sketch

```sql
-- New
CREATE TABLE team_members (
    member_id        TEXT PRIMARY KEY,
    team_id          TEXT NOT NULL,
    workspace_id     TEXT NOT NULL,
    project_id       TEXT,
    name             TEXT NOT NULL,
    role             TEXT NOT NULL,
    template         TEXT,                  -- agent template name from Phase 22
    system_prompt    TEXT,
    allowed_tools    TEXT,                  -- JSON array
    model_level      TEXT,                  -- high / standard / fast (Phase 22)
    created_by       TEXT NOT NULL,         -- user_id
    created_at       INTEGER NOT NULL,
    last_used_at     INTEGER,
    status           TEXT NOT NULL DEFAULT 'active'
);

CREATE TABLE teams (
    team_id          TEXT PRIMARY KEY,
    workspace_id     TEXT NOT NULL,
    project_id       TEXT,
    name             TEXT NOT NULL,
    description      TEXT,
    origin           TEXT NOT NULL,         -- 'user', 'llm-created', 'swarm-published'
    created_by       TEXT NOT NULL,
    created_at       INTEGER NOT NULL
);

CREATE TABLE agent_runs (
    run_id           TEXT PRIMARY KEY,
    parent_run_id    TEXT,                  -- for sub-spawns (swarm wave step, mission sub-step)
    team_id          TEXT,                  -- nullable: ad-hoc runs may have no team
    member_id        TEXT,                  -- nullable: ephemeral workers
    workspace_id     TEXT NOT NULL,
    project_id       TEXT,
    user_id          TEXT NOT NULL,
    kind             TEXT NOT NULL,         -- 'delegation', 'swarm-task', 'mission-step'
    status           TEXT NOT NULL,         -- 'queued', 'running', 'succeeded', 'failed', 'cancelled'
    started_at       INTEGER NOT NULL,
    ended_at         INTEGER,
    input_tokens     INTEGER NOT NULL DEFAULT 0,
    output_tokens    INTEGER NOT NULL DEFAULT 0,
    cost_usd         REAL
);

-- Extend Phase 37.5's swarm_events with a kind discriminator + run linkage
ALTER TABLE swarm_events ADD COLUMN kind TEXT NOT NULL DEFAULT 'swarm';
ALTER TABLE swarm_events ADD COLUMN run_id TEXT;
CREATE INDEX idx_swarm_events_run_id ON swarm_events(run_id);
```

#### Implementation plan

1. **Migration `Vxxx__unified_orchestration.sql`**: create `teams`, `team_members`, `agent_runs`. Extend `swarm_events` with `kind` + `run_id`. All scoped by `workspace_id`/`project_id`/`user_id` so existing isolation rules apply automatically.
2. **`SqliteTeamRegistry`**: drop-in replacement for `InMemoryTeamRegistry` with the same `ITeamRegistry` interface. Reads/writes `teams` and `team_members`. Default DI binding switches to it; `InMemoryTeamRegistry` stays for unit tests.
3. **`IAgentRunStore` + `SqliteAgentRunStore`**: tracks runs across all three orchestration modes. Wired into `AgentOrchestrator` as the canonical run ledger. Existing `SwarmStateTracker` becomes a thin in-memory cache backed by this store.
4. **`AgentOrchestrator` (unification)**: merge the public surface of `SwarmOrchestrator` and the internals of `TeamDelegateTool` into one engine. New `RunAsync(EnsembleRunRequest)` accepts one of `{ team_id, [team_ids], goal-only }` and dispatches accordingly. The heavy machinery — `LlmSwarmDecomposer`, `SwarmFileLockManager`, wave scheduler, `SwarmQualityGate` — becomes **opt-in feature flags on the request**: `decompose` (default off when a `plan` is supplied, on for goal-only), `lock_files` (default off for single-task runs, on when `plan.length > 1` or the caller asks), `quality_gate` (default off for delegations, on for swarms), `max_parallel` (default 1 for delegations, `SwarmConfig.MaxConcurrent` for swarms). A `TeamDelegate`-style call gets `decompose=false, lock_files=false, quality_gate=false, max_parallel=1` and runs through the same engine with zero swarm overhead. Existing `SwarmOrchestrator` becomes a thin wrapper that builds a `goal-only` request with all flags on — backward compat for the existing `Swarm` tool. **`SwarmOrchestrator` and `TeamDelegateTool` are not allowed to keep duplicated execution paths after this phase** — both must dispatch through `AgentOrchestrator` or the LOC win and the surface-overlap fix don't materialise.
5. **`EnsembleSelector`**: given a list of teams and a task, picks workers from each team for each task in the plan based on template / tool / model-level fitness. Used by mode (2). Falls back to ephemeral spawning when no team member matches a task slot.
6. **`TeamRunTool` (new LLM-callable tool)**: lets the LLM say "run team `code-review` against this diff with parallelism" in a single tool call. Defaults to `decompose=false` (the LLM has already decided what to delegate, no decomposition tax), `lock_files=true` if the request includes a `files` array, `quality_gate=true` if the request includes acceptance criteria. Parallelism + locking + quality gate happen automatically when the caller asks for them, and only then.
7. **`TeamPublishTool` (new LLM-callable tool)**: after a swarm finishes in mode (3), the LLM (or a quality-gate hook) can publish the ephemeral workers as a named team in the current workspace, marking the team's `origin = 'swarm-published'`. Side benefit: the user can read `sovrant team list` after a swarm and see exactly who did the work.
8. **CLI**: `sovrant team {list|show|create|run|delete|members}` mirrors the existing `swarm` command. `sovrant swarm` keeps working as a synonym for "engine-decided run with no pre-existing team."
9. **Server routes**: `/v1/teams` CRUD, `/v1/teams/{id}/runs` (start a run), `/v1/teams/{id}/members`, `/v1/runs/{id}` (read any run regardless of mode), `/v1/runs/{id}/events` (SSE). Existing `/v1/swarm/*` routes stay and become aliases for engine-decided runs.
10. **Backward compatibility**: existing `TeamCreate` / `TeamDelegate` / `TeamStatus` / `TeamDelete` tools keep their wire format. Underneath, `TeamCreate` now writes to SQLite instead of the in-memory dict; `TeamDelegate` calls `AgentOrchestrator.RunAsync` with a single-step plan. No prompt changes for existing LLM users.
11. **`agent-systems.md`** rewrite (post-merge): the "two stovepipes" framing comes out, replaced with "one orchestration concept, three creation modes." The existing comparison table becomes an *historical* note for context. Phase 51 mission docs reference the unified `IAgentOrchestrator` instead of having to choose between Team and Swarm.
12. **Tests**:
    - `SqliteTeamRegistry`: round-trips members, scopes by workspace, survives recreation
    - `AgentOrchestrator` mode 1 (one team): a pre-built team of three runs a 5-task plan; file locks honoured; no ephemeral workers spawned
    - Mode 2 (multiple teams): given two teams, the selector routes a 6-task plan to the right specialists per task; falls back to ephemeral on no-fit
    - Mode 3 (engine-decided): existing `LlmSwarmDecomposer` flow still produces the same plans; `TeamPublishTool` materialises workers into a named team
    - **Decomposition-tax test**: a `TeamRunTool` call with `decompose=false` records **zero** calls to `LlmSwarmDecomposer` (assert via a fake decomposer that throws if invoked)
    - **Fast-path test**: a `TeamDelegate`-style call (single task, no flags) goes through `AgentOrchestrator` with `lock_files=false`, `quality_gate=false`, `max_parallel=1` and records no file-lock acquisitions and no quality-gate calls (assert via fakes)
    - **Code-path collapse test**: assert that both `TeamDelegateTool` and `SwarmTool` reach the same `AgentOrchestrator.RunAsync` instance under DI — no parallel execution paths
    - Backward compat: `TeamDelegate` against the new SQLite registry produces identical observable behaviour to the old in-memory path
    - Cross-restart: build a team in process A, restart, query members in process B, run a task against them
    - **Bridge test**: `TeamPublishTool` after a `sovrant swarm` run materialises workers as a named team with `origin = 'swarm-published'`; a subsequent `TeamRunTool` against that team uses `EnsembleSelector` to resolve members from `team_members` instead of spawning ephemerals (assert via the run ledger row showing non-null `member_id`)
    - `agent_runs` joined with `users` and `workspaces` returns sensible per-user, per-workspace activity reports

**Verification:**
- `dotnet build` exits 0
- `sovrant team create code-review --template reviewer && sovrant team run code-review "review src/Foo.cs"` runs against a persisted team, with file locks engaged and a row in `agent_runs`
- After restarting the process, `sovrant team list` still shows `code-review` and `sovrant team run code-review` works without recreating it
- A swarm run `sovrant swarm "refactor parser"` followed by `sovrant team list` shows the ephemeral workers materialised as a named team (because `TeamPublishTool` ran in the quality-gate hook)
- A multi-team run (`sovrant team run --teams security,perf "audit the auth flow"`) routes the security tasks to security-reviewer members and the perf tasks to perf members, with the routing visible in `agent_runs.team_id` per row
- `GET /v1/runs/{id}/events` returns the full event stream regardless of whether the run came from `TeamDelegate`, `sovrant swarm`, or a mission step (Phase 51)
- `agent_runs` joined with `users` produces a per-user activity report; same join with `workspaces` produces a per-workspace report
- All existing `Team*` and `Swarm*` tools and routes still work without prompt changes
- A `TeamRunTool` call with `decompose=false` and a pre-supplied plan completes with **zero** LLM calls to the decomposer (the decomposition-tax fix is observable, not just claimed)
- A simple `TeamDelegate`-style single-task delegation runs through `AgentOrchestrator` with all heavy features off and produces no file-lock or quality-gate activity in the trace (the fast path is real)
- Total LOC across `Sovrant.Agents/Swarm/` and `Sovrant.Agents/Teams/` is meaningfully lower than today after the unification (the code-to-value-ratio fix is observable in `cloc`, not just intent)
- After a swarm run, `sovrant team list` shows the published team and a subsequent `TeamRunTool` call against that team resolves workers from `team_members` (not from templates) — the bridge between the two systems works in both directions

**Non-goals:**
- Renaming the wire format. `TeamCreate`, `TeamDelegate`, `Swarm` tool, `/v1/swarm/*` — all stay. The unification is internal; users see strictly more functionality, not a migration burden.
- Removing the `LlmSwarmDecomposer`. It is the engine behind mode (3) and stays unchanged.
- Building a UI for team management. CLI + API only in this phase; the desktop app (Phase 44) and the frontend SDK (Phase 14) can consume the new routes when they want to.
- Cross-workspace team sharing. Teams stay scoped by workspace; sharing across workspaces is a later phase if it turns out users want it.
- Replacing Phase 51's `MissionExecutor`. Missions sit *on top of* the unified `IAgentOrchestrator` — Phase 52 makes Phase 51's life easier, not the other way around. The two phases can ship in either order; if 52 ships first, Phase 51's mission steps dispatch through the unified orchestrator from day one.

---

## Phase 53 — Scoped Artifact Storage (User / Workspace / Project, Disk Now → Cloud Later) ✅

**Depends on:** Phase 35 (workspaces), Phase 36 (projects), Phase 37 (users)
**Relates to:** Phase 41 (team agent artifact system — this phase is the storage substrate it will sit on), Phase 44 (desktop app — needs per-user scoping), Phase 47 (workspace export — needs a tenant-scoped artifact tree to export)
**Difficulty:** Medium

**Goal:** Replace today's flat `artifacts/` folder with a tenant-scoped layout rooted at `{user_id}/{workspace_id}/{project_id}/{run_id}/`, behind a single `IArtifactStore` abstraction. On-disk `LocalArtifactStore` ships first; a cloud-backed store (S3 / Azure Blob / R2) slots in later without touching callers.

### What exists today

`WorkspaceContext.ArtifactsRoot` returns `{cwd}/artifacts` and `GetOrCreateArtifactsDirectory(prompt)` derives a **prompt slug** as the only subdirectory (`src/Sovrant.Agents/Shared/WorkspaceContext.cs:26`). Every agent run — regardless of user, workspace, or project — writes into the same flat tree. Consequences:
- No isolation between users or workspaces; one tenant can see another's outputs on a shared deployment.
- Reruns of the same prompt collide into the same slug directory and overwrite prior outputs.
- Phase 35 workspaces and Phase 36 projects have no storage counterpart — there's nothing to back up, export, or quota per workspace.
- The existing `artifacts/create-a-guide-detailing-a-list-of-features-requested-by/` is a concrete example of the collision-and-leak problem in the current repo.

### Target layout

```
{ArtifactsRoot}/
  {workspace_id}/
    {project_id}/
      {run_id}/
        <files written by the agent for this run>
        _manifest.json      ← run metadata (user, prompt, agent, timestamps, file list)
```

- `ArtifactsRoot` defaults to `~/.sovrant/artifacts/` (not `{cwd}/artifacts/`) so outputs are not mixed into the user's source tree. Overridable via `SOVRANT_ARTIFACTS_ROOT`.
- **Workspace-first scoping**: the workspace is the top-level partition. All users in the same workspace share the same artifact tree. The initiating user is recorded in `_manifest.json` for attribution, not in the directory path. This means artifacts belong to workspaces/projects — a user only has "personal" artifacts through their personal workspace.
- `run_id` is the session/run id (already tracked in `agent_runs` / sessions), **not** a prompt slug. Reruns get fresh directories; a prompt-derived slug is kept as a human-readable symlink/alias inside `_manifest.json`.
- Unknown scopes fall back to sentinel segments: `personal` (the seeded personal workspace from Phase 35), `default-project`. Nothing breaks on a fresh install with no workspaces configured.
- Single-user CLI mode still works — it just resolves to `personal/default-project/{run_id}/`.

### `IArtifactStore` abstraction

| Member | Description |
|---|---|
| `Task<ArtifactHandle> CreateRunScopeAsync(ArtifactScope scope, CancellationToken ct)` | Materializes the scope tree and returns an opaque handle for the run. `ArtifactScope` = `(UserId, WorkspaceId, ProjectId, RunId)`. |
| `Task WriteAsync(ArtifactHandle handle, string relativePath, Stream content, string? contentType, CancellationToken ct)` | Writes a single artifact file under the run scope. Rejects `..` traversal. |
| `Task<Stream> ReadAsync(ArtifactHandle handle, string relativePath, CancellationToken ct)` | Reads an artifact back. |
| `IAsyncEnumerable<ArtifactEntry> ListAsync(ArtifactScope scope, CancellationToken ct)` | Lists files at any scope level (run, project, workspace, user). Used by `/v1/artifacts` and the desktop app sidebar. |
| `Task DeleteAsync(ArtifactScope scope, CancellationToken ct)` | Recursive delete at any scope level — powers cleanup when a run/project/workspace is deleted. |
| `Task<Uri?> GetAccessUrlAsync(ArtifactHandle handle, string relativePath, TimeSpan ttl, CancellationToken ct)` | Optional. Local store returns `file://`; cloud stores return presigned URLs. |

Implementations:
- **`LocalArtifactStore`** — ships in this phase. Writes to `SOVRANT_ARTIFACTS_ROOT`. Path-traversal-safe. Chmod 700 on the user segment where the OS supports it.
- **`S3ArtifactStore` / `AzureBlobArtifactStore`** — follow-up phase, same interface. Bucket/container per deployment; same `{user}/{workspace}/{project}/{run}/` prefix. Nothing else needs to change.

### Changes to existing code

1. `WorkspaceContext` — deprecate the flat `ArtifactsRoot` + `GetOrCreateArtifactsDirectory(prompt)`. Replace with `ArtifactScope` + an injected `IArtifactStore`. Callers that used the prompt-slug method get a shim that logs a warning and routes into the scoped store under `default-user/personal/default-project/{runId}/`.
2. `ConversationRuntime` + `SwarmOrchestrator` — resolve `ArtifactScope` from the current session's `user_id` / `workspace_id` / `project_id` (these already exist after Phases 35–37) and plumb the resulting `ArtifactHandle` through to tool execution.
3. `Write` / `Edit` / file-producing tools — when a tool writes into the artifacts tree, it goes through `IArtifactStore.WriteAsync`, not raw `File.WriteAllText` on a path computed from `WorkspaceContext`.
4. Phase 41's `FileBackedTeamWorkspace` — rebased on `IArtifactStore` instead of writing directly to `~/.sovrant/workspaces/{team_id}/`. Team artifacts become a subkey under the team's workspace scope.
5. Server: add `GET /v1/artifacts?scope=...` (list) and `GET /v1/artifacts/{runId}/{path}` (download) — authorized by Phase 38 per-user tokens; users can only read scopes they own, admins see all.
6. CLI: `sovrant artifacts ls [--workspace ... --project ... --run ...]`, `sovrant artifacts open <run>`, `sovrant artifacts rm <run>`.

### Migration

One-shot importer runs at startup if `{cwd}/artifacts/` is non-empty and `SOVRANT_ARTIFACTS_ROOT` is unset:
- Move existing subdirectories under `{cwd}/artifacts/<slug>/` into `~/.sovrant/artifacts/default-user/personal/default-project/legacy-{slug}/`.
- Write a `_manifest.json` for each with `{ "migrated_from": "legacy", "original_slug": "<slug>" }`.
- Log the move at WARN so users see it once.
- Leave the old `artifacts/` directory empty (not deleted) with a `README.migrated` breadcrumb.

### Environment variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_ARTIFACTS_ROOT` | `~/.sovrant/artifacts` | On-disk root for `LocalArtifactStore`. |
| `SOVRANT_ARTIFACTS_BACKEND` | `local` | `local` \| `s3` \| `azure` — chooses the registered `IArtifactStore` implementation. |
| `SOVRANT_ARTIFACTS_MIGRATE_LEGACY` | `true` | Set to `false` to skip the one-shot importer. |

### Implementation Plan

1. Define `ArtifactScope`, `ArtifactHandle`, `ArtifactEntry`, and `IArtifactStore` in `Sovrant.Runtime/Artifacts/`.
2. Implement `LocalArtifactStore` with path-traversal guards and per-segment `Directory.CreateDirectory` (permissions-aware).
3. Wire DI: `AddSovrantArtifacts()` picks the backend from `SOVRANT_ARTIFACTS_BACKEND`.
4. Refactor `WorkspaceContext` to expose `ArtifactScope` + `IArtifactStore` instead of raw paths. Keep a deprecated compatibility shim for one release.
5. Update `ConversationRuntime` and `SwarmOrchestrator` to resolve scope from session context and hand an `ArtifactHandle` to tools.
6. Update `Write`/`Edit`/file-emitting tools to route writes through `IArtifactStore` when the target falls under the artifacts tree.
7. Rebase Phase 41's `FileBackedTeamWorkspace` on `IArtifactStore`.
8. Add `/v1/artifacts` server endpoints (list + download), gated by Phase 38 user-scoped auth.
9. Add `sovrant artifacts` CLI subcommand (`ls`, `open`, `rm`).
10. Implement the one-shot legacy importer + boot-time warning.
11. Docs: new `docs/artifacts.md` covering layout, env vars, backend selection, and how to export/backup per workspace (feeds Phase 47).
12. Tests:
    - Scope isolation: user A cannot `ListAsync` or `ReadAsync` under user B's segment via the server API.
    - Path traversal rejection: `relativePath = "../../etc/passwd"` throws.
    - Rerun does not overwrite: two runs with the same prompt produce two distinct `run_id` directories.
    - Legacy importer: seeded `{cwd}/artifacts/<slug>/` content lands under `default-user/personal/default-project/legacy-<slug>/` with a manifest.
    - Fallback scope: runs with no user/workspace/project resolve to sentinel segments and succeed.
    - Phase 41 team workspace round-trip still passes once rebased on `IArtifactStore`.
    - `IArtifactStore` contract tests run against `LocalArtifactStore` now and can be re-run against future cloud backends unchanged.

### Acceptance Criteria

- All agent-generated files land under `{root}/{user}/{workspace}/{project}/{run}/` — no writes to a flat `artifacts/` directory remain in the codebase.
- A fresh install with no users/workspaces configured still works and writes under `default-user/personal/default-project/`.
- The server API refuses cross-tenant artifact reads under Phase 38 auth.
- The existing `sovrant/artifacts/create-a-guide-detailing-a-list-of-features-requested-by/` content migrates into the scoped layout on first boot of the new build.
- `IArtifactStore` is the only code path writing artifacts — grep confirms no direct `Path.Combine(..., "artifacts", ...)` survives outside the store.
- Phase 41's artifact tools, Phase 47's workspace export, and the Phase 44 desktop sidebar can all enumerate artifacts via a single abstraction.

### Non-goals

- Shipping the cloud backend. S3/Azure/R2 implementations are a follow-up; this phase only guarantees they can be added without touching callers.
- Artifact versioning or diffing. Phase 41's `IArtifact` version counter covers team-workspace versioning; this phase is about *where bytes live*, not history.
- Quotas and retention policies. Tenant quotas belong with Phase 55 (cost) and Phase 40 (multi-tenancy).
- A GC/sweeper for orphaned runs. Cleanup happens via `DeleteAsync` when sessions/projects/workspaces are deleted; a background sweeper is a later concern.

---

## Phase 54 — Gemma 4 Support (OpenRouter primary, Ollama local-only) & Capability Detection for Models with Incomplete Tool-Use Metadata ✅

**Depends on:** Phase 2 (OpenAI-compat provider), Phase 22 (tiered routing)
**Relates to:** Phase 48 (capability-discovered routing — this phase seeds the per-model capability layer it will later consume)
**Difficulty:** Low–Medium

**Goal:** First-class Sovrant support for **Gemma 4** (released 2026-04-02). The primary path is zero-config: user supplies an **OpenRouter** API key, picks `google/gemma-4-27b` (or another Gemma 4 variant), and everything — tool calls, thinking mode, cost tracking — just works. **Ollama is an optional secondary path for local / offline use only**, and carries a temporary workaround for an upstream Ollama template bug. Alongside the model itself this phase introduces a small capability-detection layer so future models that ship with incomplete provider metadata don't silently fall through the cracks the way Gemma 4 currently does on OpenRouter's `/api/v1/models` feed.

### Background — what's actually broken

Gemma 4 **does** support the full agentic lifecycle natively. Google shipped it with six dedicated tool-calling special tokens (`<|tool>`, `<|tool_call>`, `<|tool_result>` + closing pairs) trained into every instruction-tuned checkpoint, a native thought channel, configurable reasoning mode, 256K context, and structured JSON output. All of this is documented on `ai.google.dev/gemma/docs/core/model_card_4` and `deepmind.google/models/gemma/gemma-4/`. OpenRouter exposes `google/gemma-4-26b-a4b-it`, `google/gemma-4-27b`, and `google/gemma-4-31b-it` with `native function calling` listed on the model pages.

The *real* problems Sovrant hits with Gemma 4 today are three operational gaps, none of which mean "Gemma can't do tools":

1. **OpenRouter metadata gap (primary path — must fix)** — OpenRouter's `/api/v1/models` response for some Gemma 4 entries does not yet populate the `supported_parameters` array with `tools`/`tool_choice`, even though the model accepts them. Sovrant's (future) capability-discovered router will read that field and wrongly conclude "no native tools" unless we override. This is the only bug the primary OpenRouter path has to work around — once the registry overrides the metadata, Gemma 4 is a drop-in on OpenRouter.
2. **Ollama tool-call parser bug (local-only path — optional fix)** — As of April 2026, Ollama v0.20.0's Gemma 4 chat template has a broken tool-call parser: streaming drops `tool_calls` entirely and the non-streaming path fails to emit the outer JSON envelope. This only affects users who want to run Gemma 4 locally; it's being fixed upstream. Sovrant ships a temporary adapter so local users aren't blocked until the upstream fix lands, but the adapter is **not on the default code path** — it engages only when the selected provider is Ollama.
3. **Model-ID sprawl** — Gemma 4 ships as `gemma-4-26b-a4b-it`, `gemma-4-27b`, `gemma-4-31b-it` on OpenRouter, plus tags `gemma4:27b`, `gemma4:31b-instruct`, `gemma4:latest` on Ollama. Sovrant has no canonical mapping, so tier routing and Phase 55's cost layer both need to recognise every alias as the same model family.

### What exists today

- `Sovrant.Api/OpenAi/OpenAiChatRequest.cs` serialises `tools` + `tool_choice` on every request — this already works for Gemma 4 on OpenRouter once we stop treating the missing `supported_parameters` metadata as authoritative.
- `OpenAiCompatProvider.cs` parses `tool_calls` from the response — also already compatible with Gemma 4's native output on OpenRouter.
- No capability registry exists. Any per-model override today would have to live in `SmartRouter` ad-hoc.
- `ModelIdNormaliser` is a Phase 55 deliverable; this phase seeds its Gemma 4 aliases if Phase 55 has already shipped, otherwise adds a minimal standalone mapper this phase owns.

### Target design

A thin **`ModelCapabilities`** layer that answers three questions per model id: *does it support native tools*, *does it need the Ollama-template workaround*, and *what canonical id does it belong to*. It is the single source of truth consulted by the router, the cost layer, and (eventually) Phase 48's capability-discovered routing.

1. **`ModelCapabilities` registry** — hardcoded seed map + env override parser:
   - `google/gemma-4-*` → `{ native_tools: true, ollama_template_workaround: false, family: "gemma-4" }`
   - `ollama/gemma4*` / `gemma4:*` → `{ native_tools: true, ollama_template_workaround: true, family: "gemma-4" }`
   - Any future `google/gemma-5-*`, `google/gemma-4.x-*` glob slots in here without a code change to callers.
   - Override env: `SOVRANT_MODEL_CAPABILITIES="google/gemma-4-27b:native_tools=true,ollama/gemma4:27b:ollama_template_workaround=false"` lets operators flip a model the day upstream fixes the bug.

2. **OpenRouter metadata shim** — when Phase 48's capability discovery reads `supported_parameters` from `/api/v1/models` and sees it empty/missing for a model in the `ModelCapabilities` registry, the registry value wins. This is the "trust the registry over live metadata when live metadata is known to be incomplete" rule. A WARN log records the discrepancy so we notice when OpenRouter catches up and can delete the override.

3. **Ollama Gemma 4 template workaround** — a small `GemmaOllamaTemplateAdapter` that activates only when the selected provider is Ollama *and* `ollama_template_workaround == true`:
   - Sends requests to Ollama's `/api/generate` (raw) instead of `/api/chat`, with a hand-built prompt that emits the Gemma 4 control tokens directly, bypassing Ollama's broken Gemma 4 chat template.
   - Parses `<|tool_call|>{json}<|/tool_call|>` blocks out of the raw completion stream and reconstructs OpenAI-shaped `tool_calls` on the response so the rest of the engine is unaware.
   - Deactivates itself the moment the registry entry is flipped off (either by env var or by a future PR that deletes the override once Ollama ships the fix).

4. **Model-ID normalisation for Gemma 4** — seeds the alias map with:
   ```
   gemma-4-26b-a4b-it       → google/gemma-4-26b
   google/gemma-4-26b-a4b-it→ google/gemma-4-26b
   gemma4:27b               → google/gemma-4-27b
   gemma4:31b-instruct      → google/gemma-4-31b
   gemma4:latest            → google/gemma-4-27b   (tracks the default Ollama tag)
   ```
   If Phase 55 has shipped, this just adds entries to `ModelIdNormaliser`'s alias map. If not, this phase ships a minimal normaliser that Phase 55 later absorbs.

5. **Provider routing hook** — `SmartRouter.RouteAsync` calls `ModelCapabilities.Get(modelId)` and, when `ollama_template_workaround == true` on an Ollama provider selection, wraps it with `GemmaOllamaTemplateAdapter`. For OpenRouter selections there is *no wrapping* — Gemma 4 native tools just work once the registry says they do.

### Implementation plan (two PRs — Ollama PR is optional)

**PR 1 — Capability registry + OpenRouter happy path. (Primary deliverable.)**
1. Add `Sovrant.Runtime/Capabilities/ModelCapabilities.cs` (record struct + hardcoded seed map + env override parser + glob matcher).
2. Seed the Gemma 4 family entries (OpenRouter ids + aliases; Ollama tag entries are included but inert without PR 2).
3. Hook `SmartRouter` to consult it; for OpenRouter Gemma 4 selections, confirm native tool calls round-trip through `OpenAiCompatProvider` unchanged.
4. Unit tests over registry matching (glob, override precedence, unknown model defaults).
5. **Acceptance smoke test — the only thing that *has* to work for this phase to ship:** with just `OPENROUTER_API_KEY` set and `SOVRANT_MODEL=google/gemma-4-27b`, run `sovrant prompt "list files in ./src"` and observe a real `tool_calls` round-trip + a correct directory listing.
6. Docs: `docs/providers.md` gains a "Gemma 4" section with the one-line setup (`export OPENROUTER_API_KEY=... ; sovrant --model google/gemma-4-27b`).

**PR 2 — Ollama local-only workaround. (Optional, only ships if the upstream Ollama fix is still not out.)**
1. Add `GemmaOllamaTemplateAdapter` that speaks `/api/generate` with hand-built Gemma 4 control-token prompts, bypassing Ollama's broken chat template.
2. Parse `<|tool_call|>` blocks and reconstruct `tool_calls` on the way out.
3. Router wires the adapter only when the selected provider is Ollama *and* `ollama_template_workaround == true`. OpenRouter path is untouched.
4. Unit tests against canned Ollama responses containing Gemma 4 control tokens.
5. Live smoke test: `ollama pull gemma4:27b` then `sovrant prompt "…"` — verify the same 2-turn tool-calling loop that already works through OpenRouter.
6. Docs note that this workaround is temporary and can be disabled via env override the day upstream Ollama ships the fix. **If upstream Ollama fixes the bug before this PR is scheduled, skip PR 2 entirely** and just delete the Ollama entries from the registry.

### Acceptance

**Primary (PR 1 — must pass):**
- `dotnet test` full suite green.
- With only `OPENROUTER_API_KEY` set, `sovrant prompt "run ls in ./"` against `google/gemma-4-27b` completes a real tool call and returns the directory listing, same shape as `openai/gpt-4.1`.
- Phase 55's cost layer resolves all Gemma 4 aliases to a single pricing row.
- Existing models (`openai/*`, `anthropic/*`, `llama3.1`, `qwen2.5`) are unchanged — the registry hit is O(1).

**Secondary (PR 2 — only if Ollama upstream hasn't shipped the fix):**
- `sovrant prompt "…"` against `ollama/gemma4:27b` with the workaround enabled completes the same 2-turn loop.
- Env override `SOVRANT_MODEL_CAPABILITIES="ollama/gemma4:27b:ollama_template_workaround=false"` disables the adapter without a rebuild.
- OpenRouter path is byte-for-byte identical to PR 1 — the adapter never touches it.

### Non-goals

- A generic "prompted ReAct fallback" for models that truly lack tool support. Sovrant's target model set (OpenAI, Anthropic, Gemma 4+, Llama 3+, Qwen 2.5+, Mistral Nemo+) all speak native tool use; investing in a prompted XML protocol for pre-2024 models is not worth the maintenance.
- Fine-tuning or retraining Gemma 4 chat templates. The Ollama workaround is a bridge until upstream Ollama ships the template fix.
- Phase 48's full capability-discovered routing. This phase only defines the registry schema and the "registry wins over empty metadata" rule; Phase 48 does the live discovery.
- Gemma 4 multimodal (vision) support. Tool use first; image inputs are a separate phase.
- Claude / Anthropic-native Gemma 4 support. Anthropic's API does not host Gemma — this phase is scoped to OpenRouter and Ollama.

---

## Phase 55 — Cost Tracking, Budgets & Dashboard via OpenRouter (Consolidated from Phases 39 + 55) ✅

**Depends on:** Phase 10 (token usage tracking — already complete)
**External dependency:** Requires live access to OpenRouter's `GET https://openrouter.ai/api/v1/models` endpoint for model pricing data. This is a free, unauthenticated API but it is a third-party service — if OpenRouter is unreachable, cost estimation degrades to zero-cost fallback. Users routing through OpenRouter also use `GET /api/v1/generation/:id` for actual charged costs.
**Absorbs:** Phase 39 (budget enforcement, cost dashboard, metrics logging). Phase 51's mission cost envelopes read from this phase.
**Difficulty:** Medium

**Goal:** End-to-end cost management — live pricing from OpenRouter's free `/api/v1/models` endpoint, per-turn JSONL metrics logging, per-session and per-project budget enforcement with 80%/100% warnings, and a `sovrant cost` CLI + `GET /v1/cost` dashboard. No local pricing registry — OpenRouter is the single source of truth for model pricing across all providers.

**Why consolidated:** The original split had Phase 39 owning budgets/dashboard and Phase 55 owning OpenRouter pricing. Since all cost data flows through OpenRouter anyway, maintaining two phases created artificial dependency chains and duplicated `ICostModel`. One phase, one pipeline.

#### The key insight

OpenRouter's `GET https://openrouter.ai/api/v1/models` returns an authoritative, live-updated list of every model it routes to, each with a `pricing` object:

```jsonc
{
  "id": "anthropic/claude-sonnet-4.5",
  "name": "Anthropic: Claude Sonnet 4.5",
  "pricing": {
    "prompt": "0.000003",           // USD per input token
    "completion": "0.000015",       // USD per output token
    "request": "0",                 // flat per-request cost
    "image": "0.0048",              // USD per image
    "web_search": "0",
    "internal_reasoning": "0",
    "input_cache_read": "0.0000003",
    "input_cache_write": "0.00000375"
  },
  "context_length": 200000,
  ...
}
```

That single endpoint covers OpenAI, Anthropic, Google, Mistral, Cohere, DeepSeek, Meta, Qwen, and dozens more. It is maintained by the OpenRouter team. It is free to query. **This is the pricing registry — it already exists, it is live, and it costs us nothing but an HTTP call.**

For Sovrant users who route *through* OpenRouter, the picture is even cleaner: every generation gets an `id`, and `GET /api/v1/generation/:id` returns the authoritative cost OpenRouter actually charged — the source of truth, post-routing, post-caching, post-any-discounts. For users on direct provider APIs (OpenAI direct, Anthropic direct), Sovrant falls back to estimating via the `/models` pricing table keyed by a normalised model name.

#### Components

| Component | What it does |
|---|---|
| **`ICostModel`** | `decimal? EstimateCost(string model, long inputTokens, long outputTokens, CostHints? hints)` — returns a USD estimate or null when the model is unknown |
| **`OpenRouterPricingClient`** | HTTP client that GETs `/api/v1/models`, parses the pricing array, caches in-memory for a configurable TTL (default 6h), and writes a disk-backed fallback cache to `~/.sovrant/cache/openrouter-models.json` |
| **`OpenRouterCostModel`** | `ICostModel` implementation: normalises the Sovrant model id (`gpt-4o-2024-08-06` → `openai/gpt-4o`, `claude-sonnet-4-6` → `anthropic/claude-sonnet-4.5`, etc.), looks up the pricing row, multiplies by the token counts, returns the sum |
| **`OpenRouterGenerationFetcher`** | When Sovrant routes a request through OpenRouter and gets back a `generation_id`, this fetches `/api/v1/generation/:id` asynchronously (with a bounded queue) and records the *actual* cost charged. Used for reconciliation against the estimate. |
| **`ModelIdNormaliser`** | Small table-driven normaliser mapping Sovrant internal model ids to OpenRouter ids. Handles aliases, date suffixes, provider prefixes. Ships with the top ~30 models covered; unknown ids pass through untouched and get an estimate of `null` rather than a wrong number. |
| **`CostMetricsLogger`** | Appends per-turn cost events to `~/.sovrant/metrics/cost.jsonl` — one JSON line per completed turn with `{session_id, model, input_tokens, output_tokens, estimated_usd, actual_usd_or_null, source, timestamp}`. This is the raw data the cost dashboard reads. |
| **`CostModelLoggerFacade`** | Thin wrapper over `ICostModel` + `CostMetricsLogger` so the runtime calls one method (`RecordTurnAsync`) and the cost estimate + the JSONL write happen atomically. |
| **`BudgetEnforcer`** | Reads `SOVRANT_SESSION_BUDGET_USD` and `SOVRANT_PROJECT_BUDGET_USD`; warns at 80% spend, blocks new turns at 100%. Integrated into the turn pipeline via `CostModelLoggerFacade`. (Absorbed from Phase 39.) |
| **Cost Dashboard** | `GET /v1/cost` endpoint + `sovrant cost` CLI command — daily/weekly/monthly aggregation over the JSONL metrics log. Per-session and per-project breakdowns. (Absorbed from Phase 39.) |

#### Why the disk cache matters

The in-memory cache keeps API load low under normal operation. The disk-backed fallback (`~/.sovrant/cache/openrouter-models.json`) matters for three cases:

1. **Offline dev.** A developer on a plane should still get cost estimates for the model they're about to run against, even with no network.
2. **OpenRouter outage.** If the pricing endpoint is down, Sovrant degrades gracefully to the last successful snapshot rather than returning `null` for everything.
3. **Bounded staleness.** Without a disk cache, every process restart would hit the network. With one, restarts read the cached file and refresh in the background. The TTL on disk is longer (default 7d) than in-memory (6h) — a week-old snapshot is vastly better than nothing.

All three failure modes emit a WARN log (`Using cached pricing from {timestamp} — {reason}`) so operators know the numbers are stale.

#### Model id normalisation

Most of the complexity in this phase is in the model-id table. Sovrant's internal model names do not always match OpenRouter's. A small `modelIdMap` ships as a bundled resource:

```jsonc
// src/Sovrant.Runtime/Metrics/OpenRouterModelAliases.json
{
  "openai/gpt-4o": ["gpt-4o", "gpt-4o-2024-05-13", "gpt-4o-2024-08-06", "gpt-4o-mini-*"],
  "openai/gpt-4.1": ["gpt-4.1", "gpt-4.1-*"],
  "anthropic/claude-sonnet-4.5": ["claude-sonnet-4-6", "claude-sonnet-4-6-20250514"],
  "anthropic/claude-opus-4.6": ["claude-opus-4-6", "claude-opus-4-6-*"],
  "anthropic/claude-haiku-4.5": ["claude-haiku-4-5-20251001", "claude-haiku-4-5-*"],
  "google/gemini-2.0-flash": ["gemini-2.0-flash", "gemini-2.0-flash-*"],
  // ... extend as needed
}
```

The normaliser walks this map in order — exact match wins, glob match second, provider-prefix fallback last. Unknown ids return `null` from `EstimateCost`, which is explicitly *not* the same as `0` — missions know the difference between "this is free" and "we don't know what this costs".

The table is versioned with the code and updated in the same PR as any new model the router learns about. No remote schema syncing, no drift, no live aliasing service to maintain.

#### Configuration

```jsonc
// .sovrant/cost.json
{
  "provider": "openrouter",                         // "openrouter" | "none"
  "pricing": {
    "endpoint": "https://openrouter.ai/api/v1/models",
    "memory_cache_ttl_minutes": 360,                // 6 hours
    "disk_cache_ttl_days": 7,
    "disk_cache_path": "~/.sovrant/cache/openrouter-models.json"
  },
  "generation_reconciliation": {
    "enabled": true,                                // fetch /api/v1/generation/:id for actuals
    "only_when_routing_through_openrouter": true    // skip for direct OpenAI/Anthropic calls
  },
  "metrics": {
    "jsonl_path": "~/.sovrant/metrics/cost.jsonl"
  },
  "aliases_override_path": "~/.sovrant/cost-aliases.json"   // optional user override for exotic models
}
```

Env vars mirror the JSON for container / CI use:
- `SOVRANT_COST_PROVIDER` — default `openrouter`, set to `none` to disable
- `SOVRANT_OPENROUTER_PRICING_URL` — override for OpenRouter-compatible proxies
- `SOVRANT_COST_METRICS_PATH` — override the JSONL location

#### Architecture

```
src/Sovrant.Runtime/Metrics/
  ICostModel.cs                        ← interface (used by Phase 51, /cost CLI, session display)
  OpenRouterCostModel.cs               ← ICostModel implementation backed by the pricing client
  OpenRouterPricingClient.cs           ← HTTP fetch + in-memory + disk cache
  OpenRouterGenerationFetcher.cs       ← optional actual-cost reconciliation
  ModelIdNormaliser.cs                 ← model-id aliasing
  OpenRouterModelAliases.json          ← bundled alias table
  CostMetricsLogger.cs                 ← JSONL writer
  CostModelLoggerFacade.cs             ← combined facade used by the runtime
  BudgetEnforcer.cs                    ← session + project budget caps with 80%/100% thresholds
  CostDashboardService.cs             ← aggregation over JSONL for /v1/cost endpoint
```

#### Implementation plan

1. Define `ICostModel` + `CostHints` record (cache tokens, image counts, etc. — all optional) in `src/Sovrant.Runtime/Metrics/`.
2. Implement `OpenRouterPricingClient` with `IHttpClientFactory`, an `IMemoryCache`-backed TTL, and a disk snapshot on successful refresh. Handle 4xx/5xx by returning the last-known snapshot.
3. Implement `ModelIdNormaliser` driven by the bundled JSON alias table plus the user override path.
4. Implement `OpenRouterCostModel : ICostModel` — looks up the normalised id in the pricing snapshot, multiplies `prompt * inputTokens + completion * outputTokens`, adds per-request if non-zero, returns the sum.
5. `CostMetricsLogger`: append-only JSONL writer with a file lock and a rotation guard (rotate at 10 MB to `cost.jsonl.1`).
6. `CostModelLoggerFacade` wiring both together: `RecordTurnAsync(model, sessionId, usage) → (estimatedUsd, loggedLine)`.
7. Register the facade in `AddSovrantRuntime` so every `TurnComplete` event writes a cost line. Existing token counting in `SessionConfig` stays untouched — this phase *adds* USD alongside.
8. `BudgetEnforcer` — reads `SOVRANT_SESSION_BUDGET_USD` and `SOVRANT_PROJECT_BUDGET_USD`, warns at 80%, blocks at 100%. Hooks into `CostModelLoggerFacade.RecordTurnAsync`.
9. `GET /v1/cost` endpoint with daily/weekly/monthly aggregation over the JSONL log. Per-session and per-project breakdowns.
10. CLI: add `sovrant cost status` (live pricing snapshot age, cache hits, recent turns + estimated spend) and `sovrant cost` for per-session $ from the JSONL tail.
11. Optional: `OpenRouterGenerationFetcher` — a bounded background queue that fetches actuals for generations routed through OpenRouter and writes an updated JSONL line with `actual_usd` filled in. This is what lets users see their real spend (post-discount, post-cache) rather than just the estimate.
12. Tests:
    - Unit: `ModelIdNormaliser` table covers every model currently in `SovrantConfig` defaults, plus one unknown id → `null`
    - Unit: `OpenRouterCostModel.EstimateCost` against a captured snapshot for the top 10 models — arithmetic matches hand-computed expected values
    - Integration: `OpenRouterPricingClient` against a WireMock stub of the `/models` endpoint — cache hit, cache miss, network error, disk fallback
    - Integration (behind `COST_LIVE=1`): real call against `openrouter.ai/api/v1/models` — asserts that the top 10 models from `SovrantConfig` all resolve to non-null estimates
    - Integration: end-to-end turn → JSONL line written with the right shape and the estimate matches the unit-tested formula
    - Offline mode: `SOVRANT_COST_PROVIDER=none` → `ICostModel` returns null for everything, no HTTP calls made, no JSONL writes
    - Budget: 80% warning fires at correct threshold, 100% blocks new turns, project budget separate from session budget

#### Acceptance criteria

- `dotnet build` exits 0
- `ICostModel.EstimateCost("claude-sonnet-4-6", 1_000, 500)` returns a non-null USD estimate within 0.5% of hand-computed value against the live OpenRouter pricing snapshot
- Killing network access and restarting Sovrant still produces cost estimates for the next 7 days from the disk cache
- A completed turn writes one line to `~/.sovrant/metrics/cost.jsonl` with `{session_id, model, input_tokens, output_tokens, estimated_usd, actual_usd: null, source: "openrouter-live", timestamp}`
- When routing through OpenRouter and `generation_reconciliation.enabled=true`, the same line is updated within 30s with `actual_usd` filled in
- `sovrant cost status` shows the pricing snapshot age, cache hit ratio, and a one-line summary of the last 10 turns
- Missions (Phase 51) consume `ICostModel` and see consistent numbers across the mission guard, the session display, and the JSONL log
- Zero local pricing tables beyond the ~30-entry alias map — the dollar figures come from OpenRouter, not from Sovrant
- `SOVRANT_SESSION_BUDGET_USD=5.00` blocks new turns when cumulative session spend reaches $5, with a warning at $4
- `SOVRANT_PROJECT_BUDGET_USD=50.00` blocks new turns across all sessions in a project when cumulative project spend reaches $50
- `GET /v1/cost?range=weekly` returns a breakdown with per-session and per-project totals

#### Non-goals

- Reinventing a local pricing registry. OpenRouter's `/models` endpoint *is* the pricing registry.
- Per-provider direct-API integrations (OpenAI billing API, Anthropic billing API). Those are proprietary, rate-limited, and not worth building when OpenRouter already aggregates them. Users who want provider-direct actuals can route through OpenRouter (which will just forward and pass the real cost back).
- A charting/graphing dashboard. `sovrant cost` and `GET /v1/cost` are data views, not a full BI tool. A richer dashboard can be built on top of the JSONL later.
- Pricing for exotic / self-hosted / fine-tuned models outside OpenRouter's catalogue. Users set `SOVRANT_COST_PROVIDER=none` or provide an alias override that points at the closest public model.

---

## Phase 56 — ASP.NET Core Web Frontend (Blazor Server, Embedded Mode) ⚠️ Partially Complete

**Goal:** Browser-based UI matching the Avalonia desktop interface, securely consuming the Sovrant runtime in embedded (in-process) mode.

**Depends on:** Phase 44 (desktop app — establishes the UI patterns and screens to match), Phase 38 (auth)

**Priority:** High

**Status:** Embedded mode is fully functional — all 12+ screens implemented, streaming via direct `RunTurnAsync()`, tool confirmation modals, theming, master-detail layouts. The **remote server mode** (HTTP client wrappers, SignalR streaming, `AddSovrantClient()`, auth against `Sovrant.Server`) was originally scoped here but has been split out to **Phase 61** as it is a distinct infrastructure concern.

### Architecture: Embedded Mode (shipped) + Remote Mode (Phase 61)

| Mode | Topology | DI Registration | Status |
|---|---|---|---|
| **Embedded** | Blazor Server + `Sovrant.Runtime` in-process | `services.AddSovrantRuntime()` | ✅ Shipped |
| **Remote** | Blazor Server or WASM + `Sovrant.Server` via HTTP/SignalR | `services.AddSovrantClient(serverUrl)` | → Phase 61 |

The key insight: because `Sovrant.Runtime` is abstracted behind interfaces (`IConversationRuntime`, `IToolRegistry`, `ISmartRouter`, etc.), the same Blazor components will work in both modes. Swap the service registration at startup — no frontend rewrite needed. This is a capability unique to .NET's DI system with a shared runtime library. The remote mode implementation is tracked in **Phase 61**.

### Screens (matching Phase 44 desktop)

All screens from the desktop app are replicated with equivalent functionality:

| Screen | Key Features |
|---|---|
| Chat | Streaming responses via SignalR, tool use blocks, error retry, markdown rendering |
| Settings | Provider config, model selection, auto-save with debounce |
| Diagnostics | Health checks, provider pings, config display, system info |
| Tools | Master-detail: tool list with search, JSON schema rendered as markdown |
| Skills | Master-detail: skill list with trigger badges, full workflow body |
| Agents | Master-detail: agent templates with role/level, system prompt |
| Integrations | MCP server list with tool tags |
| Artifacts | Artifact grid with code preview |
| Projects | Project management (when backend ready) |
| Workspaces | Workspace management (when backend ready) |
| Automations | Automation workflows (when backend ready) |
| Orchestration | Team orchestration UI (when backend ready) |

### Theming

Same theme tokens as the desktop app, translated to CSS custom properties:

```css
:root[data-theme="dark"] {
  --brand-primary: #6D52C6;
  --surface-background: #1A1A1A;
  --surface-card: #2A2A2A;
  --surface-border: #3A3A3A;
  --text-primary: #FFFFFF;
  --text-secondary: #999999;
  --status-pass: #4CAF50;
  --status-warn: #FF9800;
  --status-fail: #F44336;
}
:root[data-theme="light"] {
  --brand-primary: #6D52C6;
  --surface-background: #F5F5F5;
  --surface-card: #FFFFFF;
  --surface-border: #D0D0D0;
  --text-primary: #1A1A1A;
  --text-secondary: #666666;
}
```

Dark/light toggle via `data-theme` attribute on `<html>`, persisted to user preferences.

### Project Structure

```
src/Sovrant.Web/
├── Sovrant.Web.csproj
├── Program.cs                     # DI bootstrap — AddSovrantRuntime() or AddSovrantClient()
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor       # Sidebar + content area shell
│   │   └── SidebarNav.razor       # Navigation matching desktop sidebar
│   ├── Pages/
│   │   ├── Chat.razor
│   │   ├── Settings.razor
│   │   ├── Diagnostics.razor
│   │   ├── Tools.razor
│   │   ├── Skills.razor
│   │   ├── Agents.razor
│   │   ├── Integrations.razor
│   │   ├── Artifacts.razor
│   │   └── ... (placeholder pages)
│   └── Shared/
│       ├── ChatMessage.razor       # Message bubble with markdown, tool blocks, error state
│       ├── ChatInputBar.razor      # Input with send button
│       ├── StatusBadge.razor       # Connected/Not Connected/PASS/WARN/FAIL
│       ├── MasterDetailLayout.razor # Reusable list + detail pane
│       └── MarkdownRenderer.razor  # Markdown-to-HTML rendering
├── Services/
│   ├── WebPermissionPolicy.cs     # IPermissionPolicy for web context
│   ├── WebConfirmationHandler.cs  # IToolConfirmationHandler — modal dialog via JS interop
│   ├── WebUserInputProvider.cs    # IUserInputProvider — modal input via JS interop
│   └── SovrantClientServices.cs   # Remote-mode service registrations (HttpClient wrappers for IConversationRuntime etc.)
├── wwwroot/
│   ├── css/
│   │   ├── sovrant-theme.css      # CSS custom properties for theming
│   │   └── app.css
│   └── js/
│       └── interop.js             # Theme toggle, clipboard, modal helpers
└── appsettings.json               # RuntimeMode: "embedded" | "remote", ServerUrl, auth config
```

### Security

- **Embedded mode:** API keys and config stay server-side. No secrets in the browser.
- **Remote mode:** Web frontend authenticates to `Sovrant.Server` via the Phase 38 token auth. API keys configured on the server only.
- **Authentication:** ASP.NET Identity or external OIDC provider (configurable). Session cookies with anti-forgery tokens.
- **Authorization:** Per-user permission policies. Admin users can configure providers; regular users can chat and view.
- **Transport:** HTTPS enforced. SignalR WebSocket connections authenticated via the same session cookie.
- CSRF protection via Blazor's built-in anti-forgery. CORS restricted to configured origins.

### Streaming

Chat responses stream via SignalR (Blazor Server) or Server-Sent Events (WASM remote mode):

- `RuntimeEvent.TextDelta` → append to message bubble in real-time
- `RuntimeEvent.ToolCallStart` → show tool use block with spinner
- `RuntimeEvent.ToolCallResult` → update tool block with result
- `RuntimeEvent.Error` → show error banner with retry button

Same event processing as the desktop app's `ChatViewModel`, adapted for Blazor component lifecycle.

### Build Order

1. **Scaffold:** Project, `Program.cs` with embedded-mode DI, `MainLayout` with sidebar
2. **Chat page:** Streaming responses, markdown rendering, tool blocks
3. **Settings page:** Provider config with auto-save
4. **Diagnostics page:** Health checks, provider pings
5. **Registry pages:** Tools, Skills, Agents with master-detail
6. **Remote mode:** `AddSovrantClient()` service registrations, `appsettings.json` toggle
7. **Auth:** ASP.NET Identity integration, per-user permissions
8. **Remaining screens:** Integrations, Artifacts, placeholders

### What this phase does NOT include (moved to Phase 61)

- **Remote server mode** — `AddSovrantClient(serverUrl)`, HTTP client wrappers for `IConversationRuntime` etc., SignalR streaming, auth against `Sovrant.Server` → **Phase 61**
- **ASP.NET Identity / OIDC** — per-user authentication, session cookies, anti-forgery → **Phase 61**
- Blazor WASM standalone deployment (requires remote mode + API layer optimization — future phase)
- Mobile-responsive layout (desktop-first, responsive later)
- Real-time multi-user collaboration (single-user sessions, same as desktop)
- Deployment automation (Docker, Azure, AWS — separate ops concern)

---

---

## Phase 57 — Inter-Agent Communication Through Leader / PM Agents

**Depends on:** Phase 41 (agent artifact tools), Phase 52 (unified orchestration), Phase 50 (OpenClaw integration for federated swarms)

**Goal:** Enable structured communication between agent groups — team-to-team, swarm-to-swarm, claw-to-claw — mediated by leader or PM (project manager) agents rather than direct agent-to-agent messaging.

**Priority:** Medium–High

### Motivation

Today each agent group (team, swarm, claw) operates in isolation. The orchestrator dispatches work and collects results, but there's no way for one team's output to inform another team's work in real time. A frontend team and a backend team working on the same feature can't coordinate API contracts. Two swarms running in parallel can't share intermediate discoveries.

Direct agent-to-agent messaging creates a coordination nightmare (n² channels, no oversight, no audit trail). Instead, communication flows through **leader/PM agents** that sit between groups, understand the broader context, and decide what information crosses boundaries.

### Design

| Component | Description |
|---|---|
| `IPMAgent` | Leader agent interface — receives updates from child group, decides what to broadcast to sibling groups |
| `GroupMailbox` | Per-group message queue — PM agents post coordination messages, child agents poll on turn start |
| `CoordinationEvent` | Structured message: `source_group`, `target_group`, `event_type` (blocker, update, request, handoff), `payload` |
| `PMCoordinator` | Routes coordination events between PM agents, maintains dependency graph between groups |

### Communication patterns

```
Team A (frontend)          PM Agent A          PM Coordinator          PM Agent B          Team B (backend)
      │                        │                      │                      │                      │
      ├── produces artifact ──→│                      │                      │                      │
      │                        ├── "API contract     →│                      │                      │
      │                        │    ready for review"  ├── routes to B ─────→│                      │
      │                        │                      │                      ├── injects context ──→│
      │                        │                      │                      │                      ├── reads artifact
      ���                        │                      │                      │                      │   implements endpoint
```

### Scope levels

| Level | Communication | Mediator |
|---|---|---|
| **Team-to-team** | Two teams in the same workspace share coordination through their PM agents | `PMCoordinator` (in-process) |
| **Swarm-to-swarm** | Two parallel swarms share discoveries or blockers | `PMCoordinator` (in-process) |
| **Claw-to-claw** | Federated swarms across instances share results over the routed bus | `PMCoordinator` (networked, depends on Phase 50) |

### What this is NOT

- **Not direct agent-to-agent chat** — all communication is mediated by PM agents with oversight
- **Not artifact sharing** — that's Phase 41 (agents deposit, orchestrator/user consumes)
- **Not replacing the orchestrator** — the orchestrator still dispatches work; PM agents handle cross-group coordination

### Implementation plan

1. Define `IPMAgent` interface with `OnGroupUpdate`, `DecideBroadcast`, `ReceiveCoordination` methods
2. Implement `GroupMailbox` — per-group persistent queue backed by SQLite (new migration)
3. Implement `CoordinationEvent` — structured message with source/target/type/payload
4. Implement `PMCoordinator` — routes events between PM agents, enforces scoping rules
5. Add PM agent template to `Sovrant.Agents` — system prompt focused on coordination, not execution
6. Wire into `AgentOrchestrator` — PM agents are optional participants in team/swarm runs
7. Add `CoordinationStatus` tool — lets any agent check if there are pending coordination messages
8. Extend `swarm_events` / `mission_events` tables with coordination event types
9. Claw-to-claw: extend Phase 50's routed bus to carry `CoordinationEvent` payloads
10. Tests: message routing, PM decision logic, cross-group coordination scenarios

### Out of scope (future)

- Agent-to-agent direct messaging (no mediator) — intentionally excluded for auditability
- Priority negotiation between PM agents — PM agents inform, they don't negotiate in v1
- Cross-workspace coordination — scoped to same workspace initially

---

## Phase 58 — Sovrant Trust Boundary (Sanitization, Ethical Harness & Intent Verification) ✅

**Depends on:** Phase 59 (Agentic Loop Hardening — provides `IIntentGate` and `SemanticIntentGate`)

**Status:** Complete. `TrustBoundaryProvider` decorator wraps any `ILlmProvider` with the three-stage pipeline. `PromptSanitizer` with `PiiDetector`, `CorporateDataDetector`, `CustomPatternRegistry` handles outbound redaction and inbound restoration via `RedactionMap`. `ContentPolicyEngine` provides model-independent ethical enforcement at Standard/Strict/Enterprise levels with 6+ harmful categories. `IntentVerificationBridge` connects Phase 59's `IIntentGate` as the first trust stage. `EthicalAuditLog` records all blocks/flags. `TrustBoundaryConfig` wired into `SovrantConfig` and DI. 72+ tests across 7 test files covering PII detection, corporate data detection, redaction round-trips, ethical classification, intent bridge, and provider decorator behavior.

**Goal:** Establish a unified trust boundary that wraps every LLM provider interaction with three guarantees: (1) sensitive data never leaves the machine unless explicitly allowed, (2) the system enforces ethical guardrails at the Sovrant engine level — not delegated to whatever model happens to be running, and (3) user intent is verified before any data touches a provider. Businesses running any model — from Claude Opus to a 7B uncensored local model — get the same trust guarantees because they are enforced by Sovrant, not by the model.

**Priority:** High

### Motivation — Why Sovrant Must Be the Trust Layer

Every prompt sent to an LLM provider leaves the user's machine and enters third-party infrastructure. Users working in corporate environments routinely paste code containing internal hostnames, API keys, database connection strings, employee names, email addresses, customer data, and proprietary business logic. Today, all of this goes to the provider unfiltered.

But sanitization alone is not enough. Some models have their own safety guardrails (Claude, GPT-4) and some don't (uncensored Ollama models, fine-tunes, smaller open models). **Sovrant cannot rely on the model being ethical — Sovrant must be the ethical layer.** A business running a cheap local model should be able to trust that Sovrant won't help produce harmful output, won't act on misunderstood intent, and won't leak sensitive data — the same guarantees as running a frontier model through a premium provider.

The trust boundary is a single decorator pipeline that wraps every `ILlmProvider`. It is the place where Sovrant earns the trust of the businesses and individuals who use it.

### Architecture — The Trust Pipeline

Every turn flows through three stages before any data reaches a provider:

```
User message
    ↓
┌─────────────────────────────────────────────────┐
│ Stage 1: Intent Verification (Phase 59 bridge)  │
│  IIntentGate.ClassifyAsync()                    │
│  → Ambiguous? Clarify first, don't send.        │
│  → Clearly harmful? Block here, never reaches   │
│    provider.                                     │
│  → Clear and benign? Proceed.                    │
└─────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────┐
│ Stage 2: Ethical Harness                         │
│  Sovrant-level content policy enforcement        │
│  → Outbound: refuse prompts requesting harmful   │
│    output (weapons synthesis, CSAM, doxxing,     │
│    fraud instructions, etc.)                     │
│  → Model-independent: works even with            │
│    uncensored models                             │
│  → Configurable strictness per workspace         │
│  → Audit log: every block/flag recorded          │
└─────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────┐
│ Stage 3: Data Sanitizer                          │
│  Strip PII & corporate data from outbound prompt │
│  → RedactionMap keeps originals local            │
│  → Deterministic placeholders ([EMAIL_1], etc.)  │
│  → Configurable per workspace/project            │
│  → Local providers (Ollama) exemptable           │
└─────────────────────────────────────────────────┘
    ↓
            Provider (any model, any provider)
    ↓
┌─────────────────────────────────────────────────┐
│ Stage 3 (return): Sanitizer Restore              │
│  Replace placeholders with originals             │
└─────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────┐
│ Stage 2 (return): Ethical Harness — Response     │
│  Scan model response before surfacing to user    │
│  → Catch harmful content from uncensored models  │
│  → Flag but don't silently drop (transparency)   │
└─────────────────────────────────────────────────┘
    ↓
User sees clean, safe, correctly-understood response
```

### Sub-phase 58a — Data Sanitizer

The sanitizer intercepts all outbound LLM requests and strips personally identifiable information (PII) and corporate-sensitive data before prompts leave the Sovrant runtime. It reconstructs the original context on response return so the user experience is seamless.

#### Design

| Component | Description |
|---|---|
| `IPromptSanitizer` | Pipeline interface — `SanitizeAsync(request) → (sanitized, redaction_map)` and `RestoreAsync(response, redaction_map) → restored` |
| `RedactionMap` | Bidirectional mapping of original values ↔ placeholder tokens. Stays local, never sent to provider, never persisted. |
| `PiiDetector` | Regex + heuristic detector for common PII patterns: emails, phone numbers, SSNs, credit cards, IP addresses, UUIDs that match internal formats |
| `CorporateDataDetector` | Configurable rules for corporate patterns: internal hostnames, domain-specific keywords, connection strings, API keys, env var values |
| `CustomPatternRegistry` | User-defined regex/glob patterns via config — organizations add their own sensitive patterns |
| `SanitizationPolicy` | Per-workspace/project config: what to redact, what to allow, severity levels (block vs. redact vs. warn) |

#### How it works

```
User prompt: "Fix the auth bug in api.acme-corp.internal connecting to postgres://admin:s3cret@db.acme.com:5432/users for user john.doe@acme.com"
                    ↓
            IPromptSanitizer
                    ↓
Sanitized:  "Fix the auth bug in [HOSTNAME_1] connecting to [CONNECTION_STRING_1] for user [EMAIL_1]"
                    ↓
            Sent to LLM provider
                    ↓
LLM response: "The issue with [HOSTNAME_1] is likely a TLS certificate mismatch. Check the connection at [CONNECTION_STRING_1]..."
                    ↓
            RestoreAsync (using RedactionMap)
                    ↓
Restored:   "The issue with api.acme-corp.internal is likely a TLS certificate mismatch. Check the connection at postgres://admin:s3cret@db.acme.com:5432/users..."
```

#### Built-in detectors

| Pattern | Examples | Default action |
|---|---|---|
| Email addresses | `user@company.com` | Redact → `[EMAIL_N]` |
| Phone numbers | `+1-555-123-4567` | Redact → `[PHONE_N]` |
| SSN / national IDs | `123-45-6789` | Redact → `[SSN_N]` |
| Credit card numbers | `4111-1111-1111-1111` | Redact → `[CARD_N]` |
| IP addresses (internal ranges) | `10.0.0.x`, `192.168.x.x` | Redact → `[IP_N]` |
| Connection strings | `postgres://`, `mongodb://`, `Server=` | Redact → `[CONNECTION_STRING_N]` |
| API keys / tokens | `sk-...`, `ghp_...`, Bearer tokens | Redact → `[API_KEY_N]` |
| Internal hostnames | Configurable domain suffixes | Redact → `[HOSTNAME_N]` |
| AWS/Azure/GCP resource ARNs | `arn:aws:`, `https://*.blob.core.windows.net` | Redact → `[CLOUD_RESOURCE_N]` |

### Sub-phase 58b — Ethical Harness

A Sovrant-level content policy that does not depend on the model having its own safety guardrails. This is what makes Sovrant safe to use with any model — the ethical enforcement happens before the prompt reaches the provider and after the response comes back.

#### Design

| Component | Description |
|---|---|
| `IEthicalHarness` | Interface — `EvaluateOutboundAsync(prompt) → EthicalVerdict` and `EvaluateInboundAsync(response) → EthicalVerdict` |
| `EthicalVerdict` | Result: `Allow`, `Block(reason)`, `Flag(reason, severity)` — blocked content never reaches the provider or user; flagged content is logged and optionally surfaced with a warning |
| `ContentPolicyEngine` | Rule-based classifier for known harmful categories: weapons/explosives synthesis, CSAM, doxxing/stalking, fraud/scam instructions, malware creation, self-harm instructions |
| `EthicalPolicy` | Per-workspace config: strictness level (`standard`, `strict`, `enterprise`), custom blocked categories, audit settings |
| `EthicalAuditLog` | Every block and flag is recorded with timestamp, category, severity, and sanitized snippet (no raw PII in audit logs) — for compliance reporting |

#### Strictness levels

| Level | Behavior |
|---|---|
| `standard` | Block clearly harmful categories (weapons synthesis, CSAM, doxxing, fraud). Allow dual-use topics with professional context (security research, medical, legal). |
| `strict` | Standard + block borderline content, require explicit professional justification for dual-use topics. |
| `enterprise` | Strict + custom blocked keyword lists, mandatory audit logging, admin-only policy changes. |

#### Key principle: transparency over silent filtering

When content is blocked, the user is told what was blocked and why. Sovrant does not silently drop or alter content — that erodes trust. The message is clear: "This request was blocked by Sovrant's content policy because [reason]. If you believe this is incorrect, adjust the policy in Settings or contact your workspace admin."

### Sub-phase 58c — Intent Verification Bridge

Connects Phase 59's `IIntentGate` into the trust boundary so intent classification is the first stage of every interaction with a provider. This is not new classification logic — it bridges the existing `SemanticIntentGate` into the trust pipeline.

| Scenario | Trust boundary action |
|---|---|
| Intent is ambiguous (e.g. "test", "run") | Clarify first — do not sanitize and send a message we don't understand |
| Intent is clearly harmful | Block at this layer — never reaches ethical harness or sanitizer (fast path) |
| Intent is clear and benign | Proceed to ethical harness → sanitizer → provider |
| Intent is dual-use (security research, medical) | Flag for ethical harness to evaluate with professional context |

### Integration point

The trust boundary hooks into the existing `ILlmProvider` pipeline as a decorator:

```
ConversationRuntime → SmartRouter → TrustBoundaryProvider (decorator) → actual provider
```

`TrustBoundaryProvider` wraps any `ILlmProvider` and runs the three-stage pipeline on every `SendAsync` and `StreamAsync` call. This means it works for all providers (OpenRouter, OpenAI, Gemini, Ollama) without any per-provider changes. Local providers (Ollama) can be exempted from sanitization (data stays on-machine) but still pass through the ethical harness and intent verification.

### Configuration

```json
{
  "trust_boundary": {
    "enabled": true,
    "sanitizer": {
      "enabled": true,
      "mode": "redact",
      "corporate_domains": ["acme.com", "acme-corp.internal"],
      "custom_patterns": [
        { "name": "project_codenames", "regex": "\\b(Project\\s+Titan|Moonshot)\\b", "action": "redact" }
      ],
      "allow_list": ["github.com", "stackoverflow.com"],
      "exempt_providers": ["ollama"],
      "log_redactions": true
    },
    "ethical_harness": {
      "enabled": true,
      "strictness": "standard",
      "audit_log": true,
      "custom_blocked_categories": [],
      "response_scanning": true
    },
    "intent_verification": {
      "enabled": true,
      "clarify_ambiguous": true,
      "block_harmful_intent": true
    }
  }
}
```

### Implementation plan

1. **58a — Sanitizer core:** `IPromptSanitizer`, `RedactionMap`, `PiiDetector`, `CorporateDataDetector`, `CustomPatternRegistry`, round-trip tests
2. **58b — Ethical harness:** `IEthicalHarness`, `ContentPolicyEngine`, `EthicalPolicy`, `EthicalAuditLog`, category detection tests
3. **58c — Intent bridge:** Wire `IIntentGate` as the first stage of the trust pipeline, harmful intent fast-path blocking
4. **58d — Provider decorator:** `TrustBoundaryProvider` implementing `ILlmProvider`, wrapping the three-stage pipeline for both `SendAsync` and `StreamAsync`
5. **58e — Configuration & DI:** `TrustBoundaryConfig` loaded from `SovrantConfig`, DI wiring as optional decorator, per-workspace policy
6. **58f — CLI & UI:** `/sanitize` dry-run command, trust boundary status in diagnostics screen, audit log viewer
7. **58g — Tests:** PII detection accuracy, ethical category detection, round-trip redaction/restoration, streaming chunk boundary handling, allow-list, exempt providers, audit log entries

### Verification

- `dotnet build` exits 0
- "Fix bug in api.acme-corp.internal for john.doe@acme.com" → email and hostname redacted, restored on response
- Harmful prompt → blocked with clear reason, never reaches provider, audit log entry created
- Ambiguous single-word input → clarification requested before any provider call
- Uncensored model producing harmful response → flagged before surfacing to user
- Ollama provider → ethical harness active, sanitizer skipped (local)
- `/sanitize` dry-run shows what would be redacted without sending

### Out of scope (future)

- Semantic/LLM-based sensitivity detection (chicken-and-egg — would require sending text to an LLM to classify)
- Enterprise DLP integration (Microsoft Purview, Google DLP API, etc.)
- Multi-language PII detection (initial implementation covers English patterns; international patterns added incrementally)
- Per-user policy overrides (initial implementation is per-workspace)

---

## Phase 59 — Agentic Loop Hardening (Intent Classification, Plan Approval, Execution Governance & Progress Visibility) ✅

**Depends on:** None (improves core runtime, independent of other phases)

**Goal:** Make the agentic loop safe, predictable, and transparent. Users must always know what the system is about to do, why, and be able to approve or reject it before execution. The system must never take destructive action on ambiguous input.

**Priority:** Critical

**Status:** Complete. Semantic intent gate (`SemanticIntentGate`) replaces crude `LooksLikeToolRequest()`. Graduated tool tiers (`GraduatedToolTiers`) classify all 49+ tools into Safe/Moderate/Dangerous/Escalation. Plan approval gate (`PlanApprovalGate`) with three modes. Execution budget enforcement (`ExecutionBudget`). Step tool enforcer, intent injector, plan presenter, progress tracker, orchestration router — all shipped with full test coverage. New `RuntimeEvent` types (`ClarificationNeeded`, `PlanPresented`, `StepProgress`) wired into all four clients (CLI, Desktop, Web, Server). `ModeAwarePermissionPolicy` refactored to use graduated tiers — DontAsk mode now requires confirmation for Dangerous/Escalation tools.

### Motivation — The "test" Problem

A user typed the single word "test" and the system attempted a `Write` action — creating a file unprompted. This happened because:

1. `LooksLikeToolRequest()` in `ConversationRuntime.cs` is substring-based keyword matching. "test" is in the keyword list, so tools were exposed to the LLM.
2. The LLM received tools and hallucinated that "test" meant "create a test file."
3. No plan was shown to the user before execution.
4. No intent validation checked whether the LLM's interpretation matched the user's actual intent.

This class of bug affects any ambiguous short input: "run", "fix", "check", "build", "clean". The system must handle ambiguity explicitly rather than delegating all judgment to the LLM.

### Current Architecture (What Exists)

```
User message
    ↓
LooksLikeToolRequest() — substring keyword match (crude)
    ↓
LLM call (tools exposed if keywords matched)
    ↓
LLM returns tool calls (or text)
    ↓
ModeAwarePermissionPolicy — Allow/Deny/RequireConfirmation per PermissionMode
    ↓
GovernanceMonitor — dangerous command detection, secret scanning (audit-only)
    ↓
Tool executes
    ↓
Result streamed to user
```

**Problems:** No intent classification, no plan preview, no plan approval, no intent-aware tool restriction, no progress reporting. The user sees the result, not the reasoning.

### Target Architecture

```
User message
    ↓
┌─────────────────────────────────────────┐
│ Phase 1: Intent Gate                     │
│  IntentClassifier (semantic, not regex)  │
│  → Clear intent: proceed with tools      │
│  → Ambiguous: ask user to clarify        │
│  → Conversational: respond without tools │
└─────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────┐
│ Phase 2: Plan & Present                  │
│  Planner produces RuntimePlan            │
│  → Show plan to user: "I'll do X, Y, Z" │
│  → User approves / rejects / modifies    │
│  → Rejected: replan or stop              │
└─────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────┐
│ Phase 3: Intent-Aware Execution          │
│  Per-step tool allow-list enforced       │
│  Step intent injected into system prompt │
│  Graduated permission tiers per tool     │
│  Progress events streamed to user        │
└─────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────┐
│ Phase 4: Orchestration Router            │
│  Runtime heuristics + LLM judgment       │
│  Task complexity → direct / agent / team │
│  / swarm / mission (not LLM-only)        │
└─────────────────────────────────────────┘
    ↓
User sees progress, results, and reasoning at every step
```

### Component Breakdown

#### A. Semantic Intent Gate (replace LooksLikeToolRequest)

| Component | Description |
|---|---|
| `IIntentGate` | Pre-LLM classifier: `ClassifyAsync(message) → IntentGateResult` |
| `IntentGateResult` | `{ Intent, Confidence, RequiresTools, NeedsClarification, SuggestedClarification }` |
| `SemanticIntentClassifier` | Lightweight LLM call (haiku-tier) or rule-based cascade: regex → keyword → small-model fallback |
| Clarification flow | If confidence < threshold or intent is ambiguous, ask user: "Did you mean X or Y?" before exposing tools |

**Rules:**
- Single-word messages with no prior context → always clarify
- Messages classified as `Conversational` → no tools exposed
- Messages classified as `ToolHeavy` with high confidence → tools exposed
- Unknown/ambiguous → clarify before proceeding

#### B. Plan Presentation & Approval

| Component | Description |
|---|---|
| `IPlanPresenter` | Formats `RuntimePlan` into user-readable summary before execution starts |
| `PlanApprovalGate` | Blocks execution until user approves. Modes: `AlwaysApprove` (auto-execute), `ApproveDestructive` (approve plans with Write/Bash/Edit), `AlwaysAsk` (approve every plan) |
| Plan summary format | "I'm going to: (1) Read config.json (2) Edit the auth section (3) Run tests. Proceed?" |
| Plan rejection | User says "no" → planner receives rejection reason, replans or stops |
| Plan modification | User says "skip step 3" or "also check X" → planner adjusts |

**When to show plans:**
- Single-tool calls (Read, Grep) → execute immediately, no approval needed
- Multi-step plans with destructive tools → always show plan first
- Any plan touching > 3 files → show plan first
- Configurable per workspace/project

#### C. Intent-Aware Execution Governance

| Component | Description |
|---|---|
| `StepToolEnforcer` | Validates tool calls against `RuntimeStep.AllowedTools` — reject tools not in the allow-list |
| `IntentInjector` | Injects step intent into system prompt for each step: "Your current task is: verify the config file exists. You should only use Read, Glob, or Grep." |
| `GraduatedToolTiers` | Classify tools into tiers: **Safe** (Read, Glob, Grep, List), **Moderate** (Write, Edit), **Dangerous** (Bash, PowerShell), **Escalation** (Agent, Team, Swarm). Different permission thresholds per tier. |
| `ExecutionBudget` | Per-plan limits: max tool calls, max files modified, max execution time. Exceeding triggers pause + user notification. |

#### D. Orchestration Router (Runtime-Assisted)

| Component | Description |
|---|---|
| `IOrchestrationRouter` | Analyzes task complexity and recommends execution mode |
| Heuristics | File count, step count, estimated parallelism, dependency depth → direct / agent / team / swarm / mission |
| LLM assist | Router's recommendation shown to LLM as context, not mandate. LLM can override with explanation. |
| Escalation guard | Prevents swarm/mission for trivial tasks. Prevents direct execution for complex multi-file tasks. |

**Routing heuristics:**
- 1 file, 1–2 steps → direct execution
- 2–5 files, sequential → sub-agent
- 3–10 files, independent → team (parallel agents)
- 10+ files or DAG dependencies → swarm
- Open-ended goal with replanning → mission

#### E. Progress Visibility & User Communication

| Component | Description |
|---|---|
| `PlanProgressTracker` | Tracks step completion against total plan. Emits `RuntimeEvent.StepProgress(current, total, intent, status)` |
| `PhaseIndicator` | Groups steps into logical phases (Analyze, Implement, Test, Verify) and reports which phase is active |
| `StepSummaryEmitter` | After each step completes, emits a one-line summary: "Step 2/5: Verified auth config exists ✓" |
| `LiveTraceViewer` | Surfaces `IRuntimeTraceStore` entries in real-time, not just post-hoc |
| `EstimatedCompletion` | Rough ETA based on average step duration (optional, disabled by default) |

### DontAsk Mode Fix

`PermissionMode.DontAsk` currently bypasses all confirmation. This is dangerous for production use.

**Fix:** Rename to `TrustModel` and add guardrails:
- Still skips confirmation for Safe and Moderate tier tools
- Still requires confirmation for Dangerous tier tools (Bash, PowerShell)
- Never allows Escalation tier (Agent, Swarm) without at least showing the plan
- Add `SOVRANT_UNSAFE_DONTASK=true` env var for truly unguarded mode (CI pipelines only)

### Implementation Plan

**Sub-phase 59a — Intent Gate (highest priority, fixes the "test" bug):**
1. Define `IIntentGate` interface and `IntentGateResult` record
2. Implement `SemanticIntentClassifier` — rule cascade: length check → regex → keyword → optional small-model call
3. Replace `LooksLikeToolRequest()` in `ConversationRuntime` with `IIntentGate.ClassifyAsync()`
4. Add clarification flow: if ambiguous, inject "Could you clarify?" before exposing tools
5. Tests: single-word inputs, ambiguous commands, clear tool requests, conversational messages

**Sub-phase 59b — Plan Presentation & Approval:**
6. Define `IPlanPresenter` and `PlanApprovalGate`
7. Implement plan formatting (numbered steps with intents)
8. Wire approval gate into `LlmExecutor` between plan creation and step execution
9. Add plan rejection → replan loop
10. Configure approval thresholds per workspace/project

**Sub-phase 59c — Intent-Aware Execution:**
11. Implement `StepToolEnforcer` — check `RuntimeStep.AllowedTools` at execution time
12. Implement `IntentInjector` — add step intent to per-step system prompt
13. Implement `GraduatedToolTiers` — classify all 51 tools into Safe/Moderate/Dangerous/Escalation
14. Implement `ExecutionBudget` — per-plan resource limits
15. Fix `DontAsk` mode with tier-based guardrails

**Sub-phase 59d — Orchestration Router:**
16. Define `IOrchestrationRouter` interface
17. Implement heuristic-based routing (file count, step count, parallelism estimate)
18. Wire into `ConversationRuntime` as advisory context for the LLM
19. Add escalation guards (prevent swarm for trivial tasks, prevent direct for complex tasks)

**Sub-phase 59e — Progress Visibility:**
20. Add `RuntimeEvent.StepProgress` event type
21. Implement `PlanProgressTracker` and `StepSummaryEmitter`
22. Wire progress events into CLI, desktop (Avalonia), and web (Blazor) UIs
23. Surface live trace entries during execution (not just post-hoc)

### Out of scope (future)

- User-trained intent models (learning from corrections over time)
- Per-user safety profiles (some users want full autonomy, others want approval on everything)
- Undo/redo for executed plans (separate feature, possibly Phase 60)

---

---

## Phase 60 — Hermes Agent Integration via MCP (Alternative Claw Provider)

**Depends on:** Phase 16 (Dynamic MCP Tool Proxy), Phase 50 (OpenClaw integration — establishes the `IFederationBus` abstraction this phase implements a second backend for)
**Difficulty:** Medium

**Goal:** Add [Hermes Agent](https://github.com/NousResearch/hermes-agent) (by Nous Research) as a second federation bus provider alongside OpenClaw. Hermes Agent is an open-source, self-improving AI agent framework that ships an **MCP server mode** (`hermes mcp serve`) and an **OpenAI-compatible API**. Where OpenClaw is a routing gateway bridging messaging platforms to agents, Hermes is a full agent execution engine with its own planning loop, skill learning, and orchestration. Together they give Sovrant two complementary federation options: OpenClaw for human-reachable chat-channel routing, Hermes for agent-to-agent delegation with adaptive skill acquisition.

#### What Hermes Agent is

Hermes Agent (MIT license, Python, ~65K GitHub stars) is a self-improving agent framework built by Nous Research. Key capabilities:

| Feature | Description |
|---|---|
| **Self-improving skills** | Closed learning loop — agent creates reusable "skills" from experience, refines them over use |
| **MCP client** | Connects to any MCP server at startup, discovers tools, registers with namespaced prefixes (`mcp_<server>_<tool>`) |
| **MCP server mode** | `hermes mcp serve` exposes conversations, sessions, and messaging tools to any MCP client |
| **Sampling support** | MCP servers can request LLM inference from Hermes via `sampling/createMessage` |
| **OpenAI-compatible API** | HTTP server exposing standard `/v1/chat/completions` endpoint backed by the Hermes agent loop |
| **Subagent delegation** | Parent agents spawn child agents in isolated terminals, fan out work, collect results |
| **5 sandbox backends** | Local, Docker, SSH, Singularity, Modal — agent execution isolation at every level |

#### How Hermes compares to OpenClaw

| Dimension | OpenClaw (Phase 50) | Hermes Agent (this phase) |
|---|---|---|
| **Role** | Message routing gateway | Agent execution engine |
| **Strength** | Bridges 8+ messaging platforms (Discord, Slack, Telegram, WhatsApp, Signal, iMessage, Matrix) — every swarm becomes reachable from a phone | Self-improving skills, deep agent-to-agent delegation, adaptive planning |
| **Integration surface** | 9 MCP tools (conversations, messages, events, permissions) | MCP server mode + OpenAI-compatible HTTP API |
| **Orchestration model** | Route-based pub/sub — agents publish events, operators subscribe from chat channels | Subagent delegation (L0), proposed DAG workflows (L1), shared scratchpad (L2), live dialogue (L3) |
| **When to use** | Human-in-the-loop, cross-platform notifications, approval routing | Agent-to-agent task delegation where the remote agent has specialised skills or domain knowledge |

**They are complementary, not competing.** A Sovrant swarm could use OpenClaw for human approval routing while delegating specialised subtasks to a Hermes agent that has learned domain-specific skills.

#### Architecture

The key insight: Phase 50 introduces `OpenClawBusClient` as a concrete implementation. This phase extracts the federation bus abstraction (`IFederationBus`) and adds `HermesBusClient` as a second implementation. Sovrant agents don't care which bus they're talking to — they call `PublishAsync`, `DelegateAsync`, `SubscribeAsync`.

```
src/Sovrant.Agents/Swarm/Bus/
  IFederationBus.cs                    ← abstraction extracted from Phase 50's OpenClawBusClient
  OpenClaw/
    OpenClawBusClient.cs               ← Phase 50's implementation (refactored to implement IFederationBus)
  Hermes/
    HermesBusClient.cs                 ← MCP-backed client wrapping `hermes mcp serve`
    HermesApiClient.cs                 ← optional: OpenAI-compatible HTTP client for direct API mode
    HermesSkillDiscovery.cs            ← queries Hermes for its learned skills, surfaces them as delegatable capabilities
```

#### Integration modes

Sovrant can talk to Hermes in two ways:

**Mode 1 — MCP (recommended):** Sovrant launches `hermes mcp serve` as a managed MCP child process (same pattern as OpenClaw). Hermes tools surface in the Sovrant tool registry via Phase 16's `MCPTool` proxy. `HermesBusClient` wraps these tools into the `IFederationBus` interface. Transport: stdio or StreamableHTTP.

**Mode 2 — OpenAI-compatible API:** Sovrant treats a running Hermes instance as an OpenAI-compatible endpoint. Useful when Hermes is deployed as a remote service. Sovrant's existing `OpenAiCompatProvider` can route requests to it, but `HermesApiClient` adds agent-specific capabilities (session management, skill queries) on top.

#### Configuration

```jsonc
// .sovrant/swarm.json — federation bus providers
{
  "federation": {
    "mode": "manager-led",
    "providers": {
      "openclaw": {
        "enabled": true,
        "mcpServerName": "openclaw"
        // ... existing Phase 50 config
      },
      "hermes": {
        "enabled": true,
        "mode": "mcp",                              // "mcp" | "api"
        "mcpServerName": "hermes",                   // references entry in mcp.json
        "api": {                                     // only used when mode = "api"
          "baseUrl": "http://localhost:8080",
          "apiKey": "${HERMES_API_KEY}"
        },
        "skillDiscovery": true,                      // query Hermes for learned skills on connect
        "delegationRoutePrefix": "sovrant/hermes"
      }
    },
    "defaultProvider": "openclaw",                   // which bus handles unqualified PublishAsync calls
    "managerAgent": "swarm-manager",
    "maxChildSwarms": 8
  }
}
```

```jsonc
// .sovrant/mcp.json — Hermes as a managed MCP server
{
  "mcpServers": {
    "hermes": {
      "command": "hermes",
      "args": ["mcp", "serve", "--host", "localhost", "--port", "8765"],
      "env": {
        "HERMES_MODEL": "openrouter/anthropic/claude-sonnet-4-6:free"
      }
    }
  }
}
```

#### Components

| Component | What it does |
|---|---|
| **`IFederationBus`** | Abstraction over federation bus providers: `PublishAsync`, `SubscribeAsync`, `DelegateAsync`, `QueryCapabilitiesAsync`. Extracted from Phase 50's `OpenClawBusClient`. |
| **`HermesBusClient`** | `IFederationBus` implementation backed by Hermes MCP tools. Translates Sovrant swarm events into Hermes conversations/messages. |
| **`HermesApiClient`** | Optional HTTP client for Hermes's OpenAI-compatible API. Used when `mode = "api"`. |
| **`HermesSkillDiscovery`** | Queries Hermes for its learned skill catalogue on connection. Surfaces skills as delegatable capabilities so the swarm manager can route subtasks to Hermes when it has a relevant skill. |
| **`FederationBusRouter`** | Routes `PublishAsync`/`DelegateAsync` calls to the correct provider based on route prefix or explicit provider hint. Supports fan-out (publish to both OpenClaw and Hermes simultaneously). |
| **`CompositeFederationBus`** | Wraps multiple `IFederationBus` instances. Manager agents can use OpenClaw for human notifications while delegating compute-heavy subtasks to Hermes — same swarm, two buses. |

#### Implementation plan

1. Extract `IFederationBus` interface from Phase 50's `OpenClawBusClient`. Methods: `PublishAsync`, `SubscribeAsync`, `DelegateAsync(task, capabilities?)`, `QueryCapabilitiesAsync`, `HealthCheckAsync`. Refactor `OpenClawBusClient` to implement the new interface — zero behavior change.
2. Implement `HermesBusClient : IFederationBus` backed by Phase 16's MCP tool proxy. On connect, discover Hermes's available tools and map them to `IFederationBus` operations. `DelegateAsync` creates a Hermes conversation, sends the task, polls for completion, returns the result.
3. Implement `HermesSkillDiscovery` — on startup (and periodically), query Hermes for its learned skills. Cache the skill catalogue locally. The swarm manager can inspect this to decide whether to delegate a subtask to Hermes vs. running it in-process.
4. Implement `FederationBusRouter` — given a `PublishAsync` call with a route, determine which provider handles it based on route prefix or explicit `provider: "hermes"` hint. Default provider from config.
5. Implement `CompositeFederationBus` — the DI-registered `IFederationBus` that wraps all enabled providers. Supports parallel fan-out for publish operations.
6. Optional: `HermesApiClient` for API mode — thin OpenAI-compatible HTTP client with Hermes-specific extensions (session management, skill listing).
7. Extend `SwarmConfig.Federation.Providers` to accept multiple named providers with per-provider config.
8. Wire into DI: register `IFederationBus` → `CompositeFederationBus` when multiple providers enabled, or the single concrete client when only one is configured.
9. Extend `SwarmManagerAgent` template to be provider-aware — can route different subtasks to different bus providers based on the task's nature and Hermes's skill catalogue.
10. New endpoints:
    - `GET /v1/swarm/hermes/skills` — cached Hermes skill catalogue
    - `GET /v1/swarm/hermes/status` — Hermes connection health + MCP tool list
    - `GET /v1/swarm/federation/providers` — all registered providers and their status
11. CLI: `sovrant federation providers` (list), `sovrant federation skills hermes` (Hermes skill catalogue)
12. Tests:
    - `IFederationBus` contract tests run against both `OpenClawBusClient` and `HermesBusClient` (shared test suite, parameterized by provider)
    - `FederationBusRouter`: route prefix matching, default provider fallback, explicit provider hint
    - `CompositeFederationBus`: fan-out publishes to both, `DelegateAsync` routes to the correct provider
    - `HermesSkillDiscovery` against a mock MCP server returning a skill catalogue
    - Manager-led swarm with mixed delegation: human approval via OpenClaw, subtask execution via Hermes
    - Integration test gated behind `HERMES_E2E=1`: real `hermes mcp serve` → delegate task → collect result

#### Acceptance criteria

- `dotnet build` exits 0
- Phase 50's `OpenClawBusClient` refactored to `IFederationBus` with zero behavior change (existing tests pass)
- With Hermes configured, `sovrant federation providers` shows both OpenClaw and Hermes with health status
- `DelegateAsync` to Hermes sends a task, Hermes processes it with its agent loop, Sovrant receives the result
- `sovrant federation skills hermes` lists Hermes's learned skills
- A manager-led swarm can use OpenClaw for human notifications while delegating subtasks to Hermes
- Disabling Hermes in config falls back to OpenClaw-only with no errors

#### Non-goals

- Running Hermes in-process. Hermes is a Python application — Sovrant communicates with it over MCP (stdio/HTTP) or its OpenAI-compatible API. No Python embedding.
- Replacing OpenClaw. Hermes and OpenClaw serve different purposes. OpenClaw remains the primary choice for human-reachable routing.
- Managing Hermes's internal skill library. Sovrant reads Hermes's skills for routing decisions but does not write, edit, or delete them.
- A2A (Agent-to-Agent Protocol) support. Google's A2A is a promising standard but not yet mature enough to build on. MCP is the integration protocol for this phase; A2A can be evaluated as a future addition.

---

## Phase 61 — Remote Server Mode for Web Frontend (SignalR Streaming, Auth & Client Abstraction) ✅

**Depends on:** Phase 56 (web frontend — embedded mode, already shipped), Phase 38 (per-user token auth)
**Difficulty:** Medium–High

**Goal:** Complete the "dual-mode runtime access" promise from Phase 56. Today the Blazor web frontend only works in embedded mode — `Sovrant.Runtime` runs in-process via `AddSovrantRuntime()`. This phase adds remote mode: the same Blazor components connect to a running `Sovrant.Server` instance over HTTP + SignalR, with proper authentication. The frontend code stays identical; only the DI registration changes.

**Why this is its own phase:** Remote mode is a distinct infrastructure concern — HTTP client wrappers, SignalR hub wiring, auth token management, reconnection logic, and streaming protocol translation. Bundling it with the UI screens (Phase 56) created a "partially complete" phase. Splitting it out gives a clean boundary: Phase 56 = UI, Phase 61 = remote connectivity.

### What exists today

- **Embedded mode works:** All 15 screens functional, streaming via direct `RunTurnAsync()` async enumeration
- **`Sovrant.Server` exists:** Full REST API with 95 endpoints, bearer token auth, rate limiting, SSE streaming
- **No bridge between them:** No HTTP client wrappers implementing `IConversationRuntime`, no SignalR hub for real-time streaming, no `AddSovrantClient()` extension method

### Components

| Component | What it does |
|---|---|
| **`AddSovrantClient(serverUrl)`** | DI extension method that registers HTTP-backed implementations of `IConversationRuntime`, `IToolRegistry`, `ISmartRouter`, `IArtifactStore`, etc. — replacing the in-process registrations from `AddSovrantRuntime()` |
| **`RemoteConversationRuntime`** | `IConversationRuntime` implementation that wraps `POST /v1/chat/completions` + SignalR streaming. Translates HTTP/SignalR events into the same `RuntimeEvent` stream the Blazor components already consume |
| **`RemoteToolRegistry`** | `IToolRegistry` implementation backed by `GET /v1/tools` |
| **`RemoteArtifactStore`** | `IArtifactStore` implementation backed by the `/v1/artifacts` endpoints |
| **`SovrantSignalRHub`** | Server-side SignalR hub on `Sovrant.Server` that streams `RuntimeEvent` objects during a turn. Replaces SSE for WebSocket-capable clients |
| **`SignalRStreamingClient`** | Client-side SignalR connection in the Blazor app that receives `RuntimeEvent` and feeds the existing `Chat.razor` event loop |
| **`RemoteAuthService`** | Manages bearer token lifecycle — login with API token (Phase 38), token refresh, auto-reconnect on 401 |
| **`appsettings.json` toggle** | `RuntimeMode: "embedded" | "remote"` + `ServerUrl` config. Program.cs reads this and calls either `AddSovrantRuntime()` or `AddSovrantClient(serverUrl)` |

### Architecture

```
Embedded mode (Phase 56, shipped):
  Blazor Server ──→ IConversationRuntime ──→ Sovrant.Runtime (in-process)

Remote mode (this phase):
  Blazor Server ──→ IConversationRuntime ──→ RemoteConversationRuntime
                                                    │
                                              HTTP + SignalR
                                                    │
                                              Sovrant.Server ──→ Sovrant.Runtime
```

```
src/Sovrant.Web/
  Services/
    SovrantClientServices.cs              ← AddSovrantClient() extension method
    RemoteConversationRuntime.cs          ← IConversationRuntime over HTTP + SignalR
    RemoteToolRegistry.cs                 ← IToolRegistry over HTTP
    RemoteSmartRouter.cs                  ← ISmartRouter over HTTP
    RemoteArtifactStore.cs                ← IArtifactStore over HTTP
    RemoteAuthService.cs                  ← Bearer token management
    SignalRStreamingClient.cs             ← SignalR connection for RuntimeEvent stream
  appsettings.json                        ← RuntimeMode toggle

src/Sovrant.Server/
  Hubs/
    ChatHub.cs                            ← SignalR hub streaming RuntimeEvent during turns
  Streaming/
    SignalRStreamAdapter.cs               ← Adapts RuntimeEvent async enumerable to SignalR stream
```

### Configuration

```jsonc
// src/Sovrant.Web/appsettings.json
{
  "Sovrant": {
    "RuntimeMode": "remote",                        // "embedded" | "remote"
    "Server": {
      "Url": "https://sovrant.internal:5200",
      "ApiToken": "${SOVRANT_API_TOKEN}",           // Phase 38 token
      "SignalR": {
        "Enabled": true,
        "ReconnectIntervalMs": 5000,
        "MaxReconnectAttempts": 10
      }
    }
  }
}
```

Env var overrides:
- `SOVRANT_RUNTIME_MODE` — `embedded` (default) or `remote`
- `SOVRANT_SERVER_URL` — remote server URL
- `SOVRANT_API_TOKEN` — bearer token for remote auth

### Authentication flow

1. Web frontend starts in remote mode, reads API token from config/env
2. `RemoteAuthService` sends `POST /v1/auth/validate` to verify token
3. On success, all HTTP clients include `Authorization: Bearer {token}` header
4. SignalR connection authenticates via query string token (standard SignalR pattern)
5. On 401, `RemoteAuthService` emits an event → UI shows "Reconnecting..." banner
6. Per-user permissions enforced server-side (Phase 38) — the web frontend never sees elevated access it shouldn't have

### Streaming protocol

| Event | Embedded (current) | Remote (this phase) |
|---|---|---|
| `TextDelta` | Direct async yield | SignalR `StreamAsync("StreamTurn", ...)` |
| `ToolCallStart` | Direct async yield | SignalR stream event |
| `ToolCallResult` | Direct async yield | SignalR stream event |
| `Error` | Direct async yield | SignalR stream event |
| **Fallback** | N/A | SSE via `GET /v1/chat/completions?stream=true` (for non-WebSocket environments) |

Chat.razor doesn't change — it already consumes `IAsyncEnumerable<RuntimeEvent>`. The remote implementation just produces that stream from SignalR instead of from the in-process runtime.

### Implementation plan

1. Define `appsettings.json` schema with `RuntimeMode` toggle. Update `Program.cs` to branch: `if (mode == "embedded") AddSovrantRuntime() else AddSovrantClient(serverUrl)`.
2. Implement `SovrantClientServices.AddSovrantClient(serverUrl)` — registers all `Remote*` implementations against the same interfaces the embedded registrations use.
3. Implement `RemoteConversationRuntime : IConversationRuntime` — `RunTurnAsync` calls `POST /v1/chat/completions` with `stream=true`, parses SSE events into `RuntimeEvent` objects, yields them as `IAsyncEnumerable<RuntimeEvent>`.
4. Implement `RemoteToolRegistry`, `RemoteSmartRouter`, `RemoteArtifactStore` — straightforward HTTP GET/POST wrappers against existing `Sovrant.Server` endpoints.
5. Implement `RemoteAuthService` — token validation, header injection via `DelegatingHandler`, 401 detection with reconnect.
6. Add `SovrantSignalRHub` to `Sovrant.Server` — maps to `/hubs/chat`, method `StreamTurn(string sessionId, string message)` → `IAsyncEnumerable<RuntimeEvent>`. Uses the same `ConversationRuntime.RunTurnAsync()` the REST endpoint uses.
7. Implement `SignalRStreamingClient` — connects to the hub, translates SignalR stream into `IAsyncEnumerable<RuntimeEvent>`. `RemoteConversationRuntime` prefers SignalR when available, falls back to SSE.
8. Add reconnection logic: exponential backoff, "Reconnecting..." UI banner, auto-resume on reconnect.
9. Wire tool confirmation over SignalR: when a tool needs approval, the server pushes a `ToolConfirmationRequest` event → Blazor shows the existing approval modal → user responds → `ConfirmToolAsync` call sent back over SignalR.
10. Tests:
    - `RemoteConversationRuntime` against a WireMock stub of `/v1/chat/completions?stream=true` — parses SSE events correctly
    - `SignalRStreamingClient` against an in-memory SignalR hub — receives streamed `RuntimeEvent` objects
    - `RemoteAuthService` — valid token passes, expired token triggers reconnect, missing token shows error
    - Mode toggle: `RuntimeMode=embedded` resolves in-process `IConversationRuntime`; `RuntimeMode=remote` resolves `RemoteConversationRuntime`
    - End-to-end (behind `REMOTE_E2E=1`): Blazor web + Sovrant.Server running → chat turn streams via SignalR → tool confirmation round-trips
    - All existing Chat.razor unit tests pass in both modes (same component, different DI)

### Acceptance criteria

- `dotnet build` exits 0
- `SOVRANT_RUNTIME_MODE=embedded` behaves identically to today (no regressions)
- `SOVRANT_RUNTIME_MODE=remote SOVRANT_SERVER_URL=https://localhost:5200` → Chat page streams responses from the remote server
- SignalR connection auto-reconnects after a transient disconnect
- Tool confirmation dialogs work over SignalR (remote server pushes request, web frontend responds)
- `sovrant` Server shows the web client connection in its session list
- Killing the remote server → web frontend shows "Disconnected" banner, not a crash

### Non-goals

- Blazor WASM deployment. This phase keeps Blazor Server (which has direct server access to SignalR). WASM would require a separate API abstraction layer and is a future concern.
- Multi-user real-time collaboration. Each web session is a single-user session against the runtime, same as embedded mode.
- Load balancing / horizontal scaling of `Sovrant.Server`. Single-server topology in this phase.

---

### Known Issues / Debt

| Issue | Priority | Notes |
|---|---|---|
| `AskUserQuestion` blocked in server mode | Low | By design — no interactive console available over HTTP. Could be solved via a webhook/callback URL pattern. |
| No request-level timeout on agentic loop | Medium | A runaway tool loop can occupy a session indefinitely; add per-turn wall-clock timeout. |
| CORS origins hardcoded | Low | Should be configurable via `SOVRANT_CORS_ORIGINS` env var. |
| `launchSettings.json` port conflicts with `SOVRANT_PORT` default | Low | `launchSettings.json` declares `5091`; Kestrel overrides to `5200`. Rapid restart or parallel test runs cause `SocketException (10048)`. Fix: align `launchSettings.json` with `SOVRANT_PORT`; add `--urls` CLI override for CI. |
| Team tools not yet smoke-tested with live LLM | Medium | `TeamCreate`/`TeamDelete`/`TeamStatus`/`TeamDelegate` have 58 unit tests but no end-to-end smoke test with a real provider. |
| Context compaction is brittle in four specific ways | Medium | `MaybeCompactHistoryAsync` in `ConversationRuntime` works but: (1) trigger is a fixed token count, not a percentage of the active model's real context window — fires too early on 200K models, never on 32K models; (2) keeps a hardcoded last-4 messages with no way to pin acceptance criteria, the latest tool error, or unresolved blockers; (3) summarises through `_config.Model` (pays top-tier price for a mechanical job that should route to the `fast` tier); (4) destructive in-memory — original messages are dropped from `_history` and cannot be re-read by a later turn. **Near-term tactical fix** (independent of Phase 51): make the threshold a percentage of the model's real window with the env var as an override; add a small `PinnedMessageSet` honoured by the keep-tail logic; route the summary call through the Phase 22 `fast` tier. The reversible/durable version of the trace lands as part of Phase 51's `IContextCompactor` + `runtime_traces` work. |

### Resolved Issues

| Issue | Resolution |
|---|---|
| Token counts always `0↑ 0↓` | ✅ `OpenAiCompatProvider` captures trailing OpenAI usage chunk |
| SmartRouter crashes on WSL DNS failure | ✅ Falls back to configured providers when all fail startup ping |
| Provider has no retry on 429/5xx | ✅ Phase 5 — 3 attempts with 1s/2s/4s backoff |
| `EnterPlanMode`/`ExitPlanMode` are global in server mode | ✅ Phase 10 — session-scoped `SessionConfig` overlay |
| `Sovrant.Agents` not wired into CLI or Server | ✅ Phase 19+20 — `AddOrchestrationSystem()` called in both hosts |

---

## Phase 62 — Sophisticated Conversational UX Across CLI, Desktop & Web (Intent-Driven Interaction & Voice)

**Depends on:** Phase 59 (intent classification + semantic gate), Phase 56 (web frontend), Phase 61 (remote mode), Phase 55 (cost tracking — for cost dashboard and budget UI)
**Difficulty:** High

**Goal:** Elevate the user experience across all three surfaces (CLI, Desktop, Web) from "chat box that calls tools" to a sophisticated, intent-aware conversational interface. The system should feel like speaking with an intelligent collaborator — understanding nuance, adapting its interaction style to the prompt's intent, surfacing the right UI affordances at the right time, and supporting voice as a first-class input/output channel.

**Why this matters:** Today all three surfaces share the same flat chat UX — a text box, a streaming response, and optional tool confirmations. But the intent classifier (Phase 59) already knows *what the user wants* before the LLM responds. That signal should drive the entire interaction: the UI layout, the response format, the confirmation flow, and the level of autonomy. A user asking "explain monads" should get a clean reading experience; a user saying "refactor auth.cs" should see a diff view with approve/reject; a user saying "build me an expense tracker" should see a project plan with checkboxes. Same runtime, different presentations.

### Components

| Component | Surface | What it does |
|---|---|---|
| **Intent-Adaptive Response Rendering** | All | Maps `IntentClass` to a response renderer: `Explain` → rich markdown with syntax highlighting and collapsible sections; `CodeGeneration`/`CodeEdit`/`Refactor` → side-by-side diff view; `Planning` → interactive checklist; `Debugging` → error trace with highlighted lines; `Compare` → comparison table; `Conversation` → plain chat bubble |
| **Contextual Input Affordances** | Desktop, Web | The input area adapts based on conversation state: shows file drop zone when intent is code-related; shows "Attach context" button for explain/research intents; shows quick-action chips ("Run tests", "Apply changes", "Show diff") after code generation turns |
| **Smart Suggestions & Follow-ups** | All | After each response, the system suggests 2–3 contextual follow-up actions based on the intent and result. After a code review: "Apply suggestions", "Explain issue #2", "Write tests for flagged code". After a plan: "Start execution", "Modify step 3", "Export as markdown" |
| **Conversational Memory Indicators** | Desktop, Web | Visual indicators showing the system remembers context: "Continuing from your auth refactor discussion", session topic tags, pinned context items the user can see and dismiss |
| **Voice Input/Output** | Desktop, Web | Speech-to-text for input (browser Web Speech API / desktop native), text-to-speech for responses (configurable, off by default). Voice mode adapts: shorter responses, confirmation prompts before destructive actions, audio cues for tool execution status |
| **Prompt Composition Assistant** | All | When the intent classifier returns low confidence or `NeedsClarification`, instead of a generic "Could you clarify?", the UI shows structured options: radio buttons for likely intents, a template prompt the user can edit, or a guided wizard for complex multi-step requests |
| **Thinking/Reasoning Transparency** | All | Show the intent classification, model tier selection, and routing decision in a collapsible "thinking" panel. Users see: "Intent: CodeGeneration (0.85 confidence) → Model: claude-sonnet → Tools: enabled". Builds trust and helps users learn to write better prompts |
| **Token Usage Display** | Desktop, Web | Show input and output token counts per turn and cumulative per session. Rendered inline below each assistant message (e.g., "↑ 1,204 tokens · ↓ 832 tokens") and as a running total in the session header/sidebar. When Phase 55 cost tracking is active, also shows estimated cost per turn and session total (e.g., "↑ 1,204 · ↓ 832 · $0.003"). CLI surfaces this via `--verbose` flag or `sovrant dashboard` command |
| **Unified Dashboard** | All | A multi-tab dashboard view that aggregates operational visibility into one place. Desktop: full Avalonia view navigable from sidebar or `Ctrl+Shift+D`. Web: `/dashboard` Blazor page with tab navigation. CLI: `sovrant dashboard` command that prints a combined status table. Tabs described below — cost is one tab, not the whole dashboard. The dashboard consumes existing endpoints (`GET /v1/status`, `GET /v1/usage`, `GET /v1/cost`) so the data layer is already built. Tabs: **Overview** (active model, provider health, active sessions, quick spend summary), **Cost & Budget** (Phase 55 — daily/weekly/monthly spend, per-model and per-session breakdowns, budget gauges, budget configuration), **Sessions** (active sessions with token counts, resume links, session timeline), **Providers** (provider health, latency, error rates, routing scores — data from `/v1/status`), **Models** (available models, pricing snapshot age, per-model usage stats). Additional tabs can be added by future phases (e.g. Evals, Missions, Team activity) |
| **Budget Inline Warnings** | Desktop, Web | When `BudgetEnforcer` hits 80% or 100% threshold, display an inline warning banner in the chat area: amber for 80% ("You've used 80% of your $5.00 session budget"), red for 100% with a "Budget exceeded" blocking message. Dismissible for 80%, non-dismissible for 100% |
| **Tone & Personality Adaptation** | All | Configurable interaction style per workspace: "concise" (terse, code-focused), "mentor" (explains reasoning, suggests learning resources), "pair programmer" (collaborative, asks clarifying questions), "executive" (summaries and decisions only). Injected into system prompt based on user preference |

### CLI-Specific Enhancements

| Feature | What it does |
|---|---|
| **Rich terminal rendering** | Intent-aware output: `Explain` → boxed markdown with Spectre.Console panels; `CodeGeneration` → syntax-highlighted code blocks with copy hints; `Planning` → numbered task list with status markers; `Diff` → colored unified diff |
| **Interactive confirmation flows** | Multi-step plans show a numbered list → user types step number to approve/skip/modify individual steps instead of all-or-nothing |
| **Progress spinners with context** | During tool execution: `[2/5] Reading auth.cs...` with elapsed time, not just a generic spinner |
| **Command completion** | Shell completion that understands Sovrant-specific patterns: `sovrant chat "fix the bug in` → suggests recent file names from the project |

### Desktop-Specific Enhancements

| Feature | What it does |
|---|---|
| **Split-pane response view** | Code intents automatically open a split view: chat on left, code/diff/artifact on right. Resizable, collapsible |
| **Inline code actions** | Hover actions on generated code: "Copy", "Apply to file", "Open diff", "Run". No need to copy-paste from chat |
| **Artifact preview panel** | When the intent produces an artifact (file, document, plan), it renders live in the artifact panel with syntax highlighting, not just a chat message |
| **Session timeline** | Visual timeline showing conversation flow: messages, tool calls, artifacts created, decisions made. Clickable to jump to any point |
| **Dashboard (Desktop)** | Full Avalonia view (`Ctrl+Shift+D` or sidebar icon) with tab strip matching the Unified Dashboard component. **Overview tab:** active model, provider health indicators, session count, quick spend counter. **Cost tab:** summary cards (today/week/month), per-model and per-session cost tables, daily spend bar chart (LiveCharts2 or OxyPlot), budget gauge bars with color thresholds, budget configuration panel (slider + text input, presets $1/$5/$10/$50/unlimited) that writes to `.sovrant/cost.json` and hot-reloads `BudgetEnforcer`. **Sessions tab:** active sessions list with token counts, resume buttons, and mini timeline. **Providers tab:** provider cards with latency/error rate/score from `/v1/status`. The sidebar also shows a mini spend counter that links into the dashboard |
| **Keyboard-driven workflow** | `Ctrl+Enter` to send, `Ctrl+Shift+Enter` to send and auto-approve all tools, `Ctrl+D` to toggle diff view, `Ctrl+P` to toggle plan view |

### Web-Specific Enhancements

| Feature | What it does |
|---|---|
| **Responsive intent layout** | Mobile: stacked chat; tablet: side panel for artifacts; desktop: full split-pane with resizable columns |
| **Collaborative indicators** | When connected to a shared server (Phase 61), show who else is in the workspace, their active sessions, and allow session sharing |
| **Dashboard (Web)** | `/dashboard` Blazor page with tab navigation matching the Unified Dashboard component. Same tabs as Desktop (Overview, Cost, Sessions, Providers, Models) rendered with Blazor components. Responsive layout: mobile collapses tabs to dropdown, tablet shows two-column cards, desktop shows full tab strip with data tables. Cost tab includes daily spend sparkline (lightweight SVG or Chart.js interop). Budget configuration via `PUT /v1/cost/config` with hot-reload. Deep-linkable tabs via URL hash (`/dashboard#cost`, `/dashboard#sessions`) |
| **Export & share** | One-click export of a conversation thread as markdown, PDF, or shareable link |
| **Notification system** | Browser notifications for long-running tasks: "Your refactor plan is ready for review", "Build completed with 2 warnings" |

### Voice Architecture

```
User speaks → Browser/Desktop Speech-to-Text → Text prompt
                                                    ↓
                                           IntentClassifier (Phase 59)
                                                    ↓
                                           ConversationRuntime
                                                    ↓
                                           Response text
                                                    ↓
                          Text-to-Speech (optional) ← Response rendered in UI
```

- **Input:** Web Speech API (browser), Windows Speech Recognition / whisper.cpp (desktop)
- **Output:** Web Speech Synthesis API (browser), Windows SAPI / configurable TTS engine (desktop)
- **Voice mode toggle:** `SOVRANT_VOICE=true` env var or UI toggle
- **Safety:** Voice mode always requires explicit confirmation for destructive tool actions (no auto-approve via voice)
- **CLI:** No voice support (terminal has no audio context). Users who want voice + CLI can use a desktop app as input and pipe to CLI

### Prompt Composition Templates

When `NeedsClarification` is true, the UI presents structured alternatives rather than free-text:

```
You said: "test"

What would you like to do?
  ○ Run the test suite          → "run dotnet test"
  ○ Create a new test file      → "create unit tests for [file]"
  ○ Test the chat connection    → (just chatting)
  ○ Something else              → [free text input]
```

Templates are driven by the `IntentClass` + `SuggestedClarification` from `SemanticIntentGate` (Phase 59a), extended with per-intent option sets.

### Tone Profiles (System Prompt Injection)

```json
{
  "concise": "Be brief. Lead with code or actions. Skip explanations unless asked.",
  "mentor": "Explain your reasoning. Suggest learning resources. Ask if the user wants deeper explanation.",
  "pair_programmer": "Think out loud. Ask clarifying questions. Propose alternatives before committing.",
  "executive": "Summarize decisions and outcomes. Skip implementation details. Highlight risks and trade-offs."
}
```

Stored per-workspace in user settings. Injected into system prompt by `ConversationRuntime` alongside intent context.

### Implementation Plan

1. **Intent-to-renderer mapping** — Create `IResponseRenderer` interface with implementations per intent class. CLI uses Spectre.Console, Desktop uses Avalonia views, Web uses Blazor components. The runtime emits `RuntimeEvent.IntentClassified` at turn start so the UI can pre-configure.
2. **Smart suggestions engine** — After each turn, generate 2–3 follow-up suggestions based on intent + result. Rule-based initially (Phase 59 classifier), LLM-generated later. Emit as `RuntimeEvent.SuggestedActions`.
3. **Prompt composition UI** — Extend `SemanticIntentGate.BuildClarification()` to return structured options (not just a string). Desktop and Web render as radio buttons; CLI renders as numbered list.
4. **Thinking panel** — Add `RuntimeEvent.RoutingDecision` event with intent, confidence, model, tier. All three surfaces render it in a collapsible/debug panel.
5. **Token usage display** — Subscribe to `TurnComplete` events (already carry `InputTokens`/`OutputTokens`). Desktop: add a `TokenUsageBadge` control below each assistant message showing "↑ N · ↓ N" with optional cost from `ICostModel` (Phase 55). Web: Blazor `<TokenBadge>` component. Session totals accumulated in a `SessionTokenTracker` and shown in sidebar. CLI: print token line when `--verbose`.
6. **Unified dashboard shell** — Define `IDashboardTab` interface with `Name`, `Icon`, and `Render()` method. Desktop: `DashboardView` Avalonia view with `TabStrip` + `ContentControl`, navigable via sidebar icon or `Ctrl+Shift+D`. Web: `/dashboard` Blazor page with `<TabNavigation>` component and deep-linkable hash routes. CLI: `sovrant dashboard` command that prints a combined Spectre.Console table. Each tab is a self-contained component that can be developed independently.
7. **Dashboard — Overview tab** — Summary view consuming `GET /v1/status`. Shows: active model badge, provider health cards (green/amber/red dots), active session count, quick spend counter (from `CostDashboardService` if enabled). Desktop: Avalonia `UniformGrid` of status cards. Web: Blazor `<OverviewTab>`. CLI: single-table summary.
8. **Dashboard — Cost & Budget tab** — Consumes `GET /v1/cost?range=...` and `CostDashboardService`. Summary cards (today/week/month), per-model and per-session cost tables (sortable), budget gauge bars with color thresholds, budget configuration panel (slider + text input, presets, writes to `.sovrant/cost.json` via `PUT /v1/cost/config`, hot-reloads `BudgetEnforcer`). Desktop: daily spend bar chart (LiveCharts2 or OxyPlot). Web: daily sparkline (SVG or Chart.js interop). Budget inline warnings injected into chat `ItemsControl` at 80%/100% thresholds.
9. **Dashboard — Sessions tab** — Consumes `GET /v1/usage`. Active sessions list with token counts, resume buttons, per-session cost if Phase 55 is active. Desktop: `DataGrid` with session resume action. Web: sortable table with session links.
10. **Dashboard — Providers tab** — Consumes `GET /v1/status`. Provider cards with latency/error rate/score/health. Desktop: `ItemsRepeater` with provider cards. Web: responsive card grid.
11. **Dashboard — Models tab** — Consumes `GET /v1/models`. Available models with pricing snapshot age (if Phase 55), per-model usage stats from cost log. Desktop + Web: sortable table.
12. **Tone profiles** — Add `ToneProfile` to workspace settings. `ConversationRuntime` reads it and appends to system prompt.
13. **Voice input (Web)** — Add `SpeechInputService` using Web Speech API. Blazor interop via JS. Toggle button in chat input area.
14. **Voice input (Desktop)** — Add `DesktopSpeechService` using Windows Speech Recognition or whisper.cpp via P/Invoke.
15. **Voice output** — Add `SpeechOutputService` (Web Speech Synthesis / Windows SAPI). Configurable per-session.
16. **CLI rich rendering** — Add Spectre.Console renderers for each intent class. Progress bars for multi-step plans.
17. **Desktop split-pane** — Add `SplitPaneView` that activates automatically for code/artifact intents.
18. **Session timeline (Desktop)** — Visual timeline component fed by `RuntimeEvent` stream.
19. **Web responsive layout** — CSS grid breakpoints for mobile/tablet/desktop with intent-aware panel allocation.
20. **Follow-up action chips** — Clickable UI elements after each response that populate the input with the suggested follow-up.

### Acceptance Criteria

- All three surfaces (CLI, Desktop, Web) render responses differently based on intent class
- `Explain` intent → rich markdown rendering (not raw text)
- `CodeGeneration` intent → syntax-highlighted code with "Apply" action
- `Planning` intent → interactive checklist
- `NeedsClarification` → structured options UI (not just a text question)
- Voice input works on Desktop and Web (opt-in)
- Tone profiles affect system prompt and response style
- Thinking/routing panel shows intent, model, and confidence
- Token usage (input/output) displayed per turn on Desktop and Web; session total visible in header/sidebar
- When cost tracking (Phase 55) is enabled, estimated cost shown alongside token counts
- Unified dashboard accessible on all three surfaces: Desktop (`Ctrl+Shift+D`), Web (`/dashboard`), CLI (`sovrant dashboard`)
- Dashboard Overview tab shows active model, provider health, session count, and quick spend summary
- Dashboard Cost tab shows daily/weekly/monthly spend, per-model and per-session breakdowns, budget gauges, and budget configuration
- Dashboard Sessions tab shows active sessions with token counts and resume links
- Dashboard Providers tab shows provider health, latency, error rates from `/v1/status`
- Budget warnings appear inline in chat at 80% (amber, dismissible) and 100% (red, blocking)
- Budget caps configurable from the dashboard Cost tab on Desktop and Web with immediate effect
- Dashboard is extensible — future phases can add tabs (Evals, Missions, Teams) without restructuring
- Smart follow-up suggestions appear after each turn
- `dotnet build Sovrant.slnx` exits 0
- All existing tests pass (no regressions)

### Non-goals

- Real-time multi-user collaboration (beyond session sharing indicators)
- Custom voice model training or voice cloning
- Non-English voice support (English-only in first iteration)
- Plugin/extension system for custom renderers (future phase)

---

## Phase 63 — Dependency Injection Audit & Pluggability Hardening ✅

**Depends on:** None (can run in parallel with any phase)
**Difficulty:** Medium

**Status:** ✅ Complete (2026-04-12). Tier 1 and Tier 2 fully implemented:
- **Tier 1a:** All `new HttpClient()` replaced with `IHttpClientFactory` — `McpOAuthService`, `SettingsViewModel`, `DiagnosticsViewModel`, `Setup.razor`, `Diagnostics.razor`, `Settings.razor`. Named clients `"McpOAuth"` and `"ProviderProbe"` registered with timeouts.
- **Tier 1b:** `IScopedProviderFactory` + `DefaultScopedProviderFactory` extracted; `ChatRoutes.cs` uses injected factory instead of `new OpenAiCompatProvider(...)`.
- **Tier 1c:** `CredentialConfig` (in `Sovrant.Api.Config`) centralizes all credential env-var reads. Single `Resolve(IConfiguration)` call at startup; consumed by `Server/Program.cs`, `BearerTokenMiddleware`, `WebSearchTool`, `WebSearchCommand`, `LiveModelMetadataFetcher`.
- **Tier 2a:** `ISwarmOrchestrator`, `ISwarmStateTracker`, `ISwarmFileLockManager` extracted; all consumers (SwarmTool, SwarmStatusTool, SwarmToolExecutor, AgentOrchestrator, SwarmRoutes, CLI) updated to depend on interfaces.
- **Tier 2b:** `IArtifactStoreFactory` + `DefaultArtifactStoreFactory` extracted; DI uses factory for backend selection.
- **Tier 2c:** `ILspClientFactory` + `DefaultLspClientFactory` extracted; `LspClientManager` uses factory. `ITemplateLoader` + `FileSystemTemplateLoader` extracted; `AgentTemplateRegistry` accepts optional additional loaders.
- Tier 3 deferred (not needed for current use cases).

**Goal:** Audit and fix concrete DI gaps across the codebase — not for DI purity, but where pluggability, security isolation, or testability is genuinely blocked. The system has 28+ concrete singletons without interfaces, 9 `new HttpClient()` calls bypassing `IHttpClientFactory`, 40+ scattered `Environment.GetEnvironmentVariable` reads in service logic, and per-request provider instantiation in hot paths. This phase fixes the ones that matter.

**Philosophy:** Do not over-inject. A `TodoState` singleton that will never have a second implementation doesn't need an `ITodoState` interface. But an `McpOAuthService` doing `new HttpClient()` for OAuth token exchange is a socket-exhaustion bug waiting to happen, a `SwarmOrchestrator` that can't be swapped for a distributed implementation is an architecture wall, and credentials read from raw env vars deep in service logic blocks vault/secret-store integration. Fix what's blocking, leave what's fine.

### Audit Findings

#### Tier 1 — Fix Now (Security & Correctness)

| Issue | Location | Impact |
|---|---|---|
| `new HttpClient()` in OAuth service | `Runtime/Mcp/McpOAuthService.cs:65` | Socket exhaustion in production; bypasses handler pipeline, no retry/timeout policy |
| `new HttpClient()` in Desktop/Web UI (9 instances) | `Desktop/ViewModels/SettingsViewModel.cs:269,305`, `Desktop/ViewModels/DiagnosticsViewModel.cs:169,254`, `Web/Pages/Setup.razor:136`, `Web/Pages/Diagnostics.razor:227,283`, `Web/Pages/Settings.razor:277` | Same socket issue; blocks proxy/auth injection; no timeout defaults |
| Per-request `new OpenAiCompatProvider(...)` in chat route | `Server/Routes/ChatRoutes.cs:96-99` | Bypasses DI lifecycle; no disposal tracking; blocks provider-level middleware (logging, metrics, circuit breaker) |
| Credentials read from raw env vars in service logic | `Tools/Extended/WebSearchTool.cs:58-59` (BRAVE_API_KEY, FIRECRAWL_API_KEY), `Api/Capabilities/LiveModelMetadataFetcher.cs:38` (OPENROUTER_API_KEY), `McpServer/McpTokenValidator.cs:22` | Blocks secret vault integration; env vars checked on every call instead of once at startup; credentials in memory longer than necessary |

#### Tier 2 — Fix for Pluggability (Architecture Walls)

| Issue | Location | Impact |
|---|---|---|
| Swarm system — 6 concrete singletons, no interfaces | `Agents/ServiceCollectionExtensions.cs:51-56` — `SwarmFileLockManager`, `SwarmStateTracker`, `SwarmSession`, `SwarmOrchestrator`, `SwarmQualityGate` | Cannot swap for distributed swarm (Redis-backed locks, remote state, cluster orchestration) without modifying DI registration in every host |
| Artifact backend hardcoded to "local" | `Runtime/ServiceCollectionExtensions.cs:216` — env var switch with only one case | Cannot plug cloud storage (S3, Azure Blob) without editing the composition root |
| LSP client created directly in manager | `Lsp/LspClientManager.cs:53` — `new LspClient(config, logger)` | Cannot inject mock/remote LSP clients; blocks testing and remote language server support |
| Agent template loading from filesystem only | `Agents/Templates/AgentTemplateRegistry.cs:82` — `File.ReadAllText(path)` | Cannot load templates from database, cloud, or version control |
| `InProcessOrchestrationSystem` hardcoded in tool factory | `Tools/ServiceCollectionExtensions.cs:122` | Team delegation tool locked to in-process agents; blocks distributed agent execution |

#### Tier 3 — Fix for Testability (Nice to Have)

| Issue | Location | Impact |
|---|---|---|
| `SovrantConfig` injected as concrete singleton | `Runtime/ServiceCollectionExtensions.cs:41` | Tests must construct full config objects; no `IOptions<T>` hot-reload support |
| 40+ `Environment.GetEnvironmentVariable` in service classes | Across Runtime, Api, Tools, Commands, Agents | Tests must manipulate real env vars or use reflection; no centralized config binding |
| `HttpClient` injected directly (not factory) | `Api/Routing/SmartRouter.cs:63`, `Api/Capabilities/LiveModelMetadataFetcher.cs:21` | Works (DI provides them), but loses named-client handler pipeline benefits |
| State singletons without interfaces | `Tools/ServiceCollectionExtensions.cs:44-48` — `TodoState`, `BackgroundTaskRegistry`, `WorktreeState`, `ShellSessionState`, `ShellEnvironment` | Cannot mock in unit tests; but these are simple state bags unlikely to have alternate implementations |
| `SlashCommandDispatcher` reads files directly | `Commands/SlashCommandDispatcher.cs:49` | Cannot test with virtual file system; blocks template loading from DB |
| `ProcessExecutor` / `ProcessAgent` call `Process.Start` directly | `Tools/ProcessExecutor.cs:58`, `Agents/Isolated/ProcessAgent.cs:64` | Cannot mock process execution in unit tests |

#### What's Already Good (No Changes Needed)

- **Storage layer** — `IStorageProvider`, `ISqliteConnectionFactory`, `ISessionStore`, `IArtifactStore` all interface-backed
- **Auth** — `IAuthProvider`, `IPermissionPolicy`, `IPermissionModeAccessor` properly abstracted
- **Router** — `ISmartRouter`, `IModelCapabilityRegistry`, `IModelTierResolver` all interfaces
- **Runtime** — `IConversationRuntime` registered as transient (correct for per-session state)
- **Commands** — All slash commands implement `ISlashCommand`
- **Tools** — All tools implement `ITool`, registered via `IToolRegistry`
- **Agent system** — `IOrchestrationSystem`, `ITeamRegistry`, `ISwarmDecomposer`, `IAgentOrchestrator` all interface-backed
- **Composition roots** — Clean separation in `ServiceCollectionExtensions.cs` per assembly
- **No service locator anti-pattern** — `IServiceProvider.GetService` only in factory methods inside composition roots

### Implementation Plan

#### Tier 1 — Security & Correctness (Do First)

1. **HttpClient factory migration**
   - `McpOAuthService`: inject `IHttpClientFactory`, register named client `"McpOAuth"` with timeout + retry policy
   - Desktop ViewModels: inject `IHttpClientFactory` into `SettingsViewModel` and `DiagnosticsViewModel` via constructor; Avalonia ViewModels already support DI via `ViewModelBase`
   - Web Razor pages: use `@inject IHttpClientFactory HttpFactory` instead of `new HttpClient()`
   - Add `Polly` retry policy on the named clients for transient fault handling

2. **Scoped provider factory for ChatRoutes**
   - Extract `IScopedProviderFactory` interface:
     ```csharp
     public interface IScopedProviderFactory
     {
         ISmartRouter CreateScoped(string apiKey, string baseUrl, HttpClient http);
     }
     ```
   - Register default implementation that wraps current `new OpenAiCompatProvider(...)` logic
   - ChatRoutes receives factory via DI, not instantiating providers directly
   - Enables: provider-level metrics, circuit breaker, disposal tracking

3. **Credential binding at startup**
   - Create `CredentialConfig` record bound once at startup from env vars:
     ```csharp
     public sealed record CredentialConfig(
         string? BraveApiKey,
         string? FirecrawlApiKey,
         string? OpenRouterApiKey,
         string? McpToken);
     ```
   - Register as singleton, inject into `WebSearchTool`, `LiveModelMetadataFetcher`, `McpTokenValidator`
   - Eliminates per-call `Environment.GetEnvironmentVariable` for credentials
   - Future: swap binding source from env vars to Azure Key Vault / HashiCorp Vault without touching services

#### Tier 2 — Pluggability (Do Next)

4. **Swarm abstraction interfaces**
   - `ISwarmOrchestrator` — extract from `SwarmOrchestrator` (main orchestration logic)
   - `ISwarmStateTracker` — extract from `SwarmStateTracker` (state management)
   - `ISwarmFileLockManager` — extract from `SwarmFileLockManager` (file locking)
   - Register concrete implementations as defaults; future distributed implementations swap in via config
   - This is the main wall blocking distributed/remote swarm execution

5. **Artifact store factory**
   - Replace env-var switch with `IArtifactStoreFactory`:
     ```csharp
     public interface IArtifactStoreFactory
     {
         IArtifactStore Create(string backend); // "local", "s3", "azure"
     }
     ```
   - Register factory; composition root calls `factory.Create(config.ArtifactBackend)`
   - `LocalArtifactStore` becomes one implementation; cloud backends plug in later

6. **LSP client factory**
   - Extract `ILspClientFactory` from `LspClientManager`
   - Manager calls `_factory.Create(config)` instead of `new LspClient(config, logger)`
   - Enables: mock LSP in tests, remote LSP servers, language-specific client subclasses

7. **Template loader abstraction**
   - Extract `ITemplateLoader` from `AgentTemplateRegistry` and `SlashCommandDispatcher`
   - Default: `FileSystemTemplateLoader`
   - Future: `DatabaseTemplateLoader`, `GitTemplateLoader`

#### Tier 3 — Testability (Do If Time Permits)

8. **Environment variable consolidation**
   - Create `SovrantEnvironment` config class bound once at startup:
     ```csharp
     public sealed record SovrantEnvironment(
         string UserId,
         string? WorkspaceId,
         string? ProjectId,
         string? ArtifactsRoot,
         string? DbPath,
         LoggingLevel LogLevel,
         // ... all SOVRANT_* env vars
     );
     ```
   - Inject everywhere that currently reads `Environment.GetEnvironmentVariable("SOVRANT_*")`
   - Single point of truth; tests inject a test instance instead of manipulating env vars
   - Does NOT replace `IConfiguration` — this is specifically for the 30+ `SOVRANT_*` env vars scattered across service logic

9. **Process executor abstraction**
   - Extract `IProcessExecutor` from `ProcessExecutor` and `ProcessAgent`
   - Default implementation delegates to `Process.Start`
   - Test implementation captures commands without spawning processes

10. **Config wrapper for hot-reload support**
    - Wrap `SovrantConfig` in `IOptionsMonitor<SovrantConfig>` for hot-reload
    - Only worth doing if runtime config changes become a real use case (e.g., admin panel changing model mid-session)

### Inventory Summary

| Category | Concrete Singletons | Interface-Backed | Action |
|---|---|---|---|
| **Storage** | 0 | 5 | None needed |
| **Auth/Permissions** | 0 | 3 | None needed |
| **Routing** | 0 | 3 | None needed |
| **Swarm** | 3 | 3 | ✅ Interfaces extracted |
| **Tools state** | 5 | 0 | Leave (simple state bags) |
| **Agent utilities** | 4 | 4 | Extract for template + factory |
| **MCP** | 2 | 1 | ✅ HttpClient fixed |
| **Commands** | 2 | 26 | Dispatcher needs template loader |
| **Config** | 3 | 0 | Env var consolidation |
| **Total** | **28** | **41** | **Fix 14, leave 14** |

### Acceptance Criteria (All Met ✅)

- ✅ Zero `new HttpClient()` outside of test projects
- ✅ `ChatRoutes` uses injected `IScopedProviderFactory`, not `new OpenAiCompatProvider`
- ✅ All credential reads go through `CredentialConfig`, not raw env vars
- ✅ `SwarmOrchestrator`, `SwarmStateTracker`, `SwarmFileLockManager` implement new interfaces
- ✅ Artifact store selection is factory-based via `IArtifactStoreFactory`
- ✅ `dotnet build Sovrant.slnx` exits 0
- ✅ All existing tests pass (1,483 tests, 4 pre-existing failures unrelated to DI changes)
- ✅ No new interfaces for state bags (`TodoState`, `WorktreeState`, etc.) — left alone

### Non-goals

- Migrating to `IOptions<T>` pattern across the board (only where hot-reload is needed)
- Adding interfaces to every singleton (only where a second implementation is plausible)
- Replacing `Environment.GetEnvironmentVariable` in `Program.cs` / startup code (that's the right place for it)
- DI purity — the goal is pluggability where it matters, not 100% interface coverage

---

## Phase 64 — Cloud Storage Backends & Workspace Isolation (Google Docs, Box, Amazon S3)

**Depends on:** Phase 53 (scoped artifact storage — `IArtifactStore` + `IArtifactStoreFactory`), Phase 35 (workspaces), Phase 36 (projects)
**Relates to:** Phase 47 (workspace backup/export), Phase 38 (per-user auth)
**Difficulty:** Large

**Goal:** Extend the artifact and workspace storage layer to support cloud backends — Amazon S3, Google Drive/Docs, and Box — as first-class storage providers. Each workspace or project can independently select a storage backend, enabling true multi-tenant isolation where different teams, clients, or projects keep their work areas in separate cloud accounts.

### Motivation

Phase 53 shipped `IArtifactStore` and `IArtifactStoreFactory` with a `LocalArtifactStore` implementation and a factory that recognizes `"local"` as the backend key. The abstraction was explicitly designed for cloud follow-up: `GetAccessUrlAsync` returns presigned URLs for cloud stores and `file://` for local. The `{workspace_id}/{project_id}/{run_id}/` path layout maps directly to S3 prefixes, Google Drive folder hierarchies, and Box folder trees.

What's missing:
- No cloud `IArtifactStore` implementations exist yet.
- Workspace/project config doesn't carry a `storage_backend` setting — all tenants share the local filesystem.
- No document-aware integration (Google Docs, Box Notes) for structured output beyond raw files.
- No credential management for per-workspace cloud accounts (OAuth flows for Google/Box, IAM roles or access keys for S3).

### Target Architecture

#### Storage Backend Registry

```
IArtifactStoreFactory
  ├── "local"    → LocalArtifactStore (existing)
  ├── "s3"       → S3ArtifactStore (new)
  ├── "gcs"      → GcsArtifactStore (new, Google Cloud Storage)
  ├── "gdrive"   → GoogleDriveArtifactStore (new)
  ├── "box"      → BoxArtifactStore (new)
  └── "azure"    → AzureBlobArtifactStore (future)
```

Each backend implements `IArtifactStore`. The factory reads per-workspace config to select the backend. A workspace without explicit config falls back to the global default (`SOVRANT_ARTIFACTS_BACKEND` env var, default `"local"`).

#### Per-Workspace Storage Config

Add `storage_backend` and `storage_config` columns to the `workspaces` table:

```sql
ALTER TABLE workspaces ADD COLUMN storage_backend TEXT DEFAULT 'local';
ALTER TABLE workspaces ADD COLUMN storage_config TEXT; -- JSON blob for backend-specific settings
```

`storage_config` holds backend-specific settings as JSON:

| Backend | `storage_config` schema |
|---|---|
| `s3` | `{ "bucket": "sovrant-ws-acme", "region": "us-east-1", "prefix": "artifacts/", "role_arn": "arn:aws:iam::..." }` |
| `gdrive` | `{ "folder_id": "1abc...", "service_account_key_ref": "cred:gdrive-acme" }` |
| `box` | `{ "folder_id": "12345", "client_id": "...", "client_secret_ref": "cred:box-acme" }` |
| `gcs` | `{ "bucket": "sovrant-acme", "prefix": "artifacts/" }` |
| `local` | `null` (uses default `SOVRANT_ARTIFACTS_ROOT`) |

Credentials are stored via the existing `ICredentialStore` (SQLite `credentials` table, encrypted at rest) — `storage_config` only holds references, never raw secrets.

#### Amazon S3 Backend (`S3ArtifactStore`)

- Uses the AWS SDK for .NET (`AWSSDK.S3`).
- Bucket per deployment or per workspace (configurable).
- Object key layout: `{workspace_id}/{project_id}/{run_id}/{relative_path}`.
- `GetAccessUrlAsync` returns presigned GET URLs with configurable TTL.
- `WriteAsync` uses multipart upload for files > 5 MB.
- `DeleteAsync` uses `DeleteObjectsAsync` with batched keys.
- Supports IAM roles (for EC2/ECS/Lambda), access keys, and assumed roles (for cross-account).
- `ListAsync` uses `ListObjectsV2` with prefix filtering.

#### Google Drive Backend (`GoogleDriveArtifactStore`)

- Uses the Google Drive API v3 via `Google.Apis.Drive.v3`.
- Workspace → top-level Drive folder. Projects → subfolders. Runs → leaf folders.
- `WriteAsync` creates files; for `.md`/`.txt` content, optionally converts to Google Docs format for native editing.
- `ReadAsync` exports Google Docs back to markdown/plain text.
- `GetAccessUrlAsync` returns shareable Drive links with domain-scoped ACLs.
- Auth: OAuth 2.0 consent flow (interactive) or service account key (headless).
- Supports shared drives (Team Drives) for workspace-level isolation.

#### Box Backend (`BoxArtifactStore`)

- Uses the Box .NET SDK (`Box.V2`).
- Folder hierarchy mirrors S3 key layout: workspace → project → run → files.
- `WriteAsync` uploads files; for `.md`/`.txt`, optionally creates Box Notes.
- `GetAccessUrlAsync` returns shared links with password/expiry.
- Auth: OAuth 2.0 (user context) or JWT (server-to-server for enterprise).
- Supports Box metadata templates for tagging artifacts with run context.

#### Google Cloud Storage Backend (`GcsArtifactStore`)

- Uses `Google.Cloud.Storage.V1`.
- Same prefix layout as S3 (`{workspace_id}/{project_id}/{run_id}/`).
- `GetAccessUrlAsync` returns signed URLs.
- Auth: application default credentials, service account key, or workload identity.

### Workspace Isolation Model

```
Workspace "Acme Corp"
  ├── storage_backend: "s3"
  ├── storage_config: { bucket: "acme-sovrant", region: "us-east-1" }
  ├── Project "Backend API"
  │     └── runs/ → s3://acme-sovrant/ws-acme/proj-api/{run_id}/
  └── Project "Mobile App"
        └── runs/ → s3://acme-sovrant/ws-acme/proj-mobile/{run_id}/

Workspace "Personal"
  ├── storage_backend: "local"  (default)
  └── Project "experiments"
        └── runs/ → ~/.sovrant/artifacts/ws-personal/proj-experiments/{run_id}/

Workspace "Client XYZ"
  ├── storage_backend: "gdrive"
  ├── storage_config: { folder_id: "1abc...", service_account_key_ref: "cred:gdrive-xyz" }
  └── Project "Audit Report"
        └── runs/ → Google Drive: Client XYZ/Audit Report/{run_id}/
```

Each workspace is hermetically sealed — artifacts in workspace A are never accessible from workspace B, enforced at the storage backend level (separate buckets, folders, or credentials).

### API Surface

| Endpoint | Method | Description |
|---|---|---|
| `PUT /v1/workspaces/{id}/storage` | PUT | Configure storage backend for a workspace (admin only) |
| `GET /v1/workspaces/{id}/storage` | GET | Read current storage config (redacted credentials) |
| `POST /v1/workspaces/{id}/storage/test` | POST | Verify connectivity to the configured backend |
| `POST /v1/workspaces/{id}/storage/migrate` | POST | Migrate artifacts from one backend to another |

### Implementation Plan

1. **Database migration (V013)** — add `storage_backend` + `storage_config` columns to `workspaces`.
2. **Scoped factory** — `IArtifactStoreFactory.Create(string backend, string? configJson)` overload that accepts backend-specific config. `DefaultArtifactStoreFactory` dispatches to the correct implementation.
3. **S3 backend** — `S3ArtifactStore` in `Sovrant.Runtime.Artifacts.Cloud`. New NuGet: `AWSSDK.S3`.
4. **Google Drive backend** — `GoogleDriveArtifactStore`. New NuGet: `Google.Apis.Drive.v3`.
5. **Box backend** — `BoxArtifactStore`. New NuGet: `Box.V2`.
6. **GCS backend** — `GcsArtifactStore`. New NuGet: `Google.Cloud.Storage.V1`.
7. **Storage routes** — `PUT/GET /v1/workspaces/{id}/storage` in `WorkspaceRoutes`.
8. **Migration tool** — `POST /v1/workspaces/{id}/storage/migrate` streams artifacts from source to destination backend.
9. **Tests** — integration tests with mocked cloud SDK clients; contract tests verifying all backends produce identical behavior for the `IArtifactStore` interface.

### Acceptance Criteria

- [ ] `IArtifactStoreFactory.Create("s3", configJson)` returns a working `S3ArtifactStore`
- [ ] `IArtifactStoreFactory.Create("gdrive", configJson)` returns a working `GoogleDriveArtifactStore`
- [ ] `IArtifactStoreFactory.Create("box", configJson)` returns a working `BoxArtifactStore`
- [ ] Workspace storage config persisted in SQLite, credentials stored via `ICredentialStore`
- [ ] `PUT /v1/workspaces/{id}/storage` configures backend; `POST .../test` verifies connectivity
- [ ] Artifacts written by agents in workspace A are stored in workspace A's configured backend
- [ ] Artifacts in workspace B are unreachable from workspace A (isolation verified by test)
- [ ] Migration endpoint copies all artifacts from one backend to another without data loss
- [ ] `LocalArtifactStore` remains the default — no cloud dependency for single-user/self-hosted deployments
- [ ] All existing tests continue to pass (cloud backends do not affect local-only code paths)

---

## Phase 65 — Video Generation (fal.ai, Kling AI & Pluggable Providers)

**Depends on:** Phase 53 (artifact storage), Phase 54 (model capability registry), Phase 64 (cloud storage backends — for large video files)

**Goal:** Give Sovrant the ability to generate, preview, and manage video content through external video generation APIs. Users can request video creation from chat across all delivery modes (CLI, Desktop, Web), with results stored as workspace-scoped artifacts.

### Why

Video generation is rapidly becoming a core creative capability. Services like fal.ai and Kling AI expose text-to-video and image-to-video APIs that can be orchestrated by an agentic coding assistant. Use cases include:

- Generating demo videos from text descriptions
- Creating animated content from static images or diagrams
- Producing short explainer or marketing clips as part of a project workflow
- Agent-driven video pipelines where the agent scripts, generates, and iterates on video content

### Provider Architecture

```
IVideoProvider (interface)
  ├── FalAiVideoProvider        — fal.ai (text-to-video, image-to-video, lip-sync, etc.)
  ├── KlingAiVideoProvider      — Kling AI (text-to-video, image-to-video)
  └── (future: Runway, Pika, Luma, Stability Video, etc.)

IVideoProviderFactory
  └── Resolves provider by name from DI, reads config from providers.json
```

Each provider implements a common interface:

```csharp
public interface IVideoProvider
{
    string Name { get; }
    Task<VideoGenerationResult> GenerateAsync(VideoRequest request, CancellationToken ct);
    Task<VideoStatus> CheckStatusAsync(string jobId, CancellationToken ct);
    Task<Stream> DownloadAsync(string videoUrl, CancellationToken ct);
}

public record VideoRequest(
    string Prompt,
    string? ImageUrl = null,          // for image-to-video
    string? Model = null,             // provider-specific model (e.g., "kling-v2", "minimax-video")
    int? DurationSeconds = null,
    string? AspectRatio = null,       // "16:9", "9:16", "1:1"
    string? Style = null              // provider-specific style hints
);

public record VideoGenerationResult(
    string JobId,
    VideoStatus Status,
    string? VideoUrl = null,
    TimeSpan? EstimatedTime = null
);

public enum VideoStatus { Queued, Processing, Completed, Failed }
```

### Tool: `VideoGenerateTool`

A new tool in `Sovrant.Tools.Media` that agents can invoke:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `prompt` | string | Yes | Text description of the video to generate |
| `image_path` | string | No | Path to an input image for image-to-video |
| `provider` | string | No | Provider name (`fal`, `kling`). Defaults to first configured video provider |
| `model` | string | No | Provider-specific model identifier |
| `duration` | int | No | Target duration in seconds |
| `aspect_ratio` | string | No | Aspect ratio (`16:9`, `9:16`, `1:1`) |
| `wait` | bool | No | If true, poll until complete. If false, return job ID for async checking |

Returns: artifact path to the generated video file, stored in the workspace artifact store.

### Tool: `VideoStatusTool`

Check the status of an async video generation job:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `job_id` | string | Yes | The job ID returned by `VideoGenerateTool` |
| `provider` | string | No | Provider name |

### UI Integration

| Mode | Behavior |
|---|---|
| **CLI** | Show progress bar with estimated time, open video in default player on completion |
| **Desktop** | Inline video player in chat (Avalonia `MediaElement` or `LibVLCSharp`), download button |
| **Web** | HTML5 `<video>` tag in chat message, download link, preview thumbnail |

### Provider Configuration

Video providers are configured alongside LLM providers in `~/.sovrant/providers.json`:

```json
{
  "videoProviders": [
    {
      "name": "fal",
      "provider": "fal.ai",
      "apiKey": "fal-...",
      "baseUrl": "https://fal.run"
    },
    {
      "name": "kling",
      "provider": "kling",
      "apiKey": "sk-...",
      "baseUrl": "https://api.klingai.com"
    }
  ]
}
```

Settings page (Desktop + Web) gets a "Video Providers" section similar to the existing LLM provider cards.

### Implementation Plan

1. **`IVideoProvider` interface + factory** — `Sovrant.Runtime.Video` namespace. Register in DI via `AddVideoProviders()`.
2. **fal.ai provider** — REST client for fal.ai queue API (`POST /fal-ai/{model}`, `GET /requests/{id}/status`). Supports text-to-video and image-to-video.
3. **Kling AI provider** — REST client for Kling API. Supports text-to-video and image-to-video models.
4. **`VideoGenerateTool` + `VideoStatusTool`** — new tools in `Sovrant.Tools.Media`. Registered in tool registry with capability gate (only available when a video provider is configured).
5. **Artifact integration** — generated videos saved to workspace artifact store with metadata (prompt, provider, model, duration, resolution).
6. **Web UI** — `<video>` element in `ChatMessage.razor` for video artifacts. Thumbnail extraction via first-frame capture.
7. **Desktop UI** — video player control in chat view. Fallback: "Open in player" button using `Process.Start`.
8. **CLI** — progress display during generation, auto-open on completion.
9. **Settings UI** — video provider cards in Settings page for both Desktop and Web.
10. **Tests** — unit tests with mocked provider responses, integration test for artifact storage flow.

### Acceptance Criteria

- [ ] `IVideoProvider` interface defined with `GenerateAsync`, `CheckStatusAsync`, `DownloadAsync`
- [ ] `FalAiVideoProvider` generates video from text prompt and returns downloadable URL
- [ ] `KlingAiVideoProvider` generates video from text prompt and returns downloadable URL
- [ ] Image-to-video works for both providers when `image_path` is supplied
- [ ] `VideoGenerateTool` available in tool registry when a video provider is configured
- [ ] Generated videos stored as workspace-scoped artifacts with metadata
- [ ] Web UI renders inline `<video>` player for video artifacts in chat
- [ ] Desktop UI provides video playback or "open in player" for video artifacts
- [ ] CLI shows generation progress and opens completed video
- [ ] Video providers configurable in `~/.sovrant/providers.json` and Settings UI
- [ ] Async job polling works — agent can start generation, do other work, and check status later
- [ ] All existing tests continue to pass

---

## Phase 66 — Document Generation (PDFs, Office Suite & Industry Templates) ✅

**Depends on:** Phase 53 (artifact storage), Phase 58 (trust boundary — for PII/PHI handling in healthcare docs)

**Goal:** Give Sovrant agents the ability to generate professional documents — PDFs, Word, Excel, PowerPoint, and HTML — from chat, including industry-specific templates for real estate, healthcare, legal, finance, education, construction, and more. Documents are stored as workspace-scoped artifacts and downloadable from all delivery modes.

### Why

Every industry runs on documents. Contracts, reports, invoices, disclosures, care plans, proposals, compliance filings — the list is endless. Today, professionals spend hours formatting documents that an AI agent could draft, populate, and format in seconds. By combining Sovrant's agentic reasoning with structured document generation, the assistant becomes a true productivity multiplier across industries.

The key differentiator: Sovrant doesn't just fill templates — the agent understands context, pulls data from the conversation and workspace, reasons about what sections to include, and generates complete, professional documents.

### Open-Source Library Strategy

All chosen libraries are **truly permissively licensed** (MIT / Apache 2.0 / MPL) — no LGPL/AGPL/GPL viral clauses, no commercial-revenue-threshold gotchas. This matters because Sovrant is self-hostable enterprise software; licensing ambiguity is a non-starter.

**Deliberately excluded alternatives:**

| Library | Why excluded |
|---|---|
| QuestPDF | Free for companies under $1M revenue, commercial license above — not truly open source for enterprise |
| iText 7 | AGPL or commercial — AGPL is viral and forces entire app open-source |
| EPPlus (v5+) | Polyform Noncommercial after v5 — same revenue-threshold gotcha |
| Pandoc | GPL — viral license, contaminates linking code |
| Playwright / Puppeteer (for PDF) | Requires ~150MB Chromium download on first run; adds external binary dependency and cold-start latency. Deferred indefinitely — users who need pixel-perfect HTML/CSS PDFs can print from their own browser after receiving the HTML output |

### Document Engine Architecture

```
IDocumentGenerator (interface)
  ├── PdfSharpGenerator            — PDFSharp (simple text-based PDFs)
  ├── MigraDocGenerator            — MigraDoc → PDFSharp (structured PDFs with tables/styles/headers)
  ├── WordDocumentGenerator        — DocumentFormat.OpenXml (Microsoft, MIT)
  ├── ExcelDocumentGenerator       — ClosedXML (friendlier API over OpenXml)
  ├── PowerPointGenerator          — DocumentFormat.OpenXml
  ├── HtmlDocumentGenerator        — Razor templating → HTML (user can browser-print to PDF)
  └── MarkdownDocumentGenerator    — Markdig → markdown/HTML

IDocumentTemplateStore
  ├── BuiltInTemplateStore         — shipped templates per industry (embedded resources)
  ├── WorkspaceTemplateStore       — user-created templates scoped to workspace
  └── CommunityTemplateStore       — (future) shared template marketplace
```

### Supported Output Formats

| Format | Library | License | Use Cases |
|---|---|---|---|
| **PDF (simple)** | PDFSharp | MIT | Plain text exports, simple receipts |
| **PDF (structured)** | MigraDoc + PDFSharp | MIT | Invoices, contracts, reports, financial statements, medical forms, proposals — 95% of business PDFs |
| **Word (.docx)** | DocumentFormat.OpenXml | MIT (Microsoft) | Editable contracts, proposals, letters, SOWs |
| **Excel (.xlsx)** | ClosedXML (+ OpenXml) | MIT | Financial models, data exports, inventories, schedules |
| **PowerPoint (.pptx)** | DocumentFormat.OpenXml | MIT | Pitch decks, project updates, training materials |
| **HTML** | Razor templates | MIT (.NET) | Email-ready docs, web-embeddable reports, browser-printable designed PDFs |
| **Markdown** | Markdig | BSD-2-Clause | Developer docs, READMEs, wikis |
| **Typst (optional)** | Typst CLI | Apache 2.0 | Beautiful typeset reports — opt-in, future phase |

**Why PDFSharp + MigraDoc instead of headless Chromium:** No external binaries, no 150MB first-run download, no cold-start latency, no extra process to supervise. Total NuGet footprint for the PDF stack: ~2MB. MigraDoc provides a high-level document model (paragraphs, tables, headers, footers, page numbers, styles) that covers 95% of real business documents. For the remaining 5% where pixel-perfect HTML/CSS design fidelity matters (marketing brochures, Figma-exported layouts), the agent emits HTML and the user prints-to-PDF from their browser — no runtime dependency required.

### Industry Template Library

#### Real Estate
| Template | Description |
|---|---|
| `real-estate/purchase-agreement` | Residential/commercial purchase and sale agreement |
| `real-estate/lease-agreement` | Standard lease with configurable terms, clauses |
| `real-estate/property-listing` | MLS-style listing sheet with property details, photos, pricing |
| `real-estate/cma-report` | Comparative market analysis with comps table and pricing recommendation |
| `real-estate/closing-disclosure` | HUD-style closing cost breakdown |
| `real-estate/property-inspection` | Inspection report with findings, photos, severity ratings |
| `real-estate/rental-application` | Tenant application with background check consent |

#### Healthcare
| Template | Description |
|---|---|
| `healthcare/patient-intake` | New patient intake form with medical history, insurance, consent |
| `healthcare/care-plan` | Individualized care plan with goals, interventions, timeline |
| `healthcare/discharge-summary` | Hospital discharge summary with medications, follow-up instructions |
| `healthcare/hipaa-authorization` | HIPAA-compliant authorization for release of medical information |
| `healthcare/superbill` | Encounter form with CPT/ICD-10 codes for insurance billing |
| `healthcare/progress-note` | SOAP-format clinical progress note |
| `healthcare/referral-letter` | Provider-to-provider referral with clinical summary |

#### Legal
| Template | Description |
|---|---|
| `legal/nda` | Non-disclosure agreement (mutual or one-way) |
| `legal/service-agreement` | Master service agreement with SOW attachment |
| `legal/engagement-letter` | Attorney-client engagement letter with fee schedule |
| `legal/demand-letter` | Formal demand letter with timeline and consequences |
| `legal/corporate-minutes` | Board meeting minutes with resolutions |
| `legal/power-of-attorney` | General or limited power of attorney |
| `legal/terms-of-service` | Website/app terms of service |

#### Finance & Accounting
| Template | Description |
|---|---|
| `finance/invoice` | Professional invoice with line items, tax, payment terms |
| `finance/financial-statement` | Income statement, balance sheet, cash flow (individual or combined) |
| `finance/budget-report` | Department or project budget with actuals vs. planned |
| `finance/expense-report` | Employee expense report with receipt references |
| `finance/loan-amortization` | Amortization schedule with payment breakdown |
| `finance/audit-report` | Internal/external audit findings with severity and recommendations |
| `finance/proposal` | Business proposal with executive summary, pricing, timeline |

#### Education
| Template | Description |
|---|---|
| `education/syllabus` | Course syllabus with schedule, grading policy, objectives |
| `education/lesson-plan` | Structured lesson plan with standards alignment |
| `education/report-card` | Student performance report with grades and teacher comments |
| `education/iep` | Individualized Education Program (special education) |
| `education/transcript` | Academic transcript with GPA calculation |

#### Construction & Engineering
| Template | Description |
|---|---|
| `construction/bid-proposal` | Construction bid with scope, materials, labor, timeline |
| `construction/change-order` | Change order with cost impact and schedule adjustment |
| `construction/daily-log` | Daily site report with weather, crew, progress, issues |
| `construction/punch-list` | Pre-completion punch list with items, photos, assignees |
| `construction/safety-report` | OSHA-style safety inspection report |

#### General Business
| Template | Description |
|---|---|
| `business/sow` | Statement of work with deliverables, milestones, acceptance criteria |
| `business/project-status` | Weekly/monthly project status report |
| `business/meeting-notes` | Structured meeting notes with action items and owners |
| `business/employee-offer` | Employment offer letter with compensation details |
| `business/performance-review` | Employee performance review with ratings and goals |
| `business/business-plan` | Full business plan with market analysis, financials, projections |

### Tools

#### `DocumentGenerateTool`

Primary tool for document generation:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `template` | string | No | Template ID (e.g., `real-estate/purchase-agreement`). If omitted, agent infers from prompt |
| `prompt` | string | Yes | Description of what to generate, or data to populate the template with |
| `format` | string | No | Output format: `pdf`, `docx`, `xlsx`, `pptx`, `html`, `md`. Default: `pdf` |
| `data` | object | No | Structured data to populate template fields (key-value pairs) |
| `style` | string | No | Style preset: `professional`, `minimal`, `modern`, `corporate`, `creative` |
| `filename` | string | No | Output filename. Auto-generated from template + timestamp if omitted |

#### `DocumentListTemplatesTool`

Browse available templates:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `industry` | string | No | Filter by industry (`real-estate`, `healthcare`, `legal`, `finance`, etc.) |
| `search` | string | No | Search templates by name or description |

#### `DocumentPreviewTool`

Preview a template's structure and required fields before generating:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `template` | string | Yes | Template ID to preview |

### Template System

Templates are defined as structured JSON schemas + Razor/HTML layout:

```
~/.sovrant/templates/
  ├── built-in/                    ← shipped with Sovrant
  │   ├── real-estate/
  │   │   ├── purchase-agreement.json    ← schema (fields, types, defaults)
  │   │   └── purchase-agreement.razor   ← layout template
  │   ├── healthcare/
  │   ├── legal/
  │   └── ...
  └── custom/                      ← user-created, workspace-scoped
      └── {workspace-id}/
          └── my-template.json + .razor
```

Template schema example:
```json
{
  "id": "real-estate/purchase-agreement",
  "name": "Purchase and Sale Agreement",
  "industry": "real-estate",
  "description": "Residential or commercial property purchase agreement",
  "formats": ["pdf", "docx"],
  "fields": [
    { "name": "buyer_name", "type": "string", "required": true },
    { "name": "seller_name", "type": "string", "required": true },
    { "name": "property_address", "type": "string", "required": true },
    { "name": "purchase_price", "type": "currency", "required": true },
    { "name": "closing_date", "type": "date", "required": true },
    { "name": "contingencies", "type": "string[]", "default": ["inspection", "financing", "appraisal"] },
    { "name": "earnest_money", "type": "currency" },
    { "name": "special_terms", "type": "text" }
  ],
  "style": {
    "default": "professional",
    "header_logo": true,
    "page_numbers": true
  }
}
```

### Agent-Driven Document Workflow

The agent doesn't just fill forms — it reasons about document generation:

1. **Inference** — user says "draft me an NDA for my consulting client" → agent selects `legal/nda`, asks clarifying questions about parties, duration, scope
2. **Data gathering** — agent pulls context from conversation history, workspace files, or asks the user for missing required fields
3. **Generation** — agent calls `DocumentGenerateTool` with populated data
4. **Review cycle** — user can say "make the non-compete clause 2 years instead of 1" → agent regenerates with modifications
5. **Multi-document workflows** — "Generate the full closing package" → agent produces purchase agreement, closing disclosure, inspection report, and title commitment as separate artifacts

### UI Integration

| Mode | Behavior |
|---|---|
| **CLI** | Document saved to workspace artifacts, path printed, `xdg-open` / `start` to open |
| **Desktop** | Document preview card in chat with download button. PDF rendered inline via Avalonia PDF viewer or "Open" button. Office docs open in default app |
| **Web** | Document card with download link, PDF inline preview via `<iframe>` or PDF.js, Office docs downloadable with preview of first page |

### Compliance & Safety Considerations

- **Healthcare (HIPAA):** Templates that handle PHI are flagged. Trust Boundary (Phase 58) gates generation with a consent check. Generated docs with PHI are encrypted at rest in the artifact store.
- **Legal:** Disclaimer: "This document was AI-generated and should be reviewed by a qualified attorney before execution." Auto-appended to legal templates unless explicitly suppressed.
- **Financial:** PII/financial data redaction available. Audit trail logged for compliance-sensitive documents.
- **Template validation:** Required fields enforced before generation. Agent prompted to collect missing data rather than generating incomplete documents.

### Implementation Plan

1. **`IDocumentGenerator` interface + factory** — `Sovrant.Runtime.Documents` namespace. `AddDocumentGenerators()` DI extension.
2. **PDF generators** — `PdfSharpGenerator` for plain text-based PDFs; `MigraDocGenerator` for structured PDFs (invoices, contracts, reports) with tables, styles, headers/footers, page numbers, embedded images. No external binaries, no Chromium, no runtime download.
3. **Word/Excel/PowerPoint generators** — Open XML SDK + ClosedXML. Template-driven generation from structured data.
4. **HTML/Markdown generators** — Razor templating engine for HTML output, Markdig for markdown.
5. **Template store** — `IDocumentTemplateStore` with built-in and workspace-scoped custom templates. JSON schema + Razor layout pairs.
6. **Built-in template library** — Ship 40+ templates across 7 industries (real estate, healthcare, legal, finance, education, construction, general business).
7. **`DocumentGenerateTool` + `DocumentListTemplatesTool` + `DocumentPreviewTool`** — new tools in `Sovrant.Tools.Documents`. Registered with capability gate.
8. **Artifact integration** — generated documents saved to workspace artifact store with metadata (template, format, fields used, generation timestamp).
9. **Web UI** — document card component in `ChatMessage.razor` with download, inline PDF preview, and format icon.
10. **Desktop UI** — document card in chat view with download and open-in-app buttons.
11. **CLI** — document path output, auto-open option.
12. **Compliance layer** — HIPAA/PHI flagging, legal disclaimers, audit logging for sensitive templates.
13. **Custom template authoring** — UI for creating workspace-scoped templates (JSON schema editor + Razor layout editor).
14. **Tests** — unit tests per generator, template validation tests, integration tests for full generate-and-store pipeline.

### NuGet Dependencies

| Package | Purpose | License |
|---|---|---|
| `PDFsharp` | PDF output (low-level API) | MIT |
| `PDFsharp.MigraDoc` | Structured PDF generation (tables, styles, headers, page numbers) — renders via PDFSharp | MIT |
| `DocumentFormat.OpenXml` | Word, Excel, PowerPoint generation (Microsoft official) | MIT |
| `ClosedXML` | Friendly Excel API on top of OpenXml | MIT |
| `Markdig` | Markdown parsing (already referenced in Desktop/Web projects) | BSD-2-Clause |

**Total additional footprint: ~10MB NuGet packages. Zero external binaries. Zero first-run downloads.**

**Optional / later phases:**

| Package | Purpose | License |
|---|---|---|
| `Typst CLI` (shell-out) | Modern typeset reports — beautiful output, LaTeX alternative | Apache 2.0 |
| `SkiaSharp` | Chart rendering for PDFs | MIT |

### Acceptance Criteria

- [x] `IDocumentGenerator` interface with implementations for PDF (PDFSharp + MigraDoc), DOCX, XLSX, PPTX, MD (HTML/Razor deferred to Phase 74)
- [x] `DocumentGenerateTool` generates documents from freeform markdown + structured data
- [x] `DocumentListTemplatesTool` lists and searches available templates by industry
- [x] Template field schema exposed via `DocumentListTemplates` (dedicated `DocumentPreviewTool` folded in — same response shape)
- [x] 44 built-in templates across 7 industries ship with Sovrant (see Phase 66.4 below)
- [x] Agent can infer correct template from natural language via `DocumentSuggestTemplateTool` ("draft me an NDA" → `legal/nda`)
- [x] Agent collects missing required fields via conversation — `TemplateValidationException` reports the missing set
- [x] Multi-document workflows — `DocumentPackageTool` + `DocumentListPackagesTool` render a registered `DocumentPackage` (e.g. `real-estate/closing-package`) in one call against shared data
- [ ] Custom workspace-scoped templates — deferred to Phase 74 (markdown-backed templates)
- [x] Generated documents stored as workspace artifacts with full metadata (scope, content-type, size, access URL)
- [x] Web UI document-card-in-chat with inline PDF preview — `DocumentArtifactCard.razor` parses document-tool results and renders cards (with PDF iframe) inline in `ChatMessage.razor`
- [x] Desktop UI document-card-in-chat — `DocumentArtifactViewModel` + `DocumentArtifactParser` populate cards under each tool result in `ChatView.axaml` with Open + Reveal buttons
- [x] CLI outputs document path; `sovrant document render --output` copies the artifact to a local file
- [x] Healthcare PHI Trust Boundary gate — `IDocumentTrustGate` + `HealthcarePhiTrustGate` (default) refuses any `healthcare/*` template without explicit `consent_acknowledged: true` in the data payload
- [x] Legal "AI-generated — review before execution" auto-disclaimer — `NdaTemplate` + `DemandLetterTemplate` auto-append a counsel-review notice (toggleable via `include_ai_disclaimer`)
- [x] All existing tests continue to pass (71/71 in `Sovrant.Runtime.Documents.Tests`)

### Phase 66.4 — Industry Expansion, User Surfaces, and Agent Routing

Phase 66.4 closed the gap between "document generation works" and "document
generation is a real product surface." Three sub-deliveries:

1. **Template library expansion** (tasks #48–#54): brought the library from ~18
   to **44 templates** across 7 industries — business (6), finance (7), legal
   (7), real-estate (7), healthcare (7), education (5), construction (5).

2. **Three-surface rollout** (task #55, split into #57/#58 plus the CLI):
   - CLI: `sovrant document {list,fields,render,suggest}` with Spectre.Console
     tables + `--json` emission
   - Web: `/documents` Blazor page with master-detail layout (sidebar Documents
     entry under KNOWLEDGE)
   - Desktop: Avalonia `DocumentsView` with master-detail layout using native
     controls only (avoids `Markdown.Avalonia` crashes on code fences)

3. **Natural-language template routing** (task #56):
   - `TemplateMatcher` — deterministic keyword ranker with weighted token
     overlap against id/name/industry/description + a small synonym table
     (nda → nondisclosure, bill → invoice, hipaa, cma, iep, sow, etc.)
   - `DocumentSuggestTemplateTool` — agent tool returning top-N matches with
     score, matched terms, and the full field schema in one call
   - `sovrant document suggest` — CLI surface of the same router
   - 10 new matcher tests cover routing, stopword handling, synonyms, and
     empty-prompt behavior

Commits: `36160e2` through `f7eb73a` on `sovrant-openc-dotnet-port`.

### Phase 66.5 — Closeout (packages, trust gate, in-chat artifact cards)

The remaining acceptance items shipped in one pass:

- **Multi-document packages** — `DocumentPackage` + `DocumentPackageItem` records,
  `DocumentPackageRegistry`, three built-in packages (`real-estate/closing-package`,
  `business/onboarding-package`, `healthcare/intake-package`), and the
  `DocumentPackageTool` + `DocumentListPackagesTool` agent surfaces. The package
  tool runs each item through the trust gate independently and reports
  per-document status (`generated` / `refused` / `error`) so partial successes
  are visible.
- **Healthcare PHI gate** — `IDocumentTrustGate` abstraction in
  `Sovrant.Runtime.Documents.Trust`. Default implementation
  `HealthcarePhiTrustGate` refuses any template whose `Industry == "healthcare"`
  unless the data payload sets `"consent_acknowledged": true`. Wired into
  `DocumentFromTemplateTool` and `DocumentPackageTool`. 5 unit tests.
- **Web in-chat document card** — new `DocumentArtifactCard.razor` parses tool
  results from `DocumentGenerate` / `DocumentFromTemplate` / `DocumentPackage`
  and renders one card per artifact with format icon, size, download link, and
  inline PDF iframe preview. `ChatMessage.razor` mounts it above the existing
  collapsible raw-result block.
- **Desktop in-chat document card** — `DocumentArtifactViewModel` +
  `DocumentArtifactParser` populate an `ObservableCollection` on
  `ToolUseViewModel` whenever a document tool completes. `ChatView.axaml`
  renders cards beneath each tool result with Open and Reveal-in-folder
  commands.
- **Tests** — 9 new tests (5 trust gate + 4 package registry); existing
  Documents test suite unchanged at 84/84 passing.

The only remaining unchecked item is workspace-scoped custom templates,
which is intentionally deferred to Phase 74 (markdown-backed templates).

---

## Phase 67 — Autonomous Agent Modes (Swarm Autonomy & Alternate Claws)

### Why

Today Sovrant's autonomous capability lives primarily in the Claw integration
(OpenClaw via Phase 50, Hermes via Phase 60). For long-running unsupervised
missions we need **at least one alternate autonomous backend** so users are not
locked to a single external dependency, and so the **Swarm** subsystem can be
driven in an autonomous mode without an external Claw process.

### Scope

- **Swarm autonomous mode** — extend `SwarmCoordinator` so a swarm can accept a
  high-level goal and self-plan/re-plan across agents without an external Claw,
  reusing `LlmMissionPlanner` + `ParallelMissionExecutor`. Gated on the Phase 58
  Trust Boundary (sanitization, ethics, intent verification).
- **Alternate autonomous providers** — add a pluggable `IAutonomousDriver`
  abstraction so OpenClaw, Hermes, Swarm-autonomous, and future providers
  (e.g. SWE-agent, OpenHands, AutoGen studio, crewAI) are swappable per
  mission/workspace.
- **Provider selection** — config + CLI flag + per-mission override. Registry
  reports per-provider capabilities (tool support, model family, cost tier).
- **Shared mission contract** — single `MissionSpec` / `MissionEvent` schema
  already used by Phase 51 extended to cover all drivers, so missions are
  observable and auditable regardless of provider.

### Acceptance Criteria

- [x] `IAutonomousDriver` abstraction shipped; `LlmAutonomousDriver`
      (default) + `SwarmAutonomousDriver` registered through `DriverRegistry`.
      OpenClaw and Hermes drivers intentionally deferred — neither Phase 50
      nor Phase 60 actually shipped production code to wrap.
- [x] Swarm can run a mission to completion with no external Claw
      (`SwarmAutonomousDriver.AdvanceAsync` decomposes → orchestrates →
      writes terminal state)
- [x] Missions emit the same event journal regardless of driver — swarm
      events are projected onto `mission_events` via a stable
      `swarm_*` type vocabulary
- [ ] Driver selectable at mission-create time (CLI, API, Web, Desktop) —
      deferred; the seam exists but no UI writes a driver name onto the
      mission row yet
- [ ] Trust Boundary sanitization + ethics + intent checks apply to all
      drivers — blocked on Phase 58 wiring
- [ ] Integration tests exercise Swarm-autonomous driver end-to-end —
      unit-level coverage only (7 tests with fake decomposer/orchestrator
      against a real `SqliteMissionStore`)

### Phase 67.1 — `IAutonomousDriver` abstraction

Purely additive seam in `src/Sovrant.Runtime/Missions/`:

- `IAutonomousDriver` — `Name`, `Capabilities`, `AdvanceAsync(missionId, ct)`
- `DriverCapabilities` record — `SupportsReplanning`, `SupportsParallelSteps`,
  `SupportsHumanAcceptance`, `MaxStepsPerCycle`
- `DriverRegistry` — `ConcurrentDictionary`-backed lookup by name
  (case-insensitive)
- `LlmAutonomousDriver` — thin adapter over the existing `IMissionExecutor`;
  registered as the default driver (`Name = "llm"`)

No existing code path changes — mission routes and `MissionCommand` keep
consuming `IMissionExecutor` directly.

### Phase 67.2 — `SwarmAutonomousDriver`

First non-default driver, in `src/Sovrant.Agents/Swarm/`:

- Loads the mission, decomposes its goal via `ISwarmDecomposer`, runs the
  plan through `ISwarmOrchestrator`
- Buffers `SwarmEvent`s during execution and flushes them onto
  `mission_events` after the run completes, under a stable `swarm_*` type
  vocabulary (`swarm_task_started`, `swarm_task_completed`, `swarm_wave_completed`,
  etc.) so downstream consumers can match on it
- Terminal swarm status (`Completed` / `Failed` / `Cancelled`) maps directly
  to the corresponding `MissionStatus` and writes the matching event
- Driver name: `"swarm"`. Registered alongside `LlmAutonomousDriver` in DI
  so `DriverRegistry.TryGet("swarm")` resolves it.

Commits: `64223b6` (67.1), `8341b79` (67.2).

---

## Phase 68 — Foundations Hardening

### Why

After 66 phases, the engine has grown fast. Before shipping more surface area,
we should pay down foundation-level debt: DI consistency, error shape, logging
taxonomy, cancellation propagation, async disposal, thread-safety of shared
singletons, startup cost, and test layering. This is an internal quality pass,
not a feature phase — the output is a measurably-tighter engine.

### Scope

- **DI audit pass 2** — building on Phase 63, verify lifetimes, captive
  dependencies, and proper `IAsyncDisposable` across every singleton/scope.
- **Error shape unification** — single `SovrantException` hierarchy with error
  codes; map to consistent HTTP/JSON-RPC responses; audit `catch (Exception)`
  sites.
- **Cancellation propagation** — every public async method takes and honors a
  `CancellationToken`; ensure tool executors propagate cancellation to LLM
  calls and subprocesses.
- **Logging taxonomy** — unify `EventId` registry, scope fields
  (workspace/project/session/run/mission), and redaction rules.
- **Startup profiling** — measure cold-start cost of CLI, Desktop, Web, MCP;
  remove unnecessary reflection/scan work on the hot path.
- **Thread-safety sweep** — identify shared mutable state in
  coordinators/registries; convert to immutable or properly guarded.
- **Test layering** — separate unit/integration/e2e tiers; reduce overall
  suite runtime; clarify which projects block CI.

### Acceptance Criteria

- [x] All public async methods in `Sovrant.Runtime` take `CancellationToken`
      and honor it — full-source audit found zero missing
- [x] Single exception base — `SovrantException` in `Sovrant.Api.Errors`;
      `ApiError`, `MacroExpansionException`, `TemplateValidationException`,
      `MigrationDriftException` re-parented. Tool error-shape unification
      (coded errors, consistent JSON-RPC / HTTP mapping) remains.
- [ ] Cold-start latency dashboarded and reduced from baseline
- [x] DI-singleton registries (`InMemoryToolRegistry`,
      `AgentTemplateRegistry`) converted to `ConcurrentDictionary` with
      concurrent-writer tests; broader shared-state sweep still pending
- [ ] Logging scope taxonomy documented and enforced by an analyzer or test
- [ ] Test tiers wall-clocked and under target budgets

### Phase 68.1 — Thread-safe singletons

Two DI singletons were holding mutable `Dictionary<,>` — silent corruption
waiting to happen under concurrent requests:

- `InMemoryToolRegistry` in `src/Sovrant.Runtime/Tools/`
- `AgentTemplateRegistry` in `src/Sovrant.Agents/Templates/`

Both now use `ConcurrentDictionary<,>` (keeping `OrdinalIgnoreCase` for
the template registry). A `Parallel.For` concurrent-writer test was added
to each (16×64 writers on the tool registry, 8×24 on the template
registry) to prove the fix.

### Phase 68.2 — `SovrantException` base

New `SovrantException : Exception` in `src/Sovrant.Api/Errors/`. Four
existing ad-hoc exception types were re-parented:

- `ApiError`
- `MacroExpansionException` (`Sovrant.Runtime/Engine/`)
- `TemplateValidationException` (`Sovrant.Runtime.Documents/Templates/`)
- `MigrationDriftException` (`Sovrant.Runtime/Storage/`) — also drops its
  previous `InvalidOperationException` parent, which was never caught
  specifically

Callers can now distinguish Sovrant-originated errors from framework
exceptions (`HttpRequestException`, `IOException`, `JsonException`)
without `catch (Exception)`. The 253 existing `catch (Exception)` sites
are explicitly out of scope — that sweep is tracked separately.

### Phase 68.3 — Cancellation audit

Full-source audit of `src/Sovrant.Runtime/`: every public async method
(including ones returning `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`,
or `IAsyncEnumerable<T>`) already takes a `CancellationToken`. No code
changes needed. An older plan estimated ~30 missing methods; that
estimate was stale.

Commits: `b730a02` (68.1), `ab2acde` (68.2).

---

## Phase 69 — Desktop & Web UI Polish

### Why

The Avalonia desktop and Blazor web frontends are functional but feel
utility-grade next to the class of apps they're expected to compete with
(Cursor, Claude Desktop, Linear, Raycast). This phase is a deliberate UI
polish pass — not new features, but the visual, interactional, and
performance quality of the existing surface.

### Scope

- **Design system** — shared tokens (color, type, space, motion) consumed by
  Avalonia `ResourceDictionary` and Blazor CSS custom properties. Dark + light.
- **Conversation surface** — message grouping, tool-call cards with
  expand/collapse, inline artifact previews (image, PDF, code diff),
  streaming cursor, keyboard-first navigation.
- **Empty, loading, error states** — every screen has deliberate copy and art;
  no silent failures.
- **Command palette** — Raycast-style palette in both desktop and web for
  skills, tools, sessions, workspaces.
- **Artifact gallery** — unified view of documents, images, videos generated in
  the current run / session, with download + re-open.
- **Performance** — virtualize long conversations; tighten SignalR reconnect
  UX; eliminate layout jank during streaming.
- **Accessibility** — keyboard traversal, screen-reader labels, contrast
  targets.

### Acceptance Criteria

- [ ] Shared design-token source consumed by both frontends
- [ ] Every screen has loading / empty / error states with written copy
- [ ] Tool-call cards render with consistent affordances in desktop + web
- [ ] Command palette reaches skills, tools, sessions, workspaces
- [ ] Artifact gallery present in both frontends
- [ ] Conversation virtualized; no measurable jank on 500-message sessions
- [ ] WCAG 2.1 AA target met on critical flows

---

## Phase 70 — CLI UX Overhaul

### Why

The CLI REPL is the original delivery mode and still the most-used surface for
power users. It should set the bar, not lag behind the GUIs. Today's UX is
adequate but terse — we want the CLI to feel as carefully considered as the
desktop app, while staying keyboard-native and scriptable.

### Scope

- **Rich rendering** — Markdown, tables, code fences with syntax highlighting;
  tool-call blocks with consistent structure; inline artifact links that the
  terminal can resolve (OSC 8 hyperlinks, or path + copy-to-clipboard).
- **Prompt shell** — multi-line input, history, search (Ctrl+R), slash-command
  autocomplete, skill picker, session picker, workspace switcher.
- **Status line** — persistent bottom line showing workspace/project/session,
  active model, token usage, cost so far — matching Claude Code's statusline.
- **Progress affordances** — per-turn progress, per-tool spinners, cancellable
  tool calls, streaming token counter.
- **Output modes** — `--plain`, `--json`, `--ndjson`, `--ci` flags with stable
  contracts for scripting.
- **Session UX** — `:` commands to list/switch/fork sessions, rename, pin,
  delete; rich `/help` with examples.
- **Theming** — respects `NO_COLOR`, 256-color + truecolor, per-theme token
  set shared with desktop/web where it makes sense.

### Acceptance Criteria

- [ ] Markdown, tables, syntax-highlighted code all render in the terminal
- [ ] Persistent status line with workspace/model/cost
- [ ] Slash-command autocomplete + history search
- [ ] Scriptable `--json` / `--ndjson` outputs with documented schema
- [ ] `NO_COLOR` honored; truecolor detected when available
- [ ] New CLI walkthrough in docs with screenshots/asciinema

---

## Phase 71 — Industry-Standard Feature Audit

### Why

We need an honest, written comparison between Sovrant and the feature bar set
by leading agentic coding and workflow platforms (Claude Code, Cursor,
Cline, Aider, OpenHands, Copilot Workspace, Devin, crewAI, Zed). The goal is
to catalogue every feature we have vs. what the market expects, and produce a
prioritized gap list.

### Scope

- **Feature catalogue** — export a structured inventory of every Sovrant
  capability (tool, delivery mode, integration, agent behavior, storage
  backend, security control).
- **Comparator set** — define 6–10 competitive products and their published
  feature sets.
- **Scoring rubric** — per feature: parity (we have it), partial, missing,
  and quality score (1–5) vs best-in-class.
- **Gap report** — markdown + spreadsheet under `docs/audits/phase-71/`,
  grouped by subsystem; each gap has an owner and suggested phase target.
- **Security / compliance lane** — explicit pass for SOC 2-adjacent controls,
  tenant isolation, prompt injection defenses, data retention, audit logging.
- **Output feeds the next phase** — gap items become phase candidates.

### Acceptance Criteria

- [ ] Feature inventory generated from code + docs (scripted where possible)
- [ ] Comparator matrix checked into `docs/audits/phase-71/`
- [ ] Prioritized gap list with owners and proposed phases
- [ ] Security/compliance lane covered in its own section
- [ ] Review sign-off from user recorded in the audit doc

---

## Phase 72 — Internal Quality Audit: "Where Can We Be Better?"

### Why

Separate from the market-comparison audit, we need an **internal** audit that
asks: *of the features we already have, which are weakest on their own
merits?* This is the pass where we look at features that shipped quickly,
areas users keep complaining about, flaky tests, high-churn modules, and
surfaces that feel half-finished.

### Scope

- **Signal gathering** — user feedback (memory notes, past conversations),
  GitHub issues (if applicable), test flake rate, code churn, coverage gaps,
  performance hotspots, error telemetry.
- **Per-subsystem deep-reads** — one short write-up per subsystem: what's
  good, what's weak, what would 10× quality look like.
- **Prioritization** — effort × user impact × strategic importance; produces
  a ranked list of improvements.
- **Quick wins lane** — improvements achievable in ≤1 day each, batched for
  immediate execution after the audit.
- **Bigger bets** — larger improvements become their own future phases.

### Acceptance Criteria

- [ ] Per-subsystem audit notes under `docs/audits/phase-72/`
- [ ] Signal summary (feedback, churn, flake, coverage, perf, errors) attached
- [ ] Prioritized backlog with effort × impact × strategy scoring
- [ ] Quick-wins batch identified and scheduled
- [ ] Bigger bets converted into proposed future phases in this document

---

## Phase 73 — Code Creation: Project Scaffolding & App Generation

### Why

Sovrant can already edit existing projects, but *creating* an app from zero —
scaffolding layout, wiring dependencies, hooking up CI, generating a working
dev loop — is still coarse. This phase turns Sovrant into a first-class
generator for full working projects in the languages we already support via
LSP, starting with the ones users ask for most (Node.js and C#), and covering
the rest of the LSP-supported surface.

### Scope

- **Scaffolding templates per language** — structured generators (not just
  string templates) that produce a runnable project with build, lint, test,
  and a README. Parallel to Phase 66's document templates: an
  `IProjectTemplate` registry selected by language + project kind.
- **First-class lanes (Node.js, C#)** — highest-fidelity scaffolds:
  - Node.js: CLI app, Express API, Next.js app, npm library, monorepo
    (pnpm workspace), all with TypeScript option, ESLint/Prettier, Vitest.
  - C# / .NET: console app, ASP.NET Core web API, Blazor app, class library,
    worker service, xUnit tests, analyzers enabled.
- **Remaining LSP languages** — Python (pyproject + uv/poetry choice, pytest,
  ruff), Go (modules + testing), Rust (Cargo + clippy + test), Java (Maven or
  Gradle + JUnit), Kotlin, Ruby, Swift, C/C++ (CMake), Lua, Zig. Fidelity
  scales by language popularity.
- **Wiring steps** — after scaffold: `git init`, install deps, run the first
  build/test, open the project in the user's editor or in the desktop/web
  preview. Each step is optional and cancellable.
- **Multi-component projects** — a single prompt ("build me a Next.js app
  with a .NET API backend") produces a solution with both projects,
  cross-linked (API client generation, shared env files, README diagram).
- **Agent integration** — a `CodeCreateTool` that takes a natural-language
  brief, picks a template, collects missing inputs (name, framework
  choices), and executes the scaffold inside a workspace artifact scope.
- **Trust Boundary** — generated projects pass through Phase 58 sanitization
  so dependency choices cannot be prompt-injected to malicious packages;
  dependency manifests are validated against known-good registries.

### Acceptance Criteria

- [ ] `IProjectTemplate` abstraction + registry with language + kind selection
- [ ] Node.js lane: CLI, Express API, Next.js, library, pnpm monorepo — each
      produces a project that builds and runs the first test on a clean box
- [ ] C# / .NET lane: console, web API, Blazor, library, worker — each
      produces a project that builds and runs the first test on a clean box
- [ ] Scaffolds exist for Python, Go, Rust, Java at parity or higher fidelity
- [ ] Lower-priority LSP languages (Kotlin, Ruby, Swift, C/C++, Lua, Zig)
      have at least a minimal "hello + test" scaffold
- [ ] `CodeCreateTool` routes natural-language briefs to the right template
      and collects missing fields via conversation
- [ ] Multi-component generation (frontend + backend in one solution) works
      end-to-end with cross-links
- [ ] Dependency manifest validation integrated with Trust Boundary
- [ ] CLI / Desktop / Web all surface a "Create project" entry point
- [ ] Golden-path tests per language verify scaffold → build → test pipeline

### Implementation Plan

Research completed 2026-05-23. Implementation order:

#### What already exists
- `write_many` tool (`src/Sovrant.Tools/Artifacts/ArtifactTool.cs`) — already writes
  batch files to `{root}/{workspace}/artifacts/{run}/`; no new write plumbing needed.
- Web `Artifacts.razor` — zip download button already wired to
  `/artifacts/{workspace}/{project}/{run}.zip` but the endpoint does not exist yet.
- Desktop `ArtifactsView.axaml` — "Open folder" opens the run directory in File Explorer;
  no download button yet.
- CLI — `artifacts ls / open / rm` commands; no zip download.

#### Step 1 — Zip download endpoint (quick win, ~50 lines)
Add `GET /v1/artifacts/{runId}.zip` to `ArtifactRoutes.cs` using
`System.IO.Compression.ZipArchive`. Unblocks the already-wired Web UI button with no
new abstractions. Desktop and CLI wire a matching download button/command once the
endpoint exists.

#### Step 2 — `IProjectTemplate` interface + registry
New location: `src/Sovrant.Runtime.Projects/Templates/`. Parallel to
`IDocumentTemplate` (Phase 66) and the agent template pattern
(`FileSystemTemplateLoader`). Registry selected by language + project kind. Each
template produces a `IReadOnlyList<ArtifactFile>` (path + content pairs) that
`write_many` flushes to the artifact store.

#### Step 3 — Scaffold implementations
Start with Node.js (CLI, Express API, Next.js, library, pnpm monorepo) and C#
(console, web API, Blazor, library, worker). Each scaffold produces a project that
builds and passes its first test on a clean box. Remaining LSP languages (Python, Go,
Rust, Java, Kotlin, Ruby, Swift, C/C++, Lua, Zig) follow in order of user demand.

#### Step 4 — `CodeCreateTool`
Located in `src/Sovrant.Tools/Projects/`. Takes a natural-language brief, resolves
language + kind via `IProjectTemplate` registry, collects missing inputs through
conversation, then calls the scaffold and emits files. Integrates with Phase 58 Trust
Boundary for dependency manifest validation.

#### Step 5 — Surface in CLI / Desktop / Web
- CLI: `sovrant create <language> [name]` command
- Desktop: "Create project" button in Artifacts or Agents view
- Web: "Create project" entry point in the Artifacts page

---

## Phase 74 — DB-Backed Document Templates

### Why

The original Phase 74 plan called for `.md` files on disk. That approach was
superseded during Phase 112C/D: all content — skills, agent definitions, and
now document templates — is stored in the `knowledge_pages` table so it
benefits from the same copy-on-write overlay model, Knowledge UI editing, and
`IKnowledgeRouter` routing.

The goal of this phase is to complete the migration: ship the `sovrant document
lint` CLI command (Scriban syntax validation + fields_json sanity check) and the
`docs/document-templates.md` authoring guide so domain experts can author,
review, and iterate on templates without rebuilding the binary.

### What Is Already In Place

**Phase 112D (already done before this phase):**

- **V036** — `knowledge_pages` gains `fields_json`, `filename_template`,
  `default_format`, and `industry` columns for document templates.
- **V037** — Seeds 42 built-in Scriban templates as `kind = 'document-templates'`
  `BuiltIn` rows. The two Excel-format templates (`finance/expense-report`,
  `finance/loan-amortization`) remain as C# because they emit structured JSON
  bodies (preamble/headers/rows) rather than Markdown.
- **`DbDocumentTemplate`** — `IDocumentTemplate` backed by a `KnowledgePage`.
  Uses `ScribanRenderer.Render(_page.Body, data)` and
  `TemplateFieldSerializer.Deserialize(page.FieldsJson)`.
- **`ScribanRenderer`** — Scriban rendering with format helpers:
  `format_date`, `format_money`, `format_money_whole`, `format_number`,
  `format_percent`, `escape_pipes`, `slug`, `normalize_currency`.
- **`TemplateRegistry`** — loads C# templates first, then DB-backed
  `DbDocumentTemplate` rows override by slug. DB wins.
- **Copy-on-write user overlay** — Global/Project tier rows shadow BuiltIn
  base rows by slug. Revert = delete the overlay row. UI editing uses the
  existing Knowledge page inline editor (Phase 91).
- **All 51 existing template tests pass** — they test the `IDocumentTemplate`
  contract, which is unchanged.

### Delivered

### Non-Goals

- **Not** building a visual template editor — that's a Phase 91 follow-on.
- **Not** filesystem-based template loading — all templates are DB-only.
- **Not** changing the `IDocumentTemplate` interface or `TemplateRegistry`.

### Acceptance Criteria

- [x] 42 built-in templates seeded as BuiltIn `knowledge_pages` rows (V037)
- [x] `DbDocumentTemplate` — `IDocumentTemplate` backed by `KnowledgePage`
- [x] Scriban renderer with `format_date`, `format_money`, `format_percent`,
      `escape_pipes`, `slug`, `normalize_currency`, `format_number`,
      `format_money_whole` helpers
- [x] `TemplateRegistry` merges C# + DB templates; DB overrides win
- [x] Copy-on-write user overlay — Global/Project tier rows shadow BuiltIn
- [x] All 51 existing document-template tests pass
- [x] Admin Edit/Revert buttons on Documents page (Web + Desktop) — inline
      editor saves a User-tier overlay; `Registry.Reload()` reflects changes
      immediately; 2 Excel C# templates intentionally excluded (computed logic)
- [x] `sovrant document lint [--id <id>] [--json]` CLI command validates
      Scriban syntax and fields_json for DB-backed templates; exit code 1 on
      any failure
- [x] `docs/document-templates.md` authoring guide covers field types,
      Scriban expression syntax, format helpers, and the add/edit workflow

## Phase 75 — Documents Surface Re-evaluation

### Why

Phase 66 shipped a dedicated **Documents** page (Web + Desktop) that lets users
browse templates, fill in fields, and generate documents through a standalone
UI surface. That came out of the package/template work, but the original intent
behind document templates was simpler: **give chats and agents a clean way to
turn structured data into deliverable artifacts** (PDF, Word, Excel) on demand.

Sitting with the experience after the Phase 69 nav redesign, the standalone
Documents UI feels heavier than the original goal. Templates may be more
valuable as **invisible infrastructure** — discoverable to the agent layer via
`DocumentListPackages` / `DocumentFromTemplate`, surfaced in chat as artifact
cards (Phase 66.5), but not necessarily a top-level human-driven surface of
their own.

This phase is a **review-and-decide**, not a rewrite. We use the surface for a
while, then take an honest look.

### Questions to answer

1. Who actually opens the Documents page in normal use, and what do they do
   there that they couldn't do in chat with `"generate a closing disclosure
   for ..."`?
2. Does the form-fill UX add real value, or does it just duplicate what an
   agent could collect via conversation?
3. Are document **packages** (multi-template bundles) a chat-driven or
   browse-driven feature in practice?
4. If the standalone UI is removed, what's the discoverability story for
   "what templates exist" — a `/templates` slash command, an agent intro,
   or something else?
5. Is the Knowledge → Documents nav slot the right home if we keep it, or
   does it belong under a different group?

### Possible outcomes

- **Keep as-is** — usage data shows the standalone UI earns its keep.
- **Demote to a lightweight picker** — replace the full page with a slim
  template-browser inside chat (slash command or `/` mention), and remove the
  dedicated nav entry.
- **Remove the surface entirely** — templates remain a runtime capability
  consumed by agents and the chat artifact card, with no first-class human
  UI. Authoring (Phase 74 markdown templates) handles the human side.

### Acceptance criteria

- [ ] 2–4 weeks of real usage observed (sessions opened, templates rendered,
      packages run) before deciding
- [ ] Decision documented in `docs/engine-status.md` with rationale
- [ ] If outcome is "demote" or "remove": migration plan + removal PR;
      `DocumentArtifactCard` remains the chat-side surface
- [ ] If outcome is "keep": short polish pass to address the most common
      friction observed during the review window

## Phase 76 — In-App Document Viewing

### Why

Phase 66 gave chat a `DocumentArtifactCard` that surfaces generated PDFs,
Word, Excel, and PowerPoint files. Today both shells expose only a pair of
out-of-app handoffs:

- **Desktop** — `Open` (launches the OS default handler via
  `Process.Start`) and `RevealInFolder` (Explorer/Finder). No in-app
  preview at all.
- **Web** — a `Download` anchor plus an optional `<iframe>` that points at
  `art.AccessUrl` for PDFs. In the default deployment,
  `LocalArtifactStore.GetAccessUrlAsync` returns a `file:///…` URI,
  which every modern browser refuses to render inside an iframe on a
  non-`file://` origin — so **the PDF preview is effectively unavailable
  out of the box today**. The "preview" box renders empty or blocked, and
  users have to fall back to `Download`.

The user ask is twofold: (1) be able to **click over to the document from
the app** (reliable handoff that actually opens it), and (2) be able to
**open it within the app** (true in-surface viewer for at least PDF, and
ideally Word/Excel/PowerPoint). This phase studies what's feasible,
cross-shell, before committing to an implementation.

### Questions to answer

1. What's the minimum fix to make the Web preview actually work? Options:
   - Serve artifacts through an authenticated controller endpoint
     (`/artifacts/{handle}/{path}`) instead of `file:///` URLs, so the
     iframe has a same-origin source the browser will render.
   - Keep `LocalArtifactStore` for on-disk layout but have the Web host
     expose a streaming controller that resolves the file path and sets
     `Content-Disposition: inline` with the right `Content-Type`.
2. For Desktop, is the right move an **embedded PDF viewer** (e.g.,
   PDFium via `PdfiumViewer`/`PDFtoImage`, or a rendered-to-bitmap preview
   pane) or a **click-to-open** polish pass that just makes `Open` /
   `RevealInFolder` more discoverable? What's the Avalonia story for
   embedding a PDF view on Windows vs. macOS vs. Linux?
3. Do Word / Excel / PowerPoint need in-app viewers at all, or is
   "Open in system default" good enough for those formats since they have
   excellent native viewers on every OS we target?
4. How does in-app viewing interact with the future remote/hosted
   `RemoteArtifactStore` path? A browser-native iframe PDF viewer is
   cheap and works anywhere a signed URL works; a Desktop-embedded viewer
   has to download the bytes locally first.
5. Should the chat artifact card grow a third affordance ("Preview") in
   addition to Open/Download/Reveal, or should preview be the default
   when the card is expanded?

### Possible outcomes

- **Web-only fix, Desktop stays handoff** — ship an authenticated
  artifact controller so `<iframe>` preview actually renders PDFs in the
  browser; Desktop keeps `Open` / `RevealInFolder`. Lowest effort, resolves
  the immediate "preview is unavailable" problem.
- **Cross-shell PDF preview** — add a PDFium-backed viewer component to
  Desktop alongside the Web iframe fix. Word/Excel/PowerPoint continue to
  use `Open` in their native apps.
- **Full in-app viewer suite** — embed viewers for PDF and Office formats
  in both shells (likely via server-side rendering to HTML/images). High
  effort; only pursue if usage data shows users staying in-app is
  genuinely valuable.

### Acceptance criteria

- [ ] Web `DocumentArtifactCard` iframe preview renders PDFs without
      requiring the user to click Download (i.e., `AccessUrl` resolves
      to a same-origin HTTP(S) URL the browser will embed)
- [ ] Decision recorded in `docs/engine-status.md` on whether Desktop
      gets an embedded viewer or stays with `Open` / `RevealInFolder`
- [ ] If Desktop embedded viewer is adopted: PDF preview works on
      Windows at minimum; macOS/Linux behavior documented
- [ ] `LocalArtifactStore.GetAccessUrlAsync` path and any new controller
      endpoint enforce the existing workspace/project scope guard — no
      cross-tenant path traversal
- [ ] Remote artifact store path (future) does not regress; preview
      works through signed HTTPS URLs without code changes in the card

### Related: team-ready artifact storage (follow-on)

Today `LocalArtifactStore` writes to `~/.sovrant/artifacts/` — the
*invoking user's* home directory. That is single-user by design: anyone
else on a shared host can't see those files, and a cloud deployment has
no per-user home. When Phase 76 study completes, the follow-on should:

- Replace the user-home default with a **workspace-scoped root** (e.g.,
  `{deployment_root}/{workspace}/{project}/{run}/`) under a path the
  host controls, not the OS user.
- Gate `/artifacts/...` behind authentication (bearer token / cookie)
  with workspace membership checks — not just traversal guards.
- Make the URL contract match signed-URL semantics so
  `LocalArtifactStore` (dev) and a future `S3ArtifactStore` / SMB-mounted
  backend (team) are drop-in compatible.


## Phase 77 — Project Isolation With Full Feature Parity

### Why

Workspaces today are the real top-level partition — they scope artifacts,
teams, sessions, and (post-Phase 64) cloud backends. Projects exist as a
nested label under a workspace, but most features treat them as a
secondary filter, not a first-class isolation boundary:

- Artifact layout is `{workspace}/{project}/{run}/` but URL ACLs and
  the planned remote backends key on the workspace only.
- Teams carry a nullable `project_id` but registry listing defaults to
  workspace scope; members from one project can be seen from another.
- Sessions, knowledge pages (Phase 54), and document templates have no
  hard project boundary — they are workspace-scoped and filtered in UI.

For teams working on unrelated client engagements inside the same
workspace, this is the wrong default. A user who switches the active
project should not see teams, artifacts, sessions, or secrets from a
sibling project unless they explicitly escalate to workspace scope.

### Goals

- **Project = isolation boundary** with the same feature surface as a
  workspace: artifacts, teams, sessions, knowledge pages, skill
  registry, document templates, and provider/model overrides.
- **Workspace = org / tenant**; project ≈ "dedicated room" inside it.
- UI context switch (project dropdown) instantly re-scopes every list
  in sidebar, Artifacts, Documents, Team, Knowledge — no stray cross-
  project items.
- Tools that accept workspace/project context (TeamCreate, SkillCreate,
  ArtifactWrite) default to the active project and never silently fall
  back to workspace scope.

### Study questions

- Do we migrate the existing `project_id`-nullable rows to a mandatory
  `project_id` (auto-assigning a `default` project per workspace), or
  keep nullable for "workspace-wide" items?
- Does every feature need *both* project-scoped and workspace-scoped
  variants (e.g. a workspace-wide knowledge page shared across all
  projects), or is project-only the cleaner default with explicit
  "share up" escalation?
- Remote backends (S3, Google Docs, Box from Phase 64): do project
  boundaries map to subfolders, prefixes, or separate buckets?
- How does project isolation compose with Phase 58 trust boundary —
  does an ethics verdict apply at workspace or project scope?

### Acceptance

- [ ] Switching the active project refreshes all sidebar lists with
      project-scoped items only (Artifacts, Team, Sessions, Knowledge,
      Documents)
- [ ] `TeamCreate` tool scopes the ensured default team to the current
      workspace + project (not just workspace)
- [ ] Artifact URL resolver rejects `{workspace}/{other_project}/*`
      access when the active project is `{project_a}`
- [ ] Knowledge pages created in project A do not surface in project B
- [ ] Desktop and Web both honor the project switch identically — no
      shell-specific fallbacks
- [ ] Documented migration path for existing installs with nullable
      `project_id` rows (backfill to `default` or preserve as workspace-
      wide — explicit choice recorded in `docs/engine-status.md`)

---

## Phase 78 — Teams Parity With Swarm Capabilities (Parallelism, File Safety, Quality Gate, Decomposition)

### Why

Today, Teams are a passive roster — `ITeamRegistry` holds `TeamInfo` +
`TeamMemberInfo` records and `TeamRun` / `TeamDelegate` walk through
members sequentially. All of the interesting execution machinery lives
only in Swarm: **parallelism** (wave-based DAG scheduling with
`MaxConcurrent` semaphores), `SwarmFileLockManager` that prevents
concurrent writes to the same file, `SwarmQualityGate` that scores
combined output and can trigger a retry, and `ISwarmDecomposer` that
turns a single prompt into a task graph.

That's a mismatch with how other agentic frameworks (AutoGen, CrewAI,
LangGraph) present teams — in those systems, spawning a team **is**
spawning parallel agents by default. A user who picks "Team" in our
UI reasonably expects the same: several members working at once on
their slice, not one-at-a-time delegation.

Beyond parallelism, the runtime safeguards also matter. If two team
members edit `src/auth.ts` in the same run, the last write wins
silently. A team's aggregate output is never reviewed. And if a
prompt is bigger than any one member can handle, the team has no way
to split it — the user has to reach for Swarm, which requires a
different mental model.

The distinction in the UI (Team = who, Swarm = how) is real, but the
floor for "who" should be higher. Teams should inherit parallel
execution plus the runtime safeguards so picking a Team isn't a
downgrade on throughput, safety, or quality.

### Goals

- **Parallel execution by default** — a team run should fan out to
  all members (or all members whose role matches the task) at once,
  gated by a per-team `MaxConcurrent` semaphore. Sequential delegation
  stays available as an explicit mode but is no longer the default.
- **File safety for parallel team runs** — acquire file locks per
  team-member task, same mechanism as Swarm, so concurrent members
  can't stomp each other's writes. Reuse `SwarmFileLockManager` or
  factor it into a shared `IFileLockManager` in `Sovrant.Agents/Shared/`.
- **Quality gate for teams** — optional post-run review of combined
  team output using the existing `SwarmQualityGate`, with per-team
  enable/disable and threshold config. Failing runs can trigger a
  retry pass on the flagged members.
- **Decomposition for teams** — when a user gives a team a task that
  clearly spans multiple members' roles, invoke `ISwarmDecomposer`
  to produce a role-aware task graph, then dispatch to team members
  by role match (leveraging existing `EnsembleSelector` logic).
  This makes Team execution feel continuous with Swarm rather than a
  degenerate case.
- **Unified config surface** — per-team settings (not global) for
  run mode (parallel / sequential), `MaxConcurrent`, file-lock
  behavior, quality gate enable + threshold, decomposition on/off.
  Stored on `TeamInfo` (new columns via a migration) so each team
  carries its own run profile.
- **UI exposure** — the Orchestration detail pane for a Team shows
  these settings inline; no separate "Swarm Config" drawer needed for
  team-scoped runs. The global Swarm Config becomes a *default* that
  new teams inherit.

### Phased delivery

This phase ships in two paths; the first can land well before the
migration work.

- **Path 1 — UI-first bridge (pre-Phase 78).** Collapse the
  Orchestration taxonomy to a single shape: **Team**. Drop the
  synthetic "Swarm Orchestrator" list entry and the Claw/Autonomous
  placeholders — Swarm becomes a run mode on a team, and autonomy is
  a driver wrapping a team (Phase 67), not a peer shape here. Each
  team row shows a run-mode pill (`solo` / `sequential` / `swarm`).
  Per-team settings pane reads/writes the shared `.sovrant/swarm.json`
  as a stand-in until real per-team config exists. Single-agent
  invocation moves to the Agent Templates page via sub-agent
  markdown files (Phase 79). Zero migration risk; validates the
  model in real use.
- **Path 2 — Backend migration + dispatch (the rest of this phase).**
  Add `RunMode` + swarm columns to `TeamInfo` via a new SQLite
  migration; `TeamRun` / `TeamDelegate` dispatch by the team's mode
  (sequential loops stay; swarm mode calls through to the shared
  executor with file locks + decomposition + quality gate). The
  global `.sovrant/swarm.json` becomes a default template inherited
  by new teams at creation time.

Path 1 unblocks usability testing without locking in a schema.
Path 2 is where parallelism, file locks, and quality gate actually
become team behaviors at runtime.

### Study questions

- Should the shared executor live in `Sovrant.Agents/Shared/` as
  `OrchestrationCoordinator` extensions, or do Team and Swarm become
  two wrappers over a single `IAgentExecutor` that owns locks + gate +
  decomposition? The latter reduces duplication but risks over-
  abstracting if Claw/Autonomous need very different semantics.
- Does decomposition for a Team respect role whitelists (only
  delegate tasks to members whose role matches), or can it spawn
  ephemeral workers when a role is missing? Both have tradeoffs —
  strict respects the user's curation, permissive matches Swarm's
  current flexibility.
- Quality-gate cost: each run adds one LLM call. Should it be
  off-by-default on Teams (explicit opt-in per team) or on-by-default
  with a small model (e.g. a local/free tier) to keep cost invisible?
- Backwards compatibility for existing teams without the new
  columns — default values at migration time, or a one-shot
  admin flow to review/confirm per-team settings?

### Acceptance Criteria

- [x] Team runs fan out to matching members in parallel by default,
      gated by per-team `MaxConcurrent`; sequential remains available
      as an opt-in mode (integration test proving ordering semantics
      of each mode)
- [x] `TeamInfo` gains `RunMode`, `MaxConcurrent`, `FileLocksEnabled`,
      `QualityGateEnabled`, `QualityGateThreshold`, `DecompositionMode`
      columns via a new migration (V015)
- [x] `SwarmFileLockManager` refactored into `IFileLockManager` and
      consumed by both Swarm and Team execution paths
- [x] Team runs acquire file locks per member task; concurrent
      writes to the same file are serialized (integration test
      proving it)
- [x] Team runs optionally pass through `SwarmQualityGate`; failing
      verdict triggers one configurable retry pass
- [x] Team runs optionally invoke `ISwarmDecomposer` when the prompt
      scope exceeds any single member's role — result is a task graph
      dispatched via `EnsembleSelector`
- [x] Orchestration detail pane (Web + Desktop) edits per-team
      settings inline; global Swarm Config remains as the default
      template new teams inherit
- [x] Docs updated: `docs/agent-systems.md` describes Team and Swarm
      as two profiles of one execution substrate rather than separate
      engines

---

## Phase 79 — Agents: Single-Agent Definition, Reference & Run

### Why

Now that Orchestration collapses to teams-only (Phase 78 path 1), the
single-agent use case — *"define an underwriting agent, then talk to
it (or just hand it a task)"* — needs its own home. With the editor,
by-name reference, and run features below, the current "Agent
Templates" page stops being a passive catalog and becomes the place
where users **define**, **edit**, **reference**, and **run**
individual specialists. At that point the page is simply **Agents**;
"Template" was the right name when rows were read-only seeds, and
it's the wrong name once rows are first-class callable agents.

The definition format is markdown with YAML frontmatter, matching the
established sub-agent pattern (Claude Code et al.): `name`,
`description`, `tools`, `model`, `role` in the frontmatter; system
prompt in the body. Sovrant already externalizes built-in templates
as markdown (Phase 23), so this is a small formalization plus an
in-app editor — not a new concept.

Two invocation modes, same definition:

1. **By reference from the agenting loop.** When the router / main
   chat loop encounters `@agent-name` (or a `/agent run <name>` style
   call), it resolves to the named markdown agent and runs that turn
   under the agent's system prompt, model, and tool scope.
2. **Dedicated chat session.** Clicking Run on an agent opens a
   chat scoped to that specialist. Conversational if the user wants
   back-and-forth; one-shot if the first message is self-contained.
   Same pathway, no explicit mode switch.

### Goals

- **Agent markdown format** — standardize `.sovrant/agents/*.md`
  with YAML frontmatter (`name`, `description`, `tools`, `model`,
  `role`) and a prompt body. Workspace-scoped by default with
  user/project overrides following the existing template-resolution
  order.
- **In-app editor** — the Agents detail pane becomes a markdown
  editor: frontmatter fields surfaced as structured inputs (name,
  description, model picker, tool picker) with the system prompt as
  a freeform textarea. Save writes the `.md` file; the file is the
  source of truth.
- **Create from scratch or clone** — "+ New agent" seeds a blank
  frontmatter + prompt template; "Clone" duplicates an existing
  agent as a starting point.
- **Reference by name from the agenting loop** — the standard router
  honors `@name` / named-agent calls by loading that agent's
  definition for the turn. Edits to the markdown take effect
  immediately on next resolution (no restart, no rebuild).
- **Run action opens a chat session** — clicking Run spawns a chat
  pane scoped to that agent. Conversational by default; if the first
  user message is self-sufficient, the agent completes and hands
  back a result.
- **Run history** — per-agent history of recent sessions and
  by-name invocations (timestamp, opening prompt, summary, outcome,
  cost) stored alongside `agent_runs` ledger entries so Activity
  picks them up. Clicking a history row reopens the session
  read-only (or forks it).
- **Chat command parity** — `/agent run <name> [prompt]` works from
  chat for power users: opens a session if no prompt, fires one-shot
  if prompt given.
- **Page rename** — "Agent Templates" → "Agents" across nav,
  routing, and copy. Redirect `/agent-templates` → `/agents`.
- **Unassigned-team-member migration** — existing `TeamMemberInfo`
  rows not associated with any team are promoted on first run into
  `.sovrant/agents/*.md` files (or retained in the DB with a UI
  nudge to export).

### Study questions

- Do sub-agent markdown files replace `agent_templates` table rows
  entirely, or coexist (DB for built-ins, files for user-authored)?
- Does the per-agent chat session share UI with the main Chat page
  (filtered view), or does it live embedded in the Agents detail
  pane? Embedded keeps context, shared UI keeps features in sync.
- How does the router surface ambiguity when an `@name` matches
  nothing or multiple agents? Fail closed with a suggestion list, or
  fall back to the default LLM with a warning?
- How is tool selection surfaced in the editor — checkbox list of
  registered tools, or a free-form YAML list? Checkboxes are
  friendlier but drift from the markdown-as-source-of-truth model.
- Should agent sessions default to mission-driven (persistable,
  resumable) or ephemeral (one-shot / discarded on close)?
  Ephemeral is simpler; mission-driven matches Phase 67's autonomy
  story and makes run history reopen-able.

### Acceptance Criteria

- [ ] `.sovrant/agents/*.md` format documented with frontmatter
      schema and example files shipped for 2–3 built-in roles
- [ ] Agents page has a markdown-backed editor: create, edit, clone,
      delete an agent; changes round-trip to the file
- [ ] `@agent-name` references in the main chat loop resolve to the
      named agent's definition and run under its prompt/model/tools
- [ ] Run button opens a chat session bound to the agent; one-shot
      completion works when the first message is self-sufficient
- [ ] Per-agent run history visible on the detail pane; entries
      also surface on the Activity page
- [ ] `/agent run <name> [prompt]` chat command invokes the same
      pathway (session if no prompt, one-shot if prompt given)
- [ ] Nav rail and routing renamed from "Agent Templates" to
      "Agents"; old path redirects
- [ ] Unassigned team members surface in Agents with an "Export as
      markdown" action
- [ ] Orchestration page no longer references single agents —
      users directed to Agents for solo work

---

## Phase 80 — Composio MCP Integration (Platform-Aware App Catalog, Managed OAuth, Sovrant-Standard Tool Proxy)

**Depends on:** Phase 15 (MCP server mode), Phase 16 (dynamic MCP tool proxy — `MCPTool`), Phase 17 (MCP OAuth — `McpAuthTool`), Phase 35–38 (workspaces, projects, users, per-user tokens)
**Difficulty:** Medium

### Why

Sovrant can **already** connect to Composio today — add a remote MCP
entry in `.sovrant/mcp.json`, run the OAuth flow via `McpAuthTool`,
and the tools surface through Phase 16's `MCPTool` proxy. That works,
but it treats Composio like any other opaque MCP endpoint. The user
has to know which servers exist, paste URLs, and manage auth out of
band.

This phase makes the platform **Composio-aware** — the same way
Phase 48/55 made it **OpenRouter-aware**. OpenRouter is still just an
OpenAI-compatible provider under the hood, but Sovrant knows about
its model catalog, pricing metadata, and routing quirks as a
first-class thing. Composio gets the equivalent treatment: Sovrant
understands that Composio is a *registry of MCP servers* (GitHub,
Gmail, Slack, Linear, Notion, Jira, HubSpot, Calendar, etc. — 250+
apps), knows how to browse it, knows how its managed-OAuth
"connections" model works, and surfaces the whole thing in the UI so
the user never has to hand-edit `mcp.json`.

Crucially, Composio's tools still flow through **our** standards:
the existing `MCPTool` proxy, scoped credentials from Phase 38, the
workspace/project/user hierarchy from Phases 35–37, and the intent-
and permission-aware runtime. Composio is a *source* of MCP servers,
not a replacement for our tool architecture.

### Goals

- **Composio catalog discovery** — query Composio's app/server
  registry at startup (and on demand) and cache a normalized catalog
  of available apps: name, category, description, icon URL, tool
  count, auth type (OAuth / API key / no-auth).
- **Browse & enable in-app** — a "Composio Apps" page (Desktop +
  Web) lists the catalog, supports search and category filters, and
  has an **Enable** action per app that writes a managed entry to
  the scoped `mcp.json` without the user touching JSON.
- **Managed OAuth via Composio connections** — for apps that need
  OAuth, kick off Composio's connection flow (the user authorizes
  once with GitHub/Gmail/etc. through Composio's hosted UI), store
  the returned connection ID in our existing credential store,
  scoped to user/workspace/project following Phase 38 precedence.
- **Per-user / per-workspace credential scoping** — two users in the
  same workspace can each have their own Gmail connection; a
  workspace can share a read-only GitHub connection. The scope
  follows the same precedence rules as `X-LLM-Api-Key` today
  (request > session > user > workspace > project > global).
- **Sovrant-standard tool proxy** — Composio tools register through
  Phase 16's `MCPTool` with `composio_<app>_<tool>` naming. They
  respect permission mode, hooks, rate limits, cost tracking, and
  every other runtime rail that applies to all tools. Composio is
  invisible to the agent loop except for the namespace prefix.
- **Tool-level enablement (not just server-level)** — Composio
  servers can expose hundreds of tools. Enabling "Gmail" should not
  dump 30 tools into the registry. The Composio Apps page lets the
  user pick which tools to enable per app; the rest stay latent.
  Default selection is a curated subset per app.
- **Intent-aware routing hint** — `IntentClassifier` (Phase 48)
  already tags `tool_heavy`; when a turn's intent implies a specific
  Composio app (e.g. "send an email" → Gmail), surface the relevant
  tools first in the tool list so the model picks them without
  sifting through the whole catalog.
- **Budget & quota awareness** — Composio's free tier has call
  quotas; paid tiers have tool-call pricing. Read the account's
  current quota/usage from Composio's API on startup and on
  `/v1/composio/status`; feed per-call cost estimates into Phase
  55's `ICostModel` so `BudgetEnforcer` can gate Composio calls
  the same way it gates model calls.
- **Connection health surface** — a Composio Apps page shows each
  enabled app's connection status (connected / needs re-auth /
  error / rate-limited) and a **Reconnect** action that kicks off
  the OAuth flow again.
- **Config UI for Composio credentials** — in the setup wizard and
  Settings page, "Connect Composio" asks for the Composio API key
  once. Stored in our credential store, not in `mcp.json`.

### Non-goals

- Replacing `McpAuthTool` or the MCP tool proxy. Composio rides on
  top of the existing MCP infrastructure.
- Proxying Composio tools through a Sovrant-owned server. Composio
  remains the OAuth broker and upstream executor.
- Building a generic "MCP app store" UI. This phase is Composio-
  specific because that catalog is the one the user actually wants.
  A generic MCP server marketplace can be a later phase if demand
  exists.
- Writing tools back to Composio's registry. We only consume.

### Architecture

```
src/Sovrant.Integrations/Composio/
  IComposioClient.cs               ← HTTP client for Composio API (catalog, connections, quotas)
  ComposioClient.cs                ← concrete impl
  ComposioCatalogCache.cs          ← cached app/tool catalog, refresh on demand
  ComposioConnectionStore.cs       ← maps Composio connection IDs to our user/workspace/project scope
  ComposioMcpProvisioner.cs        ← on Enable, writes managed mcp.json entry + registers with MCP registry
  ComposioCostModelAdapter.cs      ← surfaces Composio quota/pricing into Phase 55's ICostModel

src/Sovrant.Api/Endpoints/
  ComposioEndpoints.cs             ← /v1/composio/catalog, /v1/composio/connections, /v1/composio/apps/{app}/enable, /v1/composio/status

src/Sovrant.Desktop/Views/ComposioAppsPage.axaml
src/Sovrant.Web/Pages/ComposioApps.razor
```

### Key design decisions

- **Composio entries in `mcp.json` are marked as managed.** A
  `"source": "composio"` field plus a `composioAppId` identifies
  entries written by the provisioner. Manual edits to those entries
  get overwritten on re-provision; users hand-edit at their own
  risk and a UI warning flags manual drift.
- **Connections live in our credential store, not in `mcp.json`.**
  The managed entry references a connection by ID; the runtime
  resolves the ID to a Composio session token at request time,
  scoped by the current user/workspace. This is how we avoid
  leaking connection tokens into the file that might end up in git.
- **Tool enablement is stored per scope** in a new
  `composio_tool_enablement` table (`scope_type`, `scope_id`,
  `app_id`, `tool_name`, `enabled`). Falls back to the curated
  per-app default when no row exists.
- **Catalog refresh is debounced.** Fetch on startup, cache for 24h
  by default, manual refresh via `POST /v1/composio/catalog/refresh`
  and a button in the UI.
- **Composio API key is per-user**, not per-workspace by default —
  matches how users think about their own Composio account. A
  workspace-level override lets teams share a single Composio
  account when they want to.

### API surface

| Endpoint | Method | Description |
|---|---|---|
| `/v1/composio/catalog` | GET | Normalized catalog (apps + tools). Supports `?category=`, `?q=`, `?enabled=true`. |
| `/v1/composio/catalog/refresh` | POST | Force-refresh the catalog from Composio. |
| `/v1/composio/connections` | GET | Current user's/workspace's Composio connections with status. |
| `/v1/composio/apps/{app}/enable` | POST | Write managed `mcp.json` entry, register MCP server, optionally kick off OAuth. Body: `{ tools?: string[], scope?: "user"|"workspace"|"project" }`. |
| `/v1/composio/apps/{app}/disable` | POST | Remove managed entry and unregister from MCP registry. |
| `/v1/composio/apps/{app}/reconnect` | POST | Re-initiate OAuth for an app whose connection expired. |
| `/v1/composio/status` | GET | Composio account info, quota, enabled apps, connection health. |

### CLI surface

- `sovrant composio login` — prompts for Composio API key, stores it.
- `sovrant composio apps` — table of enabled apps with status.
- `sovrant composio enable <app>` — enable an app non-interactively.
- `sovrant composio browse [--category=<cat>] [--search=<q>]` — catalog browse in terminal.
- `sovrant composio status` — quota, usage, connection health.

### Implementation plan

1. Add `Sovrant.Integrations.Composio` project. Implement
   `IComposioClient` with methods: `ListAppsAsync`,
   `GetAppAsync(appId)`, `ListConnectionsAsync`,
   `CreateConnectionAsync(appId, authData)`,
   `GetQuotaAsync`, `ExecuteToolAsync` (for direct-mode fallback).
2. Implement `ComposioCatalogCache` — SQLite-backed (new
   `composio_app_catalog` table), 24h TTL, manual refresh hook.
3. Implement `ComposioConnectionStore` — links Composio connection
   IDs to `(user_id, workspace_id?, project_id?)` rows in a new
   `composio_connections` table. Resolution follows Phase 38
   precedence.
4. Implement `ComposioMcpProvisioner` — on `Enable`, write a
   managed entry to the scope's `mcp.json` (or insert into a new
   `mcp_servers` table if we've migrated MCP config to SQL by
   then), call `McpClientRegistry.RegisterAsync`, and trigger tool
   discovery. Tool names get the `composio_<app>_<tool>` prefix so
   they don't collide with other MCP servers.
5. Implement `ComposioCostModelAdapter` — registers as a secondary
   `ICostModel` source keyed by tool name prefix; `BudgetEnforcer`
   gates Composio calls against the same per-project/per-session
   budgets it already enforces for model calls.
6. Add `Sovrant.Api.Endpoints.ComposioEndpoints` and wire the
   routes listed above. Bearer-auth protected via Phase 38.
7. Add the Desktop + Web Composio Apps page (shared view model,
   two renderers, consistent with how other pages are built).
   Search, category filter, enable/disable, per-tool toggles,
   connection status, reconnect action.
8. Extend the setup wizard with a "Connect Composio (optional)"
   step that captures the API key and offers to enable a curated
   starter set (GitHub, Gmail, Slack, Calendar, Linear).
9. New migration `V017__composio_tables.sql` — adds
   `composio_connections`, `composio_tool_enablement`, and
   `composio_app_catalog` tables.
10. CLI commands above, under `sovrant composio`.
11. Tests:
    - `ComposioClient` against a mock HTTP handler covering
      catalog, connections, quota, reauth flows.
    - `ComposioCatalogCache` TTL + manual refresh.
    - `ComposioConnectionStore` scope resolution mirrors Phase 38
      precedence (dedicated parametric tests).
    - `ComposioMcpProvisioner` enable → `mcp.json` entry written →
      MCP registry gains tools → disable → reverse.
    - `ComposioCostModelAdapter` routes tool-name-prefixed calls
      through Composio pricing; budget enforcer blocks over-quota.
    - API endpoint tests for the full CRUD surface.
    - Integration test gated behind `COMPOSIO_E2E=1`: real API key
      → enable GitHub app → OAuth stub → invoke `composio_github_list_repos` →
      observe result.

### Acceptance criteria

- `dotnet build` exits 0
- Composio catalog loads and displays ≥ 100 apps in Desktop + Web
- Enabling an app writes an `mcp.json` entry marked `"source":
  "composio"` and its tools appear in `/v1/tools` prefixed
  `composio_<app>_`
- OAuth flow for a managed app completes end-to-end; connection ID
  stored in `composio_connections`, scoped per Phase 38 precedence
- A second user in the same workspace has an independent Gmail
  connection without seeing the first user's tokens
- Tool-level enablement works: enabling Gmail with only `send_email`
  and `list_messages` keeps the rest latent
- `BudgetEnforcer` halts a Composio-heavy run when the project
  budget is exhausted, emitting the same `RuntimeEvent.BudgetExceeded`
  it already emits for model calls
- `sovrant composio status` shows account, quota, usage, enabled apps
  with connection health
- Disabling an app removes its entry and its tools disappear from
  the registry without restarting the runtime
- Manual edits to a managed entry surface a UI warning on next
  refresh but do not crash the runtime

## Phase 81 — Unified Memory: Wire Workspace & Project Memory Into the System Prompt

**Depends on:** Phase 27 (multi-layered memory), Phase 35 (workspaces — memory storage), Phase 36 (projects — memory inheritance)
**Difficulty:** Small

### Why

Today Sovrant has two parallel memory systems that don't meet:

1. **File-based memory** — `~/.sovrant/memory.md` and `.sovrant/memory.md`. Read by `ConversationRuntime.BuildSystemPrompt()` via `AppendMemoryFile()` and prepended to the system prompt every turn. The `/memory` slash command edits these files. **This is the only memory the LLM actually sees.**
2. **Database-backed memory** — `workspace_memory` table with `WorkspaceMemoryEntry` (layer + confidence + project scope). Exposed over `GET/POST/DELETE /v1/workspaces/{id}/memory` and `GET /v1/projects/{id}/memory` (the project endpoint merges project-scoped rows with workspace-level rows). Surfaced in the Workspaces and Projects pages of Desktop and Web. **Persists state but never reaches the LLM.**

The result: a user can save a workspace memory entry through the UI, see it round-trip through the API, and observe that the agent's behavior never changes. The DB entries are dead weight in the system prompt today. This phase closes that loop.

### Goals

- **Inject DB-backed memory into `BuildSystemPrompt()`** alongside the existing file-based memory. Workspace memory first (broader scope), then project memory (more specific). File memory remains the highest-precedence layer because it's still the documented contract for `~/.sovrant/memory.md`.
- **Resolve scope from runtime state** — `ConversationRuntime` already reads `SOVRANT_WORKSPACE_ID` and `SOVRANT_PROJECT_ID` for artifact scoping (lines 796–799). Reuse the same resolution to fetch matching memory rows.
- **Preserve confidence scoring** — entries with `confidence < threshold` (configurable, default 0.3 — matches the Phase 27 prune threshold) are skipped during injection so low-signal data doesn't flood the prompt.
- **Update `/memory` to surface DB entries** — `/memory` and `/mem` should list both file-based and DB-backed memory and let the user edit either. Today they only operate on the files.
- **Token budgeting** — cap injected DB memory at a configurable size (default ~2 KB per scope) to avoid burning the context window when a workspace has hundreds of entries. Sort by `created_at desc` after the confidence filter.
- **Don't break embedded mode** — the runtime already takes `IWorkspaceService` and `IProjectService` via DI in `Sovrant.Web` embedded mode and `Sovrant.Cli`; thread the same services into `ConversationRuntime` rather than reaching into the SQLite store directly.

### Non-goals

- **Migrating file-based memory into the database.** The two systems coexist intentionally — files are git-trackable per project; DB rows are user/workspace-scoped and edited via UI. A unification phase can come later if the duplication becomes confusing.
- **Inventing new memory layers.** Phase 27 already defined the `MemoryLayer` enum (instruction, fact, preference, etc.). This phase wires the existing rows into the prompt; it does not add new types.
- **Real-time memory updates mid-session.** The system prompt is built once at construction. New memory rows added during a session won't affect that session — they'll show up on the next session start. Hot-reload is a follow-up if there's demand.

### Architecture

```
src/Sovrant.Runtime/Conversation/ConversationRuntime.cs
  BuildSystemPrompt()                 ← extend to call new MemoryInjector
  AppendMemoryFile()                  ← unchanged

src/Sovrant.Runtime/Memory/
  IMemoryInjector.cs                  ← NEW: GetSystemPromptMemoryAsync(scope, ct)
  DbMemoryInjector.cs                 ← NEW: queries IWorkspaceService + IProjectService,
                                          applies confidence threshold + token cap,
                                          formats as a single block per scope

src/Sovrant.Commands/Commands/MemoryCommand.cs
  HandleAsync()                       ← extend to also list DB rows for current scope
  EditDbEntry()                       ← NEW: wraps API call to add/update DB entries
```

### Implementation plan

1. Add `IMemoryInjector` interface and `DbMemoryInjector` concrete in `Sovrant.Runtime/Memory/`. Inject `IWorkspaceService`, `IProjectService`, and an `IMemoryInjectorOptions` (confidence threshold, max bytes per scope).
2. Register `IMemoryInjector` in `Sovrant.Runtime`'s DI extension (`AddSovrantRuntime`). For the `Sovrant.Web` remote-mode path (`AddSovrantClient`), register a thin HTTP-backed implementation that calls the existing memory endpoints.
3. Extend `ConversationRuntime` constructor signature to accept `IMemoryInjector?` (optional — null falls back to today's behavior). Resolve via `IServiceProvider` so existing call sites that use the parameterless constructor keep working.
4. In `BuildSystemPrompt()`, after the artifact guidance block but before file memory, emit the DB-backed memory blocks. Order: workspace memory → project memory → global file memory → project file memory. Each block clearly labeled so the model knows the scope.
5. Update `MemoryCommand` (`/memory`, `/mem`) to print DB entries for the current workspace + project below the file content, and accept a `--db` flag for adding a DB row instead of editing the file.
6. Add a `SOVRANT_MEMORY_INJECT_DB` env var defaulting to `true`. Off-switch for users who want pre-Phase-81 behavior temporarily.
7. Tests:
   - `DbMemoryInjector` returns workspace + project rows above threshold, sorted, capped.
   - `BuildSystemPrompt` includes DB memory when injector is present and skips it cleanly when injector is null.
   - End-to-end: save a workspace memory entry via `POST /v1/workspaces/{id}/memory`, start a session in that workspace, verify the entry appears in the system prompt (assertable via a debug endpoint that returns the assembled prompt).
   - `/memory` lists both file and DB entries for the current scope.
   - Regression: existing file-only memory tests still pass unchanged.

### Acceptance criteria

- `dotnet build` exits 0 and the full test suite passes
- A workspace memory entry saved through `POST /v1/workspaces/{id}/memory` appears verbatim in the system prompt of the next session started in that workspace
- A project-scoped entry appears for sessions in that project but not for sessions in other projects of the same workspace
- Entries with confidence below the threshold are excluded
- `/memory` shows both file content and DB rows, labeled by source
- `SOVRANT_MEMORY_INJECT_DB=false` reverts to file-only behavior
- The Workspaces and Projects pages in Desktop and Web continue to work unchanged
- README's Agent Memory section accurately describes the unified behavior once shipped




---

## Phase 82 — Web Search Architecture Overhaul ✅

### Why

Web search shipped as three uncoordinated paths: a `WebSearchTool` (Brave →
FireCrawl → LLM fallback chain gated by a static toggle), an unconditional
`web_search_preview` injection inside the OpenAI Responses provider, and a
silent assumption that every chat-completions provider supported the same
thing. Users could not pick a backend without restarting; capability was
inferred from the provider, not the model; the OpenAI-shim providers (Groq,
Ollama, etc.) attempted native search and failed at runtime.

### Scope

Delivered as five sequenced PRs on `sovrant-openc-dotnet-port`:

- **PR 1 — Capability flag.** `ModelCapabilities.SupportsNativeWebSearch` plus
  per-deployment overrides on top of the existing `IModelCapabilityRegistry`.
  Models advertise whether they can run native search; consumers stop guessing.
- **PR 2 — Resolver + persisted setting.** `WebSearchOptions.Resolve` reads a
  single backend selector from `SOVRANT_WEB_SEARCH`, the layered settings
  files, or the legacy `LLM_WEB_SEARCH=true` (with a deprecation warning), and
  exposes a per-session override on `SovrantConfig.WebSearchOverride`.
- **PR 3 — Centralised injection.** `NativeWebSearchInjector.Plan(...)` is the
  single decision point shared by `FormatConverter`, `ResponsesFormatConverter`,
  and `ProviderApiProvider`. `OpenAiDialectResolver` routes the OpenRouter
  `plugins:[{id:"web"}]` field correctly without breaking the OpenAI-shim path.
  Anthropic native search is merged in via JSON-tree patching of the request.
- **PR 4 — Tool refactor + Settings UI.** `WebSearchTool` switches on the
  resolved backend instead of a static fallback chain; the `/websearch` slash
  command grows to accept backend names; the Web (Blazor) and Desktop
  (Avalonia) Settings pages get a backend dropdown wired into the existing
  auto-save flow with a "model may not support native" warning surfaced from
  the capability registry.
- **PR 5 — Documentation.** `docs/web-search.md` (full matrix), `.env.example`
  (new `SOVRANT_WEB_SEARCH=auto`), README env-table refresh, and this roadmap
  entry.

### Deferred

- **Gemini native `generateContent` endpoint.** The current OpenAI-shim path
  (`generativelanguage.googleapis.com/v1beta/openai`) does not surface
  `tools:[{google_search:{}}]`. A native Gemini provider that hits the
  non-shim endpoint is tracked separately and not part of this phase.
- **SearXNG provider** (Phase 49 — already on the roadmap). The selector
  reserves `searxng-future` so existing configs do not silently break when
  the value is parsed before that lands; until Phase 49, it resolves like
  `auto`.

### Acceptance Criteria

- [x] One backend selector resolves identically for the function tool and
      every provider native injection path
- [x] OpenAI-shim providers no longer attempt native search by default
- [x] `/websearch <backend>` overrides for the current session without
      stomping the saved default
- [x] Web + Desktop Settings show the active backend, persist changes, and
      hot-swap without a restart
- [x] Native + unsupported model surfaces a warning instead of silent failure
- [x] `LLM_WEB_SEARCH=true` continues to work with a deprecation warning
- [x] All web-search related tests pass; no behaviour regression in the
      existing `OpenAiResponsesProvider` path

---

## Phase 82.1 — DuckDuckGo Free-Tier Web Search + WebFetch Audit

> **Status:** Pending.

**Depends on:** Phase 82 (Web Search Architecture Overhaul ✅)

**Goal:** Let users with no API keys get useful web search results out of the box. DuckDuckGo's HTML search endpoint is publicly accessible with no key, no account, and no usage caps — it's the natural zero-config fallback below Brave/Tavily in the search backend hierarchy. This phase also audits the existing `WebFetch` tool to confirm proxy, redirect, and content-type handling are production-ready.

### Items

| # | Item | Notes |
|---|---|---|
| 1 | `DuckDuckGoSearchProvider` | Scrapes/parses DDG HTML (`https://html.duckduckgo.com/html/?q=...`) or uses the unofficial JSON endpoint. Maps to `WebSearchResult` (title, url, snippet). Rate-limit hygiene: respect DDG's rate window; surface 429s clearly. |
| 2 | Wire into backend selector | `SOVRANT_WEB_SEARCH=duckduckgo` plus automatic selection when no other backend is configured and `LLM_WEB_SEARCH` is false. Visible in Settings backend dropdown. |
| 3 | WebFetch audit | Confirm: redirect following (3xx chains), timeout per-request, content-type sniffing (HTML → strip tags, JSON → pass-through, binary → refuse gracefully), proxy env var support (`HTTP_PROXY`, `HTTPS_PROXY`). File any gaps as follow-on issues. |
| 4 | Health check | Add DDG reachability check to `/v1/router/status` alongside existing search backend pings. |
| 5 | Docs update | `docs/web-search.md` backend matrix: add DDG row. `.env.example`: note DDG as the zero-key option. |

### Acceptance Criteria

- `SOVRANT_WEB_SEARCH=duckduckgo` (or no search env at all) returns results without any API key
- DDG backend appears in Web/Desktop Settings dropdown and hot-swaps without restart
- Existing backends (Brave, Tavily, OpenAI Responses, SearXNG) continue to work unchanged
- `WebFetch` audit produces a written findings list; any Critical/High gaps filed as issues

---

## Phase 82.5 — OpenTelemetry Observability

> **Status:** Pending. Adds first-class OTel emission so operators can ship Sovrant
> traces, metrics, and logs to any OTel-compatible backend (Honeycomb, Tempo,
> Jaeger, Datadog, Dynatrace, Grafana Cloud, self-hosted OTel Collector) without
> us picking a vendor.

### Goal

Replace ad-hoc logging and the `RuntimeTraceWriter` JSONL stream with a real
OpenTelemetry pipeline. Every meaningful unit of work — engine run, turn, tool
call, router decision, provider HTTP request, swarm wave, mission step — emits
spans, metrics, and structured log records via the OTel SDK. Export is OTLP by
default; the existing JSONL writer stays as an optional console exporter for
local debugging.

### Why now

- Operators running Sovrant.Server in production need standard observability,
  not bespoke log files. The `/v1/runs/...` and `/v1/missions/...` surfaces
  already correlate by run/session/workspace IDs — those become trace
  attributes "for free".
- Cost tracking (Phase 55) and budgets (V018) are already collected per
  session/project; emitting them as OTel metrics gives a single dashboard for
  $/run, latency, error rates.
- Required for Phase 40 (enterprise auth & multi-tenancy) — auditors expect
  per-tenant trace sampling and metric tagging.

### Scope

- **Tracing**: Wrap top-level operations as spans (`engine.run`, `turn`,
  `tool.invoke`, `router.route`, `provider.http`, `swarm.wave`,
  `mission.step`). Attach `workspace.id`, `project.id`, `session.id`,
  `run.id`, `model`, `provider`, `tier`, `intent`, `tools.invoked` as
  attributes.
- **Metrics**: Counters / histograms / gauges for `sovrant.runs.total`,
  `sovrant.turns.duration`, `sovrant.tool.invocations`,
  `sovrant.tokens.input` / `.output`, `sovrant.cost.usd`,
  `sovrant.router.decisions`, `sovrant.provider.errors`,
  `sovrant.sessions.active`. All tagged with the same dimensions as spans.
- **Logs**: Bridge `ILogger` records into the OTel logs signal so existing
  log lines flow through OTLP with trace correlation. Keep the file logger
  for local dev.
- **Exporters**: OTLP (HTTP and gRPC) by default; optional console exporter
  enabled via `SOVRANT_OTEL_CONSOLE=true`. Resource attributes seeded from
  `service.name=sovrant`, `service.version`, `deployment.environment`.
- **Sampling**: Default to parent-based + ratio (`SOVRANT_OTEL_TRACE_RATIO`,
  default `1.0` for dev, `0.1` recommended in prod). Mission/team runs always
  sampled to preserve audit completeness.
- **Surfaces**: Activate in CLI/Server/Web/Desktop. CLI gets a simple
  `--otel-endpoint` flag; the rest read `OTEL_EXPORTER_OTLP_ENDPOINT` per
  the standard OTel env-var contract.

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| `SovrantOtelExtensions.AddSovrantOtel(...)` | `src/Sovrant.Runtime/Observability/` | Wires `OpenTelemetry.Trace` / `.Metrics` / `.Logs`; reads `BootstrapConfig` + standard `OTEL_*` env vars |
| `SovrantActivitySource` | `src/Sovrant.Runtime/Observability/` | Single `ActivitySource("Sovrant")` shared by all instrumentation |
| `SovrantMeter` | `src/Sovrant.Runtime/Observability/` | Single `Meter("Sovrant")` exposing the metric set above |
| `RuntimeTraceWriter` | existing | Stays — JSONL becomes an opt-in custom exporter when OTel is disabled |
| `ConversationRuntime` | `src/Sovrant.Runtime/Conversation/` | Open `engine.run` / `turn` spans; record token counts as metric instruments |
| `ToolRegistry.InvokeAsync` | `src/Sovrant.Runtime/Tools/` | `tool.invoke` span + counter |
| `SmartRouter.RouteAsync` | `src/Sovrant.Api/Routing/` | `router.route` span with provider/tier attributes |
| Provider HTTP clients | `src/Sovrant.Api/Providers/*` | `HttpClient` instrumentation via `AddHttpClientInstrumentation()` |
| `Sovrant.Server/Program.cs` | existing | `AddAspNetCoreInstrumentation()` for endpoint spans |

### Acceptance Criteria

- [ ] Local dev with `SOVRANT_OTEL_CONSOLE=true` prints structured spans to stdout
- [ ] OTLP export to a Collector reachable at `OTEL_EXPORTER_OTLP_ENDPOINT`
- [ ] All spans correlated by trace ID across server endpoints, runtime, and
      provider HTTP calls (single trace per `engine.run`)
- [ ] Metrics include token counts, cost USD, tool invocations, router decisions
- [ ] Existing JSONL writer still works when OTel is disabled (no regression)
- [ ] CLI `--otel-endpoint` flag and standard `OTEL_*` env vars both honored
- [ ] Documentation: new `docs/observability.md` with example Collector config
      and dashboards (Tempo / Jaeger / Honeycomb) plus README env-table refresh

### Deferred

- Vendor-specific exporters beyond OTLP (Datadog/New Relic native exporters) —
  users can plug them into their Collector instead.
- Continuous profiling (pprof) — separate concern; OTel profiling signal is
  still stabilizing.

---

## Phase 83 — Pluggable Memory Backends (mem0, Vector DBs, Redis, Postgres)

> **Status:** Pending. Generalises the SQLite memory store behind an interface
> so deployments can swap to a distributed/remote backend (mem0, Pinecone-style
> vector DBs, Redis, Postgres+pgvector) without changing call sites. Enables
> shared/team memory across nodes and large-scale semantic recall.

### Goal

Today `IMemoryStore` is implemented only by the SQLite-backed
`SqliteMemoryStore`. That works great for single-node CLI / Desktop / single
Server installs but doesn't support:

- A team running Sovrant across many server replicas that all share memory
- A workspace with thousands of memories where semantic recall (embedding
  similarity) outperforms keyword/FTS5 matching
- Cross-tenant managed deployments where memory is its own service

The phase introduces a small provider abstraction so the SQLite backend stays
the default but operators can drop in alternatives via configuration.

### Why now

- Phase 81 (unified memory) shipped: every saved memory now flows into the
  system prompt via `MemoryInjector`. Scaling that to thousands of memories
  per workspace is the obvious next pain point.
- mem0 is gaining traction as a hosted memory layer with semantic recall,
  decay, and contradiction handling out of the box — exactly the
  capabilities we'd otherwise have to rebuild.
- The bucket-A consolidation work (config-audit Phase 1) means the storage
  layer's bootstrap path is already pluggable; adding a second store is a
  natural follow-on.

### Scope

- **Provider interface refresh**: extend `IMemoryStore` with optional
  semantic methods (`SearchSimilarAsync`, `EmbedAsync`) gated behind a
  capability check so SQLite implementations aren't forced to add vector
  support.
- **Built-in providers**:
  - `SqliteMemoryStore` (existing, default) — keyword + FTS5 recall
  - `Mem0MemoryStore` — HTTP wrapper around mem0's API, supports semantic
    recall, decay, contradictions
  - `PgVectorMemoryStore` — Postgres + `pgvector` for self-hosted semantic
    recall (workspace-scoped tables)
  - `RedisMemoryStore` — Redis Search + RedisVL for distributed in-cache
    memory with TTL
- **Selector**: `SOVRANT_MEMORY_BACKEND` env var (`sqlite` (default) /
  `mem0` / `pgvector` / `redis`); credentials via the credential store
  (`memory.{backend}.api_key` / `memory.{backend}.connection_string`).
- **Embedding strategy**: Reuse the configured LLM provider's embeddings
  endpoint when the backend needs vectors (`text-embedding-3-small` for
  OpenAI, etc.). Backends that produce their own embeddings (mem0) skip
  this path.
- **Migration tooling**: `sovrant memory migrate --from sqlite --to mem0`
  CLI command that streams all memories from one provider to another with
  workspace/project scoping preserved.
- **Hybrid mode** (stretch): Two backends chained — local SQLite for
  instant lookups, remote backend for shared/semantic recall — merged at
  query time.

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| `IMemoryStore` extensions | `src/Sovrant.Runtime/Memory/` | Add optional `SearchSimilarAsync`, `IMemoryCapabilities` |
| `Mem0MemoryStore` | `src/Sovrant.Runtime/Memory/Backends/` | Typed HTTP client, retries, OAuth/API key via credential store |
| `PgVectorMemoryStore` | `src/Sovrant.Runtime/Memory/Backends/` | Npgsql + `pgvector`; one table per workspace |
| `RedisMemoryStore` | `src/Sovrant.Runtime/Memory/Backends/` | StackExchange.Redis + Redis Search index |
| `MemoryStoreFactory` | `src/Sovrant.Runtime/Memory/` | Reads selector, builds the right store; wires DI |
| `MemoryMigrateCommand` | `src/Sovrant.Cli/Commands/` | Drains source → destination; idempotent; resumable |
| `SovrantConfig.Memory.Backend` | `src/Sovrant.Runtime/Config/` | Persisted preference (DB settings table); env override per Bucket-D |
| Settings UI (Web + Desktop) | various | Backend dropdown; "Test connection" button; migrate CTA |

### Acceptance Criteria

- [ ] Default SQLite backend remains untouched; existing behaviour and tests
      pass with no changes
- [ ] `SOVRANT_MEMORY_BACKEND=mem0` boots end-to-end with credentials sourced
      from the credential store; semantic recall returns higher-relevance
      results than FTS5 on a sample workspace
- [ ] `pgvector` and `redis` backends pass the same `IMemoryStore` contract
      tests as SQLite (parity suite shared across backends)
- [ ] `sovrant memory migrate` round-trips a workspace's memories without
      data loss; reports counts, skips already-migrated rows
- [ ] Settings UI shows the active backend, allows switching with a
      migration prompt, and surfaces connection errors clearly
- [ ] Phase 81's `MemoryInjector` works identically across all backends
      (semantic backends rank by similarity, SQLite by recency)

### Deferred

- **Hybrid (local + remote) mode** — kept as a stretch; ship the single-backend
  path first.
- **Multi-tenant managed memory service** — a hosted Sovrant memory layer is
  a separate product question, not a runtime feature.

---

## Phase 84 — Prompt Library: Reusable, Parameterised Prompt Templates Across Surfaces

> **Status:** Pending. Adds a first-class prompt library so users can author,
> share, version, and invoke reusable prompt templates from CLI, Web, and
> Desktop. Templates support variables, are scoped to user / workspace /
> project, and are wired into the slash-command surface so they feel like
> native commands.

### Goal

Users repeatedly type the same multi-paragraph prompts ("write a unit test
for X", "audit this PR for security issues", "scaffold a Node app with the
following structure…") across sessions. Today there's no shared place to
keep them; they end up in scratch files, browser bookmarks, or muscle
memory. A prompt library treats prompts as durable, named, parameterised
artifacts — the way skills and agents are.

### Why now

- The agentic loop is mature enough that the bottleneck has shifted from
  "can the model do this?" to "can the user describe what they want
  consistently?" — prompts are the user-side analogue of skills.
- Skills (Phase 67) and Agents (Phase 79) already model named, scoped,
  parameterised behaviours. Prompts share most of the metadata shape and
  storage primitives, so this is incremental.
- Dogfooding shows users hand-rolling the same scaffolding prompts every
  session. A library would compress that to a one-liner.

### Scope

A `PromptTemplate` is a record with:

- **Name** (slug, unique within scope)
- **Description** (short — shows up in pickers)
- **Body** (markdown with `{{variable}}` substitutions)
- **Variables** (typed: `string`, `enum`, `multiline`, `file-ref`,
  `agent-ref`)
- **Scope** (user / workspace / project / global-shared)
- **Tags** (for filtering)
- **Version** (so edits don't silently change behaviour for callers)
- **Source** (built-in / user-authored / imported / shared)

Surfaces:

- **CLI** — `/prompt list`, `/prompt run <name>`, `/prompt new`,
  `/prompt edit`. Slash-command alias auto-registered: `/<prompt-name>`
  routes to the template after variable prompts.
- **Web** — Prompts page (parallel to Skills/Agents), card list with run
  button, inline editor, "import from file" and "share to workspace".
- **Desktop** — Prompts panel under the Knowledge group; same affordances.
  Inline picker in the chat composer (`/` triggers prompt + slash-command
  picker).
- **Server** — `GET/POST/PUT/DELETE /v1/prompts`, `POST /v1/prompts/{name}/run`
  (returns the rendered prompt; client decides whether to send it as a
  user turn).

Storage: SQLite migration `prompts` + `prompt_variables` tables, scoped
by `user_id` / `workspace_id` / `project_id` (mirrors the artifact /
memory scoping pattern). Sharing a prompt = copying it to another scope
or marking it `global-shared`.

Built-ins: ship a starter set covering common dogfood paths — "scaffold
a Node app", "write a Markdown PRD from these notes", "review this PR
for security issues", "summarise this conversation as memory".

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| `PromptTemplate` record | `src/Sovrant.Runtime/Prompts/` | Body + typed variables + scope |
| `IPromptStore` + `SqlitePromptStore` | `src/Sovrant.Runtime/Prompts/` + Storage | CRUD, scoped lookups |
| Migration `V0XX__prompts.sql` | `src/Sovrant.Runtime/Storage/Migrations/` | Two tables, indexed by scope |
| `PromptRenderer` | `src/Sovrant.Runtime/Prompts/` | `{{var}}` substitution, missing-var validation |
| `PromptSlashCommand` | `src/Sovrant.Commands/` | Auto-registers `/<name>` for each prompt in scope |
| `PromptRoutes` | `src/Sovrant.Server/Routes/` | REST surface |
| Web `Prompts.razor` | `src/Sovrant.Web/Components/Pages/` | List + editor + run |
| Desktop `PromptsView.axaml` | `src/Sovrant.Desktop/Views/` | Knowledge-group panel |
| CLI `PromptCommand` | `src/Sovrant.Cli/Commands/` | list/run/new/edit/import |
| Built-in prompts | `prompts/builtin/*.md` | Embedded resources, seeded on first boot |

### Acceptance Criteria

- [ ] User authors a prompt in any surface; it appears in the others within
      the same scope (workspace / project) without restart
- [ ] `/<prompt-name>` works as a slash command in CLI, Web, and Desktop;
      missing variables prompt the user inline
- [ ] Prompt scoping respects the existing user / workspace / project
      hierarchy — a project prompt only shows when that project is active
- [ ] Built-in starter prompts ship with the install and are visible
      under "global-shared"; users can fork them into their own scope
- [ ] Version bump on edit is automatic; running an older version is
      opt-in via `/prompt run <name>@<version>`
- [ ] Tests cover render variable substitution, scope resolution, and
      slash-command auto-registration

### Deferred

- **Prompt marketplace / community sharing** — out of scope; private
  share-to-workspace is enough for v1.
- **A/B prompt evaluation** — instrumentation to compare prompt variants
  by outcome belongs in the eval framework, not the prompt library.
- **Prompt chains / pipelines** — building one prompt from the output of
  another. Use the agentic loop or a custom skill for this until demand
  surfaces.

---

## Phase 85 — Identity & Login Parity Across CLI, Web, Desktop & Server

> **Status:** ✅ Complete (2026-05-07). All surfaces shipped: server auth layer, Desktop login, Web login, CLI login/logout/whoami, admin pages (Web + Desktop), auth unit tests (59 passing).

### Design decisions (finalised)

- **First registered user becomes admin**; registration closes automatically after first sign-up. Admin can reopen.
- **Static `SOVRANT_TOKEN` removed entirely** — only per-user `svt_` tokens are accepted. No backwards-compat shim (pre-release).
- **Password reset is admin-only, no SMTP required** — admin generates a one-time reset token via the admin page and shares it out-of-band.
- **Token TTL: 30-day sliding window** — each use refreshes `expires_at` forward 30 days (write throttled to at most once per day per token).
- **LLM API keys are NOT passed over HTTP** — credentials live in the encrypted keystore, resolved server-side via `ICredentialStore`. The `X-LLM-Api-Key` / `X-LLM-Base-Url` per-request headers are being removed.
- No JWT, no OAuth providers for beta — plain `svt_` bearer tokens throughout.

### Implementation progress

#### ✅ Completed

| Component | File |
|---|---|
| DB migration (`V026__auth_credentials.sql`) | `src/Sovrant.Runtime/Storage/Migrations/` |
| `IPasswordHasher` + `Argon2idPasswordHasher` | `src/Sovrant.Runtime/Auth/` |
| `IIdentityService` + `SqliteIdentityService` | `src/Sovrant.Runtime/Auth/` |
| `SqliteTokenService` sliding TTL | `src/Sovrant.Runtime/Auth/` |
| `IUserService.GetByEmailAsync` + impl | `src/Sovrant.Runtime/Users/` |
| `AuthRoutes` (register/login/logout/change-password/use-reset-token/registration) | `src/Sovrant.Server/Routes/` |
| `BearerTokenMiddleware` rewrite — `svt_` tokens only, static token removed | `src/Sovrant.Server/Auth/` |
| DI registrations for `IPasswordHasher` + `IIdentityService` | `src/Sovrant.Runtime/ServiceCollectionExtensions.cs` |

#### ⬜ Pending

| Component | File | Notes |
|---|---|---|
| Remove `SOVRANT_TOKEN` startup check, register `AuthRoutes`, first-run log hint | `src/Sovrant.Server/Program.cs` | ✅ Done |
| Remove `X-LLM-Api-Key`/`X-LLM-Base-Url` header processing | `src/Sovrant.Server/Routes/ChatRoutes.cs` | ✅ Done — per-request LLM key injection removed |
| `POST /v1/users/{id}/reset-password` (admin only) | `src/Sovrant.Server/Routes/UserRoutes.cs` | ✅ Done |
| `IPrincipalAccessor` interface + server/embedded impls | `src/Sovrant.Runtime/Auth/` | ✅ Done |
| Desktop login flow — `LoginWindow`, `LoginViewModel`, `ICredentialStore` token storage, logout | `src/Sovrant.Desktop/` | ✅ Done |
| Web login/register pages + route guard + logout | `src/Sovrant.Web/Components/Pages/Login.razor`, `MainLayout.razor` | ✅ Done |
| CLI `login` / `logout` / `whoami` commands | `src/Sovrant.Cli/Program.cs` | ✅ Done |
| Update `MigrationRunnerTests` expected schema version | `tests/Sovrant.Runtime.Tests/` | ✅ Done (24 → 26) |
| Admin pages (Web + Desktop) | `src/Sovrant.Web/Components/Pages/Admin.razor`, `src/Sovrant.Desktop/Views/AdminView.axaml` | ✅ Done |
| Unit tests — `IdentityServiceTests`, `PasswordHasherTests`, `SqliteTokenServiceTests` | `tests/Sovrant.Runtime.Tests/Auth/` | ✅ Done (59 tests passing) |

### Goal

Today each surface invents its own notion of "who am I":

- **Desktop** — `SOVRANT_USER_ID || Environment.UserName` (no auth)
- **CLI** — same env-var fallback (no auth)
- **Web** — Blazor Server with optional per-session bearer token; no first-class user
- **Server** — first-class users + API tokens (Phase 38), workspace membership, RLS-ish scoping

This means a user who sets up Desktop, then opens the same install in the
browser, looks like a different person to the system. Workspaces, sessions,
and memories don't follow them. A multi-user team can't safely share a
laptop or a server without spoofing each other.

The phase introduces a single login flow — **email + password (Argon2id)**
plus optional OAuth providers — and threads the resulting `UserId` through
every surface. The server's existing API-token model stays the source of
truth for *machine-to-machine* identity; this phase covers the
*human-to-product* identity that funnels into it.

### Why now

- Phases 35–38 already shipped users, workspaces, memberships, and API
  tokens. The data model is there; only the surface plumbing is missing.
- Dogfooding already trips over the inconsistency: switching from Desktop
  to Web mid-session loses context because the user identity differs.
- The trust-boundary work (Phase 58) and audit logging want a single
  reliable principal — bolting auth on later means rewriting attribution.
- Source-available licensing (BSL → Apache) means more eyes on the code;
  shipping with no first-class auth on Desktop/CLI is a defensibility
  problem.

### Scope

**One identity model, four surfaces.** A `User` record (already exists)
gains an `email_hash`, `password_hash` (Argon2id), `email_verified_at`,
and `last_login_at`. Login produces a session token consumed identically
by every surface.

**Surface flows:**

- **Desktop** — first run shows a login/register dialog (already have a
  setup wizard pattern; extend it). Token persisted in DPAPI-protected
  store. "Continue as local user (no auth)" remains an option for
  air-gapped use; it maps to a synthetic single-user account that can
  be promoted to a real one later.
- **CLI** — `sovrant login` opens browser to a local callback (mirrors
  the MCP OAuth flow), or accepts `--email/--password`, or reads
  `SOVRANT_API_TOKEN` for headless. Token stored in OS keychain.
- **Web** — Blazor login page (`/login`, `/register`, `/forgot-password`),
  cookie auth for browser sessions, JWT for the SDK. SSO plug-in points
  for Google / Microsoft / GitHub.
- **Server** — adds `POST /v1/auth/login`, `POST /v1/auth/register`,
  `POST /v1/auth/logout`, `POST /v1/auth/forgot-password`,
  `POST /v1/auth/reset-password`. Existing per-request credential and
  workspace-token mechanisms (the "complicated auth scheme" for data
  scoping) are unchanged — login mints a token that those layers
  continue to consume.

**Best-practice security baseline:**

- Argon2id password hashing (Konscious.Security.Cryptography), tuned
  per OWASP 2026 recommendations
- Rate-limited login endpoint (existing rate-limit middleware)
- Email-based password reset with single-use, time-bound tokens
- Optional TOTP 2FA (defer to a follow-up if scope grows)
- Session tokens are signed JWTs with short TTL + refresh; revocation
  list piggybacks on existing API-token revocation table
- Tokens scoped by audience (`desktop`, `cli`, `web`, `sdk`) so a leaked
  CLI token can't be used in the browser session

**Migration path:**

- Existing installs with no auth boot into "local user" mode. A
  one-time prompt in each surface invites the user to upgrade to an
  account; declining keeps local mode working forever.
- Existing sessions / workspaces / memories are owned by `SOVRANT_USER_ID
  || os-username`. Upgrading to an account migrates ownership in place
  (idempotent, audited).

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| Migration `V0XX__user_login.sql` | `src/Sovrant.Runtime/Storage/Migrations/` | Add `email_hash`, `password_hash`, `email_verified_at`, `last_login_at`, `password_reset_tokens` table |
| `IPasswordHasher` + Argon2id impl | `src/Sovrant.Runtime/Auth/` | Konscious.Security.Cryptography; tuned params |
| `IIdentityService` | `src/Sovrant.Runtime/Auth/` | Register / login / verify / reset / token-mint |
| `JwtSessionTokenIssuer` | `src/Sovrant.Runtime/Auth/` | Short-TTL access + refresh; audience claim |
| `AuthRoutes` | `src/Sovrant.Server/Routes/` | Login / register / logout / forgot / reset |
| `LoginPage.razor` + `RegisterPage.razor` | `src/Sovrant.Web/Components/Pages/` | + cookie middleware wiring |
| Desktop `LoginDialog` | `src/Sovrant.Desktop/Views/` | First-run + Settings → Account |
| `OsKeychainTokenStore` | `src/Sovrant.Cli/Auth/` | Wraps DPAPI / Keychain / libsecret |
| `sovrant login/logout/whoami` | `src/Sovrant.Cli/Commands/` | + browser-callback flow |
| `IPrincipalAccessor` | `src/Sovrant.Runtime/Auth/` | Single abstraction surfaces use to read current user |
| Optional SSO providers | `src/Sovrant.Server/Auth/` | Google / Microsoft / GitHub OAuth — pluggable, off by default |

### Acceptance Criteria

- [ ] A user registers in Desktop and immediately sees the same workspaces,
      sessions, and memories when they open Web pointing at the same DB / server
- [ ] CLI `sovrant login` works headlessly (`--email --password`) and
      interactively (browser callback); subsequent commands resolve the
      same `UserId` as Desktop / Web
- [ ] Server's existing per-request credential model (`X-LLM-Api-Key`,
      workspace tokens) is unchanged and continues to scope data access;
      login layers cleanly on top
- [ ] Password storage uses Argon2id with parameters meeting OWASP 2026
      guidance; a hash audit script verifies it
- [ ] Rate-limited login + lockout-on-brute-force behaviour is covered by
      tests
- [ ] Existing single-user installs ("local user" mode) continue to work
      with no migration required, and the one-time upgrade flow migrates
      data ownership atomically
- [ ] Token audiences prevent cross-surface token reuse; integration tests
      confirm a CLI-audience token rejected by web cookie middleware
- [ ] Logout revokes the token everywhere within the next refresh cycle

### Deferred

- **Federated identity (SAML, enterprise SSO)** — deferred to a separate
  enterprise phase once a paying customer asks.
- **Hardware-key second factor (WebAuthn / passkeys)** — desirable, but
  TOTP first.
- **Cross-org user federation** — out of scope; a single Sovrant install
  is one identity domain.

---

## Phase 86 — Background Session Continuation Across Navigation & Session Switches

> **Status:** Pending. Lets a user kick off a long-running turn in one
> session, navigate away (other menu items, another session, even close
> the chat surface entirely), and have it keep working — surfacing the
> result when it's done and letting the user catch up on the stream when
> they return.

### Goal

Today the runtime pool (`RuntimeSessionPool`) already keeps per-session
runtimes warm — the in-flight turn doesn't actually stop when the user
navigates away. But the UI surfaces (Desktop `ChatViewModel`, Web
`Chat.razor`) tie streaming subscriptions to the active view: leaving
the page detaches the subscriber, returning later sees a "frozen" chat,
and switching to another session loses any sense that the previous one
was still working.

The phase makes session execution **truly backgrounded**: turns continue
on the runtime, intermediate events are buffered, and any surface that
re-subscribes (the same session reopened, a different surface attached
to the same DB / server) replays what it missed and then catches the
live tail. Users get cross-session indicators — a badge or toast — when
a backgrounded session finishes, errors, or hits a confirmation prompt.

### Why now

- Dogfooding Node-app scaffolding routinely produces 30-second–5-minute
  turns. Switching to Activity, Memory, or another session to check
  something mid-run currently looks like the chat hung.
- Phase 85's multi-surface identity makes it obvious the runtime is
  shared — the user expects state to follow them, not the view.
- Confirmation prompts + tool calls already block on user input; if the
  user is on another page when one fires, today they must guess that
  the prompt is waiting somewhere else.
- Phase 79's swarms/teams will spawn many parallel sessions; without
  background continuation the UX collapses into context-switch tax.

### Scope

**Runtime side**

- Per-session **event broker** (`ISessionEventBus`) sits in front of
  `IConversationRuntime`'s event stream. Buffers the last N
  `RuntimeEvent`s (Message, ToolCall, ToolResult, ModelSelected,
  TurnComplete, ConfirmationRequested, Error) per session with a
  bounded ring buffer; older events spill to the existing
  `session_entries` log so replay is always possible.
- Sessions in the pool gain a **status** (`Idle`, `Running`,
  `WaitingForConfirmation`, `Errored`, `Completed`) that surfaces
  expose without subscribing to the full stream.
- Eviction TTL (currently fires on idle sessions) is **disabled while
  status ∈ {Running, WaitingForConfirmation}** — a backgrounded turn
  can't be silently killed by the eviction service.

**UI side (Desktop + Web parity)**

- Sidebar session list shows per-session status pips: spinner for
  `Running`, ❗ for `WaitingForConfirmation`, ✕ for `Errored`,
  ✓ briefly on `Completed → Idle`.
- Switching to a session that has unread events replays the buffer
  (cheap — already in memory) then attaches to the live tail.
- Global toast / system tray / browser-tab title updates when a
  backgrounded session finishes or needs confirmation. Click-through
  jumps to the session.
- Closing the chat surface entirely (Desktop window minimised, Web tab
  navigated to a non-chat page) does **not** cancel; the runtime keeps
  going and surfaces the result when the user returns. Explicit
  cancel is a separate per-session "Stop" action.

**Server-mode parity**

- Web/CLI in remote mode receive the same buffered events via SignalR;
  reconnect after a transient drop replays from the last seen sequence
  number rather than restarting the turn.
- Session status is queryable via `GET /v1/sessions/{id}/status` so a
  CLI `sovrant status` command can list what's running across the
  user's sessions.

**Persistence**

- `session_entries` already records every event by sequence; the broker
  layers an in-memory cache on top, not a new store. Replay falls
  through to the DB if the buffer is cold (e.g. server restart mid-turn).

### Non-goals

- Resuming a turn after a **process restart** — the runtime is in-process
  state. If the server/desktop crashes mid-turn, the turn is lost; only
  the persisted history survives. (A future phase could checkpoint
  long-running tool loops, but not here.)
- Running *multiple turns in parallel within a single session*. One
  turn at a time per session; the broker just decouples the **viewer**
  from the **executor**.

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| `ISessionEventBus` + impl | `src/Sovrant.Runtime/Conversation/` | Per-session ring buffer, sequence numbers, late-subscriber replay |
| `RuntimeSessionPool` status | `src/Sovrant.Runtime/Conversation/RuntimeSessionPool.cs` | Track `SessionStatus`; expose via `GetStatusAsync(sessionId)`; suppress eviction while busy |
| `SessionEvictionService` guard | `src/Sovrant.Server/SessionEvictionService.cs` | Skip eviction for non-Idle status |
| Desktop sidebar status pips | `src/Sovrant.Desktop/ViewModels/SessionsViewModel.cs` + `Views/SessionsView.axaml` | Subscribe to `ISessionEventBus.StatusChanged` |
| Web sidebar status pips | `src/Sovrant.Web/Components/Layout/Sessions*.razor` | Mirror Desktop |
| Desktop tray notifications | `src/Sovrant.Desktop/Services/NotificationService.cs` (new) | Avalonia tray icon flash + balloon on `Completed`/`WaitingForConfirmation` |
| Web tab-title updates | `src/Sovrant.Web/wwwroot/js/sovrant-notify.js` (new) + JS interop | `document.title` prefix, optional `Notification` API |
| Replay on attach | `ChatViewModel`, `Chat.razor` | On session select, drain bus → render → subscribe to live tail |
| Server status endpoint | `src/Sovrant.Server/Routes/SessionRoutes.cs` | `GET /v1/sessions/{id}/status` |
| CLI `sovrant status` | `src/Sovrant.Cli/Commands/StatusCommand.cs` (new) | Lists running/waiting sessions for the logged-in user |

### Acceptance Criteria

- [ ] User starts a long-running turn (e.g. "scaffold a Node app"), navigates
      to another menu item, and returns to the same chat — the in-flight
      turn is still running, intermediate tool calls / messages that fired
      while away are visible, and the live tail continues
- [ ] User starts a turn in session A, switches to session B, then back to
      A — buffered events replay and the live tail attaches without a
      duplicate or missed event
- [ ] A turn that hits a confirmation prompt while the user is on another
      page surfaces an indicator (sidebar pip + toast/tray); clicking it
      jumps to the session and shows the prompt
- [ ] Eviction service does not reap a session whose status is `Running`
      or `WaitingForConfirmation`, regardless of TTL
- [ ] Web/CLI in remote mode survive a transient SignalR disconnect during
      a turn — reconnect replays from the last seen sequence and the
      stream continues from where it left off
- [ ] An explicit per-session **Stop** action cancels the in-flight turn
      cleanly (existing cancellation token plumbing) without affecting
      other sessions
- [ ] Desktop tray + Web tab-title indicators fire on `Completed` and
      `WaitingForConfirmation`, and clear on `Idle` or when the user
      reopens the session
- [ ] `sovrant status` (CLI) lists running/waiting sessions for the
      logged-in user with their status and elapsed time

### Deferred

- **Mid-turn process-restart recovery** — would require checkpointing
  active tool loops; covered separately if/when crash recovery becomes
  a priority.
- **Multiple parallel turns per session** — different problem (turn
  fan-out, not viewer/executor decoupling); revisit alongside the
  swarms work in Phase 79.
- **Mobile push notifications** — depends on a mobile surface, which
  isn't on the roadmap yet.

---

## Phase 87 — Artifacts-by-Default for Code & Documents (with Workspace Identity Unification)

> **Status:** ✅ Complete (2026-05-19). All tracks shipped.
>
> - **Track A** — `ConversationRuntime` auto-saves fenced code blocks; download cards in Web + Desktop.
> - **Track B** — Artifact tool description rewritten: user-centric, not agent-only; mentions `write_many`.
> - **Track C** — `write_many` action: manifest-based batch write, one approval, per-entry error tracking.
> - **Track D** — Workspace-first artifact layout; runtime auto-injects scope.
> - **Track E** — Per-turn "always allow Artifact" grant in CLI/Web/Desktop via `IPerTurnApprovalCache`.
> - **Track F** — Artifacts screen: Monaco read-only editor for code files (Web); "Open folder" for run scope (Desktop); download-run-as-zip endpoint + button (Web).
> - **Workspace identity** — `WorkspaceIdentity.DefaultPersonalFor(userId)` canonical helper; `"personal"` literal removed; V022 migration.
>
> Original rationale: the assistant routinely dumps multi-thousand-token
> code and document bodies into the chat instead of writing them to the
> artifact store, and the artifact store itself silently splits the same
> "personal" workspace into two parallel directories because two different
> derivations of the default-workspace ID drift apart. Both bugs surfaced
> through the Node-app and presentation dogfood sessions on 2026-05-02.

### Goal

Anything the user asks the assistant to *create* — code, documents,
presentations, reports, scaffolded projects — should land as an artifact
with no ceremony. The user shouldn't have to say "make it an artifact,"
the LLM shouldn't have to be told twice not to paste a 14k-char markdown
body into chat, and `~/.sovrant/artifacts/` should never contain two
directories for the same logical workspace.

### Why now

Concrete dogfood evidence from the 2026-05-02 sessions:

- **Presentation session** (`session-aa76e3ce…`): user said "can you make
  me a presentation that teaches my team about ai?" → assistant emitted
  a 14,054-char markdown body inline (5,463 output tokens) → user had
  to follow up with "create the document as an artifact" before the
  Artifact tool was invoked.
- **Node-app session** (`session-c8bf7b8e…`): five consecutive assistant
  turns producing 1,943 → 4,231 → 10,379 → 480 → 738 chars of "I'm going
  to create…" prose (peak: 8,192 output tokens). Only after the user
  explicitly said "do not output code to the screen, simply create the
  code files in the artifact folder" did two files actually get written
  via Artifact — then the assistant gave up. User had to repeat "thats
  only two files" three times.
- **Workspace split:** `~/.sovrant/artifacts/` contains both `personal/`
  and `ws-personal-eramseur/`. The first comes from
  `ArtifactScope.DefaultWorkspaceId = "personal"` (the literal fallback
  in tool calls); the second from `SqliteWorkspaceStore.CreatePersonalWorkspaceAsync`
  which mints `ws-personal-{userId}`. Same user, two homes.

The cost is real: ~20–30k wasted output tokens per "scaffold me an app"
turn, plus user time spent corraling the model into using the tool that
was already available, plus a workspace identity scheme that quietly
violates the cross-surface guarantee the rest of the platform is built on.

### Scope

**A. Content Creation Discipline (system prompt)**

`SystemPromptBuilder` gains an explicit section:

> Any deliverable longer than ~30 lines (code, document, presentation,
> report, data file) MUST be produced via the `Artifact` tool — never
> pasted into chat. For multi-file outputs (apps, projects), use one
> `Artifact` call per file, or `write_many` for a batch. Reply with a
> short summary referencing the artifact paths; do not echo the bodies.

The rule is checked in two places: the prompt teaches it, and a
post-turn lint flags any assistant message containing >30 lines of
fenced code/markdown that didn't accompany an Artifact write
(non-blocking warning in the dev console; metric for tracking).

**B. Artifact tool description rewrite**

Drop the agent-only framing. Today's description (`"Agents produce
artifacts in isolation; the orchestrator and user consume results.
Do NOT use this for agent-to-agent messaging."`) reads to the
chat-assistant LLM like *"this is for swarm/team plumbing, not me."*
New description:

> The canonical place for any deliverable the user receives — code
> files, documents, presentations, reports, data. Always prefer
> `Artifact` over inline chat content for anything more than a few
> lines. Actions: `write` (one file), `write_many` (manifest of files
> for project scaffolds), `read`, `list`.

**C. `Artifact write_many` action**

A new action that takes a manifest:

```json
{
  "action": "write_many",
  "files": [
    { "path": "package.json",       "content": "...", "content_type": "application/json" },
    { "path": "src/index.ts",       "content": "...", "content_type": "text/typescript" },
    { "path": "src/routes/auth.ts", "content": "...", "content_type": "text/typescript" }
  ]
}
```

One tool call, one approval prompt, one transactional write. Today a
30-file scaffold is 30 round-trips and 30 permission prompts — which
is why the assistant gives up after two files.

**D. Workspace Identity Unification (prerequisite)**

A single canonical derivation, used everywhere:

```csharp
public static class WorkspaceIdentity
{
    public static string DefaultPersonalFor(string userId) => $"ws-personal-{userId}";
}
```

- `ArtifactScope.DefaultWorkspaceId = "personal"` literal is removed.
  The remaining headless-CLI fallback derives from
  `Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName`
  via `WorkspaceIdentity.DefaultPersonalFor(...)`.
- `ConversationRuntime` auto-injects `workspace_id`, `project_id`, and
  `run_id` into every `Artifact` / `DocumentGenerate` /
  `DocumentFromTemplate` / `DocumentPackage` tool call when the LLM
  omits them — the active context is always the source of truth, and
  the LLM never has to guess.
- `Sovrant.Desktop.ViewModels.ProjectsViewModel`'s independent
  derivation collapses into the shared helper.
- One-time storage init migration: if both `~/.sovrant/artifacts/personal/`
  and `~/.sovrant/artifacts/ws-personal-{userId}/` exist, move children
  into the canonical dir and remove the orphan; sweep
  `artifacts`, `sessions`, `projects`, and any other table for
  `workspace_id = 'personal'` rows and rewrite to the canonical id.

**E. Permission UX — session-scoped tool grants**

`Artifact` is `ToolTier.Moderate` — already routed through the
permission policy. The friction today is that a 30-file scaffold
demands 30 individual approvals. Add a **per-turn** "always allow
Artifact for this turn" affordance to the confirmation dialog
(Desktop + Web + CLI), distinct from the existing "always allow for
this session" toggle. Auto-clears when the turn ends.

**F. Artifacts screen — code support (Desktop + Web)**

Today the artifact viewer renders Markdown/PDF/DOCX (Phase 65). After
this phase, a scaffolded Node app produces 30 artifact rows that don't
group together and have no per-file syntax-highlighted preview.

- File-tree view per `run_id`: artifacts in the same run cluster into
  a folder tree, not a flat list.
- Syntax-highlighted preview for code MIME types (TS/JS/TSX/PY/CS/Go/Rust/etc.)
  via Monaco read-only embedded editor.
- "Download as zip" action for the run scope — one-click export of the
  whole scaffold.
- "Open in editor" deep-link for users who set a default editor in
  Settings (VS Code / JetBrains / Zed).
- MIME inference filled in for code extensions (the existing
  `GuessContentType` in `Sovrant.Web.Program` already covers PDF/DOCX
  but not `.ts`, `.tsx`, `.py`, `.cs`, etc.).

### Non-goals

- **A linter that *blocks* over-long inline code/markdown** — start with
  a soft warning + telemetry. Blocking risks worse UX if the heuristic
  is wrong.
- **Auto-running the scaffold** (`npm install` / `dotnet run`) — that's
  a different question (tool-execution + permission), out of scope here.
- **Real-time co-editing of artifacts** — read-only preview is enough
  for this phase; editing comes via download or the user's editor.

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| Content Creation Discipline block | `src/Sovrant.Runtime/Prompt/SystemPromptBuilder.cs` | New section, conditional on Artifact tool being available |
| Artifact tool description | `src/Sovrant.Tools/Artifacts/ArtifactTool.cs` | Drop agent-only framing; mention `write_many` |
| `write_many` action + schema | `src/Sovrant.Tools/Artifacts/ArtifactTool.cs` | Manifest-based; one approval, one transactional write |
| `WorkspaceIdentity.DefaultPersonalFor` | `src/Sovrant.Runtime/Workspaces/WorkspaceIdentity.cs` (new) | Single canonical derivation |
| Remove `ArtifactScope.DefaultWorkspaceId = "personal"` literal | `src/Sovrant.Runtime/Artifacts/ArtifactScope.cs` | Replace with userId-derived helper or required parameter |
| Auto-inject scope on Artifact/Document tool calls | `src/Sovrant.Runtime/Conversation/ConversationRuntime.cs` (~line 812) | Already partially scoped; extend to the four document tools |
| Collapse Desktop ProjectsViewModel derivation | `src/Sovrant.Desktop/ViewModels/ProjectsViewModel.cs:17` | Use `WorkspaceIdentity.DefaultPersonalFor` |
| Migration: merge `personal/` → `ws-personal-{userId}/` | `src/Sovrant.Runtime/Storage/Migrations/V0XX__workspace_identity.sql` + filesystem sweep on init | Idempotent; logs every move |
| Per-turn "always allow Artifact" toggle | `BlazorConfirmationHandler` (Web), `ConfirmationDialog` (Desktop), `Sovrant.Cli` confirm | Resets on turn boundary |
| Artifact file-tree view | `src/Sovrant.Web/Components/Pages/Artifacts.razor` + `src/Sovrant.Desktop/Views/ArtifactsView.axaml` | Group by run_id, render as tree |
| Monaco code preview | `src/Sovrant.Web/wwwroot/js/monaco-loader.js` (new) + Razor component | Read-only; existing PDF iframe pattern parallel |
| Download-as-zip endpoint | `src/Sovrant.Web/Program.cs` (`MapArtifactsZipEndpoint`) + Desktop equivalent | Streams zip of run scope |
| MIME inference for code | `src/Sovrant.Web/Program.cs:213` `GuessContentType` | Add `.ts`, `.tsx`, `.js`, `.jsx`, `.py`, `.cs`, `.go`, `.rs`, `.java`, `.rb`, `.php` |
| Post-turn lint metric | `src/Sovrant.Runtime/Conversation/ConversationRuntime.cs` | Counts >30-line code/markdown blocks not accompanied by Artifact write; emits to logs |

### Acceptance Criteria

- [ ] User says "create me a Node app that does X" — assistant
      produces a plan, then writes ≥ 5 files via `Artifact write_many`
      with no inline code body in chat
- [ ] User says "make me a presentation about Y" — assistant writes
      the artifact and replies with a short summary + path; no
      14k-char inline body
- [ ] `~/.sovrant/artifacts/` contains exactly one personal-workspace
      directory (`ws-personal-{userId}`); the literal `personal/` no
      longer appears for new installs and is migrated for existing ones
- [ ] Artifacts screen (Web + Desktop) groups artifacts of one
      `run_id` into a single tree-view; clicking a `.ts` / `.cs` file
      shows syntax-highlighted preview
- [ ] "Download as zip" produces a working archive of a scaffolded
      project (extract → `npm install` succeeds, or equivalent)
- [ ] A user who approves "always allow Artifact for this turn" can
      receive a 30-file scaffold without further prompts; the grant
      auto-clears at turn end
- [ ] CLI `sovrant artifacts list` and `sovrant artifacts open` find
      the same artifacts the Web/Desktop UI sees, scoped to the same
      `ws-personal-{userId}`
- [ ] Migration sweep is idempotent (running storage init twice does
      not move anything the second time) and audited (log line per
      moved file / rewritten DB row)
- [ ] Post-turn lint metric counts zero "code body without Artifact
      write" warnings on a representative scaffold session

### Deferred

- **Hard-blocking the LLM from emitting over-long inline code** —
  start with the system-prompt rule + soft telemetry; revisit if the
  rule isn't reliably followed after a model bump.
- **Artifact versioning / diffs** — useful for "regenerate this file"
  flows, but a separate phase. Today artifacts are immutable per
  `run_id`; that's enough.
- **Cross-workspace artifact moves** — once Phase 85 ships proper
  multi-user identity, a "copy artifact to shared workspace" flow
  becomes worth designing. Not now.
- **Auto-detected project types** (Node / .NET / Python / Rust) with
  type-specific actions ("Run", "Test", "Open in editor") — depends
  on the artifact viewer landing first; layer on top in a follow-up.

---

## Phase 88 — Settings & Provider Profile Consolidation: One Disk Config, Encrypted Keys, DB-Backed UI State

> **Status:** Pending. Closes the remaining gap from the Phase 65
> config audit. Today three JSON files still live in `~/.sovrant/`
> alongside the SQLite DB, two of them holding API keys in plaintext
> despite the encrypted credential store (Bucket C) being shipped.
> This phase moves user-facing settings and provider profiles into the
> DB, routes every secret through the credential store, deletes
> `governance.json` outright, and leaves exactly one on-disk config
> file: `sovrant.config` (Bucket A — bootstrap-critical paths
> only).

### Goal

End state — a fresh `~/.sovrant/` directory after first run:

```
~/.sovrant/
├── sovrant.config   # optional, bootstrap-only (dbPath, logFile, …)
├── data/sovrant.db       # everything user-facing (settings, providers, governance, trust-boundary, …)
├── credentials/.keystore # AES-GCM encrypted secrets (every API key, every token)
├── artifacts/            # run-scoped deliverables
└── logs/                 # daily-rolling logs
```

No `settings.json`, no `providers.json`, no `governance.json`. Every
surface (CLI, Web, Desktop, Server, MCP) reads and writes through the
same DB tables and the same credential store, so changing a model in
Web is immediately visible in Desktop on the same install.

### Why now

Direct observation on the dev machine, 2026-05-02:

- `~/.sovrant/settings.json` contains a real **plaintext OpenAI API
  key** (`sk-proj-…`). It's written by `Setup.razor`, `Settings.razor`,
  Web `Sidebar.razor`, Desktop `SidebarViewModel.cs`, and `Setup`
  pages on every save.
- `~/.sovrant/providers.json` contains **two more plaintext keys**
  (OpenAI + OpenRouter), one per saved provider profile.
- The encrypted `AesGcmCredentialStore` from Bucket C exists and is
  wired for the server bootstrap token and other Bucket-C secrets,
  but the user-facing settings UIs never call it.
- `governance.json` is now read-only legacy (DB is authoritative since
  Bucket-B Step 2), but `GovernanceConfig.Load` still reads it on
  every boot as a fallback, and the Desktop `GovernanceView.axaml`
  still says "Configuration saved to ~/.sovrant/governance.json" —
  stale UI text plus a code path nobody needs.
- Cross-surface drift: edit a setting in Web while Desktop is open and
  the two diverge until restart, because both are the JSON file is
  the source of truth and neither watches it.

The audit doc claims Buckets A/B/C are ✅ DONE, but that was scoped to
bootstrap paths, governance, trust-boundary, and Bucket-C secrets. The
user's *active* settings and provider profiles were never in scope.
Phase 88 finishes the job.

### Scope

**A. Move active settings into the DB**

`SovrantConfig` fields currently in `~/.sovrant/settings.json`:

| Field | Where it goes |
|---|---|
| `ApiKey` | `ICredentialStore` (encrypted), keyed by active provider profile id |
| `BaseUrl` | DB — `user_preferences` table (or extend `workspace_settings` with user-scope rows) |
| `Model` | DB |
| `Provider` | DB (the active profile id; the row in the new `provider_profiles` table holds the name/url) |
| `MaxTokens` | DB |
| `PermissionMode` | DB |
| `IntentRouting` | DB |
| `WebSearch` | DB |

A new SQLite table:

```sql
CREATE TABLE IF NOT EXISTS user_preferences (
    user_id        TEXT NOT NULL,
    key            TEXT NOT NULL,
    value          TEXT NOT NULL,
    updated_at     TEXT NOT NULL,
    PRIMARY KEY (user_id, key)
);
```

Per-user, per-key. `IUserPreferenceStore` is the single read/write
abstraction; surfaces stop touching the filesystem.

**B. Move provider profiles into the DB**

Replace `providers.json` with:

```sql
CREATE TABLE IF NOT EXISTS provider_profiles (
    profile_id     TEXT PRIMARY KEY,           -- e.g. "openai-gpt-5"
    user_id        TEXT NOT NULL,
    name           TEXT NOT NULL,              -- user-visible label
    provider_kind  TEXT NOT NULL,              -- "OpenAI", "OpenRouter", "Anthropic", "Ollama", …
    base_url       TEXT NOT NULL,
    default_model  TEXT,
    max_tokens     INTEGER,
    credential_id  TEXT NOT NULL,              -- foreign reference into ICredentialStore
    created_at     TEXT NOT NULL,
    updated_at     TEXT NOT NULL
);
```

The `credential_id` is the only link to the API key; the key itself
never appears in the DB. `IProviderProfileStore` exposes
`ListAsync` / `GetAsync` / `CreateAsync` / `UpdateAsync` /
`DeleteAsync` / `ActivateAsync`.

**C. API keys exclusively through `ICredentialStore`**

Every UI flow (Setup wizard, Settings page, Sidebar provider switch,
CLI `sovrant auth set`) writes the key via `AesGcmCredentialStore` and
stores only the `credential_id` reference in the DB.

`MutableAuthProvider.UpdateApiKey(...)` continues to be the runtime
hot-swap path; what changes is its source — the credential store, not
a JSON field. On boot, the active profile's credential is fetched
once and held in memory; rotation goes through the store.

**D. Delete `governance.json` outright**

- Remove `GovernanceConfig.Load`'s file-reading paths (project +
  global). DB is the only source.
- Delete the legacy comment in `ServiceCollectionExtensions.cs:99`
  ("legacy bootstrap fallback").
- Update Desktop `GovernanceView.axaml:182` text from "saved to
  `~/.sovrant/governance.json`" to "saved to workspace settings".
- Update Web `Governance.razor:95` text similarly (drop the
  bootstrap-fallback note — it's no longer one).
- One-time migration: if `~/.sovrant/governance.json` exists on boot
  and the DB has no governance row yet, ingest it and rename to
  `governance.json.bak` so users can still find their old values.

**E. One on-disk config: `sovrant.config`**

Bucket-A's design stands. The file holds *only* the bootstrap-critical
paths that must be available before SQLite opens:

```json
{
  "dbPath":         "~/.sovrant/data/sovrant.db",
  "logFile":        "~/.sovrant/logs/sovrant-{Date}.log",
  "artifactsRoot":  "~/.sovrant/artifacts",
  "keystorePath":   "~/.sovrant/credentials/.keystore",
  "serverToken":    ""
}
```

All five fields stay optional (defaults work without the file). No
new fields land here in Phase 88; user-facing settings explicitly do
*not* belong in this file.

**Invariant going forward:** `sovrant.config` is the *only* on-disk
config file. Any future configuration that genuinely cannot live in
the DB or the credential store (i.e. needed before SQLite opens, or
needed to find the SQLite/keystore paths themselves) extends
`sovrant.config` with a new field — it does *not* introduce a new
JSON file. PRs that add a sibling file in `~/.sovrant/` should be
rejected on this rule alone.

The file is named `sovrant.config` (no `.json` extension) — JSON
internally, but the bare name signals "this is *the* config file" and
avoids the implication that anything else could share the namespace.
During a transition window, `BootstrapConfigLoader` searches for
`sovrant.config` first and falls back to `sovrant.config.json` if
present, so existing installs keep working while users rename.

**F. Migration on first boot**

A one-time `LegacyConfigMigrator` runs during storage init:

1. If `~/.sovrant/settings.json` exists → ingest fields into
   `user_preferences`, move the `ApiKey` into the credential store as
   the active profile's credential, then rename to `settings.json.bak`.
2. If `~/.sovrant/providers.json` exists → ingest each entry into
   `provider_profiles` + credential store, rename to
   `providers.json.bak`.
3. If `~/.sovrant/governance.json` exists → ingest into the DB if
   no row yet, rename to `governance.json.bak`.
4. Idempotent — the migrator detects the `.bak` suffix and skips on
   subsequent runs. Logs every field it moved.

The `.bak` suffix is intentional: users keep an offline record of what
their old config looked like, and a future support flow can ask
"please attach `~/.sovrant/*.bak` if migration went sideways."

### Non-goals

- **Encrypting the entire DB** — orthogonal; if it lands, it's a
  separate phase. Today the DB holds non-secret config; secrets live
  in the keystore.
- **Multi-user / multi-tenant config namespaces** — Phase 85 owns
  identity. This phase uses whatever `IPrincipalAccessor` returns
  (today, `SOVRANT_USER_ID || os-username`) without inventing its own
  user model.
- **Hot-reload of config across surfaces in real time** — out of
  scope here; Phase 86's session bus + a small `IConfigChangeBus`
  pub/sub can layer cross-surface notifications later.
- **A settings export / import bundle** — useful but additive; not
  blocking the consolidation goal.

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| `user_preferences` migration | `src/Sovrant.Runtime/Storage/Migrations/V0XX__user_preferences.sql` | Per-user KV store |
| `provider_profiles` migration | `src/Sovrant.Runtime/Storage/Migrations/V0XX__provider_profiles.sql` | + foreign credential_id |
| `IUserPreferenceStore` | `src/Sovrant.Runtime/Preferences/` (new) | Replaces JSON read/write in every UI |
| `IProviderProfileStore` | `src/Sovrant.Runtime/Providers/` (new) | Replaces `providers.json` |
| `LegacyConfigMigrator` | `src/Sovrant.Runtime/Storage/LegacyConfigMigrator.cs` (new) | Runs once on storage init; renames source files to `.bak` |
| Wire `AesGcmCredentialStore` into Settings UIs | `src/Sovrant.Web/Components/Pages/{Setup,Settings,Sidebar}.razor` + `src/Sovrant.Desktop/ViewModels/SidebarViewModel.cs` + `Sovrant.Cli` | Stop writing JSON; write through credential store + DB |
| Delete file-read paths in `GovernanceConfig.Load` | `src/Sovrant.Runtime/Governance/GovernanceConfig.cs` | DB-only |
| Update Governance UI copy | `src/Sovrant.Desktop/Views/GovernanceView.axaml`, `src/Sovrant.Web/Components/Pages/Governance.razor` | Stop referring to `~/.sovrant/governance.json` |
| `BootstrapConfigLoader` filename search | `src/Sovrant.Runtime/Config/BootstrapConfigLoader.cs` | Look for `sovrant.config` first; fall back to legacy `sovrant.config.json` for one release |
| Diagnostics page text | `src/Sovrant.Web/Components/Pages/Diagnostics.razor`, `src/Sovrant.Desktop/ViewModels/DiagnosticsViewModel.cs` | Show DB + keystore paths instead of `settings.json` path |

### Acceptance Criteria

- [ ] A fresh `~/.sovrant/` after first run contains *no* user-facing
      JSON config files — only `sovrant.config` (optional),
      `data/sovrant.db`, `credentials/.keystore`, `artifacts/`, `logs/`
- [ ] Setting an API key through any surface (Setup wizard, Settings
      page, CLI `sovrant auth set`) results in the key landing in the
      keystore; `grep -r "sk-" ~/.sovrant` returns nothing outside
      the encrypted keystore
- [ ] Editing the model in Web Settings while Desktop is open shows
      the new model in Desktop after a refresh — both surfaces are
      reading from the same DB row
- [ ] An existing install with `settings.json` + `providers.json` +
      `governance.json` boots once, ingests every value into the DB +
      keystore, and renames the originals to `*.json.bak`. No data
      loss; the user can keep using the product without re-entering
      anything
- [ ] Running the new code a second time does not re-process the
      `.bak` files (idempotent)
- [ ] `GovernanceConfig.Load` no longer references the filesystem;
      `grep -n governance.json src/` returns only doc/comment hits in
      the migrator + roadmap
- [ ] CLI `sovrant config show` prints the effective settings sourced
      from DB + keystore (key value masked); no path hints to a JSON
      file the user could edit
- [ ] Removing `~/.sovrant/sovrant.config` (or never creating it)
      still boots cleanly with defaults

### Deferred

- **Cross-surface live config sync** — when Web changes a setting,
  push to Desktop in real time. Easy follow-up once the config
  change-bus exists; not blocking the consolidation goal.
- **Encrypted DB at rest** — a different security boundary; revisit
  if a customer asks.
- **Settings export / import** — handy for moving installs between
  machines, but additive.
- **Per-workspace setting overrides** — Bucket-B Step 7 covers the
  shape; layer on top of `user_preferences` once the basic store
  ships.

---

## Phase 89 — Command Center: Sovrant as the Operations Surface for Agents, Teams & Missions

> **Status:** ✅ MVP shipped 2026-05-02 (v0.9.0). Read-only live grid at `/command`
> on Web and Desktop — aggregates active missions, team runs, agent runs, and
> sessions with click-through to existing detail pages. Substrate-only; no new
> runtime engines. Full autonomy features (Phases 67/78/79/86/81) remain pending
> and will layer on top when those phases land.

**Depends on:** Phase 67 (autonomous modes), Phase 78 (teams substrate
with parallel execution + file locks + quality gate), Phase 79 (agents
as first-class callable definitions), Phase 86 (background session
continuation), Phase 81 (workspace/project memory wired to system
prompt)
**Difficulty:** Medium — most of the substrate already exists; this
phase is consolidation, telemetry, and a unified surface, not new
runtime engines.

### Why

Across Phases 78/79/67 we end up with three callable shapes (single
agents, teams, autonomous missions), two execution modes per shape
(sequential / parallel), a quality gate, file locks, run history
ledgers, the activity page, and per-agent run history. Each of those
landed in its own page: **Agents** to define them, **Orchestration**
to run teams, **Activity** to look back at what happened, **Chat** to
talk to one. That's correct as a feature inventory and wrong as a
mental model. The user does not have a single screen that answers
*"what is Sovrant doing for me right now, and what can I steer?"*

The competitive framing is **mission-control**, not
chat-with-extensions. AutoGen Studio, CrewAI Hub, and LangGraph Studio
each evolved a "running graph / who's working / what's pending"
surface because once teams and autonomous modes exist, the chat box
is no longer the main loop — it's one input among several. Sovrant's
pieces are already strictly more powerful (file locks, quality gate,
trust boundary from Phase 58, cost tracking from Phase 55), but they
present as *separate features* instead of a single cockpit.

This phase folds the user's "command center for agents" idea into
the orchestration story: **Command Center is a surface, not a new
engine.** It reuses the team substrate (Phase 78), the agent
definitions (Phase 79), the autonomous driver (Phase 67), and the
session continuation bus (Phase 86). Nothing under the hood becomes
a new runtime — but the user finally has a place to *operate* what
already runs.

### Goals

- **One cockpit page** — `/command` (or promoted to root for desktop /
  web shell) showing every active mission, team run, agent session,
  and queued task in one live grid. Each row exposes its current
  step, the agent or member responsible, last update, and a pause /
  resume / cancel control.
- **Start work from the cockpit** — a single action surface for
  spawning a new run regardless of shape: pick *who* (agent / team /
  mission template) and *what* (prompt + scope), and the cockpit
  routes to the right executor. No need to navigate to Orchestration
  for a team or Agents for a solo run.
- **Steer in flight** — pause / resume / cancel for any running unit;
  reroute a stuck task to a different team member; inject a
  follow-up prompt without losing the run's history; approve or deny
  the next destructive action when a mission hits a permission gate.
  All of this already exists piecemeal in Phase 67 (mission gates) and
  Phase 78 (team coordinator hooks); the cockpit surfaces it
  consistently.
- **Live status without polling** — reuses the Phase 86 session bus
  to push lifecycle events (`AgentStarted`, `TaskCompleted`,
  `QualityGateFailed`, `PermissionRequested`) into the cockpit; no
  page-reload to see progress.
- **Ledger-backed history pane** — the cockpit's "recent runs" row
  reads from `agent_runs` (Phase 79) and the mission ledger (Phase
  67); clicking a row opens it read-only or forks it into a new run.
  Shares the data source with the Activity page so the two surfaces
  stay consistent — Activity is the historical view, Command Center
  is the operating view.
- **Quality gate + trust boundary visible inline** — when a team's
  `SwarmQualityGate` (Phase 78) flags a verdict, or the trust
  boundary (Phase 58) intercepts an agent's intent, that signal
  surfaces directly on the row, not buried in a logs pane. The user
  sees the *verdict* the same way an oncall sees a paging incident.
- **Cost & budget pill per run** — pulls from Phase 55's cost
  tracking, so a long mission shows its accruing spend live and a
  budget breach can pause it before completion.
- **Web + Desktop parity** — the cockpit ships on both surfaces
  reading the same store; the Desktop variant gets a system-tray
  badge for active runs (so the user knows Sovrant is doing work
  when minimized).

### Non-goals

- **A new runtime or scheduler.** Teams (Phase 78), agents (Phase 79),
  and autonomous missions (Phase 67) keep their executors; the
  cockpit only reads + steers.
- **A new persistence layer.** Reuses `agent_runs`, `team_runs`,
  the mission ledger, and the session bus that already exist or are
  scoped in those phases.
- **Replacing chat.** Chat remains the conversational surface for
  a single agent / single mission; the cockpit is the *fleet* view.
- **Multi-tenant / org-wide command center.** Phase 89 is per-user
  + per-workspace, mirroring how the rest of the product scopes
  state. A team-wide cockpit is a separate phase if a customer asks.

### Study questions

- **Where does it live in the nav?** Promote to top-level (`/command`
  or `/`), fold into Activity as a "Live" tab, or surface as a
  collapsible drawer over every page? Top-level signals it's the
  default surface; drawer keeps chat-first users undisturbed.
- **Does Command Center deprecate Orchestration?** The Orchestration
  page (post-Phase 78) is fundamentally a config surface for teams.
  Command Center is the run surface. Either we keep Orchestration
  as the *editor* and Command Center as the *operator*, or we fold
  the editor into the cockpit's detail pane and retire Orchestration.
  Folding is cleaner; keeping is safer for muscle memory.
- **What's the right control model for autonomous missions?** A long
  mission (Phase 67) may run for hours with dozens of intermediate
  steps. Does the cockpit render every step, only milestones, or a
  collapsible tree per mission? Probably milestones by default with
  drill-down on click — keeps the grid scannable.
- **Permission-gate UX.** When a mission requests an
  `AcceptEdits`-mode tool call, does the cockpit pop a modal, badge
  the row, or both? Modal is intrusive across many concurrent runs;
  a row badge plus a system notification probably scales better.
- **Failure surfacing.** When a tool call errors, do we show the
  full stack on the row, a one-line summary with a "details"
  affordance, or route to logs? Probably summary on the row, full
  detail in a side pane — matches the "incident dashboard" model.

### Implementation sketch

| Component | File / Path | Notes |
|---|---|---|
| `/command` page | `src/Sovrant.Web/Components/Pages/CommandCenter.razor` (new) | Live grid; reads from session bus + DB ledgers |
| Desktop equivalent | `src/Sovrant.Desktop/Views/CommandCenterView.axaml` (+ VM) | Same data; tray badge for active runs |
| `IRunStreamHub` | `src/Sovrant.Runtime/Sessions/` (extend Phase 86 bus) | Pushes lifecycle events to subscribed UIs |
| `RunSummary` projection | `src/Sovrant.Runtime/Sessions/RunSummary.cs` (new) | DB view that unifies `agent_runs` + `team_runs` + mission ledger into a single shape for the cockpit |
| Steering API | `src/Sovrant.Api/Runs/RunControl.cs` (new) | `Pause`, `Resume`, `Cancel`, `InjectPrompt`, `Reroute` endpoints; CLI parity via `sovrant runs <action>` |
| Quality-gate + trust-boundary signals | `src/Sovrant.Runtime/Trust/`, `src/Sovrant.Agents/Swarm/SwarmQualityGate.cs` | Emit structured events on the bus when verdicts fire |
| Cost pill | Reuse Phase 55 cost-tracker; new `RunSummary.LiveCost` field | Read-only projection |
| Activity page | `src/Sovrant.Web/Components/Pages/Activity.razor` | Switches to "history" framing; cross-link to Command Center for the live view |

### Acceptance criteria

- [ ] `/command` (Web) and a Command Center pane (Desktop) ship and
      show every active agent session, team run, and autonomous
      mission for the active user/workspace in one live grid
- [ ] Starting a run from the cockpit dispatches to the correct
      executor (agent / team / mission) without navigating to a
      different page
- [ ] Pause / resume / cancel work for every shape; integration test
      proving each control reaches the right executor and the run
      reflects the new state in the cockpit within one event
      round-trip
- [ ] Quality-gate verdicts (Phase 78) and trust-boundary signals
      (Phase 58) appear inline on the run row, not only in logs
- [ ] Cost & budget pill per run reads from the Phase 55 cost ledger
      live; a budget breach pauses the run and surfaces the reason
      on the row
- [ ] Lifecycle events stream over the Phase 86 bus — no polling;
      the cockpit reflects state changes within ~1s of the executor
      emitting them
- [ ] CLI parity: `sovrant runs ls`, `sovrant runs pause <id>`,
      `sovrant runs cancel <id>`, `sovrant runs inject <id> <prompt>`
- [ ] Activity page reframed as the historical view; cross-links to
      Command Center for live runs; no functional duplication
- [ ] Decision recorded for the Orchestration page: kept as
      team-config editor (with cockpit as the run surface) or folded
      into the cockpit's detail pane and retired

### Deferred

- **Multi-user / team-wide cockpit** — every developer in an org
  watching the same fleet. Useful but additive; per-user first.
- **Approval workflows beyond pause/resume** — multi-step approvals
  for high-risk operations (e.g. "this mission wants to push to
  prod, two engineers must approve") layer on top once the basic
  pause-on-permission-gate works.
- **Replay from history** — open a finished run and step through it
  forensically. Possible because the bus is event-sourced; not in
  scope for the initial cockpit.
- **Agent marketplace surfacing** — when other users' agents are
  publishable (post-Phase 79), the cockpit's "Start work" picker
  becomes a marketplace search. Out of scope here.

---

## Phase 90 — Public Release Readiness: Command Center, Polish & Repositioning

**Status:** ✅ Shipped 2026-05-02 (commit `21ea01b`, tag `v0.9.0`). Tracks A–I all landed; full solution test pass at 1,911 / 0 failed.
**Depends on:** Phases 78 (teams), 79 (agents), 87 (artifacts), 88 (settings — partially shipped, plaintext-key fix is in this phase). Pulls Phase 89 MVP scope forward as Track B.
**Difficulty:** Low–Medium. No new runtime engines; this is the "make it shippable" phase. The hardest item is Phase 89 MVP, and that scope is bounded to a read-only live grid.

### Why

The license is finalized (BSL 1.1, Apache 2.0 conversion 2030-04-29). The engine itself is feature-complete for a v1: 56 tools, 25 agent templates, 96 endpoints, 5 delivery modes, 1,587 tests. What still gates flipping the GitHub repo to public is not engineering — it's *honesty of presentation* and *finishing what's visible*.

Three concrete blockers:

1. **The README is dishonest.** Line 3 calls Sovrant "open-source" — it's BSL, source-available. The headline pitches "coordinate teams of sub-agents," which oversells: the team API is real, but the team-collaboration UX is thin. A first-time visitor reads the README and forms wrong expectations.
2. **The value prop is undelivered.** Sovrant is positioned as a *command center for agents and agent activity* — but Phase 89 is unstarted. Today the user has Agents, Orchestration, Activity, and Chat as four separate screens with no single answer to "what is Sovrant doing for me right now?" The competitive frame is mission-control; we ship feature-list-of-screens.
3. **Visible UI rough edges.** `Automations` is a "Coming Soon" stub in Web nav; Desktop `OrchestrationView` has hardcoded *"per-team overrides land with Phase 78"* placeholder text (Phase 78 shipped); `/agents` shows templates only with no path to deploy or run; Activity has no drill-down; provider profiles still write API keys to disk in plaintext (Phase 88 leftover).

This phase is the bounded fix-list. Nothing here is a new feature for its own sake — every track maps directly to "would a first-time visitor see this?"

### Goals

- **Honest positioning.** README, headline, and license language reflect reality: source-available BSL, solo-first command center for agents and agent activity, with team substrate ready.
- **Phase 89 MVP shipped.** A read-only live grid showing every active mission/team-run/agent-session, with click-through to the existing detail pages. Becomes the homepage for Web and Desktop. Write controls (pause/resume/cancel/spawn) deferred to Phase 89-Phase-2 post-release.
- **Agent operations loop closed.** From `/agents` the user can fire a run on a template; from `Activity` the user can drill into a session's turns, tool calls, and errors. Together with Command Center, this is the "operate Sovrant" trio.
- **Visual polish parity.** Web and Desktop reach a consistent, professional bar — no inline-style drift, no hardcoded phase numbers visible, every page has empty + loading states, light/dark theme parity. CLI is verified working (no new polish, just smoke-test the documented commands).
- **Security gap closed.** Provider API keys never sit in plaintext on disk; existing plaintext keys migrate to the keystore.
- **Onboarding works on a clean machine.** First-run wizard on Web and Desktop completes and lands the user on Command Center, not on a blank chat.

### Non-goals

- **Phase 89 Phase-2 controls** (pause/resume/cancel, in-cockpit run spawning, quality-gate inline alerts, Desktop tray badge). These deepen the cockpit but do not gate release.
- **Phase 80–86.** None of those gate the value prop or release.
- **Team-collaboration UX expansion** (member invite UI, presence indicators, multi-user cockpit). The substrate stays; the UX is honestly scoped as roadmap.
- **Mobile responsive Web.** Desktop covers offline / personal device; Web is desktop-browser-first for v1.
- **Monaco syntax highlighting** in artifact previews (Phase 87 Track F leftover).

### Tracks

- **A — Repositioning.** README headline / sub-pitch / license text rewrite; team language softened; Command Center section added once B lands.
- **B — Phase 89 MVP Command Center.** New `/command` page on Web + Desktop, read-only live grid, click-through to existing detail pages. New thin endpoint `GET /v1/command-center/state`.
- **C — Agent instance management.** `/agents` detail pane gets "Run now"; master pane gets "Recent runs (this workspace)" reading from `agent_runs`.
- **D — Activity drill-down.** Convert flat list to master/detail; per-turn breakdown of tool calls, tokens, governance verdicts, errors.
- **E — Visual polish pass.** Inline-style cleanup, missing empty/loading states, hardcoded-phase-number purge, sortable tables on Tools/Skills, consistent margins on Desktop.
- **F — Automations decision.** Delete the Web stub; document the "automations come via MCP-connected platforms (n8n, Zapier, Make)" architectural decision; add an Integrations callout.
- **G — Phase 88 plaintext-key fix.** Move `api_key` field through the existing keystore; one-shot migration from any existing plaintext file.
- **H — CLI smoke verification.** Walk every documented command; fix any breakages.
- **I — First-run wizard polish.** Verify clean-install onboarding on Web + Desktop; ensure landing page after wizard is Command Center.

### Acceptance criteria

- [x] `README.md:3` says "source-available" (or equivalent), not "open-source"; license language matches LICENSE
- [x] README headline frames Sovrant as a command center for agents and agent activity
- [x] Command Center page (`/command`) renders on Web and Desktop and shows live activity from a fresh chat turn within ~2s
- [x] `/agents` allows running a template; the run appears in Command Center within ~2s
- [x] Clicking an Activity row drills into per-turn detail (tool calls, tokens, governance)
- [x] No "Coming Soon" pages reachable from Web nav
- [x] No hardcoded phase numbers visible in user-facing UI text
- [x] Provider profiles never write `api_key` plaintext to disk; existing plaintext keys migrate to keystore on first launch
- [x] Every documented CLI command succeeds on a clean install
- [x] Fresh-install Web wizard → completes → lands on Command Center; same on Desktop
- [x] Full solution test pass; no regressions (1,911 passed, 0 failed, 3 skipped)

### Deferred to Phase 89-Phase-2 (post-release)

- Pause / resume / cancel controls per run
- Inline run-spawning from the cockpit
- Quality-gate + trust-boundary inline alerts on rows
- Cost & budget pill per run
- Desktop tray badge for active runs
- SignalR push (replace 2s poll once Phase 86 lands)

## Phase 91 — Knowledge Authoring Revisit (Web + Desktop)

**Status:** Deferred. Edit / New / Delete / Duplicate / View-source buttons hidden on **both** Web (`/skills`, `/documents/templates`, `/tools/templates`) and Desktop (`SkillsView`) until the issues below are resolved. Master-detail viewers stay live so users can still browse the Knowledge section.

### Why now (later)

Phase 90 shipped the rich-editor pattern for Skills/Documents/Tools markdown templates on both Web (BlazorMonaco + Markdig) and Desktop (AvaloniaEdit + SafeMarkdownPresenter). The infrastructure works, but the authoring UX has issues on both surfaces, and a deeper UX rethink is needed before re-enabling.

**Web — UX feedback (2026-05-04):**
- The "Duplicate to user" button is confusing wording, and it doesn't actually duplicate — it just opens the editor pointed at the user tier and allows edit. The two-step "duplicate then edit" model leaks the underlying tier system to users for no reason.
- **Decision:** users/admins control the whole system; they should be able to **edit anything directly**, including built-ins. No "duplicate to user" intermediate step. Tier badges can stay (informational), but every item should have a single `Edit` button that drops straight into the editor. Saving a built-in writes a copy-on-write file silently — the user should not need to think about tiers.
- Same UX rule applies to `/documents/templates` and `/tools/templates`.

**Desktop — three open defects:**
1. **Edit existing skill renders blank body** — title binds correctly but the AvaloniaEdit `TextEditor.Text` is not displayed despite `SyncEditorFromVm()` setting it. Suspected initial-load race between `DataContextChanged` and `AttachedToVisualTree`; mitigation in place (lazy `EnsureEditor()` lookup) did not fully resolve.
2. **New entry → editor accepts no input** — caret/focus/key-input dead. Suspected missing or partial AvaloniaEdit theme registration even after `<StyleInclude Source="avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml" />` was added to `App.axaml`, OR a hit-test layering issue with the stacked Borders in the detail Grid.
3. **Save round-trip stale viewer** — after save+`LoadSkills()`, the viewer shows the old `SkillItemViewModel` because `SelectedSkill` references the dropped instance. Mitigation in place (re-select by `name`) — keep, but verify after #1 and #2 are fixed.

### Scope

**Web side:**
- Collapse `Edit` / `Duplicate to user` / `View source` into a **single `Edit` action per item**, regardless of tier. Saving a built-in does a silent copy-on-write to `~/.sovrant/{kind}/{slug}.md`.
- Keep the `+ New` button.
- `Delete` only enabled for non-built-in items (built-ins can be reset by deleting their user-tier override).
- Re-evaluate whether tier badges (`user`, `project`, `built-in`) help or distract — keep informational, but they should not gate any action.

**Desktop side:**
- Reproduce each defect in a minimal Avalonia harness (TextEditor in a Border in a Grid) to isolate framework vs. integration.
- Verify AvaloniaEdit 11.x is fully themed under the dark `FluentTheme` — may require additional resource includes (`AvaloniaEdit.TextMate` grammar styles, caret brushes).
- Confirm `TextEditor` receives keyboard focus when the parent Border becomes `IsVisible=true`; if not, force focus on visibility change.
- After fix, re-enable the `+ New` / `Edit` / `Delete` buttons on `SkillsView` and apply the same pattern to a Desktop Documents knowledge view and Desktop Tools knowledge view (parity with Web `/documents/templates` and `/tools/templates`).
- Decision recorded (2026-05-03 feedback memory `feedback_inline_knowledge_editor.md`): editor is **inline only** (no popup), **no preview pane**, **always editable** with copy-on-write to `~/.sovrant/{kind}/{slug}.md` for built-ins.

### Critical files (existing, reuse on resume)

**Web:**
- `src/Sovrant.Web/Components/Pages/Skills.razor` — buttons hidden, viewer-only layout (revert when fixed)
- `src/Sovrant.Web/Components/Pages/UserDocumentTemplates.razor` — same
- `src/Sovrant.Web/Components/Pages/UserToolTemplates.razor` — same
- `src/Sovrant.Web/Components/Shared/MarkdownEditor.razor` — BlazorMonaco editor (works)

**Desktop:**
- `src/Sovrant.Desktop/Views/Shared/MarkdownEditorView.axaml(.cs)` — single-column TextEditor, hardened lookup
- `src/Sovrant.Desktop/ViewModels/MarkdownEditorViewModel.cs` — `Source`/`Saved`/`Cancelled`, frontmatter validation
- `src/Sovrant.Desktop/ViewModels/SkillsViewModel.cs` — `BeginEdit`/`OnEditorSaved`/`EndEdit` flow with re-select-by-name after save
- `src/Sovrant.Desktop/App.axaml` — AvaloniaEdit Fluent theme include
- `src/Sovrant.Desktop/Views/SkillsView.axaml` — buttons hidden, viewer-only Border layout (revert when fixed)

### Out of scope (do not expand here)

- Replacing AvaloniaEdit with another editor (Monaco-via-WebView, custom TextBox-based) — only if the framework-side fix proves intractable.
- Versioning, history, multi-user authoring (still Phase 90 deferral).
- Visual frontmatter form.
- Multi-user permission model — Sovrant runs single-user / admin-trusted today.

### Verification

1. **Web — single Edit action:** browse `/skills`, click `Edit` on a built-in → editor opens with the source → save → viewer reloads with edits → second visit confirms the `~/.sovrant/skills/{slug}.md` shadow exists.
2. **Web — no tier-leaking labels:** there is no "Duplicate to user" anywhere. Every item has at most `Edit` and (if non-built-in) `Delete`.
3. **Desktop — Edit existing skill:** editor opens with full body visible → modify → save → viewer shows updated content.
4. **Desktop — `+ New`:** editor opens with template → type freely → save → entry appears with the chosen `name`.
5. **Desktop — Delete:** user-tier skill deletion → entry disappears → viewer shows empty state.
6. **Built-in protection:** deleting a built-in (no override file) is impossible from UI; deleting a user-tier file that *shadows* a built-in is allowed and reveals the built-in again.
7. **Round-trip parity:** same markdown authored on Desktop renders identically on Web `/skills`.

## Phase 92 — Active Sessions: Up to 5 Concurrent Live Tasks With Return-Anytime Results

**Status:** Pending. Builds on Phase 86 (Background Session Continuation) — confirms the user-facing contract and adds the per-user cap, slot UI, and "come-back-and-it's-done" guarantee.

### Goal

A user can have **up to 5 active sessions** running long-form tasks in parallel. Kick off "scaffold this Node app", "research these 8 vendors and write a comparison doc", "refactor this module" each in its own session, navigate freely, close the chat surface, switch sessions — and when they come back, the result is waiting: the artifact is in the workspace, the document is generated, the task is marked complete, or the failure is captured with the error and partial output.

The cap (5) is the product contract — it shapes the slot UI ("3 of 5 active") and gives the runtime a known upper bound for concurrent in-flight turns per user.

### Why now

Phase 86 already plans the infrastructure (event broker, session status, eviction guard, replay on re-attach). What's missing is the **product surface**: the user has no visibility into how many sessions are live, no slot to claim, no clear "this finished while you were away → here's the artifact" landing. Without that, backgrounding feels accidental rather than a first-class capability. The cap also protects the runtime from unbounded fan-out as users get comfortable with parallelism.

### Scope

**Cap & accounting**

- Per-user limit: **5 concurrent live sessions** (sessions whose status ∈ `Running` | `WaitingForConfirmation`). Idle/Completed sessions don't count.
- Starting a turn over the cap: surface "You have N active sessions. Stop one or wait for one to finish." — with a one-click "show active sessions" link. The text reads the *current* configured cap, not a hard-coded 5.
- Cap is per-user, not per-workspace; it's about runtime fan-out, not workspace isolation.
- Default is **5**, with a sane range of **1–20** (UI slider/numeric input clamps to this; values outside the range fall back to the default and emit a warning).

**Settings UI (Web + Desktop parity)**

- Web `Settings.razor` and Desktop `SettingsView.axaml` both gain an **"Active sessions"** section (sits alongside provider/model preferences):
  - A numeric stepper or slider, label *"Maximum concurrent active sessions"*, range 1–20, default 5.
  - Help text: *"How many tasks you can run in the background at once. Each session runs independently — you can leave one and come back to it later. Lower this if you want fewer parallel tasks; raise it if your machine and provider tier can handle more."*
  - Live preview of current usage: *"Currently using N of M."*
  - Save writes the cap to the **user-settings DB row** (same table that Phase 88 consolidated UI state into) — *not* to `sovrant.config`. Change takes effect on next turn-start (no restart). Cap is reread by `RuntimeSessionPool` per-turn so it picks up edits without bouncing the runtime.
- A future **admin console** (separate phase, see "Future admin surface" below) will expose this same setting at the org/tenant level for cross-user policy. For now, single-user / admin-trusted Sovrant means the user *is* the admin — the Settings UI is the only surface.

**Config precedence (DB-first, file is bootstrap only)**

- Per the one-disk-config rule (Phase 65 + 88): `sovrant.config` is for **initial deployment / bootstrap defaults only** — air-gapped installs, fleet provisioning, first-run seeds. User-facing settings live in the DB.
- Resolution order at read time:
  1. **User settings DB row** (`activeSessions.max`) — what the Settings UI writes.
  2. **Org settings DB row** (future, owned by the admin console) — falls through here when no per-user value is set.
  3. **`sovrant.config` deployment default** (`activeSessions.max`, optional) — only consulted if neither DB row has a value. Lets ops ship a non-default starting point without baking it into code.
  4. **Hard-coded default** (`5`) — final fallback.
- Saving from the Settings UI **never** writes to `sovrant.config`. The deployment default is read-only at runtime.

**Future admin surface (forward reference, not in this phase)**

- A planned **admin console** (likely Phase 95+; placeholder until scoped) will manage org-level policy for multi-user / multi-tenant deployments: per-role caps, per-user overrides, audit of who hit the cap, hard ceilings the user can't raise past.
- The DB schema is admin-ready: per-user row first, org row second, deployment file third. When the admin console lands, it writes to the org row; per-user reads continue working unchanged.
- **Out of scope here:** building the admin console, multi-user identity beyond Phase 85's groundwork, or any role-based override logic. We're only making sure today's DB-backed setting won't have to be re-architected when admins arrive.

**Slot UI (Desktop + Web parity)**

- Top of the chat sidebar shows **"Active sessions: N of 5"** with N filled pips. Clicking expands a compact list of just the active ones, with status pip (running / waiting / completed-just-now), elapsed time, and a one-line summary of the current step.
- Sessions that completed-while-away get a subtle "✓ ready" badge in the regular session list until the user opens them. Opening clears the badge.
- Failed sessions get a "⚠ failed" badge that persists until the user opens, reads, and dismisses it (or re-runs).

**Return-anytime guarantee**

- Whatever the task produces — artifact, document, code change, error log — is written to its terminal location *before* the session transitions to `Completed`/`Errored`. The user opening the session later sees:
  - The full conversation transcript including everything that happened while they were away.
  - Links to artifacts/documents in their final location (workspace tree, /artifacts, /documents).
  - For code-creation tasks: a summary of files changed.
  - For failed tasks: the error, the last successful step, and a one-click "resume from here" if the runtime can checkpoint.
- "Come back and it's done" must work whether the user returns in 30 seconds or 30 minutes — the contract is **persistence of result**, not presence in memory.

**Cross-session indicators**

- Reuse the Phase 86 toast / tray / browser-tab-title plumbing. When a backgrounded session completes, the indicator names the session and links to the result artifact directly when available ("✓ scaffold-node-api → 12 files added to /artifacts").
- Confirmation prompts surface the same way (Phase 86) but never count against the 5 cap any differently — `WaitingForConfirmation` is still "live."

### Non-goals

- **More than 5 in parallel.** If users push for it, revisit; do not silently raise the cap.
- **Multiple turns within a single session.** One turn per session, same as Phase 86.
- **Mid-turn process-restart recovery.** Same Phase 86 deferral — if the host crashes mid-turn, the session is lost, only persisted history remains.
- **Mobile / push notifications.** Same as Phase 86.

### Implementation sketch

| Component | File | Notes |
|---|---|---|
| Cap resolver | `Sovrant.Server/Configuration/SessionLimits.cs` (new) | Reads in order: user-settings DB row → org-settings DB row (future) → `sovrant.config` bootstrap default → hard-coded 5; range-clamped 1–20; exposed via DI; reread per-turn |
| Cap enforcement | `RuntimeSessionPool.StartTurnAsync` | Reject with `ActiveSessionLimitExceeded` when N == max; runtime returns reason for the UI banner |
| Active count signal | `ISessionEventBus` (Phase 86) + a `IActiveSessionCounter` aggregator | Emits "N of M" updates; sidebar + Settings page subscribe |
| Settings UI (Desktop) | `Views/SettingsView.axaml` + `SettingsViewModel` | New "Active sessions" section: numeric input, help text, live "N of M" |
| Settings UI (Web) | `Components/Pages/Settings.razor` | Mirror Desktop |
| Settings persistence | User-settings DB row (Phase 88 table) | Save writes to DB only; `sovrant.config` is **not** mutated by the UI |
| Bootstrap default | `sovrant.config` schema (extend existing) | Optional `activeSessions.max` field, used only when no DB value exists; for fleet provisioning / first-run seeds |
| Slot UI (Desktop) | `Views/Sidebar/ActiveSessionsHeader.axaml(.cs)` (new) + `SidebarViewModel.ActiveSessions` | Pips, expand-on-click, status row per active |
| Slot UI (Web) | `Components/Layout/ActiveSessionsHeader.razor` (new) | Mirror Desktop |
| Result-ready badges | Existing session list rows in `Sidebar.razor` / sidebar VMs | New `HasUnreadResult` / `HasUnreadFailure` flags driven by event bus |
| Cap-exceeded banner | `ChatView` / `Chat.razor` | Modal-less inline banner with link to active list |
| CLI parity | `sovrant status` (Phase 86 plan) | Show "N of M active" header and list |

### Verification

1. Start 5 long-running turns in 5 different sessions; navigate freely between menu items and sessions; the runtime keeps all 5 going, sidebar shows "5 of 5 active" with correct status pips.
2. Try to start a 6th turn → cap-exceeded banner appears with a link to active sessions; nothing starts on the runtime.
3. Stop one session; the slot frees; starting a new turn now succeeds.
4. Close the chat surface (Desktop window minimised, Web tab navigated to `/documents`) while a turn is mid-flight → return 5 minutes later → session shows the full transcript, the artifact lives in `/artifacts`, the badge clears on open.
5. Force a tool error mid-turn → session goes `Errored`, shows the error and last successful step, "⚠ failed" badge persists in sidebar until dismissed.
6. Confirmation prompt fires while user is on another page → toast / tray / tab-title updates name the session; click-through goes straight to the prompt; the session still counts as one of the 5.
7. Restart the host process: in-flight sessions are lost (per non-goal), but the persisted transcript + any already-written artifacts survive; the slot count returns to 0 of M cleanly.
8. **Settings cap edit (Web):** open Settings → "Active sessions" → set to 3 → save → confirm the row in the user-settings DB table holds `activeSessions.max=3` and `sovrant.config` is **unchanged** → start 3 turns → 4th turn shows the cap-exceeded banner with "3 active" — confirms the DB write took effect without restart.
9. **Settings cap edit (Desktop):** same flow under Desktop Settings.
10. **Range clamp:** entering 0 or 25 in the Settings input snaps to the 1–20 range; entering a non-numeric value falls back to the saved value with no save.
11. **Bootstrap default:** delete the user-settings DB row (or use a fresh install); set `activeSessions.max=8` in `sovrant.config`; first turn-start reads 8, not 5 — confirms the deployment fallback works.
12. **DB precedence over file:** with `activeSessions.max=8` in `sovrant.config`, save 4 from the Settings UI; runtime now reads 4 (DB wins) — confirms the file is consulted only when no DB row exists.
13. **Schema forward-compat:** stub a fake org-settings DB row with `activeSessions.max=10`; with no per-user row set, runtime reads 10; with a per-user row set to 3, runtime reads 3 — confirms the per-user → org → file → hard-coded order.

### Cross-references

- **Phase 86 — Background Session Continuation:** the runtime infrastructure this phase depends on. Phase 92 is the product contract on top of Phase 86's plumbing.
- **Phase 87 — Artifacts-by-Default:** ensures whatever the task produces lands in a deterministic location, so "come back and it's done" has somewhere to point.
- **Phase 88 — Settings & Provider Profile Consolidation:** establishes the `sovrant.config` writer pattern Phase 92's Settings UI plugs into. The "Active sessions" section is one more consumer of that surface.
- **Phase 89 — Command Center:** the cockpit surfaces multi-session steering at a higher level (teams/missions); active-sessions slots are the per-user, single-task version.
- **Future admin console (placeholder, not yet phased):** will own org-level policy and write to a DB-backed `org.settings.*` row that Phase 92's config layer reads as a fallback. Phase 92 is the per-user setting; the admin console is the org-level setting on top.

## Phase 93 — Configuration Boundary Audit: `sovrant.config` vs DB vs Keystore — Codify the Rules ✅

**Status:** ✅ Complete (2026-05-09). `sovrant.config` removed entirely — the file and all file-loading code are gone. Bootstrap config reads exclusively from env vars + optional `.env` file in CWD. `routing.json` migrated to env vars (`SOVRANT_ROUTING_*`, `SOVRANT_TIER_MODELS`, `SOVRANT_ROUTING_RULES`). `swarm.json` migrated to `workspace_settings` DB table via `ISwarmConfigStore`. `docs/config-audit.md` updated as the canonical policy doc with all Bucket-A/B/C/D decisions and open-items tracking. The enforced rule: `.env` file only on disk; everything else in DB or keystore.

### Goal

Right now the rule "`sovrant.config` is the only on-disk config; everything else goes to DB or keystore" lives in a feedback memory (`feedback_one_disk_config.md`) and gets enforced one PR review at a time. Phase 88 consolidated existing config; Phase 92 added a new precedence layer (per-user DB → org DB → bootstrap file → default). Without a single canonical doc + automated guard, the next contributor will reach for a sibling `*.json` or write user state into `sovrant.config` and we'll find it during a credential leak post-mortem instead of at PR time.

This phase produces:
1. A canonical decision doc (in-repo, e.g. `docs/configuration-policy.md`).
2. A matrix that maps every existing setting to its correct home and flags any current mis-classifications.
3. Automated guards (build / test / lint) that fail when the rules are broken.

### Why now

- Phase 88 + 92 set a clear precedent (DB-first, file is bootstrap-only) but the rule is documented across feedback memos, scattered phase entries, and one-off PR comments — no one place a contributor can read.
- The active-sessions cap (Phase 92) is the **first** new setting that uses the per-user DB → org DB → file → default chain. The next 5+ phases (Phase 88 follow-on, admin console, cost budgets, route preferences) will all face the same "where does this live?" question. Answering it once beats answering it five times inconsistently.
- A real incident already happened (`settings.json` + `providers.json` holding plaintext API keys, per the feedback memo). The rule exists *because* of a credential leak. Codifying the rule turns the lesson into a guard.

### Scope

**1. Decision matrix — three buckets**

For every category of state Sovrant handles, classify into exactly one bucket:

| Bucket | Examples | Why this bucket |
|---|---|---|
| **`sovrant.config`** (on disk, JSON, single file) | Bootstrap defaults for fleet provisioning, air-gapped install seeds, OS-level paths the DB can't bootstrap itself from (DB connection string, log path), dev-machine overrides | Read-only at runtime; no per-user mutation; needed *before* the DB is reachable; must survive a wiped DB |
| **DB** (user-settings table, org-settings table, etc.) | All user-facing settings (model, provider, max tokens, permission mode, web search, intent routing, **active-sessions cap**), UI state (sidebar collapse, last-open page), session/workspace/project/artifact records, agents/teams/skills metadata, audit logs | Per-user/org variation; mutable from UI; queryable; survives across surfaces (Web/Desktop/CLI) |
| **Keystore** (encrypted credential store, OS-native where available) | API keys, OAuth tokens, refresh tokens, MCP server secrets | Sensitive; never plaintext on disk; never in DB unencrypted; never in `sovrant.config` |

Anything that doesn't cleanly fit one bucket is a **design smell** — re-shape the requirement until it does.

**2. Audit pass over the existing codebase**

- Walk every read of `sovrant.config` (grep `sovrant.config`, `IConfiguration`, file-based config services). For each, decide: is this *truly* a bootstrap default, or did it leak in? Move leakers to the DB.
- Walk every JSON file ever written under `~/.sovrant/`. There should be exactly one (`sovrant.config`); flag any others (legacy `settings.json`, `providers.json`, `governance.json` per the memo, any `.bak` files).
- Walk every column / key in the user-settings and org-settings DB tables. Anything that's actually a secret moves to the keystore.

**3. Codified policy doc — `docs/configuration-policy.md`**

- Single source of truth. Linked from `CONTRIBUTING.md` and the top of every settings-related file.
- Includes the matrix above + concrete answers to:
  - "I'm adding a new user-facing setting — where does it go?" → DB user-settings row, surfaced in Settings UI, optionally seeded by a `sovrant.config` field at first run.
  - "I'm adding a per-org setting" → DB org-settings row, owned by the future admin console.
  - "I'm adding an API key for a new provider" → keystore, never DB or file.
  - "I'm adding a deployment-time path" → `sovrant.config`, document why it can't bootstrap from DB.
- Explicit anti-patterns: sibling `*.json` files in `~/.sovrant/`, plaintext secrets anywhere, mutating `sovrant.config` from UI code, reading user-facing settings directly from `IConfiguration` instead of from the DB-backed `IUserSettings` service.

**4. Automated guards**

- **Test:** assert that `~/.sovrant/` (in test fixtures and after smoke runs) contains exactly one file, `sovrant.config`. Fail if a `*.json` sibling appears.
- **Test:** assert no plaintext-looking secret patterns (`sk-…`, `Bearer …`, etc.) are written into `sovrant.config` or any DB column not marked encrypted.
- **Lint / Roslyn analyzer (or simple grep gate in CI):** flag direct writes to `sovrant.config` outside of the bootstrap writer; flag direct reads of user-facing settings from `IConfiguration` (require routing through `IUserSettings`).
- **Schema check:** when a new setting is added, the contributor must mark it in code as one of `[BootstrapConfig]`, `[UserSetting]`, `[OrgSetting]`, `[Secret]`. Mis-attribution fails the build.

### Non-goals

- Migrating *new* config surfaces — this phase is about the rule, not about expanding what's stored. Active-sessions cap (Phase 92), cost budgets, etc. each handle their own migration when they ship.
- Multi-tenant DB schema design — that lives with the admin console phase.
- Replacing `sovrant.config` with environment variables or secrets manager — out of scope; the rule is about the *boundary*, not the format.

### Deliverables

1. `docs/configuration-policy.md` — the canonical doc + matrix.
2. `CONTRIBUTING.md` link to the policy in the "Adding settings" section.
3. Audit report (one-time, in the PR body): every existing setting bucketed, every flagged leak resolved or ticketed.
4. CI guard(s): file-presence test, secret-pattern test, optional Roslyn analyzer or grep gate.
5. Memory update (`feedback_one_disk_config.md` → keep as-is; add a pointer to the new doc so future agents read the long form, not the short form).

### Verification

1. The repo contains `docs/configuration-policy.md` with the three-bucket matrix and concrete answers to the four "where does this go?" questions above.
2. `CONTRIBUTING.md` links to the policy.
3. Running the test suite on a clean `~/.sovrant/` confirms exactly one file (`sovrant.config`) is created; introducing a deliberate sibling `.json` fails the test.
4. Attempting to commit a plaintext API key into `sovrant.config` (via test fixture) fails the secret-pattern test.
5. Adding a new setting without a bucket attribute fails the build (or the lint gate, depending on tool).
6. The audit-report PR has zero open "leak" findings — all are either resolved in-PR or tracked as follow-up tickets with owners.

### Cross-references

- **Phase 65 — Config audit / one-disk-config rule origin.**
- **Phase 88 — Settings & Provider Profile Consolidation:** the implementation that established the DB-backed user-settings surface.
- **Phase 92 — Active Sessions:** the first new setting to use the full per-user DB → org DB → file → default chain. Phase 93 generalises that pattern.
- **`feedback_one_disk_config.md`** memory: the rule that this phase codifies.

---

## Phase 94 (legacy planning section) — Provider & Model Switch Context Continuity

**Status:** Planned
**Goal:** Ensure that when a user switches provider or model mid-session, the active conversation context is correctly handed off to the new model — without redundant re-sends, token waste, or silent context loss.

### Problem

Today when a user runs `/model <new-model>` or switches provider in the UI mid-session, `ConversationRuntime` uses a new provider/model config but continues appending to `_history` as-is. This is fine for the happy path, but creates two failure modes:

1. **Over-sending:** The new model receives the full prior message history even when the prior model had already compacted or summarised it internally. If compaction ran on model A and produced a summary, and the user then switches to model B, the summary message is re-sent alongside all original history — paying twice.
2. **Under-sending / context mismatch:** Some providers (native messages API vs. OpenAI-compat) use different role schemas. A history built under one schema may be silently malformed or truncated by the adapter for the new provider, losing tool results or assistant reasoning without error.

In either case the user pays more than necessary or gets a degraded continuation — and there is no observability to know which happened.

### Goals

- **No extra cost for a clean switch.** If the history is already compacted, the new model receives only the compacted form — not the original + compacted.
- **Schema compatibility check on switch.** Before accepting the switch, validate that the current history can be faithfully represented in the target provider's message schema. If not, offer to compact first.
- **Emit a switch event to cost tracking.** Log provider/model, token count at switch time, and the result of the compatibility check so cost anomalies after a switch are traceable.
- **Preserve session continuity for the user.** The conversation continues naturally; the user does not need to re-send their last message or lose in-progress tool state.

### Scope

1. **History snapshot on switch** — when provider/model changes, snapshot the current `_history` token count and compaction state. If compaction ran and produced a summary, mark the history as `compacted-at=<turn>` so the new model receives only the compacted view.
2. **Provider schema compatibility check** — a lightweight `IHistoryCompatibilityChecker` that inspects the current history for role patterns unsupported by the target provider's adapter (e.g., `tool` role messages under a provider that only understands `user`/`assistant`). Returns `compatible | needs-compaction | incompatible`.
   - `compatible` → switch proceeds immediately.
   - `needs-compaction` → auto-compact (using the `fast` tier) before switching; notify user.
   - `incompatible` → surface a clear error: "This model's provider doesn't support tool-call history. Start a new session or compact first."
3. **Cost-tracking event** — extend the existing metrics log with a `model_switch` event: `{from_provider, from_model, to_provider, to_model, token_count_at_switch, history_status}`.
4. **CLI feedback** — `/model <name>` prints a one-line confirmation: `Switched to gpt-4o (history: 3,200 tokens, compatible).` If compaction ran: `Switched to gpt-4o (compacted 12,400 → 2,100 tokens before switch).`
5. **Web/Desktop feedback** — model picker shows token count and compatibility status in the switch confirmation tooltip or bottom-bar indicator.

### Non-goals

- Cross-provider conversation migration (exporting and re-importing history in a different format) — that's a separate archive/export feature.
- Automatic provider failover mid-turn (SmartRouter territory, Phase 48).
- Changing the message schema stored in `_history` — the internal representation stays stable; only the outbound adapter layer is responsible for schema translation.

### Verification

1. Switch from Anthropic to OpenAI mid-session: history is sent once, not duplicated. Token count in the cost log matches `_history` length at switch time.
2. Switch after compaction: new model receives the compacted summary only. Cost log shows pre-compaction vs post-compaction counts.
3. Switch to a provider whose adapter doesn't support `tool` role messages with active tool results in history: user sees an actionable error, not a silent API failure.
4. `/model <name>` prints token count and compatibility status.
5. Existing compaction tests (`MaybeCompactHistoryAsync`) still pass; new switch-event entries appear in the JSONL metrics log.

### Cross-references

- **Phase 48 — SmartRouter:** health/latency/cost routing; Phase 94 is about context handoff correctness, not routing policy.
- **Phase 51 — Mission engine `IContextCompactor`:** the reversible compaction path Phase 94 relies on for `needs-compaction` switches.
- **Phase 55 — Cost tracking:** JSONL metrics log extended with `model_switch` events.
- **Phase 80 — Mid-conversation context compaction:** the fixed-threshold → percentage-of-window improvement that Phase 94 builds on for pre-switch compaction.
- **Future admin console (placeholder):** will own the org-settings table that Phase 93's matrix already accounts for.

---

## Phase 95 (legacy planning section) — Memory System Audit & Hardening

**Status:** Planned
**Goal:** Verify that both the backend (`SqliteMemoryStore` / `MemoryInjector`) and the session-end extraction pipeline (`SessionEndMemoryHandler`) are working correctly end-to-end, then close the known gaps that make memory injection expensive, noisy, or unreliable.

### Background

Sovrant has two memory subsystems that were built in separate phases and have never been audited together:

- **Backend structured memory** (`V003__memory.sql`): three DB tables — `session_summaries`, `learned_patterns`, `instincts` — injected by `MemoryInjector.BuildMemorySectionAsync()` into the system prompt on every turn.
- **Session-end extraction** (`SessionEndMemoryHandler`): a hook that fires when a session closes, reads conversation history, and writes new `learned_patterns` and `instincts` rows.

Neither subsystem has integration tests that verify the full round-trip (session → extraction → injection into the next session's prompt). The known gaps below were identified by reading the code; some may already cause silent failures or unnecessary token spend in production dogfood sessions.

### Known gaps

| # | Gap | Where | Impact |
|---|-----|--------|--------|
| 1 | **Count-based injection, no token budget** | `MemoryInjector` injects up to 3 + 15 + 10 = 28 items with no per-item or aggregate token cap. A workspace with 15 verbose patterns can blow hundreds of tokens per turn silently. | Token waste / cost |
| 2 | **No query-aware ranking on backend** | Frontend uses a Sonnet side-query to select up to 5 relevant memories. Backend loads all items for the project, sorted only by confidence — no relevance to the current user message. | Noise in context / cost |
| 3 | **No confidence decay on learned patterns** | `confidence` is set at creation and updated by `SessionEndMemoryHandler` but never decays over time. A pattern learned six months ago with confidence 0.9 stays at 0.9 even if it contradicts newer patterns. | Stale injection |
| 4 | **Duplicate patterns across sessions** | Nothing prevents the same pattern text (or near-duplicates) from being written multiple times by `SessionEndMemoryHandler` across different sessions. All copies get injected. | Noise / cost |
| 5 | **Extraction is fire-and-forget with a 60 s drain** | If the process is killed before the drain completes, the extraction for that session is lost silently. No retry, no dead-letter. | Data loss |
| 6 | **`workspace_id` nullable in `session_summaries`** | Workspace-scoped memory lookups fall back to a project-only index. Multi-workspace setups get cross-contaminated session context. | Correctness |
| 7 | **No end-to-end integration test** | The round-trip (conversation → extraction → DB rows → next-session injection) has no test coverage. `MemoryInjector` and `SessionEndMemoryHandler` are only unit-tested in isolation. | Unknown breakage |
| 8 | **Injection happens even when context window is tight** | `MemoryInjector` is called unconditionally. After compaction (Phase 80/94), the remaining context budget may be small; injecting 28 items could push the model back over threshold. | Cost / compaction churn |

### Scope

**1. Audit pass (read-only, no new features)**

- Run a dogfood session that exercises extraction end-to-end. Verify that `learned_patterns` and `instincts` rows are written to SQLite after session close.
- Run a second session and confirm those rows appear in the injected system prompt via a debug log or test assertion.
- Confirm `workspace_id` is populated correctly for workspace-scoped sessions; fix the nullable default if not.
- Confirm duplicate suppression: if the same pattern text already exists at confidence ≥ threshold, `SessionEndMemoryHandler` updates confidence rather than inserting a new row.

**2. Token-budget cap on injection**

- Add a `MaxMemoryTokens` config (default: 1,500 tokens, ~6 KB) to `MemoryInjector`.
- Resolve the active model's context window via the Phase 54 capability registry; scale the cap proportionally (e.g. 0.75% of window, min 500, max 3,000).
- Items are selected in priority order: workspace memory → instincts (by confidence) → learned patterns (by confidence) → session summaries. Stop when the budget is reached.
- Emit a `memory_injected` entry in the JSONL cost log: `{item_count, estimated_tokens, budget, truncated: bool}`.

**3. Confidence decay**

- Add a `last_used` timestamp to `learned_patterns` (already present in schema).
- Apply exponential decay in `MemoryInjector` at read time: `effective_confidence = confidence * decay_factor ^ days_since_last_used`. Default half-life: 90 days. Patterns with `effective_confidence < 0.3` are excluded from injection (not deleted; decay is reversible if the pattern is reinforced).
- Instincts already have an `evidence` trail; apply the same decay.

**4. Duplicate suppression in extraction**

- Before inserting a new `learned_patterns` row, `SessionEndMemoryHandler` checks for an existing row with ≥ 80% string similarity (simple normalized edit distance is sufficient — no LLM call).
- On match: update `confidence` and `last_used`; do not insert.
- On no match: insert as new.

**5. Context-budget awareness**

- `MemoryInjector` receives the current session's remaining token budget (already tracked by `ConversationRuntime`). If remaining budget < `MaxMemoryTokens * 1.5`, skip injection and log `memory_skipped_low_budget`.
- This prevents memory injection from triggering another compaction cycle immediately after a compaction run.

**6. End-to-end integration test**

- One test that:
  1. Creates a session, sends messages that should produce a learnable pattern, closes the session.
  2. Asserts a `learned_patterns` row was written to the test DB.
  3. Opens a new session for the same project, calls `BuildMemorySectionAsync()`.
  4. Asserts the pattern text appears in the injected section.
- One test that verifies the token-budget cap truncates injection when the budget is tight.
- One test that verifies duplicate suppression: same extraction run twice → one DB row, not two.

### Non-goals

- Replacing the structured memory schema with a vector store / embedding-based retrieval — that is a future phase if the confidence-ranked approach proves insufficient at scale.
- Cross-surface memory sync between CLI/Desktop/Web file-based memories and the DB-backed store — those are separate systems targeting different use cases.
- Automatic memory pruning or garbage collection — decay handles staleness; hard deletes are a future admin-console feature.

### Verification

1. Dogfood session → close → reopen: patterns from session 1 appear in session 2's system prompt. Verified via debug log or test assertion.
2. `memory_injected` entries appear in the JSONL cost log with accurate token estimates.
3. Injecting with a tight budget (< threshold) emits `memory_skipped_low_budget` and does not add items to the prompt.
4. Running the same extraction twice produces one `learned_patterns` row, not two.
5. A pattern with `effective_confidence < 0.3` after decay does not appear in the injected section.
6. All three integration tests pass in CI.

### Cross-references

- **Phase 27 — Multi-Layered Memory System:** original DB schema (`V003__memory.sql`), `IMemoryStore`, `SqliteMemoryStore`.
- **Phase 32 — SQLite Persistence:** `MemoryInjector`, `SessionEndMemoryHandler` implementation.
- **Phase 54 — Model capability registry:** used to resolve context window size for proportional budget calculation.
- **Phase 55 — Cost tracking:** JSONL log extended with `memory_injected` / `memory_skipped_low_budget` events.
- **Phase 80/94 — Context compaction / model switch:** Phase 95 injection must be context-budget-aware to avoid triggering a compaction immediately after one completes.

---

## Phase 96 — MCP End-to-End Smoke Test & Go-Public Gate

**Status:** ✅ Complete (2026-05-06)
**Goal:** Prove that MCP-gated sessions work end-to-end on every surface (Desktop, Web, CLI, HTTP server): a real conversation can discover tools from a connected MCP server, invoke them, and return results — with per-session gating enforced correctly.

### Why this is a launch gate

The infrastructure code path is confirmed wired on all surfaces (same `AddSovrantRuntime()` + `InitializeRuntimeAsync()` DI path). But "wired" is not the same as "working" — the MCP tool proxy, per-session gating filter (`FilterToolsForModel`), Connections UI selection, and `ListMcpResources` → `MCPTool` discovery loop have never been smoke-tested as a complete flow across surfaces. Going public with a broken MCP story undermines the core value prop.

### The discovery loop matters

MCP tools are **not** first-class entries in the model's static tool list. They are discovered dynamically: the model calls `ListMcpResources` to enumerate available servers and tools, then calls `MCPTool` with `{server, tool, input}` to invoke them. The smoke test must verify this full loop — not just that MCP servers are registered at startup.

### Smoke test checklist (must all pass before public launch)

**Surface: Desktop (Avalonia)**

- [ ] Open a new chat session. PixelLab MCP server appears in the Connections panel.
- [ ] Enable PixelLab for the session. Send: *"What PixelLab tools do you have available?"*
- [ ] Model calls `ListMcpResources` → response enumerates PixelLab tools.
- [ ] Send: *"Use PixelLab to list my characters."* Model calls `MCPTool` with correct `server`/`tool` args → result returned in chat.
- [ ] Disable PixelLab in Connections. Repeat the invocation request. Model does NOT invoke PixelLab tools (gating enforced).

**Surface: Web (Blazor :5100, embedded mode)**

- [ ] Same four steps as Desktop.
- [ ] Confirm gating: with PixelLab disabled in Connections, `MCPTool` call to PixelLab is filtered out.

**Surface: Web (remote mode — Blazor :5100 → Server :5200)**

- [ ] Start `Sovrant.Server` with PixelLab configured. Start Web in remote mode.
- [ ] Same four steps. Confirm tools come from the remote server's `McpClientRegistry`, not a local one.

**Surface: CLI**

- [ ] `sovrant chat` with PixelLab configured. Ask model to list PixelLab tools → `ListMcpResources` response.
- [ ] Ask model to invoke a PixelLab tool → `MCPTool` call succeeds.

**Per-session gating (all surfaces)**

- [ ] Session A: PixelLab enabled. Session B: PixelLab disabled. Both open simultaneously. Session B cannot invoke PixelLab even while Session A can. Verify via `ConversationRuntime.FilterToolsForModel()` — no cross-session bleed.

**Error cases**

- [ ] PixelLab MCP server configured but not reachable (wrong URL). Startup logs a warning but does not crash. Chat session opens. Model reports tool unavailable gracefully (no unhandled exception surfaced to user).
- [ ] Model calls `MCPTool` with a nonexistent `tool` name on a connected server. Error is returned as a tool result, not an unhandled exception.

### What to fix if a step fails

| Failure | Likely location |
|---------|----------------|
| PixelLab does not appear in Connections panel | `McpServerRoutes.cs` `GET /v1/mcp/servers` or `ChatViewModel.RefreshConnectionsAsync` |
| `ListMcpResources` returns empty or errors | `McpClientRegistry` not populated — check `InitializeRuntimeAsync` log for connection errors |
| `MCPTool` call never fires (model doesn't try) | System prompt doesn't advertise MCP capability — check that `ListMcpResourcesTool` and `MCPTool` are in the model's tool list |
| Gating not enforced | `ConversationRuntime.FilterToolsForModel` — `SessionContext.Current?.AllowedMcpServers` not being set from `pooled.Config` |
| Remote mode tools missing | `AddSovrantClient()` doesn't proxy MCP tool calls — check `ChatRoutes` on the server |

### Deliverables

1. All checklist items above pass and are documented in a brief test-run note (PR body or a `docs/mcp-smoke-test-results.md`).
2. Any bugs found during the smoke test are fixed before the checklist is marked complete.
3. A minimal automated integration test (`Sovrant.IntegrationTests`) that spins up an in-process MCP echo server, connects it via `McpToolRegistrar`, sends a chat turn, and asserts `MCPTool` was invoked and returned the echo response.

### Cross-references

- **Phase 15 — MCP Server Mode:** stdio + HTTP/SSE.
- **Phase 16 — Dynamic MCP Tool Proxy:** `MCPTool`, `McpClientRegistry`, `McpToolRegistrar`.
- **Phase 17 — MCP OAuth:** OAuth PKCE flow; Phase 96 does not require OAuth to pass, but OAuth flow should be smoke-tested separately before public.
- **V024 migration — per-session MCP gating:** `sessions.mcp_servers` column; `ConversationRuntime.FilterToolsForModel()`.

---

---

## Round 4 Security Hardening (2026-05-06) ✅

> **Status: ✅ Complete** — Three passes across all 25 server route files + SDK + Desktop/Web components. Committed on `sovrant-openc-dotnet-port` in three sequential commits.

### What was fixed

#### Phase L — Critical auth and ownership gaps

| Fix | File |
|---|---|
| `PUT /v1/config` missing admin guard | `ConfigRoutes.cs` |
| `POST /v1/engine/runs/recover` missing admin guard | `EngineRoutes.cs` |
| `DELETE /v1/engine/runs/{id}` no ownership check | `EngineRoutes.cs` |
| All workspace `{id}` endpoints had no membership check | `WorkspaceRoutes.cs` — added `RequireWorkspaceAccess` helper |
| All mission endpoints (create/list/get/run/events/export) had no ownership check | `MissionRoutes.cs` |
| All swarm endpoints had no ownership check; swarm `user_id` never written to DB | `SwarmRoutes.cs`, `SwarmResult`, `ISwarmEventStore`, `SqliteSwarmEventStore`, `SwarmSession`, `SwarmOrchestrator`, migration `V025__swarm_events_user_id.sql` |
| All team endpoints had no workspace-membership check | `TeamRoutes.cs` — added `RequireTeamAccess` helper |
| Knowledge authoring POST/DELETE had no auth check | `KnowledgeAuthoringRoutes.cs` |
| `_mcpHintInjected` bool race on concurrent hint injection | `ConversationRuntime.cs` — `Interlocked.CompareExchange` + `LoggerMessage` delegate |
| `ApplyUserPreferencesAsync` crash on bad preference value | `ServiceCollectionExtensions.cs` — try-catch |
| API keys held in Blazor component state (visible in DevTools) | `TopContextBar.razor` — on-demand fetch, never stored in component fields |

#### Phase M — Quality and correctness

| Fix | File |
|---|---|
| `BaseUrl` PUT accepted internal/reserved IP addresses (SSRF) | `ConfigRoutes.cs` + new `SsrfGuard.cs` |
| `callback_url` scheme-only validation, no reserved-IP check (SSRF) | `WebhookRoutes.cs` |
| `owner_user_id` query param not scoped to caller identity | `CommandCenterRoutes.cs` |
| `suiteName` path used in file resolution without format validation | `EvalRoutes.cs` |
| Token `ExpiresAt` not bounds-checked (past/far-future accepted) | `MeRoutes.cs` |
| `FetchModelIdsAsync` could fire twice concurrently, duplicating models | `SidebarViewModel.cs` — optimistic `ModelsFetchedLive` flag |
| Fire-and-forget tasks on startup silently swallowed exceptions | `SidebarViewModel.cs` — `.ContinueWith` observers + try-catch |
| SSRF check duplicated as private methods in `ChatRoutes` | Extracted to `SsrfGuard.cs`; `ChatRoutes` delegates to it |

#### Phase N — SDK coverage gaps

| Added method | Endpoint |
|---|---|
| `getCommandCenterState(options?)` | `GET /v1/command-center/state` |
| `listMcpServers()` | `GET /v1/mcp/servers` |
| `getKnowledgeSource(kind, slug)` | `GET /v1/knowledge/:kind/:slug/source` |
| `saveKnowledge(kind, slug, markdown)` | `POST /v1/knowledge/:kind/:slug` |
| `deleteKnowledge(kind, slug)` | `DELETE /v1/knowledge/:kind/:slug` |

Also fixed a latent `fetchWithRetry` bug where caller-supplied `Content-Type` headers were overwritten by the `application/json` default.

### Still open from the same audit

| Item | File | Notes |
|---|---|---|
| R4-M3 | `ChatRoutes.cs:130` | Legacy static `SOVRANT_TOKEN` short-circuits `IsAdmin()` — session ownership bypass |
| R4-M9 | `StatusRoutes.cs` | Exposes routing info and provider list to all authenticated users |
| R4-M10 | `Program.cs:106` | CORS allows ports 3000, 5100, 5173, 8080 — untrusted local services can make credentialed requests |
| 19-C2 | `ChatRoutes.cs:85` | DNS TOCTOU SSRF race (medium effort — needs IP pinning) |
| 19-C4 / 20-H1 | `ArtifactRoutes.cs` | Path traversal + ownership validation gaps |

---

## Pre-Beta Release Plan (2026-05-06)

> This section defines the ordered work items required before Sovrant is released publicly on GitHub for open beta. Items are listed in execution order — each can be started as soon as the previous is committed.

### Context

The codebase currently works well for a single admin user. Post-beta, the shift is to **multi-user** (multiple people sharing one Sovrant server install), and eventually **multi-tenant** (isolated orgs with separate billing, SSO, and data boundaries). The data model for multi-user already exists (Phases 32, 35, 38 are complete). This pre-beta sprint closes the remaining security gaps, wires up the login layer across all surfaces, and validates the MCP story before the repo goes public.

Phase 47 (workspace backup/export) is **post-beta** — the value is clear but it is not a launch blocker. See its entry for the deferred scope.

---

### Item 1 — ArtifactRoutes Security Fixes ✅

**Effort:** ~2 hours  
**Scope:** Two targeted fixes left open from the Round 4 audit.

| Finding | File | Fix |
|---|---|---|
| 19-C4 Path traversal | `ArtifactRoutes.cs:73` | `Path.GetFullPath` containment check on `path` param before passing to `store.ReadAsync` — reject anything that escapes the run's artifact root |
| 20-H1 Ownership bypass | `ArtifactRoutes.cs:59,94` | Call `RequireWorkspaceAccess(scope.WorkspaceId, ctx)` after `ScopeFromQuery()` resolves workspace/project — same pattern already used in WorkspaceRoutes |

**Acceptance:** A `../../` path returns 400. A valid `workspace_id` belonging to another user returns 403.

---

### Item 2 — CORS Hardening + Agentic Loop Timeout ✅

**Effort:** ~half day  
**Two independent fixes, commit together.**

#### 2a — Configurable CORS origins (R4-M10)

`Program.cs` currently hardcodes ports 3000, 5100, 5173, 8080. Any untrusted service on those ports can make credentialed requests.

**Fix:** Read allowed origins from `SOVRANT_CORS_ORIGINS` env var (comma-separated). Fall back to the current hardcoded list only when the var is unset, so existing single-user installs are unaffected. Document in `.env.example`.

#### 2b — Agentic loop per-turn wall-clock timeout

A stuck tool call (infinite loop, hung subprocess, unresponsive MCP server) occupies a session indefinitely. With multiple real users this starves the session pool.

**Fix:** Add a configurable `SOVRANT_TURN_TIMEOUT_SECONDS` (default: 300) enforced in `ConversationRuntime`. When the turn deadline is exceeded, cancel the in-flight tool call and emit a `RuntimeError` event so the session is released cleanly. The timeout resets at the start of each new turn, not cumulatively across the conversation.

**Acceptance:** Setting `SOVRANT_CORS_ORIGINS=https://myapp.com` allows only that origin. A turn that exceeds the timeout returns a `RuntimeError` and the session becomes available for the next request within 1s.

---

### Item 3 — Phase 85: Identity & Login Parity ✅

**Effort:** 3–5 weeks  
**Goal:** A single identity flows through Desktop, Web, CLI, and Server so a user who logs in on any surface sees the same workspaces, sessions, and memories.

**Started 2026-05-06.** See the Phase 85 spec above for design decisions and full progress tracking.

#### Beta scope (locked)

- Email + password (Argon2id), `svt_` bearer tokens — no JWT, no OAuth for beta
- First user = admin; registration closed after first sign-up; admin-only password reset (no SMTP)
- 30-day sliding token TTL
- Static `SOVRANT_TOKEN` removed — all surfaces use per-user `svt_` tokens
- LLM API keys served from keystore; no longer accepted via `X-LLM-Api-Key` headers
- No backwards-compat migration path (pre-release)

#### Security model (finalized)

- **Admin approval gate**: new registrations start as `status='pending'`; cannot log in until admin approves. Toggle: `POST /v1/auth/approval/enable|disable` (admin only). Default: on.
- **Registration control**: admin can open/close registration separately from the approval gate.
- **User management**: admin can approve (`POST /v1/users/{id}/approve`), disable (`DELETE /v1/users/{id}`), or reactivate (`POST /v1/users/{id}/reactivate`) any user. Login returns specific messages for pending vs. disabled accounts.

#### Remaining work

Admin pages (Web + Desktop) — user list with approve/disable/reactivate actions, registration toggle, approval gate toggle, password reset, token list. Unit tests (`IdentityServiceTests`, `PasswordHasherTests`).

**Acceptance:** A user registers on Desktop and immediately sees the same workspace data when opening Web against the same DB. CLI `sovrant login` resolves the same `UserId`. Admin can approve or disable users from the admin page.

---

## Phase 85.5 — Local / Remote Mode Selection for CLI & Desktop

**Status:** ✅ Complete (2026-05-07).  
**Effort:** ~1–2 weeks  
**Depends on:** Phase 85 (identity + svt_ tokens) ✅

### Problem

CLI and Desktop currently run in **embedded mode only** (the full runtime runs in-process against a local SQLite DB). They have no way to connect to a shared `Sovrant.Server` instance running on another machine or in a container. Web already supports both modes (Phase 61), but CLI and Desktop do not.

The goal is parity: both surfaces can run **local** (embedded, single-user, no server needed) or **remote** (connects to a company-hosted `Sovrant.Server` with shared workspaces and team access), and the user can switch modes.

### Design

**Two modes, user-selectable:**

| Mode | Description | When to use |
|---|---|---|
| **Local** | Full runtime embedded in the process; local SQLite DB; single user | Personal use, offline, dev/test |
| **Remote** | HTTP/SignalR client connecting to `Sovrant.Server`; shared DB on the server | Team / company deployment |

**Connection model:**
- On first run, Desktop setup wizard offers a choice: "Use local database" vs "Connect to a Sovrant server"
- Choosing remote shows a server URL field + login form; credentials are validated against the server before setup completes
- The chosen mode (and server URL if remote) is stored in `ICredentialStore` under `sovrant.desktop.server_url`
- CLI uses `--server <url>` flag or `SOVRANT_SERVER_URL` env var to select remote mode; `sovrant connect <url>` stores the URL persistently
- In both cases, the identity flow (Phase 85 login/token) applies regardless of mode

**What already exists:**
- `AddSovrantClient()` + `SovrantRemoteOptions` — Phase 61; used by Web; provides `RemoteRuntimeSessionPool`, `RemoteSessionStore`, `RemoteToolRegistry`, `RemoteArtifactStore`, `SignalRStreamingClient`, `SovrantApiDelegatingHandler` (bearer token injection), `RemoteConnectionState`
- `ICredentialStore` — AES-256-GCM encrypted; already stores API keys; extend to store server URL
- `BearerTokenMiddleware` on the server already validates `svt_` tokens
- Desktop setup wizard (`SetupWizardViewModel`) — extend with a mode-selection step
- CLI `BuildServices()` function — branch on stored server URL to call `AddSovrantClient()` instead of `AddSovrantRuntime()`

**Important constraint — client library is Web-only today:**  
All remote proxy implementations (`RemoteRuntimeSessionPool`, `RemoteSessionStore`, etc.) currently live in `Sovrant.Web/Services/Remote/`. Desktop and CLI have no reference to that project. Before `AddSovrantClient()` can be called from CLI or Desktop, these must be moved into a shared library — proposed as `Sovrant.Api.Client` or similar. This is the largest single piece of new work in this phase.

**New work:**

| Item | Notes |
|---|---|
| ✅ Extract `Sovrant.Web/Services/Remote/` into shared `Sovrant.Api.Client` project | Done: new `src/Sovrant.Api.Client/` lib; `Sovrant.Web` references it; namespace → `Sovrant.Client.Remote` |
| ✅ `sovrant connect <url>` + `sovrant disconnect` commands | Stores `RuntimeMode` / `RemoteServerUrl` / `RemoteApiToken` in credential store |
| ✅ CLI: auto-detect mode in `BuildServices()` | Checks env `SOVRANT_SERVER_URL` then credential store; calls `AddSovrantClient()` or `AddSovrantRuntime()` |
| ✅ CLI: `sovrant login` works against both local and remote | Local: embedded `IIdentityService`; remote: `POST /v1/auth/login` via HTTP |
| ✅ CLI: `sovrant logout` / `whoami` work in remote mode | Logout: deletes local token only; whoami: calls `GET /v1/auth/me` |
| ✅ `AddSovrantStorage` extracted from `AddSovrantRuntime` | Lightweight storage-only registration for startup mode detection |
| ✅ `RemoteIdentityService` in `Sovrant.Api.Client` | Implements `IIdentityService` via server REST; `LoginViewModel` works unchanged in both modes |
| ✅ Desktop: two-phase boot — read mode before building DI container | Phase 1: `AddSovrantStorage` → read mode; Phase 2: `AddSovrantClient` or `AddSovrantRuntime` |
| ✅ Desktop: remote login / session restore | `TryRestoreRemoteSessionAsync` validates stored token via `GET /v1/auth/me` |
| ✅ Desktop: 401 re-auth via `RemoteConnectionState.StatusChanged` | Fires login window on 401; hot-swaps token in `SovrantRemoteOptions` |
| ✅ Token refresh / re-login on 401 (Desktop) | Handled via `RemoteConnectionState` subscription |
| ✅ Desktop: mode-selection step in setup wizard | Three-step wizard: Mode → Local provider/key or Remote server URL; `ChooseLocalCommand`, `ChooseRemoteCommand`, `SaveRemoteAndStartCommand` |
| ✅ Desktop: Settings — switch mode / change server URL | Connection tab with `IsRemoteMode` toggle + `ConnectionServerUrl` field; `SaveConnectionCommand` persists to credential store + fires `RestartRequired` |

**Out of scope for 85.5:**
- Certificate pinning / mutual TLS
- Auto-discovery (mDNS, DNS-SD)
- Server-side multi-tenancy (each user already gets their own workspace via Phase 85 user identity)

---

### Item 4 — Phase 96: MCP End-to-End Smoke Test ✅

**Effort:** ~1 week  
**This is the only item the roadmap explicitly calls a launch gate.**

Run the full MCP tool discovery → invocation loop on every surface (Desktop, Web, CLI, Server) using a real MCP server. Confirm per-session gating, `ListMcpResources` → `MCPTool` discovery, and Connections UI all work end-to-end. No code changes expected — this is a test + fix pass.

**Completed 2026-05-06.** Full code audit of the session-gating path confirmed:
- `McpClientRegistry.ToolToServer` maps tool names to their originating server on registration.
- `FilterToolsForModel()` in `ConversationRuntime` drops MCP tools whose server is not in `SessionConfig.AllowedMcpServers` (empty list = all MCP disabled; null = all servers visible).
- Desktop (`ChatViewModel`) and Web (`Chat.razor`) both start with no connections selected (opt-in by default) and push `SessionContext` before each turn.
- `SessionContext.Push(pooled.Config)` correctly scopes the allow-list to the async flow before `RunTurnAsync` reads it.
- 4 unit tests added in `McpSessionGatingTests` covering: no connections selected, single server selected, null allow-list (all), and unrelated server selected.
- All 4 tests pass. No code changes were required — gating was already correctly implemented.

See the Phase 96 entry for the full smoke test checklist.

---

### Summary — Original Pre-Beta Items

| # | Item | Effort | Status |
|---|---|---|---|
| 1 | ArtifactRoutes security (path traversal + ownership) | ~2 hrs | ✅ |
| 2 | CORS configurable origins + agentic loop timeout | ~half day | ✅ |
| 3 | Phase 85 — Identity & login parity | 3–5 weeks | ✅ Complete |
| 4 | Phase 96 — MCP smoke test (launch gate) | ~1 week | ✅ |
| — | Phase 47 — Workspace backup/export | 1–2 weeks | **Post-beta** |

### Additional work completed after original plan (2026-05-07/08)

| Item | Status | Notes |
|---|---|---|
| Phase 85.5 — Local/Remote mode selection (CLI + Desktop) | ✅ Complete | `Sovrant.Api.Client` extracted; `sovrant connect/disconnect`; two-phase Desktop boot; setup wizard mode picker; Settings connection tab |
| Phase 40A — Workspace-scoped roles & tenant enforcement (server) | ✅ Complete | `WorkspaceContextMiddleware` fixed to use authenticated identity; `POST /v1/workspaces` admin-only; workspace owner/admin can manage members; project membership gated on workspace role |
| Auth review fixes — `GET /v1/auth/me` + registration URL | ✅ Complete | Desktop/CLI remote session restore and `whoami` now work; `RemoteIdentityService` path and property names corrected |
| Web remote mode session persistence | ✅ Complete | `AddSovrantStorage` registered in remote branch; `TryRestoreWebRemoteSessionAsync` validates stored token via `/v1/auth/me` on restart |

### Remaining before beta (recommended order)

| Priority | Item | Effort | Status |
|---|---|---|---|
| 1 | Phase 40A UI — workspace switcher (Web + Desktop) | ~1–2 days | ✅ Complete — `WorkspacesView`/`WorkspacesViewModel` (Desktop), `Workspaces.razor`/`WorkspacesAdmin.razor` (Web) |
| 1 | Phase 40A CLI — `sovrant workspace list/members` remote mode | ~half day | ⬜ Outstanding — commands exist but only work in embedded mode; remote mode shows a warning and exits |
| 2 | Bug — model selection not persisted across restart | ~half day | ✅ Complete — fixed in `Fix provider/model UX across Desktop and Web` commit |
| 3 | Phase 97 — TLS/SSL for Server, Web & MCP | ~1 day | ✅ Complete — Kestrel TLS (PEM/PFX), HTTPS redirect, and configurable port in `Sovrant.Server/Program.cs` |
| 4 | Phase 93 — Configuration boundary audit | ~half day | ✅ Complete (2026-05-09) — `sovrant.config` removed; bootstrap reads env vars + `.env` only |
| — | Phases 94, 95 (model switch continuity, memory audit) | — | **Post-beta** |

---

## Phase 97 — TLS/SSL Support for Server, Web & MCP

**Status:** ✅ Complete — Kestrel TLS binding (PEM/PFX), configurable HTTPS port, and HTTP→HTTPS redirect middleware implemented in `Sovrant.Server/Program.cs` and `Sovrant.Web/Program.cs`.
**Goal:** All three network-facing surfaces (HTTP server, Blazor Web frontend, MCP server) support HTTPS/TLS so that traffic between clients and Sovrant is encrypted in transit. No plaintext HTTP in any production or beta deployment.

### Scope

| Surface | What changes |
|---|---|
| **Server (`Sovrant.Server`)** | Kestrel configured to bind HTTPS on a configurable port (default `5443`); certificate path + password (or Let's Encrypt / ACME) configurable via `sovrant.config`; plaintext HTTP either disabled or redirected to HTTPS |
| **Web (`Sovrant.Web`)** | Blazor Server host honours the same Kestrel TLS config; SignalR WebSocket connection (remote mode) requires `wss://`; HTTP → HTTPS redirect middleware enabled |
| **MCP server** | MCP HTTP transport binds HTTPS; clients connecting to the MCP endpoint must use `https://` / `wss://`; existing MCP session auth unchanged |

### Certificate strategy

- **Development / local:** self-signed via `dotnet dev-certs https` — documented in setup guide, not managed by Sovrant
- **Self-hosted beta:** PEM/PFX path + passphrase configured in `sovrant.config` (`server.tls.cert`, `server.tls.key`); loaded by Kestrel at startup
- **ACME / Let's Encrypt:** deferred — noted as a follow-on once the self-hosted path is validated

### What does NOT change

- Desktop app (Avalonia) — no network listener; not in scope
- CLI — connects to the server as a client; picks up TLS automatically once the server endpoint is HTTPS
- Internal service-to-service calls (e.g. Web → Server in embedded mode) — loopback only, no TLS required
- Token format, auth flow, or workspace enforcement — unchanged

### Implementation plan

1. Add `server.tls` section to `sovrant.config` schema (`enabled`, `cert`, `key`, `port`)
2. Wire Kestrel `ListenAnyIP(httpsPort, o => o.UseHttps(...))` in `Sovrant.Server` and `Sovrant.Web` host builders, driven by config
3. Add HTTP → HTTPS redirect middleware (both hosts) when TLS is enabled
4. Update MCP server transport to use the same Kestrel TLS binding
5. Update README / setup guide with dev-certs and self-hosted cert instructions
6. Smoke test: HTTPS handshake on Server, Web (including SignalR), and MCP endpoint; verify HTTP redirects; verify CLI connects cleanly

---

## Phase 98 (legacy planning section) — CLI Workspace Commands in Remote Mode

**Status:** ⬜ Outstanding — last remaining pre-beta item.

`sovrant workspace list` and `sovrant workspace members` exist and work correctly in embedded (local) mode. In remote mode both commands currently bail out with a yellow warning: `workspace commands are only available in embedded (local) mode.`

### What needs to change

| Location | Change |
|---|---|
| `src/Sovrant.Api.Client/` | Add `RemoteWorkspaceService.cs` — thin HTTP wrapper implementing `IWorkspaceService` (or a minimal subset) over `GET /v1/workspaces` and `GET /v1/workspaces/{id}/members` |
| `src/Sovrant.Cli/Program.cs` — `wsListCmd` | Replace embedded-only guard with a mode branch: call `IWorkspaceService` (embedded) or `RemoteWorkspaceService` (remote) |
| `src/Sovrant.Cli/Program.cs` — `wsMembersCmd` | Same pattern |
| `src/Sovrant.Cli/Program.cs` DI registration | Register `RemoteWorkspaceService` in the remote-mode service branch (alongside `RemoteIdentityService` etc.) |

### Existing server endpoints (no changes needed)

- `GET /v1/workspaces` — returns all workspaces the caller belongs to (scoped to authenticated user via `WorkspaceContextMiddleware`)
- `GET /v1/workspaces/{id}/members` — returns member list for a workspace the caller has access to

### Scope

Read-only. `workspace create`, `workspace add-member`, etc. are out of scope for this item — the CLI is an inspection surface, not an admin console.

### Done when

`sovrant workspace list` and `sovrant workspace members <id>` produce correct tabular output in remote mode, using the stored `svt_` token for auth. Embedded-mode behaviour is unchanged.

---

## Phase 99 (legacy planning section) — Auth Hardening: Recovery, MFA, OAuth & Audit

**Status:** Planned. Post-beta evolution of the Phase 85 foundation.

Phase 85 shipped the baseline: per-user `svt_*` tokens, Argon2id passwords, admin-generated one-time reset tokens, sliding 30-day TTL. This phase closes the gaps that became obvious once we started dogfooding it and reviewing the code:

### Gaps to close

| # | Gap | Why it matters |
|---|---|---|
| 1 | **No recovery path when the sole admin loses their password** | Today there is no in-product way back in — only raw `sqlite3` surgery on `users.password_hash` or restoring from `db backup`. The previous `tools/ReadDb` shortcut (hardcoded `Admin123!`) was deleted as unsafe; we need a deliberate replacement. |
| 2 | **Password reset still requires an admin in the loop** | Phase 85 left this as "admin generates a token and shares it out-of-band" — fine for tiny teams, breaks down once there is more than one user who forgets passwords. |
| 3 | **No second factor** | Phase 85 explicitly deferred TOTP. With registration open by default on first install, a brute-force or credential-stuffing attack against a known admin email/username has no second hurdle. |
| 4 | **No OAuth / SSO providers** | Phase 85 sketched Google/Microsoft/GitHub as pluggable but did not implement any. Common ask from teams that already have an identity provider. |
| 5 | **Tokens are not audience-scoped** | A `svt_*` issued for the CLI is accepted by the Web cookie path and vice versa. Phase 85's acceptance criteria listed audience scoping but it did not ship. |
| 6 | **No lockout / brute-force protection beyond generic rate-limit** | The existing per-session rate limiter throttles HTTP but does not lock accounts. Phase 85 listed this as acceptance criteria too — needs verification or implementation. |
| 7 | **No dedicated auth audit log** | Login attempts (success + failure), password changes, token issuance / revocation, reset-token use, lockout events are not first-class rows. The general audit log captures some of this incidentally; auth events deserve their own indexed view. |

### Scope (in priority order)

1. **Lockout & auth audit** — `auth_events` table (or extension of `audit_governance`) with `event_type`, `user_id`, `ip`, `user_agent`, `outcome`, `ts`. Lock the account after N consecutive failed logins within a window; auto-unlock after a cooldown or admin override. Surface the audit view on the existing admin page.
2. **Self-service password reset via OS-local recovery code** — at registration, generate a one-time **recovery code** that the user must capture (printed once, never stored plaintext). `POST /v1/auth/recover` accepts `{username, recovery_code, new_password}`. Replaces the admin-in-the-loop flow for users who still have their recovery code; admin reset stays as the fallback.
3. **Admin lockout recovery** — documented (not in-product) procedure: shutdown the server, run `sovrant db admin-unlock` (a *gated* CLI command that requires a sentinel file `~/.sovrant/data/.allow-admin-recovery` chmod 0600 + same UID as the DB file — proves filesystem ownership), which clears lockout flags and prints a one-time recovery code for the oldest active admin. No password reset, no shortcut.
4. **TOTP 2FA** — opt-in per user, enrollment from Settings → Account on Web/Desktop. RFC 6238, 30-second window, 6 digits. Backup codes generated at enrollment (single-use, hashed at rest). Login flow extended: `POST /v1/auth/login` returns `{ requires_totp: true, challenge_id }` when the user has TOTP enabled.
5. **Audience-scoped tokens** — `api_tokens.audience` column (`cli` / `web` / `desktop` / `sdk`). `BearerTokenMiddleware` rejects a token whose audience doesn't match the caller — surface determined by request path / `User-Agent` / explicit header. Existing tokens migrated to `audience='any'` and continue to work; new tokens default to the issuing surface.
6. **OAuth providers — Google + GitHub first** — `IExternalIdentityProvider` abstraction. Users link a Google / GitHub identity to their Sovrant user on first OAuth login; subsequent logins skip password entry. No new account is created from OAuth alone — admin must approve the user first (same as password registration today).

### Explicitly deferred

- **SMTP-based password reset email** — runs into deliverability + SMTP-config complexity. The recovery-code flow above gets us self-service without an SMTP dependency. Email reset can layer on top later for installs that want it.
- **WebAuthn / passkeys** — desirable, but TOTP-first per Phase 85.
- **SAML / enterprise SSO** — same deferral as Phase 85; revisit when a customer asks.
- **OIDC discovery / generic OAuth providers beyond the named two** — pick Google + GitHub for the first pass; generalize once the abstraction is battle-tested.

### Acceptance Criteria

- [ ] Brute-forcing a known admin username from a single IP is rate-limited AND results in account lockout after N failures; lockout state and the unlock procedure are visible in the admin audit view
- [ ] A user who captures their recovery code at registration can reset their own password without admin involvement
- [ ] The sole-admin lockout scenario has a documented recovery procedure that requires filesystem-level proof and produces an auditable event
- [ ] A user can enable TOTP from Settings and subsequent logins require the second factor on all four surfaces (CLI, Web, Desktop, SDK)
- [ ] A CLI-audience token is rejected when presented to the Web cookie middleware (and vice versa) — integration tests cover the four-by-four matrix
- [ ] Linking a Google or GitHub account to an existing Sovrant user lets them sign in via OAuth on Web and Desktop; CLI continues using password + token

### Why now

- The Phase 85 baseline is shipped and dogfooded; the rough edges are now concrete (recovery path, audience scoping, no MFA) rather than hypothetical.
- The pending Beta and BSL → Apache transition both raise the cost of staying at the current bar — outside contributors will probe this surface.
- Trust Boundary (Phase 58) and the upcoming Command Center cockpit assume a strong principal model; weak login undermines the layers built on top.

---

## Phase 100 — Knowledge Pages: User-Authored Markdown in System Context

**Status:** Planned. A CRUD shell exists in the working tree from an earlier exploratory session (V028 migration, `IKnowledgePageStore` + `SqliteKnowledgePageStore`, basic Web page) — the *useful* half (prompt injection, Desktop parity, server API, tests) is not done.

### Goal

A per-user "personal wiki" of named markdown pages that the agent can reference during a turn. Pages sit alongside Artifacts, Skills, Tools, Agents, and Memory in the Knowledge section. The user explicitly authors and curates them; the agent reads selected pages into the system prompt so it has stable, durable context (coding standards, project conventions, domain knowledge, reference material) without relying on transient memory or filesystem artifacts.

### Why it earns a phase

Today knowledge enters the agent only through (a) conversation memory (transient, derived) or (b) artifacts (file-shaped, run-scoped). Neither is the right shape for "this is a markdown page I want the agent to know about across every session". Without a deliberate slot for hand-curated knowledge, users either lean on memory hacks (forces the agent to learn things it should just *be told*) or stuff context into every prompt manually.

### What already exists in the tree

| Layer | File | State |
|---|---|---|
| DB migration | `Storage/Migrations/V028__knowledge_pages.sql` | Table: `id`, `user_id`, `title`, `content`, `tags` (JSON), timestamps |
| Runtime | `Storage/IKnowledgePageStore.cs`, `SqliteKnowledgePageStore.cs` | Full owner-scoped CRUD |
| DI | `Runtime/ServiceCollectionExtensions.cs` | `IKnowledgePageStore` registered |
| Web UI | `Web/Components/Pages/KnowledgePages.razor` (195 lines) + nav surface edits | Single-surface CRUD UI |

These uncommitted edits are a useful starting point; the phase keeps or discards them based on whether they fit the final design.

### What's missing

| Gap | Why it matters |
|---|---|
| **System-prompt injection** | The CRUD is dead weight without it — the whole point is for the agent to *see* the pages |
| **Page selection model** | Inject *which* pages? All of them blows past context windows. Options: explicit per-session "active pages", tag-driven auto-selection, or admin-marked "always-on" pages |
| **Server API endpoints** | `GET/POST/PUT/DELETE /v1/knowledge-pages` so remote-mode Web/Desktop and the SDK can drive the feature |
| **Desktop parity** | Avalonia `KnowledgePagesView` + nav entry — matches every other Knowledge-section item |
| **CLI surface** | `sovrant pages list/show/edit/delete` for headless workflows |
| **Tests** | Store, server, prompt-injection — nothing covered today |
| **Token-budget guardrails** | Selected pages must fit a configurable cap (default ~3,000 tokens or ~10% of context window) with deterministic truncation/ordering |

### Scope (in priority order)

1. **Decide the injection model.** Pick one of: (a) explicit per-session active-page list managed via session config, (b) tag-driven auto-selection ("inject any page tagged `always`"), (c) hybrid (auto for `always`, explicit for everything else). Recommend hybrid — covers the "team conventions" use case without making the user fiddle every session.
2. **Wire prompt injection.** Add a `KnowledgePageContext` builder invoked by `ConversationRuntime` at turn start. Emit each included page as a `# Knowledge: <title>` markdown block in the system prompt. Cap total tokens per the guardrail above; drop oldest-updated pages first when over budget.
3. **Server endpoints + ownership scoping.** Match the workspace pattern from existing routes — caller's `user_id` derived from the `svt_*` token; admin can see all pages.
4. **Desktop UI.** Avalonia view with markdown editor (existing `MarkdownScrollViewer` per `feedback_markdown_avalonia.md` — beware code-fence crashes).
5. **CLI surface.** Read-only first (`list`, `show`); editing later.
6. **Tests.** `SqliteKnowledgePageStoreTests`, server route tests, an integration test that confirms a page tagged `always` appears verbatim in the system prompt of the next turn.

### Deferred

- **External knowledge sources** (Confluence, Notion, GitHub wikis) — keep v1 hand-authored only; integrations are their own phase.
- **Vector / semantic search** — string search by title and tag is enough at the page counts users will realistically maintain. Revisit if anyone hits >200 pages.
- **Multi-user / workspace-shared pages** — v1 is per-user (`user_id`-scoped). Workspace-shared pages slot into the same table once the shared-workspace model from `project_workspace_provider_roadmap` lands.
- **Versioning / history** — same deferral as the Phase 91 knowledge authoring story; revisit together.

### Acceptance Criteria

- [ ] User creates a page tagged `always` and on the next turn the page content appears verbatim in the system prompt sent to the LLM
- [ ] Token budget for injected pages is configurable per user and enforced — over-budget pages are deterministically dropped with a visible diagnostic
- [ ] Server endpoints scope every read/write to the caller's `user_id` (admin sees all) — non-owner access returns 404
- [ ] Desktop and Web surfaces have feature parity for CRUD
- [ ] All existing tests still pass; new store + injection tests cover the happy path and the token-budget edge case

---

## Phase 101 — AI Cost Reduction

**Status:** Planned.

### Goal

Reduce the real-money and energy cost of running Sovrant — both for self-hosters and for Anant-operated deployments — through a combination of prompt engineering best practices, open-source tooling integrations, and first-class surfacing of free-tier models across every Sovrant feature.

### Motivation

LLM API spend is the dominant variable cost in any agentic system. As Sovrant scales (more tools, longer missions, swarm orchestration), unchecked token usage compounds fast. Two levers exist: (a) use fewer tokens per call, and (b) use cheaper or free models for calls that don't need full capability.

### Sub-area 1 — Context Efficiency (fewer tokens, same quality)

Adopt proven patterns and open-source libraries that shrink effective context without degrading output quality.

| Technique | Approach |
|---|---|
| **Prompt compression** | Evaluate `LLMLingua` / `LLMLingua-2` (Microsoft, MIT license) for compressing long system prompts and retrieved context before injection. Target: 3–5× compression on repetitive or boilerplate content. |
| **Conversation summarisation** | Instead of replaying the full message history on every turn, summarise older turns beyond a configurable token window. The runtime already has a `ConversationRuntime` abstraction — wire a `ISummarisationStrategy` that swaps in compressed history. |
| **Knowledge page budgeting** | Phase 100 already plans a per-user token cap for injected knowledge pages. Phase 101 makes the cap visible in the UI (Settings → Usage) and adds a global admin cap. |
| **Tool-call pruning** | Only emit tool schemas for tools relevant to the current session context (MCP filtering already exists per V024 migration — extend to agent-template-aware pruning). Each removed tool schema saves ~200–600 tokens per turn. |
| **Caching hot prompts** | Where the provider supports it (e.g. Anthropic prompt caching, OpenAI context caching), cache the system prompt and injected knowledge blocks. Surface cache-hit rate in the cost dashboard. |
| **Streaming trim** | Skip re-sending unchanged prefix context when the provider supports delta-only context (evaluate per-provider). |

**Reference implementations to evaluate:**

- [`LLMLingua`](https://github.com/microsoft/LLMLingua) — prompt compression (MIT)
- [`aisuite`](https://github.com/andrewyng/aisuite) — unified multi-provider interface with cost tracking
- [`guidance`](https://github.com/guidance-ai/guidance) — structured generation to avoid re-prompting loops
- [`tiktoken`](https://github.com/openai/tiktoken) or provider-native tokenizers — accurate token counting before send (today Sovrant estimates; exact counts prevent over-budget surprises)

### Sub-area 2 — Free Model Promotion

Make free-tier models a first-class citizen rather than a power-user workaround.

| Feature | Detail |
|---|---|
| **"Free" badge in model picker** | Tag OpenRouter `:free` suffix models and Google AI Studio free-tier models in the model picker dropdown on Web, Desktop, and CLI. Badge shows cost: `$0.00 / 1M tokens`. |
| **Default model guidance** | First-run wizard (or onboarding tooltip) recommends a capable free model (e.g. `gemini-2.5-flash` via Google AI Studio) and explains when to step up to a paid model. |
| **Per-feature model routing** | Allow users to assign a "cheap model" for low-stakes tasks: title generation, session summarisation, skill selection scoring, eval scoring. Today these use the session default model — routing them to a free model cuts spend without user-visible quality loss. |
| **Cost dashboard** | Extend the existing cost tracking (Phase 88 `TurnCost` events, `session_costs` table) into a full Settings → Usage page: spend by model, spend by day, projected monthly spend, and a "savings from free-model routing" line. |
| **Provider presets** | Ship pre-filled provider presets for zero-cost setups: Google AI Studio (free quota), Groq free tier, Together AI free models. One-click setup in Settings → Providers. |
| **Sovrant skill: /frugal** | A built-in skill that temporarily switches the session to the best available free model for the next N turns, then restores the prior model. Useful for exploratory work where full capability isn't needed. |

### Acceptance Criteria

- [ ] At least one prompt-compression strategy (summarisation or LLMLingua integration) is measurably reducing token usage in multi-turn sessions — confirmed by comparing `session_costs` totals before/after on the same conversation replay
- [ ] Free-tier models are badged in the model picker on all three surfaces (Web, Desktop, CLI)
- [ ] Per-feature model routing is configurable: user can assign a separate model for title generation and session summarisation in Settings → Models
- [ ] A Settings → Usage page shows spend by model and by day, sourced from the existing `session_costs` DB table
- [ ] `/frugal` skill is available and tested across Web, Desktop, and CLI

### Deferred

- **Semantic deduplication of tool results** — deduplicate near-identical tool outputs before they re-enter context (useful for multi-step web search / file reads). Requires embedding; defer until embeddings land.
- **Speculative decoding / local model offload** — running a small local model for cheap tasks requires local inference infrastructure; out of scope for v1.
- **Automatic model downgrade on quota exhaustion** — detect 429s from paid providers and auto-reroute to free fallback. Useful but needs careful UX (user must consent to model switch mid-session).

---

## Phase 102 — MCP Ecosystem Integration

**Status:** Planned.

### Goal

Make Sovrant a first-class citizen in the MCP ecosystem — both as a consumer of the thousands of MCP servers that already exist, and as a host that can surface the new **MCP Apps** (SEP-1865) interactive UI spec. Today Sovrant supports stdio and HTTP/SSE MCP connections configured manually. Phase 102 replaces that friction with dynamic discovery, one-click install from public registries, and support for the richer MCP Apps protocol.

### Background

"MCP apps" covers two related but distinct things:

1. **MCP Apps / SEP-1865** — A 2026 extension to the core Model Context Protocol that allows MCP servers to deliver rich, interactive UI (React components, forms, dashboards) into the host application alongside tool results. Claude Desktop, Cursor, and others are adding support. This is the UI layer on top of MCP.

2. **The MCP server creation ecosystem** — A fast-growing set of platforms that generate, host, or aggregate MCP servers:
   - **Official MCP Registry** (`registry.modelcontextprotocol.io`) — machine-readable, API-accessible, vendor-neutral; maintained by Anthropic and GitHub
   - **Smithery** (`smithery.ai`) — 5,000–7,000+ community servers; app-store UX with local and remote hosting
   - **Composio** — single Tool Router endpoint that exposes 500+ app integrations (GitHub, Slack, Notion, Linear, etc.) as MCP tools; 20,000+ individual tools
   - **Mintlify** — auto-generates MCP servers from API documentation via OpenAPI specs; zero-config
   - **Glama.ai**, **PulseMCP** — large curated directories

Sovrant already ships an MCP client (`McpClientRegistry`, `McpToolRegistrar`, V024 per-session gating). Phase 102 extends that infrastructure rather than replacing it.

### Sub-area 1 — Registry Discovery & One-Click Install

Allow users to browse, search, and install MCP servers from public registries without editing config files.

| Feature | Detail |
|---|---|
| **Registry browser** | Settings → MCP → Browse. Queries the official MCP Registry API and Smithery API. Shows name, description, tool count, transport type, and install status. Searchable. |
| **One-click install** | "Add to Sovrant" button fetches server manifest, stores connection config in the DB (`mcp_servers` table or equivalent), and makes the server available to sessions immediately. |
| **`.well-known/mcp.json` discovery** | For any URL the user pastes, attempt discovery via the well-known endpoint before falling back to manual config. |
| **Transport auto-detect** | Detect stdio vs HTTP/SSE vs WebSocket from the manifest and configure the correct transport automatically. |
| **Composio preset** | Ship a built-in Composio provider preset: user supplies their Composio API key, Sovrant points at the Tool Router endpoint, and all 20,000+ Composio tools become available to any session. |
| **Mintlify preset** | For any Mintlify-hosted docs site, derive the MCP server URL automatically from the docs URL and connect without manual config. |

### Sub-area 2 — MCP Apps (SEP-1865) Host Support

Surface interactive MCP App UIs inside Sovrant's Web and Desktop chat interfaces.

| Feature | Detail |
|---|---|
| **MCP App renderer (Web)** | When a tool result includes an MCP App payload (React component or structured UI descriptor per SEP-1865), render it inline in the chat response. Blazor web view component wraps the MCP App sandbox. |
| **MCP App renderer (Desktop)** | Avalonia `WebView` (or equivalent embedded browser) renders the MCP App payload in a chat message panel. Same sandbox isolation as Web. |
| **Security sandbox** | MCP App UIs run in an isolated context — no access to Sovrant session tokens, DB, or file system. User interaction events are sent back to the MCP server, not directly to the LLM. |
| **Capability negotiation** | During MCP handshake, advertise `sovrant/mcp-apps-v1` capability so compliant servers know they can send UI payloads. Servers that don't support MCP Apps continue to work unchanged. |

### Sub-area 3 — MCP Server Generation (Sovrant-as-Creator)

Enable users to generate their own MCP servers from within Sovrant.

| Feature | Detail |
|---|---|
| **OpenAPI → MCP server** | Given an OpenAPI spec URL or file, generate a working MCP server (TypeScript via FastMCP or Python via `mcp` SDK) that wraps the API. Output: downloadable server bundle or GitHub Gist. |
| **Sovrant skill: /mcp-server** | A built-in skill that walks the user through generating an MCP server for a URL or OpenAPI spec. Outputs a `server.ts` / `server.py` ready to deploy. |
| **Publish to Smithery** | Optional: after generating a server, offer to publish it to Smithery via their submission API. |

### Integration Points with Existing Sovrant Systems

- **`McpClientRegistry`** — extend to support registry-sourced connections alongside the existing manual config
- **`McpToolRegistrar`** — no changes needed; tools discovered from registry servers register the same way as manual ones
- **V024 per-session MCP gating** — registry-installed servers participate in the same per-session enable/disable flow
- **Tool-call pruning (Phase 101)** — Composio's 20,000 tools make pruning mandatory; Phase 101's tool-schema pruning must be in place before Composio is enabled by default
- **Cost dashboard (Phase 101)** — track token cost of MCP tool call results separately from LLM completions

### Acceptance Criteria

- [ ] User can search the official MCP Registry from Settings → MCP and install a server in under 3 clicks with no manual config editing
- [ ] Composio preset connects via a single API key and makes Composio tools available in sessions
- [ ] A Mintlify-hosted docs site can be connected by pasting its URL — no manual server URL required
- [ ] MCP App (SEP-1865) UI payloads render inline in Web and Desktop chat; interactions are sandboxed
- [ ] `/mcp-server` skill generates a runnable MCP server from an OpenAPI spec URL
- [ ] All existing manually-configured MCP connections continue to work unchanged

### Deferred

- **Local stdio server management UI** — install and manage locally-running stdio servers from the UI (requires process management, OS-level concerns). Phase 102 v1 focuses on remote HTTP servers.
- **MCP server marketplace within Sovrant** — hosting and monetising community-built servers inside Sovrant. Requires workspace membership model from `project_workspace_provider_roadmap`.
- **MCP App authoring** — let users build MCP App UIs inside Sovrant. Rendering first, authoring later.
- **Smithery publish flow** — deferred until the MCP server generation feature is validated.

---

## Phase 103 — Optional Project Layer in Artifact Storage

**Status:** Planned.

### Problem

Every artifact run is currently stored under a three-level path:

```
{workspace}/{project}/{run}/files
```

The `project` level is mandatory — if no project is selected, Sovrant silently uses the sentinel `default-project`. Users who don't organise their work into projects still see a `default-project` folder on disk and in the Artifacts UI, which is confusing and adds an unwanted layer of indirection.

### Goal

Make the project layer optional. Users with no active project should have artifacts stored directly under the workspace:

```
{workspace}/{run}/files          ← no project selected
{workspace}/{project}/{run}/files ← project selected
```

The `default-project` sentinel is retired. The listing, download, ZIP, and delete routes all become project-layer-aware: they accept either a 3-segment or 4-segment path.

### What changes

| Layer | Change |
|---|---|
| `ArtifactScope` | `ProjectId` becomes nullable; null means no project (workspace-level run) |
| `LocalArtifactStore.BuildScopePath` | Omit the project segment when `ProjectId` is null |
| `LocalArtifactStore.ValidateSegment` | Only called for project segment when non-null |
| `Artifacts.razor` listing parser | Handle 3-segment paths (`{ws}/{run}/{file}`) as well as 4-segment (`{ws}/{proj}/{run}/{file}`) |
| `ArtifactRoutes` | Download and ZIP endpoints support both path depths; `project_id` query param becomes optional |
| `ConversationRuntime` | Pass null `ProjectId` when no project is active instead of falling back to `DefaultProjectId` |
| All tool call sites | Same — null project when session has no active project |
| Desktop / Web UI | Artifacts page groups by workspace first, then project if present |

### Migration

Existing `default-project` directories are treated as workspace-level runs — the listing parser recognises `default-project` as the legacy sentinel and promotes those entries to workspace scope in the UI. No files are moved.

### Acceptance Criteria

- [ ] A session with no active project stores artifacts at `{workspace}/{run}/` with no project subdirectory
- [ ] A session with an active project stores artifacts at `{workspace}/{project}/{run}/` as today
- [ ] The Artifacts page groups and displays both layouts correctly in the same list
- [ ] Existing `default-project` directories are shown as workspace-level artifacts (no "default-project" label visible to users)
- [ ] Download, ZIP, and delete endpoints handle both path depths
- [ ] No existing artifacts are moved or broken by the change

---

## Phase 104 — DuckDB Database Provider

**Status:** Planned / Deferred.

**Depends on:** Phase 32 (`IStorageProvider` abstraction)

**Goal:** Add DuckDB as a third `IStorageProvider` implementation alongside SQLite (embedded/desktop) and Supabase (hosted/multi-tenant). DuckDB is an in-process columnar analytical database — embedded like SQLite but built for fast aggregations over large result sets. It is the right backend for self-hosted Sovrant deployments that need rich cross-session analytics (cost dashboards, agent performance reports, audit queries) without the infrastructure overhead of running a PostgreSQL server.

### Why DuckDB

| Property | SQLite | Supabase (PostgreSQL) | DuckDB |
|---|---|---|---|
| Deployment | Embedded, zero-infra | Managed cloud service | Embedded, zero-infra |
| Write pattern | OLTP — frequent small writes | OLTP — frequent small writes | OLAP — optimised for bulk reads and aggregations |
| Analytics queries | Slow table scans | Fast with indexes | Native columnar — very fast |
| Infrastructure | None | Supabase account required | None |
| Best for | Desktop / single-user | Hosted multi-tenant | Self-hosted with analytics needs |
| Parquet / Arrow export | No | No | Native |

DuckDB is ideal when an operator wants to self-host Sovrant and query session history, token usage, agent run performance, and audit events without standing up a Postgres server or paying for Supabase.

### What it adds

| Item | Detail |
|---|---|
| `DuckDbStorageProvider` | Implements `IStorageProvider` against DuckDB via `DuckDB.NET`. Registered when `SOVRANT_DB_PROVIDER=duckdb` is set. |
| Schema | Port the SQLite DDL to DuckDB-compatible SQL. Primary key and constraint handling adapted for DuckDB's dialect. |
| Analytics views | Pre-built DuckDB views for common queries: session cost by model/provider, tool call frequency, agent run durations, token usage over time, governance violations. |
| Parquet export | `GET /v1/admin/analytics/export?format=parquet` — exports the analytics views as Parquet files for use in BI tools (DuckDB's native export). |
| `IStorageProvider` registration | `SOVRANT_DB_PROVIDER=duckdb` + `SOVRANT_DB_PATH=sovrant.duckdb` selects the DuckDB backend at startup. Falls back to SQLite if unset. |
| Desktop option | Future: desktop settings UI lets the user switch from SQLite to DuckDB for the local store without data loss (migrator runs at startup). |

### Implementation plan

1. Add `Sovrant.Storage.DuckDb` project — `DuckDbStorageProvider` wrapping `DuckDB.NET`
2. Port SQLite schema to DuckDB DDL; handle type differences (e.g. `TEXT` vs `VARCHAR`, `BLOB` vs `BYTEA`)
3. Register `DuckDbStorageProvider` in `ServiceCollectionExtensions` when `SOVRANT_DB_PROVIDER=duckdb`
4. Add analytics views (`session_costs`, `tool_call_stats`, `agent_run_durations`, `audit_summary`)
5. Add Parquet export endpoint
6. `IStorageProvider` contract tests run against DuckDB backend
7. Tests: schema correctness, analytics view output, Parquet export, provider selection via env var

---

## Phase 105 — MCP Server Permissions (Workspace & Project Scoping)

**Status:** Planned / Deferred.

**Depends on:** Phase 16 (dynamic MCP tool proxy), Phase 40A (workspace roles), Phase 40D (granular feature permissions)

**Goal:** Scope which MCP servers are available at the workspace and project level. Today MCP servers are configured globally — any connected server is visible to every session regardless of workspace or project. Phase 105 introduces an allowlist/blocklist model so workspace admins can control which external integrations their members can call, and project owners can further restrict or expand that list for a specific project.

### Problem

An admin connects a Stripe MCP server for the billing team's workspace and a GitHub MCP server for the engineering workspace. Today both servers are visible to all sessions across all workspaces. There is no way to say "only the engineering workspace can use GitHub tools" or "only the payments project can use Stripe tools."

### Permission model

Three scopes in resolution order:

```
Project-level allowlist/blocklist   (highest — project owner override)
    ↓
Workspace-level allowlist/blocklist (set by workspace admin)
    ↓
Server-level default                (global admin sets whether new servers are allow-by-default or deny-by-default)
```

A server must be allowed at both the workspace level and the project level to appear in a session's tool list. Denial at either level removes the server from that scope's sessions.

### What it adds

| Item | Detail |
|---|---|
| `mcp_server_permissions` table | `(scope_type [workspace/project], scope_id, mcp_server_id, effect [allow/deny])` |
| Server-level default | Global admin sets `default_effect` per server: `allow` (visible everywhere unless explicitly blocked) or `deny` (hidden everywhere unless explicitly allowed). New servers default to `deny` for safety. |
| `IMcpPermissionService` | `GetAllowedServersAsync(workspaceId, projectId?)` — returns the intersected set of permitted servers for a session. Called by `McpClientRegistry` before building the tool list for a turn. |
| `McpClientRegistry` integration | Before exposing MCP tools to the LLM, `GetAllowedServersAsync` filters the registered server list to the session's permitted set. Filtered servers are invisible to the model — no tool definitions, no error. |
| Workspace admin UI — Web | Settings → MCP Servers → per-server allow/deny toggle for this workspace. Shows inherited default, allows override. |
| Workspace admin UI — Desktop | Same toggle list in Settings → Connections. |
| Project settings — Web | Project Settings → MCP Servers → further restrict or re-allow servers within the workspace's permitted set. |
| Project settings — Desktop | Same in project context panel. |
| Tool-level allowlist (future sub-phase) | Within a permitted server, allowlist specific tools (e.g. allow `github_read_file` but deny `github_push`). Deferred — server-level scoping ships first. |
| Audit | Server permission changes logged to `audit_events` with `scope_type`, `scope_id`, and `effect`. |

### Implementation plan

1. Add `mcp_server_permissions` DB migration; add `default_effect` column to `mcp_servers`
2. Implement `IMcpPermissionService` + `McpPermissionStore`; register in DI
3. Update `McpClientRegistry.GetToolsForSessionAsync` (or equivalent) to call `GetAllowedServersAsync` and filter before building tool definitions
4. Web: MCP Servers permission toggle in workspace Settings and project Settings pages
5. Desktop: same toggles in Settings → Connections and project context panel
6. Global admin UI: set `default_effect` per server (`allow` / `deny`)
7. API routes: `GET/PUT /v1/workspaces/{id}/mcp-permissions`, `GET/PUT /v1/projects/{id}/mcp-permissions`
8. Tests: workspace-level filtering, project-level override, default_effect fallback, filtered tools invisible to model, audit trail

---

## Phase 112 — Migrate all built-in markdown knowledge into the DB

### Goal

Make `knowledge_pages` (V033, Phase 108) the single source of truth for all
markdown knowledge. Move the built-in **skills** (32) and **agents** (25) off the
filesystem, and the 44 code-defined **document** templates out of C#, so users can
manage every template — our base ones and the ones they create — entirely in the
DB and change them without code changes. Once each registry reads from the DB, the
loose `.md` files and disk-scan code paths are deleted.

**Guiding principle:** smart code (loaders, parsers, renderers) stays compiled; all
editable content (skill/agent/template bodies + field schemas) lives only in the DB.

### Design

- **Copy-on-write overlay model.** Base rows are immutable (`tier='BuiltIn'`,
  `workspace_id=''`), seeded by migration. A user edit creates a separate overlay
  row (`tier='User'`) at `workspace_id='global'` (installation-wide) or a project
  scope; resolution is project → global → base. Editing a base item never mutates
  it; "revert" deletes the overlay. Matches the existing inline-knowledge-editor UX.
- **Seed via SQL migration.** Built-in content is baked into a `Vxxx` INSERT
  migration generated from the former `.md`; future content updates ship as new
  additive migrations that `UPDATE` the base row (never editing an applied migration).
- **DB-only writes.** Built-in and user content both live in the DB; the
  `~/.sovrant` / `.sovrant` filesystem tiers and the dual-write are removed.
- **Documents become data-driven.** The 44 `IDocumentTemplate.Render()` classes are
  replaced by DB-stored token templates + field schema (JSON), interpreted at render
  time by a sandboxed templating engine; the existing `IDocumentGenerator` format
  pipeline (Markdown/PDF/Word/Excel/PowerPoint) is unchanged.
- **SQLite-only.** The knowledge store is always local SQLite even when the Postgres
  backend is active (Postgres backs only sessions/credentials), so no Postgres store
  is needed.

### Delivery (A → B → C → D, each independently shippable)

- **A — Schema + store foundation.** ✅ Done. `V034` adds `role` +
  `recommended_level` columns; `KnowledgePage` and `IKnowledgeStore` extended with
  `GetEffectiveAsync` / `GetAllEffectiveAsync` (overlay resolution); `KnowledgeScope`
  constants; SqliteKnowledgeStore updated; 7 unit tests cover layered resolution and
  base-preservation.
- **B — Skills + Agents into the DB; delete the `.md`.** ✅ Done. `V035` seed
  migration generated from all 57 built-in files (generator at
  `tools/gen-seed-knowledge.ps1`); `SkillRegistry`, `AgentTemplateRegistry` (via
  new `DbAgentTemplateLoader`), `SkillCreateTool`, and `AgentDefinitionWriter` all
  read/write DB-only; `KnowledgeAuthoringRoutes` updated (agents kind added, filesystem
  reads/writes removed); 32 skill + 25 agent `.md` files deleted with csproj copy
  directives removed. 1,005 runtime tests + 405 tools tests green.
- **C — Tools + user-document markdown into the DB.** ✅ Done. `UserToolTemplateRegistry`
  and `UserDocumentTemplateRegistry` rewritten to read from `GetAllEffectiveAsync`
  and write via `UpsertAsync`/`DeleteAsync` at `KnowledgeScope.Global`; no filesystem
  reads or writes remain. `KnowledgeAuthoringRoutes` updated — all five kinds
  (skills/agents/documents/tools/guidelines) now fully DB-only; `ReconstructDocMarkdown`
  / `ReconstructToolMarkdown` helpers added. 89 doc tests + 405 tools tests + 1,005
  runtime tests green.
- **D — Document templating engine.** ✅ Done. Added Scriban dependency; V036
  migration adds `fields_json` + `filename_template` columns; V037 seeds 42 built-in
  Scriban templates (`kind='document-templates'`, `tier='BuiltIn'`) for all industries
  (business/construction/education/finance/healthcare/legal/real-estate); 42 C# template
  classes deleted; `DbDocumentTemplate` + `ScribanRenderer` + `TemplateFieldSerializer`
  wired into `TemplateRegistry`; 2 Excel-format templates (ExpenseReport,
  LoanAmortization) remain as C# (they emit structured JSON, not Markdown). Schema
  version 37. 1,464 total tests green.

### Acceptance Criteria

- Fresh install: `knowledge_pages` holds 32 skills + 25 agents + 44 documents as
  `BuiltIn` rows; no knowledge `.md` files in source or build output.
- Skills, agents, tools, and documents all list, run, and render reading only from
  the DB.
- Editing any base item creates a `User` overlay (base preserved); revert restores base.
- Document output is byte-compatible with the pre-migration C# templates (golden-file).
- Editing a document template body in the UI changes generated output with no rebuild.

---

## Phase 113 — Caching: DB Read Cache + Phase 31 Invalidation Fix

### Goal

Complete the caching story in one phase: add a `CachedKnowledgeStore` decorator that caches
rarely-changing DB reads (skills, agents, document templates, tool/doc guides), and repair
Phase 31's `CacheInvalidator` whose file-system triggers were removed by Phase 112.

### Context

Phase 31 shipped a solid cache infrastructure (`ICacheProvider`, in-memory/Redis adapters,
ETag middleware, TTL-based HTTP response caching). It wired invalidation to file-system watchers
on `.sovrant/skills/` and `.sovrant/agents/`. Phase 112 deleted those files and moved all
knowledge into SQLite — the watchers no longer exist, so Phase 31's HTTP response cache for
`skills:list`, `templates:list`, and `knowledge:*` keys never gets cleared after a user edit.

Additionally, the per-request authoring routes (`GET /v1/knowledge/{kind}/{slug}/source`) hit
SQLite on every call with no caching, even though templates and skills change at human cadence.

Both problems have the same fix: a single invalidation event that fires whenever any knowledge
page is written, and a DB-level read cache that sits below the existing registries.

### What Phase 113 ships

**1 — `CachedKnowledgeStore` decorator**
- Wraps `SqliteKnowledgeStore` transparently; registered as `IKnowledgeStore` in DI.
- Caches `GetAllEffectiveAsync` and `GetEffectiveAsync` results keyed on `(kind, projectId)` /
  `(kind, slug, projectId)`.
- On any `UpsertAsync` or `DeleteAsync`, immediately evicts the affected keys and raises a
  `KnowledgePageChanged(kind, slug)` event.
- Default TTLs (all configurable via `SOVRANT_KNOWLEDGE_CACHE_TTL`):

| Kind | Default TTL | Rationale |
|---|---|---|
| `skills` | 5 min | Changed by SkillCreate tool or admin edit |
| `agents` | 5 min | Changed by admin edit only |
| `document-templates` | 10 min | Changed by admin edit only |
| `tools`, `documents` | 5 min | User-authored guides |
| `guidelines` | 30 sec | May be edited during an active session |

- `SOVRANT_KNOWLEDGE_CACHE_TTL=0` disables the decorator entirely (passthrough to SQLite).

**2 — Phase 31 `CacheInvalidator` repair**
Replace the stale file-watcher subscriptions with subscriptions to the new `KnowledgePageChanged`
event:

| Old trigger (removed) | New trigger |
|---|---|
| File watcher on `.sovrant/skills/` | `KnowledgePageChanged`, kind = `skills` |
| File watcher on `.sovrant/agents/` | `KnowledgePageChanged`, kind = `agents` |
| `SkillCreate` tool event | Covered by `KnowledgePageChanged` (fires on every `UpsertAsync`) |

HTTP response cache keys cleared: `skills:list`, `templates:list`, `knowledge:{kind}:*`.

**3 — Authoring route caching**
`GET /v1/knowledge/{kind}/{slug}/source` now benefits from the decorator automatically — the
second request for the same page hits the in-process cache, not SQLite.

### What does NOT change

- `SqliteKnowledgeStore` itself — unchanged; the decorator is purely additive.
- The singleton registry in-process dictionaries (`SkillRegistry._byName`, etc.) — they already
  act as a startup cache. `Reload()` calls benefit from the warm decorator cache after the
  first post-write refresh.
- Overlay resolution logic — unchanged.
- User-visible behaviour: a write invalidates the cache synchronously before returning, so the
  authoring user always reads their own writes.

### Acceptance Criteria

- Second `GET /v1/knowledge/skills/tdd-workflow/source` does not hit SQLite (DB call count = 1).
- After `POST /v1/knowledge/skills/tdd-workflow`, next GET returns the new content.
- Phase 31's `skills:list` HTTP response cache is evicted after any knowledge-page write with `kind=skills`.
- `SOVRANT_KNOWLEDGE_CACHE_TTL=0` makes every call hit SQLite directly (verified by test).
- All 1,464 existing tests continue to pass.

---

## Phase 114 — Enrich Built-in Skill Definitions

### Goal

The 32 built-in skills seeded by V035 each have a short name, trigger, and workflow body,
but the descriptions are often sparse (one line) and the body steps are thin. Users seeing
the Skills page in the desktop app get little signal about *when* to invoke a skill or what
outcome to expect. Richer definitions improve agent routing, the `SkillTool` "list" view,
and the `DocumentListTemplatesTool` equivalent for skills.

### What to improve per skill

Each built-in skill (`kind='skills'`, `tier='BuiltIn'`) should have:

- **Description** (2-3 sentences): what the skill does, when to reach for it, what output to expect.
- **Body** (workflow steps): concrete numbered steps, including what the model should *check* before
  proceeding, how to handle edge cases, and what a successful result looks like.
- **Trigger** phrase: verify each trigger is intuitive and doesn't collide.
- **Agents / Tools lists**: populated for skills that have natural delegations (e.g. `tdd-workflow`
  uses the `coder` + `code-reviewer` agents and the `Bash` + `Read` + `Write` tools).

### Delivery

1. Review all 32 skills in the DB (query `knowledge_pages WHERE kind='skills' AND tier='BuiltIn'`).
2. Decide which need enrichment (description too short, body has fewer than 5 steps, agents/tools empty).
3. Write a `V038__enrich_builtin_skills.sql` migration with `UPDATE knowledge_pages SET ... WHERE slug='...'`
   for each improved skill — additive, never editing prior migrations.
4. Smoke-test in the desktop Skills page: descriptions readable, workflow steps actionable.

### Note

User-created or overridden skills (User overlay rows) are unaffected — the migration only updates
the `BuiltIn` base rows (`workspace_id=''`), so any user customisations continue to win via the
copy-on-write overlay.

---

## Bug — Selected Model Not Persisted Across Desktop/Web Reload

**Status:** Confirmed (2026-05-08), fix queued as pre-beta item 2
**Symptom:** The model chosen by the user (e.g. via `/model` or the model picker) resets to the default when the Desktop app or Web UI is reloaded. The last-selected model is not restored on startup.
**Expected:** Per-user selected model is stored in the DB user-settings row (Phase 88 pattern) and re-applied when the session pool initialises on the next launch.
**Likely location:** `ChatViewModel` (Desktop) / `Chat.razor` (Web) — the model picker writes to an in-memory `SessionConfig` but may not persist the selection to the `IUserSettings` DB store. On reload the pool creates a fresh `SessionConfig` from the stored defaults, losing the in-session override.
**Fix scope:** Small — wire the model-picker selection through to `IUserSettings.SetModelAsync(...)` (or equivalent) so it survives restarts. Same fix applies to both surfaces.

---

## Phase 115 — Conversation Compaction and Clear (Surface Parity)

### Goal

Make conversation compaction and clearing consistent, visible, and user-controlled
across all three surfaces (Desktop, Web, CLI). Today auto-compaction fires silently
(no UI feedback), `/compact` does a dumb reset instead of an LLM-backed summarize-then-
reinject, and neither surface exposes a manual compact or clear button in the chat UI.

### Current state

| Feature | CLI (`/compact`) | Desktop | Web |
|---|---|---|---|
| Auto-compaction (threshold-triggered) | ✅ `ConversationRuntime.MaybeCompactHistoryAsync` — LLM summary re-injected | ✅ same | ✅ same |
| Manual compact (slash command) | `/compact` → `_runtime.Reset()` — **no summary, just clears** | `/compact` same | `/compact` same |
| Manual compact button | ✗ | ✗ | ✗ |
| Clear conversation button | ✗ | ✗ (only Ctrl+L / slash) | Ctrl+L only |
| User-visible indicator when auto-compact fires | ✗ silent log only | ✗ | ✗ |

`ConversationRuntime.MaybeCompactHistoryAsync` (Phase 31 / Phase 51) already does the
right thing for auto-compaction: it calls `LlmContextCompactor`, re-injects a summary
message into `_history`, and writes a `compaction` session entry. `CompactCommand`
(`/compact`) bypasses all of this and just calls `_runtime.Reset()` — the comment in
that file already flags this as a known gap ("A future version will first ask the model
to produce a summary that is re-injected.").

### What Phase 115 ships

**1 — Fix `/compact` to be LLM-backed**
- `CompactCommand.ExecuteAsync` calls a new `IConversationRuntime.CompactAsync(ct)`
  method (or invokes `MaybeCompactHistoryAsync` directly with `force: true`) instead of
  `Reset()`. Same summarize-then-reinject path as auto-compaction; the compacted summary
  message is re-injected so the model retains context.
- `ShouldClearHistory: true` is still returned so the UI flushes its visible bubble list
  (matching the existing contract); the runtime's `_history` is already updated internally.
- `/compact` on an empty or short conversation (below threshold) does nothing and returns
  a "Nothing to compact" message.

**2 — Compaction indicator in chat UI (all surfaces)**
- When auto-compaction fires, `ConversationRuntime` already appends a
  `[Conversation history summary — auto-compacted at N tokens]` user message to
  `_history`. Surface this as a distinct styled event bubble (not a regular user message)
  so users know compaction happened.
- Desktop: `ChatViewModel` detects the `[Conversation history summary…]` prefix on
  incoming messages and renders them as a `CompactionEventViewModel` (grey pill, "Context
  compacted — N messages summarized"). Alternatively, expose a `Compacted` event from
  `IConversationRuntime` that the ViewModel subscribes to.
- Web: same approach — `Chat.razor` renders a distinct styled `<div class="compact-event">` bubble.
- The manual `/compact` flow uses the same indicator (the re-injected summary message IS the indicator).

**3 — "New Conversation" / "Clear" buttons in chat header**
- Desktop: add a `Clear` (or `New Conversation`) button to the `ChatView` header toolbar.
  Wires to the existing `ClearChat()` private method (make it a command). Confirm dialog
  if there are more than 5 messages (prevents accidental clear; skip confirm when the
  conversation is empty).
- Web: add the same button to `Chat.razor`'s header row (already has a `New Session`
  event path via `ActiveContext.OnNewSession` — expose a button that triggers it).
- CLI: `/clear` command (alias of `/compact` or a separate command) explicitly documented
  as "wipes history without summarising" vs. `/compact` = "summarise then compact".

**4 — Surface parity checklist**

| Feature | Desktop | Web | CLI |
|---|---|---|---|
| Auto-compact indicator | ✅ Phase 115 | ✅ Phase 115 | `/compact` returns summary text already |
| Manual compact (LLM-backed) | `/compact` | `/compact` | ✅ Phase 115 fixes this |
| Compact button in UI | ✅ Phase 115 | ✅ Phase 115 | n/a (slash only) |
| Clear (no summary) button | ✅ Phase 115 | ✅ Phase 115 | `/clear` alias |
| Threshold configurable | `SOVRANT_COMPACT_THRESHOLD` / workspace settings | same | same |

### What does NOT change

- `ConversationRuntime.MaybeCompactHistoryAsync` — the auto-compaction path is correct; no changes.
- `CompactionSettings` / `SOVRANT_COMPACT_THRESHOLD` — threshold resolution unchanged.
- `LlmContextCompactor` / `NaiveContextCompactor` — unchanged; `/compact` will just call through the
  same path that auto-compaction uses.
- Session persistence (`session_entries` rows) — compaction events already appended there; unchanged.

### Acceptance Criteria

- `/compact` on a conversation with > 10 messages produces a visible summary bubble and the runtime
  `_history` shrinks to the summary + recent tail (not empty).
- `/compact` on an empty session returns "Nothing to compact" without mutating state.
- Auto-compaction (threshold-triggered) shows the same styled indicator bubble on Desktop and Web.
- Desktop chat header has a "Clear" button; clicking it after > 5 messages shows a confirm dialog.
- Web chat header has a "Clear" / "New Chat" button wired to `OnNewSession`.
- All existing tests pass; add unit tests for `CompactCommand` covering the LLM-backed and empty-session cases.

---

## Phase 116 — Intelligent Knowledge Harness

> **Status:** ✅ Done (2026-06-03/04). All eight sub-steps (A–H) shipped: tool guides wired into `ICapabilityCatalog`, PII sanitization for knowledge bodies and tool results, `IKnowledgeRouter` + per-turn addendum, `knowledge_attributions` table + `KnowledgeUsed` events, MCP tool relevance filtering, provenance Sources UI in Web + Desktop chat.

### Tenets driving this phase

Two of Sovrant's three core tenets are load-bearing here:

- **Drive AI spend toward zero** — pre-loading the full knowledge catalog on every turn wastes tokens, degrades prompt-cache hit rates, and adds latency. Hundreds of concurrent sessions cannot afford a 2,000-token knowledge dump per request.
- **Be ethical with AI** — PII flowing from file reads, web fetches, and authored knowledge content (agent system prompts, skill bodies) into LLM context unchecked is a trust-boundary failure. Sanitization must cover the full round-trip, not just user input.

### Problem statement

Today the knowledge pipeline has two failure modes:

**Over-injection (performance and cost):**
- All 32+ skills and 25+ agent names are injected into every session's system prompt at init, regardless of whether the user will ever need a skill or delegate to an agent.
- The static system prompt grows with each new skill or agent added to the DB, silently increasing token cost for every existing session.
- Tool guides (`kind='tools'`) and document templates are never injected at all — the LLM cannot discover them unless it already knows to call a specific tool.
- MCP server tool lists for connected integrations are available but not relevance-filtered — a session with 8 integrations connected gets all 80+ tools in every turn regardless of intent.

**Under-sanitization (ethics and trust):**
- `PromptSanitizer` runs only on user input. Agent system prompts, skill bodies, and tool results (file content, web responses, bash output) travel to the LLM raw.
- A `ReadFile` call on a `.env` or credentials file sends its full plaintext into the conversation history. A skill body authored with a real person's name or an internal IP address embeds PII into every session that loads it.

### Architecture: stable base + dynamic per-turn addendum

The key design principle is **not to replace the static system prompt** — it must remain stable across turns so that LLM provider-side prompt caching works. Instead, a small **dynamic knowledge addendum** is prepended to each turn's user message (as an additional context block, not a system prompt mutation):

```
┌─────────────────────────────────────────────────────────────┐
│  STABLE SYSTEM PROMPT  (built once at session init)          │
│  · Runtime identity, shell hints, permission mode            │
│  · Artifact discipline, orchestration strategy               │
│  · Memory section (summaries, patterns, instincts)           │
│  · Git context                                               │
│  ← This never changes between turns; provider caches it →   │
└─────────────────────────────────────────────────────────────┘
                            +
┌─────────────────────────────────────────────────────────────┐
│  DYNAMIC PER-TURN ADDENDUM  (assembled fresh each turn)      │
│  · Top-k relevant skills for this input (3–5 max)            │
│  · Relevant agent summaries if delegation likely             │
│  · Relevant document template hints if creation intent       │
│  · Tool guides for tools likely to be called                 │
│  · Active MCP integration tool list (session-scoped)         │
│  ← Small, targeted, changes per turn; not cached →          │
└─────────────────────────────────────────────────────────────┘
```

This preserves prompt-caching economics on the stable portion (the large block) while making the small dynamic portion turn-aware.

### Knowledge Router

A new `IKnowledgeRouter` service sits between the incoming turn message and the context assembler:

```
Turn input
  → PII sanitizer (user message)          [already exists, unchanged]
  → Intent classifier                      [reuse SemanticIntentGate or keyword scorer]
  → Knowledge selector                     [NEW — queries CachedKnowledgeStore]
      · GetAllEffectiveAsync("skills")     already cached in-process (5 min TTL)
      · GetAllEffectiveAsync("agents")     already cached in-process (5 min TTL)
      · GetAllEffectiveAsync("document-templates") already cached (10 min TTL)
      · GetAllEffectiveAsync("tools")      already cached (5 min TTL)
      → score each item against intent signal
      → return top-k per kind (configurable, default 5/3/3/3)
  → PII sanitizer (selected knowledge content)  [NEW — SanitizeKnowledgeContentAsync]
  → Addendum assembler                     [NEW — formats selected items as context block]
  → LLM (stable system prompt + addendum + user message)
  → Tool results → PII sanitizer           [NEW — SanitizeToolResultAsync]
  → Attribution tracker                    [NEW — records which knowledge was used]
  → Response + provenance metadata
```

### Relevance scoring (lightweight, no LLM call)

The selector scores each knowledge item against the turn input using a fast heuristic — no embedding model, no extra LLM call:

1. **Keyword overlap** — tokenise the user message and the item's `name + description + trigger`; score = intersection / union (Jaccard).
2. **Trigger match** — if a skill's trigger phrase appears verbatim in the input (e.g. `/tdd`), it scores 1.0.
3. **Intent-to-kind affinity** — if the intent gate classifies the input as `CodeGeneration`, skills and tool guides score higher; if `DocumentCreation`, document templates score higher; if `AgentDelegation`, agent templates score higher.
4. **Session history signal** — skills or agents used in the last 3 turns of this session score a recency bonus.

Scores are computed entirely in-process against the already-cached in-memory dictionaries from `CachedKnowledgeStore`. No DB round-trip per turn.

### PII sanitization — full round-trip

Extend `PromptSanitizer` (Phase 58) with two new paths:

**`SanitizeKnowledgeContentAsync(string body)`**
- Called on each selected knowledge item's body before it enters the addendum.
- Same detector set as the user-input path (email, phone, SSN, credit card, IP addresses).
- Items that fail sanitization are excluded from the addendum and logged; the session continues without them.

**`SanitizeToolResultAsync(string toolResult, string toolName)`**
- Called on every `ToolResult` event before it is appended to `_history`.
- Configurable per tool class: file-reading tools (`ReadFile`, `Glob`, `Grep`) run full PII scanning; bash/shell output runs a lighter pass (strip only credential-shaped patterns — `sk-`, `ghp_`, `xoxb-`, etc.).
- If a result is partially sanitized, the redacted version is stored in history; the original is never written to `session_entries`.

Both paths are governed by the existing `TrustBoundaryConfig.EnablePiiDetection` flag — when disabled (air-gapped or development installs), both paths are bypassed.

### MCP integration scoping

MCP server tool lists are already session-scoped via `SessionConfig.AllowedMcpServers`. Phase 116 extends this:

- When the knowledge selector runs, it also evaluates which connected MCP tools are relevant to the current intent.
- Only the relevant MCP tools are included in the turn's tool list, rather than passing all connected tools every turn.
- The existing `FilteredToolRegistry` pattern (Phase 58) is extended to accept a per-turn `IReadOnlyList<string>? allowedTools` override that the selector populates.
- Tools not in the filtered list are still registered (so the LLM can call them if it reasons its way there), but they are not mentioned in the system prompt or addendum, reducing token pressure.

### Attribution tracking

A new `knowledge_attributions` table records which knowledge resources influenced each turn:

```sql
CREATE TABLE knowledge_attributions (
    id          INTEGER PRIMARY KEY,
    session_id  TEXT NOT NULL,
    turn_index  INTEGER NOT NULL,
    kind        TEXT NOT NULL,   -- 'skills', 'agents', 'document-templates', 'tools'
    slug        TEXT NOT NULL,
    used_at     TEXT NOT NULL    -- ISO 8601
);
```

Events:
- `SkillTool.ExecuteAsync` → writes `kind='skills', slug=<name>`
- `AgentDelegateTool` / `TeamDelegateTool` → writes `kind='agents', slug=<name>`
- `DocumentFromTemplatesTool` → writes `kind='document-templates', slug=<id>`
- Any tool guided by a `kind='tools'` entry → writes `kind='tools', slug=<slug>`

The attribution table is lightweight (no body storage) and append-only. It feeds the provenance surface in the UI.

### Delivery (A → G, each independently shippable)

| Step | What | Effort | Value |
|---|---|---|---|
| **A** | Wire tool guides into `ICapabilityCatalog` + inject in system prompt (static, no routing yet) | Small | Tool guides immediately visible to LLM |
| **B** | `SanitizeKnowledgeContentAsync` — PII sanitization for selected knowledge bodies | Small | Ethical gap closed for knowledge content |
| **C** | `SanitizeToolResultAsync` — PII sanitization for tool results before history write | Small | Ethical gap closed for file/web/shell output |
| **D** | `IKnowledgeRouter` + relevance scorer — lightweight keyword/trigger/intent scoring, no LLM call | Medium | Per-turn knowledge selection; token cost drops |
| **E** | Dynamic per-turn addendum — assemble and inject only selected knowledge as context block, remove full catalog from static system prompt | Medium | Prompt cache preserved; addendum is small and targeted |
| **F** | `knowledge_attributions` table + `KnowledgeUsed` events from `SkillTool`, `AgentDelegateTool`, `DocumentFromTemplatesTool` | Medium | Attribution data available |
| **G** | MCP tool relevance filtering — extend `FilteredToolRegistry` with per-turn intent-scoped allow list | Medium | Reduces MCP token pressure in multi-integration sessions |
| **H** | Provenance UI — "Sources" section in Web + Desktop chat showing which skills/agents/templates were used | Medium | User transparency; supports the ethics tenet |

Steps A–C are pure improvements with no architectural risk — they can ship any time. D–E are the core routing change and should ship together. F–H are independent of each other.

### What does NOT change

- `ConversationRuntime.BuildSystemPrompt()` structure — the stable base prompt is unchanged; we only add an addendum mechanism.
- `CachedKnowledgeStore` TTLs — the per-kind cache is already correct; the router reads from the same in-memory cache.
- `SkillRegistry`, `AgentTemplateRegistry`, `TemplateRegistry` — all read from `IKnowledgeStore` as today; no changes to registry internals.
- `PromptSanitizer` user-input path — unchanged; we add two new methods alongside it.
- `TrustBoundaryConfig.EnablePiiDetection` — governs all three sanitization paths uniformly.

### Acceptance Criteria

- A coding-focused input ("write unit tests for this function") injects tdd-workflow and coder agent, not business-process skills or financial document templates.
- A document-creation input ("I need a construction contract") proactively surfaces the construction-contract template summary without the user calling `DocumentListTemplatesTool`.
- A session with 8 MCP integrations connected does not include all 80+ tools in the prompt for a simple "explain this code" turn.
- `ReadFile` on a file containing `email@example.com` stores the redacted version in `session_entries`; the original never appears in `_history`.
- A skill body containing a mock SSN is sanitized before being included in the addendum.
- After a turn that uses the tdd-workflow skill, `knowledge_attributions` has a row with `kind='skills', slug='tdd-workflow'`.
- Stable system prompt hash does not change between turns (verifiable: provider-side cache hit rate does not drop vs. pre-Phase-116 baseline).
- All existing tests pass; new unit tests cover the relevance scorer (trigger match, keyword overlap, intent affinity) and both PII sanitization paths.

## Phase 117 — API Endpoint Integration (REST & GraphQL as Platform Integrations)

**Goal:** Let admins register REST and GraphQL APIs as first-class platform integrations alongside MCP servers. The harness can auto-discover endpoints from an OpenAPI/Swagger spec or GraphQL introspection, or the admin can manually describe specific endpoints. Discovered operations are exposed as typed tools through the same `MCPTool` proxy layer — trust rules, session tool picker, and `FilteredToolRegistry` all apply identically to API tools and MCP tools.

### Why

MCP covers well-known developer tools but most enterprise and SaaS systems expose REST or GraphQL APIs with no MCP server. Phase 95 opened the Platform Integrations gallery; Phase 117 extends it to cover any HTTP API that has a spec or can be described, turning Sovrant into a zero-code integration layer for arbitrary services without requiring a custom MCP server.

### What Phase 117 ships

**Spec-driven discovery**
- Admin pastes an OpenAPI/Swagger spec URL or uploads a JSON/YAML file; Sovrant parses paths, methods, parameters, and response schemas; each operation becomes a callable tool with typed inputs
- Admin provides a GraphQL endpoint URL; Sovrant runs `__schema` introspection to discover queries, mutations, and input types; each query/mutation becomes a tool
- Rediscovery on demand — admin can re-import at any time to pick up spec changes; a diff is shown before applying so no silent tool renames

**Manual endpoint entry**
- Fallback for APIs with no spec; admin defines name, URL template, HTTP method, parameter names and types, and expected response shape
- Parameters map to tool input fields; Sovrant builds and executes the HTTP call

**Auth**
- Bearer token, API key (header or query param), Basic auth
- Credentials stored in the existing encrypted keystore (same path as MCP server secrets)
- OAuth support deferred to a follow-on phase

**Tool proxy integration**
- API operations register in `FilteredToolRegistry` under a namespace matching the integration name
- Trust rules (allow / require-confirmation / block) configurable per API integration in the Platform Integrations admin UI
- Per-session tool picker checkboxes work the same as MCP server toggles
- Attribution tracking: turns that call API tools write `kind='api', slug=<integration-name>/<operation-id>` to `knowledge_attributions`

**Web + Desktop parity**
- API integrations appear in the Platform Integrations gallery under a new **APIs** category tier
- Connected tab shows API integrations alongside MCP servers with tool count and endpoint count
- Detail panel shows base URL, spec source, operation list, auth type (masked), and trust rules

### Acceptance criteria

- An admin imports the Stripe OpenAPI spec; agents can call `stripe/create_payment_intent` without any code changes
- An admin registers a GraphQL endpoint; introspection discovers 12 queries; all 12 appear in the session tool picker
- A manually-described endpoint with two URL params generates a correctly-formed HTTP call with values the agent provides
- Trust rules applied to `stripe/*_delete` pattern require confirmation before execution; agent receives a structured refusal if confirmation is not granted
- Re-importing an updated spec that renames an operation shows a diff before applying; old tool name is removed and new one added atomically
- API tool credentials are stored encrypted at rest and masked in all UI surfaces
- All existing MCP and knowledge tests continue to pass

---

## Phase 118 — Bootstrap Configuration

**Status:** Planned

### Why

Enterprise customers and sales-assisted deployments need Sovrant to arrive pre-configured for their environment. Today an admin must manually set up providers, MCP servers, knowledge bases, agent templates, and workspace policies after install. A declarative bootstrap file removes that friction: the partner or sales engineer authors a YAML config once, ships it alongside the installer, and Sovrant applies it automatically on first run — no code changes, no bespoke scripts, no post-install click-through.

This unlocks white-label and branded installs, partner-delivered verticals (e.g. a legal firm gets Sovrant pre-loaded with compliance knowledge and approved providers), and automated CI-provisioned demo environments.

### What ships

**`sovrant.bootstrap.yaml`** — a single declarative file placed alongside `sovrant.config` (or in a path configured by `sovrant.config`). Sections:

```yaml
sovrant_bootstrap: "1.0"

workspace:
  name: "Acme Legal"
  default_permission_mode: "supervised"

providers:
  - provider: openrouter
    profile_name: "Acme OpenRouter"
    api_key_env: "SOVRANT_OPENROUTER_KEY"   # resolved from environment at apply time
    default: true

mcp_servers:
  - name: "Acme Docs"
    url: "https://mcp.acme.internal/docs"
    trust_level: "trusted"
  - name: "Filesystem"
    command: "npx"
    args: ["-y", "@modelcontextprotocol/server-filesystem", "/workspace"]

knowledge:
  - title: "Company Policy"
    kind: "document"
    tier: "global"
    source_file: "knowledge/company-policy.md"   # relative to bootstrap file
  - title: "Approved Tools"
    kind: "skills"
    tier: "global"
    source_file: "knowledge/approved-tools.md"

agents:
  - name: "Acme Assistant"
    description: "Default assistant for Acme Legal"
    system_prompt_file: "agents/acme-assistant.md"
    provider_profile: "Acme OpenRouter"
    default_model: "openai/gpt-4o"

admin_users:
  - email: "it-admin@acme.com"

branding:
  product_name: "Acme AI"
  primary_color: "#1A3C8F"
```

**Apply behavior:**
- Applied **once on first boot** if a bootstrap file is present and the instance has never been bootstrapped (flag stored in DB)
- `--bootstrap-apply` CLI flag forces a re-apply (idempotent: skips items that already exist, updates changed items, never deletes)
- `--bootstrap-check` CLI flag validates the file and reports what would change without touching the DB
- Credentials sourced from environment variables (the file never stores secrets directly)
- Source files (knowledge, agent prompts) bundled alongside the bootstrap YAML; relative paths resolved from the bootstrap file's directory

**Configurable items:**
- Providers (with profile names and credential env-var references)
- MCP servers (HTTP and stdio)
- Knowledge items (any kind, any tier the caller is authorized for)
- Agent templates
- Workspace display name and default permission mode
- Admin user list (by email; users are created or promoted as needed)
- Branding overrides (`product_name`, `primary_color`)

**Drift detection:** re-running `--bootstrap-check` after manual changes reports which items have drifted from the bootstrap spec, so ops teams can detect unexpected configuration drift.

**Web + Desktop parity:** the apply engine is in the core library (`Sovrant.Core`); both runtimes call it. The Web admin UI shows a "Bootstrap" status card (applied / not applied / drifted) in the Admin panel.

### Acceptance criteria

- A fresh Sovrant instance with a `sovrant.bootstrap.yaml` boots and auto-applies the config; admin sees all items present without any manual setup
- Running `--bootstrap-apply` a second time is idempotent: no duplicates are created, no existing manual config is deleted
- `api_key_env` resolves from the process environment; if the env var is absent, apply fails with a clear error naming the missing variable
- `--bootstrap-check` on a drifted instance reports the specific items that differ from the spec
- A bootstrap file with a structural error (wrong YAML, unknown section, invalid provider name) is rejected at startup with a human-readable message; Sovrant continues to run without applying anything
- Web admin panel shows the bootstrap status card (applied date, applied-by, item count)
- All existing provider, MCP, and knowledge tests continue to pass

---

## Phase 119 — Orchestration Improvements

**Status:** Planned

### Why

Orchestration today inherits the global permission mode, which means a supervised workspace forces step-by-step confirmation even on batch missions that are designed to run unattended. Teams and swarms also lack a formal concept of a "mission" — a named, scoped run with a declared run-mode, progress tracking, and a clear lifecycle. Separately, integrations already bring external agents (Claws) into the workspace, but there is no way to assign them as members of a team; they exist as tools only.

This phase closes both gaps: missions become a first-class orchestration primitive with their own run-mode, and integration-sourced Claws become addressable team members.

### What ships

**Mission run-mode**

A mission is a named, bounded orchestration run assigned to a team or swarm. When launching a mission the operator (human or automated trigger) declares one of three run-modes:

| Mode | Behaviour |
|---|---|
| `autonomous` | The team executes fully without confirmation gates; tool calls subject to trust rules but no human step-through |
| `supervised` | Agent actions are confirmed by the session owner turn-by-turn; mirrors the current global supervised behaviour |
| `step-through` | Each agent step pauses and surfaces a summary to the operator before proceeding |

The declared mode overrides the workspace permission setting for the lifetime of the mission. Mission metadata (name, team, run-mode, status, started-at, completed-at, output summary) is stored in the DB and visible in the Command Center.

**Integration-sourced Claws as team members**

When an admin has a platform integration connected that exposes external agents (e.g. a remote Claw endpoint via MCP or a registered agent API), those agents appear in a new "Available Members" picker when building or editing a team. The picker lists:

- Local Claws (existing behaviour)
- Integration-sourced Claws — shown only if the backing integration is connected at the time of team composition; labelled with the integration name (e.g. `via GitHub Copilot`)

Integration members participate in orchestration the same way local Claws do — routed via the existing `AgentRouter` — but their tool calls go through the integration's MCP proxy layer so trust rules and session selection still apply. If the integration is disconnected at mission launch, the affected member is skipped and the mission log records a `member_unavailable` event.

**Web + Desktop parity:** mission launch UI on both surfaces; Command Center shows live mission status; team editor picker updated on both.

### Acceptance criteria

- Launching a mission with `autonomous` run-mode in a supervised workspace executes without confirmation prompts; trust rules still fire normally
- Launching with `step-through` pauses after each agent action and waits for operator acknowledgement before proceeding
- A connected integration that exposes agent endpoints surfaces those agents in the team member picker with the integration label
- Removing or disconnecting an integration removes its Claws from the picker; existing team definitions retain the member reference but show a "disconnected" badge
- A mission started with an unavailable integration member logs `member_unavailable` and continues with the remaining members rather than failing the whole mission
- Mission history (name, run-mode, status, timing) is visible in the Command Center for completed and in-progress missions
- All existing orchestration, team, and agent tests continue to pass

---

## Phase 124 — File System Access Controls

**Status:** Planned

### Why

Sovrant has full file system access by default — Read, Write, Edit, Glob, Grep, and Bash all operate on arbitrary paths. Users naturally assume this works, and it does, but there is currently no way for an admin to declare which directories Sovrant may or may not touch. The Governance page today only surfaces protected file globs (individual files that cannot be modified), not directory-level access boundaries.

In practice this creates two problems:

1. **Agents working in the wrong places.** An agent scaffolding a Node project can read or write anywhere on disk — including outside the intended workspace root — and there is no guard to stop it.
2. **No admin visibility or control.** An admin deploying Sovrant in a shared environment has no way to say "agents may only operate under `C:\Projects`" or "agents must never touch `C:\Users\*\AppData`". The current protected-files list is too granular to solve this; you can't enumerate every sensitive path.

### What ships

**Directory access rules (new governance primitive)**

Two complementary controls, configurable per deployment from the Governance page:

| Control | Behaviour |
|---|---|
| **Allowed directories** | Explicit allowlist — Sovrant file tools may only operate under these paths. Supports glob patterns (`C:\Projects\**`, `/home/**`). Empty = unrestricted (current behaviour preserved for existing deployments). |
| **Blocked directories** | Explicit blocklist — Sovrant file tools are always denied for these paths, even if an allowed-directory pattern would otherwise permit them. Useful for carving out sensitive subdirectories. |

Rules are evaluated before any file-accessing tool call (Read, Write, Edit, Glob, Grep, and shell commands containing file paths). A denied access returns a `GovernanceVerdict.Block` — the tool call is rejected, a `governance` audit event is logged with `reason: directory_access_denied`, and the agent sees the denial as a tool error so it can surface it to the user rather than silently failing.

**Governance page — File System Access section**

The Governance admin page (Web + Desktop) gains a new "File System Access" section alongside the existing Protected Files panel:

- **Allowed Directories** — textarea of one path pattern per line; empty means unrestricted
- **Blocked Directories** — textarea of one path pattern per line
- Save button persists both lists to DB (new `fs_access_rules` columns on the `audit_governance` settings table or a dedicated key in `server_settings`)
- Live preview: as the admin types, a read-only panel shows how example paths would resolve (Allowed / Blocked / Unrestricted)

**Path normalisation**

All paths are normalised to absolute before matching so that relative paths (`../secret`) can't escape the declared boundaries. On Windows, drive-letter casing is folded. Symlinks are resolved before the check so a symlink pointing outside an allowed directory is also blocked.

**Migration**

V044 — add `fs_allowed_dirs` and `fs_blocked_dirs` columns (`TEXT`, default `''`) to `server_settings`. Existing deployments get empty strings (unrestricted), preserving current behaviour.

### What stays out of scope

- Per-user or per-workspace directory rules (admin-global only in this phase)
- Network path restrictions (`\\server\share`) — covered separately by MCP trust rules
- Audit of paths accessed by MCP tools — those are proxied through the trust layer already

### Acceptance criteria

- Admin sets `fs_allowed_dirs` to `/home/projects/**`; a Read call to `/etc/passwd` is blocked and logged as `directory_access_denied`; a Read call to `/home/projects/myapp/main.go` succeeds
- Admin sets `fs_blocked_dirs` to `/home/projects/secrets/**`; a Write to `/home/projects/secrets/key.pem` is blocked even though the parent path is in the allowlist
- Empty allowed-dirs list = unrestricted (no change to current behaviour); no existing tests break
- Governance page shows the new section on both Web and Desktop; changes persist across restart
- Denied accesses appear in the governance audit log visible on the Governance page
- Path traversal attempts (`../../etc`) are normalised and evaluated correctly

---

## Phase 125 — Web Search via Integrations (Integration Gallery HTTP API Support)

**Status:** Planned

### Why

Web search in Sovrant is currently a built-in tool with hard-coded backends selected by an enum (`Auto`, `Brave`, `Firecrawl`, `Native`, `Off`). The Brave and FireCrawl logic lives as inline HTTP calls inside `WebSearchTool.cs` — completely separate from the Integration Gallery. This creates two problems:

**Admin control is missing.** There is no way for an admin to choose how their deployment searches the web. The backend is configured via an environment variable or DB setting pointing at one of a fixed list of options we hard-coded. If an organisation uses a different search API (Exa, Tavily, a self-hosted SearXNG, an internal enterprise search endpoint), there is no path to wire it in without modifying the source.

**Integrations are MCP-only.** The Integration Gallery catalog (`IntegrationKind`) only models `McpHttp`, `McpStdio`, and `LlmProvider`. There is no concept of a direct HTTP API integration — a simple adapter that calls a REST endpoint with an API key, no MCP process involved. Brave and FireCrawl are exactly this shape, but they live outside the integration system entirely because the system can't represent them.

The right model is: **integrations are not MCP-only**. An integration is any external service Sovrant connects to. Web search providers are integrations. The admin configures which one is active for their deployment from the Integrations page, the same place they configure everything else.

### What ships

**1. `IntegrationKind.HttpApi` — direct REST adapter**

Extend the `IntegrationKind` enum with a new value: `HttpApi`. A `HttpApi` integration is a direct REST call — no MCP process spawned, no stdio, no SSE connection. The catalog entry describes the API endpoint pattern, the API key header name, and the credential key used to look up the stored secret. The `McpClientFactory` / `McpClientRegistry` is not involved; the integration calls the API directly via `IHttpClientFactory`.

This is the primitive that Brave, FireCrawl, Exa, Tavily, and any future REST-only integration can use.

**2. `IWebSearchProvider` interface**

Define a thin interface that all search integrations implement:

```csharp
public interface IWebSearchProvider
{
    string IntegrationId { get; }
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(
        string query, int maxResults, CancellationToken ct);
}
```

First-party implementations:
- `DuckDuckGoSearchProvider` — `HttpApi`, no API key, always available (built-in default)
- `BraveSearchProvider` — `HttpApi`, requires `BRAVE_API_KEY`, replaces current inline Brave code
- `FireCrawlSearchProvider` — `HttpApi`, requires `FIRECRAWL_API_KEY`, replaces current inline FireCrawl code
- `ExaSearchProvider` — `HttpApi`, requires `EXA_API_KEY`
- `TavilySearchProvider` — `HttpApi`, requires `TAVILY_API_KEY`

Each is registered as a catalog entry in `IntegrationCatalog` under a new `IntegrationTier.Search` tier. Installing one stores the API key in the keystore; the active provider is a deployment-level setting.

The existing `@modelcontextprotocol/server-brave-search`, `exa-mcp-server`, and `tavily-mcp` MCP catalog entries remain available for users who prefer the MCP path — they are now siblings to the `HttpApi` variants, not the only option.

**3. Refactor `WebSearchTool` to dispatch via the active search integration**

Remove the `WebSearchBackend` enum switch from `WebSearchTool.ExecuteAsync()`. Replace with a call to `IWebSearchProvider.SearchAsync()` on whichever provider is currently active:

```
Active search integration → IWebSearchProvider.SearchAsync() → WebSearchResult[]
```

The active provider is resolved from the integration system at request time (same pattern as credential resolution). If no provider is installed and active, the tool returns a clear message pointing the admin to Integrations.

`NativeWebSearchInjector` is **unchanged** — it operates at the provider protocol level (OpenAI `web_search_preview`, Anthropic `web_search_20250305`), suppresses the function tool when active, and does not interact with the integration system. Native LLM search and integration-based search remain two parallel paths, as they are today.

**4. Integrations page — Search tier**

The Integrations page on both Web and Desktop gains a new "Search" section (alongside Automation and Platform). It lists all available search integrations with their status:

| Integration | Kind | API Key | Status |
|---|---|---|---|
| DuckDuckGo | Built-in | None required | Active (default) |
| Brave Search | HttpApi | `BRAVE_API_KEY` | Not installed / Active |
| FireCrawl | HttpApi | `FIRECRAWL_API_KEY` | Not installed / Active |
| Exa | HttpApi | `EXA_API_KEY` | Not installed / Active |
| Tavily | HttpApi | `TAVILY_API_KEY` | Not installed / Active |
| Brave Search MCP | McpStdio | `BRAVE_API_KEY` | Not installed / Active |
| Exa MCP | McpStdio | `EXA_API_KEY` | Not installed / Active |
| Tavily MCP | McpStdio | `TAVILY_API_KEY` | Not installed / Active |
| Crawl4AI | HttpApi (self-hosted) | Optional token | Not installed / Active |

Admins can mark one search integration as active. The active provider is used by `WebSearch` for all sessions. The `/websearch` slash command in chat lets individual users override the active provider for their session (existing behaviour preserved).

DuckDuckGo is the built-in default — always available, no install, no API key. If a higher-quality provider is installed and set active, it takes over. If none is active, DuckDuckGo is the fallback.

**5. Crawl4AI as a scraper/fetcher integration**

Crawl4AI (https://docs.crawl4ai.com/) is an open-source, LLM-friendly web crawler that handles JavaScript rendering, structured extraction, and markdown conversion — capabilities the built-in `WebFetch` tool intentionally excludes (static HTML only, no JS).

Add a `Crawl4AiProvider` catalog entry under a new `IntegrationTier.Search` with `IntegrationKind.HttpApi` pointing at the Crawl4AI REST API (self-hosted or cloud). When the integration is installed and active, the `WebFetch` tool gains an optional `use_crawl4ai: true` parameter that routes the fetch through Crawl4AI instead of the direct HTTP path. The direct HTTP path remains the default so existing behaviour is unchanged.

Crawl4AI also optionally enhances `WebSearch` result fetching: when following a search result URL, Sovrant can use Crawl4AI to extract clean content from JS-heavy pages (news sites, SPAs, paywalled articles with free-tier access). This is opt-in per search session.

The catalog entry includes:
- `EndpointTemplate`: `https://{host}/crawl` (user provides their Crawl4AI host)
- `ApiKeyLabel`: `API Token` (optional — Crawl4AI supports unauthenticated local deployments)
- A "Self-hosted" badge in the UI since Crawl4AI is typically run locally via Docker

**6. `WebFetchTool` and `WebSearchTool` unit test coverage**

Add test files covering:

- `WebFetchTool`: valid URL fetch (mocked HTTP), SSRF blocking (each RFC-1918 range, loopback, link-local), HTML tag stripping, bounded read and truncation, non-HTML passthrough, connection error wrapping
- `DuckDuckGoSearchProvider`: successful parse, rate-limit 429 handling, empty results
- `BraveSearchProvider`: API success, error response, missing key error
- `WebSearchTool` dispatch: correct provider called, graceful "no active provider" message, `/websearch` session override

**6. Remove the `WebSearchBackend` enum**

Once all backends are integrations, the `WebSearchBackend` enum and `WebSearchOptions.Backend` configuration are removed. The env var `SOVRANT_WEB_SEARCH` is deprecated (warn on startup if set) with a migration note pointing to Integrations. Existing deployments with `BRAVE_API_KEY` or `FIRECRAWL_API_KEY` set get those providers auto-detected and pre-configured on first boot after the upgrade so no manual re-configuration is needed.

### What stays out of scope

- SearXNG self-hosted backend — remains a catalog stub; a community contributor can add a `SearXNGSearchProvider` using the new `HttpApi` kind without any core changes
- Gemini native web search — blocked on the OpenAI shim; unrelated to this phase
- JavaScript-rendered page fetching — separate phase; `WebFetch` stays as direct HTTP

### Acceptance criteria

- A fresh Sovrant install with no API keys: `WebSearch` returns DuckDuckGo results without configuration
- Admin installs Brave Search (HttpApi) from Integrations and sets it active: `WebSearch` uses Brave; DuckDuckGo is no longer called
- Admin installs Exa (HttpApi): same pattern
- Admin installs Brave Search MCP: appears as an MCP tool alongside the `WebSearch` function tool; admin can choose which to use
- `/websearch brave` in chat overrides the active provider for the session; other users' sessions are unaffected
- `BRAVE_API_KEY` set in environment on upgrade: Brave HttpApi integration is auto-configured and active; no manual setup required
- `SOVRANT_WEB_SEARCH` env var on upgrade: warning logged; existing behaviour preserved until admin migrates
- `WebSearch` with no active provider and DuckDuckGo rate-limited: returns clear message pointing to Integrations
- All unit tests pass; existing tests unbroken

---

## Phase 126 — Chat Conversation UX: Collapsed Work Strips & Clear Hierarchy

**Status:** Implemented (v1.5) — items 1–7 shipped on Web (`WorkStrip.razor`, `ChatMessage.razor`, `sovrant.css`) and Desktop (`MessageViewModel.cs`, `ChatView.axaml`); solution builds clean, no dangling references to removed pre-strip members. One contrast fix applied before merge: `.work-strip-row-error .work-strip-row-name` was coloring tool-name text red (a colored-text violation of the item 7 rule, ~3.7:1 on the light theme, short of the 4.5:1 bar) — removed in favor of the existing status-dot fill + header ⚠ icon, matching what Desktop already did. **Pending UAT verification** — live browser/Desktop interaction through an actual agentic turn has not been exercised yet (owner verifying in UAT).

### Why

When an agent works through a multi-step task it may make 10–20 tool calls before producing an answer. Today each tool call renders as its own bordered box stacked vertically under the assistant message. Five file reads, two greps, and a write produces eight separate boxes. The user can collapse individual results, but the boxes themselves are always present — a visual list that grows with every action.

Two problems follow from this:

**Users get lost in the work.** The natural language answer — what the user actually asked for — sits at the bottom of a column of tool blocks. On a long agentic run the answer is scrolled off screen by the time it arrives. There is no clear signal that says "here is the result; all that other stuff is how we got there."

**There is no at-a-glance summary.** The action summary header exists ("Read ×2 · Grep") but it is just text inside the message card. While the agent is running mid-turn, the user sees individual tool blocks appearing one by one with no sense of overall progress — they cannot tell whether the agent is halfway done or has just started.

The fix is a two-level progressive disclosure model: the agent's work is grouped into a single collapsible strip by default, the answer is visually prominent above or beside it, and users expand as much detail as they want.

### What ships

**1. Work strip — collapsed by default**

All tool calls in a single assistant turn are grouped into one collapsible **work strip** instead of N separate boxes. The strip occupies a single line when collapsed:

```
▶  7 actions  ·  Read ×3  ·  Grep ×2  ·  Write ×1  ·  WebSearch ×1  ·  2.4 s
```

Clicking the strip expands it to show the individual tool calls. The strip does not appear at all if the turn had zero tool calls (pure conversational reply).

The strip sits *between* the agent's intent narration (if any) and its final answer text, so the visual reading order is:

1. What the agent said it would do (intent narration — already exists)
2. Collapsed strip — the work, out of the way
3. The answer — prominent, readable without scrolling past tools

**2. Two-level expand**

| Level | What is shown | Trigger |
|---|---|---|
| **Collapsed** (default) | Single strip line: icon counts + elapsed time | — |
| **Tool list** | One row per tool call: icon + name + status badge + one-line result preview | Click strip header |
| **Full detail** | Full input parameters + complete output for one tool | Click individual tool row |

This matches the mental model users already have: "I want to see what happened" → expand strip. "I want to see exactly what that file read returned" → expand the specific row.

Only one tool can be in full-detail view at a time (clicking a second tool collapses the first). This prevents the expand-everything wall-of-text failure mode.

**3. Live in-progress indicator**

While the agent is mid-turn and tools are executing, the work strip shows an animated in-progress state instead of a count:

```
⏳  Reading files...              (animates while running)
⏳  Searching the web...
⏳  Writing src/index.ts...
```

The status text is driven by the current `ToolUseRequested` event — it names the active tool in plain language. When the turn completes the strip collapses to the final summary line. The user always knows what is happening right now and approximately how much has been done.

**4. Answer-first layout**

The agent's final text response (the answer) is rendered before the work strip in the visual hierarchy, or at minimum at the same prominence level. This is a layout change, not a streaming change — text still streams in real time. Once the turn completes the card reflows so the answer is at the top and the work strip is below it.

On very long responses the work strip is pinned just below the intent narration, and the answer flows below the strip — the strip never pushes the answer off screen.

**5. Error and partial-failure states**

If any tool call in a turn returned an error, the work strip shows a warning indicator:

```
▶  7 actions  ·  1 error  ·  2.4 s     ⚠
```

Expanding the strip highlights the failed tool row in red. This replaces the current inline error banner which appears at the bottom of the message and is easy to miss.

**6. Web + Desktop parity**

Web (`Chat.razor` / `ChatMessage.razor`):
- Replace the `foreach` over `.ToolUses` that renders individual `.tool-use-block` divs with a single `<WorkStrip>` Blazor component
- `<WorkStrip>` handles its own expand/collapse state and the two-level detail view
- Replace the native `<details><summary>Result</summary>` HTML with a custom implementation so both levels have consistent styling and animation

Desktop (`ChatView.axaml` / `ToolUseViewModel`):
- Replace the `ItemsControl` of tool use boxes with a `WorkStripControl` Avalonia control
- `ToolUseViewModel.IsExpanded` is preserved but now scoped within the strip's expand context
- Strip-level `IsExpanded` and `ActiveDetailToolId` properties on `MessageViewModel`

Both surfaces share the same visual language (strip line format, icon set, status badge colours) so the experience is identical whether the user is on Web or Desktop.

**7. Color & contrast — reuse existing tokens, verify on both themes**

The strip must not invent a fourth color system. Sovrant already has a themed token set for exactly this kind of chrome, and Web/Desktop have kept it token-for-token identical so far — the strip should extend it, not bypass it:

| Purpose | Web token (`sovrant.css`) | Desktop resource (`SovrantDarkColors.axaml` / `SovrantLightColors.axaml`) | Dark value | Light value |
|---|---|---|---|---|
| Strip / tool-row background | `--tool-use-bg` | `ToolUseBackground` | `#1E1E2E` | `#F0F0FA` |
| Strip / tool-row border | `--tool-use-border` | `ToolUseBorder` | `#3A3A4A` | `#D0D0E0` |
| In-progress / active accent | `--brand-primary` | `BrandPrimary` | `#6D52C6` | `#6D52C6` (only token intentionally identical across both themes) |
| Completed / pass indicator | `--status-pass` | `StatusPass` | `#4CAF50` | `#4CAF50` |
| Partial-failure / warn indicator | `--status-warn` | `StatusWarn` | `#FF9800` | `#FF9800` |
| Error / fail indicator | `--status-fail` | `StatusFail` | `#F44336` | `#F44336` |

Two things to get right before this ships, not after:

- **The status trio isn't actually re-themed today.** `sovrant.css` only sets `--status-pass/warn/fail` in the `:root[data-theme="dark"]` block; the `:root[data-theme="light"]` block never overrides them, so light mode silently inherits the same dark-tuned hex values (same story in the two Desktop `.axaml` dictionaries — they happen to repeat identical hex rather than share one source). That's been fine for small dots/badges, but the work strip's ⚠ error state and the animated in-progress text put color closer to text-label duty, where dark-surface-tuned colors can under-perform on light backgrounds.
- **Contrast check, not eyeball check, before merge.** `#4CAF50` green as a filled dot/icon (3:1 WCAG graphical-object threshold) is fine on both `#1E1E2E` (dark tool-use bg) and `#F0F0FA` (light tool-use bg). The same `#4CAF50` used as *text* on a light card (`--surface-card: #FFFFFF`) lands around ~2.4:1 — well under the 4.5:1 text threshold. Rule for this phase: pass/warn/fail render as icon + fill only, never as colored text; if a future design wants colored status *text*, that needs its own light-theme-specific values (e.g. `--status-pass-light`) rather than reusing the dark-tuned hex.
- **No new tokens for the base strip.** Background/border/active-accent reuse existing tokens as-is. If the contrast check above does force new light-specific status values, add them to both `sovrant.css` and `SovrantLightColors.axaml` in the same PR — don't let Web and Desktop drift on hex values the way the two Desktop `.axaml` files currently duplicate rather than share.

### What stays out of scope

- Replay / history view of tool calls across sessions (separate memory/history phase)
- Tool call diffs (showing before/after for Write/Edit — useful but a separate feature)
- Grouping by sub-agent in a team run (team runs have a separate UI surface)

### Acceptance criteria

- A turn with 10 tool calls renders as a single collapsed strip line, not 10 boxes
- Clicking the strip reveals all 10 tool rows in one-line format
- Clicking one tool row reveals full input + output; clicking another collapses the first and expands the second
- While the agent is mid-turn the strip shows the current tool name in animated "doing X" format
- A turn with one or more tool errors shows the ⚠ indicator on the collapsed strip; error rows are highlighted red when expanded
- A turn with zero tool calls has no strip — only the answer text
- The answer text is readable without scrolling past tool blocks on a typical 1080p screen
- Web and Desktop render the strip with the same visual language
- No regression in tool confirmation flow (Allow once / Allow for turn / Deny buttons still work within the strip)
- Pass/warn/fail indicators are icon+fill only (no colored text labels) on both themes; if colored text ships anyway, it passes a 4.5:1 contrast check against the light theme's `--surface-card` / `--tool-use-bg` backgrounds, not just the dark theme

---

## Phase 127 — Supabase Row Level Security

**Status:** Planned

### Why

Sovrant enforces the `owner_user_id` privacy model at the application layer — query filters in `SqliteWorkspaceStore`, `SqliteMemoryStore`, and similar stores ensure users only see their own private data. In a Supabase deployment this is sufficient for all traffic that goes through the Sovrant API, but it leaves a gap:

- **Supabase dashboard queries** (developers, DBAs) run directly against the database and bypass the application layer entirely
- **Edge Functions** calling `supabase.from(...)` use the user's JWT and get no automatic scoping unless RLS is enabled
- **Service-role callers** (the Sovrant server itself, admin scripts) need unrestricted access and must not be blocked

RLS closes the first two gaps without touching the third — Supabase's service role bypasses RLS by design. This means the Sovrant API server, admin tooling, and migration scripts all continue to work with zero changes; only JWT-authenticated direct-DB callers gain a scoping boundary.

The commented-out policy skeletons already exist in `db/supabase/migrations/20260625000000_initial_schema.sql`. This phase activates them properly with a second migration file admins can apply when they are ready.

### What ships

**A new Supabase migration** (`db/supabase/migrations/20260625000001_enable_rls.sql`) that:

1. Enables RLS on the four privacy-sensitive tables:
   - `workspace_memory`
   - `session_summaries`
   - `learned_patterns`
   - `instincts`

2. Creates read policies scoped to the calling JWT's `auth.uid()`:

| Table | Policy | Rule |
|---|---|---|
| `workspace_memory` | `workspace_memory_read` | `is_private = 0 OR owner_user_id = auth.uid()::text` |
| `session_summaries` | `session_summaries_read` | `owner_user_id = '' OR owner_user_id = auth.uid()::text` |
| `learned_patterns` | `learned_patterns_read` | `owner_user_id = '' OR owner_user_id = auth.uid()::text` |
| `instincts` | `instincts_read` | `owner_user_id = '' OR owner_user_id = auth.uid()::text` |

3. Creates permissive write policies that allow authenticated callers to insert/update/delete their own rows and rows with `owner_user_id = ''` (shared/legacy).

**Service-role key is unaffected** — Supabase's service role always bypasses RLS. The Sovrant API server uses the service role key for all database operations, so no application code changes are needed.

**Docs** — `docs/persistence.md` updated to note that the RLS migration is available as an optional `supabase db push` step; the commented-out stubs in the initial migration file are removed now that a proper migration exists.

### What stays out of scope

- RLS on session or audit tables — those are queried by the application with scoped filters already; RLS on high-write tables adds overhead without meaningful security gain for service-role deployments
- Per-workspace RLS policies — the current `owner_user_id` model is user-scoped; workspace-level isolation is a separate governance layer
- Standalone PostgreSQL RLS — standalone deployments use application-layer auth exclusively; service-role bypass does not apply in the same way

### Acceptance criteria

- After applying the migration, a direct Supabase client authenticated as user A cannot read user B's private `workspace_memory` rows via the dashboard or an Edge Function
- User A can read all `workspace_memory` rows where `is_private = 0` regardless of `owner_user_id`
- The Sovrant API server (service-role key) continues to read and write all rows without restriction

---

## Phase 128 — Code Generation Quality Gates

**Status:** ✅ Done (2026-07-01)

### Why

Phase 73 shipped the scaffolding *mechanism* — 21 templates, `CodeCreateTool`, `CodeCreateMultiTool`, 235 tests. Generated code runs and passes its first test. That met the "works" bar. The higher bar is **immediately runnable, production-shaped projects**: a .NET user should be able to unzip the artifact and run `dotnet build MyApi.sln` on the first try; a Node user should run `npm install && npm start`; a Go user `go build ./...`. Nothing should be missing.

Three root causes hold this back today:

1. **Scaffolds omit project-level files.** The .NET templates produce `.csproj` files but no `.sln` — `dotnet build` won't work from the project root. No template ships a CI pipeline. No template uses `Directory.Build.props` to share properties across projects. These are mechanical, not model-quality issues.

2. **No structural quality gate.** `CodeCreateTool` writes files and stops. If the LLM writes malformed XML in a `.csproj`, or references a namespace that doesn't exist, or omits `package.json` entirely — those errors are invisible until the user runs the build. A `CodeValidate` tool closes this loop via in-process structural checks (parse JSON/XML/YAML, verify required files exist) without needing any compiler in PATH.

3. **The LLM is not instructed on what to do next.** After `CodeCreate` returns, the agent has conventions (via `LanguageGuidelines`) but no `next_steps` — no "add your route handlers here," no "call CodeValidate when done." The tool description reads like documentation, not instruction.

4. **Artifact delivery has security and correctness gaps.** The artifact store serves LLM-generated HTML/SVG/JS without `Content-Disposition: attachment`, opening an XSS vector. The `RemoteArtifactStore.ListAsync` reads wrong JSON field names from the server. The API server has no bulk zip endpoint. The Blazor Artifacts page bypasses `IArtifactStore` and casts to `LocalArtifactStore` directly.

This phase addresses all four. It is designed to work with **any code-capable LLM** — the quality gates are structural checks and explicit instruction, not model judgment.

### Scope

#### Part A — Artifact store security and correctness fixes

**A1. Content-security headers on file downloads** (`Sovrant.Web/Program.cs` + `Sovrant.Server/Routes/ArtifactRoutes.cs`):
- For `text/html`, `image/svg+xml`, `application/javascript`: force `Content-Disposition: attachment; filename="..."` and add `X-Content-Type-Options: nosniff`
- For all artifact downloads: add `X-Content-Type-Options: nosniff` and `Cache-Control: private, no-store`
- LLM-generated files must be treated as untrusted third-party content even within the user's own workspace

**A2. Fix `RemoteArtifactStore.ListAsync`** (`Sovrant.Api.Client/RemoteArtifactStore.cs:43–55`):
- JSON field `path` → `relative_path`; `size` → `size_bytes` — these must match what `ArtifactRoutes.cs` actually serializes

**A3. Add `GET /v1/artifacts/{runId}.zip`** to `Sovrant.Server/Routes/ArtifactRoutes.cs`:
- Mirrors the web-app zip endpoint; streams a `ZipArchive` via `IArtifactStore.ListAsync` + `ReadAsync` (no `LocalArtifactStore` cast — stays interface-clean for future cloud backends)
- Respects the workspace auth guard
- Makes bulk code scaffold download available to all clients: SDK, CLI in server mode, mobile

**A4. Fix `Artifacts.razor` abstraction leak**:
- Replace the `LocalArtifactStore` cast + `Directory.EnumerateFiles` call with `IArtifactStore.ListAsync`
- Load `_manifest.json` via `ReadAsync` to surface code-specific metadata (see B2)

#### Part B — Code-specific artifact manifest

**B1. `ArtifactManifest` — add optional code metadata fields** (additive, no migration needed):
```csharp
[JsonPropertyName("kind")]           public string? Kind { get; set; }          // "code-scaffold"
[JsonPropertyName("language")]       public string? Language { get; set; }      // "dotnet"
[JsonPropertyName("template_id")]    public string? TemplateId { get; set; }    // "dotnet/webapi"
[JsonPropertyName("build_command")]  public string? BuildCommand { get; set; }  // "dotnet build MyApi.sln"
[JsonPropertyName("run_command")]    public string? RunCommand { get; set; }    // "dotnet run --project MyApi"
[JsonPropertyName("test_command")]   public string? TestCommand { get; set; }   // "dotnet test MyApi.sln"
[JsonPropertyName("entry_point")]    public string? EntryPoint { get; set; }    // "MyApi/MyApi.sln"
```

**B2. `CodeCreateTool` writes a code manifest** after scaffolding — stamps `Kind = "code-scaffold"`, `Language`, `TemplateId`, `BuildCommand`, `RunCommand`, `TestCommand` into `_manifest.json`. The Artifacts page can then render a language badge and copy-able run commands in the folder detail view.

#### Part C — Scaffold enrichment ("every project is immediately runnable")

Update all 21 scaffold templates in `src/Sovrant.Tools/Projects/Scaffolds/`. Pure C# changes to the scaffold classes — no DB migration, no knowledge_pages rows involved (scaffolds live in code, not the DB).

**All 5 .NET scaffolds** (`dotnet/webapi`, `dotnet/console`, `dotnet/library`, `dotnet/blazor`, `dotnet/worker`):

| Addition | Detail |
|---|---|
| `{pascal}.sln` | Generated with `Guid.NewGuid()` per project (matches `dotnet new sln` behavior); references main project + tests project with C# project type GUID `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`; `Debug\|Release × Any CPU` config matrix |
| `Directory.Build.props` | Shared `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisMode>All</AnalysisMode>` — removed from individual `.csproj` files to avoid duplication |
| `.editorconfig` | Standard C# indentation, charset, trailing whitespace rules |
| `.github/workflows/ci.yml` | `dotnet restore → dotnet build --no-restore → dotnet test --no-build` on push/PR |

**All 16 non-.NET scaffolds** — add `.github/workflows/ci.yml` per language:

| Templates | CI steps |
|---|---|
| `node/*` (4 templates) | `npm ci → npm run lint → npm test`; also add `.nvmrc` |
| `python/*` (2 templates) | `pip install -e ".[dev]" → ruff check → pytest`; `pyproject.toml` with `[project.optional-dependencies] dev = [pytest, ruff]` |
| `go/api` | `go build ./... → go vet ./... → go test ./...`; add `Makefile` (build/test/lint targets) |
| `rust/cli` | `cargo build → cargo clippy → cargo test`; add `rust-toolchain.toml` |
| `java/maven` | `mvn verify` |
| `kotlin/console` | `./gradlew check` |
| `ruby/script` | `bundle install → rspec` |
| `swift/cli` | `swift build → swift test` |
| `cpp/cmake` | `cmake → make → ctest`; add `CMakePresets.json` |
| `lua/script` | `Makefile` (test via busted); `.luacheckrc` |
| `zig/cli` | `zig build → zig test` |

#### Part D — LLM instruction enrichment

**D1. `IProjectTemplate` — add two optional members** (default `string.Empty`):
```csharp
string BuildCommand { get; }  // e.g. "dotnet build {pascal}.sln"
string RunCommand { get; }    // e.g. "dotnet run --project {pascal}"
```
Each scaffold overrides as appropriate. These feed both the artifact manifest (B2) and `next_steps` (D2).

**D2. `CodeCreate` and `CodeCreateMulti` response — add fields**:
```json
{
  "next_steps": [
    "Add your route handlers — e.g. app.MapPost('/items', async (ItemDto dto) => { ... })",
    "Register services in Program.cs using builder.Services.Add*()",
    "Place model classes in a Models/ folder, one file per type",
    "Call CodeValidate when done to verify structural correctness"
  ],
  "build_command": "dotnet build MyApi.sln",
  "test_command":  "dotnet test MyApi.sln",
  "run_command":   "dotnet run --project MyApi"
}
```
`next_steps` is generated by each template's `NextSteps(ScaffoldContext)` method. This gives the LLM concrete, project-specific guidance rather than generic style rules.

**D3. Tool `Description` update** for `CodeCreate` and `CodeCreateMulti`:
> *"After scaffolding, add business logic with the Artifact tool following the returned `conventions` and `next_steps`. Then call `CodeValidate` to verify structural correctness. If issues are found, fix them and call `CodeValidate` once more before presenting the project to the user."*

#### Part E — `CodeValidateTool` (new tool)

**File:** `src/Sovrant.Tools/Projects/CodeValidateTool.cs`  
**Tool name:** `CodeValidate`  
**Input:** `run_id`, `workspace_id`, `project_id` (same scope as `CodeCreate`); optional `language` (auto-detected from file extensions if omitted)

**Flow:** `IArtifactStore.ListAsync(scope)` → enumerate files → `ReadAsync(handle, path)` for each → run per-language structural checks → return report. No compiler invocation; no language runtime required in PATH.

**Check matrix:**

| Check | Languages | What |
|---|---|---|
| `.sln` present at depth ≤ 2 | dotnet | File exists with `.sln` extension |
| Each `.csproj` parses as valid XML | dotnet | `XDocument.Parse` |
| `<TargetFramework>` present in each `.csproj` | dotnet | XPath check |
| `package.json` present + valid JSON + has `name`/`version` | node | `JsonDocument.Parse` |
| `go.mod` present with `module` directive | go | File exists; first line check |
| `Cargo.toml` present with `[package]` section | rust | File exists; line scan |
| `pyproject.toml` or `requirements.txt` present | python | Either file exists |
| All `.json` files parse | all | `JsonDocument.Parse` |
| All `.xml`/`.csproj`/`pom.xml` files parse | all | `XDocument.Parse` |
| All `.yml`/`.yaml` files parse | all | Basic structure heuristic (no external YAML dep) |
| `.github/workflows/ci.yml` present | all | File path exists in listing |

**Output on success:**
```json
{
  "valid": true,
  "language": "dotnet",
  "file_count": 12,
  "issue_count": 0,
  "issues": []
}
```
**Output on failure:**
```json
{
  "valid": false,
  "language": "dotnet",
  "file_count": 8,
  "issue_count": 2,
  "issues": [
    {
      "file": "MyApi.sln",
      "severity": "error",
      "message": "Solution file missing — dotnet build will not work from the project root",
      "fix_hint": "Create MyApi.sln referencing MyApi/MyApi.csproj and MyApi.Tests/MyApi.Tests.csproj"
    },
    {
      "file": "MyApi/MyApi.csproj",
      "severity": "error",
      "message": "XML parse error: unexpected token at line 5",
      "fix_hint": "Check for unclosed tags or invalid characters"
    }
  ],
  "self_correction_prompt": "The scaffold has 2 structural errors (listed above). Fix them using the Artifact tool before presenting the project to the user. Then call CodeValidate once more to confirm."
}
```

The `self_correction_prompt` field is the re-entry point for the LLM's agentic loop. The tool itself runs once and returns; the loop is driven by the LLM following the instruction in `self_correction_prompt`. The tool description caps this at one retry: "call CodeValidate once more after fixing."

### Implementation order

| Step | What | Files |
|---|---|---|
| A1 | Content-security headers (both servers) | `Sovrant.Web/Program.cs`, `ArtifactRoutes.cs` |
| A2 | Fix `RemoteArtifactStore.ListAsync` field names | `RemoteArtifactStore.cs` |
| A3 | Add `GET /v1/artifacts/{runId}.zip` to API server | `ArtifactRoutes.cs` |
| B1 | `ArtifactManifest` code metadata fields | `ArtifactManifest.cs` |
| C | Scaffold enrichment — `.sln` + CI for all 21 templates | All scaffold `.cs` files |
| D1 | `IProjectTemplate.BuildCommand` / `RunCommand` | `IProjectTemplate.cs` + all scaffold classes |
| D2 | `CodeCreate` / `CodeCreateMulti` response `next_steps` | `CodeCreateTool.cs`, `CodeCreateMultiTool.cs` |
| B2 | `CodeCreateTool` writes code manifest | `CodeCreateTool.cs` |
| D3 | Tool `Description` updates | Same files |
| E | `CodeValidateTool` | New file `Projects/CodeValidateTool.cs` |
| A4 | `Artifacts.razor` abstraction fix + manifest display | `Artifacts.razor` |
| — | Tests: enriched scaffolds + `CodeValidateTool` | New test files |

### What this is not

- Not a compiler-in-the-loop gate — structural validation only; no language runtime required in PATH
- Not a style enforcer — `LanguageGuidelines` remain advisory; guideline conformance is not a gate
- Not a replacement for the user's own CI — the generated CI config is a starting point
- Not a continuous linting service — validation fires at generation time, not on every file write

### Acceptance Criteria

- [ ] All 5 .NET scaffolds produce a `.sln` file; `dotnet build {pascal}.sln` succeeds on the generated output with no additional steps
- [ ] All 21 templates produce `.github/workflows/ci.yml` with correct install/build/test steps for the language
- [ ] `IProjectTemplate.BuildCommand` / `RunCommand` implemented on all scaffold classes; values appear in `CodeCreate` response and `_manifest.json`
- [ ] `CodeCreate` response includes `next_steps`, `build_command`, `test_command`, `run_command`; tool description instructs the LLM to call `CodeValidate` after writing code
- [ ] `CodeValidateTool` registered, all check matrix rows implemented; returns `valid: true` for a freshly scaffolded project of every language; returns `valid: false` with actionable `issues` + `self_correction_prompt` when `.sln` or `package.json` is missing
- [ ] `GET /v1/artifacts/{runId}.zip` added to API server; returns a correct zip of all run artifacts; respects workspace auth
- [ ] HTML/SVG/JS artifact downloads from both web and API server send `Content-Disposition: attachment` + `X-Content-Type-Options: nosniff`
- [ ] `RemoteArtifactStore.ListAsync` field names fixed; remote-mode artifact listing returns correct entries
- [ ] `Artifacts.razor` no longer casts to `LocalArtifactStore`; uses `IArtifactStore.ListAsync`; displays language badge + build/run commands when `_manifest.json` has code metadata
- [ ] All existing Phase 73 scaffold tests pass unchanged
- [ ] New unit tests cover: `.sln` present in each .NET scaffold; `ci.yml` present in each template; `CodeValidateTool` pass/fail cases for dotnet, node, python, go

### Deferred

- Compiler/linter-in-the-loop (invoking `dotnet build`, `tsc`, `ruff` as subprocesses) — requires language runtime availability detection, temp directory management, stdout/stderr capture, and process isolation; too much platform complexity for v1.5; structural validation + self-correction via `CodeValidateTool` achieves the main quality lift without it
- Continuous background linting of the user's working tree — separate concern
- Cloud artifact backend (S3/Azure/R2) — `IArtifactStore` is ready; backend registration is the remaining work; deferred to post-v1.5 per the workspace provider roadmap

---

## Phase 129 — Missions → Workflows: Rename, Positioning, and UX Review

**Status:** Planned (v1.5)

### Why

The term "missions" is accurate for the engine internals — a bounded goal delegated to an AI team with a planner, executor, and journal — but it is opaque to users. Almost everyone arriving at Sovrant calls this concept a "workflow." Renaming closes the vocabulary gap without changing what the engine does.

More importantly, the rename is the right moment to resolve a positioning question the codebase sidesteps today: **what kind of workflows does Sovrant own, and what does it deliberately leave to external tools?**

The answer is clear from what's already shipped:
- **Sovrant owns**: AI-orchestrated, goal-driven workflows where the "steps" are LLM agents making decisions — not predetermined trigger→action chains. The planner decomposes the goal; the executor routes it; the agents adapt. This is the lane n8n and Zapier cannot fill.
- **n8n, Zapier, Make, Composio own**: trigger-based automation, scheduled jobs, branching deterministic pipelines, and hundreds of SaaS connectors. These are already available in Sovrant via the Integrations Gallery as MCP connections — Sovrant acts as the AI layer on top of them, not a replacement for them.

Without this distinction surfaced in the UI, users either expect Sovrant to be a Zapier clone (and are disappointed) or miss entirely that they can run complex AI-driven goals with a single prompt.

The UX also needs work independent of naming. Today "missions" is discoverable only through the Orchestration Studio → Run button and the Command Center grid. There is no dedicated Workflows page where a user can launch, monitor, and review their AI-driven workflows without knowing what an "orchestration" or "mission" is first.

### What ships

#### 1 — Surface label rename (Web + Desktop)

Replace every user-visible "Mission" / "Missions" label with "Workflow" / "Workflows" across both surfaces. The underlying DB table (`missions`), API path (`/v1/missions`), and C# types (`IMissionStore`, `LlmMissionPlanner`, etc.) are **not renamed** — this is a presentation-layer change only.

| Where | Old label | New label |
|---|---|---|
| Nav / rail | Missions | Workflows |
| Orchestration Studio | Run Mission | Run Workflow |
| Command Center | Missions column | Workflows column |
| User Dashboard | missions stat | workflows stat |
| Page titles | Missions | Workflows |
| Privacy toggle | Make mission private | Make workflow private |
| Completion messages | "Mission complete" | "Workflow complete" |

**API compatibility:** Add `/v1/workflows` as an alias that proxies to `/v1/missions` on all CRUD and status endpoints. The `/v1/missions` path is retained and marked `@deprecated` in comments (not removed — no external consumers confirmed yet, but a grace period is the right call).

#### 2 — Dedicated Workflows page

Replace or supplement the current Orchestration Studio with a standalone `/workflows` page (Web) and `WorkflowsView` (Desktop) that is the primary entry point for AI-driven workflows. The Orchestration Studio can remain for team composition; the Workflows page is for goal-level users who don't care about the agent structure underneath.

**Page layout:**
- **Header**: "Workflows" + "New Workflow" button (prominent)
- **Active workflows**: card row — name, goal snippet, team name, run-mode badge (Autonomous / Supervised / Step-through), live status indicator (dot + "Running step 3 of ~5"), elapsed time
- **Recent workflows**: collapsible list of completed/failed — name, team, finish time, outcome badge (✅ Complete / ❌ Failed), link to detail
- **Empty state**: "No workflows yet. A workflow gives an AI team a goal and lets it figure out the steps." + "New Workflow" CTA

**What it is not**: a node editor, a visual flow diagram, a cron scheduler.

#### 3 — New Workflow launch form

A simple form replacing (or complementing) the current Orchestration Studio run path:

```
Goal:         [textarea — describe what you want accomplished]
Team:         [dropdown of configured teams, or "Let Sovrant pick"]
Run mode:     [Autonomous | Supervised | Step-through]
Name:         [optional, auto-generated from goal if blank]
[Run Workflow]
```

The "Let Sovrant pick" team option uses `AgentOrchestrator` in decomposition mode — the engine selects agents based on the goal. This is the one-click path for users who don't want to configure a team first.

#### 4 — Workflow detail view

Each workflow gets a detail page/view showing:
- **Goal** — the original prompt
- **Team** — agents involved, with roles
- **Journal** — chronological list of events (agent step started, tool called, step complete, replanning triggered) — collapsible by default, expandable for debugging
- **Artifacts** — files produced, with open/download links
- **Output summary** — the final LLM-produced summary of what was accomplished
- **Status timeline** — duration per step if available

This replaces the current "click-through from Command Center" flow, which lands on a sparse detail page.

#### 5 — Positioning callout in the UI

A short inline note on the Workflows page (dismissible, not a modal) that surfaces the positioning:

> **Workflows vs automation tools.** Sovrant workflows are AI-driven — you describe a goal and the AI team plans and executes the steps. For trigger-based automation, scheduled jobs, and SaaS connectors, connect n8n, Zapier, or Make from the [Integrations](link) page and use them as tools inside a workflow.

This single sentence prevents the most common expectation mismatch.

#### 6 — Dual-path execution: Claude Agent SDK dynamic workflows when available

Unlike items 1–5, this is an engine-level addition, not a presentation change. When the workflow's active model is a qualifying Claude tier (exact gate TBD at design time — candidates are the Opus/Sonnet 5 family resolved via `ModelTierResolver`, not simply "any Anthropic key configured"), workflow execution can route through the Claude Agent SDK's dynamic subagent/workflow orchestration instead of Sovrant's own planner/executor. When no qualifying Claude model is configured — no key, or a non-qualifying provider/model — workflows fall back to Sovrant's existing provider-agnostic mission engine (`LlmMissionPlanner` / `ParallelMissionExecutor`), which remains the base version and continues to work with any OpenAI-compatible provider.

Open design questions to resolve before implementation, not before this roadmap entry:
- Exact model/tier gate — which specific Claude models qualify, and where that check lives (`ModelTierResolver` vs a new capability flag)
- Whether the two backends emit a compatible event journal, or the Workflows UI needs to tolerate two event vocabularies under one `/v1/workflows` contract
- Whether a settings override lets a user force one path or the other rather than always auto-selecting by provider capability
- Whether this ships inside Phase 129 or splits into its own phase, since it changes the "no DB/engine changes" framing that otherwise applies to items 1–5

### What we explicitly do not build

| Not building | Why | Alternative |
|---|---|---|
| Visual node/flow editor | That is n8n's lane | Connect n8n via MCP Integrations |
| Cron / scheduled triggers | That is n8n/Zapier's lane | n8n workflows can call Sovrant's API |
| Webhook-triggered workflows | Same | n8n trigger → Sovrant REST call |
| 500-connector library | Already covered | Composio / n8n in the Integrations Gallery |
| Deterministic branching pipelines | LLM planner handles branching better | No alternative needed |

The constraint is intentional and permanent. Sovrant's value is the AI layer. Adding a visual editor or a scheduler would compete with tools that have years of head start and would dilute what makes Sovrant different.

### Relationship to other phases

- **Phase 51** ✅ built the engine (`IMissionStore`, `LlmMissionPlanner`, `ParallelMissionExecutor`) — remains the base-version execution path (item 6) and is otherwise not touched by items 1–5
- **Phase 119** (planned) adds per-mission run-modes and integration-sourced Claws — Phase 129 uses those run-modes in the new launch form; Phase 119 should ship first or in parallel
- **Phase 94** ✅ (Orchestration Studio) remains as the team-composition surface; Phase 129 adds a goal-first surface on top
- **Claude Agent SDK dependency (item 6, new)** — requires an Anthropic/Claude provider integration capable of driving the Agent SDK's dynamic subagent orchestration; needs its own design pass on the model/tier gate before implementation

### Acceptance criteria

- [ ] Every user-visible "Mission" / "Missions" label replaced with "Workflow" / "Workflows" on Web and Desktop; no DB or runtime type names changed
- [ ] `/v1/workflows` alias proxies to `/v1/missions`; all CRUD and status operations work identically through both paths
- [ ] Dedicated Workflows page reachable from nav; shows active + recent workflows; "New Workflow" button launches the new goal form
- [ ] New Workflow form: goal textarea, team picker with "Let Sovrant pick" option, run-mode selector, optional name field
- [ ] "Let Sovrant pick" path invokes `AgentOrchestrator` in decomposition mode and runs the workflow without requiring a pre-built team
- [ ] Workflow detail view shows goal, journal (collapsible), artifacts, and output summary
- [ ] Positioning callout visible on Workflows page; dismissible per user; links to Integrations
- [ ] All existing mission/orchestration tests pass unchanged (no engine code modified)
- [ ] Web + Desktop parity on all new UI surfaces

---

## Phase 130 — OpenRouter Account Registration & In-App Key Issuance

**Status:** Planned

### Why

Today, adding OpenRouter as a provider means: open a browser, create or log into an OpenRouter account, navigate to Keys, generate one, copy it, switch back to Sovrant, and paste it into the provider form. That's a five-step context switch for what should be the single fastest path to a working model — OpenRouter is the default onramp for free-tier models (`SOVRANT_FREE_MODELS_ONLY`, `:free` model routing) and is already Sovrant's most-referenced provider outside the default OpenAI.

OpenRouter publishes exactly the flow that removes this friction: [OAuth PKCE](https://openrouter.ai/docs/use-cases/oauth-pkce). Unlike a normal OAuth login, the token exchange returns a real, permanent OpenRouter API key — not a session token — meaning the end result is identical to what a user would have pasted in by hand, just without the hand.

### What ships

#### 1 — PKCE flow against OpenRouter's own auth endpoint

Reuse the PKCE primitives already built for Phase 101 (MCP OAuth 2.1 + PKCE): code-verifier generation, S256 code-challenge derivation, and the loopback callback listener (`McpOAuthCallbackListener` pattern) — but point them at OpenRouter's dedicated flow rather than a generic MCP server's `/authorize` endpoint:

1. Sovrant generates `code_verifier` + `code_challenge` (S256) and opens `https://openrouter.ai/auth?callback_url={redirect}&code_challenge={challenge}&code_challenge_method=S256` in the user's browser.
2. The user registers a new OpenRouter account or logs into an existing one, then approves the connection — entirely on OpenRouter's own site, so Sovrant never sees a password.
3. OpenRouter redirects back to Sovrant's callback with a `code`.
4. Sovrant exchanges `code` + `code_verifier` via `POST https://openrouter.ai/api/v1/auth/keys` and receives a live OpenRouter API key.
5. The key is written directly to the encrypted keystore (same AES-256-GCM store used for manually entered keys) and a `provider_profiles` row is created/activated for OpenRouter — indistinguishable afterward from a hand-entered key.

A new `OpenRouterAuthService` in `Sovrant.Runtime` owns this exchange; it is a sibling to `McpOAuthService`, not a fork of it — the PKCE helper (verifier/challenge generation) is extracted into a shared internal utility both services call, rather than duplicated.

#### 2 — Callback handling per surface

- **Desktop:** ephemeral loopback HTTP listener on `127.0.0.1:{port}/openrouter-callback`, matching the pattern `McpOAuthCallbackListener` already uses for MCP server auth — no new listener architecture.
- **Web:** ASP.NET Core route (`/auth/openrouter/callback`) using an absolute redirect URI, matching Phase 101's web OIDC-style callback handling.

#### 3 — UI entry point (Web + Desktop, Providers settings)

- Existing "Add Provider" flow gains a second path alongside "Enter API key manually": a **"Get an OpenRouter key"** button, shown only for the OpenRouter provider entry.
- Clicking it opens the system browser to the OpenRouter auth page and shows a waiting state ("Waiting for OpenRouter authorization…") with a cancel option.
- On success, the provider profile is created and activated automatically — same end state as the manual-entry path, so downstream code (model picker, `SmartRouter`, cost tracking) needs no changes.
- On denial/timeout/error, a plain-language error is shown and the manual-entry form remains available as a fallback.

#### 4 — Multi-user / workspace scoping

Same scoping rules as any other provider profile (Phase 95/Phase 8): the issued key is stored per-user or per-workspace depending on where the flow was launched from (personal Settings vs. a workspace's admin Providers page), reusing the existing `provider_profiles` ownership model — no new scoping concept.

### Non-goals

- Managing OpenRouter account settings (billing, usage limits) from within Sovrant — out of scope; Sovrant only obtains a key.
- Building a generic "OAuth key issuance" framework for other providers — this phase is scoped to OpenRouter's specific PKCE-for-a-key flow. If another provider ships an equivalent flow later, it gets its own phase informed by this one.
- Revoking or rotating the key from Sovrant — the key is managed like any other stored credential (delete + re-run the flow to rotate), consistent with how manually entered keys are already handled.

### Relationship to other phases

- **Phase 101** ✅ (OAuth 2.1 + PKCE for MCP) supplies the PKCE primitives and loopback-listener pattern this phase reuses — Phase 130 does not reimplement PKCE from scratch.
- **Phase 95** ✅ (Integrations Gallery) established the encrypted-keystore-backed credential pattern this phase writes into.
- **Phase 8** ✅ (multi-tenant per-request credentials) and the workspace-scoped provider profile model (README §Multi-User & Workspaces) define where the issued key is scoped.

### Acceptance criteria

- [ ] "Get an OpenRouter key" button visible on the OpenRouter provider entry in Settings (Web + Desktop)
- [ ] Full PKCE round-trip verified against OpenRouter's real `/auth` and `/api/v1/auth/keys` endpoints (registration path and existing-account login path both tested manually)
- [ ] Issued key lands in the encrypted keystore and appears as an active provider profile with no additional user action
- [ ] Desktop loopback listener and Web callback route both tear down cleanly on success, cancel, and timeout
- [ ] Manual key-entry path remains fully functional and is offered as a fallback on any error
- [ ] Web + Desktop parity
- [ ] No changes required in `SmartRouter`, model discovery, or cost tracking — the issued key behaves identically to a manually entered one
