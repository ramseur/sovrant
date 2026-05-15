---
name: code-review
description: Multi-severity code review with structured findings
trigger: /review
agents: [code-reviewer]
tools: [Read, Grep, Glob, Bash]
---

# Code Review

Systematic code review with severity-ranked findings.

## Steps
1. **Understand intent** — read PR description, commit messages, and related tests
2. **Scan for issues** — review changes across all severity levels
3. **Logic review** — trace through critical paths, check edge cases
4. **Security scan** — look for injection, auth bypass, data exposure
5. **Performance** — check for N+1 queries, unnecessary allocations, blocking I/O
6. **Style** — naming, consistency, idiomatic usage
7. **Report** — structured findings with file:line references

## Severity Levels
- **CRITICAL** — data loss, security breach, crash risk. Must fix before merge.
- **HIGH** — correctness bug or significant performance issue.
- **MEDIUM** — non-idiomatic, error-prone, or hard to maintain.
- **LOW** — style, naming, or minor improvement.

## Output Format
```
[SEVERITY] file:line — description
  Suggestion: how to fix
```

## Rules
- Only report findings with >= 70% confidence
- Mark uncertain findings with [UNCERTAIN]
- Praise good patterns — don't only report problems
- Focus on the diff, not pre-existing issues (unless they interact with the change)
