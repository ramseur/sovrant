---
name: verification-loop
description: 6-phase quality gate pipeline (Phase 22)
trigger: /verify
tools: [Bash, Read, Grep, Glob, Verify]
---

# Verification Loop

Run the structured 6-phase quality verification pipeline.

## Steps
1. **Build** — compile the project, zero errors required
2. **Type Check** — language-specific type checking (if applicable)
3. **Lint** — formatting and style check
4. **Test** — run test suite with coverage collection
5. **Security Scan** — check for vulnerable dependencies
6. **Diff Review** — scan git diff for debug code, secrets, unintended files

## Usage
Invoke the `Verify` tool with the project path, or run manually:
- For .NET: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test`
- For Node: `npm run build`, `npx tsc --noEmit`, `npx eslint .`, `npm test`

## Rules
- All 6 phases must pass before merge
- If a phase fails, fix the issue and re-run from that phase
- Coverage threshold is configurable in `.sovrant/verify.json`
- Do not skip phases without explicit user approval
