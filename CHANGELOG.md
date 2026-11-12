# Changelog

## v0.1.0-alpha

- Windows 10/11 transparent screen ink overlay.
- Window selection/lock; minimized target hides overlay and restores on restore.
- Long-press Alt + left drag to draw line, arrow, freehand, box; polyline tool uses left-click points and right-click finish.
- Inline note editor at draw endpoint/box center.
- Local JSONL records in `%APPDATA%\ScreenInk\annotations.jsonl`.
- Restores saved annotations for the same target app/title or HWND.
- Offscreen bitmap rendering and dirty-region updates.
- Save/open overlay files via file dialogs; browse recent save folder.
- Select/move tool: drag whole annotation or point handles; edit note/delete selected.
- Export annotated PNG to Pictures\ScreenInk.
- No network, no process-memory reading, no object-level semantics.
