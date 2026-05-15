---
name: lead-intelligence
description: AI-native prospecting with signal scoring and warm-path discovery
trigger: /leads
tools: [WebSearch, WebFetch, Read, Write]
---

# Lead Intelligence

Research and qualify potential leads with structured signal scoring.

## Steps
1. **Define ICP** — ideal customer profile (industry, size, tech stack, pain points)
2. **Discover leads** — search for companies matching the ICP
3. **Score signals** — rate each lead on:
   - **Fit** (0-10): how well they match the ICP
   - **Timing** (0-10): recent signals suggesting need (hiring, funding, tech changes)
   - **Access** (0-10): warm paths, shared connections, public contact info
4. **Rank** — sort by composite score, highlight top 10
5. **Warm paths** — identify mutual connections, shared events, engagement opportunities
6. **Draft outreach** — personalised first-touch message per top lead

## Output Format
| Company | Fit | Timing | Access | Score | Signal |
|---------|-----|--------|--------|-------|--------|
| ...     | ... | ...    | ...    | ...   | ...    |

Plus: personalised outreach drafts for top 5.
