[![Codex quota widget](assets/social-preview.png)](https://github.com/ferretgeek/codex-quota-widget/releases/latest)

# Codex quota widget

[中文](README.md) · English

[![Latest release](https://img.shields.io/github/v/release/ferretgeek/codex-quota-widget?display_name=tag)](https://github.com/ferretgeek/codex-quota-widget/releases/latest)
[![License](https://img.shields.io/github/license/ferretgeek/codex-quota-widget)](LICENSE)
[![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?logo=windows&logoColor=white)](#install)

> A small window on your desktop that tells you how much Codex quota is left and when it resets.

## Why this exists

The worst moment when coding with Codex is running out of quota halfway through a thought — one message you're refining logic, the next you're cut off.

To check how much is left, you have to open Codex and go look. Doing that once is fine. Doing it ten times a day is not.

So: a small window parked in the corner of your screen showing the remaining percentage of your primary Codex quota from your ChatGPT plan, plus how many days and hours until it resets. It notifies you when you're nearly out, and again when it's back.

It **never asks you to paste a token, never reads your credential files, and never uploads anything.**

## The interface

Show it as a compact bar, a ring, or both. Six global themes (including Sakura and Mint light palettes), with opacity, always-on-top, click-through, and auto-hide on fullscreen.

| Action | Result |
| --- | --- |
| Left-drag | Move the widget |
| Right-click the widget | Expand quota details, auto-collapses after 3s |
| Right-click again | Collapse immediately |
| `Ctrl` + right-click | Open the settings menu |
| Right-click the tray icon | Open the settings menu |
| Double-click the tray icon | Bring the widget back |

Details show the remaining percentage, days and hours until reset, your plan identifier, and — importantly — **whether that number is live or a local snapshot**, so you always know what you're looking at.

> With click-through enabled the widget can't be clicked. Turn it off from the tray menu.

## Install

### From a release (recommended)

1. Open the [latest release](https://github.com/ferretgeek/codex-quota-widget/releases/latest).
2. Download `CodexOrbit-<version>-windows-x64.exe`, or the ZIP if you want offline bilingual docs.
3. Verify it against `SHA256SUMS.txt`.
4. Put it in a permanent directory and run it.
5. To start it with Windows, enable autostart from the tray menu.

Releases ship with build provenance attestations. With GitHub CLI installed you can confirm the file really came from this repository's Actions:

```powershell
gh attestation verify .\CodexOrbit-<version>-windows-x64.exe --repo ferretgeek/codex-quota-widget
```

> Moving the EXE requires re-enabling autostart once.
>
> Windows SmartScreen may warn about an unknown publisher, because community builds don't carry a commercial code-signing certificate. Check the SHA-256 and the attestation first. **Provenance is not an Authenticode signature and will not make the SmartScreen prompt go away.**

### From source

Open the repository in PowerShell 7 or Windows PowerShell:

```powershell
.\scripts\build.ps1
```

The output lands in `src\CodexOrbit\bin\x64\Release\CodexOrbit.exe`.

You can also open `CodexOrbit.sln` in Visual Studio 2022 and build `Release | x64`. The project uses only assemblies shipped with .NET Framework — no NuGet runtime dependencies.

## Requirements

- Windows 10/11 x64 with .NET Framework 4.8.
- At least one Codex runtime installed (it does not need to be running):
  - Codex Windows App
  - Codex CLI
  - A VS Code / Cursor / Windsurf extension with Codex
  - Codex CLI inside WSL
- **Codex must be signed in with a ChatGPT account.** API-key sign-in can call the API, but that isn't plan quota and this number won't exist — the UI says so explicitly instead of showing zero.

| Your setup | Works? | Notes |
| --- | --- | --- |
| Codex App installed, not running | ✅ | Finds the runtime bundled with the App |
| Codex App running | ✅ | Reuses the same-origin executable to start an independent service |
| Codex CLI installed, not running | ✅ | Located via npm or `PATH` |
| Only an IDE extension | ✅ | Scans common extension directories |
| Only Codex CLI in WSL | ✅ | WSL must also be signed in with ChatGPT |
| No Codex runtime and no local cache | ❌ | Nothing to connect to |
| Signed in with an API key | ❌ no plan quota | The UI flags the auth mismatch |

## Worth noting technically

**It doesn't touch your credentials.** In live mode the app spawns a hidden local `codex app-server` child process on demand and talks to it only over stdio. Authentication, token refresh, and every remote request are handled by Codex itself — `.codex\auth.json` is never read.

**Sparse notifications are merged safely.** Upstream `account/rateLimits/updated` events are incremental and sparse; overwriting wholesale would drop fields. The app merges field by field according to the current protocol rather than replacing state with the last message received.

**There's a fallback, and it tells you it's a fallback.** When live notifications aren't available, it read-only tails `rate_limits` entries from recent Codex session logs. Conversation content is never stored or uploaded, and the UI clearly marks the value as a local snapshot instead of dressing up stale data as live.

**Six runtimes are auto-discovered.** A running App, an installed App, Codex CLI, `PATH`, IDE extension directories, the Windows installer, and WSL — probed in priority order, so having any one of them is enough and you never point at a path by hand.

**The upstream protocol is still moving, so there's slack built in.** The current version is validated against the protocol schema generated by `codex-cli 0.147.0`, while keeping the local-log fallback for temporary compatibility gaps.

Codex sign-in and the `app-server` protocol are documented in [OpenAI's authentication docs](https://learn.chatgpt.com/docs/auth) and the [Codex App Server docs](https://learn.chatgpt.com/docs/app-server); quota rules are in the [Codex pricing and usage docs](https://learn.chatgpt.com/docs/pricing).

## Privacy

These are the only things that may appear locally:

```text
%LOCALAPPDATA%\CodexQuota\
├─ settings.json   UI and notification preferences
├─ runtime\        Runtime cache prepared from an installed Windows App
└─ error.log       Local diagnostic log, created only on an unhandled error
```

The diagnostic log is never uploaded, entries are length-capped, and user directories and common token shapes are redacted — but it can still contain exception context, so **read it before you share it.** None of the above is included in the source repository or release archives.

Full scope in [PRIVACY.en.md](PRIVACY.en.md).

## What it doesn't do

- It doesn't show OpenAI API credit or API rate limits — those are a different thing entirely.
- It doesn't bypass, relax, or refresh any usage limit.
- It doesn't read your credential files or ask you to paste a token or API key.
- No telemetry, no "anonymous analytics."

## Troubleshooting

**It keeps saying no Codex runtime was found.** Confirm the Codex App or CLI is installed and opens normally. CLI users can run `codex --version`, then choose "re-detect runtime" from the tray menu.

More cases in [SUPPORT.en.md](SUPPORT.en.md).

## More documentation

[Architecture](docs/ARCHITECTURE.en.md) · [Operations](docs/OPERATIONS.en.md) · [Privacy](PRIVACY.en.md) · [Changelog](CHANGELOG.en.md) · [Contributing](CONTRIBUTING.en.md) · [Security policy](SECURITY.en.md) · [Support](SUPPORT.en.md) · [Third-party notices](THIRD_PARTY_NOTICES.en.md)

## License and disclaimer

See [LICENSE](LICENSE).

This is an unofficial, community-maintained project with no affiliation with, authorization from, or endorsement by OpenAI. It displays Codex quota from a ChatGPT plan — not OpenAI API credit or API rate limits — and does not bypass any usage limit. All trademarks belong to their respective owners.
