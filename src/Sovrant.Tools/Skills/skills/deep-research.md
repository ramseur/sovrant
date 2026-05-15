---
name: deep-research
description: Multi-source research with citation and confidence scoring
trigger: /research
agents: [researcher]
tools: [Read, Grep, Glob, WebSearch, WebFetch]
---

# Deep Research

Conduct thorough research from 15-30 sources. Prioritise primary sources over secondary, and recent over dated.

## Steps
1. **Define scope** — clarify the research question and boundaries
2. **Search broadly** — web, documentation, codebase, and any available APIs
3. **Evaluate sources** — rate reliability (official docs > blog posts > forum threads)
4. **Cross-reference** — verify claims across multiple independent sources
5. **Synthesise** — produce a structured report with findings, citations, and gaps

## Output Format
- **Executive summary** (3-5 sentences)
- **Detailed findings** — each finding with inline citations [Source: URL or reference]
- **Confidence assessment** — High/Medium/Low per finding
- **Gaps and unknowns** — what couldn't be verified
- **Recommended next steps**

## Rules
- Never present a single source as definitive — always cross-reference
- Flag conflicting information explicitly
- Distinguish between facts, expert opinions, and speculation
- Include date of source where available
