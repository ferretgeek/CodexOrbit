# Changelog

[简体中文](CHANGELOG.md)

Codex Orbit follows Semantic Versioning where practical. Compatibility code
may need to change when the upstream experimental Codex App Server protocol
changes.

## 3.2.1 - 2026-08-08

- Immediately discard email and unrelated account fields after deserializing
  `account/read`; retain only the authentication requirement, account type, and
  plan type in cache.
- Redact user paths and common secret formats in local `error.log` entries,
  bound entry length, rotate the file, and document its scope in both privacy
  and support guides.
- Extend current-schema coverage for Business and Enterprise plan identifiers,
  account-cache minimization, and diagnostic redaction.
- Run Build only for pushes and pull requests targeting `main`, eliminating
  duplicate push-plus-PR runs and their redundant failure notifications.
- Pin GitHub Actions to complete commit SHAs and constrain CI to Windows Server
  2022 with the Visual Studio 2022 toolchain family.
- Add C# CodeQL `security-extended` analysis and GitHub build-provenance
  attestations for Release assets.
- Build ZIPs with stable entry ordering and commit-derived timestamps for
  stable packaging under the same source, content, and toolchain; no claim is
  made for binary identity across compiler versions.
- Group monthly Dependabot updates for GitHub Actions and complete the
  bilingual documentation and v3.2.1 release notes.

## 3.2.0 - 2026-08-07

- Merge sparse `account/rateLimits/updated` payloads with the latest complete
  snapshot, matching the current App Server schema.
- Keep the mini window on-screen and preserve its nearest screen edge when its
  content width changes.
- Restore a docked mini window to the monitor where it was last positioned.
- Add a 15-second retry backoff after live-service failures while retaining
  the existing 30-second snapshot grace period.
- Pin App Server input to BOM-less UTF-8 so inherited hosted-console encodings
  cannot corrupt the first JSONL message.
- Dispatch notifications independently and publish live snapshots directly,
  preventing a busy thread pool from delaying quota updates.
- Validate persisted settings before applying them.
- Make shutdown and background refresh callbacks race-safe.
- Extend deterministic coverage for sparse notifications, local-log parsing,
  quota thresholds, plan mapping, reset notifications, and window placement.
- Add bilingual project documentation, support and community files, issue and
  pull-request templates, release notes, checksums, and tag-driven releases.
- Allow a larger fake-runtime cold-start budget on hosted CI runners.
- Update the official GitHub Actions to their current v7 major versions.

## 3.1.2 - 2026-08-07

- Process `account/rateLimits/updated` notifications and retain a five-second
  safety poll.
- Support App, CLI, PATH, IDE extension, packaged Windows runtime, and WSL
  discovery.
- Prefer the primary Codex quota so model-specific Spark limits do not replace
  the main display.
- Make the primary reset time prominent.
- Add deterministic notification tests and harden runtime caching.

## Unreleased

Record unreleased changes here, then move them into a versioned section when a
release is prepared.
