-- Phase 116 F: tracks which knowledge items (skills, agents, document templates, tool guides)
-- were invoked during each session turn so provenance can be shown in the UI (Step H).
CREATE TABLE IF NOT EXISTS knowledge_attributions (
    id          INTEGER PRIMARY KEY,
    session_id  TEXT    NOT NULL,
    turn_index  INTEGER NOT NULL,
    kind        TEXT    NOT NULL,   -- 'skills', 'agents', 'document-templates', 'tools'
    slug        TEXT    NOT NULL,
    used_at     TEXT    NOT NULL    -- ISO 8601
);

CREATE INDEX IF NOT EXISTS idx_knowledge_attributions_session
    ON knowledge_attributions (session_id);
