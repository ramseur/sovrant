-- V034: Agent-specific columns for knowledge_pages.
-- Phase 109 migrates built-in agent templates into knowledge_pages (kind='agents').
-- Agent templates carry a role and a recommended model level that have no home in
-- the V033 schema, so add them as nullable columns (additive, backwards-compatible —
-- existing rows and the existing kinds are unaffected).
--
-- role:              AgentRole enum value (e.g. 'Planner', 'Coder', 'Reviewer').
-- recommended_level: RecommendedLevel enum value ('High' | 'Standard' | 'Fast').
ALTER TABLE knowledge_pages ADD COLUMN role TEXT;
ALTER TABLE knowledge_pages ADD COLUMN recommended_level TEXT;
