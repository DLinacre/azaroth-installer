# Phased Action Plan

A practical roadmap from "today's v1.0.0" to a production-grade, trustworthy
installer. Phases are cumulative. Effort estimates assume 1 developer; design/growth
tasks can run in parallel.

---

## 🔴 Immediate (Today) — quick wins, mostly < 1 day each

1. **Randomize default credentials.** Remove `gm/gm1234` and `Azar0th!DB` from
   `config.json`/`AppConfig.cs`; generate a random GM + DB password on first run;
   display it once and require confirmation. Set `rootPassword` to empty and never
   try `test`/`root` against non-bundled servers.
2. **Un-ignore and commit CI.** Delete the `.github/workflows/` line from
   `.gitignore`; add `.github/workflows/build.yml` (see `Developer-Tasks.md`).
3. **Remove hardcoded local paths** from `build.ps1` / `build_repack.ps1`; use
   `$PSScriptRoot` and `Get-Command dotnet`. Move `build_repack.ps1` into `tools/`
   with a "test fixture, not for end users" header.
4. **Fix `FindExistingInstall`** to scan all fixed drives for
   `azaroth-installer.json` (reuse `SysProbe` drive enumeration), not just three
   hardcoded C: paths.
5. **Add `SECURITY.md`** (template provided in `Security/SECURITY.md`).
6. **GitHub discoverability in 10 minutes:** add topics
   (`azerothcore`, `world-of-warcraft`, `wotlk`, `335a`, `playerbots`,
   `private-server`, `installer`, `one-click`, `winforms`, `dotnet`, `csharp`,
   `windows`); set a social preview banner (optimized to <1 MB); add a homepage
   link to the Releases page.
7. **Unify version strings.** Move the 1.1.0 CHANGELOG entry under `##
   [Unreleased]`; set csproj `<Version>` to match the next tag; make the wizard
   header read the assembly version (`FileVersionInfo`) instead of a hardcoded
   "v1.0".
8. **Add README badges** (license, latest release, build once CI is green) and a
   big "⬇ Download setup.exe" button linking to `/releases/latest`.
9. **Publish SHA-256** for `setup.exe` and `config.json` on the v1.0.0 release
   (compute locally, add to release notes).
10. **Fix the log truncation direction** in `WizardForm.cs` (remove from the top,
    not `Clear()`).

---

## 🟠 Short term (1–2 weeks) — high-value improvements

1. **SHA-256 verification in the downloader.** Add optional `"sha256"` to
   `downloads.*` config; verify after download; refuse/override on mismatch.
2. **`--defaults-extra-file` for MySQL.** Replace command-line `--password=` with
   a randomly-named, ACL-restricted temp my.cnf; delete in `finally`.
3. **SQL hardening.** Centralize identifier/value quoting; escape backticks in DB
   names; quote GM username/realm name before interpolating; use
   `MySqlConnector` prepared statements for account/grants where feasible.
4. **Localhost-by-default networking.** Change `listenAddress` default to
   `127.0.0.1`; add an explicit "Allow LAN play" toggle that, when enabled, binds
   to the LAN IP and scopes the firewall rule to the local subnet.
5. **Accessibility pass on the wizard:** nav Labels → Buttons; `AccessibleName`/
   `AccessibleRole` on interactive controls; `AcceptButton`/`CancelButton`;
   `UseSystemPasswordChar` + confirm field; field-level `ErrorProvider`; text +
   icon (not color alone) for step status; visible focus ring.
6. **Screenshots/GIF.** Capture 4 screens (Welcome, System Check, Full Auto
   progress, Done) and embed in README. Record a 30–60 s GIF.
7. **Fix smoke test** to poll `logs/worldserver.log` for the "running" line
   instead of a fixed 45 s; cap at 5 min.
8. **Fix GPU VRAM** reporting (>4 GB cards) via DXGI or registry.
9. **Add a PR template** (`.github/PULL_REQUEST_TEMPLATE.md`) matching
   CONTRIBUTING's testing checklist.
10. **Enable Dependabot** (`dependabot.yml` for NuGet + GitHub Actions) and
    **CodeQL** scanning; add an SBOM step to CI.
11. **Uninstaller.** Add "Remove Azaroth Core" shortcut / wizard mode that stops
    services, optionally removes DB + data, deletes firewall rule and shortcuts,
    restores `realmlist.wtf.orig`.
12. **Resumable downloads** with HTTP `Range` + `.part` resume; show ETA.
13. **"First 5 minutes" card** on the Done screen (login, `lfg`, log location).

---

## 🟡 Medium term (1–3 months) — larger enhancements

1. **Code signing.** Obtain an OV/EV Authenticode certificate; sign `setup.exe`
   and every released DLL/MSI in CI; show "✓ Signed" in README and release notes.
2. **GitHub Pages landing/docs site.** A simple static site (`docs/` Jekyll or plain
   HTML) with: hero + download CTA, screenshots, features, docs, FAQ, JSON-LD
   `SoftwareApplication` schema, Open Graph tags, `robots.txt`, `sitemap.xml`.
   Templates provided in `HTML/`, `Metadata/`, `Schema/`, `Robots/`.
3. **Test project.** xUnit tests for `ZipEx.SafePath`, `AppConfig` (load/merge/
   JSONC/trailing commas), `ServerBuilder` layout detection on synthetic zips, SQL
   quoting, `SysProbe` parsers. Replace `build_repack.ps1` stub with test fixtures.
4. **Design system.** Introduce `Theme.cs` tokens, card/button/label helpers,
   consistent spacing scale and focus styles; support Windows High Contrast.
5. **PerMonitorV2 high-DPI**; test at 100/125/150/200% on 1080p/4K.
6. **Auto-updater.** On launch, compare version to the latest GitHub Release;
   prompt to download + verify (SHA-256) the new `setup.exe`.
7. **Backup/restore.** One-click export of the `azaroth_characters` DB to `.sql`;
   restore on a fresh install.
8. **Community.** Enable GitHub Discussions; stand up a Discord (or list an
   existing one); add a CONTRIBUTING "getting help" path.
9. **Privacy statement + third-party licenses.** In-wizard About box crediting
   AzerothCore (AGPL), MySQL (GPL), etc.; a one-line "no telemetry" notice.
10. **Performance:** enable single-file compression; evaluate trimming; async WMI
    probes with cancellation; optimize banner to WebP.

---

## 🟢 Long term — strategic roadmap

1. **Opt-in AI log-diagnoser** (local or user-provided LLM key; never on by
   default) using TROUBLESHOOTING.md as the knowledge base.
2. **AI/natural-language config generator** on the website → downloadable
   `config.json` preset.
3. **Curated, verified repack catalog.** A community-moderated JSON of known-good
   repacks with pinned SHA-256 hashes and module compatibility; the wizard can
   list them (with big "use at your own risk" + license notices).
4. **Module marketplace/catalog UI** with descriptions, compatibility, and ratings.
5. **Localization** of installer + docs (zhCN, ruRU, esMX, koKR at minimum).
6. **LAN/cloud friend server mode** with optional auth hardening, account
   lockout, and a simple admin web panel (read-only dashboards).
7. **macOS/Linux story** (Docker-based or native) — only if demand warrants; would
   require replacing WinForms with Avalonia/MAUI or a web UI.
8. **Telemetry (strictly opt-in, anonymized)** to guide prioritization, with a
   transparent dashboard.
9. **Formal threat model** and an independent security review before a 2.0 label.

---

## Success metrics to track

- **Download → successful install rate** (derive from release download counts vs
  issues/Discord questions; add an optional, anonymous "I installed successfully"
  button later).
- **Time-to-first-`world server is running`** (the wizard already has the timing
  data — surface it).
- **SmartScreen run-anyway rate** (indirect: support questions mentioning it).
- **Issue resolution time** and **recurring failure signatures** from logs.
- **Stars / forks / Discord members** as reach proxies.
