# Architecture

[简体中文](ARCHITECTURE.md)

## Data flow

```text
Codex App / CLI / IDE / WSL
            │
            │ local stdio JSONL
            ▼
      codex app-server
            │
            ▼
   CodexAppServerClient
            │
            ├─ account/read ─────────────────► field minimization
            ├─ account/rateLimits/updated ──► sparse merge
            └─ account/rateLimits/read ─────► 5-second safety poll
            │
            ▼
     CodexUsageReader
            │
            ├─ primary-limit selection
            ├─ local-log fallback
            └─ change deduplication
            │
            ▼
 MainWindow / MiniStatusWindow / Tray
```

## Main modules

- `CodexAppServerClient` discovers runtimes, owns the child process, and sends
  and receives protocol messages.
- `CodexUsageReader` merges complete reads and sparse updates, selects the
  primary Codex quota, and falls back to local snapshots.
- `MainWindow` coordinates the ring, tray menu, notifications, and display
  modes.
- `MiniStatusWindow` renders the compact quota and reset view.
- `AppSettings` persists UI and notification preferences only.
- `UsageProbe` provides a fake App Server plus deterministic and optional live
  integration checks.

## Runtime discovery order

1. Explicit `CODEX_ORBIT_CODEX_PATH` override.
2. A running `codex.exe`.
3. Codex Windows App local runtime.
4. A previously prepared Codex Orbit runtime cache.
5. npm Codex CLI and `%USERPROFILE%\.codex\bin`.
6. `codex.exe`, `codex.cmd`, or `codex.bat` on PATH.
7. Common VS Code, Cursor, and Windsurf extension directories.
8. Installed Windows App package.
9. Codex CLI in WSL distributions.

Candidates are tried in order. An incompatible, unavailable, or
non-ChatGPT-authenticated runtime does not prevent later candidates from being
tested.

## Live updates

After initialization, `account/rateLimits/read` establishes a complete
snapshot. Current App Server `account/rateLimits/updated` notifications are
sparse: fields such as window duration and reset time may be absent. Codex
Orbit recursively merges non-null notification fields into the latest complete
response before parsing it.

The upstream `account/read` response can contain an email address. Immediately
after deserialization and before caching, the client rebuilds a minimal
response containing only `requiresOpenaiAuth`, `account.type`, and
`account.planType`. Email and other unrelated fields never enter the account
cache.

A five-second timer acts as a safety poll. Failed live reads use a 15-second
retry backoff. The most recent live snapshot can remain visible as explicitly
non-live for up to 30 seconds to avoid flicker.

## Local fallback

If every live source is unavailable, the reader scans only the tail of recent
Codex session JSONL files and extracts `rate_limits` records. The UI marks this
mode as non-live and does not emit low-quota or reset notifications from stale
history.

## Security boundaries

- Never read `auth.json` or accept a user-provided token.
- Keep App Server communication on redirected local standard streams.
- Verify copied runtime-cache files with SHA-256.
- Keep runtime cache cleanup inside `%LOCALAPPDATA%\CodexQuota\runtime`.
- Replace settings through a temporary file and validate values before use.
- Keep unhandled-error diagnostics in a local, size-bounded `error.log`; redact
  user directories and common token/API-key formats before writing, and never
  upload the file.
- Do not bundle user data, runtime caches, PDBs, or build paths in releases.

The App Server interface is an evolving upstream dependency. Deterministic
fixtures must cover every protocol compatibility change.
