# Document Template Authoring Guide

Document templates in Sovrant are stored as rows in the `knowledge_pages` table
(`kind = 'document-templates'`). The body is a Scriban template that renders to
Markdown (or structured JSON for Excel), and the field schema is stored as a
JSON array in the `fields_json` column.

This guide covers everything a domain expert needs to author, edit, test, and
deploy a new document template without touching C#.

---

## Quick Start

1. **Open the Knowledge UI** → Documents → find the template you want to edit.
   Click **Edit** to modify the body or fields inline. Changes land in a
   `Global`-tier overlay row that shadows the built-in automatically.

2. **Or write a SQL migration** (see [Adding a new template via migration](#adding-a-new-template-via-migration)).

3. **Validate** with the CLI:
   ```
   sovrant document lint --id legal/nda
   ```

---

## Template Body — Scriban Syntax

The body is a [Scriban](https://github.com/scriban/scriban) template. Scriban
uses `{{ }}` for expressions and `{%  %}` for statements.

### Variable interpolation

```scriban
**Client:** {{ client_name }}
**Date:** {{ format_date effective_date }}
```

### Conditionals

```scriban
{{ if governing_law && governing_law != "" }}
## Governing Law
This agreement is governed by the laws of {{ governing_law }}.
{{ end }}
```

### Loops over string arrays

```scriban
## Parties
{{ for party in parties }}- {{ party }}
{{ end }}
```

### Loops over object arrays

```scriban
## Action Items
{{ for item in action_items }}- {{ item.task }}{{ if item.owner && item.owner != "" }} — **{{ item.owner }}**{{ end }}
{{ end }}
```

### Markdown tables from object arrays

```scriban
| Date | Category | Amount |
|------|----------|--------|
{{ for row in line_items }}| {{ format_date row.date }} | {{ escape_pipes row.category }} | {{ format_money row.amount currency }} |
{{ end }}
```

Always call `escape_pipes` on string values inside table cells to prevent
Markdown table corruption from `|` characters in user data.

---

## Format Helpers

These functions are available in every template body.

| Helper | Signature | Example |
|--------|-----------|---------|
| `format_date` | `(value) → string` | `{{ format_date start_date }}` → `2024-03-15` |
| `format_money` | `(amount, currency) → string` | `{{ format_money total "USD" }}` → `USD 1,500.00` |
| `format_money_whole` | `(amount, currency) → string` | `{{ format_money_whole fee "USD" }}` → `USD 1,500` |
| `format_number` | `(value) → string` | `{{ format_number count }}` → `1,234` |
| `format_percent` | `(rate) → string` | `{{ format_percent tax_rate }}` → `8.5%` |
| `escape_pipes` | `(value) → string` | `{{ escape_pipes description }}` — escapes `\|` in Markdown tables |
| `slug` | `(value, fallback?) → string` | `{{ slug client_name "client" }}` → `acme-corp` |
| `normalize_currency` | `(value) → string` | `{{ normalize_currency currency }}` → `USD` |

`format_date` accepts ISO date strings (`2024-03-15`) and passes them through
unchanged if they don't parse. `format_money` / `format_money_whole` accept
numeric amounts (Decimal or Double from JSON).

---

## Field Schema (`fields_json`)

Every template declares its inputs as a JSON array of field objects stored in
the `fields_json` column. The agent uses this to collect missing data before
calling the template.

### Field object shape

```json
{
  "name": "client_name",
  "type": "string",
  "required": true,
  "description": "Full legal name of the client."
}
```

### Field types

| `type` value | JSON shape expected | Notes |
|---|---|---|
| `string` | `"value"` | Single-line text |
| `text` | `"value"` | Multi-line / paragraph |
| `integer` | `42` | Whole number |
| `decimal` | `3.14` or `"3.14"` | Fractional number |
| `currency` | `1500.00` or `"1500.00"` | Monetary amount (use `format_money` in body) |
| `date` | `"2024-03-15"` | ISO date string |
| `boolean` | `true` / `false` | |
| `stringArray` | `["a", "b"]` | List of strings |
| `objectArray` | `[{...}, {...}]` | List of objects; requires `itemFields` |

### `objectArray` with nested fields

```json
{
  "name": "line_items",
  "type": "objectArray",
  "required": true,
  "description": "Expense line items.",
  "itemFields": [
    { "name": "date",        "type": "date",    "required": true  },
    { "name": "description", "type": "string",  "required": true  },
    { "name": "amount",      "type": "currency", "required": true  },
    { "name": "receipt",     "type": "string",  "required": false }
  ]
}
```

---

## `filename_template` and `default_format`

### `default_format`

One of: `Word`, `StructuredPdf`, `Excel`, `Markdown`, `Pdf`, `PowerPoint`.

Most Markdown-body templates use `Word` (rendered to DOCX via MigraDoc).

### `filename_template`

A Scriban expression producing the output filename. The same format helpers
and field variables are available.

```
nda-{{ slug client_name "client" }}-{{ slug counterparty_name "counterparty" }}-{{ format_date effective_date }}.docx
```

If omitted, Sovrant falls back to `<slug>.<ext>`.

---

## Adding a New Template via Migration

Create `src/Sovrant.Runtime/Storage/Migrations/V0NN__<description>.sql`:

```sql
-- V0NN: Add <template name> template (Phase NN).

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-my-template',   -- unique ID; prefix 'doctempl_' + industry slug
  'document-templates',
  'legal/my-template',            -- id used in the API and CLI
  'My Template Name',
  'One-sentence description shown in template listings.',
  'BuiltIn',
  '# {{ title }}

**Party:** {{ party_name }}
...rest of Scriban body...',
  '',                             -- workspace_id '' = BuiltIn base row
  unixepoch(),
  unixepoch(),
  'legal',                        -- industry
  'Word',                         -- default_format
  '[{"name":"title","type":"string","required":true},{"name":"party_name","type":"string","required":true}]',
  'my-template-{{ slug party_name "party" }}.docx'
);
```

Then update the test files:
- `tests/Sovrant.Runtime.Tests/Storage/SqliteStorageProviderTests.cs` — bump schema version
- `tests/Sovrant.Runtime.Tests/Storage/OldDbUpgradeTests.cs` — bump schema version + row count

---

## Editing a Built-In Template

Built-in templates (`tier = 'BuiltIn'`, `workspace_id = ''`) are read-only.
Edits land in a **Global overlay** row (`tier = 'User'`, `workspace_id = 'global'`)
that shadows the built-in by slug. The built-in row is never touched.

**Via the Knowledge UI:** Documents → select template → Edit. The UI handles
copy-on-write automatically.

**Via `IKnowledgeStore.UpsertAsync`** (programmatic):

```csharp
var page = new KnowledgePage(
    KnowledgeId:   $"doctempl_global_legal-nda",
    Kind:          "document-templates",
    Slug:          "legal/nda",
    Name:          "Non-Disclosure Agreement",
    Tier:          "User",
    WorkspaceId:   KnowledgeScope.Global,
    Body:          "...updated Scriban body...",
    // ... other fields
);
await store.UpsertAsync(page);
```

To **revert** to the built-in: delete the Global overlay row.

---

## Validating a Template

```
# Validate a specific template
sovrant document lint --id legal/nda

# Validate all DB-backed templates
sovrant document lint

# Machine-readable output for CI
sovrant document lint --json
```

The lint command checks:
- Scriban syntax — `Template.Parse(body)` reports parse errors
- `fields_json` — must be a valid JSON array

Exit code 0 = all pass. Exit code 1 = one or more failures.

---

## Template Anatomy Example

Complete example — a simple consulting agreement:

**`fields_json`:**
```json
[
  { "name": "client_name",   "type": "string",  "required": true,  "description": "Client's full legal name." },
  { "name": "consultant",    "type": "string",  "required": true,  "description": "Consultant's full legal name." },
  { "name": "start_date",    "type": "date",    "required": true  },
  { "name": "end_date",      "type": "date",    "required": false },
  { "name": "rate",          "type": "currency","required": true,  "description": "Hourly or project rate." },
  { "name": "currency",      "type": "string",  "required": false, "description": "ISO currency code (default USD)." },
  { "name": "scope",         "type": "text",    "required": true,  "description": "Description of services." },
  { "name": "governing_law", "type": "string",  "required": false }
]
```

**Body (Scriban):**
```scriban
# Consulting Agreement

**Client:** {{ client_name }}
**Consultant:** {{ consultant }}
**Start Date:** {{ format_date start_date }}{{ if end_date && end_date != "" }}
**End Date:** {{ format_date end_date }}{{ end }}
**Rate:** {{ format_money rate (normalize_currency currency) }}

## Scope of Services

{{ scope }}

{{ if governing_law && governing_law != "" }}
## Governing Law

This agreement is governed by the laws of {{ governing_law }}.

{{ end }}
## Signatures

**Client:** {{ client_name }}

Signature: ____________________________  Date: ____________

**Consultant:** {{ consultant }}

Signature: ____________________________  Date: ____________
```

**`filename_template`:**
```
consulting-agreement-{{ slug client_name "client" }}-{{ format_date start_date }}.docx
```
