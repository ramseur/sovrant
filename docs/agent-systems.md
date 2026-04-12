# Sovrant — Agent Systems: Team vs Swarm

**Last updated:** 2026-04-12

Sovrant ships **two distinct multi-agent systems** that share the same agent factory (`SovrantAgentFactory`) and template registry (`AgentTemplateRegistry`) underneath but solve very different problems and live at very different levels of the stack.

This document explains what each one is, where the value comes from, where the overlap is uncomfortable, and how they might converge in the future.

> **TL;DR**
> - **Team** is "the LLM has co-workers it can hire and fire mid-conversation." Tiny code surface (~90 LOC), heavily used by the model in normal turns, no parallelism, no observability after the conversation ends.
> - **Swarm** is "the user has a build system for one big task — break it up, schedule it, run it in parallel, gate the result." Large code surface (~1,376 LOC), CLI-only, real parallelism with file-lock coordination, replayable session logs.
> - They are not exclusive: a swarm plan can name a `TeamId` and pull its workers from the team registry instead of from templates.

---

## Where they live

| Concept | Code path |
|---|---|
| **Team registry & member model** | `src/Sovrant.Agents/Teams/` (~388 LOC across 6 files, including `SqliteTeamRegistry`) |
| **Team tools (LLM-callable)** | `src/Sovrant.Tools/Team/{TeamCreateTool,TeamDelegateTool,TeamStatusTool,TeamDeleteTool,TeamRunTool,TeamPublishTool}.cs` |
| **Team HTTP** | `src/Sovrant.Server/Routes/TeamRoutes.cs` (`/v1/teams/*`, `/v1/runs/*`) |
| **Swarm engine** | `src/Sovrant.Agents/Swarm/` (~1,493 LOC across 15 files) |
| **Swarm CLI** | `src/Sovrant.Cli/Program.cs` (`swarm` subcommand) |
| **Swarm HTTP** | `src/Sovrant.Server/Routes/SwarmRoutes.cs` (`/v1/swarm/*`) |
| **Shared infrastructure** | `SovrantAgentFactory`, `AgentTemplateRegistry`, `IMultiAgentSystem` — used by both |

`SwarmOrchestrator`'s constructor takes an `ITeamRegistry` so swarm-spawned agents *can* be registered as team members for tracking, but in normal use the two stay in their own lanes.

---

## Side-by-side comparison

| Dimension | **Team** | **Swarm** |
|---|---|---|
| **Lines of code** | ~388 LOC + 6 tools | ~1,493 LOC across 15 files |
| **Who drives it?** | The **LLM** calls `TeamCreate`, `TeamDelegate`, `TeamStatus`, `TeamDelete`, `TeamRun`, `TeamPublish` like any other tool inside its tool-use loop. Users can also manage teams via `POST/GET/DELETE /v1/teams/*` HTTP endpoints. | The **user** runs `sovrant swarm "<task>"` from the CLI; the LLM is not in the conversation loop while a swarm runs |
| **Lifecycle** | Persistent — members live in `SqliteTeamRegistry` (Phase 52), surviving process restarts. The LLM creates one and reuses it across turns and sessions. | Ephemeral — one swarm = one task, lives only until completion |
| **State** | `SqliteTeamRegistry` backed by the `teams` and `team_members` tables (Phase 52). Persists across restarts with full workspace/project scoping. `InMemoryTeamRegistry` is still available as a fallback. | `SwarmStateTracker` + `SwarmSession` writing events to the SQLite `swarm_events` table (shipped in Phase 37.5). Legacy JSONL can be imported via `sovrant db import-swarm`. |
| **Concurrency** | Sequential by construction — `TeamDelegate` is one call → one agent → one result | Wave-based parallelism. `LlmSwarmDecomposer` builds a DAG (`SwarmTaskNode` with `Dependencies`), `SwarmOrchestrator` topologically sorts it into waves, then runs each wave's tasks in parallel with a `SemaphoreSlim` concurrency cap |
| **Coordination primitives** | None — each delegation is independent | `SwarmFileLockManager` (pessimistic file locks declared up-front via `FilesToModify`), `SwarmQualityGate`, retry logic, token-budget enforcement, file-conflict resolution |
| **Task decomposition** | The caller (the LLM) decides what to delegate and when | `LlmSwarmDecomposer` (218 LOC) calls an LLM to break the user's natural-language goal into a `SwarmPlan` of `SwarmTaskNode`s with explicit dependencies and predicted file-touch sets |
| **Agent identity** | Each member has a name, role, system prompt, optional tool whitelist, optional model — created once, reused | Each task wave spawns ephemeral agents from templates (coder, reviewer, etc.); same `SovrantAgentFactory` and `AgentTemplateRegistry`, but no persistent identity |
| **Workspace scoping** | Full workspace/project scoping via `SqliteTeamRegistry` (Phase 52). `teams` and `team_members` tables carry `workspace_id` and `project_id`. HTTP endpoints filter by workspace. | `SwarmOrchestrator` takes a `WorkspaceContext` in its constructor; scoping flows into the `swarm_events` table via `WorkspaceContextMiddleware` |
| **Trigger surface** | LLM tool calls (`TeamCreate` / `TeamDelegate` / `TeamStatus` / `TeamDelete` / `TeamRun` / `TeamPublish`), HTTP REST API (`POST/GET/DELETE /v1/teams/*`, `POST /v1/teams/{id}/runs`, `GET /v1/runs/*`) | CLI `sovrant swarm` command, `Swarm` tool from inside an agent conversation, `POST /v1/swarm` HTTP endpoint |
| **Cancellation** | Implicit via `CancellationToken` | First-class — wave-by-wave checks; can stop mid-DAG |
| **Failure model** | Single delegation fails → caller decides | Per-task retry budgets, quality gate scoring, partial completion semantics, token-budget halts |
| **Observability** | `TeamStatus` tool returns last output / error per member. `agent_runs` table (Phase 52) tracks all delegations with token counts and status. HTTP: `GET /v1/runs`, `GET /v1/runs/{id}`. | SQLite event log per swarm run (in `swarm_events` table), replayable via `/v1/swarm/{id}/events` and `GET /v1/swarm/sessions` |

---

## Value-add of each system

### Team's value-add
1. **Conversational delegation.** The model can say "spin up a security reviewer with these tools and ask it to look at this diff," all inside its tool-use loop. Swarm cannot do that — there is no `Swarm` tool that creates persistent specialists.
2. **Persistent specialists.** You can build a "code reviewer" once at the start of a session and call back to it 10 turns later with new context. Swarm tears everything down at the end of every run.
3. **Full HTTP API** (Phase 52). Teams are first-class HTTP citizens — `POST/GET/DELETE /v1/teams/*`, member management, and `POST /v1/teams/{id}/runs` for starting multi-agent runs. `TeamPublish` lets swarm workers be published as reusable team members.
4. **No DAG ceremony.** When you just want one sub-agent to look at one thing, you do not need wave scheduling or file locks.

### Swarm's value-add
1. **Real parallelism with safety.** `SwarmFileLockManager` is the differentiator. Two sub-agents asked to edit `Foo.cs` will not trample each other; one waits. Team has nothing equivalent.
2. **LLM-driven decomposition.** `LlmSwarmDecomposer` is a 218-LOC component whose entire job is "given a vague goal, produce a runnable DAG with predicted file touches." That is substantial engineering.
3. **Wave scheduling.** Topological sort + concurrency cap → measurable wall-clock improvements on tasks the decomposer can split well.
4. **Quality gate, retries, token budget.** Production-hardening that Team does not have.
5. **Replayable.** SQLite `swarm_events` table (shipped in Phase 37.5) makes every swarm run SQL-queryable, joinable to users and workspaces, and covered by the same backup story as everything else.

---

## Where the value is **less** clear

- **Massive code-to-value ratio difference.** Swarm is ~15× the LOC for capabilities most users probably never trigger (DAG decomposition, file locking, wave scheduling). Team is tiny but heavily used by the LLM in normal conversation.
- **Surface overlap.** Both spawn sub-agents from the same factory + templates. From the user's perspective, "ask an agent to do X" has two completely different code paths and two completely different observability stories. That is confusion debt.
- **Decomposition tax.** Swarm's killer feature (auto-decomposition) costs an LLM call up front. For tasks the user could decompose themselves, the swarm overhead is pure loss.
- **~~No persistence on Team.~~** ✓ Resolved in Phase 52. Teams now persist to SQLite via `SqliteTeamRegistry` with full workspace/project scoping.
- **Bridge between the two is partial.** `TeamPublish` lets swarm workers be published as reusable team members, and `TeamRun` can run an existing team with optional parallelism/locking. The swarm can use long-lived team members as workers since `SwarmOrchestrator` takes `ITeamRegistry`. Full composition (a swarm that transparently uses pre-existing teams as its worker pool) is still being refined.

---

## Honest take

**Team is the more general primitive and the one your LLM actually uses.** Phase 52 shipped the missing pieces — SQLite persistence via `SqliteTeamRegistry`, full workspace/project scoping, `TeamRun` for parallelism, and `TeamPublish` to bridge swarm workers into reusable teams. The ~388 LOC investment is modest relative to the value.

**Swarm is a much bigger investment whose value depends entirely on how often the decomposer produces a good plan.** The file-lock mechanism, the wave scheduler, and the quality gate are real engineering, but they only pay off on tasks where (a) the decomposition is accurate and (b) parallelism actually saves time vs the LLM-decomposition cost up front. For a single-user dev workstation, those wins are narrower than they look.

---

## Unification status (Phase 52 — shipped)

Phase 52 unified the two systems. The path followed was:

1. **✓ Team promoted to first-class citizen**
   - `SqliteTeamRegistry` persists teams to the `teams` and `team_members` SQLite tables across restarts
   - Workspace-scoped membership — `team_members` table carries `workspace_id` and `project_id`
   - LLM creates teams via `TeamCreate`; users can also manage teams via the HTTP REST API (`POST/GET/DELETE /v1/teams/*`)

2. **✓ Swarm exposed as one orchestration mode of Team**
   - `TeamRunTool` runs an existing team with optional parallelism and file-locking
   - `TeamPublishTool` converts ephemeral swarm workers into reusable team members
   - Three creation modes: (1) pre-existing team, (2) multiple composed teams, (3) engine's own decomposition (swarm default)
   - The CLI `sovrant swarm` command stays; swarms can now operate on whatever team the user has set up

3. **✓ Single observability store**
   - Both systems write to the `agent_runs` table (unified run ledger for delegations, swarm tasks, and mission steps)
   - `swarm_events` extended with `kind` (discriminator) and `run_id` (link to `agent_runs`)
   - Per-user, per-workspace, per-project queries work uniformly via `GET /v1/runs`

This keeps Swarm's hard-won machinery (file locks, decomposer, quality gate) without maintaining two parallel concepts on top of the same agent factory.

---

## Cross-references

- [`README.md` § Agent System](../README.md#agent-system) — user-facing overview
- [`README.md` § Team vs Swarm — When to Use Which](../README.md#team-vs-swarm--when-to-use-which) — short-form comparison
- [`README.md` § Swarm Orchestrator](../README.md#swarm-orchestrator) — swarm execution details, locking, API
- [`docs/roadmap.md` § Phase 37.5](roadmap.md#phase-375--swarm-sessions-into-the-database) — moving swarm state into the DB (prerequisite, shipped)
- [`docs/roadmap.md` § Phase 52](roadmap.md#phase-52--unified-agent-orchestration-one-team-or-swarm-abstraction-in-the-database) — the unification phase itself
- [`docs/persistence.md`](persistence.md) — where agent state lives today and where it is going
