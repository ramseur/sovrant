-- V045: Seed built-in tool guides (kind='tools') for Phase 128 code-generation tools.
-- These BuiltIn rows (workspace_id='') appear in the system prompt under "Tool Guides"
-- and in the Knowledge UI under the Tools page. User edits land in overlay rows and
-- are never clobbered here. Add new guides via a later Vxxx migration.

INSERT INTO knowledge_pages
    (knowledge_id, kind, slug, name, description, tier, body, workspace_id,
     trigger, agents, tools, industry, default_format, category, role, recommended_level)
VALUES

  ('tool_CodeCreate', 'tools', 'CodeCreate', 'CodeCreate',
   'Scaffold a new code project from a built-in template and get ready-to-run commands.',
   'BuiltIn',
   '# CodeCreate

Scaffold a complete, runnable code project from a built-in template in one call.

## When to use
- User asks to "create a project", "start a new app", "scaffold a service", or names a language/framework
- Before writing any files manually — always scaffold first, then customise

## Inputs
| Field | Required | Description |
|---|---|---|
| `template_id` | one of these | Exact template ID (e.g. `dotnet/webapi`, `node/express-api`, `python/fastapi`, `go/api`, `rust/cli`) |
| `language` | or these | Language name used to auto-select the best template |
| `kind` | optional | Narrows selection within a language (e.g. `cli`, `webapi`, `library`) |
| `project_name` | yes | Used as directory name and wired into generated source |
| `run_id` | yes | Scopes all files to this artifact run |

## Response fields
- **`build_command`** — the command to compile/build the project; execute it first
- **`run_command`** — the command to start the app
- **`test_command`** — the command to run the test suite
- **`next_steps`** — ordered list of commands to share with the user
- **`files`** — every generated file with its `path` and `access_url`
- **`conventions`** — language style guide (present this to the user or apply it when writing more code)

## Usage pattern
1. Call `CodeListTemplates` if unsure which `template_id` to use
2. Call `CodeCreate` with `project_name` and `run_id`
3. Present `next_steps` to the user verbatim
4. Apply `conventions` when writing any additional code in the same session

## Rules
- Always include `run_id` — without it the call fails
- Use `template_id` over `language` when you know the exact template
- If `error_count > 0`, check `errors[]` and report which files failed
- Never scaffold into an existing `run_id` unless intentionally appending',
   '', NULL, '["coder","executor"]', '["CodeCreate","CodeListTemplates","ArtifactTool","Bash"]',
   NULL, NULL, 'code-generation', NULL, NULL),

  ('tool_CodeCreateMulti', 'tools', 'CodeCreateMulti', 'CodeCreateMulti',
   'Scaffold multiple interdependent project components (monorepo) in a single call.',
   'BuiltIn',
   '# CodeCreateMulti

Scaffold an entire monorepo — multiple interdependent components — in a single call.
Use this instead of multiple `CodeCreate` calls when the components form one deliverable.

## When to use
- User wants a full-stack app (API + frontend), a service + shared library, or any multi-component project
- Do NOT use for a single component — use `CodeCreate` instead

## Inputs
| Field | Required | Description |
|---|---|---|
| `root_name` | yes | Top-level directory (e.g. `my-platform`). All components go under `<root_name>/<project_name>/` |
| `components` | yes | Array of component specs (see below) |
| `run_id` | yes | Scopes all files to this artifact run |

### Component spec
```json
{
  "project_name": "api",
  "template_id": "node/express-api"
}
```
Alternatively use `language` + optional `kind` instead of `template_id`.

## Response fields
- **`next_steps`** — combined ordered list of build/run/test commands across all components
- **`components[]`** — per-component results, each with `build_command`, `run_command`, `test_command`, `files`
- **`total_file_count`** — aggregate file count

## Usage pattern
1. Call `CodeListTemplates` to confirm template IDs for each component
2. Call `CodeCreateMulti` with all components in one call
3. Present `next_steps` to the user, organised per component
4. Mention each component''s `README.md` for full setup

## Rules
- `root_name` becomes the outer directory — choose something meaningful (e.g. project slug)
- Components are written sequentially; a scaffold failure on one component does not abort others
- Check `error_count` at both the top level and per-component before declaring success',
   '', NULL, '["coder","executor"]', '["CodeCreateMulti","CodeListTemplates","ArtifactTool","Bash"]',
   NULL, NULL, 'code-generation', NULL, NULL);
