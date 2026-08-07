# Changelog

[简体中文](CHANGELOG.md)

Codex Orbit follows Semantic Versioning where practical. Compatibility code
may need to change when the upstream experimental Codex App Server protocol
changes.

## 3.2.0 - 2026-08-07

- Merge sparse `account/rateLimits/updated` payloads with the latest complete
  snapshot, matching the current App Server schema.
- Keep the mini window on-screen and preserve its nearest screen edge when its
  content width changes.
- Restore a docked mini window to the monitor where it was last positioned.
- Add a 15-second retry backoff after live-service failures while retaining
  the existing 30-second snapshot grace period.
- Validate persisted settings before applying them.
- Make shutdown and background refresh callbacks race-safe.
- Extend deterministic coverage for sparse notifications, local-log parsing,
  quota thresholds, plan mapping, reset notifications, and window placement.
- Add bilingual project documentation, support and community files, issue and
  pull-request templates, release notes, checksums, and tag-driven releases.
- Update official GitHub Actions to their current major versions.

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
