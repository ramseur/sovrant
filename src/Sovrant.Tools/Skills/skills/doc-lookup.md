---
name: doc-lookup
description: API and documentation research with structured extraction
trigger: /docs-lookup
tools: [WebSearch, WebFetch, Read, Grep, Glob]
---

# Documentation Lookup

Find and extract specific information from API docs, library references, and technical documentation.

## Steps
1. **Identify source** — find the official documentation for the library/API/tool
2. **Navigate to relevant section** — locate the specific topic, endpoint, or function
3. **Extract details** — parameters, return types, examples, gotchas
4. **Verify version** — confirm the docs match the version in use
5. **Summarise** — present findings in a directly actionable format

## Output Format
- **Source** — URL and version
- **API/Function signature**
- **Parameters** — name, type, required/optional, description
- **Return value**
- **Example usage**
- **Gotchas and notes**

## Rules
- Always note the documentation version
- Prefer official docs over third-party tutorials
- If docs are incomplete, note what's missing
