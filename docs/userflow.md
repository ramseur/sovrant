# Web App User Flow

A screen-by-screen walkthrough of the Sovrant Web app (Blazor Server, `http://localhost:5100`), captured from a live run of version 1.4.0 in embedded mode with a fresh database. Every screenshot lives in [`docs/images/`](images/) with the `web-flow-` prefix.

The flow follows the order a new user encounters the product: first-run setup → chat home → the rail-nav sections (Dashboard, Knowledge, Agents, Projects) → settings and provider setup → the admin surfaces.

---

## 1. First-run: create the administrator account

**Route:** `/login`

On a fresh install no accounts exist, so the login page switches to first-time setup. The first account created becomes the administrator with full control over registration, approvals, and server settings. On subsequent visits this page shows the normal sign-in form (plus self-registration, if the admin has enabled it).

![Login — first-run setup](images/web-flow-01-login-first-run.png)

## 2. Chat (home)

**Route:** `/`

After registering, the user lands on Chat — the home screen. The empty state asks "What are you working on?" and offers starter prompts (create a custom agent, build a team, set up a mission, connect an MCP server). The left sidebar holds **+ New Chat** and conversation search; the top context bar exposes the model picker ("No model" until a provider is configured), workspace selector, project selector, and integrations menu. The composer includes a per-record **Private** toggle and a **+ Remember** control.

![Chat home](images/web-flow-02-chat.png)

## 3. User Dashboard

**Route:** `/dashboard` (rail nav: chart icon)

Cross-workspace activity for the signed-in user: missions, team runs, agent runs, sessions, shared items, and Claws, with quick actions to start a chat or run an agent.

![User Dashboard](images/web-flow-03-dashboard.png)

## Knowledge section

### 4. Memory

**Route:** `/memory`

Per-user memory entries the agents can recall across sessions, with privacy scoping.

![Memory](images/web-flow-04-memory.png)

### 5. Documents

**Route:** `/documents`

Document library for the current workspace — files agents can read, produce, and update.

![Documents](images/web-flow-05-documents.png)

### 6. Document Templates

**Route:** `/documents/templates`

Reusable document templates users can instantiate.

![Document Templates](images/web-flow-06-document-templates.png)

### 7. Artifacts

**Route:** `/artifacts`

Artifacts produced by agent runs (reports, files, outputs), browsable per workspace.

![Artifacts](images/web-flow-07-artifacts.png)

### 8. Guidelines

**Route:** `/guidelines`

Behavioral guidelines that steer agents (the CLAUDE.md-style instruction layer).

![Guidelines](images/web-flow-08-guidelines.png)

## Agents section

### 9. Agents

**Route:** `/agents`

The 25 built-in agent templates (architect, coder, code-reviewer, data-analyst, …) in a master–detail layout showing each template's role, effort level, and tool grants, plus a **+ New** button for custom agents and a recent-runs strip.

![Agents](images/web-flow-09-agents.png)

### 10. Skills

**Route:** `/skills`

The 32 built-in skills agents can invoke, with search and detail view.

![Skills](images/web-flow-10-skills.png)

### 11. Tools

**Route:** `/tools`

The 58 tools available to agents (file I/O, shell, web search, delegation, …), including enablement and permission info.

![Tools](images/web-flow-11-tools.png)

### 12. Tool Templates

**Route:** `/tools/templates`

User-defined tool templates.

![Tool Templates](images/web-flow-12-tool-templates.png)

### 13. Orchestration

**Route:** `/orchestration`

Teams, swarms, and missions — the multi-agent orchestration surfaces with per-team run profiles.

![Orchestration](images/web-flow-13-orchestration.png)

## Projects section

### 14. Projects

**Route:** `/projects`

Projects within the current workspace; conversations and runs can be filed under a project via the top context bar.

![Projects](images/web-flow-14-projects.png)

### 15. Workspaces

**Route:** `/workspaces`

The user's workspaces (a personal workspace is seeded on first login) and membership management.

![Workspaces](images/web-flow-15-workspaces.png)

### 16. Code

**Route:** `/code`

Code-focused view for repository-oriented sessions.

![Code](images/web-flow-16-code.png)

## Settings & provider setup

### 17. Settings

**Route:** `/settings`

Per-user preferences: appearance, defaults, and account controls (including sign-out).

![Settings](images/web-flow-17-settings.png)

### 18. Provider Setup

**Route:** `/setup`

Connect an LLM provider (API key + model) so chats and agent runs have a model to route to. Reachable from the "Set up →" link in the model picker; models can be switched anytime from the sidebar.

![Provider Setup](images/web-flow-18-provider-setup.png)

## Admin section (admin role only)

### 19. Command Center

**Route:** `/command`

The live cockpit: missions, team runs, agent runs, sessions, and Claws currently in flight, auto-refreshing while runs are active.

![Command Center](images/web-flow-19-command-center.png)

### 20. Admin — Users

**Route:** `/admin`

User management with tabs for Users, Registration (open/closed, approval required), and Password Reset. The first-run admin account appears as active with the admin role.

![Admin Users](images/web-flow-20-admin.png)

### 21. Admin — Providers

**Route:** `/admin/providers`

Server-wide LLM provider configuration.

![Admin Providers](images/web-flow-21-admin-providers.png)

### 22. Admin — Integrations

**Route:** `/admin/integrations`

Webhook/chat integrations (Slack, Teams, Discord, custom).

![Admin Integrations](images/web-flow-22-admin-integrations.png)

### 23. Admin — System Integrations

**Route:** `/admin/system-integrations`

System-level integrations such as MCP servers and Claw runtimes.

![Admin System Integrations](images/web-flow-23-admin-system-integrations.png)

### 24. Admin — Workspaces

**Route:** `/admin/workspaces`

Administration across all workspaces on the server.

![Admin Workspaces](images/web-flow-24-admin-workspaces.png)

### 25. Governance

**Route:** `/governance`

Governance controls: policies and limits that bound what agents may do.

![Governance](images/web-flow-25-governance.png)

### 26. Diagnostics

**Route:** `/diagnostics`

Runtime diagnostics: health, logs, and environment information.

![Diagnostics](images/web-flow-26-diagnostics.png)

### 27. Trust Boundary

**Route:** `/trust-boundary`

The trust-boundary view — what data and capabilities are exposed where, in line with the security architecture (see [security-architecture.md](security-architecture.md)).

![Trust Boundary](images/web-flow-27-trust-boundary.png)

---

## How these were captured

Screenshots were taken with Playwright/Chromium at 1440×900 against a debug build (`dotnet run --project src/Sovrant.Web`) with a fresh SQLite database: the script registers the first-run admin account through the real login form, then visits each route as that user. Re-running the capture on an existing database signs in instead of registering.
