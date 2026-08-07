# Contributing

[简体中文](CONTRIBUTING.md)

Thank you for helping improve Codex Orbit.

## Development environment

- Windows 10/11 x64
- Visual Studio 2022 or an available MSBuild installation
- .NET Framework 4.8 Developer Pack
- PowerShell 7 or Windows PowerShell 5.1

Build and run deterministic tests:

```powershell
.\scripts\test.ps1
```

Optionally test against the signed-in Codex account on the current machine:

```powershell
.\scripts\test.ps1 -Live
```

The live test is read-only, but it depends on a real account and must not run
in CI or appear in screenshots and logs.

## Before opening a pull request

1. Confirm the Release x64 build has zero warnings.
2. Run the complete deterministic test suite.
3. If UI code changed, render and inspect both mini and ring previews.
4. Remove usernames, absolute machine paths, logs, tokens, API keys, and local
   settings.
5. Do not commit `bin/`, `obj/`, executables, PDBs, runtime caches, or real
   account screenshots.
6. Update both Chinese and English documentation for user-visible changes.
7. Add a changelog entry for behavior changes.

## UI previews

After building, generate previews that contain demo data only:

```powershell
.\src\CodexOrbit\bin\x64\Release\CodexOrbit.exe `
  --render-mini-preview "$env:TEMP\codex-orbit-mini.png" `
  --theme midnight

.\src\CodexOrbit\bin\x64\Release\CodexOrbit.exe `
  --render-preview "$env:TEMP\codex-orbit-ring.png" `
  --theme midnight
```

## Diagnostic environment variables

| Variable | Purpose |
| --- | --- |
| `CODEX_ORBIT_CODEX_PATH` | Use a specific Codex executable |
| `CODEX_ORBIT_ONLY_RUNTIME` | Restrict runtime discovery for testing |
| `CODEX_ORBIT_DISABLE_PACKAGED_RUNTIME=1` | Disable packaged App runtime discovery |
| `CODEX_ORBIT_DISABLE_WSL=1` | Disable WSL discovery |

`CODEX_ORBIT_ONLY_RUNTIME` accepts `override`, `running`, `app`, `local`,
`cli`, `path`, `extension`, `package`, or `wsl`.

Never put credentials in these variables, documentation, or bug reports.

## Scope

Keep the project free of telemetry, direct credential storage, and unnecessary
third-party dependencies. Protocol changes must include deterministic coverage
so contributors do not need a real account to verify them.

By participating, you agree to follow the
[Code of Conduct](CODE_OF_CONDUCT.en.md).
