---
name: architecture-decision
description: Structured Architecture Decision Record creation
trigger: /adr
agents: [architect]
tools: [Read, Grep, Glob, Write]
---

# Architecture Decision Record

Create a structured ADR following the standard template.

## Steps
1. **Identify decision** — what architectural question needs answering
2. **Gather context** — read relevant code, prior ADRs, and constraints
3. **Enumerate options** — list 2-4 viable approaches
4. **Evaluate** — assess each option against criteria:
   - Complexity, maintainability, performance, security, team familiarity
5. **Recommend** — state the decision with full reasoning
6. **Write ADR** — output in standard format

## ADR Template
```markdown
# ADR-NNN: [Title]

## Status
Proposed | Accepted | Deprecated | Superseded by ADR-XXX

## Context
[What is the issue? What forces are at play?]

## Decision
[What is the change being proposed/decided?]

## Options Considered
1. **Option A** — [description, pros, cons]
2. **Option B** — [description, pros, cons]

## Consequences
- **Positive:** [benefits]
- **Negative:** [trade-offs]
- **Risks:** [what could go wrong]
```

## Rules
- Every ADR must have at least 2 options considered
- State consequences honestly — every decision has trade-offs
- Link to related ADRs if they exist
