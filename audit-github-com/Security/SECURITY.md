# Security Policy

## Supported versions

| Version | Supported          |
|---------|--------------------|
| `main` (latest release) | ✅ Yes |
| Older releases | ❌ No |

## Reporting a vulnerability

This installer runs with administrator privileges and downloads/executes server
components, so we take security reports seriously.

**Please do NOT open a public GitHub issue for security vulnerabilities.**

Instead, report privately via one of:

- **GitHub Private Vulnerability Reporting:**
  https://github.com/DLinacre/azaroth-installer/security/advisories/new
- **Email:** `[replace with a monitored address — e.g. security@<your-domain>]`

Include:
- Description of the issue and its impact,
- Steps to reproduce (PoC where safe),
- Affected version / commit,
- Any suggested remediation.

We aim to acknowledge within **72 hours** and provide a fix plan within
**14 days**, depending on severity.

## Scope

In scope:
- The `setup.exe` installer and all C# source in `src/`.
- Build/release integrity (signing, checksums, artifacts).

Out of scope (report upstream instead):
- Vulnerabilities in [AzerothCore](https://github.com/azerothcore/azerothcore-wotlk)
  or [mod-playerbots](https://github.com/mod-playerbots/mod-playerbots).
- Vulnerabilities in third-party repacks not produced by this project.
- Reports requiring social engineering or physical access.

## Safe defaults

- The installer should run a **localhost-only** server unless you explicitly
  enable LAN play.
- Default GM/DB passwords are **randomized on first run** (from v1.1+); change
  any credentials you set before exposing a server to a network.
- Every released `setup.exe` is published with a **SHA-256 checksum** and,
  starting with the first signed release, an **Authenticode signature**. Verify
  both before running.
