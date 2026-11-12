# Contributing

ScreenInk is Windows-only and intentionally small.

- Build: `build.cmd` (Windows .NET Framework csc).
- Keep A (visual screen ink) and B (object-level adapters) separate.
- Do not add process-memory reading, injection, telemetry, or network calls without an explicit design discussion.
- For app compatibility reports, include Windows version, display scaling, target app name/version, and whether the overlay was above the app.
- Keep the single visible control-panel model; the overlay must remain a background layer.
