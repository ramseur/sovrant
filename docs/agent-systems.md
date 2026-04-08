# Sovrant — Agent Systems: Team vs Swarm

**Last updated:** 2026-04-08

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
| **Team registry & member model** | `src/Sovrant.Agents/Teams/` (~89 LOC across 4 files) |
| **Team tools (LLM-callable)** | `src/Sovrant.Tools/Team/{TeamCreateTool,TeamDelegateTool,TeamStatusTool,TeamDeleteTool}.cs` |
| **Swarm engine** | `src/Sovrant.Agents/Swarm/` (~1,376 LOC across 14 files) |
| **Swarm CLI** | `src/Sovrant.Cli/Program.cs` (`swarm` subcommand) |
| **Swarm HTTP** | `src/Sovrant.Server/Routes/SwarmRoutes.cs` (`/v1/swarm/*`) |
| **Shared infrastructure** | `SovrantAgentFactory`, `AgentTemplateRegistry`, `IMultiAgentSystem` — used by both |

`SwarmOrchestrator`'s constructor takes an `ITeamRegistry` so swarm-spawned agents *can* be registered as team members for tracking, but in normal use the two stay in their own lanes.

---

## Side-by-side comparison

| Dimension | **Team** | **Swarm** |
|---|---|---|
| **Lines of code** | ~89 LOC + 4 small tools | ~1,376 LOC across 14 files |
| **Who drives it?** | The **LLM** calls `TeamCreate`, `TeamDelegate`, `TeamStatus`, `TeamDelete` like any other tool inside its tool-use loop | The **user** runs `sovrant swarm "<task>"` from the CLI; the LLM is not in the conversation loop while a swarm runs |
| **Lifecycle** | Persistent — members live in an in-memory `ITeamRegistry` for the whole conversation; the LLM creates one and reuses it across turns | Ephemeral — one swarm = one task, lives only until completion |
| **State** | `InMemoryTeamRegistry` (a `ConcurrentDictionary`). Lost on process restart. | `SwarmStateTracker` + `SwarmSession` writing JSONL events to `~/.sovrant/swarm/sessions/{id}.jsonl`. Phase 37.5 will move this into the SQLite `swarm_events` table. |
| **Concurrency** | Sequential by construction — `TeamDelegate` is one call → one agent → one result | Wave-based parallelism. `LlmSwarmDecomposer` builds a DAG (`SwarmTaskNode` with `Dependencies`), `SwarmOrchestrator` topologically sorts it into waves, then runs each wave's tasks in parallel with a `SemaphoreSlim` concurrency cap |
| **Coordination primitives** | None — each delegation is independent | `SwarmFileLockManager` (pessimistic file locks declared up-front via `FilesToModify`), `SwarmQualityGate`, retry logic, token-budget enforcement, file-conflict resolution |
| **Task decomposition** | The caller (the LLM) decides what to delegate and when | `LlmSwarmDecomposer` (218 LOC) calls an LLM to break the user's natural-language goal into a `SwarmPlan` of `SwarmTaskNode`s with explicit dependencies and predicted file-touch sets |
| **Agent identity** | Each member has a name, role, system prompt, optional tool whitelist, optional model — created once, reused | Each task wave spawns ephemeral agents from templates (coder, reviewer, etc.); same `SovrantAgentFactory` and `AgentTemplateRegistry`, but no persistent identity |
| **Workspace scoping** | None today — registry is process-global | `SwarmOrchestrator` takes a `WorkspaceContext` in its constructor; scoping is wired but not yet flowing into the JSONL store (Phase 37.5 item #5) |
| **Trigger surface** | LLM tool calls (`TeamCreate` / `TeamDelegate` / `TeamStatus` / `TeamDelete`) | CLI `sovrant swarm` command, `Swarm` tool from inside an agent conversation, `POST /v1/swarm` HTTP endpoint |
| **Cancellation** | Implicit via `CancellationToken` | First-class — wave-by-wave checks; can stop mid-DAG |
| **Failure model** | Single delegation fails → caller decides | Per-task retry budgets, quality gate scoring, partial completion semantics, token-budget halts |
| **Observability** | `TeamStatus` tool returns last output / error per member; lost on restart | JSONL event log per swarm run, replayable via `/v1/swarm/{id}/events` and `GET /v1/swarm/sessions` |

---

## Value-add of each system

### Team's value-add
1. **Conversational delegation.** The model can say "spin up a security reviewer with these tools and ask it to look at this diff," all inside its tool-use loop. Swarm cannot do that — there is no `Swarm` tool that creates persistent specialists.
2. **Persistent specialists.** You can build a "code reviewer" once at the start of a session and call back to it 10 turns later with new context. Swarm tears everything down at the end of every run.
3. **Tiny surface area** (~90 LOC). Easy to reason about, easy to extend (e.g. add `TeamUpdate`, persist to SQLite).
4. **No DAG ceremony.** When you just want one sub-agent to look at one thing, you do not need wave scheduling or file locks.

### Swarm's value-add
1. **Real parallelism with safety.** `SwarmFileLockManager` is the differentiator. Two sub-agents asked to edit `Foo.cs` will not trample each other; one waits. Team has nothing equivalent.
2. **LLM-driven decomposition.** `LlmSwarmDecomposer` is a 218-LOC component whose entire job is "given a vague goal, produce a runnable DAG with predicted file touches." That is substantial engineering.
3. **Wave scheduling.** Topological sort + concurrency cap → measurable wall-clock improvements on tasks the decomposer can split well.
4. **Quality gate, retries, token budget.** Production-hardening that Team does not have.
5. **Replayable.** JSONL session files (today) mean you can re-construct exactly what happened. After Phase 37.5 they will be SQL-queryable, joinable to users and workspaces, and covered by the same backup story as everything else.

---

## Where the value is **less** clear

- **Massive code-to-value ratio difference.** Swarm is ~15× the LOC for capabilities most users probably never trigger (DAG decomposition, file locking, wave scheduling). Team is tiny but heavily used by the LLM in normal conversation.
- **Surface overlap.** Both spawn sub-agents from the same factory + templates. From the user's perspective, "ask an agent to do X" has two completely different code paths and two completely different observability stories. That is confusion debt.
- **Decomposition tax.** Swarm's killer feature (auto-decomposition) costs an LLM call up front. For tasks the user could decompose themselves, the swarm overhead is pure loss.
- **No persistence on Team.** Team's "co-workers" evaporate on restart. For a system that just shipped per-user identity and workspaces (Phases 35–37), that is an obvious gap.
- **No bridge between the two.** The LLM cannot launch a swarm directly via a tool that returns the resulting team of specialists. The swarm cannot use a long-lived team member as one of its workers (well — it can in theory, since `SwarmOrchestrator` takes `ITeamRegistry`, but no wiring resolves named team members as swarm workers today). They are two stovepipes that share infrastructure but not surface.

---

## Honest take

**Team is the more general primitive and the one your LLM actually uses.** It is also the one that needs the least work to make great. Adding SQLite persistence + per-workspace scoping + a "reuse a template" shortcut would close the biggest gaps in maybe 200 LOC.

**Swarm is a much bigger investment whose value depends entirely on how often the decomposer produces a good plan.** The file-lock mechanism, the wave scheduler, and the quality gate are real engineering, but they only pay off on tasks where (a) the decomposition is accurate and (b) parallelism actually saves time vs the LLM-decomposition cost up front. For a single-user dev workstation, those wins are narrower than they look.

---

## A possible future: unify them

If you wanted to consolidate, the natural path would be:

1. **Promote Team to first-class citizen**
   - SQLite-backed `ITeamRegistry` — persists across restarts and workspace boundaries
   - Workspace-scoped membership — `team_members` table linked to `workspaces.workspace_id`
   - LLM still creates them via `TeamCreate`; users can also pre-create personas via the server API

2. **Expose Swarm as one orchestration mode of Team**
   - `SwarmPlan` becomes "here is a team of N agents, here is a DAG of work, run it under the locking scheduler"
   - The decomposer becomes optional — callers who already know their DAG skip it
   - The CLI `sovrant swarm` command stays, but now it operates on whatever team the user has set up rather than spinning up ephemeral workers

3. **Single observability store**
   - Both systems write to the same `agent_runs` / `agent_events` tables (replacing JSONL and the in-memory registry)
   - Per-user, per-workspace, per-project queries work uniformly

This would let you keep Swarm's hard-won machinery (file locks, decomposer, quality gate) without maintaining two parallel concepts on top of the same agent factory.

This is now tracked as **[Phase 52 — Unified Agent Orchestration](roadmap.md#phase-52--unified-agent-orchestration-one-team-or-swarm-abstraction-in-the-database)** in the roadmap. Phase 37.5 (Swarm Sessions Into the Database) shipped the prerequisite — swarm events live in SQLite, so the rest of the agent state (team members, agent runs, conversation links) can join them in the same store under the same backup, query, and scoping story. Phase 52 adds three explicit creation modes so a swarm can run against (1) one pre-existing team, (2) multiple composed teams, or (3) the engine's own decomposition (current behavior), with all three going through the same orchestrator and persisting to the same tables.

---

## Cross-references

- [`README.md` § Agent System](../README.md#agent-system) — user-facing overview
- [`README.md` § Team vs Swarm — When to Use Which](../README.md#team-vs-swarm--when-to-use-which) — short-form comparison
- [`README.md` § Swarm Orchestrator](../README.md#swarm-orchestrator) — swarm execution details, locking, API
- [`docs/roadmap.md` § Phase 37.5](roadmap.md#phase-375--swarm-sessions-into-the-database) — moving swarm state into the DB (prerequisite, shipped)
- [`docs/roadmap.md` § Phase 52](roadmap.md#phase-52--unified-agent-orchestration-one-team-or-swarm-abstraction-in-the-database) — the unification phase itself
- [`docs/persistence.md`](persistence.md) — where agent state lives today and where it is going
