# Contributing to Sovrant

Thank you for your interest in contributing. This document covers how to get started, the branching model, coding standards, and the legal terms you agree to when submitting a contribution.

---

## License

Sovrant is source-available under the **Business Source License 1.1 (BSL 1.1)**. Contributions you submit are licensed under the same terms. See [LICENSE](LICENSE) and [LICENSING.md](LICENSING.md) for details.

By submitting a pull request you confirm that:

1. You authored the contribution or have the right to submit it.
2. You grant Anant Corporation the right to use, modify, and distribute your contribution under the BSL 1.1 and any future license (including the Apache 2.0 conversion on 2029-05-15).
3. Your contribution does not incorporate third-party code that is incompatible with BSL 1.1.

> **Important:** Do not submit code derived from Anthropic's Claude Code, OpenClaude, or any other source under a contested or proprietary licence. Sovrant is a clean-room implementation and must remain so.

---

## Getting Started

**Prerequisites:**
- .NET 10 SDK (`dotnet --version` should show `10.x`)
- Node.js 20+ (for TypeScript SDK work only)
- Git

**Build and test:**
```bash
git clone https://github.com/ramseur/sovrant.git
cd sovrant
dotnet build Sovrant.slnx
dotnet test Sovrant.slnx
```

**TypeScript SDK:**
```bash
cd sdk/js
npm install
npm run build
npm test
```

---

## Branching Model

| Branch | Purpose |
|---|---|
| `main` | Stable releases — protected, no direct pushes |
| `development` | Active development — target for all PRs |
| `feature/*` | Feature branches — branch from `development`, PR back to `development` |

All pull requests must target **`development`**, not `main`. Merges to `main` are done by maintainers as versioned releases.

---

## Submitting a Pull Request

1. Fork the repo and create a branch from `development`.
2. Make your changes. Keep commits focused — one logical change per commit.
3. Run the full test suite: `dotnet test Sovrant.slnx` — all tests must pass, 0 failures.
4. Run the build with warnings as errors: `dotnet build Sovrant.slnx` — 0 warnings.
5. Open a PR against `development` with a clear description of what changed and why.

**PR checklist:**
- [ ] Tests pass (`dotnet test`)
- [ ] No new warnings (`dotnet build`)
- [ ] No secrets, API keys, or credentials in any tracked file
- [ ] No code derived from Anthropic, OpenClaude, or any contested source
- [ ] Description explains the motivation, not just the mechanics

---

## Coding Standards

- **Language:** C# 14 / .NET 10. Use the latest language features where they improve clarity.
- **Nullable:** All projects run with `<Nullable>enable</Nullable>`. Suppress warnings with `!` only where you are certain the value cannot be null and can explain why.
- **Warnings as errors:** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is set globally. Fix the warning, do not suppress it without a documented reason.
- **Analysis:** `<AnalysisMode>All</AnalysisMode>` is enabled. New analyzers may flag existing code — fix rather than suppress.
- **Comments:** Only add a comment when the *why* is non-obvious — a hidden constraint, a known workaround, a subtle invariant. Do not comment *what* the code does.
- **Tests:** New behaviour requires tests. Bug fixes should include a regression test. Test projects live under `tests/`.

**Database calls (see code-review.md §25):**
- No N+1 queries — use bulk methods, add them if they don't exist.
- Observable property mutations must happen on the UI thread (`Dispatcher.UIThread.InvokeAsync`).
- Use `INSERT OR IGNORE` + `UPDATE` (upsert) for rows that may not exist yet.
- Never use `Directory.GetCurrentDirectory()` for user data paths — anchor to `~/.sovrant/` or the relevant env var override.

**Artifact / file writes:**
- Generated content goes through the `Artifact` tool → `IArtifactStore` → `~/.sovrant/artifacts`. Never write generated output via `WriteFileTool` or direct `File.WriteAll*`.
- `WriteFileTool` is for editing existing source files at absolute paths only.

---

## Reporting Bugs

Open a GitHub Issue with:
- Sovrant version (`v0.9.x`)
- Operating system and .NET runtime version
- Steps to reproduce
- Expected vs. actual behaviour
- Relevant log output (`~/.sovrant/logs/`)

For security issues, see [SECURITY.md](SECURITY.md) — do not open a public issue.

---

## Questions

Open a GitHub Discussion for questions about architecture, roadmap, or usage. Issues are for confirmed bugs and tracked work items.
