# Audit — Azaroth Core One-Click Installer

A complete professional audit of
[`DLinacre/azaroth-installer`](https://github.com/DLinacre/azaroth-installer) — a
C#/.NET 8 WinForms one-click installer for a private AzerothCore 3.3.5a (WotLK)
server with PlayerBots — conducted on **2026-08-23** against commit `27f3a39`
(release v1.0.0).

> **Note on inputs:** The brief auto-derived the product name as "Github" and the
> market as `[object Object]`. Both were incorrect and have been corrected — see
> [`Assumptions-and-Gaps.md`](Assumptions-and-Gaps.md). GitHub is the *host*;
> **Azaroth Core** is the product; the niche is open-source tooling for the WoW
> private-server / AzerothCore community.

## Overall score: **62 / 100**

A strong v1 functionally, but held back by critical security defaults (hardcoded
credentials, no download verification, unsigned elevated executable), a broken
build/CI story, and low discoverability. Full breakdown below.

## Read the report

| File | What's in it |
|---|---|
| [Executive-Summary.md](Executive-Summary.md) | Scores, strengths/weaknesses, top priorities, verdict |
| [Full-Audit.md](Full-Audit.md) | All 15 categories with observations vs recommendations |
| [Priority-Roadmap.md](Priority-Roadmap.md) | Phased plan: Today → 1–2 weeks → 1–3 months → long term |
| [Developer-Tasks.md](Developer-Tasks.md) | 20 GitHub-issues-style tasks + reference CI/Dependabot/PR templates |
| [Assumptions-and-Gaps.md](Assumptions-and-Gaps.md) | Corrected inputs, scope, evidence, out-of-scope |

## Ready-to-implement assets

| Folder | Contents |
|---|---|
| `Security/` | `SECURITY.md` template + credential/checksum/SQL/firewall fix snippets |
| `SEO/` | GitHub + GitHub Pages SEO checklist |
| `Accessibility/` | WCAG 2.2 AA checklist for the WinForms installer |
| `Performance/` | Binary-size, resumable download, VRAM, image optimisation |
| `Design/` | Design tokens + component guidance (`Theme.cs`) |
| `Content/` | Rewritten README top block, FAQ, release notes, repo description |
| `Metadata/` | Meta/Open Graph/Twitter tags + repo topics/social-preview setup |
| `Schema/` | `SoftwareApplication` + `FAQPage` JSON-LD |
| `Robots/` | `robots.txt` + `sitemap.xml` for a future Pages site |
| `HTML/` | Accessible landing page template (`index.html`) |
| `CSS/` | Matching accessible dark/light responsive stylesheet |
| `JavaScript/` | Minimal progressive-enhancement script (release label, reveal) |

## Scorecard

| Category | Score |
|---|---:|
| Executive Summary | 62 |
| Brand Review | 64 |
| User Experience | 70 |
| User Interface | 66 |
| Content / Copy | 72 |
| SEO Audit | 34 |
| Performance | 63 |
| Accessibility | 41 |
| Security & Privacy | 31 |
| Technical / Bugs | 46 |
| Conversion (CRO) | 58 |
| AI Opportunities | 40 |
| Competitive Positioning | 60 |
| Missing Features | 45 |
| Priority Matrix | 70 |
| **Overall** | **62** |

## The four things that matter most

1. 🔴 **Randomize all default credentials** (GM, DB, root) — never ship
   `gm/gm1234` / `Azar0th!DB` with a server that binds `0.0.0.0` and opens
   firewall ports.
2. 🔴 **Verify every download (SHA-256) and code-sign `setup.exe`** — it runs
   elevated and executes downloaded EXEs/DLLs/MSIs; today there's no integrity
   check.
3. 🔴 **Fix the build/CI** — un-ignore `.github/workflows/`, commit a real
   workflow, and remove the `C:\Users\KingL\...` paths from build scripts.
4. 🟠 **Make it findable and trustworthy** — add repo topics, a social preview,
   screenshots, checksums, a `SECURITY.md`, and an accessible landing page.

See [Priority-Roadmap.md](Priority-Roadmap.md) for the full sequence and
[Developer-Tasks.md](Developer-Tasks.md) for issue-ready tasks.
