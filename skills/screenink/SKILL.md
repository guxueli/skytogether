---
name: screenink
description: Use ScreenInk MCP tools to read the current screen-annotation context and infer the user's likely intent. Use when the user is drawing/boxing/annotating on screen and asks what they mean, wants a task list, or wants an agent to act on visual feedback.
---

# ScreenInk

ScreenInk is a Windows transparent screen-ink layer. It exposes a local MCP server:

```
ScreenInk.exe --mcp
```

The GUI must also be running for the MCP server to read context from `127.0.0.1:3212`.

## Tools

- `screenink_get_context` — target window, tool, recent annotations, events, heuristic `intent`, suggested prompts.
- `screenink_get_annotations` — recent annotation records.
- `screenink_get_events` — recent app events.
- `screenink_get_target` — current target window/bounds.
- `screenink_get_intent` — only the heuristic intent block.

## How to use

1. Call `screenink_get_context`.
2. Read `target`, `recentAnnotations[].text`, `recentAnnotations[].tool`, and `intent`.
3. Treat `intent` as a **hint**, not ground truth. If `confidence` is low or evidence is empty, say so and ask one clarifying question.
4. When the user wants action, convert only the annotations and target context into a plan/task list; never claim the agent saw pixels that ScreenInk did not provide.
5. Respect privacy: target titles and annotation text are local, potentially sensitive. Do not send them to remote services without user permission.

## Limits

- ScreenInk A is visual/coordinate ink; it does not read app memory or object models.
- B (UI Automation/CDP/DOM/OCR object-level annotation) is sealed; do not invent object identity.
