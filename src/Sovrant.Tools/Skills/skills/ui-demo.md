---
name: ui-demo
description: Interactive UI demonstration and prototype creation
trigger: /ui-demo
tools: [Write, Read, WebSearch]
---

# UI Demo

Create interactive UI prototypes as self-contained HTML files.

## Steps
1. **Requirements** — clarify what the UI should demonstrate
2. **Design** — plan layout, components, interactions, and user flow
3. **Build** — create as a single HTML file with:
   - Inline CSS for styling
   - Inline JS for interactivity
   - No external dependencies (CDN links are OK for frameworks)
4. **Polish** — responsive design, hover states, transitions
5. **Deliver** — save as a single file the user can open in a browser

## Design Guidelines
- Use a clean, modern design system (consider Tailwind via CDN if complex)
- Include realistic sample data, not lorem ipsum
- Make interactive elements obviously clickable (hover effects, cursor changes)
- Support both light and dark mode if practical
- Mobile-responsive by default

## Rules
- Must be openable in a browser with no build step
- Include comments explaining the key interaction patterns
- If the demo needs mock data, generate realistic examples
