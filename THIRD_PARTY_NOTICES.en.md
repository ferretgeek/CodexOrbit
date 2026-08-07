# Third-Party and Trademark Notices

[简体中文](THIRD_PARTY_NOTICES.md)

The Codex Orbit application references only assemblies provided by Windows and
.NET Framework 4.8. Release packages do not bundle NuGet runtime libraries.

The application can invoke `codex app-server` from a user-installed Codex App,
Codex CLI, or IDE extension. Those files are not part of this repository and
are not committed or redistributed in project releases. Their licenses, terms,
and privacy policies are governed by their respective providers.

GitHub Actions workflows use GitHub's `actions/*`, `github/codeql-action`,
`actions/attest`, and Microsoft's `microsoft/setup-msbuild`. They run only in
CI, are pinned to reviewed complete commit SHAs, and are not included in the
application package.

OpenAI, ChatGPT, and Codex are trademarks of their respective owners. This
community project is not affiliated with, endorsed by, sponsored by, or
officially supported by OpenAI.
