---
name: media-gen
description: Image, video, and audio generation via AI services
trigger: /media
tools: [WebFetch, Bash, Write, Read]
---

# Media Generation

Generate images, video, or audio using AI generation services.

## Steps
1. **Brief** — clarify the media type, style, dimensions, and purpose
2. **Craft prompt** — write an optimised generation prompt:
   - Be specific about subject, composition, lighting, style
   - Include negative prompts (what to avoid)
   - Specify aspect ratio and quality level
3. **Generate** — call the appropriate generation service
4. **Review** — evaluate the output against the brief
5. **Iterate** — refine the prompt and regenerate if needed
6. **Deliver** — save the final output with metadata

## Prompt Guidelines
- Lead with the subject, then style, then details
- Use concrete descriptors over abstract ones
- Specify the medium: "oil painting", "3D render", "photograph"
- Include lighting: "soft natural light", "dramatic backlighting"
- State the mood: "serene", "energetic", "mysterious"

## Rules
- Always confirm the brief before generating
- Save generation prompts alongside outputs for reproducibility
- Respect content policies — no harmful or misleading media
