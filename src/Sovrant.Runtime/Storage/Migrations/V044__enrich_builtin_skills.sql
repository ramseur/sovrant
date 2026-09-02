-- V044: Enrich built-in skill descriptions and agent/tool lists (Phase 114).
-- Updates `description` to 2-3 sentence form (what, when, output) for all 32 BuiltIn skills.
-- Wires `agents` lists where natural delegations exist and fixes incorrect tool references.
-- Only touches BuiltIn base rows (workspace_id=''); user overlay rows are never clobbered.

-- ─── SKILLS ────────────────────────────────────────────────────────────────

UPDATE knowledge_pages
SET description = 'Creates a structured Architecture Decision Record (ADR) to document an architectural choice, its context, and trade-offs. Use when your team needs a permanent record of a major design decision — framework selection, data model, API boundary. Produces a markdown ADR with options considered, decision rationale, and consequences.'
WHERE slug = 'architecture-decision' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Writes high-quality long-form content — blog posts, essays, newsletters — with voice matching and anti-slop discipline. Use when you need polished published content, not a quick draft. Produces a title, subtitle, structured body with headers, and an optional call to action.'
WHERE slug = 'article-writing' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Designs and executes configurable autonomous loops across six patterns: simple poll, pipeline, watch-and-react, retry-with-backoff, fan-out/fan-in, and DAG. Use when a task needs to repeat, monitor a condition, or process a queue of items with dependency ordering. Always produces a loop with a defined termination condition, per-iteration logging, and rate-limit awareness.'
WHERE slug = 'autonomous-loop' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Triages refund requests, analyses subscription churn patterns, and produces prioritised retention recommendations from billing data. Use when investigating cancellation spikes, processing a batch of refund tickets, or preparing a quarterly retention report. Produces a triage summary (auto-approve / review / escalate counts), a churn dashboard, and a ranked action-item list.',
       agents      = '["data-analyst"]'
WHERE slug = 'billing-ops' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Analyses 5–20 writing samples to extract a reusable brand voice profile covering tone, sentence structure, vocabulary, and signature rhetorical patterns. Use before generating any content that must match an established person or company voice. Produces a structured voice profile and a sample paragraph demonstrating the extracted style.'
WHERE slug = 'brand-voice' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Generates a comprehensive onboarding guide for a new contributor: architecture overview, tech stack, setup instructions, key patterns, and common gotchas. Use at the start of a new project or when onboarding someone who needs to get productive quickly. Produces a structured markdown guide with a Quick Start section, architecture map, and recommended reading order.'
WHERE slug = 'codebase-onboard' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Performs a systematic code review with findings ranked by severity (CRITICAL / HIGH / MEDIUM / LOW) covering logic errors, security vulnerabilities, performance issues, and style violations. Use on any diff or pull request before merge, or when auditing an unfamiliar codebase. Produces a structured findings list with file:line references, explanations, and suggested fixes.'
WHERE slug = 'code-review' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Audits a professional network, scores contacts by strategic value and relationship strength, identifies gaps, and drafts warm re-engagement messages for dormant high-value contacts. Use when preparing for a fundraise, hiring push, or strategic partnership campaign. Produces a network health report, ranked contact list, recommended additions, and personalised outreach drafts.'
WHERE slug = 'connections-optimizer' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Repurposes a single piece of anchor content (article, talk, or thread) into platform-optimised versions for X/Twitter, LinkedIn, newsletter, and blog. Use after publishing long-form content to maximise reach without duplicating the same text across channels. Produces a self-contained adapted version for each target channel with platform-native formatting and a varied hook.',
       agents      = '["content-writer"]'
WHERE slug = 'content-engine' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Adapts a single piece of content for multiple platforms by applying platform-specific constraints: character limits, formatting conventions, hashtag norms, and tone. Use when you have a finished piece and need ready-to-post versions for X, LinkedIn, Threads, Mastodon, or a blog. Produces a self-contained adapted version for each requested platform.',
       agents      = '["content-writer"]'
WHERE slug = 'crosspost' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Builds and executes a structured data collection pipeline: discover sources, fetch pages via WebFetch, clean and normalise, enrich with cross-references, and store as structured output. Use when you need to extract and consolidate data from websites, documents, or APIs into a queryable format. Produces a data summary with record counts, completeness metrics, sample records, and an output file.'
WHERE slug = 'data-scraper' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Conducts thorough multi-source research (15–30 sources), cross-references claims, scores confidence per finding, and produces a structured report with inline citations. Use for strategic decisions, due diligence, or technical investigations where a single source is insufficient. Produces an executive summary, detailed findings with citations, per-finding confidence levels, and identified gaps.'
WHERE slug = 'deep-research' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Locates and extracts specific API, library, or framework documentation — parameters, return types, examples, and version-specific gotchas — from official sources. Use when you need a precise, directly actionable reference without manually reading through an entire doc site. Produces a source URL, API signature, parameter table, usage example, and any known gotchas.'
WHERE slug = 'doc-lookup' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Keeps documentation in sync with recent code changes: locates affected docs, audits accuracy, rewrites outdated sections, verifies links, and adds missing coverage for new APIs. Use after any non-trivial code change or before a release to prevent documentation drift. Produces updated documentation files and a summary of what changed and what was added.',
       agents      = '["doc-updater"]'
WHERE slug = 'doc-update' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Creates investor-ready materials: a 12-slide pitch deck following the Problem→Solution→Market→Traction→Team→Ask structure, a one-page executive summary, and optionally basic financial projections. Use when preparing for a fundraise, demo day, or investor introduction. Produces markdown-formatted deck slides with speaker notes, a one-pager, and a financially consistent model if requested.'
WHERE slug = 'investor-materials' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Researches and qualifies potential leads with a three-axis score (ICP fit, timing signals, warm-path access), ranks the top prospects, surfaces mutual connections, and drafts personalised first-touch messages. Use when building a targeted outreach list or preparing for a sales push into a new segment. Produces a scored lead table and personalised outreach drafts for the top five.',
       agents      = '["sales-intelligence","researcher"]'
WHERE slug = 'lead-intelligence' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Produces structured competitive and market analysis: competitor mapping, feature/price matrix, SWOT summaries, TAM/SAM/SOM estimates with methodology, and trend analysis with source attribution. Use when evaluating a market entry, positioning a product, or preparing for a strategic planning session. Produces a sourced report with a competitor matrix, market-size estimates, and strategic recommendations.'
WHERE slug = 'market-research' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Crafts and executes optimised generation prompts for image, video, or audio AI services, iterates on outputs against the brief, and delivers the final file with reproducible prompt metadata. Use when you need AI-generated visuals or audio as part of a project or campaign. Produces a final media file and the generation prompt saved alongside it for reproducibility.'
WHERE slug = 'media-gen' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Decomposes a complex goal into a phased implementation plan with components, dependencies, risk assessment, and acceptance criteria per phase — before writing any code. Use before starting any non-trivial implementation to reach alignment and surface unknowns early. Produces a phased plan document presented for user review and approval before execution begins.'
WHERE slug = 'plan' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Performs structured product analysis: feature audit, user need mapping, competitive comparison, gap analysis, and prioritisation using an impact/effort matrix. Use when evaluating a roadmap, planning a feature investment, or preparing a competitive positioning brief. Produces a feature matrix, user-need map, ranked gap list, and build/buy/partner recommendations.'
WHERE slug = 'product-lens' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Coordinates project work by triaging open issues, verifying consistency with current code state, updating priorities, and producing a clear status report. Use at the start of a sprint, during triage, or when a project feels out of sync with its issue tracker. Produces an active-work summary, triage results, stale-item list, and blocker callouts.',
       agents      = '["project-manager"]'
WHERE slug = 'project-flow' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Analyses a prompt through a 6-phase evaluation (intent, gaps, structure, constraints, rewrite, comparison) and produces an improved version with a bullet-by-bullet changelog. Use whenever a prompt is producing inconsistent, off-target, or verbose model outputs. Produces the original prompt with annotated issues, an optimised rewrite, and an explanation of every change made.',
       agents      = '["prompt-optimizer"]'
WHERE slug = 'prompt-optimize' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Identifies and safely removes dead code, consolidates duplicates, simplifies complex conditionals, and improves naming — without changing any observable behaviour. Use when a codebase has grown messy or before adding a new feature to a tangled area. Produces incremental edits with a full test run after each change and a summary checklist of what was cleaned.',
       agents      = '["refactor-cleaner"]'
WHERE slug = 'refactor' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Forces thorough research before any implementation: locates existing solutions, compares 2–3 approaches with trade-offs, checks for known bugs and deprecations, and presents a ranked recommendation for user approval. Use at the start of any non-trivial task to avoid reinventing the wheel or repeating known mistakes. Produces a research summary with a recommended approach — no code until the user confirms.',
       agents      = '["researcher"]'
WHERE slug = 'search-first' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Performs a systematic OWASP Top 10 security audit covering injection, auth failures, sensitive data exposure, access control, and vulnerable dependencies, plus a secret scan for hardcoded credentials. Use on any codebase before release, after a significant feature addition, or as part of a compliance audit. Produces a severity-ranked findings table with OWASP category, file:line references, and specific remediation steps.'
WHERE slug = 'security-review' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Captures a successful workflow pattern as a reusable skill definition: identifies the repeatable steps, required tools and agents, an intuitive slash-command trigger, and saves it to the Skills registry. Use after completing a workflow you expect to repeat, or to formalise a team standard. Produces a saved skill definition ready to invoke from the Skills page or via its trigger command.'
WHERE slug = 'skill-create' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Creates a structured presentation deck as markdown or HTML slides: confirms brief, builds an outline, writes each slide with one key idea and speaker notes, and ensures a clean narrative arc from problem to call to action. Use when you need a presentation for a meeting, conference, or internal review. Produces a complete deck with title, context, core-content, summary, and CTA slides.'
WHERE slug = 'slides' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Drives strict Test-Driven Development through the Red-Green-Refactor cycle: write a failing test, confirm it fails, write minimal passing code, confirm it passes, refactor, confirm coverage. Use for any new feature or bug fix where correctness and test coverage are non-negotiable. Produces implementation code with a matching test suite, a confirmed green build, and coverage meeting the configured threshold.'
WHERE slug = 'tdd-workflow' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Assembles a team of specialised agents, writes self-contained briefs for each, dispatches them in parallel, and synthesises their outputs into a coherent result. Use when a task has clearly parallelisable sub-tasks — research + implementation + review — that different specialist agents can handle independently. Produces individual agent deliverables merged into a unified outcome with a monitoring summary.'
WHERE slug = 'team-builder' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Builds an interactive UI prototype as a single self-contained HTML file with inline CSS and JS, realistic sample data, responsive layout, and hover/transition states — no build step required. Use to quickly demonstrate a layout, interaction pattern, or user flow before committing to a full implementation. Produces a browser-ready file the user can open immediately.'
WHERE slug = 'ui-demo' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET    description = 'Runs the structured 6-phase quality gate pipeline in sequence: build, type-check, lint, test with coverage, security scan, and diff review for debug code or secrets. Use before merging any change or after a significant implementation session to confirm the codebase is clean. Produces a pass/fail result per phase with actionable failure details and the precise command that failed.',
       -- Remove the non-existent `Verify` tool; keep the real tools only
       tools       = '["Bash","Read","Grep","Glob"]'
WHERE slug = 'verification-loop' AND tier = 'BuiltIn' AND kind = 'skills';

UPDATE knowledge_pages
SET description = 'Creates animated technical explainers as self-contained HTML files using CSS animations or inline SVG: script with timing markers, storyboard with progressive reveal, and a fully playable animation requiring no build step. Use to communicate a complex concept visually — architecture diagrams, algorithm walkthroughs, data flows. Produces a single browser-ready HTML file with embedded script, animation, and labels.'
WHERE slug = 'video-explainer' AND tier = 'BuiltIn' AND kind = 'skills';
