---
name: project-flow
description: Issue triage, cross-system coordination, and project tracking
trigger: /flow
tools: [Bash, Read, Write, WebFetch]
---

# Project Flow

Coordinate project work across GitHub, issue trackers, and local codebase.

## Steps
1. **Gather context** — read open issues, PRs, and recent commits
2. **Triage** — categorise issues by priority and type:
   - **P0** — blocking, needs immediate fix
   - **P1** — important, schedule this sprint
   - **P2** — nice to have, backlog
   - **Bug/Feature/Chore** — type classification
3. **Check consistency** — verify issue descriptions match code state
4. **Update** — suggest label/priority changes, close stale issues
5. **Report** — project status summary

## Output Format
- **Active work** — in-progress issues/PRs with status
- **Triage results** — newly categorised issues
- **Stale items** — issues/PRs that need attention or closing
- **Blockers** — anything preventing progress
