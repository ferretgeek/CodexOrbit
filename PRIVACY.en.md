# Privacy

[简体中文](PRIVACY.md)

Codex Orbit is designed around local processing, minimal reads, and no project
telemetry.

## Data the project does not collect

- The application contains no analytics, advertising, or crash-reporting SDK.
- It sends no data to the project maintainers.
- It does not read or store Codex access tokens, refresh tokens, API keys, or
  `%USERPROFILE%\.codex\auth.json`.
- It never asks users to paste passwords, cookies, or tokens.

## Live quota mode

Codex Orbit starts a local `codex app-server` child process and exchanges JSONL
messages over redirected standard input and output. The installed Codex runtime
handles authentication, token refresh, and communication with OpenAI services.
Codex Orbit requests the account state and quota windows required by the
protocol. The current `account/read` response can contain the account email.
Immediately after JSON deserialization, the application discards the email and
all other unrelated account fields, retaining only the authentication
requirement, account type, and plan type in memory. The email is never used by
application logic, persisted, or written to diagnostics.

Codex Orbit does not pass `--analytics-default-enabled`. App Server analytics
are controlled by the installed Codex runtime and the user's Codex
configuration; see the official Codex documentation for those upstream
settings.

The project has no update service, analytics endpoint, or maintainer-operated
server.

## Local snapshot fallback

When the live account service is unavailable, the application reads:

```text
%USERPROFILE%\.codex\sessions
```

The read is limited to:

- at most 160 session files from the last 10 days;
- at most the final 256 KiB of each file;
- records containing `rate_limits`;
- quota windows, percentages, reset times, and plan type only.

Prompts, responses, and complete logs are not uploaded, copied, or persisted.
The parsed fallback cache exists only in memory.

## Local persistence

UI preferences are stored in:

```text
%LOCALAPPDATA%\CodexQuota\settings.json
```

They contain theme, window position, opacity, display mode, notification
threshold, and optional plan-label override—never account identifiers or
credentials.

If autostart is enabled, Codex Orbit writes:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CodexOrbit
```

When a local executable must be prepared from the installed Windows App
package, it is copied to:

```text
%LOCALAPPDATA%\CodexQuota\runtime
```

That directory contains only Codex runtime executables copied from the user's
existing installation.

After an unhandled error, the application may create:

```text
%LOCALAPPDATA%\CodexQuota\error.log
```

This file remains local and is never uploaded automatically. Each diagnostic
entry is limited to approximately 32 KiB, and a file over approximately
512 KiB is rotated before the next write. The application redacts the actual
`%USERPROFILE%` and `%LOCALAPPDATA%` paths as well as common GitHub-token and
API-key formats. Exception messages and call context can still contain
identifying information, so manually review and remove prompts, account
details, and other personal context before sharing.

## Delete local data

1. Exit Codex Orbit from the tray menu.
2. Disable autostart in the menu, or remove the registry value above.
3. Delete `%LOCALAPPDATA%\CodexQuota`.

Codex Orbit maintains no project cloud account, so there is no remote project
data to request for deletion.
