# Publishing

[简体中文](PUBLISHING.md)

This directory is the repository root; do not nest it inside another
same-named directory.

## Repository setup

Recommended settings:

- public repository named `CodexOrbit`;
- default branch `main`;
- Issues and Discussions enabled;
- private vulnerability reporting enabled;
- branch protection that requires the `build` workflow;
- repository topics: `codex`, `chatgpt`, `windows`, `wpf`,
  `dotnet-framework`, `quota-monitor`.

The repository should be created empty because this directory already contains
the README, license, ignore rules, workflows, and community files.

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

The ZIP contains the executable, the complete Chinese and English
documentation set, preview images, and an internal executable checksum.

## Tag-driven release

1. Update `AssemblyVersion`, `AssemblyFileVersion`, and
   `AssemblyInformationalVersion` in
   `src\CodexOrbit\Properties\AssemblyInfo.cs`.
2. Update both changelogs and add `docs/releases/v<version>.md`.
3. Run deterministic tests, the optional redacted live check, UI preview QA,
   and packaging.
4. Commit the verified source.
5. Create and push the matching annotated tag, for example `v3.2.0`.

The `release` workflow verifies that the tag matches the binary version,
rebuilds from the tag, runs tests, publishes the EXE, ZIP, and checksums, and
uses the bilingual release-notes file when present.

## Privacy and release audit

Search tracked source before publishing:

```powershell
rg -n -i --hidden `
  --glob '!bin/**' `
  --glob '!obj/**' `
  --glob '!artifacts/**' `
  'C:\\Users|token|password|secret|api[_-]?key|auth\.json|rollout-.*\.jsonl'
```

Every match must be documentation or protocol terminology, not a real value.
Also verify:

- no `bin/`, `obj/`, `artifacts/`, PDB, log, dump, settings, credential, or
  session file is tracked;
- screenshots were generated in preview mode and contain no real account data;
- the packaged EXE has no build-machine path;
- `SHA256SUMS.txt` validates both downloadable assets;
- the release page identifies the project as unofficial and unsigned.

## Post-release verification

Download the published asset, verify its SHA-256, start it on Windows, confirm
the tray icon and both display modes, and check that the GitHub Actions run
completed successfully.
