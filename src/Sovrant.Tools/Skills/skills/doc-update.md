---
name: doc-update
description: Documentation maintenance synced with code changes
trigger: /docs
tools: [Read, Grep, Glob, Write, Edit]
---

# Documentation Update

Keep documentation in sync with code changes.

## Steps
1. **Detect changes** — identify what code changed (git diff, recent commits)
2. **Find affected docs** — locate documentation that references changed code
3. **Audit accuracy** — verify each doc section against current code
4. **Update** — fix outdated references, examples, and descriptions
5. **Check links** — verify internal links and cross-references still work
6. **Add missing docs** — document new features or APIs that lack documentation

## Documentation Types
- **README** — project overview, setup, usage
- **API docs** — endpoint/function reference
- **Architecture docs** — system design, data flow
- **Guides** — how-to and tutorial content
- **Changelogs** — version history

## Rules
- Match the existing documentation style and format
- Update examples to use current API signatures
- Don't add documentation for internal/private implementation details
- Flag docs that reference removed features for deletion
