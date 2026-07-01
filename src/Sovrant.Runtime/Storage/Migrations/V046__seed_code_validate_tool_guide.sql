-- V046: Seed built-in tool guide for CodeValidate (Phase 128E).
-- Adds a kind='tools' BuiltIn row so agents see usage guidance in the system prompt.

INSERT INTO knowledge_pages
    (knowledge_id, kind, slug, name, description, tier, body, workspace_id,
     trigger, agents, tools, industry, default_format, category, role, recommended_level)
VALUES
  ('tool_CodeValidate', 'tools', 'CodeValidate', 'CodeValidate',
   'Validate structural completeness of a scaffolded code artifact run — no compiler required.',
   'BuiltIn',
   '# CodeValidate

Structurally validates a scaffolded code run after `CodeCreate` or `CodeCreateMulti`.
Checks that language-specific marker files and universal scaffold files are present.
No compiler or runtime is required — all checks are performed via the artifact store.

## When to use
- Immediately after `CodeCreate` to confirm the scaffold completed correctly
- When debugging a scaffold that fails to build (check structural issues first)
- In an automated post-scaffold quality gate before handing off to the user

## Inputs
| Field | Required | Description |
|---|---|---|
| `run_id` | yes | The run ID returned by `CodeCreate` or `CodeCreateMulti` |
| `workspace_id` | no | Workspace ID (defaults to ''personal'') |
| `project_id` | no | Project ID (defaults to ''default-project'') |

## Response fields
- **`pass`** — `true` if all gates passed; `false` if any gate failed
- **`language`** / **`kind`** / **`template_id`** — from the stored manifest
- **`build_command`** / **`run_command`** / **`test_command`** — ready to share
- **`gates`** — per-gate results: `name`, `check`, `severity` (critical/warning), `passed`
- **`gates_failed`** — number of failed gates
- **`remediation`** — ordered list of fixes for failed gates

## Gate severities
- **critical** — language marker file missing; the project will not build without it
- **warning** — universal file missing (.gitignore, README.md, CI workflow)

## Usage pattern
```
1. Call CodeCreate → get run_id
2. Call CodeValidate(run_id) → check pass == true
3. If pass == false: surface remediation[] to the user or fix and re-run
4. If pass == true: present build_command / next_steps to the user
```

## Language gates
| Language | Critical check |
|---|---|
| dotnet | `.sln` or `.slnx` file + `Directory.Build.props` |
| node | `package.json` |
| python | `pyproject.toml` or `setup.py` |
| go | `go.mod` |
| rust | `Cargo.toml` |
| java | `pom.xml` |
| kotlin | `build.gradle.kts` |
| ruby | `Gemfile` |
| swift | `Package.swift` |
| lua | `*.rockspec` |
| zig | `build.zig` |
| cpp | `CMakeLists.txt` |

Universal gates (all languages): `README.md`, `.gitignore`, `.github/workflows/ci.yml`',
   '', NULL, '["coder","executor"]', '["CodeValidate","CodeCreate","CodeListTemplates","ArtifactTool"]',
   NULL, NULL, 'code-generation', NULL, NULL);
