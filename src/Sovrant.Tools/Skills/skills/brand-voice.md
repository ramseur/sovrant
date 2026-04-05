---
name: brand-voice
description: Extracts durable voice profiles from writing samples
trigger: /brand-voice
tools: [Read, Write, Grep]
---

# Brand Voice Extraction

Analyse 5-20 writing samples to extract a durable, reusable voice profile.

## Steps
1. **Collect samples** — read the provided writing samples
2. **Analyse patterns** — identify recurring:
   - Sentence structure (short/long, simple/complex)
   - Vocabulary level and domain-specific terms
   - Tone markers (formal/casual, authoritative/conversational)
   - Rhetorical devices (metaphors, questions, lists)
   - Perspective (first person, second person, third person)
3. **Identify anti-patterns** — what the writer avoids
4. **Synthesise profile** — create a structured voice guide
5. **Test** — write a sample paragraph using the extracted voice

## Output Format
```
Voice Profile: [Name]

Tone: [e.g., Conversational but authoritative]
Perspective: [e.g., First person singular]
Sentence style: [e.g., Mix of short punchy and medium-length]
Vocabulary: [e.g., Technical but accessible, avoids jargon without explanation]
Signature patterns: [e.g., Opens with a question, uses numbered lists]
Avoids: [e.g., Passive voice, hedge words, exclamation marks]
Sample paragraph: [demonstration]
```
