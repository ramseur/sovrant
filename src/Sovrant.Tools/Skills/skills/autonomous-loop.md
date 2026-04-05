---
name: autonomous-loop
description: Configurable autonomous execution loops with multiple patterns
trigger: /loop
tools: [Bash, Read, Write, Edit, Grep, Glob, Agent, TaskCreate, TaskUpdate, TaskList, Sleep]
---

# Autonomous Loop

Execute a recurring task or workflow in a loop with configurable patterns.

## Loop Patterns

### 1. Simple Poll
Repeat a single command on an interval.
```
Check [target] every [interval] until [condition]
```

### 2. Pipeline
Sequential steps that repeat as a unit.
```
Step 1 → Step 2 → Step 3 → (repeat)
```

### 3. Watch and React
Monitor a source for changes, act when detected.
```
Watch [source] → on change → [action] → wait
```

### 4. Retry with Backoff
Attempt an operation, retry on failure with increasing delay.
```
Try [action] → on fail → wait [backoff] → retry (max N)
```

### 5. Fan-Out/Fan-In
Parallel execution with result collection.
```
Dispatch [tasks] in parallel → collect results → merge
```

### 6. DAG
Dependency-aware execution graph.
```
Tasks with dependencies → topological sort → execute respecting order
```

## Rules
- Always have a termination condition (max iterations, timeout, or success condition)
- Log each iteration's result
- Respect rate limits and resource constraints
- Use Sleep tool for intervals — don't busy-wait
