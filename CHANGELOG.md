# Changelog

All notable changes to Sovrant are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions correspond to tags on the `development` branch.

---

## [1.5.0] — 2026-08-26

> **Migration note:** V044–V046 are additive only (skill description/agent enrichment, code-manifest scaffolding, CodeValidateTool guide seed). No destructive schema changes in this release.

### Added

- **Phase 128 — code generation quality gates (Parts A–D)**: artifact routes gain proper content-disposition + security headers on zip download and force-download for unsafe-inline file types; `ArtifactManifest` gains a `Code` manifest (template, language, kind, build/run/test commands, entry point) populated for all 21 scaffold templates; every scaffold gets a CI workflow (`.github/workflows/ci.yml`) plus per-language project scaffolding (`.sln`/`Directory.Build.props`/`.editorconfig` for .NET); `CodeCreateTool`/`CodeCreateMultiTool` return build/run/test/next-step guidance in their responses (V045).
- **Phase 128e — `CodeValidateTool`**: compiler-free structural quality gates for generated code scaffolds — critical/warning gate checks per language (`.sln`, `package.json`, `go.mod`, `Cargo.toml`, `pom.xml`, etc.) plus universal gates (README, `.gitignore`, CI workflow), with remediation guidance per failed gate (V046).
- **Phase 114 — enriched all 32 built-in skill descriptions**: every BuiltIn skill row gets a 2–3 sentence description for the `IKnowledgeRouter` harness and Skills page; 9 skills with unset agent delegations get one wired; `verification-loop` skill's reference to a non-existent `Verify` tool corrected (V044).
- **Phase 126 — chat conversation UX**: per-turn tool calls collapse into a single work strip ("N actions · Read x3 · Grep x2 · 2.4s") with two-level expand, replacing the old per-tool-call box stack, on both Web and Desktop. Answer renders above the (now subordinate) work strip. Light-theme status colors (`--status-pass/warn/fail`) now defined explicitly instead of inheriting dark-theme values.
- **Standalone Postgres/Supabase database layout**: `db/postgres/PostgresSchema.sql` (plain Postgres, no Supabase-specific triggers/RLS) and `db/supabase/migrations/` (full GoTrue-mirrored schema for the Supabase CLI) replace the single combined schema file.

### Changed

- **Artifacts are projects-only** — workspace-level (project-less) artifact storage removed; every artifact now nests under `{workspace}/projects/{project}/artifacts/{run}`. On-disk root corrected to `~/.sovrant/workspaces` (previously `~/.sovrant/artifacts`, inconsistent with existing docs).
- **Provider setup pre-selects the personal workspace** by default when adding a provider (Web + Desktop) — other workspaces remain opt-in.
- **API key is now optional for local providers** (Ollama, LM Studio) on the provider add form, with UI copy reflecting it; previously required a dummy key.

### Fixed

- **Provider pin was abandoned on failure, silently rerouting to unconfigured Ollama** — `SmartRouter` dropped an explicit provider pin the moment the pinned provider was marked unhealthy (e.g. after repeated 401s from an expired key), falling back to cost-scored selection across *every* registered provider. Ollama is always registered at cost `0.0` regardless of whether it's configured, so it won every such fallback and failed against an unreachable `localhost:11434`. A pin now always wins, healthy or not.
- **Tool registry sent unbounded to OpenAI-compatible providers** — `ModelCapabilities.MaxTools` was never populated for any model, so the existing per-model tool cap never engaged; a full registry of 140+ tools (built-in + enabled MCP servers) got sent as-is and was hard-rejected by OpenAI/OpenRouter/Ollama's shared 128-tool limit. Added a 128-tool fallback cap for any model without an explicit override.
- **Model list not loading for Ollama** (Web + Desktop) — model-fetch helpers always set an `Authorization: Bearer` header, sending a malformed one when the key was empty; Ollama rejected it and returned no models.
- **Model list not loading when switching providers on Settings page** — the API key field was cleared before the model fetch fired, so key-gated providers (OpenRouter, etc.) always queried with an empty key.
- **Artifact routes missing workspace-membership authorization** — the two artifact-serving HTTP routes had no authorization check; any authenticated user could fetch any workspace's artifacts by guessing the URL. Both now 403 non-members, matching the existing `/v1/artifacts` API rule.
- **Provider setup "Set up →" link routed to a dead URL** — pointed at `/settings?tab=providers`, which Settings.razor silently ignores; now routes to `/admin/providers?provider=X` and preselects the provider.
- **`PostgresSchema.sql` out of sync with V043** — the username-column drop and unique constraint removal from the SQLite migration weren't mirrored to the Postgres/Supabase schema.

---

## [1.4.0] — 2026-06-25

> **Migration note:** V043 rewrites all `usr_{hex}` primary keys to email addresses and drops the `username` column via table recreation. Back up your database before upgrading any instance with existing users. The `POST /v1/users` request body and `GET /v1/users` response shape have changed (see **Changed** below).

### Added

- **V043 migration: email as user_id** — replaces opaque `usr_{hex}` PKs with the user's email address across all FK and soft-reference columns (`workspace_members`, `project_members`, `api_tokens`, `user_roles`, `auth_credentials`, `sessions`, `agent_runs`, `user_preferences`, `provider_profiles`, `swarm_events`, `workspace_memory`, `workspace_settings`, and more). Personal workspace IDs are updated in tandem (`ws-personal-usr_abc` → `ws-personal-john-example.com`). The `username` column is dropped from the `users` table (table recreation required due to SQLite UNIQUE column drop restriction).
- **MigrationRunner `-- sovrant:no-fk` directive** — migration SQL files that begin with `-- sovrant:no-fk` have `PRAGMA foreign_keys = OFF/ON` applied outside the transaction, enabling PK cascade rewrites that SQLite otherwise prohibits inside transactions.

### Changed

- **`IUserService.CreateAsync` signature** — removed `username` parameter; auth-registered users now use their email as `user_id` directly. OS-seeded dev identities use the explicit `userId` override.
- **`User` and `UserProfile` records** — `Username` property removed; display patterns now use `email ?? userId`.
- **`IUserService` — `GetByUsernameAsync` removed**, `UpdateAsync` no longer accepts `username`.
- **`POST /v1/users` admin route** — `username` field removed from request body; `email` is now required.

### Fixed

- **Provider label showed "Local" instead of "Ollama/OpenRouter"** — `FriendlyProviderName` now checks `provider.Name` before falling back to host-URL matching; `SmartRouter` is pinned to the correct provider on startup, settings save, and wizard completion.
- **FK error 19 on project creation** — `ProjectsViewModel.PersonalWorkspaceId` was a `static readonly` field evaluated at class-load time using `Environment.UserName` instead of the post-auth `App.SovrantUserId`; changed to a property.

---

## [1.3.0] — 2026-06-22

### Added

- **Admin edit/revert for standard document templates** — admins can edit any DB-backed document template inline on the web `/documents` page and revert to the built-in version; C# Excel-only templates (LoanAmortization, ExpenseReport) are correctly excluded.
- **Monaco editor** for prompt and JSON fields on the web.
- **Admin-gated agent create/edit/clone/delete** on web (was previously unrestricted).

### Fixed

- **Command Center privacy** — private records with `owner_user_id = ''` (rows migrated before Phase 123) were bypassing the mask guard and showing full content; `ShouldMask` now treats any private record with an unknown owner as masked.
- **Command Center audit view** — desktop was unmasking the current user's own private rows; Command Center is an admin audit view so all private rows now show as `(private)` regardless of ownership.
- **Command Center startup state** — desktop opened with the Agents icon selected but Command Center content on the right; startup now lands on User Dashboard with the correct nav group active.
- **Admin edit buttons on Skills and User Document Templates** — buttons had been accidentally commented out; restored with `Session.IsAdmin` guard.
- **Knowledge sub-nav order** — items are now sorted alphabetically (Artifacts, Code Templates, Documents, Memory, Skills, Tools) on both web and desktop.
- **OpenRouterPricingClient startup warning** — pricing fetch no longer runs at startup when no OpenRouter API key is configured.
- **ProcessAgent stdin pipe race** — `IOException: pipe is being closed` no longer thrown when a child process exits before reading its stdin.
- **Migration count assertions** in tests updated (40 → 42) to match V041/V042 migrations.
- **Project-level `NoWarn` overrides** — all `.csproj` files now append to `$(NoWarn)` instead of replacing it, so global suppressions in `Directory.Build.props` take effect everywhere.

### Changed

- **Avalonia 11.3.0 → 12.0.4** — desktop upgraded to Avalonia 12; `Markdown.Avalonia` removed (was unused since April; `SafeMarkdownPresenter` handles all markdown rendering). Avalonia 12 API fixes: `GotFocusEventArgs` → `FocusChangedEventArgs`, `IClipboard.SetTextAsync` → `SetValueAsync(DataFormat.Text, …)`, `TextBox.Watermark` → `PlaceholderText`.
- **Scriban 5.12.0 → 7.2.4** — resolves 1 Critical + 8 High + 3 Moderate CVEs (GHSA-5wr9-m6jw-xx44 and 11 others).
- **Bulk NuGet updates** — `Microsoft.Extensions.*` → 10.0.9, `Microsoft.Data.Sqlite` → 10.0.9, `Markdig` → 1.3.2, `ModelContextProtocol` / `ModelContextProtocol.AspNetCore` → 1.4.0, `CommunityToolkit.Mvvm` → 8.4.2, `Spectre.Console` → 0.57.0, `Markdown.Avalonia` → 11.0.3, xunit / coverlet / `Microsoft.NET.Test.Sdk` updates.
- **SQLitePCLRaw.lib.e_sqlite3 NU1903 suppressed globally** — no patched release exists yet (GHSA-2m69-gcr7-jv3q); suppression tracked in `Directory.Build.props` for removal once 2.1.12+ ships.

---

## [1.2.0] — 2026-06-18

### Added

- **Phase 124 — Auto-generated memory privacy (V042):**
  - `owner_user_id` column added to `session_summaries`, `learned_patterns`, and `instincts` (V042 migration, additive). Auto-generated memories are now stamped with the session owner at write time.
  - Load methods accept `ownerUserId` — query returns `owner_user_id = '' OR owner_user_id = $uid` so legacy unowned rows remain visible to everyone while new rows are scoped to their creator.
  - `SessionEndMemoryHandler` stamps the session owner on summaries at eviction time without filtering the source session load (prevents silent summary drops for sessions created under different ownership).

- **Phase 123 — Workspace memory with public/private scoping (V041):**
  - **V041 migration** adds `owner_user_id` and `is_private` columns to `workspace_memory` (additive; existing rows default to `owner_user_id = ''`, `is_private = 0` / public).
  - **Workspace Memory tab** on the Memory page (web `/memory` and desktop Knowledge → Memory) — shows workspace memory entries with layer badge and privacy icon (🔒/🔓); inline add form with layer selector and private toggle; delete button per entry.
  - **"+ Remember" button** in chat (web and desktop) — opens an inline panel to save a free-text note to workspace memory without needing to know the `/remember` slash command; defaults to private; panel closes automatically after save.
  - **Per-user memory injection** — `ConversationRuntime` auto-resolves the session owner's personal workspace via `IWorkspaceService.GetPersonalAsync` when `SOVRANT_WORKSPACE_ID` env var is not set, so each user's chat session injects their own workspace memories rather than a shared global.
  - **Privacy-aware `ListMemoryAsync`** — accepts `viewerUserId`; returns public entries plus the viewer's own private entries; admin path (null `viewerUserId`) returns all.
  - `MemoryInjector.BuildMemorySectionAsync` receives `ownerUserId` and passes it through to `ListMemoryAsync` for per-user filtering at the DB layer.

- **Phase 120 — Workspace access controls:**
  - **MCP server workspace gating** — each MCP server can be restricted to specific workspaces; sessions in ungated workspaces cannot call that server's tools. Enforcement is at the server layer (connection time), not just the UI.
  - **V040 migration** adds a stable `id` column (UUID surrogate) to `mcp_servers` so gating rules survive server renames; `name` remains the routing key.
  - **Provider profile workspace gating** — provider profiles can be scoped to a workspace; members inherit the profile, non-members cannot use it.
  - Admin UI for MCP workspace gating on web and desktop.

- **Admin navigation redesign (Phases 117–120):**
  - **Platform Integrations** (renamed from Integrations) moved under the Admin section — admin-only on web and desktop.
  - **Providers** moved from Settings to Admin (admin-only).
  - **Command Center** promoted to first item in Admin nav; becomes the default Admin landing page.
  - **Settings** moved from the nav rail to the footer avatar click — declutters the sidebar.
  - **Collapsible sidebar** — rail collapses to icon-only mode on web and desktop.
  - Roadmap entries added for Phase 117 (API endpoint integration) and Phase 118 (bootstrap configuration).

- **PostgreSQL / Supabase foundation:**
  - `PostgresSchema.sql` fully updated to V042 parity — `ADD COLUMN IF NOT EXISTS` guards for all new columns, V040 `mcp_servers.id` backfill (`gen_random_uuid()`), V041/V042 `owner_user_id` semantic comments.
  - **Supabase Auth mirror triggers** (`on_auth_user_created`, `on_auth_user_updated`) — new SUPABASE AUTH EXTENSION section in `PostgresSchema.sql` (Supabase-only, skip on standalone Postgres).
  - Role assignment from `app_metadata` — trigger reads `raw_app_meta_data->>'sovrant_role'` (service-role only; users cannot self-elevate); whitelist enforces `'admin'` only; `on_auth_user_updated` fires on `raw_app_meta_data` changes and syncs role automatically.
  - Commented-out RLS policy skeletons for `workspace_memory`, `session_summaries`, `learned_patterns`, `instincts`.

### Fixed

- **Memory privacy — four security fixes:**
  - `GET /workspaces/{id}/memory` was returning private entries to all workspace members — `viewerUserId` now passed to `ListMemoryAsync`.
  - Admin session evict loop was stripping the `##userId` pool-key suffix before calling `FireSessionEnd`, causing session-end memory to be written without an owner.
  - `SessionEndMemoryHandler` was passing `ownerUserId` to `LoadAsync`, causing silent summary drops for sessions created under different ownership.
  - `/remember` was saving patterns and instincts with `owner_user_id = ''`, making them globally visible — `ownerUserId` now threaded through `SlashCommandDispatcher` → `RememberCommand` via a default interface method overload (zero changes to the 28 other command implementations).
- Dashboard was showing only the last 7 days of own activity.
- Command Center was showing only one row due to a 7-day history window — extended to 30 days across sessions, missions, and agent runs.
- Privacy lock column missing from dashboard grid (web + desktop).
- Remember panel not closing automatically after save.
- Remember Save button remained disabled until the privacy checkbox was toggled — fixed by using `@bind:event="oninput"` on the textarea.
- Settings icon bar padding incorrect after Connect button removal.

### Changed

- `persistence.md` overhauled from V030/2026-05-09 to V042/2026-06-18: three-mode architecture description (SQLite / standalone Postgres / Supabase), step-by-step setup guides for both Postgres modes, corrected file layout, new environment variable tables (`SOVRANT_POSTGRES_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_SERVICE_ROLE_KEY`), updated known concerns.
- README updated with workspace provider gating documentation.
- Admin bootstrap procedure on Supabase changed from direct `UPDATE public.users` to `jsonb_set` on `auth.users.raw_app_meta_data` — trigger syncs role automatically; direct role writes on `public.users` are now explicitly discouraged.

### Schema

| Migration | Change |
|-----------|--------|
| V040 | `mcp_servers.id` — stable UUID surrogate key |
| V041 | `workspace_memory.owner_user_id` + `is_private` — per-user note privacy |
| V042 | `session_summaries`, `learned_patterns`, `instincts` — `owner_user_id` for memory ownership scoping |

---

## [1.1.0] — 2026-06-15

### Added

- **Phase 96 — Keystore in DB (V039):** master AES-256-GCM key moved from `.keystore` disk file into a `keystore` SQLite table.
  - V039 migration adds `keystore (scope TEXT PK, key_hex TEXT, created_at TEXT)`.
  - `SqliteCredentialStore.LoadOrCreateKeyAsync` reads key from DB first; one-time migration reads legacy `.keystore` file, writes to DB, then best-effort deletes the file.
  - `BootstrapConfig.KeystorePath` renamed to `LegacyKeystorePath`; `SOVRANT_KEYSTORE_PATH` env var still honoured for the migration path.
  - All credentials (MCP server configs, env vars, API keys) are encrypted at rest in a single DB file with no external key file dependency.

- **Phase 96 — MCP runtime variables:** per-server env var editor on Web and Desktop.
  - Inline key/value editor in the server detail pane (Integrations → Connected tab). Edit mode shows existing vars as editable rows; Save fetches the full server config and updates only the `Env` dict; Cancel discards.
  - `+ Add Variable` button adds blank rows; `✕` removes a row.
  - `KEY=VALUE` textarea in the stdio add form — env vars set at creation time.
  - JSON paste (`mcpServers` block) already populated `Env` from the `env` field; feedback message now reports env var count: `Imported: 'server' (12 env vars).`
  - `EnvVarRowViewModel` observable class for Desktop MVVM two-way binding.

- **Postgres store parity (V030–V039):** `PostgresSchema.sql` updated to match all SQLite migrations through V039 — all new tables, columns, and indexes present so Postgres deployments can run alongside SQLite without schema divergence.

### Fixed

- Agent badge in chat header was not scoped to the current session — opening a second session could show the wrong agent name in the badge.
- MCP env vars list on Desktop not refreshing after save.
- MCP env var delete button mispositioned causing horizontal scroll on Desktop.

### Schema

| Migration | Change |
|-----------|--------|
| V039 | `keystore` — AES-256-GCM master key in DB (migrated from `.keystore` file on first boot; new installs never create the file) |

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
