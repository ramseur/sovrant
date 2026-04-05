---
name: team-builder
description: Interactive agent team selection and parallel dispatch
trigger: /team
agents: [chief-of-staff]
tools: [Agent, TaskCreate, TaskUpdate, TaskList, Read]
---

# Team Builder

Assemble a team of specialised agents and dispatch them in parallel to complete a complex task.

## Steps
1. **Analyse task** — break down the task into parallelisable sub-tasks
2. **Select agents** — choose agent templates matching each sub-task's needs
3. **Brief agents** — write clear, self-contained briefs for each agent
4. **Dispatch** — launch agents in parallel using the Agent tool
5. **Monitor** — track progress via TaskList
6. **Synthesise** — merge agent outputs into a coherent result

## Agent Selection Guidelines
- **Coder** — implementation tasks
- **Code-reviewer** — review and quality tasks
- **Researcher** — information gathering
- **Architect** — design and planning
- **Security-reviewer** — security analysis
- Use the minimum number of agents needed — don't over-parallelise

## Rules
- Each agent brief must be self-contained (no assumed context)
- Set clear success criteria for each agent
- If an agent fails, diagnose before retrying
