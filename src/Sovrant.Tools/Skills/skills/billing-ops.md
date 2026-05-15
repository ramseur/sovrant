---
name: billing-ops
description: Subscription management, refund triage, and churn analysis
trigger: /billing
tools: [Read, Write, Bash, Grep]
---

# Billing Operations

Analyse billing data, triage refund requests, and identify churn patterns.

## Steps
1. **Ingest data** — read billing records, subscription data, or support tickets
2. **Triage refunds** — categorise requests:
   - **Auto-approve**: duplicate charges, service outage, billing error
   - **Review**: partial use, buyer's remorse, feature gap
   - **Escalate**: fraud suspicion, high-value, repeat offender
3. **Churn analysis** — identify patterns in cancellations:
   - Time-to-churn distribution
   - Feature usage before cancellation
   - Common cancellation reasons
4. **Recommendations** — suggest retention actions, pricing adjustments, or process fixes

## Output Format
- **Refund triage summary** — approved/review/escalate counts
- **Churn dashboard** — key metrics and patterns
- **Action items** — prioritised list of recommended changes
