# Sovrant — Competitor Analysis

**Last updated:** 2026-04-04 (Phase 7.5 Tier 1 complete — 27 tools)
**Products analysed:** Claude Code · OpenClaude · opencode (SST) · Sovrant

---

## Executive Summary

The agentic coding tool space has four meaningful reference points as of 2026:

- **Claude Code** — the proprietary, Anthropic-backed gold standard. Deep Claude model integration, enterprise-grade safety tooling, and the widest IDE/CI coverage. Hard vendor lock-in and a closed codebase.
- **OpenClaude** — a community fork derived from Claude Code source code that was accidentally leaked in March 2026. Full tool parity with Claude Code, genuine multi-provider support. Legally precarious; subject to ongoing DMCA takedowns.
- **opencode (SST)** — a clean-room, MIT-licensed open source coding agent with 95K+ GitHub stars and 600K+ downloads. TypeScript/Bun, SQLite persistence, 75+ LLM providers, multi-interface (TUI + desktop + VS Code + web + remote server). The strongest open source competitor.
- **Sovrant** — a .NET 10 / C# implementation of an agentic coding engine. Dual-mode (CLI REPL + OpenAI-compatible HTTP server). SmartRouter. 27 tools (Phase 7.5 Tier 1 complete). JSONL session persistence. Roadmap targeting enterprise multi-tenancy, team orchestration, and MCP server mode.

**Sovrant's unique position:** the only option that is natively .NET, provides an OpenAI-compatible HTTP server out of the box, and has a clear roadmap toward cloud multi-tenant deployment with per-user credential isolation — without the legal exposure of OpenClaude or the Node.js runtime dependency of opencode.

---

## Product Overviews

### Claude Code (Anthropic)

Claude Code is Anthropic's official agentic coding CLI and web product. Distributed as an npm package (`@anthropic-ai/claude-code`), it is written in TypeScript (~512K lines) and runs on Node.js. The agent loop connects exclusively to Anthropic's hosted API — there is no self-hosted model option and no third-party LLM support.

Its architecture is a plugin-style tool system with ~40 discrete, permission-gated tool modules. A query engine (~46K lines) handles all API calls, streaming, caching, and orchestration. An IDE bridge system uses JWT-authenticated bidirectional channels for VS Code and JetBrains extensions. Multi-agent/swarm orchestration (coordinator + parallel worker agents) is available behind feature flags. GitHub/GitLab CI integration and a Slack OAuth integration complete the enterprise surface.

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

Sovrant is a .NET 10 / C# agentic coding engine with two delivery modes: a CLI REPL for interactive developer use and `Sovrant.Server`, an ASP.NET Core HTTP server that exposes OpenAI-compatible endpoints. This makes Sovrant both a developer tool and an API backend that any OpenAI-compatible client can connect to.

The SmartRouter routes each LLM call across configured providers (OpenAI-compatible, Ollama, native messages API) based on latency, cost, and health scores. Session state persists as JSONL append-logs. The tool set covers 27 tools across file operations, shell execution, web, task management, notebook editing, sub-agents, plan mode, and git worktree isolation (Phase 7.5 Tier 1 complete). The roadmap targets multi-tenant per-user credential isolation, session TTL/eviction, per-user auth tokens, team orchestration, MCP OAuth, and a TypeScript frontend SDK.

---

## Comparison Table

| Dimension | Claude Code | OpenClaude | opencode | Sovrant |
|---|---|---|---|---|
| **Licence** | Proprietary | MIT (contested) | MIT | Not yet published |
| **Language / runtime** | TypeScript / Node.js | TypeScript / Bun | TypeScript / Bun | C# / .NET 10 |
| **LLM providers** | Anthropic only | 200+ via OpenAI compat | 75+ providers | OpenAI-compat + Ollama + native messages API |
| **Provider routing** | None (single) | None (single active) | Manual per-session switch | SmartRouter (auto, scored) |
| **CLI REPL** | ✅ | ✅ | ✅ (TUI) | ✅ |
| **HTTP server** | ❌ | ❌ | ✅ (SSE backend) | ✅ OpenAI-compatible |
| **IDE extension** | ✅ VS Code + JetBrains | ❌ | ✅ VS Code (beta) | ❌ (roadmap via MCP) |
| **Desktop app** | ❌ | ❌ | ✅ Tauri (beta) | ❌ |
| **Web UI** | ✅ claude.ai/code | ❌ | ✅ (beta) | ❌ (roadmap frontend SDK) |
| **Remote server mode** | ❌ | ❌ | ✅ | ✅ |
| **Session persistence** | Managed cloud | None | SQLite | JSONL (file-based) |
| **Session resumption** | ✅ | ❌ | ✅ | ✅ |
| **Tool count** | ~40 | ~40 (inherited) | 20+ | 27 |
| **LSP integration** | ❌ | ❌ | ✅ 20+ languages | ❌ (roadmap) |
| **MCP client** | ✅ | ✅ (inherited) | ✅ stdio + HTTP | ✅ (partial) |
| **MCP server mode** | ❌ | ❌ | ❌ | Roadmap Phase 14 |
| **Multi-agent / teams** | ✅ (feature flag) | ✅ (inherited) | ❌ | Roadmap Phase 18 |
| **Local / offline models** | ❌ | ✅ Ollama / LM Studio | ✅ Ollama / LM Studio | ✅ Ollama |
| **Air-gapped deployment** | ❌ | Partial | Partial | ✅ |
| **Multi-tenant credentials** | ❌ | ❌ | ❌ | Roadmap Phase 8 |
| **Per-user auth tokens** | Managed account | ❌ | ❌ | Roadmap Phase 9.5 |
| **Session TTL / eviction** | Managed cloud | ❌ | ❌ | Roadmap Phase 9 |
| **Rate limiting** | Subscription-based | ❌ | ❌ | Roadmap Phase 9.5 |
| **Usage tracking** | Subscription dashboard | ❌ | ❌ | Roadmap Phase 9.5 |
| **Git worktree isolation** | ✅ | ✅ (inherited) | ✅ (git undo/redo) | ✅ Phase 7.5 Tier 1 |
| **CI/CD integration** | ✅ GitHub + GitLab | ❌ | ❌ | ❌ |
| **Slack integration** | ✅ | ❌ | ❌ | ❌ |
| **Context auto-compaction** | ✅ | ✅ (inherited) | ✅ | ❌ (roadmap) |
| **Cross-platform** | Mac / Linux / Windows | Mac / Linux / Windows | Mac / Linux / Windows | Windows / Linux / macOS |
| **No Node/Python/Go dep** | ❌ (Node) | ❌ (Bun) | ❌ (Bun + Go) | ✅ |
| **Legal status** | Proprietary ✅ | Contested ⚠️ | Clean MIT ✅ | Owned codebase ✅ |
| **Community scale** | Enterprise + large OSS | Small (at-risk) | 95K stars, 600K+ DL | Early stage |

---

## Dimension-by-Dimension Analysis

### Licensing and Legal Posture

Claude Code is proprietary and Anthropic-owned — no legal risk for users but also no ability to fork, audit, or self-host. OpenClaude is effectively a pirated distribution of Anthropic proprietary code dressed in an MIT label; it can be taken down at any time and cannot be used as a foundation for any commercial or enterprise product without legal exposure. opencode is clean-room MIT with no IP entanglement — the strongest open source position. Sovrant is a wholly owned codebase with no Claude Code source derivation; its legal status is clean.

**Sovrant advantage:** no IP risk, full ownership of the codebase, free to commercialise.

### LLM Provider Strategy

Claude Code is single-vendor by design and commercially committed to staying that way. OpenClaude and opencode both offer broad multi-provider support but treat all providers as interchangeable adapters with no intelligent routing. Sovrant's SmartRouter is architecturally differentiated: it scores providers by latency, cost, and error rate, routes each request to the optimal provider, and fails over automatically. No other product in this space has a routing layer at this level.

**Sovrant advantage:** SmartRouter is unique in the field and becomes more valuable as the provider landscape grows.

### Server and API Model

Claude Code and OpenClaude are CLI-only tools — there is no HTTP server component, no API surface for a frontend to connect to, and no multi-user session isolation. opencode runs a persistent local HTTP + SSE server but it is personal/local, not designed for multi-user cloud deployment. Sovrant's `Sovrant.Server` is an OpenAI-compatible HTTP server designed from the start for multi-user deployment — multiple frontends, multiple sessions, a defined auth model, and a roadmap to per-user credentials and session management.

**Sovrant advantage:** the only product with a server-first architecture suitable for building a multi-user product on top of.

### Session Persistence

Claude Code's session state is managed in Anthropic's cloud — opaque, inaccessible to the user, and non-portable. OpenClaude has no session persistence. opencode uses SQLite, which is queryable and richer than flat files but adds a database dependency. Sovrant uses JSONL — human-readable, portable, appendable without locking, trivially processable with standard tooling, and easy to migrate or export.

**opencode advantage** for complex querying; **Sovrant advantage** for simplicity, portability, and auditability.

### Tool Parity

Claude Code and OpenClaude lead at ~40 tools (the leaked source). opencode has 20+ plus LSP integration (which makes file manipulation more semantically aware). Sovrant has 27 tools (Phase 7.5 Tier 1 complete) with 7 tools remaining to close the gap (ListMcpResources, ReadMcpResource, ToolSearch, SkillTool, ScheduleCron, ConfigTool, LSP). The gap is closable; the LSP integration is a meaningful differentiator for opencode that Sovrant should target in a later phase.

**opencode advantage** on LSP; **Sovrant** closing the tool gap via Phase 7.5 (worktree done, Tier 2 in progress).

### Enterprise and Multi-Tenant Readiness

None of the competitors have a multi-tenant, per-user-credential server deployment model. Claude Code has enterprise features (Teams/Enterprise plans, data residency options) but they are managed by Anthropic — the customer has no control. opencode and OpenClaude have no enterprise story at all. Sovrant's roadmap (Phases 8, 9, 9.5) is explicitly designed for this: per-request API keys, session-scoped config, per-user auth tokens, rate limiting, and usage tracking. This is Sovrant's strongest long-term differentiator.

**Sovrant advantage:** the only product with a documented path to enterprise multi-tenant deployment.

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

1. **OpenAI-compatible HTTP server** — no other open source product ships this for agentic coding. Any OpenAI API client can connect to Sovrant immediately.
2. **SmartRouter** — multi-provider routing with health/latency/cost scoring is unique in the field.
3. **Native .NET** — zero runtime dependency for .NET shops; natural fit for Windows-first or Azure-first environments.
4. **Clean legal posture** — wholly owned, no derived code, no IP risk.
5. **Enterprise roadmap** — per-user credentials, session TTL, rate limiting, team orchestration, MCP OAuth are not on any competitor's roadmap.

### Where Sovrant needs to close the gap

| Gap | Competitor ahead | Sovrant phase |
|---|---|---|
| Tool count (~40) | Claude Code / OpenClaude | Phase 7.5 (27/~40 done) |
| LSP integration | opencode | Phase 7.5 Tier 3 |
| IDE extension | Claude Code, opencode | Via MCP server mode (Phase 14) |
| Context auto-compaction | All three | Future |
| Git worktree isolation | Claude Code, opencode | ✅ Done (Phase 7.5 Tier 1) |
| Multi-agent teams | Claude Code | Phase 18 |
| Community scale | opencode | Requires public launch + OSS strategy |
| Licensing published | opencode (MIT) | Immediate action item |

### Recommended immediate actions

1. **Publish a licence** — the absence of a public licence is the first thing an evaluator notices. Even a source-available licence is better than silence.
2. **Complete Phase 7.5 Tier 2** — `EnterPlanMode`/`ExitPlanMode` and `EnterWorktree`/`ExitWorktree` are done (Tier 1 ✅). Next: /undo/redo, SkillTool, custom project slash commands, `ListMcpResources`/`ReadMcpResource`, `ToolSearch`.
3. **Phase 8 + Phase 9** — per-request credentials and session TTL/lock are the prerequisites for anything beyond a single-user deployment. These unlock the multi-user web frontend use case immediately.
4. **Phase 9.5** — per-user auth tokens and session-scoped config are what separate a demo from a product.

---

## Features to Consider Adding to the Sovrant Roadmap

The following features exist in one or more competitors but are not yet on Sovrant's roadmap. Each entry notes which competitor has it, what value it delivers, and a suggested roadmap phase.

---

### Tier 1 — High Value, Relatively Straightforward

#### Context Auto-Compaction
**Has it:** Claude Code ✅ · OpenClaude ✅ (inherited) · opencode ✅
**What it does:** When the conversation history approaches the model's context window limit, the agent automatically summarises older turns into a compact representation and replaces the raw history with it. This allows arbitrarily long sessions without hitting context limits or paying for redundant tokens.
**Why it matters for Sovrant:** Long agentic sessions (large refactors, multi-file tasks) will hit the 128K–1M token limit. Without compaction the session either fails or the user must manually `/compact`. The `ISessionStore` and `_history` list in `ConversationRuntime` are the right insertion points.
**Suggested phase:** Phase 9 or a new Phase 7.6 — depends only on the runtime and session store, no external dependencies.

---

#### Session Sharing / Export
**Has it:** opencode ✅ (`/share` command)
**What it does:** Generates a shareable read-only link (or exported file) of a session — useful for sharing debugging sessions with teammates, creating reproducible bug reports, or archiving completed tasks.
**Why it matters for Sovrant:** The JSONL session format already stores everything needed. A `GET /v1/sessions/{id}/export` endpoint returning rendered markdown or HTML would be a low-effort feature with high perceived value for teams.
**Suggested phase:** Phase 9.5 or Phase 13 (Frontend SDK).

---

#### `/undo` / `/redo` (Git-backed)
**Has it:** opencode ✅
**What it does:** Every file write/edit the agent makes is committed to a git stash or a temporary branch. `/undo` reverts the last agent action; `/redo` reapplies it. This is fundamentally safer than the current model where tool writes are permanent.
**Why it matters for Sovrant:** Trust is the biggest barrier to giving an agent write permissions. Git-backed undo dramatically lowers the risk of an agent making a mistake — the user can always roll back. `EnterWorktree`/`ExitWorktree` (Phase 7.5) is related but separate — undo/redo applies even outside a worktree.
**Suggested phase:** Phase 7.5 Tier 2 or a standalone Phase 7.6.

---

#### Non-Interactive / Headless Prompt Mode
**Has it:** opencode ✅ · Claude Code ✅ (`--print` flag)
**What it does:** Run a single prompt non-interactively from a shell script or CI pipeline and exit. No REPL, no interactive prompts. Output goes to stdout.
**Why it matters for Sovrant:** The CLI already supports `sovrant prompt "..."` (one-shot mode) which covers this. Confirm it works fully non-interactively including tool execution and verify it exits with a non-zero code on errors — then it is CI-ready.
**Suggested phase:** Already partially implemented — verify and document.

---

#### Custom Slash Commands per Project
**Has it:** opencode ✅ (`.opencode/command/` directory with named arguments)
**What it does:** Project-specific slash commands defined in a directory. Each command is a markdown/text file whose content is injected as a prompt when invoked. Supports named arguments (e.g., `/deploy staging`).
**Why it matters for Sovrant:** This is the lightweight version of `SkillTool` (already on the Phase 7.5 roadmap). The difference is that custom commands are per-project (checked into the repo) rather than global skills. Both should exist.
**Suggested phase:** Part of Phase 7.5 `SkillTool` work — extend to support project-local `.sovrant/commands/` directory.

---

### Tier 2 — Significant Value, More Complex

#### LSP Integration (Language Server Protocol)
**Has it:** opencode ✅ (20+ language servers, diagnostics, go-to-definition, symbol search)
**What it does:** Instead of relying solely on text-based grep/glob for code understanding, the agent launches a real language server (e.g., `clangd`, `pyright`, `typescript-language-server`, `omnisharp`) and queries it for semantically accurate information: hover types, call hierarchies, find-all-references, rename symbol, diagnostics. This makes refactoring and bug-fixing significantly more accurate.
**Why it matters for Sovrant:** Text manipulation tools (Grep, Glob, Read) are sufficient for simple tasks but miss semantic relationships — type errors, unused imports, interface mismatches. LSP gives the agent code intelligence rather than just file I/O.
**Suggested phase:** Phase 10 — LSP Integration. Implement `ILspClient` that spawns a language server process, communicates over stdio/JSON-RPC, and exposes tool wrappers (`LspHover`, `LspDefinition`, `LspReferences`, `LspDiagnostics`, `LspRename`).

---

#### IDE Extension (VS Code / JetBrains)
**Has it:** Claude Code ✅ (VS Code + JetBrains) · opencode ✅ (VS Code beta)
**What it does:** Embeds the agent directly into the IDE — sidebar panel, inline diff view, permission dialogs with file highlighting, tool output rendered in context.
**Why it matters for Sovrant:** The HTTP server (`Sovrant.Server`) is the foundation — an IDE extension is essentially a frontend that connects to it. Once Phase 14 (MCP server mode) ships, Sovrant can be consumed by any MCP-aware IDE (VS Code with GitHub Copilot, Cursor, Windsurf) without a bespoke extension. The extension is the layer on top for richer UX.
**Suggested phase:** Phase 15 — VS Code Extension backed by `Sovrant.Server`. Phase 14 (MCP server mode) is the prerequisite.

---

#### Context Window Visualisation
**Has it:** opencode ✅ (configurable via `ctx_viz` command)
**What it does:** Shows the user how much of the context window is currently used — token count, percentage remaining, which messages are taking the most space. Helps users understand when compaction will trigger and why responses may degrade.
**Why it matters for Sovrant:** Already tracked as a known issue (token counts always `0`). Fixing the token count capture (OpenAI final SSE chunk `usage` field) is the prerequisite. Once token counts are accurate, exposing them in the REPL and via `GET /v1/sessions/{id}` is low effort.
**Suggested phase:** Part of Phase 9.5 (usage tracking fix is a dependency of that phase anyway).

---

#### CI / CD Pipeline Integration
**Has it:** Claude Code ✅ (GitHub Actions + GitLab CI — monitors PR status, fixes CI failures autonomously)
**What it does:** The agent runs inside a GitHub Actions or GitLab CI workflow. It monitors pipeline status, reads test failure logs, makes code fixes, commits, and re-runs CI until green — without human intervention.
**Why it matters for Sovrant:** This is a high-value enterprise use case — "fix the broken build" automation. `Sovrant.Server` already provides the HTTP API needed to trigger an agent run from a CI step. A GitHub Actions runner that calls `POST /v1/chat/completions` with the failing test log as the prompt is a thin integration.
**Suggested phase:** Phase 11 — CI/CD Pipeline Integration. Publish a GitHub Actions action (`sovrant-agent-action`) that invokes `Sovrant.Server` with pipeline context. Lightweight — mostly documentation and a thin YAML action wrapper.

---

#### Multi-File Diff View / Structured Edit Preview
**Has it:** Claude Code ✅ · opencode ✅ (StructuredDiff component, colour diff)
**What it does:** Before applying file edits, the agent shows a structured diff (unified diff or side-by-side) of the proposed changes. The user can approve, reject, or edit individual hunks.
**Why it matters for Sovrant:** The CLI currently shows raw edit output. A proper diff view in the REPL (using Spectre.Console's markup or a colour diff) would make the permission dialog for `Edit`/`Write` far more informative and trustworthy.
**Suggested phase:** Phase 13 (Frontend SDK) — implement in the CLI REPL using Spectre.Console colour rendering.

---

#### Agent Memory / CLAUDE.md Equivalent
**Has it:** Claude Code ✅ (`CLAUDE.md` project memory files, `~/.claude/CLAUDE.md` global memory)
**What it does:** The agent reads markdown memory files from the project root and from the user's home directory at the start of each session. These files contain persistent instructions, project conventions, preferred patterns, and context that persists without the user having to re-explain on every session start.
**Why it matters for Sovrant:** This is the most direct path to making Sovrant feel "smart" about a codebase — the agent automatically knows the project's coding style, which files to avoid, which commands to run, etc. Implementation: read `.sovrant/memory.md` and `~/.sovrant/memory.md` at session initialisation and prepend their contents to the system prompt.
**Suggested phase:** Phase 7.6 — trivial to implement (file read + system prompt injection), very high perceived value.

---

#### Voice Mode
**Has it:** Claude Code ✅ (added 2026)
**What it does:** Speech-to-text input and text-to-speech output for the agent loop. Primarily useful for hands-free operation during coding sessions.
**Why it matters for Sovrant:** Low priority for a coding tool — keyboard-first workflows dominate. Worth tracking as a future differentiator but not a near-term priority.
**Suggested phase:** Future / Phase 20+.

---

#### Slack / Webhook Integration
**Has it:** Claude Code ✅ (OAuth-based Slack app)
**What it does:** Invoke the agent from a Slack message, receive streamed responses in a Slack thread. Useful for team-based "ask the codebase" workflows.
**Why it matters for Sovrant:** `Sovrant.Server`'s HTTP API makes this a thin integration — a Slack bot that forwards messages to `POST /v1/chat/completions` and streams the response back. The prerequisite is Phase 9.5 (per-user auth tokens) so each Slack user maps to an isolated session.
**Suggested phase:** Phase 12 — Slack Integration. Publish a Sovrant Slack app that connects to a self-hosted `Sovrant.Server`.

---

### Tier 3 — Speculative / Long-Term

| Feature | Has it | Notes |
|---|---|---|
| Background daemon (file watching, idle memory consolidation) | Claude Code (KAIROS, unreleased) | Long-term; `IHostedService` is the right .NET pattern |
| Swarm / parallel worker agents at scale | Claude Code (behind feature flag) | Phase 18 covers the foundation |
| GitHub PR monitoring + autonomous fix loop | Claude Code | Phase 11 CI work is the prerequisite |
| Tauri desktop app | opencode | After Phase 13 frontend SDK — wrap in Tauri |
| Mobile client | Claude Code (partial) | Very long-term |
| Fine-tuning pipeline for model specialisation | None yet | Research phase |

---

## Updated Sovrant Feature Gap Summary

| Feature | Priority | Suggested Phase |
|---|---|---|
| Context auto-compaction | High | 7.6 |
| Agent memory files (`.sovrant/memory.md`) | High | 7.6 |
| `/undo` / `/redo` git-backed | High | 7.5 Tier 2 |
| Custom project slash commands | Medium | 7.5 (extend SkillTool) |
| Context window / token visualisation | Medium | 9.5 (token fix prerequisite) |
| Session export / share | Medium | 9.5 / 13 (Frontend SDK) |
| Non-interactive headless mode | Low | Verify existing |
| Structured diff view in REPL | Medium | 13 (Frontend SDK) |
| LSP integration | High (long-term) | 10 |
| IDE extension (VS Code) | High (long-term) | 15 (after Phase 14 MCP) |
| CI/CD pipeline integration | Medium | 11 |
| Slack / webhook integration | Medium | 12 |
| Background daemon / file watching | Low | 20+ |
| Desktop app (Tauri) | Low | After Phase 13 |
| Voice mode | Low | 20+ |

---

## Summary Verdict

| | Claude Code | OpenClaude | opencode | Sovrant |
|---|---|---|---|---|
| **Best for individual devs** | ✅ if Anthropic user | Risky | ✅ | ✅ (.NET shops) |
| **Best for multi-user teams** | ✅ (managed) | ❌ | ❌ | ✅ (roadmap) |
| **Best for enterprise deploy** | ✅ (Anthropic SaaS) | ❌ | ❌ | ✅ (self-hosted roadmap) |
| **Best provider flexibility** | ❌ | ✅ | ✅ | ✅ + routing |
| **Best legal posture** | ✅ | ❌ | ✅ | ✅ |
| **Best .NET / Windows fit** | ❌ | ❌ | ❌ | ✅ |
| **Best open source community** | ❌ | ❌ | ✅ | Early |
