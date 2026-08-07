# Publishing

[简体中文](PUBLISHING.md)

This directory is the repository root; do not nest it inside another
same-named directory.

## Repository settings

Before a production release, verify:

- the repository is public and `main` is the default branch;
- Issues, Discussions, and private vulnerability reporting are enabled;
- Actions are limited to GitHub-owned actions and
  `microsoft/setup-msbuild`, with complete commit-SHA pinning required;
- `main` branch protection requires Build's `windows` check and CodeQL's
  `analyze-csharp` check;
- dependency graph, Dependabot, and secret scanning remain enabled where
  available;
- topics include `codex`, `chatgpt`, `windows`, `wpf`, `dotnet-framework`,
  and `quota-monitor`.

Workflows use `windows-2022` and the Visual Studio 2022 toolchain range. Every
action reference is pinned to a complete commit SHA; grouped monthly
Dependabot pull requests identify updates.

## Local release build

Run:

```powershell
.\scripts\package.ps1
```

The script builds, tests, and writes:

```text
artifacts\CodexOrbit-<version>-windows-x64.exe
artifacts\CodexOrbit-<version>-windows-x64.zip
artifacts\SHA256SUMS.txt
```

The ZIP contains the executable, complete Chinese and English documentation,
preview images, and an internal executable checksum. ZIP entries are sorted
and receive a common timestamp derived from the current commit. Repeated
packaging should match when source, content, and compiler toolchain match.
This is not a bit-for-bit promise across Visual Studio, MSBuild, or compiler
versions; the SHA-256 published with each Release is authoritative for that
asset.

## Tag-driven release

1. Update `AssemblyVersion`, `AssemblyFileVersion`, and
   `AssemblyInformationalVersion` in
   `src\CodexOrbit\Properties\AssemblyInfo.cs`.
2. Update both changelogs and add bilingual
   `docs/releases/v<version>.md` notes.
3. Run the complete deterministic suite, redacted live check, UI preview QA,
   and two-pass package consistency check.
4. Review the working tree, commit the verified source, and push it.
5. Wait for `windows` and `analyze-csharp` to pass.
6. Create and push the matching annotated tag, for example `v3.2.1`.

The `release` workflow rebuilds and tests the tag, verifies that the tag
matches the binary version, publishes the EXE, ZIP, and checksum file, and
creates GitHub build-provenance attestations for all three assets. It uses the
bilingual release-notes file when present.

## Privacy and security audit

Search tracked source before publishing:

```powershell
rg -n -i --hidden `
  --glob '!bin/**' `
  --glob '!obj/**' `
  --glob '!artifacts/**' `
  'C:\\Users|token|password|secret|api[_-]?key|auth\.json|rollout-.*\.jsonl'
```

Every result must be documentation, a security scanning rule, or protocol
terminology—not a real value. Also verify:

- no `bin/`, `obj/`, `artifacts/`, executable, PDB, log, dump, settings,
  credential, or session file is tracked;
- protocol fixtures contain no real email, account identifier, or credential;
- screenshots were generated in preview mode and contain no real account data;
- release assets contain no build-machine path, user data, or runtime cache;
- `SHA256SUMS.txt` validates both downloadable assets;
- local `error.log` is absent from source and release packages;
- the `build`, `codeql`, and tag-driven `release` workflows all pass.

## Post-release verification

Download fresh assets from the Release page instead of reusing local files:

```powershell
Get-FileHash .\CodexOrbit-3.2.1-windows-x64.exe -Algorithm SHA256
gh attestation verify .\CodexOrbit-3.2.1-windows-x64.exe `
  --repo ferretgeek/CodexOrbit
```

Verify the ZIP and `SHA256SUMS.txt` as well, then start the EXE on Windows and
check the tray, mini view, ring view, refresh, and exit paths. Provenance
confirms that an asset was built by the named GitHub repository workflow; it
is not a commercial Authenticode signature and does not automatically suppress
SmartScreen's unknown-publisher warning.
