---
name: tdd-workflow
description: Red-Green-Refactor with coverage enforcement
trigger: /tdd
agents: [coder, test-writer]
tools: [Bash, Read, Edit, Write, Grep, Glob]
---

# TDD Workflow

Strict Test-Driven Development: write tests first, then make them pass.

## Steps
1. **Red** — write a failing test that captures the requirement
2. **Verify red** — run the test suite, confirm the new test fails
3. **Green** — write the minimal code to make the test pass
4. **Verify green** — run the test suite, confirm all tests pass
5. **Refactor** — improve the implementation while keeping tests green
6. **Verify green again** — run tests after refactoring
7. **Coverage check** — verify coverage meets threshold (default 60%)

## Rules
- NEVER write implementation before the test exists
- Each cycle should be small — one behaviour per test
- If you're tempted to write more code than the test requires, stop and write another test
- Refactoring means changing structure without changing behaviour — tests must stay green
- If coverage drops below threshold, write more tests before continuing

## Anti-Patterns to Avoid
- Writing tests after implementation (that's not TDD)
- Testing implementation details instead of behaviour
- Skipping the refactor step
- Writing tests that always pass regardless of implementation
