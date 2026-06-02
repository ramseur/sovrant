-- V037: Seed 42 built-in document templates as BuiltIn knowledge_pages rows.
-- kind='document-templates', tier='BuiltIn', workspace_id=''.
-- Body = Scriban template (Markdown output). fields_json = serialized TemplateField[].
-- EXCLUDED: finance/expense-report and finance/loan-amortization (Excel format, C#-only).

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_business-meeting-notes',
  'document-templates',
  'business/meeting-notes',
  'Meeting Notes',
  'Structured meeting notes with attendees, agenda, decisions, and action items.',
  'BuiltIn',
  '# {{ title }}

**Date:** {{ format_date meeting_date }}{{ if location && location != "" }}
**Location:** {{ location }}{{ end }}

## Attendees
{{ for a in attendees }}- {{ a }}
{{ end }}
{{ if absent && absent.size > 0 }}
## Absent
{{ for a in absent }}- {{ a }}
{{ end }}
{{ end }}
{{ if agenda && agenda.size > 0 }}
## Agenda
{{ i = 1 }}{{ for item in agenda }}{{ i }}. {{ item }}
{{ i = i + 1 }}{{ end }}
{{ end }}
{{ if discussion && discussion != "" }}
## Discussion
{{ discussion }}

{{ end }}
{{ if decisions && decisions.size > 0 }}
## Decisions
{{ for d in decisions }}- {{ d }}
{{ end }}
{{ end }}
{{ if action_items && action_items.size > 0 }}
## Action Items
{{ for item in action_items }}- {{ item.task }}{{ if item.owner && item.owner != "" }} — **{{ item.owner }}**{{ end }}{{ if item.due_date && item.due_date != "" }} (due {{ format_date item.due_date }}){{ end }}
{{ end }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'business',
  'Word',
  '[{"name":"title","type":"string","required":true,"description":"Meeting title."},{"name":"meeting_date","type":"date","required":true,"description":"ISO date the meeting took place."},{"name":"location","type":"string","required":false,"description":"Physical room or video link."},{"name":"attendees","type":"stringArray","required":true,"description":"List of attendee names."},{"name":"absent","type":"stringArray","required":false,"description":"Invited people who did not attend."},{"name":"agenda","type":"stringArray","required":false,"description":"Ordered agenda topics."},{"name":"discussion","type":"text","required":false,"description":"Free-form discussion summary."},{"name":"decisions","type":"stringArray","required":false,"description":"Decisions made."},{"name":"action_items","type":"objectArray","required":false,"description":"Action items.","itemFields":[{"name":"task","type":"string","required":true},{"name":"owner","type":"string","required":false},{"name":"due_date","type":"date","required":false}]}]',
  'meeting-notes-{{ format_date meeting_date }}-{{ slug title }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_business-sow',
  'document-templates',
  'business/sow',
  'Statement of Work',
  'Statement of Work with scope, deliverables, payment terms, and milestones.',
  'BuiltIn',
  '# Statement of Work

**SOW Number:** {{ sow_number }}
**Client:** {{ client_name }}
**Vendor:** {{ vendor_name }}
**Effective Date:** {{ format_date effective_date }}

## Scope of Services
{{ scope }}

## Deliverables
| # | Title | Description | Due Date |
|---|-------|-------------|----------|
{{ i = 1 }}{{ for d in deliverables }}| {{ i }} | {{ escape_pipes d.title }} | {{ if d.description && d.description != "" }}{{ escape_pipes d.description }}{{ end }} | {{ if d.due_date && d.due_date != "" }}{{ format_date d.due_date }}{{ end }} |
{{ i = i + 1 }}{{ end }}

{{ if payment_terms && payment_terms != "" }}
## Payment Terms
{{ payment_terms }}

{{ end }}
{{ if milestones && milestones.size > 0 }}
## Milestones
| Milestone | Due Date | Amount |
|-----------|----------|--------|
{{ for m in milestones }}| {{ escape_pipes m.milestone }} | {{ if m.due_date && m.due_date != "" }}{{ format_date m.due_date }}{{ end }} | {{ if m.amount > 0 }}{{ format_money m.amount "" }}{{ end }} |
{{ end }}

{{ end }}
{{ if governing_law && governing_law != "" }}
## Governing Law
This Statement of Work is governed by the laws of {{ governing_law }}.

{{ end }}
## Signatures

**Client:** {{ client_name }}

Signature: ____________________________  Date: ____________

**Vendor:** {{ vendor_name }}

Signature: ____________________________  Date: ____________',
  '',
  unixepoch(),
  unixepoch(),
  'business',
  'Word',
  '[{"name":"sow_number","type":"string","required":true,"description":"SOW identifier."},{"name":"client_name","type":"string","required":true,"description":"Client legal name."},{"name":"vendor_name","type":"string","required":true,"description":"Vendor legal name."},{"name":"effective_date","type":"date","required":true,"description":"Effective date of the SOW."},{"name":"scope","type":"text","required":true,"description":"Detailed scope of services."},{"name":"deliverables","type":"objectArray","required":true,"description":"Deliverables list.","itemFields":[{"name":"title","type":"string","required":true},{"name":"description","type":"text","required":false},{"name":"due_date","type":"date","required":false}]},{"name":"payment_terms","type":"text","required":false,"description":"Payment terms and structure."},{"name":"milestones","type":"objectArray","required":false,"description":"Milestones with amounts.","itemFields":[{"name":"milestone","type":"string","required":true},{"name":"due_date","type":"date","required":false},{"name":"amount","type":"decimal","required":false}]},{"name":"governing_law","type":"string","required":false,"description":"Governing jurisdiction."}]',
  'sow-{{ slug sow_number }}-{{ slug client_name }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_business-project-status',
  'document-templates',
  'business/project-status',
  'Project Status Report',
  'Project status report with RAG health, milestones, risks, and next steps.',
  'BuiltIn',
  '# Project Status Report — {{ project_name }}

**Period:** {{ period }}
**Overall Health:** {{ overall_health }}

## Executive Summary
{{ summary }}

{{ if milestones && milestones.size > 0 }}
## Milestones
| Milestone | Target Date | Status |
|-----------|-------------|--------|
{{ for m in milestones }}| {{ escape_pipes m.milestone }} | {{ if m.target_date && m.target_date != "" }}{{ format_date m.target_date }}{{ end }} | {{ escape_pipes m.status }} |
{{ end }}

{{ end }}
{{ if risks && risks.size > 0 }}
## Risks
| Risk | Impact | Mitigation |
|------|--------|------------|
{{ for r in risks }}| {{ escape_pipes r.risk }} | {{ escape_pipes r.impact }} | {{ if r.mitigation && r.mitigation != "" }}{{ escape_pipes r.mitigation }}{{ end }} |
{{ end }}

{{ end }}
{{ if next_steps && next_steps.size > 0 }}
## Next Steps
{{ for s in next_steps }}- {{ s }}
{{ end }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'business',
  'Word',
  '[{"name":"project_name","type":"string","required":true,"description":"Project name."},{"name":"period","type":"string","required":true,"description":"Reporting period e.g. April 2026."},{"name":"overall_health","type":"string","required":true,"description":"RAG status: Green, Amber, or Red."},{"name":"summary","type":"text","required":true,"description":"Executive summary."},{"name":"milestones","type":"objectArray","required":false,"description":"Milestone list.","itemFields":[{"name":"milestone","type":"string","required":true},{"name":"target_date","type":"date","required":false},{"name":"status","type":"string","required":true}]},{"name":"risks","type":"objectArray","required":false,"description":"Risk register.","itemFields":[{"name":"risk","type":"string","required":true},{"name":"impact","type":"string","required":true},{"name":"mitigation","type":"string","required":false}]},{"name":"next_steps","type":"stringArray","required":false,"description":"Next steps."}]',
  'status-{{ slug project_name }}-{{ slug period }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_business-employee-offer',
  'document-templates',
  'business/employee-offer',
  'Employment Offer Letter',
  'Employment offer letter with compensation, start date, and benefits summary.',
  'BuiltIn',
  '# Employment Offer Letter

{{ format_date letter_date }}

Dear {{ candidate_name }},

We are pleased to extend this offer of employment with **{{ company_name }}** for the position of **{{ position_title }}**{{ if department && department != "" }}, {{ department }} department{{ end }}.

**Start Date:** {{ format_date start_date }}
**Base Salary:** {{ format_money base_salary "" }}{{ if salary_period && salary_period != "" }} {{ salary_period }}{{ end }}

{{ if benefits_summary && benefits_summary != "" }}
## Benefits
{{ benefits_summary }}

{{ end }}
{{ if at_will }}
## At-Will Employment
Your employment with {{ company_name }} is at-will, meaning either party may terminate the employment relationship at any time, with or without cause or notice.

{{ end }}
This offer is contingent upon satisfactory completion of any required background checks and execution of the company''s standard employment agreements.

Please indicate your acceptance of this offer by signing and returning this letter.

Sincerely,

{{ company_name }}

Signature: ____________________________  Date: ____________

---

**Accepted by:** {{ candidate_name }}

Signature: ____________________________  Date: ____________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'business',
  'Word',
  '[{"name":"company_name","type":"string","required":true,"description":"Company name."},{"name":"candidate_name","type":"string","required":true,"description":"Candidate full name."},{"name":"position_title","type":"string","required":true,"description":"Job title being offered."},{"name":"department","type":"string","required":false,"description":"Department name."},{"name":"start_date","type":"date","required":true,"description":"Proposed start date."},{"name":"letter_date","type":"date","required":true,"description":"Date of the offer letter."},{"name":"base_salary","type":"currency","required":true,"description":"Base salary amount."},{"name":"salary_period","type":"string","required":false,"description":"e.g. per year"},{"name":"benefits_summary","type":"text","required":false,"description":"Summary of benefits package."},{"name":"at_will","type":"boolean","required":false,"description":"At-will employment statement."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'offer-{{ slug candidate_name }}-{{ format_date letter_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_business-performance-review',
  'document-templates',
  'business/performance-review',
  'Performance Review',
  'Employee performance review with competency ratings and goals for the next period.',
  'BuiltIn',
  '# Performance Review

**Employee:** {{ employee_name }}
**Reviewer:** {{ reviewer_name }}
**Review Period:** {{ review_period }}

## Overall Summary
{{ summary }}

## Competency Ratings
| Area | Rating | Comments |
|------|--------|----------|
{{ for c in competencies }}| {{ escape_pipes c.area }} | {{ escape_pipes c.rating }} | {{ if c.comments && c.comments != "" }}{{ escape_pipes c.comments }}{{ end }} |
{{ end }}

{{ if accomplishments && accomplishments != "" }}
## Key Accomplishments
{{ accomplishments }}

{{ end }}
{{ if goals_next_period && goals_next_period.size > 0 }}
## Goals for Next Period
| Goal | Success Criteria |
|------|-----------------|
{{ for g in goals_next_period }}| {{ escape_pipes g.goal }} | {{ if g.success_criteria && g.success_criteria != "" }}{{ escape_pipes g.success_criteria }}{{ end }} |
{{ end }}
{{ end }}

## Signatures

**Employee:** {{ employee_name }}

Signature: ____________________________  Date: ____________

**Reviewer:** {{ reviewer_name }}

Signature: ____________________________  Date: ____________',
  '',
  unixepoch(),
  unixepoch(),
  'business',
  'Word',
  '[{"name":"employee_name","type":"string","required":true,"description":"Employee full name."},{"name":"reviewer_name","type":"string","required":true,"description":"Reviewer full name."},{"name":"review_period","type":"string","required":true,"description":"e.g. Q1 2026"},{"name":"summary","type":"text","required":true,"description":"Overall performance summary."},{"name":"competencies","type":"objectArray","required":true,"description":"Competency ratings.","itemFields":[{"name":"area","type":"string","required":true},{"name":"rating","type":"string","required":true},{"name":"comments","type":"text","required":false}]},{"name":"accomplishments","type":"text","required":false,"description":"Key accomplishments this period."},{"name":"goals_next_period","type":"objectArray","required":false,"description":"Goals for next period.","itemFields":[{"name":"goal","type":"string","required":true},{"name":"success_criteria","type":"string","required":false}]}]',
  'review-{{ slug employee_name }}-{{ slug review_period }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_business-business-plan',
  'document-templates',
  'business/business-plan',
  'Business Plan',
  'Comprehensive business plan with executive summary, market analysis, and financial projections.',
  'BuiltIn',
  '# Business Plan — {{ company_name }}

**Date:** {{ format_date plan_date }}

## Executive Summary
{{ executive_summary }}

## Company Overview
{{ company_overview }}

## Products & Services
{{ products_services }}

## Market Analysis
{{ market_analysis }}

{{ if marketing_strategy && marketing_strategy != "" }}
## Marketing Strategy
{{ marketing_strategy }}

{{ end }}
{{ if operations_plan && operations_plan != "" }}
## Operations Plan
{{ operations_plan }}

{{ end }}
{{ if management_team && management_team.size > 0 }}
## Management Team
{{ for m in management_team }}
### {{ m.name }} — {{ m.title }}
{{ if m.bio && m.bio != "" }}{{ m.bio }}{{ end }}

{{ end }}
{{ end }}
{{ if financial_projections && financial_projections.size > 0 }}
## Financial Projections
| Year | Revenue | Expenses | Net Income |
|------|---------|----------|------------|
{{ for p in financial_projections }}| {{ p.year }} | {{ format_money p.revenue "" }} | {{ format_money p.expenses "" }} | {{ format_money p.net_income "" }} |
{{ end }}

{{ end }}
{{ if funding_required > 0 }}
## Funding Required
**Total Funding Requested:** {{ format_money funding_required "" }}

{{ if use_of_funds && use_of_funds != "" }}
## Use of Funds
{{ use_of_funds }}
{{ end }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'business',
  'StructuredPdf',
  '[{"name":"company_name","type":"string","required":true,"description":"Company name."},{"name":"plan_date","type":"date","required":true,"description":"Date of the business plan."},{"name":"executive_summary","type":"text","required":true,"description":"Executive summary."},{"name":"company_overview","type":"text","required":true,"description":"Company overview."},{"name":"products_services","type":"text","required":true,"description":"Products or services offered."},{"name":"market_analysis","type":"text","required":true,"description":"Market analysis."},{"name":"marketing_strategy","type":"text","required":false,"description":"Marketing strategy."},{"name":"operations_plan","type":"text","required":false,"description":"Operations plan."},{"name":"management_team","type":"objectArray","required":false,"description":"Management team bios.","itemFields":[{"name":"name","type":"string","required":true},{"name":"title","type":"string","required":true},{"name":"bio","type":"text","required":false}]},{"name":"financial_projections","type":"objectArray","required":false,"description":"Year-by-year financial projections.","itemFields":[{"name":"year","type":"string","required":true},{"name":"revenue","type":"decimal","required":true},{"name":"expenses","type":"decimal","required":true},{"name":"net_income","type":"decimal","required":true}]},{"name":"funding_required","type":"currency","required":false,"description":"Total funding requested."},{"name":"use_of_funds","type":"text","required":false,"description":"Planned use of funds."}]',
  'business-plan-{{ slug company_name }}-{{ format_date plan_date }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_construction-bid-proposal',
  'document-templates',
  'construction/bid-proposal',
  'Bid Proposal',
  'Construction bid proposal with itemized costs, overhead, contingency, and totals.',
  'BuiltIn',
  '# Bid Proposal

**Contractor:** {{ contractor_name }}
**Prepared for:** {{ client_name }}
**Project:** {{ project_name }}
{{ if project_address && project_address != "" }}**Location:**
{{ project_address }}
{{ end }}
**Bid Date:** {{ format_date bid_date }}
{{ if bid_valid_days > 0 }}**Bid Valid For:** {{ bid_valid_days }} days
{{ end }}

## Scope of Work
{{ scope_of_work }}

## Itemized Bid
| # | Description | Qty | Unit | Unit Price | Amount |
|---|-------------|----:|------|----------:|-------:|
{{ i = 1 }}{{ subtotal = 0 }}{{ for item in line_items }}{{ line_total = item.quantity * item.unit_price }}{{ subtotal = subtotal + line_total }}| {{ i }} | {{ escape_pipes item.description }} | {{ format_number item.quantity }} | {{ escape_pipes item.unit }} | {{ format_money item.unit_price (normalize_currency currency) }} | {{ format_money line_total (normalize_currency currency) }} |
{{ i = i + 1 }}{{ end }}

**Subtotal:** {{ format_money subtotal (normalize_currency currency) }}
{{ ohp_amount = 0 }}{{ if overhead_and_profit_percent > 0 }}{{ ohp_amount = math.round(subtotal * overhead_and_profit_percent / 100, 2) }}**Overhead & Profit ({{ format_percent overhead_and_profit_percent }}):** {{ format_money ohp_amount (normalize_currency currency) }}
{{ end }}{{ contingency_amount = 0 }}{{ if contingency_percent > 0 }}{{ contingency_amount = math.round(subtotal * contingency_percent / 100, 2) }}**Contingency ({{ format_percent contingency_percent }}):** {{ format_money contingency_amount (normalize_currency currency) }}
{{ end }}{{ grand_total = subtotal + ohp_amount + contingency_amount }}**Total Bid Price:** {{ format_money grand_total (normalize_currency currency) }}

{{ if notes && notes != "" }}
## Notes
{{ notes }}

{{ end }}
## Acceptance

Signature of authorized representative: ____________________________  Date: ____________

---
*This bid is based on the information available at the date of submission. Field conditions, permitting delays, or owner-directed changes may affect cost and schedule.*',
  '',
  unixepoch(),
  unixepoch(),
  'construction',
  'StructuredPdf',
  '[{"name":"contractor_name","type":"string","required":true,"description":"Contractor company name."},{"name":"client_name","type":"string","required":true,"description":"Client name."},{"name":"project_name","type":"string","required":true,"description":"Project name."},{"name":"project_address","type":"text","required":false,"description":"Project site address."},{"name":"bid_date","type":"date","required":true,"description":"Date of the bid."},{"name":"bid_valid_days","type":"integer","required":false,"description":"Days the bid is valid. Default 30."},{"name":"scope_of_work","type":"text","required":true,"description":"Scope of work description."},{"name":"line_items","type":"objectArray","required":true,"description":"Itemized cost lines.","itemFields":[{"name":"description","type":"string","required":true},{"name":"quantity","type":"decimal","required":true},{"name":"unit","type":"string","required":true},{"name":"unit_price","type":"decimal","required":true}]},{"name":"overhead_and_profit_percent","type":"decimal","required":false,"description":"Overhead + profit percentage e.g. 15."},{"name":"contingency_percent","type":"decimal","required":false,"description":"Contingency percentage e.g. 5."},{"name":"notes","type":"text","required":false,"description":"Additional notes."},{"name":"currency","type":"string","required":false,"description":"ISO currency code."}]',
  'bid-{{ slug project_name }}-{{ format_date bid_date }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_construction-change-order',
  'document-templates',
  'construction/change-order',
  'Change Order',
  'Construction change order with contract summary, change value, and revised total.',
  'BuiltIn',
  '# Change Order

**Project:** {{ project_name }}
**Change Order Number:** {{ change_order_number }}
**Date:** {{ format_date change_order_date }}

**Owner:** {{ owner_name }}
**Contractor:** {{ contractor_name }}

## Contract Summary
{{ prior_contract = original_contract_sum + net_change_prior_orders }}{{ new_contract = prior_contract + this_change_amount }}
| | Amount |
|---|---:|
| Original Contract Sum | {{ format_money original_contract_sum (normalize_currency currency) }} |
{{ if net_change_prior_orders != 0 }}| Net Change — Prior Approved Orders | {{ format_money net_change_prior_orders (normalize_currency currency) }} |
| Contract Sum Prior to This Change | {{ format_money prior_contract (normalize_currency currency) }} |
{{ end }}| **This Change Order** | **{{ format_money this_change_amount (normalize_currency currency) }}** |
| **Revised Contract Sum** | **{{ format_money new_contract (normalize_currency currency) }}** |

## Reason for Change
{{ reason }}

{{ if scope_changes && scope_changes != "" }}
## Scope Changes
{{ scope_changes }}

{{ end }}
{{ if time_impact_days > 0 }}
## Schedule Impact
This change order extends the contract schedule by **{{ time_impact_days }} day(s)**.

{{ end }}
## Signatures

**Owner:** {{ owner_name }}

Signature: ____________________________  Date: ____________

**Contractor:** {{ contractor_name }}

Signature: ____________________________  Date: ____________',
  '',
  unixepoch(),
  unixepoch(),
  'construction',
  'Word',
  '[{"name":"project_name","type":"string","required":true,"description":"Project name."},{"name":"owner_name","type":"string","required":true,"description":"Owner name."},{"name":"contractor_name","type":"string","required":true,"description":"Contractor name."},{"name":"change_order_number","type":"string","required":true,"description":"Change order number."},{"name":"change_order_date","type":"date","required":true,"description":"Date of the change order."},{"name":"original_contract_sum","type":"currency","required":true,"description":"Original contract amount."},{"name":"net_change_prior_orders","type":"currency","required":false,"description":"Net value of all prior approved change orders."},{"name":"this_change_amount","type":"currency","required":true,"description":"Value of this change order (negative for deductions)."},{"name":"reason","type":"text","required":true,"description":"Reason for the change."},{"name":"scope_changes","type":"text","required":false,"description":"Detailed scope changes."},{"name":"time_impact_days","type":"integer","required":false,"description":"Schedule impact in days."},{"name":"currency","type":"string","required":false,"description":"ISO currency code."}]',
  'change-order-{{ slug project_name }}-{{ slug change_order_number }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_construction-daily-log',
  'document-templates',
  'construction/daily-log',
  'Daily Construction Log',
  'Daily construction log with weather, crews, work performed, and site events.',
  'BuiltIn',
  '# Daily Construction Log

**Project:** {{ project_name }}
**Date:** {{ format_date log_date }}
**Prepared by:** {{ prepared_by }}

## Weather
{{ if weather_am && weather_am != "" }}**AM:** {{ weather_am }}  {{ end }}{{ if weather_pm && weather_pm != "" }}**PM:** {{ weather_pm }}{{ end }}
{{ if temperature_high > 0 || temperature_low > 0 }}**Temp:** {{ temperature_low }}° — {{ temperature_high }}°F{{ end }}

{{ if crews && crews.size > 0 }}
## Crews on Site
| Company | Trade | Count |
|---------|-------|------:|
{{ for c in crews }}| {{ escape_pipes c.company }} | {{ escape_pipes c.trade }} | {{ c.count }} |
{{ end }}

{{ end }}
{{ if equipment_on_site && equipment_on_site.size > 0 }}
## Equipment on Site
{{ for e in equipment_on_site }}- {{ e }}
{{ end }}

{{ end }}
{{ if materials_received && materials_received.size > 0 }}
## Materials Received
{{ for m in materials_received }}- {{ m }}
{{ end }}

{{ end }}
## Work Performed
{{ work_performed }}

{{ if inspections && inspections.size > 0 }}
## Inspections
{{ for i in inspections }}- {{ i }}
{{ end }}

{{ end }}
{{ if visitors && visitors.size > 0 }}
## Visitors
{{ for v in visitors }}- {{ v }}
{{ end }}

{{ end }}
{{ if delays && delays != "" }}
## Delays
{{ delays }}

{{ end }}
{{ if notes && notes != "" }}
## Notes
{{ notes }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'construction',
  'Word',
  '[{"name":"project_name","type":"string","required":true,"description":"Project name."},{"name":"log_date","type":"date","required":true,"description":"Date of the log entry."},{"name":"prepared_by","type":"string","required":true,"description":"Name of person preparing the log."},{"name":"weather_am","type":"string","required":false,"description":"Morning weather conditions."},{"name":"weather_pm","type":"string","required":false,"description":"Afternoon weather conditions."},{"name":"temperature_high","type":"integer","required":false,"description":"High temperature."},{"name":"temperature_low","type":"integer","required":false,"description":"Low temperature."},{"name":"crews","type":"objectArray","required":false,"description":"Crews on site.","itemFields":[{"name":"company","type":"string","required":true},{"name":"trade","type":"string","required":true},{"name":"count","type":"integer","required":false}]},{"name":"equipment_on_site","type":"stringArray","required":false,"description":"Equipment present on site."},{"name":"materials_received","type":"stringArray","required":false,"description":"Materials received today."},{"name":"work_performed","type":"text","required":true,"description":"Summary of work performed today."},{"name":"inspections","type":"stringArray","required":false,"description":"Inspections conducted."},{"name":"visitors","type":"stringArray","required":false,"description":"Visitors to the site."},{"name":"delays","type":"text","required":false,"description":"Delays encountered."},{"name":"notes","type":"text","required":false,"description":"Additional notes."}]',
  'daily-log-{{ slug project_name }}-{{ format_date log_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_construction-punch-list',
  'document-templates',
  'construction/punch-list',
  'Punch List',
  'Construction punch list with location, trade, and status tracking per item.',
  'BuiltIn',
  '# Punch List

**Project:** {{ project_name }}
**Walkthrough Date:** {{ format_date walkthrough_date }}
**Prepared by:** {{ prepared_by }}

## Items
| # | Location | Trade | Description | Assigned To | Due Date | Status |
|---|----------|-------|-------------|-------------|----------|--------|
{{ i = 1 }}{{ for item in items }}| {{ i }} | {{ escape_pipes item.location }} | {{ escape_pipes item.trade }} | {{ escape_pipes item.description }} | {{ if item.assigned_to && item.assigned_to != "" }}{{ escape_pipes item.assigned_to }}{{ end }} | {{ if item.due_date && item.due_date != "" }}{{ format_date item.due_date }}{{ end }} | {{ if item.status && item.status != "" }}{{ escape_pipes item.status }}{{ else }}Open{{ end }} |
{{ i = i + 1 }}{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'construction',
  'Word',
  '[{"name":"project_name","type":"string","required":true,"description":"Project name."},{"name":"walkthrough_date","type":"date","required":true,"description":"Date of the walkthrough."},{"name":"prepared_by","type":"string","required":true,"description":"Name of person preparing the punch list."},{"name":"items","type":"objectArray","required":true,"description":"Punch list items.","itemFields":[{"name":"location","type":"string","required":true},{"name":"trade","type":"string","required":true},{"name":"description","type":"string","required":true},{"name":"assigned_to","type":"string","required":false},{"name":"due_date","type":"date","required":false},{"name":"status","type":"string","required":false}]}]',
  'punch-list-{{ slug project_name }}-{{ format_date walkthrough_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_construction-safety-report',
  'document-templates',
  'construction/safety-report',
  'Safety Incident Report',
  'Construction safety incident report with narrative, corrective actions, and witnesses.',
  'BuiltIn',
  '# Safety Incident Report

**Project:** {{ project_name }}
**Incident Date:** {{ format_date incident_date }}{{ if incident_time && incident_time != "" }}  **Time:** {{ incident_time }}{{ end }}
**Reported by:** {{ reported_by }}

**Incident Type:** {{ incident_type }}
**Severity:** {{ severity }}
{{ if location_on_site && location_on_site != "" }}**Location on Site:** {{ location_on_site }}
{{ end }}

{{ if persons_involved && persons_involved.size > 0 }}
## Persons Involved
{{ for p in persons_involved }}- {{ p }}
{{ end }}

{{ end }}
## Incident Narrative
{{ narrative }}

{{ if immediate_actions && immediate_actions != "" }}
## Immediate Actions Taken
{{ immediate_actions }}

{{ end }}
{{ if corrective_actions && corrective_actions.size > 0 }}
## Corrective Actions
| Action | Assigned To | Due Date | Status |
|--------|-------------|----------|--------|
{{ for a in corrective_actions }}| {{ escape_pipes a.action }} | {{ if a.assigned_to && a.assigned_to != "" }}{{ escape_pipes a.assigned_to }}{{ end }} | {{ if a.due_date && a.due_date != "" }}{{ format_date a.due_date }}{{ end }} | {{ if a.status && a.status != "" }}{{ escape_pipes a.status }}{{ else }}Open{{ end }} |
{{ end }}

{{ end }}
{{ if witnesses && witnesses.size > 0 }}
## Witnesses
{{ for w in witnesses }}- {{ w }}
{{ end }}
{{ end }}

## Signature

**Reported by:** {{ reported_by }}

Signature: ____________________________  Date: ____________',
  '',
  unixepoch(),
  unixepoch(),
  'construction',
  'Word',
  '[{"name":"project_name","type":"string","required":true,"description":"Project name."},{"name":"incident_date","type":"date","required":true,"description":"Date of the incident."},{"name":"incident_time","type":"string","required":false,"description":"Time of the incident."},{"name":"incident_type","type":"string","required":true,"description":"e.g. Near Miss, First Aid, Recordable."},{"name":"severity","type":"string","required":true,"description":"LOW, MEDIUM, HIGH, CRITICAL"},{"name":"location_on_site","type":"string","required":false,"description":"Location on site where incident occurred."},{"name":"reported_by","type":"string","required":true,"description":"Name of person reporting."},{"name":"persons_involved","type":"stringArray","required":false,"description":"Names of persons involved."},{"name":"narrative","type":"text","required":true,"description":"Detailed narrative of what happened."},{"name":"immediate_actions","type":"text","required":false,"description":"Immediate corrective actions taken."},{"name":"corrective_actions","type":"objectArray","required":false,"description":"Follow-up corrective actions.","itemFields":[{"name":"action","type":"string","required":true},{"name":"assigned_to","type":"string","required":false},{"name":"due_date","type":"date","required":false},{"name":"status","type":"string","required":false}]},{"name":"witnesses","type":"stringArray","required":false,"description":"Witness names."}]',
  'safety-report-{{ slug project_name }}-{{ format_date incident_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_education-iep',
  'document-templates',
  'education/iep',
  'Individualized Education Program',
  'IEP document with present levels, annual goals, services, and accommodations.',
  'BuiltIn',
  '# Individualized Education Program

**Student:** {{ student_name }}
**Date of Birth:** {{ format_date date_of_birth }}
**Grade Level:** {{ grade_level }}
{{ if school_name && school_name != "" }}**School:** {{ school_name }}
{{ end }}**IEP Date:** {{ format_date iep_date }}
**Disability Category:** {{ disability_category }}

## Present Levels of Academic Achievement and Functional Performance
{{ present_levels }}

## Annual Goals
{{ i = 1 }}{{ for g in annual_goals }}
### Goal {{ i }}: {{ g.goal }}

**Measurement:** {{ g.measurement }}
{{ if g.review_date && g.review_date != "" }}**Review Date:** {{ format_date g.review_date }}{{ end }}

{{ i = i + 1 }}{{ end }}

## Services
| Service | Frequency | Location | Provider |
|---------|-----------|----------|----------|
{{ for s in services }}| {{ escape_pipes s.service }} | {{ escape_pipes s.frequency }} | {{ if s.location && s.location != "" }}{{ escape_pipes s.location }}{{ end }} | {{ if s.provider && s.provider != "" }}{{ escape_pipes s.provider }}{{ end }} |
{{ end }}

{{ if accommodations && accommodations.size > 0 }}
## Accommodations
{{ for a in accommodations }}- {{ a }}
{{ end }}

{{ end }}
{{ if team_members && team_members.size > 0 }}
## IEP Team Members
| Name | Role |
|------|------|
{{ for m in team_members }}| {{ escape_pipes m.name }} | {{ escape_pipes m.role }} |
{{ end }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'education',
  'Word',
  '[{"name":"student_name","type":"string","required":true,"description":"Student full name."},{"name":"date_of_birth","type":"date","required":true,"description":"Student date of birth."},{"name":"grade_level","type":"string","required":true,"description":"Current grade level."},{"name":"school_name","type":"string","required":false,"description":"School name."},{"name":"iep_date","type":"date","required":true,"description":"Date of the IEP."},{"name":"disability_category","type":"string","required":true,"description":"Disability category."},{"name":"present_levels","type":"text","required":true,"description":"Current academic performance levels."},{"name":"annual_goals","type":"objectArray","required":true,"description":"Annual IEP goals.","itemFields":[{"name":"goal","type":"string","required":true},{"name":"measurement","type":"string","required":true},{"name":"review_date","type":"date","required":false}]},{"name":"services","type":"objectArray","required":true,"description":"Special education services.","itemFields":[{"name":"service","type":"string","required":true},{"name":"frequency","type":"string","required":true},{"name":"location","type":"string","required":false},{"name":"provider","type":"string","required":false}]},{"name":"accommodations","type":"stringArray","required":false,"description":"Accommodations and modifications."},{"name":"team_members","type":"objectArray","required":false,"description":"IEP team members.","itemFields":[{"name":"name","type":"string","required":true},{"name":"role","type":"string","required":true}]}]',
  'iep-{{ slug student_name }}-{{ format_date iep_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_education-lesson-plan',
  'document-templates',
  'education/lesson-plan',
  'Lesson Plan',
  'Lesson plan with objectives, materials, procedure phases, and assessment.',
  'BuiltIn',
  '# Lesson Plan: {{ lesson_title }}

**Teacher:** {{ teacher_name }}
**Subject:** {{ subject }}
**Date:** {{ format_date lesson_date }}
{{ if grade_level && grade_level != "" }}**Grade Level:** {{ grade_level }}
{{ end }}{{ if duration_minutes > 0 }}**Duration:** {{ duration_minutes }} minutes
{{ end }}

## Learning Objectives
{{ for o in objectives }}- {{ o }}
{{ end }}

{{ if materials && materials.size > 0 }}
## Materials
{{ for m in materials }}- {{ m }}
{{ end }}

{{ end }}
## Procedure
| Phase | Time (min) | Description |
|-------|----------:|-------------|
{{ for p in procedure }}| {{ escape_pipes p.phase }} | {{ if p.minutes > 0 }}{{ p.minutes }}{{ end }} | {{ escape_pipes p.description }} |
{{ end }}

{{ if assessment && assessment != "" }}
## Assessment
{{ assessment }}

{{ end }}
{{ if notes && notes != "" }}
## Notes
{{ notes }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'education',
  'Word',
  '[{"name":"teacher_name","type":"string","required":true,"description":"Teacher name."},{"name":"subject","type":"string","required":true,"description":"Subject area."},{"name":"lesson_title","type":"string","required":true,"description":"Lesson title."},{"name":"lesson_date","type":"date","required":true,"description":"Date of the lesson."},{"name":"grade_level","type":"string","required":false,"description":"Grade level."},{"name":"duration_minutes","type":"integer","required":false,"description":"Lesson duration in minutes."},{"name":"objectives","type":"stringArray","required":true,"description":"Learning objectives."},{"name":"materials","type":"stringArray","required":false,"description":"Materials needed."},{"name":"procedure","type":"objectArray","required":true,"description":"Lesson procedure phases.","itemFields":[{"name":"phase","type":"string","required":true},{"name":"minutes","type":"integer","required":false},{"name":"description","type":"text","required":true}]},{"name":"assessment","type":"text","required":false,"description":"How mastery will be assessed."},{"name":"notes","type":"text","required":false,"description":"Additional notes."}]',
  'lesson-{{ slug lesson_title }}-{{ format_date lesson_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_education-report-card',
  'document-templates',
  'education/report-card',
  'Report Card',
  'Student report card with subject grades, attendance, and teacher comments.',
  'BuiltIn',
  '# Report Card

**School:** {{ school_name }}
**Student:** {{ student_name }}
**Grade Level:** {{ grade_level }}
**Term:** {{ term }}
**Report Date:** {{ format_date report_date }}
{{ if teacher_name && teacher_name != "" }}**Teacher:** {{ teacher_name }}
{{ end }}

## Grades
| Subject | Grade | Effort | Comments |
|---------|-------|--------|----------|
{{ for s in subjects }}| {{ escape_pipes s.subject }} | {{ escape_pipes s.grade }} | {{ if s.effort && s.effort != "" }}{{ escape_pipes s.effort }}{{ end }} | {{ if s.comments && s.comments != "" }}{{ escape_pipes s.comments }}{{ end }} |
{{ end }}

{{ if attendance_days_present > 0 || attendance_days_absent > 0 }}
## Attendance
**Days Present:** {{ attendance_days_present }}  **Days Absent:** {{ attendance_days_absent }}

{{ end }}
{{ if overall_comments && overall_comments != "" }}
## Teacher Comments
{{ overall_comments }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'education',
  'Word',
  '[{"name":"school_name","type":"string","required":true,"description":"School name."},{"name":"student_name","type":"string","required":true,"description":"Student full name."},{"name":"grade_level","type":"string","required":true,"description":"Grade level."},{"name":"term","type":"string","required":true,"description":"e.g. Fall 2025"},{"name":"report_date","type":"date","required":true,"description":"Date of the report card."},{"name":"teacher_name","type":"string","required":false,"description":"Teacher name."},{"name":"subjects","type":"objectArray","required":true,"description":"Subject grades.","itemFields":[{"name":"subject","type":"string","required":true},{"name":"grade","type":"string","required":true},{"name":"effort","type":"string","required":false},{"name":"comments","type":"text","required":false}]},{"name":"attendance_days_present","type":"integer","required":false,"description":"Days present."},{"name":"attendance_days_absent","type":"integer","required":false,"description":"Days absent."},{"name":"overall_comments","type":"text","required":false,"description":"Overall teacher comments."}]',
  'report-card-{{ slug student_name }}-{{ slug term }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_education-syllabus',
  'document-templates',
  'education/syllabus',
  'Course Syllabus',
  'Course syllabus with objectives, grading breakdown, policies, and weekly schedule.',
  'BuiltIn',
  '# {{ course_code }}: {{ course_title }}

**Instructor:** {{ instructor_name }}{{ if instructor_email && instructor_email != "" }}  ({{ instructor_email }}){{ end }}
**Term:** {{ term }}
{{ if credit_hours > 0 }}**Credit Hours:** {{ credit_hours }}
{{ end }}{{ if meeting_times && meeting_times != "" }}**Meeting Times:** {{ meeting_times }}
{{ end }}{{ if office_hours && office_hours != "" }}**Office Hours:** {{ office_hours }}
{{ end }}

## Course Description
{{ course_description }}

## Learning Objectives
{{ for o in learning_objectives }}- {{ o }}
{{ end }}

{{ if required_materials && required_materials.size > 0 }}
## Required Materials
{{ for m in required_materials }}- {{ m }}
{{ end }}

{{ end }}
## Grading
| Component | Weight | Description |
|-----------|-------:|-------------|
{{ total_weight = 0 }}{{ for g in grading_breakdown }}{{ total_weight = total_weight + g.weight }}| {{ escape_pipes g.component }} | {{ format_percent g.weight }} | {{ if g.description && g.description != "" }}{{ escape_pipes g.description }}{{ end }} |
{{ end }}| **Total** | **{{ format_percent total_weight }}** | |

{{ if course_policies && course_policies != "" }}
## Course Policies
{{ course_policies }}

{{ end }}
{{ if schedule && schedule.size > 0 }}
## Course Schedule
| Week | Topic | Readings | Assignments |
|------|-------|----------|-------------|
{{ for w in schedule }}| {{ escape_pipes w.week }} | {{ escape_pipes w.topic }} | {{ if w.readings && w.readings != "" }}{{ escape_pipes w.readings }}{{ end }} | {{ if w.assignments && w.assignments != "" }}{{ escape_pipes w.assignments }}{{ end }} |
{{ end }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'education',
  'Word',
  '[{"name":"course_code","type":"string","required":true,"description":"Course code e.g. ENGL-101."},{"name":"course_title","type":"string","required":true,"description":"Course title."},{"name":"instructor_name","type":"string","required":true,"description":"Instructor name."},{"name":"instructor_email","type":"string","required":false,"description":"Instructor email."},{"name":"term","type":"string","required":true,"description":"Term e.g. Fall 2025."},{"name":"credit_hours","type":"integer","required":false,"description":"Number of credit hours."},{"name":"meeting_times","type":"string","required":false,"description":"Meeting days and times."},{"name":"office_hours","type":"string","required":false,"description":"Office hours."},{"name":"course_description","type":"text","required":true,"description":"Course description."},{"name":"learning_objectives","type":"stringArray","required":true,"description":"Learning objectives."},{"name":"required_materials","type":"stringArray","required":false,"description":"Required materials and texts."},{"name":"grading_breakdown","type":"objectArray","required":true,"description":"Grading components.","itemFields":[{"name":"component","type":"string","required":true},{"name":"weight","type":"decimal","required":true,"description":"Percentage weight e.g. 30 for 30%."},{"name":"description","type":"text","required":false}]},{"name":"course_policies","type":"text","required":false,"description":"Course policies."},{"name":"schedule","type":"objectArray","required":false,"description":"Weekly schedule.","itemFields":[{"name":"week","type":"string","required":true},{"name":"topic","type":"string","required":true},{"name":"readings","type":"string","required":false},{"name":"assignments","type":"string","required":false}]}]',
  'syllabus-{{ slug course_code }}-{{ slug term }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_education-transcript',
  'document-templates',
  'education/transcript',
  'Academic Transcript',
  'Official academic transcript with term-by-term course history, grades, and GPA.',
  'BuiltIn',
  '# Academic Transcript

**Institution:** {{ institution_name }}
**Student:** {{ student_name }}
{{ if student_id && student_id != "" }}**Student ID:** {{ student_id }}
{{ end }}{{ if date_of_birth && date_of_birth != "" }}**Date of Birth:** {{ format_date date_of_birth }}
{{ end }}{{ if program && program != "" }}**Program:** {{ program }}
{{ end }}{{ if enrollment_status && enrollment_status != "" }}**Enrollment Status:** {{ enrollment_status }}
{{ end }}**Issue Date:** {{ format_date issue_date }}

---

{{ for t in terms }}
## {{ t.term_name }}

| Course Code | Course Title | Credits | Grade |
|-------------|-------------|--------:|-------|
{{ for c in t.courses }}| {{ escape_pipes c.course_code }} | {{ escape_pipes c.course_title }} | {{ format_number c.credits }} | {{ escape_pipes c.grade }} |
{{ end }}

{{ end }}

---

## Summary
{{ if cumulative_gpa > 0 }}**Cumulative GPA:** {{ format_number cumulative_gpa }}
{{ end }}{{ if cumulative_credits > 0 }}**Cumulative Credits:** {{ format_number cumulative_credits }}
{{ end }}

*This transcript is an official document of {{ institution_name }}.*',
  '',
  unixepoch(),
  unixepoch(),
  'education',
  'StructuredPdf',
  '[{"name":"institution_name","type":"string","required":true,"description":"Institution name."},{"name":"student_name","type":"string","required":true,"description":"Student full name."},{"name":"student_id","type":"string","required":false,"description":"Student ID number."},{"name":"date_of_birth","type":"date","required":false,"description":"Student date of birth."},{"name":"program","type":"string","required":false,"description":"Degree program name."},{"name":"enrollment_status","type":"string","required":false,"description":"Current enrollment status."},{"name":"issue_date","type":"date","required":true,"description":"Date of transcript issuance."},{"name":"terms","type":"objectArray","required":true,"description":"Academic terms.","itemFields":[{"name":"term_name","type":"string","required":true},{"name":"courses","type":"objectArray","required":true,"itemFields":[{"name":"course_code","type":"string","required":true},{"name":"course_title","type":"string","required":true},{"name":"credits","type":"decimal","required":true},{"name":"grade","type":"string","required":true}]}]},{"name":"cumulative_gpa","type":"decimal","required":false,"description":"Cumulative GPA."},{"name":"cumulative_credits","type":"decimal","required":false,"description":"Total cumulative credits."}]',
  'transcript-{{ slug student_name }}-{{ format_date issue_date }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_finance-invoice',
  'document-templates',
  'finance/invoice',
  'Invoice',
  'Professional invoice with line items, tax, and totals.',
  'BuiltIn',
  '# Invoice {{ invoice_number }}

**Issue date:** {{ format_date issue_date }}{{ if due_date && due_date != "" }}
**Due date:** {{ format_date due_date }}{{ end }}

## From
{{ seller }}

## Bill to
{{ buyer }}

## Line items

| Description | Qty | Unit price | Subtotal |
|---|---:|---:|---:|
{{ gross_total = 0 }}{{ for item in line_items }}{{ line_total = item.quantity * item.unit_price }}{{ gross_total = gross_total + line_total }}| {{ escape_pipes item.description }} | {{ format_number item.quantity }} | {{ format_money item.unit_price (normalize_currency currency) }} | {{ format_money line_total (normalize_currency currency) }} |
{{ end }}

## Totals

| | |
|---|---:|
| Subtotal | {{ format_money gross_total (normalize_currency currency) }} |
{{ if tax_rate > 0 }}{{ tax_amount = math.round(gross_total * tax_rate / 100, 2) }}| Tax ({{ format_percent tax_rate }}) | {{ format_money tax_amount (normalize_currency currency) }} |
{{ tax_amount_final = tax_amount }}{{ else }}{{ tax_amount_final = 0 }}{{ end }}{{ grand_total = gross_total + tax_amount_final }}| **Total** | **{{ format_money grand_total (normalize_currency currency) }}** |

{{ if payment_terms && payment_terms != "" }}
## Payment terms
{{ payment_terms }}

{{ end }}
{{ if notes && notes != "" }}
## Notes
{{ notes }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'finance',
  'StructuredPdf',
  '[{"name":"invoice_number","type":"string","required":true,"description":"Unique invoice identifier, e.g. INV-2026-001."},{"name":"issue_date","type":"date","required":true,"description":"ISO date the invoice was issued."},{"name":"due_date","type":"date","required":false,"description":"ISO date payment is due."},{"name":"currency","type":"string","required":false,"description":"ISO currency code (default USD)."},{"name":"seller","type":"text","required":true,"description":"Seller block — business name and address (multiline)."},{"name":"buyer","type":"text","required":true,"description":"Buyer block — billed-to name and address (multiline)."},{"name":"line_items","type":"objectArray","required":true,"description":"One row per billable item.","itemFields":[{"name":"description","type":"string","required":true},{"name":"quantity","type":"decimal","required":true},{"name":"unit_price","type":"decimal","required":true}]},{"name":"tax_rate","type":"decimal","required":false,"description":"Tax rate as a percentage, e.g. 8.25 for 8.25%."},{"name":"payment_terms","type":"text","required":false,"description":"Payment instructions or terms."},{"name":"notes","type":"text","required":false,"description":"Additional notes shown below totals."}]',
  'invoice-{{ slug invoice_number }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_finance-budget-report',
  'document-templates',
  'finance/budget-report',
  'Budget Report',
  'Budget vs. actuals report with per-category variance.',
  'BuiltIn',
  '# Budget Report — {{ owner }}
## Period: {{ period }}

| Category | Planned | Actual | Variance | Variance % |
|---|---:|---:|---:|---:|
{{ total_planned = 0 }}{{ total_actual = 0 }}{{ for item in categories }}{{ variance = item.actual - item.planned }}{{ variance_pct = 0 }}{{ if item.planned != 0 }}{{ variance_pct = math.round((variance / item.planned) * 100, 1) }}{{ end }}{{ total_planned = total_planned + item.planned }}{{ total_actual = total_actual + item.actual }}| {{ escape_pipes item.category }} | {{ format_money item.planned (normalize_currency currency) }} | {{ format_money item.actual (normalize_currency currency) }} | {{ format_money variance (normalize_currency currency) }} | {{ format_percent variance_pct }} |
{{ end }}{{ total_variance = total_actual - total_planned }}{{ total_variance_pct = 0 }}{{ if total_planned != 0 }}{{ total_variance_pct = math.round((total_variance / total_planned) * 100, 1) }}{{ end }}| **Total** | **{{ format_money total_planned (normalize_currency currency) }}** | **{{ format_money total_actual (normalize_currency currency) }}** | **{{ format_money total_variance (normalize_currency currency) }}** | **{{ format_percent total_variance_pct }}** |

{{ if narrative && narrative != "" }}
## Narrative
{{ narrative }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'finance',
  'StructuredPdf',
  '[{"name":"owner","type":"string","required":true,"description":"Department, team, or project name."},{"name":"period","type":"string","required":true,"description":"Reporting period, e.g. April 2026."},{"name":"currency","type":"string","required":false,"description":"ISO currency code."},{"name":"categories","type":"objectArray","required":true,"description":"Budget categories.","itemFields":[{"name":"category","type":"string","required":true},{"name":"planned","type":"decimal","required":true},{"name":"actual","type":"decimal","required":true}]},{"name":"narrative","type":"text","required":false,"description":"Narrative explanation of variances."}]',
  'budget-{{ slug owner }}-{{ slug period }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_finance-audit-report',
  'document-templates',
  'finance/audit-report',
  'Audit Report',
  'Internal or external audit report with findings grouped by severity.',
  'BuiltIn',
  '# Audit Report — {{ entity_name }}
## {{ audit_type }} — {{ period }}

**Auditor:** {{ auditor_name }}
**Report Date:** {{ format_date report_date }}

## Scope
{{ scope }}

{{ if executive_summary && executive_summary != "" }}
## Executive Summary
{{ executive_summary }}

{{ end }}
## Findings

{{ for finding in findings }}
### Severity: {{ finding.severity }}

**{{ escape_pipes finding.title }}**

{{ finding.description }}

{{ if finding.recommendation && finding.recommendation != "" }}
*Recommendation:* {{ finding.recommendation }}

{{ end }}
{{ end }}
{{ if conclusion && conclusion != "" }}
## Conclusion
{{ conclusion }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'finance',
  'StructuredPdf',
  '[{"name":"entity_name","type":"string","required":true,"description":"Entity being audited."},{"name":"audit_type","type":"string","required":true,"description":"e.g. Internal, External, IT."},{"name":"period","type":"string","required":true,"description":"Audit period."},{"name":"auditor_name","type":"string","required":true,"description":"Auditor name."},{"name":"report_date","type":"date","required":true,"description":"Report date."},{"name":"scope","type":"text","required":true,"description":"Audit scope and objectives."},{"name":"methodology","type":"text","required":false,"description":"Audit methodology."},{"name":"findings","type":"objectArray","required":true,"description":"Audit findings.","itemFields":[{"name":"title","type":"string","required":true},{"name":"severity","type":"string","required":true,"description":"HIGH, MEDIUM, LOW, INFO"},{"name":"description","type":"text","required":true},{"name":"recommendation","type":"text","required":false}]},{"name":"executive_summary","type":"text","required":false,"description":"Executive summary."},{"name":"conclusion","type":"text","required":false,"description":"Audit conclusion."}]',
  'audit-{{ slug entity_name }}-{{ slug period }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_finance-financial-statement',
  'document-templates',
  'finance/financial-statement',
  'Financial Statement',
  'Combined financial statement with income statement, balance sheet, and cash flow.',
  'BuiltIn',
  '# Financial Statement — {{ entity_name }}
## Period: {{ period }}

{{ if prepared_by && prepared_by != "" }}**Prepared by:** {{ prepared_by }}
{{ end }}

## Income Statement
| Label | Amount |
|-------|-------:|
{{ income_total = 0 }}{{ for row in income_statement }}{{ income_total = income_total + row.amount }}| {{ escape_pipes row.label }} | {{ format_money row.amount "" }} |
{{ end }}| **Net Income** | **{{ format_money income_total "" }}** |

## Balance Sheet
{{ last_section = "" }}{{ for row in balance_sheet }}{{ if row.section != last_section }}
### {{ row.section }}
{{ last_section = row.section }}{{ end }}| {{ escape_pipes row.label }} | {{ format_money row.amount "" }} |
{{ end }}

## Cash Flow Statement
{{ last_cf_section = "" }}{{ for row in cash_flow }}{{ if row.section != last_cf_section }}
### {{ row.section }}
{{ last_cf_section = row.section }}{{ end }}| {{ escape_pipes row.label }} | {{ format_money row.amount "" }} |
{{ end }}

{{ if notes && notes != "" }}
## Notes
{{ notes }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'finance',
  'StructuredPdf',
  '[{"name":"entity_name","type":"string","required":true,"description":"Entity name."},{"name":"period","type":"string","required":true,"description":"Reporting period."},{"name":"prepared_by","type":"string","required":false,"description":"Preparer name."},{"name":"notes","type":"text","required":false,"description":"Financial notes."},{"name":"income_statement","type":"objectArray","required":true,"description":"Income statement line items.","itemFields":[{"name":"label","type":"string","required":true},{"name":"amount","type":"decimal","required":true}]},{"name":"balance_sheet","type":"objectArray","required":true,"description":"Balance sheet line items.","itemFields":[{"name":"section","type":"string","required":true},{"name":"label","type":"string","required":true},{"name":"amount","type":"decimal","required":true}]},{"name":"cash_flow","type":"objectArray","required":true,"description":"Cash flow line items.","itemFields":[{"name":"section","type":"string","required":true},{"name":"label","type":"string","required":true},{"name":"amount","type":"decimal","required":true}]}]',
  'financial-statement-{{ slug entity_name }}-{{ slug period }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_finance-proposal',
  'document-templates',
  'finance/proposal',
  'Business Proposal',
  'Business proposal with summary, pricing table, timeline, and acceptance block.',
  'BuiltIn',
  '# {{ proposal_title }}

**Prepared for:** {{ prepared_for }}
**Prepared by:** {{ prepared_by }}
**Date:** {{ format_date proposal_date }}

## Executive Summary
{{ executive_summary }}

{{ if problem_statement && problem_statement != "" }}
## Problem Statement
{{ problem_statement }}

{{ end }}
## Proposed Solution
{{ proposed_solution }}

## Pricing

| Item | Qty | Unit Price | Subtotal |
|------|---:|---:|---:|
{{ total = 0 }}{{ for item in pricing }}{{ qty = item.quantity }}{{ if qty == 0 }}{{ qty = 1 }}{{ end }}{{ line_sub = qty * item.unit_price }}{{ total = total + line_sub }}| {{ escape_pipes item.item }} | {{ qty }} | {{ format_money item.unit_price (normalize_currency currency) }} | {{ format_money line_sub (normalize_currency currency) }} |
{{ end }}| **Total** | | | **{{ format_money total (normalize_currency currency) }}** |

{{ if timeline && timeline.size > 0 }}
## Timeline
| Phase | Duration | Deliverables |
|-------|----------|-------------|
{{ for t in timeline }}| {{ escape_pipes t.phase }} | {{ escape_pipes t.duration }} | {{ if t.deliverables && t.deliverables != "" }}{{ escape_pipes t.deliverables }}{{ end }} |
{{ end }}

{{ end }}
{{ if terms && terms != "" }}
## Terms
{{ terms }}

{{ end }}
## Acceptance

Accepted by {{ prepared_for }}:

Signature: ____________________________

Name / Title: ____________________________

Date: ____________________________',
  '',
  unixepoch(),
  unixepoch(),
  'finance',
  'StructuredPdf',
  '[{"name":"proposal_title","type":"string","required":true,"description":"Proposal title."},{"name":"prepared_for","type":"string","required":true,"description":"Client name."},{"name":"prepared_by","type":"string","required":true,"description":"Vendor or consultant name."},{"name":"proposal_date","type":"date","required":true,"description":"Proposal date."},{"name":"executive_summary","type":"text","required":true,"description":"Executive summary."},{"name":"problem_statement","type":"text","required":false,"description":"Problem statement."},{"name":"proposed_solution","type":"text","required":true,"description":"Proposed solution."},{"name":"pricing","type":"objectArray","required":true,"description":"Pricing line items.","itemFields":[{"name":"item","type":"string","required":true},{"name":"quantity","type":"integer","required":true},{"name":"unit_price","type":"decimal","required":true}]},{"name":"timeline","type":"objectArray","required":false,"description":"Project timeline phases.","itemFields":[{"name":"phase","type":"string","required":true},{"name":"duration","type":"string","required":true},{"name":"deliverables","type":"text","required":false}]},{"name":"terms","type":"text","required":false,"description":"Terms and conditions."},{"name":"currency","type":"string","required":false,"description":"ISO currency code."}]',
  'proposal-{{ slug prepared_for }}-{{ slug proposal_title }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_healthcare-care-plan',
  'document-templates',
  'healthcare/care-plan',
  'Care Plan',
  'Patient care plan with diagnoses, problems, goals, interventions, and follow-up.',
  'BuiltIn',
  '# Care Plan

**Patient:** {{ patient_name }}
**Date of Birth:** {{ format_date dob }}
**Provider:** {{ provider_name }}
**Plan Date:** {{ format_date plan_date }}

## Diagnoses
{{ for d in diagnoses }}- {{ d }}
{{ end }}

{{ if allergies && allergies.size > 0 }}
## Allergies
{{ for a in allergies }}- {{ a }}
{{ end }}

{{ end }}
{{ if current_medications && current_medications.size > 0 }}
## Current Medications
{{ for m in current_medications }}- {{ m }}
{{ end }}

{{ end }}
## Problems, Goals & Interventions
{{ for p in problems }}
### {{ p.problem }}

**Goal:** {{ p.goal }}

{{ if p.interventions && p.interventions.size > 0 }}**Interventions:**
{{ for i in p.interventions }}- {{ i }}
{{ end }}{{ end }}
{{ end }}
{{ if follow_up_date && follow_up_date != "" }}
## Follow-up
**Follow-up Date:** {{ format_date follow_up_date }}

{{ end }}
{{ if notes && notes != "" }}
## Notes
{{ notes }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'healthcare',
  'Word',
  '[{"name":"patient_name","type":"string","required":true,"description":"Patient full name."},{"name":"dob","type":"date","required":true,"description":"Patient date of birth."},{"name":"provider_name","type":"string","required":true,"description":"Provider name."},{"name":"plan_date","type":"date","required":true,"description":"Date of the care plan."},{"name":"diagnoses","type":"stringArray","required":true,"description":"ICD codes or plain descriptions."},{"name":"allergies","type":"stringArray","required":false,"description":"Known allergies."},{"name":"current_medications","type":"stringArray","required":false,"description":"Current medications."},{"name":"problems","type":"objectArray","required":true,"description":"Problem list with goals and interventions.","itemFields":[{"name":"problem","type":"string","required":true},{"name":"goal","type":"string","required":true},{"name":"interventions","type":"stringArray","required":false}]},{"name":"follow_up_date","type":"date","required":false,"description":"Follow-up appointment date."},{"name":"notes","type":"text","required":false,"description":"Additional notes."}]',
  'care-plan-{{ slug patient_name }}-{{ format_date plan_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_healthcare-discharge-summary',
  'document-templates',
  'healthcare/discharge-summary',
  'Discharge Summary',
  'Hospital discharge summary with admission, course, discharge diagnoses, and follow-up instructions.',
  'BuiltIn',
  '# Discharge Summary

**Patient:** {{ patient_name }}
**Date of Birth:** {{ format_date dob }}
**Attending Physician:** {{ attending_physician }}
{{ if hospital_name && hospital_name != "" }}**Hospital:** {{ hospital_name }}
{{ end }}**Admission Date:** {{ format_date admission_date }}
**Discharge Date:** {{ format_date discharge_date }}

## Admitting Diagnosis
{{ admitting_diagnosis }}

## Hospital Course
{{ hospital_course }}

## Discharge Diagnosis
{{ discharge_diagnosis }}

{{ if discharge_condition && discharge_condition != "" }}
**Discharge Condition:** {{ discharge_condition }}

{{ end }}
{{ if discharge_medications && discharge_medications.size > 0 }}
## Discharge Medications
{{ for m in discharge_medications }}- {{ m }}
{{ end }}

{{ end }}
{{ if follow_up_instructions && follow_up_instructions != "" }}
## Follow-up Instructions
{{ follow_up_instructions }}

{{ end }}
{{ if follow_up_appointments && follow_up_appointments.size > 0 }}
## Follow-up Appointments
{{ for a in follow_up_appointments }}- {{ a }}
{{ end }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'healthcare',
  'Word',
  '[{"name":"patient_name","type":"string","required":true,"description":"Patient full name."},{"name":"dob","type":"date","required":true,"description":"Patient date of birth."},{"name":"attending_physician","type":"string","required":true,"description":"Attending physician name."},{"name":"hospital_name","type":"string","required":false,"description":"Hospital name."},{"name":"admission_date","type":"date","required":true,"description":"Admission date."},{"name":"discharge_date","type":"date","required":true,"description":"Discharge date."},{"name":"admitting_diagnosis","type":"text","required":true,"description":"Diagnosis at admission."},{"name":"hospital_course","type":"text","required":true,"description":"Summary of care provided during admission."},{"name":"discharge_diagnosis","type":"text","required":true,"description":"Diagnosis at discharge."},{"name":"discharge_condition","type":"string","required":false,"description":"Patient condition at discharge."},{"name":"discharge_medications","type":"stringArray","required":false,"description":"Medications at discharge."},{"name":"follow_up_instructions","type":"text","required":false,"description":"Follow-up instructions."},{"name":"follow_up_appointments","type":"stringArray","required":false,"description":"Scheduled follow-up appointments."}]',
  'discharge-{{ slug patient_name }}-{{ format_date discharge_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_healthcare-hipaa-authorization',
  'document-templates',
  'healthcare/hipaa-authorization',
  'HIPAA Authorization',
  'HIPAA-compliant authorization for release of protected health information.',
  'BuiltIn',
  '# HIPAA Authorization for Release of Health Information

**Patient Name:** {{ patient_name }}
**Date of Birth:** {{ format_date date_of_birth }}

**Releasing Entity:** {{ releasing_entity }}
**Receiving Party:** {{ receiving_party }}

## Purpose of Disclosure
{{ purpose_of_disclosure }}

## Information to be Released
{{ for item in information_to_release }}- {{ item }}
{{ end }}

**Authorization Date:** {{ format_date authorization_date }}
{{ if expiration_date && expiration_date != "" }}**Expiration Date:** {{ format_date expiration_date }}
{{ end }}

{{ if right_to_revoke }}
## Right to Revoke
You have the right to revoke this authorization at any time by submitting a written request to {{ releasing_entity }}. Revocation will not affect any actions taken prior to receipt of the revocation.

{{ end }}
## Patient Signature

I authorize the release of my health information as described above.

**Patient / Authorized Representative:** ____________________________

**Signature:** ____________________________

**Date:** ____________________________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal or medical advice. Review with qualified counsel before use.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'healthcare',
  'Word',
  '[{"name":"patient_name","type":"string","required":true,"description":"Patient full name."},{"name":"date_of_birth","type":"date","required":true,"description":"Patient date of birth."},{"name":"releasing_entity","type":"string","required":true,"description":"Entity releasing records."},{"name":"receiving_party","type":"string","required":true,"description":"Party receiving records."},{"name":"purpose_of_disclosure","type":"text","required":true,"description":"Purpose of the disclosure."},{"name":"information_to_release","type":"stringArray","required":true,"description":"Types of records to release."},{"name":"authorization_date","type":"date","required":true,"description":"Date of authorization."},{"name":"expiration_date","type":"date","required":false,"description":"Date authorization expires."},{"name":"right_to_revoke","type":"boolean","required":false,"description":"Whether to include right-to-revoke language."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'hipaa-auth-{{ slug patient_name }}-{{ format_date authorization_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_healthcare-patient-intake',
  'document-templates',
  'healthcare/patient-intake',
  'Patient Intake Form',
  'New patient intake form with demographics, insurance, medical history, and reason for visit.',
  'BuiltIn',
  '# Patient Intake Form

**Practice:** {{ practice_name }}
**Intake Date:** {{ format_date intake_date }}

## Patient Information
**Name:** {{ patient_name }}
**Date of Birth:** {{ format_date date_of_birth }}
{{ if gender && gender != "" }}**Gender:** {{ gender }}
{{ end }}{{ if address && address != "" }}**Address:** {{ address }}
{{ end }}{{ if phone && phone != "" }}**Phone:** {{ phone }}
{{ end }}{{ if email && email != "" }}**Email:** {{ email }}
{{ end }}

{{ if emergency_contact_name && emergency_contact_name != "" }}
## Emergency Contact
**Name:** {{ emergency_contact_name }}
{{ if emergency_contact_phone && emergency_contact_phone != "" }}**Phone:** {{ emergency_contact_phone }}
{{ end }}
{{ end }}
{{ if insurance_provider && insurance_provider != "" }}
## Insurance
**Provider:** {{ insurance_provider }}
{{ if insurance_id && insurance_id != "" }}**ID:** {{ insurance_id }}
{{ end }}
{{ end }}
## Reason for Visit
{{ reason_for_visit }}

{{ if current_medications && current_medications.size > 0 }}
## Current Medications
{{ for m in current_medications }}- {{ m }}
{{ end }}

{{ end }}
{{ if allergies && allergies.size > 0 }}
## Allergies
{{ for a in allergies }}- {{ a }}
{{ end }}

{{ end }}
{{ if past_medical_history && past_medical_history.size > 0 }}
## Past Medical History
{{ for h in past_medical_history }}- {{ h }}
{{ end }}

{{ end }}
{{ if surgical_history && surgical_history.size > 0 }}
## Surgical History
{{ for s in surgical_history }}- {{ s }}
{{ end }}

{{ end }}
{{ if family_history && family_history.size > 0 }}
## Family History
{{ for f in family_history }}- {{ f }}
{{ end }}

{{ end }}
{{ if social_history && social_history != "" }}
## Social History
{{ social_history }}

{{ end }}
{{ if review_of_systems && review_of_systems != "" }}
## Review of Systems
{{ review_of_systems }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'healthcare',
  'Word',
  '[{"name":"practice_name","type":"string","required":true,"description":"Practice name."},{"name":"intake_date","type":"date","required":true,"description":"Date of intake."},{"name":"patient_name","type":"string","required":true,"description":"Patient full name."},{"name":"date_of_birth","type":"date","required":true,"description":"Patient date of birth."},{"name":"gender","type":"string","required":false,"description":"Patient gender."},{"name":"address","type":"text","required":false,"description":"Patient address."},{"name":"phone","type":"string","required":false,"description":"Patient phone number."},{"name":"email","type":"string","required":false,"description":"Patient email."},{"name":"emergency_contact_name","type":"string","required":false,"description":"Emergency contact name."},{"name":"emergency_contact_phone","type":"string","required":false,"description":"Emergency contact phone."},{"name":"insurance_provider","type":"string","required":false,"description":"Insurance provider name."},{"name":"insurance_id","type":"string","required":false,"description":"Insurance ID number."},{"name":"reason_for_visit","type":"text","required":true,"description":"Reason for visit."},{"name":"current_medications","type":"stringArray","required":false,"description":"Current medications."},{"name":"allergies","type":"stringArray","required":false,"description":"Known allergies."},{"name":"past_medical_history","type":"stringArray","required":false,"description":"Past medical history items."},{"name":"surgical_history","type":"stringArray","required":false,"description":"Surgical history."},{"name":"family_history","type":"stringArray","required":false,"description":"Family medical history."},{"name":"social_history","type":"text","required":false,"description":"Social history."},{"name":"review_of_systems","type":"text","required":false,"description":"Review of systems."}]',
  'intake-{{ slug patient_name }}-{{ format_date intake_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_healthcare-progress-note',
  'document-templates',
  'healthcare/progress-note',
  'Progress Note (SOAP)',
  'SOAP-format clinical progress note with subjective, objective, assessment, and plan.',
  'BuiltIn',
  '# Progress Note

**Patient:** {{ patient_name }}{{ if date_of_birth && date_of_birth != "" }}  **DOB:** {{ format_date date_of_birth }}{{ end }}
**Encounter Date:** {{ format_date encounter_date }}
**Provider:** {{ provider_name }}

## Chief Complaint
{{ chief_complaint }}

## Subjective
{{ subjective }}

## Objective
{{ objective }}

## Assessment
{{ assessment }}

## Plan
{{ plan }}

{{ if cpt_codes && cpt_codes.size > 0 }}
**CPT Codes:** {{ for c in cpt_codes }}{{ c }} {{ end }}
{{ end }}
{{ if icd_codes && icd_codes.size > 0 }}
**ICD Codes:** {{ for c in icd_codes }}{{ c }} {{ end }}
{{ end }}

---

**Provider Signature:** ____________________________  **Date:** ____________',
  '',
  unixepoch(),
  unixepoch(),
  'healthcare',
  'Word',
  '[{"name":"patient_name","type":"string","required":true,"description":"Patient full name."},{"name":"date_of_birth","type":"date","required":false,"description":"Patient date of birth."},{"name":"encounter_date","type":"date","required":true,"description":"Date of the encounter."},{"name":"provider_name","type":"string","required":true,"description":"Provider name."},{"name":"chief_complaint","type":"text","required":true,"description":"Why patient is being seen today."},{"name":"subjective","type":"text","required":true,"description":"Patient-reported symptoms and history."},{"name":"objective","type":"text","required":true,"description":"Objective findings, vitals, exam."},{"name":"assessment","type":"text","required":true,"description":"Clinical assessment and diagnosis."},{"name":"plan","type":"text","required":true,"description":"Treatment plan and follow-up."},{"name":"cpt_codes","type":"stringArray","required":false,"description":"CPT procedure codes."},{"name":"icd_codes","type":"stringArray","required":false,"description":"ICD diagnosis codes."}]',
  'progress-note-{{ slug patient_name }}-{{ format_date encounter_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_healthcare-referral-letter',
  'document-templates',
  'healthcare/referral-letter',
  'Referral Letter',
  'Clinical referral letter with patient history, reason for referral, and urgency.',
  'BuiltIn',
  '# Referral Letter

{{ format_date letter_date }}

**To:** {{ receiving_provider_name }}{{ if receiving_specialty && receiving_specialty != "" }}, {{ receiving_specialty }}{{ end }}
**From:** {{ referring_provider_name }}{{ if referring_practice && referring_practice != "" }}, {{ referring_practice }}{{ end }}

**Re:** {{ patient_name }}, DOB {{ format_date patient_dob }}

{{ if urgency && urgency != "" }}**Urgency:** {{ urgency }}
{{ end }}

## Reason for Referral
{{ reason_for_referral }}

## Clinical Summary
{{ clinical_summary }}

{{ if current_medications && current_medications.size > 0 }}
## Current Medications
{{ for m in current_medications }}- {{ m }}
{{ end }}

{{ end }}
Thank you for seeing this patient. Please do not hesitate to contact our office with any questions.

Sincerely,

**{{ referring_provider_name }}**
{{ if referring_practice && referring_practice != "" }}{{ referring_practice }}
{{ end }}
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not medical advice.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'healthcare',
  'Word',
  '[{"name":"letter_date","type":"date","required":true,"description":"Date of the referral letter."},{"name":"referring_provider_name","type":"string","required":true,"description":"Referring provider name."},{"name":"referring_practice","type":"string","required":false,"description":"Referring practice name."},{"name":"receiving_provider_name","type":"string","required":true,"description":"Receiving provider name."},{"name":"receiving_specialty","type":"string","required":false,"description":"Receiving provider specialty."},{"name":"patient_name","type":"string","required":true,"description":"Patient full name."},{"name":"patient_dob","type":"date","required":true,"description":"Patient date of birth."},{"name":"reason_for_referral","type":"text","required":true,"description":"Reason for referral."},{"name":"clinical_summary","type":"text","required":true,"description":"Relevant medical history and findings."},{"name":"current_medications","type":"stringArray","required":false,"description":"Current medications."},{"name":"urgency","type":"string","required":false,"description":"Routine, Urgent, or Emergent."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'referral-{{ slug patient_name }}-{{ format_date letter_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_healthcare-superbill',
  'document-templates',
  'healthcare/superbill',
  'Superbill',
  'Medical superbill with diagnoses, CPT charges, and fee totals for insurance submission.',
  'BuiltIn',
  '# Superbill

**Practice:** {{ practice_name }}
**Provider:** {{ provider_name }}{{ if provider_npi && provider_npi != "" }}  NPI: {{ provider_npi }}{{ end }}

**Patient:** {{ patient_name }}
**Date of Service:** {{ format_date date_of_service }}

{{ if insurance_provider && insurance_provider != "" }}
**Insurance:** {{ insurance_provider }}{{ if policy_number && policy_number != "" }}  Policy #: {{ policy_number }}{{ end }}
{{ end }}

## Diagnoses
| Code | Description |
|------|-------------|
{{ for d in diagnoses }}| {{ escape_pipes d.code }} | {{ escape_pipes d.description }} |
{{ end }}

## Charges
| CPT Code | Modifier | Description | Fee | Units | Subtotal |
|----------|----------|-------------|----:|------:|---------:|
{{ total = 0 }}{{ for c in charges }}{{ units = c.units }}{{ if units == 0 }}{{ units = 1 }}{{ end }}{{ line_sub = c.fee * units }}{{ total = total + line_sub }}| {{ escape_pipes c.cpt_code }} | {{ if c.modifier && c.modifier != "" }}{{ escape_pipes c.modifier }}{{ end }} | {{ if c.description && c.description != "" }}{{ escape_pipes c.description }}{{ end }} | {{ format_money c.fee (normalize_currency currency) }} | {{ units }} | {{ format_money line_sub (normalize_currency currency) }} |
{{ end }}

**Total:** {{ format_money total (normalize_currency currency) }}',
  '',
  unixepoch(),
  unixepoch(),
  'healthcare',
  'Word',
  '[{"name":"practice_name","type":"string","required":true,"description":"Practice name."},{"name":"provider_name","type":"string","required":true,"description":"Provider name."},{"name":"provider_npi","type":"string","required":false,"description":"Provider NPI number."},{"name":"patient_name","type":"string","required":true,"description":"Patient full name."},{"name":"date_of_service","type":"date","required":true,"description":"Date of service."},{"name":"insurance_provider","type":"string","required":false,"description":"Insurance provider name."},{"name":"policy_number","type":"string","required":false,"description":"Policy number."},{"name":"diagnoses","type":"objectArray","required":true,"description":"Diagnosis codes and descriptions.","itemFields":[{"name":"code","type":"string","required":true},{"name":"description","type":"string","required":true}]},{"name":"charges","type":"objectArray","required":true,"description":"Procedure charges.","itemFields":[{"name":"cpt_code","type":"string","required":true},{"name":"modifier","type":"string","required":false},{"name":"description","type":"string","required":false},{"name":"fee","type":"decimal","required":true},{"name":"units","type":"integer","required":false}]},{"name":"currency","type":"string","required":false,"description":"ISO currency code."}]',
  'superbill-{{ slug patient_name }}-{{ format_date date_of_service }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-nda',
  'document-templates',
  'legal/nda',
  'Non-Disclosure Agreement',
  'Mutual NDA template with parties, term, scope, and governing law.',
  'BuiltIn',
  '# Mutual Non-Disclosure Agreement

This Mutual Non-Disclosure Agreement (the "Agreement") is entered into as of {{ format_date effective_date }} (the "Effective Date") by and between **{{ party_a }}** ("Party A") and **{{ party_b }}** ("Party B"), collectively the "Parties."

{{ if party_a_address && party_a_address != "" || party_b_address && party_b_address != "" }}
## Parties

{{ if party_a_address && party_a_address != "" }}**{{ party_a }}**
{{ party_a_address }}

{{ end }}{{ if party_b_address && party_b_address != "" }}**{{ party_b }}**
{{ party_b_address }}

{{ end }}{{ end }}
## 1. Purpose

{{ purpose }}

## 2. Confidential Information

"Confidential Information" means any non-public information disclosed by one Party (the "Disclosing Party") to the other (the "Receiving Party"), whether in writing, orally, visually, or in any other form, that is marked or identified as confidential, or that would reasonably be understood to be confidential given its nature or the circumstances of disclosure.

## 3. Obligations

The Receiving Party shall:

- hold the Confidential Information in strict confidence;
- use it solely for the Purpose described above;
- protect it with at least the same degree of care used to protect its own confidential information, and never less than reasonable care; and
- limit access to its employees, contractors, and advisors who have a need to know and who are bound by confidentiality obligations no less protective than those in this Agreement.

## 4. Exclusions

Confidential Information does not include information that (a) is or becomes publicly available without breach of this Agreement, (b) was known to the Receiving Party prior to disclosure without a duty of confidentiality, (c) is lawfully received from a third party free of any confidentiality obligation, or (d) is independently developed by the Receiving Party without use of the Disclosing Party''s Confidential Information.

## 5. Term

{{ term = term_years }}{{ if term == 0 }}{{ term = 2 }}{{ end }}This Agreement begins on the Effective Date and continues for {{ term }} year{{ if term != 1 }}s{{ end }}, unless terminated earlier by mutual written agreement. The confidentiality obligations survive termination for the same period.

## 6. Return or Destruction

On written request of the Disclosing Party, the Receiving Party shall promptly return or destroy all Confidential Information in its possession, and certify destruction in writing if requested.

## 7. No License; No Warranty

Nothing in this Agreement grants any license, ownership, or other rights in the Confidential Information. Confidential Information is provided "as is," without any warranty of any kind.

## 8. Governing Law

This Agreement is governed by the laws of **{{ governing_law }}**, without regard to its conflict-of-law provisions.

## 9. Entire Agreement

This Agreement constitutes the entire understanding between the Parties regarding its subject matter and supersedes any prior agreements, written or oral, on that subject.

## Signatures

**{{ party_a }}**

Signature: ____________________________

Name / Title: ____________________________

Date: ____________________________

**{{ party_b }}**

Signature: ____________________________

Name / Title: ____________________________

Date: ____________________________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'legal',
  'Word',
  '[{"name":"party_a","type":"string","required":true,"description":"First party — legal name."},{"name":"party_a_address","type":"text","required":false,"description":"First party address (multiline)."},{"name":"party_b","type":"string","required":true,"description":"Second party — legal name."},{"name":"party_b_address","type":"text","required":false,"description":"Second party address (multiline)."},{"name":"effective_date","type":"date","required":true,"description":"Date the agreement takes effect."},{"name":"term_years","type":"integer","required":false,"description":"Duration of confidentiality obligations, in years. Defaults to 2."},{"name":"purpose","type":"text","required":true,"description":"Why the parties are sharing confidential information."},{"name":"governing_law","type":"string","required":true,"description":"Jurisdiction whose laws govern, e.g. State of Delaware."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include the AI-generated draft disclaimer."}]',
  'nda-{{ slug party_a }}-{{ slug party_b }}-{{ format_date effective_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-service-agreement',
  'document-templates',
  'legal/service-agreement',
  'Service Agreement',
  'Service agreement with scope, fees, term, termination, and governing law.',
  'BuiltIn',
  '# Service Agreement

This Service Agreement (the "Agreement") is entered into as of {{ format_date effective_date }} between **{{ client_name }}** ("Client") and **{{ provider_name }}** ("Provider").

## Services
{{ services_description }}

## Fees and Payment
{{ fees }}

## Term
This Agreement commences on {{ format_date effective_date }} and continues for {{ term }}, unless earlier terminated as provided herein.

## Termination
Either party may terminate this Agreement upon {{ if termination_notice_days > 0 }}{{ termination_notice_days }}{{ else }}30{{ end }} days'' written notice to the other party.

## Governing Law
This Agreement is governed by the laws of **{{ governing_law }}**, without regard to its conflict-of-law provisions.

## Entire Agreement
This Agreement constitutes the entire agreement between the parties with respect to its subject matter and supersedes all prior agreements.

## Signatures

**Client:** {{ client_name }}

Signature: ____________________________  Date: ____________

**Provider:** {{ provider_name }}

Signature: ____________________________  Date: ____________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'legal',
  'Word',
  '[{"name":"client_name","type":"string","required":true,"description":"Client legal name."},{"name":"provider_name","type":"string","required":true,"description":"Provider legal name."},{"name":"effective_date","type":"date","required":true,"description":"Effective date."},{"name":"services_description","type":"text","required":true,"description":"Detailed description of services."},{"name":"fees","type":"text","required":true,"description":"Fee structure and payment terms."},{"name":"term","type":"string","required":true,"description":"Duration of agreement e.g. 12 months."},{"name":"termination_notice_days","type":"integer","required":false,"description":"Notice required to terminate. Default 30."},{"name":"governing_law","type":"string","required":true,"description":"Governing jurisdiction."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'services-agreement-{{ slug provider_name }}-{{ slug client_name }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-engagement-letter',
  'document-templates',
  'legal/engagement-letter',
  'Engagement Letter',
  'Law firm engagement letter with matter description, scope, fees, and retainer.',
  'BuiltIn',
  '# Engagement Letter

{{ format_date letter_date }}

**{{ client_name }}**

Dear {{ client_name }},

We are pleased to confirm our engagement by **{{ firm_name }}** to represent you in connection with the following matter.

## Matter
{{ matter_description }}

## Scope of Services
{{ scope_of_services }}

## Fees
{{ fee_structure }}

{{ if retainer_amount > 0 }}
## Retainer
A retainer of {{ format_money retainer_amount "" }} is required before work commences. This amount will be applied against fees as they accrue.

{{ end }}
{{ if billing_frequency && billing_frequency != "" }}
## Billing
Invoices will be issued {{ billing_frequency }}.

{{ end }}
{{ if governing_law && governing_law != "" }}
## Governing Law
This engagement is governed by the laws of {{ governing_law }}.

{{ end }}
## Acceptance

Please sign and return a copy of this letter to confirm your agreement to these terms.

Sincerely,

**{{ firm_name }}**
{{ if contact_attorney && contact_attorney != "" }}{{ contact_attorney }}
{{ end }}

---

**Accepted by:** {{ client_name }}

Signature: ____________________________  Date: ____________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'legal',
  'Word',
  '[{"name":"firm_name","type":"string","required":true,"description":"Law firm name."},{"name":"client_name","type":"string","required":true,"description":"Client name."},{"name":"letter_date","type":"date","required":true,"description":"Date of the engagement letter."},{"name":"matter_description","type":"text","required":true,"description":"Description of the legal matter."},{"name":"scope_of_services","type":"text","required":true,"description":"Scope of legal services."},{"name":"fee_structure","type":"text","required":true,"description":"Billing rates and fee arrangement."},{"name":"retainer_amount","type":"currency","required":false,"description":"Retainer amount required."},{"name":"billing_frequency","type":"string","required":false,"description":"Billing frequency e.g. monthly."},{"name":"contact_attorney","type":"string","required":false,"description":"Name of contact attorney."},{"name":"governing_law","type":"string","required":false,"description":"Governing jurisdiction."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'engagement-{{ slug firm_name }}-{{ slug client_name }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-demand-letter',
  'document-templates',
  'legal/demand-letter',
  'Demand Letter',
  'Formal demand letter with factual background, legal basis, specific demand, and deadline.',
  'BuiltIn',
  '# Demand Letter

{{ if sender_address && sender_address != "" }}{{ sender_address }}

{{ end }}{{ format_date letter_date }}

**{{ recipient_name }}**
{{ if recipient_address && recipient_address != "" }}{{ recipient_address }}
{{ end }}

**Re: {{ subject }}**

Dear {{ recipient_name }}:

## Background
{{ facts }}

{{ if legal_basis && legal_basis != "" }}
## Legal Basis
{{ legal_basis }}

{{ end }}
## Demand
{{ demand }}

{{ if amount_due > 0 }}
The total amount demanded is **{{ format_money amount_due "" }}**.

{{ end }}
Please be advised that unless the above demands are satisfied in full by **{{ format_date response_deadline }}**, we reserve all rights and remedies available at law and in equity, including but not limited to the commencement of legal proceedings without further notice.

Sincerely,

**{{ sender_name }}**
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before sending.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'legal',
  'Word',
  '[{"name":"sender_name","type":"string","required":true,"description":"Sender name."},{"name":"sender_address","type":"text","required":false,"description":"Sender address."},{"name":"recipient_name","type":"string","required":true,"description":"Recipient name."},{"name":"recipient_address","type":"text","required":false,"description":"Recipient address."},{"name":"letter_date","type":"date","required":true,"description":"Date of the letter."},{"name":"subject","type":"string","required":true,"description":"Subject line of the demand."},{"name":"facts","type":"text","required":true,"description":"Factual background giving rise to the demand."},{"name":"legal_basis","type":"text","required":false,"description":"Legal basis for the claim."},{"name":"demand","type":"text","required":true,"description":"Specific action demanded."},{"name":"response_deadline","type":"date","required":true,"description":"Date by which to respond."},{"name":"amount_due","type":"currency","required":false,"description":"Monetary amount demanded, if applicable."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'demand-{{ slug sender_name }}-to-{{ slug recipient_name }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-corporate-minutes',
  'document-templates',
  'legal/corporate-minutes',
  'Corporate Meeting Minutes',
  'Corporate meeting minutes with attendees, resolutions, votes, and adjournment.',
  'BuiltIn',
  '# {{ meeting_type }} Minutes

**Company:** {{ company_name }}
**Date:** {{ format_date meeting_date }}{{ if meeting_time && meeting_time != "" }}  **Time:** {{ meeting_time }}{{ end }}
{{ if location && location != "" }}**Location:** {{ location }}
{{ end }}

## Attendance

**Present:**
{{ for a in attendees_present }}- {{ a }}
{{ end }}

{{ if attendees_absent && attendees_absent.size > 0 }}
**Absent:**
{{ for a in attendees_absent }}- {{ a }}
{{ end }}
{{ end }}

{{ if presiding_officer && presiding_officer != "" }}**Presiding Officer:** {{ presiding_officer }}
{{ end }}{{ if secretary && secretary != "" }}**Secretary:** {{ secretary }}
{{ end }}{{ if quorum_established }}
**Quorum established.**
{{ end }}

{{ if resolutions && resolutions.size > 0 }}
## Resolutions
{{ for r in resolutions }}
### {{ r.title }}

{{ r.text }}

{{ if r.votes_for > 0 || r.votes_against > 0 || r.votes_abstain > 0 }}**Vote:** For: {{ r.votes_for }}  Against: {{ r.votes_against }}  Abstain: {{ r.votes_abstain }}
{{ end }}{{ if r.outcome && r.outcome != "" }}**Outcome:** {{ r.outcome }}
{{ end }}
{{ end }}
{{ end }}
## Adjournment
{{ if adjournment_time && adjournment_time != "" }}The meeting was adjourned at {{ adjournment_time }}.{{ else }}The meeting was duly adjourned.{{ end }}

---

**Secretary:** ____________________________  **Date:** ____________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'legal',
  'Word',
  '[{"name":"company_name","type":"string","required":true,"description":"Company legal name."},{"name":"meeting_type","type":"string","required":true,"description":"e.g. Board of Directors, Annual Shareholders."},{"name":"meeting_date","type":"date","required":true,"description":"Date of the meeting."},{"name":"meeting_time","type":"string","required":false,"description":"Time the meeting convened."},{"name":"location","type":"string","required":false,"description":"Meeting location."},{"name":"attendees_present","type":"stringArray","required":true,"description":"Names of attendees present."},{"name":"attendees_absent","type":"stringArray","required":false,"description":"Names of attendees absent."},{"name":"presiding_officer","type":"string","required":false,"description":"Name of presiding officer."},{"name":"secretary","type":"string","required":false,"description":"Name of secretary."},{"name":"quorum_established","type":"boolean","required":false,"description":"Whether quorum was established."},{"name":"resolutions","type":"objectArray","required":false,"description":"Resolutions voted on.","itemFields":[{"name":"title","type":"string","required":true},{"name":"text","type":"text","required":true},{"name":"votes_for","type":"integer","required":false},{"name":"votes_against","type":"integer","required":false},{"name":"votes_abstain","type":"integer","required":false},{"name":"outcome","type":"string","required":false}]},{"name":"adjournment_time","type":"string","required":false,"description":"Time meeting was adjourned."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'minutes-{{ slug company_name }}-{{ format_date meeting_date }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-power-of-attorney',
  'document-templates',
  'legal/power-of-attorney',
  'Power of Attorney',
  'Power of attorney with principal, agent, powers granted, and governing state.',
  'BuiltIn',
  '# Power of Attorney

**Type:** {{ poa_type }}

This Power of Attorney is executed as of {{ format_date effective_date }} by **{{ principal_name }}** ("Principal"){{ if principal_address && principal_address != "" }}, {{ principal_address }}{{ end }}, appointing **{{ agent_name }}** ("Agent"){{ if agent_address && agent_address != "" }}, {{ agent_address }}{{ end }}.

## Powers Granted

The Principal hereby grants the Agent the following powers:

{{ for p in powers_granted }}- {{ p }}
{{ end }}

{{ if limitations && limitations != "" }}
## Limitations

{{ limitations }}

{{ end }}
{{ if is_durable }}
## Durability

This Power of Attorney shall not be affected by the subsequent incapacity or incompetence of the Principal. It is intended to be a Durable Power of Attorney under the laws of {{ governing_state }}.

{{ end }}
## Governing Law

This Power of Attorney is governed by the laws of **{{ governing_state }}**.

## Revocation

This Power of Attorney may be revoked by the Principal at any time by executing a written revocation and delivering it to the Agent and any third parties relying on this document.

## Signature

I, **{{ principal_name }}**, sign this Power of Attorney voluntarily and with full authority.

**Principal Signature:** ____________________________  **Date:** ____________

*Notary Acknowledgment*

State of ____________________________

County of ____________________________

On this ____ day of ______________, before me personally appeared {{ principal_name }}, known to me to be the person whose name is subscribed to this instrument.

Notary Public: ____________________________  Commission Expires: ____________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'legal',
  'Word',
  '[{"name":"principal_name","type":"string","required":true,"description":"Principal full legal name."},{"name":"principal_address","type":"text","required":false,"description":"Principal address."},{"name":"agent_name","type":"string","required":true,"description":"Agent full legal name."},{"name":"agent_address","type":"text","required":false,"description":"Agent address."},{"name":"poa_type","type":"string","required":true,"description":"General, Limited, or Durable."},{"name":"effective_date","type":"date","required":true,"description":"Effective date."},{"name":"is_durable","type":"boolean","required":false,"description":"Whether power survives incapacity."},{"name":"powers_granted","type":"stringArray","required":true,"description":"Specific powers granted."},{"name":"limitations","type":"text","required":false,"description":"Any limitations on agent authority."},{"name":"governing_state","type":"string","required":true,"description":"Governing state."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'poa-{{ slug principal_name }}-{{ slug agent_name }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_legal-terms-of-service',
  'document-templates',
  'legal/terms-of-service',
  'Terms of Service',
  'Website/app terms of service with acceptable use, IP, disclaimers, and governing law.',
  'BuiltIn',
  '# Terms of Service — {{ service_name }}

{{ if website_url && website_url != "" }}**Website:** {{ website_url }}
{{ end }}**Effective Date:** {{ format_date effective_date }}

## 1. About {{ service_name }}
{{ service_description }}

## 2. Acceptance
By using {{ service_name }}, you agree to be bound by these Terms of Service. If you do not agree, do not use the service.

{{ if acceptable_use && acceptable_use.size > 0 }}
## 3. Acceptable Use
{{ for item in acceptable_use }}- {{ item }}
{{ end }}

{{ end }}
{{ if prohibited_conduct && prohibited_conduct.size > 0 }}
## 4. Prohibited Conduct
You agree not to:
{{ for item in prohibited_conduct }}- {{ item }}
{{ end }}

{{ end }}
{{ if intellectual_property_statement && intellectual_property_statement != "" }}
## 5. Intellectual Property
{{ intellectual_property_statement }}

{{ end }}
{{ if disclaimer_of_warranties && disclaimer_of_warranties != "" }}
## 6. Disclaimer of Warranties
{{ disclaimer_of_warranties }}

{{ end }}
{{ if limitation_of_liability && limitation_of_liability != "" }}
## 7. Limitation of Liability
{{ limitation_of_liability }}

{{ end }}
## 8. Governing Law
These Terms are governed by the laws of **{{ governing_law }}**, without regard to conflict-of-law principles.

{{ if contact_email && contact_email != "" }}
## 9. Contact
Questions about these Terms may be directed to **{{ contact_email }}**.

{{ end }}
**{{ company_name }}**
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before publication.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'legal',
  'Word',
  '[{"name":"company_name","type":"string","required":true,"description":"Company name."},{"name":"service_name","type":"string","required":true,"description":"Service or product name."},{"name":"website_url","type":"string","required":false,"description":"Website URL."},{"name":"effective_date","type":"date","required":true,"description":"Effective date of the terms."},{"name":"service_description","type":"text","required":true,"description":"Description of the service."},{"name":"acceptable_use","type":"stringArray","required":false,"description":"Acceptable use policy items."},{"name":"prohibited_conduct","type":"stringArray","required":false,"description":"Prohibited conduct items."},{"name":"intellectual_property_statement","type":"text","required":false,"description":"Intellectual property statement."},{"name":"disclaimer_of_warranties","type":"text","required":false,"description":"Disclaimer of warranties."},{"name":"limitation_of_liability","type":"text","required":false,"description":"Limitation of liability."},{"name":"governing_law","type":"string","required":true,"description":"Governing jurisdiction."},{"name":"contact_email","type":"string","required":false,"description":"Contact email for legal questions."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'terms-of-service-{{ slug service_name }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_real-estate-property-listing',
  'document-templates',
  'real-estate/property-listing',
  'Property Listing',
  'Real estate property listing with features, description, and agent contact.',
  'BuiltIn',
  '# {{ headline }}

**{{ address }}**

**Price:** {{ format_money price (normalize_currency currency) }}

## Property Details

| | |
|---|---|
{{ if bedrooms > 0 }}| Bedrooms | {{ bedrooms }} |
{{ end }}{{ if bathrooms > 0 }}| Bathrooms | {{ bathrooms }} |
{{ end }}{{ if square_feet > 0 }}| Square Feet | {{ format_number square_feet }} |
{{ end }}{{ if lot_size && lot_size != "" }}| Lot Size | {{ lot_size }} |
{{ end }}{{ if year_built > 0 }}| Year Built | {{ year_built }} |
{{ end }}{{ if property_type && property_type != "" }}| Property Type | {{ property_type }} |
{{ end }}

## Description
{{ description }}

{{ if amenities && amenities.size > 0 }}
## Amenities
{{ for a in amenities }}- {{ a }}
{{ end }}

{{ end }}
{{ if agent_name && agent_name != "" }}
## Contact
**{{ agent_name }}**{{ if brokerage && brokerage != "" }}, {{ brokerage }}{{ end }}
{{ if agent_phone && agent_phone != "" }}Phone: {{ agent_phone }}
{{ end }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'real-estate',
  'StructuredPdf',
  '[{"name":"headline","type":"string","required":true,"description":"Short marketing headline."},{"name":"address","type":"text","required":true,"description":"Property address."},{"name":"price","type":"currency","required":true,"description":"Listing price."},{"name":"bedrooms","type":"decimal","required":false,"description":"Number of bedrooms."},{"name":"bathrooms","type":"decimal","required":false,"description":"Number of bathrooms."},{"name":"square_feet","type":"integer","required":false,"description":"Interior square footage."},{"name":"lot_size","type":"string","required":false,"description":"Lot size description."},{"name":"year_built","type":"integer","required":false,"description":"Year built."},{"name":"property_type","type":"string","required":false,"description":"e.g. Single Family, Condo, Townhouse."},{"name":"amenities","type":"stringArray","required":false,"description":"Property amenities."},{"name":"description","type":"text","required":true,"description":"Full marketing description."},{"name":"agent_name","type":"string","required":false,"description":"Agent name."},{"name":"agent_phone","type":"string","required":false,"description":"Agent phone number."},{"name":"brokerage","type":"string","required":false,"description":"Brokerage name."},{"name":"currency","type":"string","required":false,"description":"ISO currency code."}]',
  'listing-{{ slug headline }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_real-estate-purchase-agreement',
  'document-templates',
  'real-estate/purchase-agreement',
  'Purchase Agreement',
  'Real estate purchase agreement with contingencies, earnest money, and closing date.',
  'BuiltIn',
  '# Real Estate Purchase Agreement

This Purchase Agreement ("Agreement") is entered into between **{{ buyer_name }}** ("Buyer"){{ if buyer_address && buyer_address != "" }}, {{ buyer_address }}{{ end }}, and **{{ seller_name }}** ("Seller"){{ if seller_address && seller_address != "" }}, {{ seller_address }}{{ end }}.

## Property
**Address:** {{ property_address }}

## Purchase Price
**Purchase Price:** {{ format_money purchase_price (normalize_currency currency) }}
**Earnest Money Deposit:** {{ format_money earnest_money (normalize_currency currency) }}

## Closing
**Closing Date:** {{ format_date closing_date }}

## Contingencies
{{ if contingency_financing }}- **Financing Contingency:** This Agreement is contingent upon Buyer obtaining satisfactory mortgage financing.
{{ end }}{{ if contingency_inspection }}- **Inspection Contingency:** This Agreement is contingent upon Buyer''s satisfactory inspection of the property.
{{ end }}{{ if contingency_appraisal }}- **Appraisal Contingency:** This Agreement is contingent upon the property appraising at or above the purchase price.
{{ end }}

## Governing Law
This Agreement is governed by the laws of **{{ governing_state }}**.

## Signatures

**Buyer:** {{ buyer_name }}

Signature: ____________________________  Date: ____________

**Seller:** {{ seller_name }}

Signature: ____________________________  Date: ____________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'real-estate',
  'Word',
  '[{"name":"buyer_name","type":"string","required":true,"description":"Buyer full legal name."},{"name":"buyer_address","type":"text","required":false,"description":"Buyer address."},{"name":"seller_name","type":"string","required":true,"description":"Seller full legal name."},{"name":"seller_address","type":"text","required":false,"description":"Seller address."},{"name":"property_address","type":"text","required":true,"description":"Property address."},{"name":"purchase_price","type":"currency","required":true,"description":"Purchase price."},{"name":"earnest_money","type":"currency","required":true,"description":"Earnest money deposit amount."},{"name":"closing_date","type":"date","required":true,"description":"Closing date."},{"name":"contingency_financing","type":"boolean","required":false,"description":"Include financing contingency."},{"name":"contingency_inspection","type":"boolean","required":false,"description":"Include inspection contingency."},{"name":"contingency_appraisal","type":"boolean","required":false,"description":"Include appraisal contingency."},{"name":"currency","type":"string","required":false,"description":"ISO currency code."},{"name":"governing_state","type":"string","required":true,"description":"Governing state."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'purchase-{{ slug buyer_name }}-{{ slug seller_name }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_real-estate-lease-agreement',
  'document-templates',
  'real-estate/lease-agreement',
  'Lease Agreement',
  'Residential lease agreement with rent, deposit, utilities, pet policy, and governing state.',
  'BuiltIn',
  '# Lease Agreement

This Lease Agreement is entered into between **{{ landlord_name }}** ("Landlord"){{ if landlord_address && landlord_address != "" }}, {{ landlord_address }}{{ end }}, and the following Tenant(s): {{ for t in tenant_names }}**{{ t }}**{{ if !for.last }}, {{ end }}{{ end }}.

## Premises
**Address:** {{ premises_address }}

## Term
**Lease Start:** {{ format_date lease_start }}
**Lease End:** {{ format_date lease_end }}

## Rent
**Monthly Rent:** {{ format_money monthly_rent (normalize_currency "") }}
{{ if security_deposit > 0 }}**Security Deposit:** {{ format_money security_deposit (normalize_currency "") }}
{{ end }}{{ if late_fee > 0 }}**Late Fee:** {{ format_money late_fee (normalize_currency "") }}{{ if grace_period_days > 0 }} (after {{ grace_period_days }}-day grace period){{ end }}
{{ end }}

## Policies
**Pets:** {{ if pets_allowed }}Allowed{{ else }}Not allowed{{ end }}
**Smoking:** {{ if smoking_allowed }}Allowed{{ else }}Not allowed{{ end }}

{{ if utilities_included && utilities_included.size > 0 }}
## Utilities Included
{{ for u in utilities_included }}- {{ u }}
{{ end }}

{{ end }}
## Governing Law
This Agreement is governed by the laws of **{{ governing_state }}**.

## Signatures

**Landlord:** {{ landlord_name }}

Signature: ____________________________  Date: ____________

{{ for t in tenant_names }}**Tenant:** {{ t }}

Signature: ____________________________  Date: ____________

{{ end }}
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. It is not legal advice. Review with qualified counsel before execution.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'real-estate',
  'Word',
  '[{"name":"landlord_name","type":"string","required":true,"description":"Landlord full legal name."},{"name":"landlord_address","type":"text","required":false,"description":"Landlord address."},{"name":"tenant_names","type":"stringArray","required":true,"description":"Names of all tenants."},{"name":"premises_address","type":"text","required":true,"description":"Rental property address."},{"name":"lease_start","type":"date","required":true,"description":"Lease start date."},{"name":"lease_end","type":"date","required":true,"description":"Lease end date."},{"name":"monthly_rent","type":"currency","required":true,"description":"Monthly rent amount."},{"name":"security_deposit","type":"currency","required":false,"description":"Security deposit amount."},{"name":"late_fee","type":"currency","required":false,"description":"Late payment fee."},{"name":"grace_period_days","type":"integer","required":false,"description":"Grace period in days before late fee applies."},{"name":"pets_allowed","type":"boolean","required":false,"description":"Whether pets are allowed."},{"name":"smoking_allowed","type":"boolean","required":false,"description":"Whether smoking is allowed."},{"name":"utilities_included","type":"stringArray","required":false,"description":"Utilities included in rent."},{"name":"governing_state","type":"string","required":true,"description":"Governing state."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'lease-{{ slug landlord_name }}-{{ format_date lease_start }}.docx'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_real-estate-cma-report',
  'document-templates',
  'real-estate/cma-report',
  'Comparative Market Analysis',
  'CMA report with subject property, comparable sales, average price per square foot, and recommended price range.',
  'BuiltIn',
  '# Comparative Market Analysis

**Prepared for:** {{ prepared_for }}
**Prepared by:** {{ prepared_by }}
**Report Date:** {{ format_date report_date }}

## Subject Property
**Address:** {{ subject_address }}
{{ if subject_beds > 0 }}**Beds:** {{ subject_beds }}  {{ end }}{{ if subject_baths > 0 }}**Baths:** {{ subject_baths }}  {{ end }}{{ if subject_sqft > 0 }}**Sq Ft:** {{ format_number subject_sqft }}{{ end }}

## Comparable Sales
| Address | Sold Date | Sold Price | Beds | Baths | Sq Ft | $/Sq Ft |
|---------|-----------|----------:|-----:|------:|------:|--------:|
{{ total_price = 0 }}{{ count = 0 }}{{ total_ppsf = 0 }}{{ ppsf_count = 0 }}{{ for c in comparables }}{{ total_price = total_price + c.sold_price }}{{ count = count + 1 }}{{ ppsf = 0 }}{{ if c.square_feet > 0 }}{{ ppsf = math.round(c.sold_price / c.square_feet, 2) }}{{ total_ppsf = total_ppsf + ppsf }}{{ ppsf_count = ppsf_count + 1 }}{{ end }}| {{ escape_pipes c.address }} | {{ format_date c.sold_date }} | {{ format_money c.sold_price (normalize_currency currency) }} | {{ c.bedrooms }} | {{ c.bathrooms }} | {{ if c.square_feet > 0 }}{{ format_number c.square_feet }}{{ end }} | {{ if ppsf > 0 }}{{ format_money ppsf (normalize_currency currency) }}{{ end }} |
{{ end }}

{{ if count > 0 }}{{ avg_price = math.round(total_price / count, 0) }}**Average Sold Price:** {{ format_money avg_price (normalize_currency currency) }}
{{ end }}{{ if ppsf_count > 0 }}{{ avg_ppsf = math.round(total_ppsf / ppsf_count, 2) }}**Average Price per Sq Ft:** {{ format_money avg_ppsf (normalize_currency currency) }}
{{ end }}

{{ if recommended_price_range && recommended_price_range != "" }}
## Recommended Price Range
**{{ recommended_price_range }}**

{{ end }}
{{ if market_commentary && market_commentary != "" }}
## Market Commentary
{{ market_commentary }}
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'real-estate',
  'StructuredPdf',
  '[{"name":"prepared_for","type":"string","required":true,"description":"Client name."},{"name":"prepared_by","type":"string","required":true,"description":"Agent/Analyst name."},{"name":"report_date","type":"date","required":true,"description":"Report date."},{"name":"subject_address","type":"text","required":true,"description":"Subject property address."},{"name":"subject_beds","type":"decimal","required":false,"description":"Subject property bedrooms."},{"name":"subject_baths","type":"decimal","required":false,"description":"Subject property bathrooms."},{"name":"subject_sqft","type":"integer","required":false,"description":"Subject property square feet."},{"name":"comparables","type":"objectArray","required":true,"description":"Comparable sales.","itemFields":[{"name":"address","type":"text","required":true},{"name":"sold_date","type":"date","required":true},{"name":"sold_price","type":"decimal","required":true},{"name":"bedrooms","type":"decimal","required":false},{"name":"bathrooms","type":"decimal","required":false},{"name":"square_feet","type":"integer","required":false}]},{"name":"recommended_price_range","type":"string","required":false,"description":"e.g. $450,000 - $470,000"},{"name":"market_commentary","type":"text","required":false,"description":"Market commentary."},{"name":"currency","type":"string","required":false,"description":"ISO currency code."}]',
  'cma-{{ slug prepared_for }}-{{ format_date report_date }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_real-estate-closing-disclosure',
  'document-templates',
  'real-estate/closing-disclosure',
  'Closing Disclosure',
  'Mortgage closing disclosure with loan terms, closing costs, and estimated monthly payment.',
  'BuiltIn',
  '# Closing Disclosure

**Borrower:** {{ borrower_name }}
**Property:** {{ property_address }}
**Closing Date:** {{ format_date closing_date }}
**Lender:** {{ lender_name }}

## Loan Information
| | |
|---|---|
| Loan Amount | {{ format_money loan_amount (normalize_currency currency) }} |
| Interest Rate | {{ format_percent interest_rate_percent }} |
| Loan Term | {{ loan_term_years }} years |
{{ if loan_type && loan_type != "" }}| Loan Type | {{ loan_type }} |
{{ end }}

## Closing Costs
| Description | Borrower Pays | Seller Pays |
|-------------|-------------:|------------:|
{{ total_borrower = 0 }}{{ total_seller = 0 }}{{ for c in closing_costs }}{{ total_borrower = total_borrower + c.borrower_pays }}{{ total_seller = total_seller + c.seller_pays }}| {{ escape_pipes c.description }} | {{ format_money c.borrower_pays (normalize_currency currency) }} | {{ format_money c.seller_pays (normalize_currency currency) }} |
{{ end }}| **Total** | **{{ format_money total_borrower (normalize_currency currency) }}** | **{{ format_money total_seller (normalize_currency currency) }}** |

## Estimated Monthly Payment
{{ rate = interest_rate_percent / 100 / 12 }}{{ n = loan_term_years * 12 }}{{ if rate > 0 }}{{ rate_n = 1 }}{{ i = 0 }}{{ for _ in (1..n) }}{{ rate_n = rate_n * (1 + rate) }}{{ end }}{{ monthly = math.round(loan_amount * rate * rate_n / (rate_n - 1), 2) }}**Estimated Principal & Interest:** {{ format_money monthly (normalize_currency currency) }} / month{{ else }}**Estimated Principal & Interest:** Contact lender for payment estimate.{{ end }}

---
*This is a Closing Disclosure. Review all figures carefully with your lender and settlement agent before closing.*',
  '',
  unixepoch(),
  unixepoch(),
  'real-estate',
  'StructuredPdf',
  '[{"name":"borrower_name","type":"string","required":true,"description":"Borrower full name."},{"name":"property_address","type":"text","required":true,"description":"Property address."},{"name":"closing_date","type":"date","required":true,"description":"Closing date."},{"name":"lender_name","type":"string","required":true,"description":"Lender name."},{"name":"loan_amount","type":"currency","required":true,"description":"Loan amount."},{"name":"interest_rate_percent","type":"decimal","required":true,"description":"Annual interest rate e.g. 6.5."},{"name":"loan_term_years","type":"integer","required":true,"description":"Loan term in years."},{"name":"loan_type","type":"string","required":false,"description":"Fixed, ARM, etc."},{"name":"closing_costs","type":"objectArray","required":true,"description":"Closing cost line items.","itemFields":[{"name":"description","type":"string","required":true},{"name":"borrower_pays","type":"decimal","required":false},{"name":"seller_pays","type":"decimal","required":false}]},{"name":"currency","type":"string","required":false,"description":"ISO currency code."}]',
  'closing-disclosure-{{ slug borrower_name }}-{{ format_date closing_date }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_real-estate-property-inspection',
  'document-templates',
  'real-estate/property-inspection',
  'Property Inspection Report',
  'Property inspection report with section-by-section condition findings and recommendations.',
  'BuiltIn',
  '# Property Inspection Report

**Property:** {{ property_address }}
**Client:** {{ client_name }}
**Inspector:** {{ inspector_name }}
**Inspection Date:** {{ format_date inspection_date }}
{{ if weather_conditions && weather_conditions != "" }}**Weather:** {{ weather_conditions }}
{{ end }}

{{ for section in sections }}
## {{ section.name }}

**Condition:** {{ if section.condition && section.condition != "" }}{{ section.condition }}{{ else }}Not Inspected{{ end }}

{{ if section.findings && section.findings.size > 0 }}
| Severity | Description | Recommendation |
|----------|-------------|----------------|
{{ for f in section.findings }}| {{ escape_pipes f.severity }} | {{ escape_pipes f.description }} | {{ if f.recommendation && f.recommendation != "" }}{{ escape_pipes f.recommendation }}{{ end }} |
{{ end }}
{{ end }}
{{ end }}

{{ if summary && summary != "" }}
## Summary
{{ summary }}

{{ end }}
{{ if limitations && limitations != "" }}
## Limitations
{{ limitations }}
{{ end }}

---
*This report reflects the condition of the property at the time of inspection only. Conditions may change after the date of inspection.*',
  '',
  unixepoch(),
  unixepoch(),
  'real-estate',
  'StructuredPdf',
  '[{"name":"property_address","type":"text","required":true,"description":"Property address."},{"name":"client_name","type":"string","required":true,"description":"Client name."},{"name":"inspector_name","type":"string","required":true,"description":"Inspector name."},{"name":"inspection_date","type":"date","required":true,"description":"Date of inspection."},{"name":"weather_conditions","type":"string","required":false,"description":"Weather conditions during inspection."},{"name":"sections","type":"objectArray","required":true,"description":"Inspection sections.","itemFields":[{"name":"name","type":"string","required":true,"description":"e.g. Roof, Electrical, Plumbing."},{"name":"condition","type":"string","required":false,"description":"Good, Fair, Poor, Not Inspected."},{"name":"findings","type":"objectArray","required":true,"itemFields":[{"name":"severity","type":"string","required":true,"description":"Informational, Minor, Major, Safety."},{"name":"description","type":"text","required":true},{"name":"recommendation","type":"text","required":false}]}]},{"name":"summary","type":"text","required":false,"description":"Overall inspection summary."},{"name":"limitations","type":"text","required":false,"description":"Inspection limitations."}]',
  'inspection-{{ slug client_name }}-{{ format_date inspection_date }}.pdf'
);

INSERT OR IGNORE INTO knowledge_pages
(knowledge_id, kind, slug, name, description, tier, body, workspace_id,
 created_at, updated_at, industry, default_format, fields_json, filename_template)
VALUES (
  'doctempl_real-estate-rental-application',
  'document-templates',
  'real-estate/rental-application',
  'Rental Application',
  'Rental application with applicant info, employment, income, references, and authorization.',
  'BuiltIn',
  '# Rental Application

**Property:** {{ property_address }}
{{ if landlord_name && landlord_name != "" }}**Landlord:** {{ landlord_name }}
{{ end }}**Application Date:** {{ format_date application_date }}

## Applicant Information
**Name:** {{ applicant_name }}
**Date of Birth:** {{ format_date applicant_dob }}
{{ if applicant_email && applicant_email != "" }}**Email:** {{ applicant_email }}
{{ end }}{{ if applicant_phone && applicant_phone != "" }}**Phone:** {{ applicant_phone }}
{{ end }}{{ if current_address && current_address != "" }}**Current Address:** {{ current_address }}
{{ end }}

## Employment & Income
**Monthly Income:** {{ format_money monthly_income "" }}
{{ if employer_name && employer_name != "" }}**Employer:** {{ employer_name }}
{{ end }}{{ if employer_address && employer_address != "" }}**Employer Address:** {{ employer_address }}
{{ end }}{{ if years_employed > 0 }}**Years Employed:** {{ years_employed }}
{{ end }}

{{ if references && references.size > 0 }}
## References
| Name | Relationship | Phone |
|------|-------------|-------|
{{ for r in references }}| {{ escape_pipes r.name }} | {{ if r.relationship && r.relationship != "" }}{{ escape_pipes r.relationship }}{{ end }} | {{ if r.phone && r.phone != "" }}{{ r.phone }}{{ end }} |
{{ end }}

{{ end }}
## Additional Information
**Pets:** {{ if has_pets }}Yes{{ if pet_description && pet_description != "" }} — {{ pet_description }}{{ end }}{{ else }}No{{ end }}
**Vehicles:** {{ if has_vehicles }}Yes{{ if vehicle_description && vehicle_description != "" }} — {{ vehicle_description }}{{ end }}{{ else }}No{{ end }}

## Emergency Contact
**Name:** {{ emergency_contact_name }}
**Phone:** {{ emergency_contact_phone }}

{{ if has_criminal_history }}
## Criminal History Disclosure
{{ if criminal_history_explanation && criminal_history_explanation != "" }}{{ criminal_history_explanation }}{{ else }}Applicant has disclosed a criminal history. Details provided separately.{{ end }}

{{ end }}
{{ if authorization_statement }}
## Authorization
I authorize the landlord to conduct a background and credit check and certify that the information provided in this application is true and complete to the best of my knowledge.

{{ end }}
**Applicant Signature:** ____________________________  **Date:** ____________
{{ if include_ai_disclaimer }}

---
*This document was generated by an AI assistant and is provided as a starting draft only. Review with qualified counsel before use.*
{{ end }}',
  '',
  unixepoch(),
  unixepoch(),
  'real-estate',
  'Word',
  '[{"name":"property_address","type":"text","required":true,"description":"Property address."},{"name":"landlord_name","type":"string","required":false,"description":"Landlord name."},{"name":"application_date","type":"date","required":true,"description":"Application date."},{"name":"applicant_name","type":"string","required":true,"description":"Applicant full name."},{"name":"applicant_dob","type":"date","required":true,"description":"Applicant date of birth."},{"name":"applicant_email","type":"string","required":false,"description":"Applicant email."},{"name":"applicant_phone","type":"string","required":false,"description":"Applicant phone."},{"name":"current_address","type":"text","required":false,"description":"Applicant current address."},{"name":"monthly_income","type":"currency","required":true,"description":"Gross monthly income."},{"name":"employer_name","type":"string","required":false,"description":"Employer name."},{"name":"employer_address","type":"text","required":false,"description":"Employer address."},{"name":"years_employed","type":"integer","required":false,"description":"Years with current employer."},{"name":"references","type":"objectArray","required":false,"description":"Personal references.","itemFields":[{"name":"name","type":"string","required":true},{"name":"relationship","type":"string","required":true},{"name":"phone","type":"string","required":false}]},{"name":"has_pets","type":"boolean","required":false,"description":"Whether applicant has pets."},{"name":"pet_description","type":"string","required":false,"description":"Pet description."},{"name":"has_vehicles","type":"boolean","required":false,"description":"Whether applicant has vehicles."},{"name":"vehicle_description","type":"string","required":false,"description":"Vehicle description."},{"name":"emergency_contact_name","type":"string","required":true,"description":"Emergency contact name."},{"name":"emergency_contact_phone","type":"string","required":true,"description":"Emergency contact phone."},{"name":"has_criminal_history","type":"boolean","required":false,"description":"Whether applicant has criminal history."},{"name":"criminal_history_explanation","type":"text","required":false,"description":"Criminal history explanation."},{"name":"authorization_statement","type":"boolean","required":false,"description":"Applicant authorizes background check."},{"name":"include_ai_disclaimer","type":"boolean","required":false,"description":"Whether to include AI-generated draft disclaimer."}]',
  'rental-application-{{ slug applicant_name }}.docx'
);
