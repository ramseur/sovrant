---
name: codebase-onboard
description: New contributor onboarding with architecture walkthrough
trigger: /onboard
tools: [Read, Grep, Glob, Bash, ListDirectory]
---

# Codebase Onboarding

Generate a comprehensive onboarding guide for a new contributor to this codebase.

## Steps
1. **Project overview** — read README, config files, and project structure
2. **Architecture map** — identify major components, their responsibilities, and relationships
3. **Tech stack** — list languages, frameworks, key dependencies, and versions
4. **Setup guide** — document how to build, test, and run the project
5. **Key patterns** — identify recurring design patterns and conventions
6. **Entry points** — where to start reading code, key files, main flows
7. **Gotchas** — non-obvious setup steps, common pitfalls, environment requirements

## Output Format
# Onboarding Guide: [Project Name]

## Quick Start
[How to get running in < 5 minutes]

## Architecture
[Component diagram and responsibilities]

## Key Patterns
[Design patterns and conventions used]

## Where to Start
[Recommended reading order for new contributors]

## Common Gotchas
[Things that trip people up]

## Rules
- Focus on what a new contributor needs to know, not everything
- Verify setup instructions actually work by checking the build system
- Highlight non-obvious conventions that aren't documented elsewhere
