# Changelog

All notable changes to Sovrant are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions correspond to tags on the `main` branch.

---

## [2.1.0] — Unreleased

> **Multi-user Web (Phase 145, GitHub #32).** Embedded Web now signs in each browser separately, so a team can share one Web server: everyone sees only their own conversations, approvals and permission mode, and picks from the models their admins configured. File and shell tools are off on hosted servers by default. Load-tested with 1,000 people on one 4-core server ([docs/web-sizing.md](docs/web-sizing.md)). Web in front of Sovrant.Server (remote mode) is still single-user — Phase 146.

### Added

- **Long-running work (Phase 145, A8).**
  - **Run time limit:** any run — a chat reply, a workflow run, a swarm, a scheduled job — stops after **2 hours** by default, keeping what it finished, and says so ("Stopped after 2 hours — the time limit set by your admin"). Admins change it under Users → Registration & sign-in → **Stop runs after** (15 minutes to 8 hours; `SOVRANT_MAX_RUN_MINUTES` is the starting value). Workflows stopped this way are marked failed with the reason instead of looking as if they were still running.
  - **Work outlives signing out:** signing out or timing out doesn't stop your work; it runs as you and the result is in the conversation when you're back.
  - **Approvals wait for you:** a run that needs an approval while none of your tabs is open now waits — until you answer, the run is stopped, or the time limit — instead of being refused at once. A banner ("A run is waiting for your approval") links to it from any page, the card appears when you open that conversation, and a request on a tab you close moves to your next tab. Waiting doesn't use up a reply's own 5-minute timeout; it counts toward the run limit.
  - **Security sign-outs stop work:** "Sign out everywhere", an admin revoke and disabling an account stop that person's running replies, workflows and swarms.
- **Admins set how long people stay signed in to Web (Phase 145, A7).** Users → **Registration & sign-in** (Web; Desktop under the registration switches) has a Sign-in section: allow "Keep me signed in" or not, how long it lasts (7, 14, 30 or 90 days), sign out after inactivity (15 minutes to 8 hours) and at most (8, 12 or 24 hours). Changes reach browsers already signed in within about a minute: shorter limits shorten existing sign-ins, turning "Keep me signed in" off makes remembered browsers follow the inactivity and maximum limits, and longer limits never extend a sign-in that already exists. The Sign in page's wording follows the settings, and the checkbox is hidden when it's off. The `SOVRANT_WEB_*` variables are now only starting values. **"Keep me signed in" now lasts 14 days by default (was 30).**
- **Web load test and sizing guide (Phase 145, part E).** `tools/Sovrant.LoadTest` starts a real Web server, signs people in through the real form, opens a real Blazor connection for each and clicks between pages, measuring memory after a full garbage collection. Results: 1,000 people on one 4-core laptop within a 2 GB memory limit — no errors, page changes 33 ms (p95), about 0.5–1 MB per connected person. [docs/web-sizing.md](docs/web-sizing.md) gives server sizes by team size and how to set a memory limit.
- **Web: each browser signs in separately (Phase 145, part A).** Sign-in sets an HttpOnly cookie for that browser, so two people on one Web server are two different users, each with their own workspace, model, conversations and Home. Signed out after **1 hour** without activity (renewed by activity) and after **12 hours** regardless; **"Keep me signed in on this browser for 30 days"** on the sign-in page. All three are env-settable: `SOVRANT_WEB_IDLE_MINUTES`, `SOVRANT_WEB_MAX_SESSION_HOURS`, `SOVRANT_WEB_REMEMBER_DAYS` (`0` hides the checkbox). Sign out (this browser) and Sign out of all browsers are on Settings. If you're signed out by the timeout or by an admin, the sign-in page says why. Remote mode (Web in front of Server) still uses one sign-in until part C.
- **Web: tool approvals and permission mode are per person (Phase 145, part A).** An approval prompt goes only to the person whose work asked for it — the tab showing that conversation, else another of their tabs — and is never shown to anyone else (no tab open: denied, as before). Your permission mode (Settings) is saved for you and applies to your conversations only; Plan mode entered in one chat no longer switches everyone's.
- **Web: the sign-in timeout works inside an open tab, plus an account menu and admin control of sign-ins (Phase 145, part A).** Typing or clicking renews your sign-in (a light ping at most once a minute); an open tab checks every minute and goes to Sign in, saying why, when its sign-in ends (an hour idle, the 12-hour limit, revoked, or signed out in another tab), and the server stops acting as you even if the page ignores that. The avatar at the bottom of the sidebar opens your account menu: how long this browser stays signed in, how many browsers you're on, Settings, Sign out, and Sign out of all browsers. **Admin → Users** shows where each person is signed in, with **Revoke** per browser and **Sign out everywhere**, on Web and Desktop.
- **Web: each member's model choice is their own (Phase 145, part B).** Members pick from the admin-configured models allowed on the workspace they're in, and that pick applies to their conversations only. Before, a pick in the top bar rewrote the server's global model, provider and default key, so one person switching changed everyone's model mid-conversation. Signing in no longer copies anyone's choices into the global configuration. If a member's pick isn't allowed on the current workspace, their conversations use the workspace default.
- **Admins: a default model set for personal workspaces.** Admin → Workspaces → **All personal workspaces** chooses which configured providers (and their models) every personal workspace gets, plus a default model, on Web and Desktop. It's one setting, editable any time: added models appear in everyone's picker; anyone using a removed model moves to the default on their next message. Team workspaces keep their own list. Until an admin saves a default set, personal workspaces keep their current list.
- **Members can use the providers an admin configured.** Providers added under Admin → Providers and enabled for a workspace now reach that workspace's members; before, they only appeared for the admin who created them.

> **Migration note:** **V049 (additive):** new `web_sign_ins` table, one row per browser sign-in (only a hash of the cookie token is stored). Existing data is untouched. SQLite only for now: under the Postgres backend, users and sign-ins stay on SQLite (Phase 134 Part B).

### Security

- **WebFetch could reach the server's own network.** Only URLs that spelled out a private IP were refused, so a hostname resolving to one (an internal service name, `localhost.`), or a public page redirecting to the cloud metadata address (`169.254.169.254`) or an internal service, got through. Every connection is now checked on the address actually connected to — covering hostnames, each redirect and DNS rebinding — against loopback, private, link-local, carrier-grade NAT, multicast and reserved ranges (IPv4 written as IPv6 included). Connections through a configured web proxy are allowed. Applies everywhere WebFetch runs, as the old check did.
- **Members could run commands on Server through evals.** `POST /v1/evals/run` was open to any signed-in user, and eval code graders run commands from suite files on the server. It's now admin-only.
- **Web: members could run slash commands that affect everyone or the server's files.** `/eval`, `/memory` (edits the server's memory files), `/artifacts` (could export a zip to any path on the server, replacing what was there), `/swarm` (could switch swarm on for everyone with "yolo" permissions), `/websearch` and `/provider` (change the search backend or pinned provider for everyone) are now admin-only in Web chat. Desktop and the CLI are unchanged.
- **Web: no automatic sign-in after a restart.** Web saved the last sign-in token and restored it at startup for the whole server, so after a restart any visitor was signed in as that user without a password. Web no longer saves the token, deletes one left by an older version, and everyone signs in again after a restart. A guard test keeps it that way.
- **Web and Server: file and shell tools are off for everyone by default — admins included (Phase 145 Part D). Breaking for Server/API users who relied on them.** These tools run on the server itself, as its account with no folder limits, so an agent could reach other people's files, the database or the server's settings; hosted services don't run them on their own servers. Two Governance switches (Web and Desktop): **Allow file and shell tools on this server** (`SOVRANT_GOVERNANCE_HOST_FILE_TOOLS`), and, when that's on, **Let members use file and shell tools** (`SOVRANT_GOVERNANCE_MEMBER_FILE_TOOLS`); without the second only admins can use them. Covers files (Read, Write, Edit, Glob, Grep, …), shell (Bash, PowerShell, REPL), **background tasks (`TaskCreate`, which runs a shell command and was missed before)**, worktrees, LSP and code tools. Blocked tools are now also **hidden from the model**, and the system prompt tells it to save files with the Artifact tool; it no longer includes the server's own `memory.md` files and git status, which belonged to whoever runs the server. On Server the check uses each request's real user; scheduled workflows run as their owner. Desktop and the CLI run on your own machine and are unaffected. Safe code execution for hosted users is planned as Phase 147 (sandboxes).

### Fixed

- **Web: a dark box around the page heading after a refresh.** The heading is focused on load so screen readers announce the page; the browser drew its focus outline around it. The outline is now hidden for headings (they aren't controls), and the focus move stays.
- **Work outside a chat ran on the install default model.** Workflow runs (Workflows page, API, scheduler), webhooks and swarms started through the API now use their owner's model pick from the models allowed in their workspace — the same choice as their chats. A workflow started from a chat keeps that conversation's model.
- **Admin Home said "Invite your team" even after people had joined.** The step only counted members of team workspaces, but people join by registering into their own personal workspace, and Web has no invites yet. It's now "Add your team" (open registration, then set roles) and ticks once anyone else has an account.
- **Web: a new workspace didn't appear in the top bar until you signed in again.** Creating or deleting a workspace under Admin → Workspaces didn't tell the top bar. It does now, and the top bar also rechecks your workspaces on every page change — so one someone else adds you to shows up too. If the workspace you're in is deleted, you're moved back to your personal workspace.
- **Web: dropdown lists looked broken in the dark theme.** The page never told the browser it was dark, so open lists were drawn in the browser's light style with light text. Native controls now follow the theme, and option lists use the theme's colours.
- **Everyone was labelled "owner" in the bottom left.** It showed the role in the current workspace, and everyone owns their own personal workspace. It now shows the account role — **Admin** or **Member** — on Web and Desktop.
- **Server: admin-only routes answered members with a 500 instead of a 403.** Auth settings, config, engine and eval routes used a response that needs an authentication service Server doesn't have; members were still refused, but with "Internal Server Error". They now get 403 Forbidden.
- **A sign-in rush could use gigabytes of memory.** Each password check uses Argon2id with 64 MB, and 20 people signing in at once needed 64 MB each at the same time (a 300-person test peaked at 3 GB). At most one check per CPU core (up to 8) now runs at once; the rest wait their turn. Password strength is unchanged.
- **Leaving a running chat and coming back broke the reply.** Three causes, on Web and Desktop:
  - **Long replies lost their middle:** the buffer that rebuilds a reply you navigated away from stopped at 500 events, and a long reply streams thousands of text pieces. Text pieces are now merged, so the whole reply comes back.
  - **Web:** the rest of the reply was attached to your *previous* answer instead of its own message.
  - **Web:** opening a conversation whose reply had already finished (still ticked in the Active list) showed the reply twice — it was loaded from the conversation and replayed on top. Only a reply that's still running is replayed now.
  - **Desktop:** coming back through the sidebar added the start of the reply a second time; coming back from Home or Command Center opened the conversation without the running reply. All paths now pick up the running chat where it is.
- **Old, unanswered requests could be carried out later.** When a turn failed (no reply), the message stayed in what the model was sent, so a later unrelated message could set it off. Reported: a conversation held six unanswered "make a pdf on using ai at non profits" messages from a failed turn weeks earlier; replying "are you here" made the model generate that PDF. Messages that never got a reply (and any half-finished tool step from that turn) are no longer sent to the model, on Web, Desktop, Server and the CLI. They stay in the conversation, so you can resend one deliberately.
- **A conversation's own model was ignored:** the conversation runtime always used the server-wide model, so Sovrant.Server's per-session model (`model` on a chat request, or set on a session) was saved but never used. It's used now.
- **Postgres: new conversations were public:** the Postgres store didn't set `is_private` when it created a conversation, so the column default (0, public) applied. New conversations are now private, as on SQLite. Conversations created on Postgres before this fix keep their current setting; owners can make any of them Private from the chat header.
- **SQLite → Postgres migration made every conversation public:** the migrator didn't copy `is_private`. It does now.
- **Postgres: private conversations were shown to admins, and the privacy toggle failed.** Postgres conversation lists never read `is_private` or `workspace_id`, so every conversation looked public: admins saw private titles in Command Center instead of "(private)", and shared conversations never reached teammates' Home → Activity. Reading a conversation's privacy threw (an integer cast to a boolean) and setting it failed (a boolean written to an integer column). All fixed; lists now return the same fields as on SQLite, and a Postgres test covers it (runs when `SOVRANT_TEST_PG` is set; not yet run against a live database, tracked in Phase 134).
- **Home said conversations were public by default:** the member "Privacy & governance" card now says "Your conversations are private by default. Make one Public to share it with your workspace." A test checks the copy against the real default.
- **Web chat privacy tooltip:** "Public" now says it's visible to your workspace and admins (it said admins only).
- **Desktop chat text overflow (long-open known issue):** no longer reproduces since the Phase 137 chat rebuild. Long URLs, long link text, unbroken words, long inline code and wide table cells all wrap inside the reply; code blocks scroll sideways in their own box. A headless test now guards it.

### Internal

- **`Sovrant.Ui.Tests` sometimes hung forever.** The headless Avalonia render tests share one UI thread and occasionally deadlocked when run in parallel (3 of 15 runs). The project now runs its tests one at a time (a few seconds in total); 20 of 20 runs clean.
- `ConcurrentUsersTests`: 50 people take turns at the same moment on one runtime; each request keeps that person's model, identity, conversation and tool list, and members never get an admin's file tools.
- New `Sovrant.Web.Tests` project: multi-user tests that run the real Web app in-process on a throwaway database (one cookie jar per simulated browser).
- **Desktop:** all 72 `TextBox.Watermark` uses renamed to `PlaceholderText` (Avalonia 12 marks `Watermark` obsolete). The Desktop build is free of AVLN5001 warnings.
- **New `Sovrant.Hosting` project:** ASP.NET Core hosting helpers shared by Web and Server only, so Desktop and the CLI don't pull in ASP.NET Core. `ForwardedHeadersSetup` now lives there once instead of being copied into both apps.

---

## [2.0.0] — 2026-10-06

**A major release:** the app (`Directory.Build.props`) and the JS SDK (`@sovrant/sdk`) both move to **2.0.0**, because the Missions → Workflows rename removes the `/v1/missions*` API and the SDK's mission methods (see Breaking changes).

Workflows that plan before they run (and run on their own on Sovrant.Server), a new **Home** page with first-run onboarding, conversation folders, a refreshed app shell (sidebar, chat, icons) on Web and Desktop, friendly MCP connection errors, Ollama only when you've actually set it up, environment configuration that works in containers, and an API and SDK that can do what the apps do.

> **Migration notes:**
> - **V047 (one-way rename):** renames `missions`→`workflows`, `mission_events`→`workflow_events` and `mission_scratchpad`→`workflow_scratchpad` (plus their indexes and the `mission_id`→`workflow_id` FK column) via `ALTER TABLE … RENAME`. No data loss: existing rows and their full event journals carry over. There is no realistic undo once written under the new names, so it was tested against a copy of a real dev database. `db/postgres/PostgresSchema.sql` and `db/supabase/migrations/` carry the equivalent guarded rename (idempotent, safe to re-run).
> - **V048 (additive):** new `session_folders` table, and new nullable `sessions.folder_id` and `agent_runs.session_id` columns. Existing rows are untouched (both columns start `NULL`). Verified against a snapshot of a real database. Postgres: `PostgresSchema.sql`; Supabase: `db/supabase/migrations/20261002000000_session_folders.sql`.
> - No other schema changes. Onboarding state (`onboarding.*`) uses existing user preferences.

### Breaking changes

- **Missions are now Workflows, with no alias period:**
  - **HTTP:** `/v1/missions*` → `/v1/workflows*`.
  - **SDK** (`sdk/js`): `createMission`, `listMissions`, … → `createWorkflow`, `listWorkflows`, ….
  - **CLI:** `/mission` → `/workflow`.
  - **Agent tool:** `Mission` → `Workflow`. Built-in governance tiers were updated in lockstep; custom governance rules that name the `Mission` tool need updating.
  - **Cockpit rows:** `Kind: "mission"` → `"workflow"`.
  - **IDs:** new workflow IDs start with `workflow-`; existing `mission-` IDs keep working (IDs are opaque).
- **Ollama is no longer always registered.** Sovrant only contacts Ollama when an admin has added an Ollama provider **and** enabled it for the workspace. Installs that relied on the implicit `localhost:11434` provider need to add it under Admin → Providers. `OLLAMA_BASE_URL` now only pre-fills that provider's address.
- **`GET /v1/sessions` no longer lists system sessions** (it gained fields; see Changed).

### Added

- **Workflows (Phase 129):** give Sovrant a goal; it plans the steps and works through them.
  - **Background scheduler (Sovrant.Server):** `WorkflowSchedulerService` advances Planning/Running workflows with bounded concurrency. Configure it with `SOVRANT_WORKFLOW_POLL_SECONDS` / `SOVRANT_WORKFLOW_MAX_CONCURRENT` or workspace settings. Web and Desktop in embedded mode don't run the scheduler: a workflow advances when you press Run now / Resume or ask the Workflow tool.
  - **Workflows page (Web + Desktop):** goal, status, plan steps, event journal, and Run now / Resume / Cancel / Export. Plan and Journal are separate tabs, and the page refreshes live (every 4 s) while a workflow is planning or running.
  - **Plan first, then run:** "Generate Plan first" has the selected model break the goal into steps, which you can review and edit (add, remove, rewrite, change tier) before running. An edited plan is no longer silently re-planned.
  - **Real output:** the journal shows each step's actual output and artifact count, not just "Completed".
  - **A linked chat for every workflow:** seeded with the goal at creation. When a workflow completes, fails or needs review, a status message is posted there, however it was advanced (you, the scheduler, or the Workflow tool).
- **Home and first-run onboarding (Phases 140–142):** the Dashboard is now **Home**, on Web and Desktop.
  - **Overview tab (opens first):**
    - **Greeting:** "Welcome to Sovrant, <name>" on your first visit, then "Welcome back, <name>".
    - **At a glance:** a row of your six stats; each opens Activity.
    - **Get started checklist:** numbered steps across the page, ticked from real state. It collapses to "All set" with Dismiss when finished. Admins see "Set up Sovrant for your team" (connect a provider, enable providers for a workspace, invite your team, connect an integration, create an agent); members see "Get started" with only things they can do themselves (pick a model, first conversation, try an agent, explore Knowledge).
    - **What Sovrant can do:** eight cards linking to each area. Integrations, Trust Boundary & Governance and Workspaces are described to members as "Managed by your admin", with no link.
  - **Activity tab:** the stats and activity table.
  - **First-run sign-up:** on a server with no accounts, the login screen explains that the first account becomes the administrator and offers "Create administrator account" (Enter submits). It also shows an approval note when new accounts need approval, a progress line while working, and success messages in the success style. Based on Rahul Singh's issue #27.
  - **Starts on Home:** every launch and sign-in, including after first-run provider setup. `/welcome` redirects to Home; the nav item says Home (the URL is still `/dashboard`).
  - **Bigger chat welcome:** the empty chat fills the main area, with a larger mark and title, a 3-column suggestion grid, and a "What Sovrant can do" strip linking to Home. Based on Rahul Singh's #28.
- **Conversation folders (Phase 133):** file any conversation into a per-user folder tree, up to 5 levels deep and across all workspaces, on Web and Desktop.
  - **Managing folders:** ⋯ menus (new subfolder, rename, move, delete), a Move dialog, a chat-header breadcrumb with Move, search across folders, and drag and drop that refuses invalid drops while you drag. Deleting a folder moves its contents up a level; no conversation is ever deleted.
  - **Sidebar labels** (`Agent · x`, `Workflow · Running`, `Swarm · n runs`, `Team · n runs`, `Webhook · source`) are derived from live links, never stored.
  - **API and SDK:** 5 endpoints (`/v1/session-folders`, `PUT /v1/sessions/{id}/folder`) and SDK methods (`listSessionFolders`, `createSessionFolder`, `updateSessionFolder`, `deleteSessionFolder`, `moveSessionToFolder`).
  - **Every run gets a conversation:** swarm and team runs launched from a chat record it (`agent_runs.session_id`), and runs started elsewhere get their own, so every run can be found and filed. `POST /v1/swarm` now also records an `agent_runs` row.
- **Friendly MCP connection errors (Phase 139):** when an MCP server can't connect, Sovrant says why in one sentence instead of printing stack traces.
  - **Failure kinds:** couldn't resolve the host, not responding (timeout or refused), rejected credentials (401/403), or a certificate problem.
  - **Integrations (Web + Desktop):** an **Unavailable** badge, the reason, what happens next, and **Retry now**, or **Update key** for rejected credentials.
  - **Top bar:** the Integrations menu shows a warning with the reason on hover.
  - **Automatic retries:** network failures retry in the background after 10 s, 1 min and 5 min, so a server that was down at startup comes back, tools included, without a restart. Credential errors aren't retried.
  - **Console:** one line per failure. The full detail is in the log file.

- **API & SDK parity for 2.0 (Phase 143):** the API and JS SDK can now do what the apps do.
  - **Workflows:** `POST /v1/workflows/plan` (plan for review without running), `PUT /v1/workflows/{id}/plan` (edit the steps) and `POST /v1/workflows/{id}/cancel`.
  - **MCP status:** `GET /v1/mcp/servers` reports each server's state, a friendly reason and the next automatic retry; `POST /v1/mcp/servers/{name}/retry` (admin).
  - **SDK (`@sovrant/sdk` 2.0.0):** `planWorkflow`, `saveWorkflowPlan`, `cancelWorkflow`, `setWorkflowPrivacy`, `setSessionPrivacy`, `setAgentRunPrivacy` and `retryMcpServer`. `Workflow.status` now includes `awaitingHuman` and `cancelled`, and `Workflow` carries `plan_json`, `is_private` and `completed_at`.
- **Environment configuration that works everywhere (Phase 144; GitHub #33, #34, #35):**
  - **Provider keys from the environment:** `LLM_API_KEY` (alias `OPENAI_API_KEY`), `PROVIDER_API_KEY`, `OPENROUTER_API_KEY`, `BRAVE_API_KEY` and `FIRECRAWL_API_KEY` are imported into the encrypted credential store on first boot. After that the stored value wins, so changes made in the UI stick. `SOVRANT_ENV_KEYS_OVERRIDE=true` re-imports them on every start.
  - **No setup for containers:** with `LLM_API_KEY` set, it becomes one shared provider (owned by the first admin) in the default model set for everyone's personal workspace. `LLM_BASE_URL` sets its address (otherwise inferred from the key) and `SOVRANT_MODEL` the default model.
  - **Web hosting:** `SOVRANT_WEB_PORT` (default 5100); `GET /health` and `GET /ready` (503 until start-up finishes) for container probes.
  - **Reverse proxies (Web and Server):** `X-Forwarded-For/-Proto/-Host` are honoured from trusted proxies: loopback by default, plus `SOVRANT_TRUSTED_PROXIES` (IPs, CIDRs or `*`).

### Changed

- **App sidebar (Phase 135):** Conversations, with their folders, stay in the sidebar on every page.
  - **Nav groups:** Knowledge, Agents and Admin are collapsible groups with their pages inline. One group is open at a time, and the current page's group opens automatically.
  - **Collapsed rail:** opens a flyout of a group's pages on hover, click or Enter/Space. Chat's flyout lists the 5 most recent conversations.
  - **Restyle:** the left nav has line icons, an accent bar for the active item, and Admin's pages grouped under Overview / Access / Safety / System. The old per-group side panels are gone.
  - **Brand mark:** shown only in the browser tab (Web) and title bar (Desktop). The rail's brand row is gone, and the collapse toggle sits on the rail's edge.
- **Chat (Phase 137):** user messages are right-aligned bubbles with an initials avatar; assistant replies sit flat beside a neutral avatar, with one model · elapsed · Copy line.
  - **Layout:** the thread and composer share a centred column, capped at 760px.
  - **Send/Stop:** one brand icon button is Send, and becomes Stop while a reply is generating. Esc stops a reply on Web too.
- **Icons (Phase 136):** Web and Desktop share one Lucide icon vocabulary (`Sovrant.Api.Ui.IconNames`). Every emoji and hand-copied icon in the UI chrome is gone. Brands use a category icon until licensed logos are added. `IntegrationCatalog.Icon` now holds an icon name instead of an emoji.
- **Model providers (Phase 138):**
  - **Own address:** every provider profile, cloud or local, uses its own URL. LM Studio and other local endpoints are no longer sent to Ollama's port.
  - **Workspace enablement:** a saved provider that isn't enabled for the current workspace is switched off, with a clear message, until it is.
  - **Local providers:** first-run setup accepts an empty API key for Ollama and LM Studio.
- **Orchestration:** the Swarm-defaults gear is now an explicit Team / Defaults toggle.
- **Web favicon:** Web now has a favicon, the same mark as Desktop.
- **`GET /v1/sessions` returns more per row:** `session_id`, `title`, `updated_at`, `folder_id`, `agent_name`, `is_private` and `labels`, alongside `id`.
- **Faster Web sidebar:** it uses stored titles instead of loading every conversation's history.
- **Desktop picks up changes made on Web** (same database): the conversation tree reloads when the Chat menu opens or the window regains focus.

### Fixed

- **Documented environment variables that did nothing:** `LLM_API_KEY`, `SOVRANT_MODEL`, `LLM_BASE_URL`, `OPENROUTER_API_KEY` and others were listed in `.env.example` but never read. All of them work now (Phase 144), and a test fails if `.env.example` documents a variable the code doesn't read.
- **`.env` was loaded too late for some variables:** `SOVRANT_USER_ID` (Web, Desktop) and `SOVRANT_RUNTIME_MODE` (Web) were read before `.env` loaded. Every app now loads `.env` first.
- **SDK `createWorkflow` ignored `session_id`, `workspace_id` and `project_id`:** the route didn't bind snake_case fields. It does now, like the other routes.
- **Prompts sometimes needed sending twice:** OpenRouter (especially `:free` models) can answer HTTP 200 and then report a rate limit or busy upstream *inside* the stream, or close it empty. The turn "completed" in about 0.3 s with no reply.
  - **In-stream errors:** now surfaced as provider errors.
  - **Empty replies:** count as failures.
  - **Retries:** both are retried by the existing 3-attempt backoff, and only shown in chat with Retry if every attempt fails.
- **Desktop crashed when you closed the sign-in window or the setup wizard:** a shutdown loop ended in a stack overflow. Quitting now goes through one exit routine that runs once, with a watchdog so a stalled exit can't leave a process running.
- **Desktop: "+ New" on the Agents page did nothing:** the editor was hidden along with the cleared selection.
- **Desktop opened the Agents page after signing out and back in:** it now starts on Home.
- **Desktop: admin pages could be opened by name:** in-app links naming an admin page opened it for non-admins, although the nav group was hidden. Desktop now refuses them, matching Web.
- **Web: importing several MCP servers at once connected only the first:** the page updated from the wrong thread and the error was swallowed. The same mistake in the OAuth connect flow is fixed too.
- **Workflows:**
  - **Double runs:** clicking Run twice could start two full plan-and-execute cycles at once.
  - **Leftover "mission" wording:** removed from Dashboard and journal labels.
  - **Long titles:** long workflow and team titles now wrap instead of overflowing.
- **Swarm file locks never applied to `Write`/`Edit`:** concurrent swarm workers could overwrite each other's files. Locks now apply under the real tool names, and `SwarmConfig.FileLocksEnabled` is honoured.
- **13 built-in tools had no explicit governance tier:** they fell back to the default. Read-only tools are now Safe; artifact-writing ones are Moderate. A coverage test catches missing tiers.
- **Isolated agents failed when the child process exited early:** a broken pipe on closing stdin failed the run. (From Rahul Singh's fork, PR #8.)
- **Eval code graders ran only the first word of their command on Linux/macOS.**
- **Artifact file URIs on Linux/macOS** were malformed (`file:////home/...`).
- **Remote mode:** the session list read the wrong field and threw.
- **Postgres:** session search failed (column mismatch).
- **Desktop Orchestration buttons rendered transparent:** a brush lookup missed application resources.
- **Desktop sign-in window ignored the theme:** its background pointed at a resource that didn't exist, and error text was hard-coded red. Both now use theme colours.
- **Gemma 4 capability overrides had expired** (2026-07-01) and were being ignored; renewed.

### Internal

- New `Sovrant.Ui.Tests` project (xUnit v3 + Avalonia headless): icon vocabulary, no-emoji guard, headless render tests, and Desktop Home / Welcome / Integrations render tests.
- Tests that change process environment variables now run in one non-parallel collection (fixes an intermittent `OllamaOptInTests` failure). The LSP Windows-path test is skipped on non-Windows hosts.
- `docs/design/` (`web.html`, `desktop.html`, `README.md`) is the cross-platform design record; every UI phase is mocked there first.
- Test suite: 2,474 tests (3 skipped on Windows).

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
