# Install, Upgrade, and Operations

[简体中文](OPERATIONS.md)

## Architecture and server boundary

Codex Orbit is a WPF desktop widget for Windows 10/11 x64. In the current interactive user session it discovers a local Codex runtime, starts a hidden `codex app-server` child process when needed, and communicates only over redirected standard input/output. It opens no listening port and never reads or copies `.codex\auth.json`.

The project intentionally has no Linux daemon, Windows Service, or remote web panel. Quota belongs to the locally signed-in Codex user, while the window, tray, notifications, topmost behavior, and fullscreen detection require an interactive Windows desktop. Republishing authentication or quota over a network would add credential brokering, access control, TLS, rate limiting, and multi-user isolation risks without replacing each user's own Codex sign-in. You may run the normal desktop app inside a controlled RDP or virtual-desktop session, but its state belongs only to that session user and disconnected-session UI behavior depends on host policy. That is not a server mode.

## Install

1. Use Windows 10/11 x64 with .NET Framework 4.8 and at least one installed Codex App, CLI, supported IDE extension, or WSL CLI signed in with ChatGPT.
2. Download the standalone EXE or ZIP and `SHA256SUMS.txt` from a GitHub Release and verify SHA-256. If GitHub CLI is available, also run the README's `gh attestation verify` command.
3. Community binaries are not commercially Authenticode-signed, and provenance does not suppress SmartScreen. Run `scripts\build.ps1` from source if provenance cannot be independently verified.
4. Put the EXE in a stable directory writable only by the intended user. Enable autostart explicitly from the tray if desired; the app never creates it without user action.

## Upgrade and rollback

1. Exit from the tray and confirm that no Codex Orbit process remains.
2. Copy `%LOCALAPPDATA%\CodexQuota\settings.json` to a controlled location. It contains only UI/notification preferences, not tokens. `runtime\` is regenerable cache and `error.log` is diagnostics, not settings backup.
3. Verify the new checksum and provenance, then replace the old EXE. If its path changes, toggle autostart off and on.
4. Start and check theme, display mode, window placement, tray, manual refresh, and live status. Settings are normalized through an allowlist and ranges.
5. To roll back, exit and restore the previous EXE plus the pre-upgrade `settings.json`. Because App Server evolves, an old release may not understand a newer Codex runtime; update Codex Orbit instead of copying old authentication files.

## Backup and restore

- Codex Orbit stores no account credential or quota database. Its only non-regenerable state is `%LOCALAPPDATA%\CodexQuota\settings.json`; copy it only while the app is closed.
- To restore, exit, place a verified `settings.json` at the same path, start, and review every setting. Damaged or future fields are normalized to safe defaults and the allowlist; unknown fields cannot grant capability.
- `runtime\` can be deleted and rediscovered, while `error.log` can be removed after private diagnosis. Never back up or distribute `.codex\auth.json`, session logs, or an entire user profile.

## Health checks

- `codex --version` or Codex App works, and Codex uses ChatGPT sign-in rather than API-key-only authentication.
- Tray and widget are visible. **Refresh now** produces live primary quota, plan, and reset countdown; unavailable service is clearly labeled non-live.
- Mini bar, ring, six themes, opacity, topmost, click-through, fullscreen hiding, and multi-monitor restore follow settings.
- Development/release candidates pass `scripts\test.ps1`. Use `-Live` only for an explicitly authorized read-only check on the current machine. Release packages also pass `scripts\package.ps1` and [`PUBLISHING.en.md`](PUBLISHING.en.md).

## Troubleshooting

- **No Codex runtime:** Confirm at least one App/CLI/IDE/WSL runtime is executable, then refresh from the tray. Never insert an untrusted `codex.exe` into the cache to bypass discovery.
- **Not signed in to ChatGPT:** Sign in through Codex App or CLI. `OPENAI_API_KEY` alone cannot expose ChatGPT-plan quota.
- **Non-live/stale:** Allow the 15-second backoff, check that security software did not block the upstream runtime, and refresh. Old snapshots never trigger low-quota/reset notifications.
- **Bad settings:** Exit, back up, then delete `settings.json` to restore defaults. Never edit it while running.
- **Bad runtime cache:** Exit, delete `%LOCALAPPDATA%\CodexQuota\runtime\`, and restart to rediscover and verify the runtime.
- **Reporting:** Inspect bounded `error.log`, manually remove prompts, account, path, and identity context, and share only a minimal excerpt. Report security issues privately.

## Uninstall

Exit from the tray and disable autostart there if it was enabled. Delete the Codex Orbit EXE/extracted files, then optionally remove `%LOCALAPPDATA%\CodexQuota\`. That permanently removes UI settings, cache, and local diagnostics but does not sign out Codex, delete Codex credentials, or affect Codex App/CLI. Confirm the autostart path is gone before moving or deleting the EXE.
