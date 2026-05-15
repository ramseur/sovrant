# CI/CD Integration

Sovrant can run autonomously inside CI pipelines — fix failing tests, resolve build errors, update generated code — without human intervention.

---

## CLI `--ci` flag

The `--ci` flag switches the CLI to machine-readable mode:

- **JSON output** — a single JSON object on stdout with the agent's response, tool calls, errors, and token counts.
- **Non-zero exit** — returns exit code `1` if any tool errors or runtime errors occurred.
- **CI permission policy** — auto-approves file edits and shell commands; denies unknown destructive operations.
- **No interactive prompts** — `AskUserQuestion` returns empty; no console input is expected.
- **Silent logging** — console log output is suppressed; file logging still writes to the configured path.

### Usage

```bash
export LLM_API_KEY="sk-..."

# Fix failing tests
dotnet run --project src/Sovrant.Cli -- --ci --model gpt-4o-mini prompt "The tests are failing with this error: <paste error>"

# With a specific model and session
dotnet run --project src/Sovrant.Cli -- --ci --model gemini-2.5-flash --session ci-fix prompt "Update the generated API client from the new OpenAPI spec"
```

### JSON output format

```json
{
  "success": true,
  "text": "I've fixed the failing test by updating the expected value...",
  "tool_calls": [
    { "id": "tc_1", "tool_name": "read", "content": "...", "is_error": false },
    { "id": "tc_2", "tool_name": "edit", "content": "...", "is_error": false }
  ],
  "errors": [],
  "input_tokens": 1200,
  "output_tokens": 350
}
```

| Field | Type | Description |
|---|---|---|
| `success` | boolean | `true` if no errors occurred |
| `text` | string | The agent's final text response |
| `tool_calls` | array | All tool invocations with their results |
| `errors` | array | Error messages (empty on success) |
| `input_tokens` | integer | Total input tokens consumed |
| `output_tokens` | integer | Total output tokens generated |

---

## CI permission policy

In `--ci` mode, Sovrant uses `CiPermissionPolicy` instead of the normal mode-based policy:

| Tool category | Decision |
|---|---|
| Read-only tools (read, glob, grep, ls, web_fetch, etc.) | Allow |
| File edits (write, edit, create, delete) | Allow |
| Shell commands (bash, powershell) | Allow |
| Worktree tools (enter/exit) | Allow |
| Unknown destructive tools | **Deny** |
| Unknown non-destructive tools | Allow |

The CI runner owns the checkout, so file and shell operations are safe. Unknown destructive operations are denied as a safety net.

> **Graduated Tool Tiers:** All 56 tools are classified into four tiers (Safe, Moderate, Dangerous, Escalation) via `GraduatedToolTiers`. The `CiPermissionPolicy` allows Safe, Moderate, and Dangerous tools (Bash/PowerShell are expected in CI). Escalation-tier tools (Agent, Team, Swarm, Mission) are denied by default in CI — they spawn sub-processes or long-running orchestrations not suitable for headless CI runs. The Trust Boundary is active in CI mode: PII/corporate data sanitization applies to all outbound LLM calls, and the ethical harness blocks harmful content generation.

> **Tip:** For read-only CI runs (e.g. code review), combine `--ci` with `--permission-mode plan` to block all write operations.

---

## GitHub Actions

### Using the built-in action

The repository includes a composite action at `.github/actions/sovrant-agent/`:

```yaml
name: Fix failing tests
on:
  workflow_dispatch:
    inputs:
      prompt:
        description: "What should the agent do?"
        default: "Fix the failing tests"

jobs:
  agent:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: ./.github/actions/sovrant-agent
        id: sovrant
        env:
          LLM_API_KEY: ${{ secrets.LLM_API_KEY }}
        with:
          prompt: ${{ inputs.prompt }}
          model: gpt-4o-mini

      - name: Commit fixes
        if: steps.sovrant.outputs.success == 'true'
        run: |
          git config user.name "sovrant-bot"
          git config user.email "sovrant-bot@users.noreply.github.com"
          git add -A
          git diff --staged --quiet || git commit -m "fix: automated fix by Sovrant agent"
          git push
```

### Action inputs

| Input | Required | Description |
|---|---|---|
| `prompt` | Yes | The message to send to the agent |
| `model` | No | LLM model (default: config file or env) |
| `session` | No | Session ID for persistent context |
| `working-directory` | No | Working directory (default: repo root) |

### Action outputs

| Output | Description |
|---|---|
| `success` | `true` / `false` |
| `output` | The agent's text response |
| `json` | Full JSON output |

### Manual workflow (without the action)

```yaml
jobs:
  fix:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Run Sovrant
        env:
          LLM_API_KEY: ${{ secrets.LLM_API_KEY }}
        run: |
          output=$(dotnet run --project src/Sovrant.Cli -- \
            --ci --model gpt-4o-mini \
            prompt "Fix the failing tests" 2>&1)
          echo "$output" | jq .
          success=$(echo "$output" | jq -r '.success')
          if [ "$success" != "true" ]; then
            echo "Agent reported errors"
            exit 1
          fi
```

---

## GitLab CI

```yaml
stages:
  - agent

sovrant-fix:
  stage: agent
  image: mcr.microsoft.com/dotnet/sdk:10.0
  variables:
    LLM_API_KEY: $LLM_API_KEY
  script:
    - dotnet build src/Sovrant.Cli --configuration Release --nologo -v quiet
    - |
      output=$(dotnet run --project src/Sovrant.Cli --configuration Release --no-build -- \
        --ci --model gpt-4o-mini \
        prompt "Fix the failing tests")
      echo "$output" | jq .
      success=$(echo "$output" | jq -r '.success')
      if [ "$success" != "true" ]; then
        echo "Agent reported errors"
        exit 1
      fi
    - git add -A
    - git diff --staged --quiet || git commit -m "fix: automated fix by Sovrant agent"
    - git push
  rules:
    - when: manual
```

---

## Use cases

| Scenario | Prompt example |
|---|---|
| Fix failing tests | `"The tests are failing with: <test output>"` |
| Update generated code | `"Regenerate the API client from the OpenAPI spec at docs/api.yaml"` |
| Resolve merge conflicts | `"Resolve the merge conflicts in the current branch"` |
| Code review | `"Review the changes in the last commit and suggest improvements"` (use `--permission-mode plan`) |
| Dependency updates | `"Update all NuGet packages to their latest stable versions and fix any breaking changes"` |

---

## Security

- **Never pass production secrets** into the agent session. Use read-only API keys scoped to the CI provider.
- **Use `--permission-mode plan`** for CI runs that should not make destructive changes (e.g. code review).
- The `CiPermissionPolicy` denies unknown destructive tools as a safety net.
- The agent's LLM API key (`LLM_API_KEY`) should be stored as a CI secret, never hardcoded.
- Consider using a session ID to isolate CI runs from each other when using the server mode.
