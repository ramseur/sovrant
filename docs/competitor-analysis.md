# Sovrant — Competitor Analysis

**Last updated:** 2026-05-02 (56 tools, 25 agent templates, Command Center cockpit, desktop app, web app, MCP server, mission engine, SDK covering the 97-endpoint server, BSL 1.1 published)
**Products analysed:** Claude Code · OpenClaude · opencode (SST) · Sovrant

---

## Executive Summary

The agentic coding tool space has four meaningful reference points as of 2026:

- **Claude Code** — the proprietary, Anthropic-backed gold standard. Deep Claude model integration, enterprise-grade safety tooling, and the widest IDE/CI coverage. Hard vendor lock-in and a closed codebase.
- **OpenClaude** — a community fork derived from Claude Code source code that was accidentally leaked in March 2026. Full tool parity with Claude Code, genuine multi-provider support. Legally precarious; subject to ongoing DMCA takedowns.
- **opencode (SST)** — a clean-room, MIT-licensed open source coding agent with 95K+ GitHub stars and 600K+ downloads. TypeScript/Bun, SQLite persistence, 75+ LLM providers, multi-interface (TUI + desktop + VS Code + web + remote server). The strongest open source competitor.
- **Sovrant** — a clean-room C# / .NET 10 reimplementation inspired by the architecture of OpenClaude. **No Anthropic source code was copied or incorporated** — every line is original C#. Source-available under BSL 1.1 with a four-year Apache 2.0 conversion (2030-04-29). Five delivery modes: CLI REPL, OpenAI-compatible HTTP server (97 endpoints), Avalonia desktop app (Windows/macOS/Linux), Blazor Server web app, and MCP server for IDE integration. SmartRouter with intent-aware model routing. 56 tools. 25 agent templates. 32 built-in skills. **Command Center cockpit** at `/command` aggregating active missions, team runs, agent runs, and sessions in one read-only live grid. Team orchestration, swarm auto-decomposition, mission engine, SQLite persistence with FTS5, eval framework, LSP integration, and a TypeScript SDK covering all 97 server endpoints.

**Sovrant's unique position:** the only option that is natively .NET, provides five independent frontends (CLI, server, desktop, web, MCP), has built-in orchestration with two backends, and ships enterprise multi-tenant infrastructure (per-user credentials, session TTL, rate limiting, workspace/project scoping). Unlike OpenClaude, Sovrant carries no Anthropic IP — it is a clean-room reimplementation in a different language and runtime, with no code derivation. Unlike opencode, it ships a mission engine, swarm orchestrator, and enterprise multi-tenant primitives out of the box.

---

## Product Overviews

### Claude Code (Anthropic)

Claude Code is Anthropic's official agentic coding CLI and web product. Distributed as an npm package (`@anthropic-ai/claude-code`), it is written in TypeScript (~512K lines) and runs on Node.js. The agent loop connects exclusively to Anthropic's hosted API — there is no self-hosted model option and no third-party LLM support.

Its architecture is a plugin-style tool system with ~40 discrete, permission-gated tool modules. A query engine (~46K lines) handles all API calls, streaming, caching, and orchestration. An IDE bridge system uses JWT-authenticated bidirectional channels for VS Code and JetBrains extensions. Orchestration/swarm orchestration (coordinator + parallel worker agents) is available behind feature flags. GitHub/GitLab CI integration and a Slack OAuth integration complete the enterprise surface.

**Source:** The Claude Code source code was inadvertently exposed in March 2026 via an npm source-map packaging error. Anthropic subsequently issued DMCA copyright takedown notices against repositories hosting the leak. OpenClaude and the wider community forks are a direct consequence of that leak.

### OpenClaude (Community / Gitlawb)

OpenClaude is a community-maintained fork derived directly from the leaked Claude Code source. Its defining modification is an OpenAI-compatible provider shim that replaces Anthropic API calls, allowing the full Claude Code tool set and agent loop to run against any OpenAI-compatible endpoint, Ollama, or LM Studio. Telemetry is stripped.

OpenClaude uses TypeScript with Bun as its runtime. It is CLI-only — no server component, no IDE extension, no web UI. The tool set and agent loop are structurally identical to Claude Code, including all ~40 tools and the same slash command set.

**Legal status:** OpenClaude declares an MIT licence but the underlying code is derived from Anthropic proprietary software. Anthropic has issued DMCA takedown notices against such repositories. The project's continued availability is uncertain and it cannot claim clean-room independent provenance.

### opencode (SST / anomalyco)

opencode is a clean-room, MIT-licensed open source coding agent built by the SST team (creators of the SST/Ion serverless infrastructure framework). After the original Go TUI version was renamed "Crush" and transferred to charmbracelet, SST retained the opencode name and rewrote the project in TypeScript/Bun in approximately one month. The rewrite has accumulated 95K+ GitHub stars and 600K+ downloads as of early 2026.

**Architecture:** A persistent background HTTP + SSE server (`opencode serve`) is the backend. Multiple client types connect to it — TUI (Solid.js), desktop (Tauri), VS Code extension (beta), web UI, and remote clients. All session state persists in SQLite via Drizzle ORM. The agent loop runs server-side; clients receive streamed events via SSE.

opencode supports 75+ LLM providers through a unified AI SDK abstraction layer and includes real LSP (Language Server Protocol) integration for 20+ languages, giving the agent access to diagnostics, go-to-definition, and symbol search rather than relying solely on text manipulation.

### Sovrant

Sovrant is a clean-room C# / .NET 10 reimplementation of an agentic AI engine, inspired by the architecture and feature set of OpenClaude (the community fork of Claude Code). **No Anthropic source code was copied, translated, or incorporated** — the project uses OpenClaude only as a functional reference for capability parity. Every line of Sovrant is original C# 14 code written from scratch in a completely different language and runtime.

Five delivery modes: a CLI REPL for interactive developer use, `Sovrant.Server` (ASP.NET Core HTTP server with 97 OpenAI-compatible endpoints), `Sovrant.Desktop` (Avalonia GUI for Windows/macOS/Linux), `Sovrant.Web` (Blazor Server browser UI), and `Sovrant.Mcp` (shared MCP protocol handlers — stdio via the CLI's `mcp-server` subcommand, HTTP/SSE via `Sovrant.Server`).

The SmartRouter routes each LLM call across configured providers (OpenAI-compatible, Ollama, native messages API) based on latency, cost, and health scores, with intent-aware model tier routing. 56 tools cover file operations, shell execution (Bash, PowerShell, REPL), web access, task management, notebook editing, LSP code intelligence, sub-agents, plan/worktree mode, team orchestration, swarm orchestration, mission management, skill execution, document generation, MCP resources, and quality verification. 25 agent templates and 32 built-in skills ship with the engine. Session state persists in SQLite (22 versioned migrations) with FTS5 full-text search. The Command Center cockpit (`/command` on Web and Desktop, default homepage post-Phase-90) gives the operator a single live view of every active mission, team run, agent run, and session — no more bouncing between Agents, Orchestration, Activity, and Chat. SQLite-backed team orchestration (two backends: isolated process-per-agent and shared in-process), a swarm auto-decomposition engine with DAG execution, file locking, and quality gates, and a mission engine for long-lived goal-driven execution are fully operational. A TypeScript/JavaScript SDK covers all 97 server endpoints with SSE streaming and a React `useChat()` hook.

---

## Comparison Table

| Dimension | Claude Code | OpenClaude | opencode | Sovrant |
|---|---|---|---|---|
| **Licence** | Proprietary | MIT (contested) | MIT | BSL 1.1 → Apache 2.0 (2030-04-29) |
| **Language / runtime** | TypeScript / Node.js | TypeScript / Bun | TypeScript / Bun | C# / .NET 10 |
| **LLM providers** | Anthropic only | 200+ via OpenAI compat | 75+ providers | OpenAI-compat + Ollama + native messages API |
| **Provider routing** | None (single) | None (single active) | Manual per-session switch | SmartRouter (auto, scored) + intent-aware tier routing |
| **CLI REPL** | ✅ | ✅ | ✅ (TUI) | ✅ |
| **HTTP server** | ❌ | ❌ | ✅ (SSE backend) | ✅ OpenAI-compatible (97 endpoints) |
| **IDE extension** | ✅ VS Code + JetBrains | ❌ | ✅ VS Code (beta) | ✅ MCP server mode (any MCP IDE) |
| **Desktop app** | ❌ | ❌ | ✅ Tauri (beta) | ✅ Avalonia (Win/Mac/Linux, 15 pages) |
| **Web UI** | ✅ claude.ai/code | ❌ | ✅ (beta) | ✅ Blazor Server (15 pages) |
| **Remote server mode** | ❌ | ❌ | ✅ | ✅ |
| **Session persistence** | Managed cloud | None | SQLite | ✅ SQLite + FTS5 |
| **Session resumption** | ✅ | ❌ | ✅ | ✅ |
| **Tool count** | ~40 | ~40 (inherited) | 20+ | 56 |
| **LSP integration** | ❌ | ❌ | ✅ 20+ languages | ✅ 18 languages |
| **MCP client** | ✅ | ✅ (inherited) | ✅ stdio + HTTP | ✅ |
| **MCP server mode** | ❌ | ❌ | ❌ | ✅ (stdio transport) |
| **Orchestration / teams** | ✅ (feature flag) | ✅ (inherited) | ❌ | ✅ Teams + Swarm |
| **Local / offline models** | ❌ | ✅ Ollama / LM Studio | ✅ Ollama / LM Studio | ✅ Ollama + LM Studio |
| **Air-gapped deployment** | ❌ | Partial | Partial | ✅ |
| **Multi-tenant credentials** | ❌ | ❌ | ❌ | ✅ Per-request API keys |
| **Per-user auth tokens** | Managed account | ❌ | ❌ | ✅ API token issuance |
| **Session TTL / eviction** | Managed cloud | ❌ | ❌ | ✅ Configurable TTL + LRU |
| **Rate limiting** | Subscription-based | ❌ | ❌ | ✅ Per-session RPM |
| **Usage tracking** | Subscription dashboard | ❌ | ❌ | ✅ Per-session + per-user |
| **Git worktree isolation** | ✅ | ✅ (inherited) | ✅ (git undo/redo) | ✅ |
| **CI/CD integration** | ✅ GitHub + GitLab | ❌ | ❌ | ✅ `--ci` flag + JSON output |
| **Slack integration** | ✅ | ❌ | ❌ | ✅ Webhook endpoint |
| **Context auto-compaction** | ✅ | ✅ (inherited) | ✅ | ✅ Configurable threshold |
| **Eval framework** | ❌ | ❌ | ❌ | ✅ 3 grader types, pass@k |
| **Agent templates** | ❌ | ❌ | ❌ | ✅ 25 built-in |
| **Built-in skills** | ❌ | ❌ | ❌ | ✅ 32 across 7 domains |
| **Frontend SDK** | ❌ | ❌ | ❌ | ✅ TypeScript (96-endpoint coverage) |
| **Cross-platform** | Mac / Linux / Windows | Mac / Linux / Windows | Mac / Linux / Windows | Windows / Linux / macOS |
| **No Node/Python/Go dep** | ❌ (Node) | ❌ (Bun) | ❌ (Bun + Go) | ✅ |
| **Legal status** | Proprietary ✅ | Contested ⚠️ | Clean MIT ✅ | Clean-room reimplementation ✅ |
| **Community scale** | Enterprise + large OSS | Small (at-risk) | 95K stars, 600K+ DL | Early stage |

---

## Dimension-by-Dimension Analysis

### Licensing and Legal Posture

Claude Code is proprietary and Anthropic-owned — no legal risk for users but also no ability to fork, audit, or self-host. OpenClaude is effectively a pirated distribution of Anthropic proprietary code dressed in an MIT label; it can be taken down at any time and cannot be used as a foundation for any commercial or enterprise product without legal exposure. opencode is clean-room MIT with no IP entanglement — the strongest open source position.

**Sovrant's provenance:** Sovrant is a **clean-room reimplementation** inspired by OpenClaude's architecture and feature set, written entirely in C# / .NET 10. No Anthropic source code was copied, translated, or incorporated at any point. The project used OpenClaude as a functional reference — understanding *what* an agentic coding tool should do (tool set, permission model, session flow, agent loop structure) — but all implementation is original code in a different language, runtime, and architecture. This is analogous to how a company might study a competitor's product to understand its capabilities and then build its own from scratch.

Key legal distinctions from OpenClaude:

| Factor | OpenClaude | Sovrant |
|---|---|---|
| **Source origin** | Direct fork of leaked Anthropic code | Clean-room reimplementation, no code copied |
| **Language** | Same as Claude Code (TypeScript) | Different language and runtime (C# / .NET 10) |
| **Code lineage** | Line-for-line derivation traceable to Anthropic | No line of code traces to any Anthropic source |
| **DMCA exposure** | Subject to takedowns (Anthropic has issued notices) | Not subject to code-derivation claims |
| **Commercial use** | High legal risk | Standard IP ownership — free to commercialise |

**Sovrant advantage:** clean provenance, full ownership of the codebase, no code-derivation IP risk, free to commercialise. The different-language clean-room approach is a well-established legal pattern (cf. Compaq's BIOS reverse-engineering of IBM PC, Google's reimplementation of Java APIs in Android — upheld by the Supreme Court in *Google v. Oracle*).

### LLM Provider Strategy

Claude Code is single-vendor by design and commercially committed to staying that way. OpenClaude and opencode both offer broad multi-provider support but treat all providers as interchangeable adapters with no intelligent routing. Sovrant's SmartRouter is architecturally differentiated: it scores providers by latency, cost, and error rate, routes each request to the optimal provider, and fails over automatically. No other product in this space has a routing layer at this level.

**Sovrant advantage:** SmartRouter is unique in the field and becomes more valuable as the provider landscape grows.

### Server and API Model

Claude Code and OpenClaude are CLI-only tools — there is no HTTP server component, no API surface for a frontend to connect to, and no multi-user session isolation. opencode runs a persistent local HTTP + SSE server but it is personal/local, not designed for multi-user cloud deployment. Sovrant's `Sovrant.Server` is an OpenAI-compatible HTTP server designed from the start for multi-user deployment — multiple frontends, multiple sessions, a defined auth model, and a roadmap to per-user credentials and session management.

**Sovrant advantage:** the only product with a server-first architecture suitable for building a multi-user product on top of.

### Session Persistence

Claude Code's session state is managed in Anthropic's cloud — opaque, inaccessible to the user, and non-portable. OpenClaude has no session persistence. opencode uses SQLite. Sovrant uses SQLite with FTS5 full-text search, versioned migrations, and 26+ tables covering sessions, memory, audit, credentials, token usage, workspaces, projects, teams, eval results, and swarm state. Legacy JSONL dual-write is available via environment variables.

**Sovrant advantage:** same queryability as opencode, plus full-text search, multi-domain persistence (not just sessions), workspace/project scoping, and a complete user/workspace/project hierarchy pre-built into the schema.

### Tool Parity

Claude Code and OpenClaude lead at ~40 tools (the leaked source). opencode has 20+ plus LSP integration (which makes file manipulation more semantically aware). Sovrant now has 50 tools — surpassing all competitors — including 5 LSP tools (18 languages), 4 MCP tools (ListMcpResources, ReadMcpResource, MCPTool, McpAuth), ToolSearch, Skill/SkillCreate, Swarm/SwarmStatus, team orchestration tools (TeamCreate, TeamDelete, TeamStatus, TeamDelegate, TeamRun, TeamPublish), Mission, and a 6-phase Verify quality gate.

**Sovrant advantage:** highest tool count in the field, with LSP parity and unique tools (Swarm, Mission, TeamRun, TeamPublish, Verify, SkillCreate) that no competitor offers.

### Enterprise and Multi-Tenant Readiness

None of the competitors have a self-hosted multi-tenant server deployment model. Claude Code has enterprise features (Teams/Enterprise plans, data residency options) but they are managed by Anthropic — the customer has no control. opencode and OpenClaude have no enterprise story at all. Sovrant now ships all the enterprise primitives: per-request LLM API keys (`X-LLM-Api-Key` header), session-scoped config overrides, per-user API token issuance and revocation, configurable session TTL with LRU eviction, per-session rate limiting (RPM), per-user and per-session usage tracking, workspace/project scoping with membership and invites, and an audit log. This is no longer a roadmap item — it is shipped.

**Sovrant advantage:** the only product with a complete, self-hosted enterprise multi-tenant deployment story.

### Runtime and Dependency Footprint

Claude Code requires Node.js. OpenClaude and opencode require Bun (and opencode also requires Go 1.24.x for development builds). Sovrant requires only the .NET 10 runtime, which is already present in most enterprise Windows environments and is trivially installable on Linux/macOS via a single script. For .NET shops deploying into IIS, Azure App Service, containers based on .NET base images, or any Windows Server environment, Sovrant has zero additional runtime dependencies.

**Sovrant advantage** for .NET-ecosystem organisations.

### Community and Maturity

Claude Code is backed by Anthropic with full enterprise investment. opencode has 95K GitHub stars and 600K+ downloads — the strongest open source community in this space. OpenClaude has a small community constrained by legal uncertainty. Sovrant is early-stage by community metrics but has a clean codebase, a fully tested server, and an explicit roadmap.

**opencode advantage** on community scale; **Claude Code advantage** on enterprise backing; **Sovrant** is early but unencumbered.

---

## Security Notes

Two disclosed CVEs against Claude Code as of early 2026:

| CVE | CVSS | Description |
|---|---|---|
| CVE-2025-59536 | 8.7 (High) | Arbitrary code execution via malicious project-level hook configurations |
| CVE-2026-21852 | 5.3 (Medium) | API key exfiltration via crafted repository contents |

Both exploit Claude Code's trust model around project files and hooks. Sovrant's permission system (tool-level gating, `bypassPermissions` / `dontAsk` / `plan` modes) should be evaluated against equivalent attack surfaces as the codebase matures.

---

## Sovrant Strategic Positioning

### Where Sovrant wins today

1. **Five independent frontends** — CLI, HTTP server (96 endpoints), desktop app (Avalonia), web app (Blazor), and MCP server. No competitor ships more than three.
2. **SmartRouter + intent-aware routing** — multi-provider routing with health/latency/cost scoring plus automatic model tier selection per intent class. Unique in the field.
3. **50 tools with LSP** — highest tool count, with 5 LSP tools (18 languages), swarm orchestration, mission management, quality gates, and skill system.
4. **Orchestration orchestration + missions** — Teams (SQLite-backed, two agent backends) + Swarm (auto-decomposition with DAG execution) + Missions (long-lived goal-driven execution). Claude Code has teams behind feature flags; no other competitor has anything comparable.
5. **Enterprise multi-tenant** — per-request LLM keys, API token issuance, session TTL/LRU, rate limiting, usage tracking, workspace/project scoping, audit log. All shipped, not roadmap.
6. **Native .NET** — zero runtime dependency for .NET shops; natural fit for Windows-first or Azure-first environments.
7. **Clean legal posture** — clean-room C# reimplementation with no Anthropic code copied. Different language, different runtime, no code-derivation IP risk.
8. **TypeScript SDK** — typed client covering the 96-endpoint server with SSE streaming and React hook. No competitor exposes an SDK for their API.
9. **Eval framework** — 3 grader types, pass@k metrics, trend tracking. No competitor ships a built-in eval system.

### Where Sovrant needs to close the gap

| Gap | Competitor ahead | Status |
|---|---|---|
| Community scale | opencode (95K stars) | Public launch tagged `v0.9.0` (2026-05-02); awaiting smoke test then flip-the-switch |
| IDE extension (native) | Claude Code (VS Code + JetBrains) | MCP server mode covers MCP-aware IDEs; native extension is future |
| Voice mode | Claude Code | Future |

---

## Features to Consider Adding to the Sovrant Roadmap

The following features exist in one or more competitors but are not yet in Sovrant. Previously identified gaps that have since been implemented are marked ✅.

---

### Previously Identified Gaps — Now Shipped ✅

| Feature | Status |
|---|---|
| Context auto-compaction | ✅ `SOVRANT_COMPACT_THRESHOLD` (default 80K tokens) |
| Session export / share | ✅ `GET /v1/sessions/{id}/export?format=markdown` |
| Custom project slash commands | ✅ `.sovrant/commands/{name}.md` |
| Agent memory files | ✅ `~/.sovrant/memory.md` + `.sovrant/memory.md` |
| LSP integration | ✅ 5 tools, 18 languages |
| MCP server mode | ✅ `Sovrant.Mcp` (stdio + HTTP/SSE transports) |
| Orchestration teams | ✅ Teams + Swarm, two backends |
| Desktop app | ✅ Avalonia (15 pages, setup wizard) |
| Web UI | ✅ Blazor Server (15 pages) |
| Frontend SDK | ✅ TypeScript SDK (covers the 96-endpoint server, SSE, React hook) |
| Multi-tenant credentials | ✅ `X-LLM-Api-Key` per-request header |
| Per-user auth tokens | ✅ `POST /v1/users/me/tokens` |
| Session TTL / eviction | ✅ `SOVRANT_SESSION_TTL_SECONDS` + LRU |
| Rate limiting | ✅ `SOVRANT_RATE_LIMIT_RPM` per-session |
| Usage tracking | ✅ `GET /v1/usage`, `GET /v1/users/{id}/usage` |
| CI/CD integration | ✅ `--ci` flag with JSON output, non-zero exit |
| Slack / webhook integration | ✅ `POST /v1/webhook` |
| Non-interactive headless mode | ✅ `sovrant prompt "..."` |
| Git worktree isolation | ✅ `EnterWorktree` / `ExitWorktree` |

---

### Remaining Gaps

#### Context Auto-Compaction
#### `/undo` / `/redo` (Git-backed)
**Has it:** opencode ✅
**What it does:** Every file write/edit the agent makes is committed to a git stash or a temporary branch. `/undo` reverts the last agent action; `/redo` reapplies it. This is fundamentally safer than the current model where tool writes are permanent.
**Why it matters for Sovrant:** Trust is the biggest barrier to giving an agent write permissions. Git-backed undo dramatically lowers the risk of an agent making a mistake — the user can always roll back. `EnterWorktree`/`ExitWorktree` handles isolated branches but undo/redo applies even outside a worktree.
**Suggested phase:** Future.

---

#### IDE Extension (VS Code / JetBrains)
**Has it:** Claude Code ✅ (VS Code + JetBrains) · opencode ✅ (VS Code beta)
**What it does:** Embeds the agent directly into the IDE — sidebar panel, inline diff view, permission dialogs with file highlighting, tool output rendered in context.
**Why it matters for Sovrant:** The HTTP server (`Sovrant.Server`) is the foundation — an IDE extension is essentially a frontend that connects to it. Once Phase 14 (MCP server mode) ships, Sovrant can be consumed by any MCP-aware IDE (VS Code with GitHub Copilot, Cursor, Windsurf) without a bespoke extension. The extension is the layer on top for richer UX.
**Suggested phase:** Phase 15 — VS Code Extension backed by `Sovrant.Server`. Phase 14 (MCP server mode) is the prerequisite.

---

#### Multi-File Diff View / Structured Edit Preview
**Has it:** Claude Code ✅ · opencode ✅ (StructuredDiff component, colour diff)
**What it does:** Before applying file edits, the agent shows a structured diff (unified diff or side-by-side) of the proposed changes. The user can approve, reject, or edit individual hunks.
**Why it matters for Sovrant:** The CLI currently shows raw edit output. A proper diff view in the REPL (using Spectre.Console's markup or a colour diff) would make the permission dialog for `Edit`/`Write` far more informative and trustworthy.
**Suggested phase:** Phase 13 (Frontend SDK) — implement in the CLI REPL using Spectre.Console colour rendering.

---

#### Voice Mode
**Has it:** Claude Code ✅ (added 2026)
**What it does:** Speech-to-text input and text-to-speech output for the agent loop. Primarily useful for hands-free operation during coding sessions.
**Why it matters for Sovrant:** Low priority for a coding tool — keyboard-first workflows dominate. Worth tracking as a future differentiator but not a near-term priority.
**Suggested phase:** Future / Phase 20+.

---

### Tier 3 — Speculative / Long-Term

| Feature | Has it | Notes |
|---|---|---|
| Background daemon (file watching, idle memory consolidation) | Claude Code (KAIROS, unreleased) | Long-term; `IHostedService` is the right .NET pattern |
| GitHub PR monitoring + autonomous fix loop | Claude Code | CI `--ci` flag is the prerequisite |
| Mobile client | Claude Code (partial) | Very long-term |
| Fine-tuning pipeline for model specialisation | None yet | Research phase |

---

## Updated Sovrant Feature Gap Summary

| Feature | Priority | Status |
|---|---|---|
| `/undo` / `/redo` git-backed | Medium | Not started |
| Native IDE extension (VS Code) | Medium | MCP server covers MCP-aware IDEs; native extension future |
| Structured diff view in REPL/UI | Low | Not started |
| Background daemon / file watching | Low | Future |
| Voice mode | Low | Future |

---

## Summary Verdict

| | Claude Code | OpenClaude | opencode | Sovrant |
|---|---|---|---|---|
| **Best for individual devs** | ✅ if Anthropic user | Risky | ✅ | ✅ |
| **Best for multi-user teams** | ✅ (managed) | ❌ | ❌ | ✅ (shipped) |
| **Best for enterprise deploy** | ✅ (Anthropic SaaS) | ❌ | ❌ | ✅ (self-hosted, shipped) |
| **Best provider flexibility** | ❌ | ✅ | ✅ | ✅ + SmartRouter + intent routing |
| **Best tool/agent ecosystem** | ✅ | ✅ (inherited) | ❌ | ✅ (50 tools, 25 templates, 32 skills) |
| **Best orchestration** | Partial (feature flag) | Partial (inherited) | ❌ | ✅ (Teams + Swarm) |
| **Best legal posture** | ✅ | ❌ | ✅ | ✅ |
| **Best .NET / Windows fit** | ❌ | ❌ | ❌ | ✅ |
| **Best open source community** | ❌ | ❌ | ✅ | Early |
