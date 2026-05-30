-- V033: Knowledge pages — DB-backed store for skills, document templates, and tool templates.
-- This table becomes the primary write target for the Knowledge Authoring UI.
-- Reads continue from the filesystem in the short term; over time the filesystem
-- will be deprecated and this table will be the sole source of truth (Phase 108+).
--
-- workspace_id = '' means global (installation-wide, not workspace-scoped).
-- tier: 'BuiltIn' (assembly-shipped) | 'User' (user-created/overridden).
CREATE TABLE IF NOT EXISTS knowledge_pages (
    knowledge_id   TEXT NOT NULL PRIMARY KEY,
    kind           TEXT NOT NULL,              -- 'skills' | 'documents' | 'tools'
    slug           TEXT NOT NULL,              -- file-safe identifier (alphanumeric + dash/underscore)
    name           TEXT NOT NULL,
    description    TEXT NOT NULL DEFAULT '',
    tier           TEXT NOT NULL DEFAULT 'User',
    -- skills-specific columns
    trigger        TEXT,
    agents         TEXT,                       -- JSON array e.g. '["agent-a","agent-b"]'
    tools          TEXT,                       -- JSON array e.g. '["read","write"]'
    -- documents-specific columns
    industry       TEXT,
    default_format TEXT,
    -- tool-templates-specific columns
    category       TEXT,
    -- content
    body           TEXT NOT NULL DEFAULT '',
    -- ownership / scope
    workspace_id   TEXT NOT NULL DEFAULT '',
    -- audit
    created_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    UNIQUE (kind, slug, workspace_id)
);

CREATE INDEX IF NOT EXISTS idx_knowledge_pages_kind
    ON knowledge_pages (kind);

CREATE INDEX IF NOT EXISTS idx_knowledge_pages_workspace
    ON knowledge_pages (workspace_id);
