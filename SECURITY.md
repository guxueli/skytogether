# Security Notes

ScreenInk uses a Windows low-level mouse hook and a transparent topmost window to draw over the desktop.

- The hook only consumes left-button down/up while the user is in annotation mode; mouse moves are passed through.
- No screen pixels are read until the user explicitly exports a PNG.
- No external network, telemetry, or process-memory reading is performed.
- A local-only HTTP bridge (127.0.0.1:3212) exposes target titles/annotation text to local agents; it has no auth, so do not run on shared/untrusted sessions.
- The app is unsigned. Windows SmartScreen/antivirus may warn.
- Do not use the app over password fields, protected video, secure desktop/UAC prompts, or sensitive documents.
- A global hook cannot cover elevated/secure-desktop windows reliably; that is a Windows boundary, not a bug.

Report security issues privately rather than opening a public issue with secrets/screenshots.
