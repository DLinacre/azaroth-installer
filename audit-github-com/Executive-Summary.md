# Executive Summary

**Product:** Azaroth Core — One-Click Installer (`DLinacre/azaroth-installer`)
**Audited surface:** GitHub repository + the C#/.NET 8 WinForms installer it ships
**Audit date:** 2026-08-23 · Commit `27f3a39` · Release v1.0.0
**Overall score:** **62 / 100**

A genuinely impressive v1 for a solo/community project: the installer does a lot of
hard things well (hardware probing, repack layout detection, zip-slip protection,
repair mode, live smoke test, thorough docs). But it ships with **several serious,
exploitable security defaults**, a **broken build script** that leaks the author's
local machine paths, a **CI workflow that is git-ignored and therefore never runs**,
and near-zero project discoverability on GitHub. The product is functionally strong;
its trust, supply-chain and go-to-market posture is not yet production-grade.

---

## Scorecard

| # | Category | Score |
|---|---|---:|
| 1 | Executive Summary | **62** |
| 2 | Brand Review | **64** |
| 3 | User Experience | **70** |
| 4 | User Interface | **66** |
| 5 | Content / Copy | **72** |
| 6 | SEO Audit | **34** |
| 7 | Performance | **63** |
| 8 | Accessibility | **41** |
| 9 | Security & Privacy | **31** |
| 10 | Technical / Bugs | **46** |
| 11 | Conversion (CRO) | **58** |
| 12 | AI Opportunities | **40** |
| 13 | Competitive Positioning | **60** |
| 14 | Missing Features | **45** |
| 15 | Priority Matrix | **70** (process score) |
| — | **Overall (weighted)** | **62** |

Weighting note: security and correctness weigh heavily for an installer that runs
**elevated**, downloads executables/zips/MSIs, and provisions databases — hence the
overall score sits below the simple average.

---

## Biggest strengths

1. **Genuinely solves a hard problem.** A self-contained one-click path from
   "I have a WoW client" to "I'm playing on my own GM server with bots" — with
   hardware checks, auto drive selection, repack-agnostic layout detection, database
   resolution, firewall rules, realmlist patching and a boot smoke test. This is far
   beyond a typical shell-script repack.
2. **Thoughtful safety engineering.** Zip-slip protection (`ZipEx.SafePath`),
   character-preserving repair mode, idempotent re-runs, "never re-import existing
   `azaroth_*` DBs", temp-file cleanup in `SqlUtil`, and a deliberate "prefer what's
   on disk" philosophy called out in CONTRIBUTING.
3. **Excellent end-user documentation.** README, `docs/CONFIG.md` (full JSONC
   reference) and `docs/TROUBLESHOOTING.md` (symptom→fix table) are clear, honest
   about the SmartScreen/unsigned reality, and written for non-developers.
4. **Solid installer UX skeleton.** 9-step wizard, Full-Auto mode, live log, download
   progress/speed, marquee indicator, repair detection.
5. **Legal honesty.** Up-front warning about Blizzard ToU, no client redistribution,
   clear credit/license section, MIT license.

## Biggest weaknesses

1. 🔴 **CRITICAL SECURITY — hardcoded default credentials.** `gm/gm1234`, DB user
   `azaroth/Azar0th!DB`, blank `root` password — committed in `config.json` *and*
   embedded in `AppConfig.cs` defaults. The server binds `0.0.0.0`, opens firewall
   ports 3724/8085, and adds a GM account. On any LAN-exposed machine this is a
   trivially compromised, fully-GM'd game server.
2. 🔴 **CRITICAL SECURITY — unsigned 139 MiB `setup.exe` that runs elevated and
   downloads/extracts/executes arbitrary zips/DLLs/MSIs with no checksum/signature
   verification.** Supply-chain compromise is one compromised repack link away.
   There is no SHA-256 pinning anywhere in `Downloader.cs`.
3. 🔴 **BROKEN BUILD / CI.** `.github/workflows/` is listed in `.gitignore`, so the
   CI workflow the README and CHANGELOG promise is **not in the repo and never runs**.
   `build.ps1` and `build_repack.ps1` hardcode `C:\Users\KingL\.gemini\antigravity-ide\...`
   paths — they fail on any other machine and leak the author's environment.
4. 🟠 **SQL/command-injection surface.** SQL strings built by concatenation
   (DB names, GM username/password) and credentials passed on the command line to
   `mysql.exe` via temp `.bat` files (password visible in process args and in the
   temp batch file).
5. 🟠 **Discoverability is essentially zero.** 0 stars, **no repository topics**,
   no GitHub About description link/homepage, no social preview image, no
   Discussions, README uses relative links that break on some renderers, and the
   brand name is misspelled ("Azaroth" vs the canonical "Azeroth") which hurts
   search.
6. 🟠 **Accessibility of the WinForms UI is poor.** No `AccessibleName`/`AccessibleRole`
   anywhere, nav is click-only `<Label>`s (not keyboard operable), low-contrast
   "terminal green" log text on near-black, hardcoded 1080×740 fixed-size window,
   emoji used as the only status indicator, password fields not confirmed masked in
   all paths, no high-DPI scaling beyond SystemAware.
7. 🟡 **Governance gaps for a project that asks users to run it as admin:** no
   `SECURITY.md`, no PR template, no Dependabot, no SBOM, no code-signing plan,
   version mismatch (csproj says 1.0.0, header says "v1.0", CHANGELOG has an
   unreleased 1.1.0), `build_repack.ps1` is a developer-only mock-server generator
   shipped to end users.

---

## Highest-priority improvements (the "do these first" list)

| # | Fix | Why | Effort |
|---|---|---|---|
| 1 | Force a **random GM password + random DB password** on first run; never ship defaults; require password change on first login. | Closes the #1 critical. | S (1–2 days) |
| 2 | Add **SHA-256 checksum verification** for every downloaded zip/MSI/DLL; pin expected hashes in `config.json`; refuse mismatches. | Closes the supply-chain hole. | M (3–5 days) |
| 3 | **Un-ignore `.github/workflows/`**, commit a real `build.yml`, remove the hardcoded `C:\Users\KingL\...` paths; produce signed+hashed releases. | Makes the build reproducible and trustworthy. | S (1–2 days) |
| 4 | Obtain (or document a path to) an **Authenticode code-signing certificate**; publish SHA-256 hashes on every release; add `SECURITY.md`. | Trust + SmartScreen. | M (cost + time) |
| 5 | Switch SQL to **parameterized queries / `--defaults-extra-file`** for MySQL credentials; stop passing passwords in argv/bat files. | Removes injection & credential-leak vectors. | M (3–5 days) |
| 6 | **GitHub discoverability pass:** topics, About link, social preview, absolute README links, Discussions, a `README` hero that states the value prop in one line. | Drives the only conversion that matters (downloads). | S (1 day) |
| 7 | Accessibility pass on `WizardForm.cs`: real buttons for nav, AccessibleNames, contrast, keyboard flow, masked + confirmed password boxes, scalable layout. | WCAG 2.2 alignment, usability. | M (3–5 days) |

---

## Effort vs. business impact (bubble view)

```
IMPACT
  High │  ② checksum     ① random creds   ⑥ GitHub SEO
       │  ⑤ SQL params                      ③ fix CI/build
       │  ④ code-signing (cost)
  Med  │  ⑦ a11y pass    SECURITY.md       banner/social
       │  Dependabot     PR template       website/GH Pages
  Low  │  emoji→icons    perf tweaks       CHANGELOG fix
       └──────────────────────────────────────────────
          Low              Medium             High
                          EFFORT
```

- **Quick wins (high impact, low effort):** random default credentials, un-ignore
  CI, fix hardcoded paths, add topics/About/social preview, fix version strings,
  add `SECURITY.md`, add SHA-256 to release notes.
- **Projects (high impact, medium effort):** checksum verification, parameterized
  SQL / `--defaults-extra-file`, code signing, accessibility pass, a landing page
  (GitHub Pages).
- **Strategic (longer term):** signed auto-updater, optional analytics/consent,
  community/Discussions, multi-language docs, a curated "known good repack"
  allowlist, AI-assisted troubleshooting bot.

---

## Verdict

**Functionally an A− for a v1 community installer; operationally a C−.** The code
shows real engineering care (zip-slip, repair mode, smoke test, great docs). The
gaps are exactly the ones that get people to trust — or abandon — a tool that asks
for administrator rights and downloads executables: **credentials, signatures,
reproducible builds, and discoverability**. Fix the four Critical/High items above
and this jumps from 62 to the low 80s without changing a single feature.
