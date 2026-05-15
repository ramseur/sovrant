---
name: prompt-optimize
description: 6-phase prompt analysis and optimisation
trigger: /optimize-prompt
tools: [Read, Write]
---

# Prompt Optimizer

Analyse and improve a prompt through structured evaluation.

## Steps
1. **Parse intent** — identify what the prompt is trying to achieve
2. **Gap analysis** — find missing context, ambiguous instructions, or implicit assumptions
3. **Structure review** — evaluate organisation, flow, and clarity
4. **Constraint check** — verify constraints are explicit and non-contradictory
5. **Optimise** — rewrite with:
   - Clear role/task/context separation
   - Explicit output format specification
   - Edge case handling
   - Concise but complete instructions
6. **Compare** — present before/after with explanation of changes

## Evaluation Criteria
- **Clarity** — could two different people interpret this the same way?
- **Completeness** — does it cover edge cases and failure modes?
- **Conciseness** — is every word earning its place?
- **Specificity** — are instructions concrete or vague?
- **Testability** — can you tell if the output matches the intent?

## Output Format
### Analysis
[What works, what doesn't, what's missing]

### Optimised Prompt
[The rewritten prompt]

### Changes Made
[Bullet list of what changed and why]
