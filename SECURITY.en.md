# Security Policy

[简体中文](SECURITY.md)

## Supported versions

Security fixes are provided for the latest published release. Users should
upgrade before reporting an issue that may already be fixed.

## Report a vulnerability

Use GitHub **Security → Report a vulnerability** to submit a private report.
If private vulnerability reporting is unavailable, open a public issue that
contains no sensitive details and ask the maintainer for a private contact
channel.

Include:

- affected version;
- Windows and Codex runtime environment;
- minimal reproducible steps;
- expected and actual behavior;
- impact and a possible remediation direction.

Never submit real tokens, API keys, cookies, complete session logs, usernames
in paths, or account screenshots. Redact prompts, responses, account details,
and local paths from any diagnostic material.

## Security boundaries

- Codex Orbit must not read Codex credential files.
- Communication with `codex app-server` must remain on redirected local
  standard streams.
- Release archives must not contain cached runtimes, user settings, session
  logs, build paths, or debug symbols.
- Every release binary and ZIP must have a published SHA-256 checksum.
- The upstream App Server protocol is experimental; compatibility regressions
  are not automatically security vulnerabilities, but reports are welcome.
