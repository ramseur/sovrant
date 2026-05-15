---
name: refactor
description: Dead code detection and safe removal with refactoring
trigger: /refactor
tools: [Read, Grep, Glob, Edit, Bash]
---

# Refactor

Identify and safely remove dead code, simplify complex patterns, and improve structure.

## Steps
1. **Analyse** — read the target code and understand its purpose
2. **Detect dead code** — find unused functions, variables, imports, and types
3. **Identify complexity** — spot overly complex logic that can be simplified
4. **Plan changes** — list proposed refactorings with rationale
5. **Execute** — make changes incrementally, running tests after each change
6. **Verify** — run full test suite to confirm no regressions

## Refactoring Checklist
- [ ] Unused imports removed
- [ ] Unused functions/methods removed
- [ ] Unused variables removed
- [ ] Duplicate code consolidated
- [ ] Complex conditionals simplified
- [ ] Long methods broken up
- [ ] Naming improved where unclear

## Rules
- Never change behaviour — only structure
- Run tests after every change
- If you're unsure whether something is used, grep before removing
- Prefer small, focused commits over one large refactor
