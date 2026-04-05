---
name: plan
description: Structured planning with phased implementation and dependency mapping
trigger: /plan
agents: [architect]
tools: [Read, Grep, Glob, Write]
---

# Structured Planning

Create a detailed implementation plan with phases, dependencies, and risk assessment.

## Steps
1. **Understand goal** — clarify what needs to be built/changed and why
2. **Analyse current state** — read relevant code, docs, and configuration
3. **Identify components** — break the work into discrete, testable units
4. **Map dependencies** — which components block others
5. **Phase the work** — group into sequential phases with clear milestones
6. **Assess risks** — what could go wrong, what's uncertain
7. **Present plan** — structured output for user review before execution

## Output Format
### Phase N: [Name]
- **Goal:** what this phase achieves
- **Components:** list of changes
- **Dependencies:** what must be done first
- **Acceptance criteria:** how to verify it's done
- **Estimated complexity:** Low/Medium/High
- **Risks:** what could go wrong

## Rules
- Each phase must be independently testable
- Dependencies must be explicit — no implicit ordering
- Present the plan and wait for approval before implementing
