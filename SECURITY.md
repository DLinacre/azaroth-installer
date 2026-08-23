# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| `main` (latest release) | ✅ Yes |
| Older releases | ❌ No |

## Reporting a Vulnerability

This installer runs with administrator privileges and downloads/executes server components, so security reports are prioritized.

**Please do NOT open a public GitHub issue for security vulnerabilities.**

Instead, report privately via:
- **GitHub Private Vulnerability Reporting:** https://github.com/DLinacre/azaroth-installer/security/advisories/new

Include:
- Description of the issue and its impact,
- Steps to reproduce (PoC where safe),
- Affected version / commit,
- Any suggested remediation.

We aim to acknowledge within **72 hours** and provide a fix plan within **14 days**.

## Scope

In scope:
- The `setup.exe` installer and all C# source in `src/`.
- Build/release integrity (signing, checksums, artifacts).

Out of scope (report upstream instead):
- Vulnerabilities in [AzerothCore](https://github.com/azerothcore/azerothcore-wotlk) or [mod-playerbots](https://github.com/mod-playerbots/mod-playerbots).
- Third-party repacks not produced by this project.
- Reports requiring social engineering or physical access.

## Safe Defaults

- The installer configures a **localhost-only** server (`127.0.0.1`) unless you explicitly enable LAN play.
- Default GM/DB passwords are **cryptographically randomized on first run**; change any credentials before exposing a server to a network.
- Every released `setup.exe` is published with a **SHA-256 checksum**.
