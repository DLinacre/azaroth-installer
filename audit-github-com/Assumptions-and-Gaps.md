# Assumptions & Gaps

## Auto-resolved inputs — corrections

The mission brief contained two auto-resolved fields that are **wrong** and have been
corrected for this audit:

| Brief field | Auto-resolved value | Corrected value |
|---|---|---|
| Product / brand name | "Github" | **Azaroth Core — One-Click Installer** (the actual product hosted on the GitHub repo `DLinacre/azaroth-installer`). GitHub is merely the *hosting platform*, not the brand. |
| Market / niche | `[object Object]` (a serialization bug) | **Open-source desktop tooling for the World of Warcraft private-server / AzerothCore community** — specifically a Windows one-click installer for AzerothCore 3.3.5a (WotLK) with the PlayerBots module. |
| Target type | Website / Web app | The target is a **GitHub repository** that distributes a **Windows desktop installer** (`setup.exe`, C# / WinForms / .NET 8). There is no standalone marketing website. |

## What "the website" actually is

Because the product has no independent website, the **publicly accessible web
experience** is the GitHub repository surface:

- Repository home (`/DLinacre/azaroth-installer`) — README = landing page
- Releases page (download CTA)
- `docs/CONFIG.md`, `docs/TROUBLESHOOTING.md`
- Issue templates, CONTRIBUTING, CODE_OF_CONDUCT, CHANGELOG, LICENSE

The audit therefore evaluates **two surfaces together**:

1. **The web/repository presence** (brand, content, SEO, discoverability, CRO for the
   "land → read → download" journey on GitHub).
2. **The product itself** (the WinForms installer) for UX, UI, accessibility,
   performance, security, code quality and bugs — since the product *is* the
   experience users convert into.

Standard website-only checks (robots.txt, sitemap.xml, Core Web Vitals field data,
CSP/HSTS response headers on a first-party origin) are **N/A for a GitHub-hosted
repo** and are marked as such; GitHub.com already supplies those headers at platform
level. Where an equivalent check makes sense (e.g. a recommended `robots.txt`/sitemap
**if** the project later adds GitHub Pages), assets are still provided.

## Evidence gathered

- Full clone of `main` at commit `27f3a39` (2026-08-23) — all 11 C# source files
  (~4,400 LOC), both docs, build scripts, assets, config.
- GitHub REST API: repo metadata, topics, releases/assets.
- Direct inspection of `config.json`, `SqlUtil.cs`, `Downloader.cs`, `ServerBuilder.cs`,
  `WizardForm.cs`, `Misc.cs`, `ZipEx.cs`, `Program.cs`.
- Banner/icon image inspection (dimensions, file size).
- Release v1.0.0 assets confirmed: `setup.exe` (145,940,105 bytes ≈ 139 MiB) and
  `config.json` (2,916 bytes).

## Out of scope / not testable without a Windows VM

- Live runtime behaviour of `setup.exe` (SmartScreen, UAC, actual extraction/smoke
  test) — assessed from source only.
- Field Core Web Vitals for GitHub's repo page (Google doesn't expose CrUX for the
  arbitrary repo path; GitHub's own performance is platform-managed).
- Dynamic/penetration test of a running install.

All security findings below are **static-analysis observations** with defensive,
non-exploit remediation guidance.
