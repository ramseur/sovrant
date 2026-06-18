# Changelog

All notable changes to Sovrant are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions correspond to tags on the `development` branch.

---

## [Unreleased]

### Added

- **Phase 123 — Memory system overhaul:**
  - **V041 migration** adds `owner_user_id` and `is_private` columns to `workspace_memory` (additive; existing rows default to `owner_user_id = ''`, `is_private = 0` / public).
  - **Workspace Memory tab** on the Memory page (web `/memory` and desktop Knowledge → Memory) — fourth tab shows workspace memory entries with layer badge and privacy icon (🔒/🔓); inline add form with layer selector and private toggle; delete button per entry.
  - **"+ Remember" button** in chat (web and desktop) — opens an inline panel to save a free-text note to workspace memory without needing to know the `/remember` slash command; defaults to private.
  - **Per-user memory injection** — `ConversationRuntime` auto-resolves the session owner's personal workspace via `IWorkspaceService.GetPersonalAsync` when `SOVRANT_WORKSPACE_ID` env var is not set, so each web user's chat session injects their own workspace memories rather than a shared global.
  - **Privacy-aware `ListMemoryAsync`** — `IWorkspaceService.ListMemoryAsync` now accepts a `viewerUserId` parameter; returns public entries plus the viewer's own private entries; admin path (null `viewerUserId`) returns all.
  - `MemoryInjector.BuildMemorySectionAsync` receives `ownerUserId` and passes it through to `ListMemoryAsync` for per-user filtering at the DB layer.

- **Phase 96 — MCP runtime variables:** per-server env var editor on Web and Desktop.
  - Inline key/value editor in the server detail pane (Integrations → Connected tab). Edit mode shows existing vars as editable rows; Save fetches the full server config and updates only the `Env` dict; Cancel discards.
  - `+ Add Variable` button adds blank rows; `✕` removes a row.
  - STATUS message shows how many variables were saved.
  - `KEY=VALUE` textarea in the stdio add form — env vars set at creation time.
  - JSON paste (`mcpServers` block) already populated `Env` from the `env` field; feedback message now reports env var count: `Imported: 'sitecore' (12 env vars).`
  - `EnvVarRowViewModel` observable class for Desktop MVVM two-way binding.
  - `ParseEnvVarLines()` helper shared across stdio-add and is symmetrical to the Web implementation.

- **Phase 96 — Keystore in DB (V039):** master AES-256-GCM key moved from `.keystore` disk file into a `keystore` SQLite table.
  - V039 migration adds `keystore (scope TEXT PK, key_hex TEXT, created_at TEXT)`.
  - `SqliteCredentialStore.LoadOrCreateKeyAsync` reads key from DB first; one-time migration reads legacy `.keystore` file, writes to DB, then best-effort deletes the file.
  - `BootstrapConfig.KeystorePath` renamed to `LegacyKeystorePath`; `SOVRANT_KEYSTORE_PATH` env var still honoured for the migration path.
  - All credentials (MCP server configs, env vars, API keys) are encrypted at rest in a single DB file with no external key file.

---

## [1.0.2] — 2026-05-26

### Changed

- **Phase 107 — Integration connection audit:** all 19 gallery entries audited and corrected.
  - Composio: API key header corrected (`Authorization` → `x-api-key`).
  - Zapier: stale endpoint URL removed; replaced with user-supplied endpoint from Zapier dashboard; OAuth flag added.
  - GitHub: env var corrected (`GITHUB_TOKEN` → `GITHUB_PERSONAL_ACCESS_TOKEN`); deprecation note added.
  - Linear: switched from non-existent `@linear/mcp-server` npm package to Linear's official remote HTTP endpoint (`https://mcp.linear.app/mcp`) with OAuth flag.
  - Snowflake: package name corrected (`snowflake-mcp-server` → `snowflake-mcp`); description updated to list all 6 required env vars.
  - Optimizely CMS: removed — no installable npm package exists.
  - OAuth badge added to connect forms (Web + Desktop) for Zapier, Linear, Supabase, Sitecore Marketer, and Adobe AEM.

### Added

- `docs/integration-connection-matrix.md` — connection status, credential fields, and open issues for all gallery integrations; serves as the acceptance gate going forward.

---

## [1.0.1] — 2026-05-26

### Added

- **Phase 106 — Agent identity in chat:** agent name is now persisted to the
  `sessions` table (V031 migration: `agent_name` column) and restored on
  session resume.  Both Web and Desktop surfaces show the active agent:
  - **Chat hero state** — "Chatting with [AgentName]" badge when the session
    is scoped to a named agent (Web + Desktop).
  - **Top context bar** — permanent agent pill visible on all pages while
    a scoped session is active (Web + Desktop).
  - Agent context is cleared when the user starts a fresh session and
    restored automatically when resuming a previous agent-scoped session.

---

## [1.0.0] — 2026-05-26

First stable release. Combines the 0.10.0 milestone bump with the Phase 98/99
feature work completed on the same day.

### Added

- **User Dashboard** (`/dashboard`) — personal cross-workspace activity view
  showing own public ("Shared"), own private, and teammates' public records.
  Other users' private records are excluded entirely. Backed by
  `UserDashboardAggregator` and `GET /v1/user-dashboard/state`. Reached via
  👤 rail nav icon on Web and Desktop.
- **Per-record privacy toggles** — any session, agent run, or mission can be
  marked private. Private records are visible only to the owner. On the
  Command Center they appear as masked rows (title/content hidden); on the
  User Dashboard they are excluded from all other users' views. Server-side
  enforcement via `is_private` column (V030 migration).
- Command Center and User Dashboard: paginated grid, header timestamp, 30-second
  auto-refresh, page-preserve on refresh/navigation, guide panels.
- User Dashboard guide panel; Command Center guide text wrapping fix.

### Changed

- Default provider in setup wizard and admin UI changed from OpenRouter to OpenAI.
- Command Center poll interval changed from 2 seconds to 30 seconds.
- Dashboard "Shared" stat redefined as own public items (not others' activity).
- User Dashboard moved to first nav position on both Web and Desktop.
- Masked Command Center rows are non-clickable.
- Desktop User Dashboard nav button uses 📊 bar-chart icon.
- Sidebar stop button only shown for actively running sessions.

### Fixed

- Privacy toggle state no longer lost when set before sending the first message.
- Dashboard Shared stat count now matches the grid row count.
- Desktop pager position and last-updated label corrected.
- User Dashboard stat row fits 6 tiles on narrow viewports.

---

## [0.9.9] — 2026-05-25

### Added

- **Integrations Gallery expansions:**
  - Sitecore (GraphQL Content Delivery, Community MCP, Marketer MCP) — consolidated
    into a single grouped card with Community/Commercial tabs.
  - Adobe AEM, Optimizely CMS, Snowflake added to catalog.
  - Snowflake repositioned alongside PostgreSQL and Supabase in the Platform tier.
- Multi-file artifact runs grouped into folder items on Web and Desktop Artifacts view.
- `/chat` route alias so Documents "Chat to create" deep-link works without
  polluting the browser URL; prompt seeded via `ChatSeedService`.

### Changed

- Web System Integrations styling aligned with Desktop (dot + pill status indicators).
- Integrations outcome-badges replaced with colored status dots.
- Code Scaffolding page removed from Web and Desktop nav (functionality available
  via chat and the scaffolding tools directly).
- Documents UX: Generate prompt moved to top of detail pane; JSON textarea
  replaced with chat-to-create primary flow.
- MCP server opt-in toggle removed from Desktop Projects panel.
- Projects rail icon changed from 🏗 to 🗂️ on Web and Desktop.

### Fixed

- Zip artifact download in Chrome (buffered into MemoryStream before sending).
- Sitecore Community MCP auth — `AUTORIZATION_HEADER` env var optional.
- Integrations page icon conflicts (Supabase, Zapier, Groq).

---

## [0.9.8] — 2026-05-23

### Added

- **Phase 40C — Supabase / PostgreSQL backend (optional):**
  - Admin → System Integrations UI (Web + Desktop) with Test Connection,
    Initialize Schema, Migrate Data from SQLite, Switch/Revert actions.
  - `PostgresSessionStore` and `PostgresCredentialStore` in `Sovrant.Storage.Postgres`.
  - `PostgresSchemaInitializer` — embedded DDL matching SQLite migrations V001–V029.
  - `SqliteToPostgresMigrator` — idempotent copy of sessions, entries, and credentials.
  - Boot-time DI switch: two-phase bootstrap reads SQLite credentials first, then
    optionally overrides `ISessionStore` + `ICredentialStore` with Postgres.
- **Phase 73 — Code scaffolding (complete):**
  - 21 project templates: Node/TS, .NET (standard + Blazor + worker), Python, Go,
    Rust, Java, Kotlin, Ruby, Swift, Lua, Zig, C++/CMake, Node monorepo.
  - `CodeCreateTool`, `CodeCreateMultiTool` (multi-component generation),
    `CodeListTemplatesTool`, `ScaffoldManifestValidator`.
  - Artifact zip download via CLI, Web, and Desktop.
  - 235 golden-path + manifest validation tests.
- **Phase 50 — OpenClaw federation:**
  - `SwarmFederationMode` enum (Silo / Federated / ManagerLed).
  - `OpenClawBusClient`, `RouteResolver`, `ListChildrenAsync`.
  - V029 migration adds `parent_swarm_id` to `swarm_events`.
  - New REST endpoints: `POST /v1/swarm/manager`, `GET /v1/swarm/openclaw/routes`,
    `GET /v1/swarm/{id}/children`.
  - `swarm-manager` agent template.
- **Session-level MCP opt-in** lifted to persistent context bar (Desktop
  `WorkspacePanelView`, Web `TopContextBar`) — replaces per-chat MCP selector.
- Command Center: Owner column resolves `userId` → username/email.
- Agent run prompt stored on `agent_runs` (V028) and rendered as run title
  with agent name badge in Recent Runs on Web and Desktop.

### Changed

- MCP switcher redesigned to match workspace/project switcher style; always
  visible in context bar with Integrations deep-link when no servers are connected.

### Fixed

- Desktop: clickable links and missing messages on session resume.
- Integrations: browser autofill prevention on all MCP server credential inputs.
- Integrations: duplicate Filesystem catalog entries removed.
- Command Center: grid widened; session owners shown correctly.
- Web: autofill prevention, input sizing, MCP flyout light-dismiss.

---

## [0.9.7] — 2026-05-20

### Added

- **Phase 87 — Artifacts-by-default (complete):** workspace-first artifact layout
  with workspace/project routing; auto-save large chat code blocks as artifacts;
  artifact tool writes rendered as download cards in Web and Desktop.
- **Phase 86 — Background session continuation:** sessions remain live across
  page navigation and session switches; always-on (settings UI removed).

---

## [0.9.6] — 2026-05-19

### Added

- **Phase 92 — Active background sessions:** up to 5 concurrent live tasks with
  return-anytime results; DB-backed cap configurable via Settings UI on Web and Desktop.
- Workspace role (Admin / Member) shown in user chip instead of hardcoded "Personal".

---

## [0.9.5] — 2026-05-18

### Added

- **Phase 95 — Integrations Gallery:** catalog-first MCP onramp with 14 integrations
  across Automation (Composio, n8n, Zapier, Make), Platform (GitHub, Slack, Notion,
  Linear, Stripe, PostgreSQL, Supabase, Filesystem), and Search (Brave, Exa, Tavily)
  tiers. Encrypted credential keystore for all MCP server configs. Web + Desktop parity.
- **Phase 94 — Orchestration Studio:** compose and run teams from the UI; team +
  member create forms; Run button with task prompt on Web and Desktop.
- **Phase 79 — Agents page:** in-app create/edit/clone/delete of agent definitions
  (silent copy-on-write for built-ins); Launch Chat and Run one-shot actions;
  agent-scoped chat experience.
- Model switcher: shows configured vs available-to-configure providers; deep-link
  to Settings → Providers tab with pre-selected provider for unconfigured entries.
- MCP server configs encrypted at rest via `ICredentialStore` (no plaintext in DB).
- Admin: hard-delete user, disable/delete confirmation dialogs (Web + Desktop).
- Interactive chat UX improvements; artifact simplification.
- `SECURITY.md` and `CONTRIBUTING.md` added for public release.

### Fixed

- Workspace root directory created at store initialization.
- Artifact/document system prompt strengthened to force immediate tool use.
- Agents page: Run one-shot card moved above markdown detail on Desktop.
- Orchestration: form input widths and gap on Web.

---

## [0.9.4] — 2026-05-16

### Added

- `SECURITY.md` security policy and disclosure process.
- `CONTRIBUTING.md` contribution guide.

### Fixed

- README: corrected endpoint count to 141; removed stale server env var instructions.
- Various README and docs cleanup.

---

## [0.9.3] — 2026-05-16

Internal release candidate. Not formally tagged but represents the state shipped
to UAT before the public release prep.

### Added

- **Phase 85 — Identity & login parity:** per-user `svt_*` bearer tokens, Argon2id
  password hashing, admin pages (Web + Desktop), CLI `login` / `logout` / `whoami`,
  first-user admin bootstrap, open-registration and admin-approval toggles.
- **Phase 93 — Configuration boundary audit:** `sovrant.config` removed entirely;
  all bootstrap knobs are env vars; `routing.json` → env vars + `workspace_settings`;
  `swarm.json` → `workspace_settings`; `config-audit.md` policy doc.
- Phase 97 — TLS/SSL: Kestrel HTTPS with PEM/PFX cert support, HTTPS redirect,
  configurable port via `SOVRANT_TLS_*` env vars.
- Phase 40C step A — System Integrations admin section scaffolded.

### Changed

- License Change Date moved to 2029-05-15.
- Legacy `SOVRANT_TOKEN` env var and dead static-token paths removed.
- `tools/ReadDb` admin-reset binary removed.

### Fixed

- Cross-user provider profile leakage: workspace provider profiles now correctly
  scoped so non-members cannot see another workspace's keys.
- Settings API key field starts blank on every load (no stale value shown).
- Admin registration toggles fixed on Web.

---

## [0.9.2 and earlier] — 2026-04-03 to 2026-05-15

Pre-release development. Major phases completed during this period:

| Phase | Feature |
|---|---|
| Phase 98 / V030 | User Dashboard + `is_private` (shipped in 1.0.0) |
| Phase 92 | Active background sessions (up to 5 concurrent) |
| Phase 90 | Public release readiness, Command Center cockpit polish |
| Phase 89 | Command Center — live aggregated cockpit surface |
| Phase 88 | Settings & provider profile consolidation (one disk config) |
| Phase 87 | Artifacts-by-default + workspace identity unification |
| Phase 86 | Background session continuation |
| Phase 85 | Identity & login parity — multi-user auth |
| Phase 84 | Prompt library: reusable parameterised templates |
| Phase 82 | Web search architecture overhaul |
| Phase 79 | Agents page: in-app create/edit of agent definitions |
| Phase 78 | Team run profiles (run mode, concurrency, quality gate) |
| Phase 73 | Code scaffolding — 21 project templates |
| Phase 67 | Autonomous driver layer (`LlmAutonomousDriver`, `SwarmAutonomousDriver`) |
| Phase 66 | Document generation — 6 generators, 44 templates, 7 verticals |
| Phase 63 | DI audit + pluggability hardening; MCP v1.2.0 protocol additions |
| Phase 61 | Remote server mode — SignalR hub, `AddSovrantClient()`, dual embedded/remote |
| Phase 59 | Agentic loop hardening — intent classification, plan approval, governance |
| Phase 58 | Trust Boundary — sanitization + ethics + intent as unified pipeline |
| Phase 57 | Inter-agent coordination — PM agents, `GroupMailbox`, `PMCoordinator` |
| Phase 56 | Web application — Blazor Server, 15 pages, port 5100 |
| Phase 55 | Cost tracking — OpenRouter pricing, budgets, JSONL metrics, `/cost` CLI |
| Phase 54 | Model capability registry — layered resolution, Gemma 4 support |
| Phase 53 | Scoped artifact storage — workspace-first layout, `/v1/artifacts` API |
| Phase 52 | Unified agent orchestration — `SqliteTeamRegistry`, `AgentOrchestrator`, run ledger |
| Phase 51 | Mission engine — durable goals, re-planning, acceptance gates, event journal |
| Phase 50 | OpenClaw federation bus (shipped in 0.9.8) |
| Phase 48 | SmartRouter — health/latency/cost scoring, intent-aware model tier routing |
| Phase 44 | Desktop application — Avalonia, 15 pages, streaming chat, dark/light theme |
| Phase 43 | Windows PowerShell native integration — cwd persistence, version detection |
| Phase 42.5 | Database lifecycle CLI — `sovrant db status/version/migrate/backup/inspect` |
| Phase 41 | Agent artifact tools — isolated produce-and-deposit pattern |
| Phase 40C | Supabase/Postgres optional backend (shipped in 0.9.8) |
| Phase 38 | Per-user token auth and database hardening |
| Phases 35–37 | Workspaces, projects, and user management |
| Phase 32 | SQLite persistence layer — 5 initial migrations, 26+ tables |
| Phase 29 | Swarm orchestrator — auto-decomposition, DAG execution, quality gate |
| Phase 28 | Eval framework — 3 grader types, pass@k metrics |
| Phase 27 | Multi-layered memory system |
| Phase 26 | Skills system — 32 composable workflow packages |
| Phase 25 | Governance, security monitoring, and audit |
| Phases 18–19 | Multi-agent orchestration: isolated + shared backends, team tools |
| Phase 17 | MCP OAuth authentication |
| Phase 16 | Dynamic MCP tool proxy (`MCPTool`) |
| Phase 15 | MCP server mode (stdio JSON-RPC 2.0) |
| Phase 13 | Frontend TypeScript SDK, structured diff view, session export |
| Phase 12 | Slack / webhook integration |
| Phase 11 | CI/CD pipeline integration (`--ci` flag, GitHub Actions, GitLab CI) |
| Phase 10 | LSP integration — 5 tools, 18 languages |
| Phases 7–9 | Security hardening, session lifecycle, multi-tenant credentials, rate limiting |
| Phases 1–6 | Initial build: agentic runtime, SmartRouter, 22 tools, CLI REPL, HTTP server |
