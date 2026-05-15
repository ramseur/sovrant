-- V016: Add provider column to session_entries so loaded chats can display
-- "Provider · Model" on assistant bubbles (parity with live streaming).

ALTER TABLE session_entries ADD COLUMN provider TEXT;
