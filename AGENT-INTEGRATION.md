# Agent Integration

ScreenInk exposes a **local-only** read-only HTTP bridge while the app is running:

```
http://127.0.0.1:3212
```

It is intended for local agents/scripts. No remote network access is made by ScreenInk.

## MCP server

Start ScreenInk.exe --mcp as a stdio MCP server while the GUI is running. Use docs/mcp-config.example.json as the client config template.

MCP tools:

- screenink_get_context
- screenink_get_annotations
- screenink_get_events
- screenink_get_target
- screenink_get_intent

A skill template is in skills/screenink/SKILL.md.

## Endpoints

- `GET /health` — liveness and API URL.
- `GET /context` — current target, tool, shape count, recent annotations, recent events, heuristic intent, suggested agent prompts.
- `GET /annotations` — latest local annotation records.
- `GET /events` — recent in-app events (start/stop/save/export/load).

Example:

```powershell
curl http://127.0.0.1:3212/context
```

## Context shape

```json
{
  "app": "ScreenInk",
  "version": "0.1.0-alpha",
  "active": true,
  "tool": "box",
  "target": { "title": "OpenCode", "app": "OpenCode", "hwnd": "123", "screen": { "x": 0, "y": 0, "w": 1280, "h": 800 } },
  "recentAnnotations": [ ... ],
  "events": [ ... ],
  "intent": {
    "summary": "用户可能在提出修改意见或验收反馈",
    "tags": ["change", "feedback"],
    "confidence": 0.74,
    "evidence": ["这里应该改成..."]
  },
  "suggestedPrompts": [
    "请根据我最近的屏幕标注，总结我想解决的问题。"
  ]
}
```

## Intent inference

`intent` is a local heuristic, not a mind reader:

- annotation text keywords (问题/错误/建议/讲解/重点...)
- target title/app (video/meeting/demo...)
- tool and number of boxes/lines
- recent annotation count/time

Agents should treat it as a hint with `confidence` and evidence, not ground truth. Object-level intent requires the future B adapters (UIA/CDP/DOM/OCR), currently sealed.

## Suggested agent prompts

Agents can use:

- “根据最近屏幕标注判断我想做什么，并给出下一步建议。”
- “把标注整理成任务清单，标注位置和原文。”
- “区分这是讲解、评审还是问题报告。”

## Security

The bridge binds to `127.0.0.1` only and has no authentication. Any local process can read target titles and annotation text. Do not use it on a shared/untrusted machine with sensitive content.
