---
name: data-scraper
description: Autonomous data collection pipeline with enrichment
trigger: /scrape
tools: [WebFetch, WebSearch, Bash, Write, Read]
---

# Data Scraper

Build and execute a data collection pipeline: discover → collect → clean → enrich → store.

## Steps
1. **Define targets** — identify data sources and what to extract
2. **Discover structure** — fetch sample pages, identify data patterns
3. **Collect** — extract data systematically using WebFetch
4. **Clean** — normalise formats, remove duplicates, handle missing values
5. **Enrich** — cross-reference with other sources, add computed fields
6. **Store** — write structured output (JSON, CSV, or markdown)

## Output Format
- **Data summary** — record count, field list, completeness metrics
- **Sample records** — first 5 records as formatted table
- **Data quality notes** — missing fields, inconsistencies found
- **Output file location**

## Rules
- Respect robots.txt and rate limits
- Never store credentials or PII without explicit instruction
- Log all sources for audit trail
- Prefer structured APIs over HTML scraping when available
