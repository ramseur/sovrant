-- Phase 112D: columns for document-template knowledge pages
-- fields_json  : JSON array of TemplateField objects (field schema for validation + agent docs)
-- filename_template : Scriban expression that evaluates to the output filename
ALTER TABLE knowledge_pages ADD COLUMN fields_json TEXT;
ALTER TABLE knowledge_pages ADD COLUMN filename_template TEXT;
