---
name: skill-create
description: Define new skills at runtime from successful workflow patterns
trigger: /new-skill
tools: [Read, Write, SkillCreate]
---

# Skill Creator

Create a new reusable skill from a successful workflow pattern.

## Steps
1. **Identify pattern** — what workflow was successful and should be repeatable?
2. **Extract steps** — document the key steps that made it work
3. **Define constraints** — which tools and agents are needed?
4. **Choose trigger** — pick a memorable slash command (e.g., /my-workflow)
5. **Write definition** — create the SKILL.md content
6. **Save** — use the SkillCreate tool to persist the skill

## Skill Definition Guidelines
- **Name**: kebab-case, descriptive (e.g., `api-integration-test`)
- **Description**: one line explaining what the skill does
- **Trigger**: slash command, short and memorable
- **Agents**: only list agents that are actually needed
- **Tools**: only list tools the workflow uses
- **Body**: clear steps, output format, and rules

## Rules
- Skills should be general enough to reuse, specific enough to be useful
- Include concrete output format expectations
- Document anti-patterns and rules learned from experience
- Test the skill by invoking it after creation
