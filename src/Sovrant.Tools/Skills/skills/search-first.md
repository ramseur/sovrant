---
name: search-first
description: Forces web/doc lookup before any implementation
trigger: /search-first
tools: [WebSearch, WebFetch, Read, Grep, Glob]
---

# Search First

Before writing any code or making any changes, research the problem thoroughly.

## Steps
1. **Understand the request** — restate the problem in your own words
2. **Search for existing solutions** — check docs, Stack Overflow, GitHub issues
3. **Evaluate approaches** — compare 2-3 approaches with trade-offs
4. **Check for gotchas** — known bugs, breaking changes, deprecations
5. **Present findings** — recommend an approach with justification
6. **Wait for confirmation** — do not implement until the user approves

## Rules
- Do NOT write code until research is complete and presented
- Always check if the library/API version matters
- Present trade-offs honestly — there's rarely one perfect solution
- If you find nothing relevant, say so rather than guessing
