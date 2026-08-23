# Developer / Product Task List (GitHub Issues style)

Copy each block into a GitHub issue. Labels: `security`, `bug`, `ci`, `a11y`,
`docs`, `enhancement`, `ux`, `growth`, `perf`.

---

### T-01 · Generate random default GM & DB passwords (never commit credentials)

- **Priority:** 🔴 Critical · **Effort:** S (1–2 days) · **Owner:** eng / security
- **Description:** `config.json` and `AppConfig.cs` currently ship working
  credentials (`gm/gm1234`, `azaroth/Azar0th!DB`, blank root). On first run, the
  wizard must generate a strong random DB password and require the user to set (or
  generate) a GM password, shown once with a "copy" button. Blank out
  `database.rootPassword` and never probe arbitrary/existing MySQL with `""`,
  `"test"`, `"root"` except for the installer's own fresh/bundled install.
- **Acceptance criteria:**
  - [ ] No working credential is committed in the repo.
  - [ ] First run generates random DB password (≥ 20 chars) and stores it only in
        the install dir with restrictive ACLs.
  - [ ] GM password field is masked, confirmed, and validated (min 8 chars).
  - [ ] Existing-install repair path still connects (reads saved creds).
  - [ ] Docs updated to remove `gm1234`.
- **Files:** `src/config.json`, `src/AppConfig.cs`, `src/ServerBuilder.cs`
  (≈L408–421, 528, 625), `src/WizardForm.cs`, `docs/*.md`, `README.md`.

---

### T-02 · SHA-256 verification for all downloads

- **Priority:** 🔴 Critical · **Effort:** M (3–5 days) · **Owner:** eng / security
- **Description:** `Downloader.DownloadOneAsync` saves bytes unverified. Add an
  optional per-URL `"sha256"` field in config; after download, stream-hash the
  file and compare. On mismatch, delete the file, log a prominent error, and abort
  (with an "I trust this source — proceed anyway" override that logs the decision).
  Publish SHA-256 for every release asset.
- **Acceptance criteria:**
  - [ ] `UrlDownload` supports `Sha256`; `config.json` schema documented in
        `docs/CONFIG.md`.
  - [ ] Mismatch aborts by default; override requires explicit checkbox + log line.
  - [ ] Release notes include SHA-256 for `setup.exe` and `config.json`.
  - [ ] CI computes and attaches `SHA256SUMS.txt` to releases.
- **Files:** `src/AppConfig.cs`, `src/Downloader.cs`, `docs/CONFIG.md`,
  `.github/workflows/build.yml`.

---

### T-03 · Commit the CI workflow (currently gitignored)

- **Priority:** 🔴 Critical · **Effort:** S (2–4 hours) · **Owner:** eng
- **Description:** `.gitignore` excludes `.github/workflows/`, so the build
  workflow the docs promise does not exist in-repo and never runs. Remove the
  ignore line and add a real `build.yml` that restores, publishes
  (win-x64 single-file), uploads artifacts, and on tag creates a signed
  release with `setup.exe`, `config.json`, and `SHA256SUMS.txt`.
- **Acceptance criteria:**
  - [ ] `.github/workflows/build.yml` exists on `main`.
  - [ ] Push/PR produces a green build artifact.
  - [ ] Tag push creates a GitHub Release with assets + checksums.
  - [ ] README badge reflects build status.
- **Reference workflow:** see `Developer-Tasks.md` appendix below.

---

### T-04 · Remove machine-specific paths from build scripts

- **Priority:** 🔴 Critical · **Effort:** S (1 hour) · **Owner:** eng
- **Description:** `build.ps1` and `build_repack.ps1` hardcode
  `C:\Users\KingL\.gemini\antigravity-ide\...`. Parameterize via
  `$PSScriptRoot` and `Get-Command dotnet`; fail with a clear message if the SDK
  is missing. Move `build_repack.ps1` (a stub-server test fixture) to `tools/`
  with a "not for end users" header, or delete it in favor of tests (T-15).
- **Acceptance criteria:**
  - [ ] No `C:\Users\` paths in any tracked file (`grep -rn "C:\\Users" .`).
  - [ ] `build.ps1` runs on a clean Windows VM with only the .NET 8 SDK.
  - [ ] README build instructions work verbatim.

---

### T-05 · Repair mode must find installs on all drives

- **Priority:** 🟠 High · **Effort:** S (2–3 hours) · **Owner:** eng
- **Description:** `ServerBuilder.FindExistingInstall` checks only three hardcoded
  C: paths, but the installer auto-picks the drive with most free space (often
  D:/E:). Enumerate fixed drives via `SysProbe` and look for
  `azaroth-installer.json` at `<drive>\Azaroth Core\` and `<drive>\<installFolderName>\`.
- **Acceptance criteria:**
  - [ ] Installs on non-system drives are detected on re-run.
  - [ ] Marker lookup is bounded and fast (no deep recursive scan).
  - [ ] Unit test covers a simulated D: install.

---

### T-06 · Harden MySQL credential handling (`--defaults-extra-file`)

- **Priority:** 🟠 High · **Effort:** M (2–3 days) · **Owner:** eng / security
- **Description:** `SqlUtil` writes passwords into temp `.bat` files and passes
  `--password=` on the command line (visible in process args/temp files/ETW). Use
  `mysql --defaults-extra-file=<temp-my.cnf>` containing `[client]`/`[mysqldump]`
  with `user`, `password`, `host`, `port`; create the file with a random GUID name
  and ACLs for the current user only; delete in `finally`.
- **Acceptance criteria:**
  - [ ] No `--password=` appears in any process command line.
  - [ ] Temp creds file is ACL-restricted and deleted on success/failure/crash.
  - [ ] Imports and queries still work for bundled/local/fresh DB paths.

---

### T-07 · Parameterize / safely quote SQL

- **Priority:** 🟠 High · **Effort:** M (2–3 days) · **Owner:** eng / security
- **Description:** `SqlUtil.Query` interpolates DB names, GM username and realm
  name into SQL strings. Centralize quoting: escape backticks in identifiers
  (`` ` `` → `` `` ` ``) and quotes in string values; use `MySqlConnector`
  prepared statements for `account`/`account_access` inserts; treat all
  config/UI-sourced strings as untrusted.
- **Acceptance criteria:**
  - [ ] No unescaped config value is interpolated into SQL.
  - [ ] A GM username containing `'` or backtick cannot break the query.
  - [ ] Tests cover quoting edge cases.

---

### T-08 · Default to localhost; make LAN play an explicit opt-in

- **Priority:** 🟠 High · **Effort:** S (1 day) · **Owner:** eng / ux
- **Description:** Default `listenAddress` to `127.0.0.1` and do not add firewall
  rules unless the user enables "Allow LAN play". When enabled, bind to the
  detected LAN IP and scope the firewall rule to the local subnet
  (`remoteip=192.168.0.0/16,10.0.0.0/8,172.16.0.0/12`), not `any`.
- **Acceptance criteria:**
  - [ ] Fresh default install is reachable only from localhost.
  - [ ] LAN toggle shows the LAN IP and a clear "this exposes your server" warning.
  - [ ] Firewall rule names/ports are cleaned up on uninstall.

---

### T-09 · Add SECURITY.md and Dependabot + CodeQL + SBOM

- **Priority:** 🟠 High · **Effort:** S (1 day) · **Owner:** eng / security
- **Acceptance criteria:**
  - [ ] `SECURITY.md` exists (template provided).
  - [ ] `.github/dependabot.yml` tracks NuGet + GitHub Actions (weekly).
  - [ ] CodeQL workflow enabled for C#.
  - [ ] CI generates an SBOM (CycloneDX/SPDX) and attaches it to releases.

---

### T-10 · Accessibility pass on WizardForm (WCAG 2.2)

- **Priority:** 🟠 High · **Effort:** M (3–5 days) · **Owner:** eng / design
- **Description:** Keyboard, screen reader, contrast, and scaling fixes.
- **Acceptance criteria:**
  - [ ] Left-nav items are real `Button`s (or `LinkLabel`s), keyboard operable;
        future steps are `Enabled=false`.
  - [ ] Every interactive control has `AccessibleName` (and `AccessibleRole`
        where non-obvious); regions labeled.
  - [ ] `AcceptButton`/`CancelButton` wired; visible focus ring on all controls.
  - [ ] Password boxes use `UseSystemPasswordChar`; show/hide + confirm field.
  - [ ] Errors shown with `ErrorProvider` + `AccessibleDescription`, not only log.
  - [ ] Step status uses text + icon, never color alone.
  - [ ] `PerMonitorV2` DPI; tested at 100/125/150/200%; high-contrast fallback.
- **Files:** `src/WizardForm.cs` primarily; add `src/Theme.cs`.

---

### T-11 · Screenshots/GIF + README conversion pass

- **Priority:** 🟠 High · **Effort:** S (1 day) · **Owner:** design / content / growth
- **Acceptance criteria:**
  - [ ] 4 screenshots (Welcome, System Check, Full Auto progress, Done) committed
        to `assets/screens/` and embedded in README.
  - [ ] 30–60 s GIF/MP4 showing the install → play path.
  - [ ] Above-the-fold "⬇ Download setup.exe" button links to `/releases/latest`.
  - [ ] Badges: license, release, build, .NET 8.
  - [ ] Repo description shortened (~120 chars); topics added; social preview set.

---

### T-12 · Unify versioning and fix CHANGELOG

- **Priority:** 🟡 Medium · **Effort:** S (2 hours) · **Owner:** eng
- **Acceptance criteria:**
  - [ ] csproj `<Version>` / assembly version / wizard header / CHANGELOG / tag all
        agree; wizard header reads `FileVersionInfo.FileVersion`.
  - [ ] CHANGELOG has `## [Unreleased]`; 1.1.0 content moved there until tagged.
  - [ ] Add `global.json` pinning the .NET SDK.

---

### T-13 · Replace fixed 45 s smoke test with log polling

- **Priority:** 🟡 Medium · **Effort:** S (3 hours) · **Owner:** eng
- **Acceptance criteria:**
  - [ ] Smoke test waits for the worldserver "running/initialized" log line or
        process exit, up to a 5-minute timeout.
  - [ ] No false failures on slow HDDs; no 45-second waste on fast SSDs.
  - [ ] Failure still dumps log tails.

---

### T-14 · Fix GPU VRAM reporting (>4 GB) and async WMI probes

- **Priority:** 🟡 Medium · **Effort:** M (1–2 days) · **Owner:** eng
- **Acceptance criteria:**
  - [ ] VRAM reported correctly for ≥4 GB cards (DXGI or registry fallback; not
        the uint32-wrapping `AdapterRAM`).
  - [ ] WMI probes run async with cancellation and a status line; UI never blocks.

---

### T-15 · Add a unit test project; retire build_repack.ps1 stub

- **Priority:** 🟡 Medium · **Effort:** M (3–5 days) · **Owner:** eng
- **Acceptance criteria:**
  - [ ] `tests/AzarothInstaller.Tests` (xUnit) covers `ZipEx.SafePath`,
        `AppConfig` (JSONC/trailing commas/merge), layout detection on synthetic
        zips, SQL quoting, `SysProbe` parsers.
  - [ ] The stub auth/world server generators from `build_repack.ps1` become test
        fixtures, not shipped scripts.
  - [ ] CI runs `dotnet test`.

---

### T-16 · Uninstaller

- **Priority:** 🟡 Medium · **Effort:** M (2–3 days) · **Owner:** eng / ux
- **Acceptance criteria:**
  - [ ] "Remove Azaroth Core" shortcut / wizard mode stops services and kills
        processes.
  - [ ] Options to keep or delete characters DB / data folder.
  - [ ] Removes firewall rule, desktop shortcuts, marker; restores
        `realmlist.wtf.orig` if present.

---

### T-17 · Resumable downloads + ETA

- **Priority:** 🟡 Medium · **Effort:** M (3 days) · **Owner:** eng / perf
- **Acceptance criteria:**
  - [ ] Resumes from `.part` via HTTP `Range`; verifies completed file hash.
  - [ ] Status shows ETA based on rolling throughput.
  - [ ] Cancellation leaves a resumable partial file.

---

### T-18 · GitHub Pages landing/docs site

- **Priority:** 🟡 Medium · **Effort:** M (3–5 days) · **Owner:** growth / eng / design
- **Acceptance criteria:**
  - [ ] Static site at `dlinacre.github.io/azaroth-installer` with hero, download
        CTA, screenshots, features, docs, FAQ, legal.
  - [ ] Includes `robots.txt`, `sitemap.xml`, Open Graph/Twitter tags,
        `SoftwareApplication` JSON-LD (assets provided).
  - [ ] Homepage link set in repo About.

---

### T-19 · Code signing (Authenticode)

- **Priority:** 🟡 Medium · **Effort:** M (cost + 1–2 days setup) · **Owner:** eng
- **Acceptance criteria:**
  - [ ] `setup.exe` signed in CI with an OV (or EV) cert stored as GitHub secrets.
  - [ ] README shows signature verification steps; SmartScreen reputation
        monitored.
  - [ ] All released DLLs/MSIs (where shipped) signed too.

---

### T-20 · Backup/restore characters database

- **Priority:** 🟢 Low · **Effort:** S (1–2 days) · **Owner:** eng
- **Acceptance criteria:**
  - [ ] One-click "Backup characters" exports `azaroth_characters` to a
        timestamped `.sql` (using `--defaults-extra-file`).
  - [ ] "Restore" imports a chosen backup after confirmation.

---

## Appendix: reference `.github/workflows/build.yml`

```yaml
name: build
on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore
        run: dotnet restore src
      - name: Test
        run: dotnet test tests/AzarothInstaller.Tests
      - name: Publish
        run: |
          dotnet publish src -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            -o dist
          Copy-Item src/config.json dist/config.json -Force
      - name: SHA-256 checksums
        run: Get-FileHash dist/* -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash,(Split-Path $_.Path -Leaf) } | Set-Content dist/SHA256SUMS.txt
      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: azaroth-installer
          path: dist/
      - name: Release (on tag)
        if: startsWith(github.ref, 'refs/tags/v')
        uses: softprops/action-gh-release@v2
        with:
          files: dist/setup.exe,dist/config.json,dist/SHA256SUMS.txt
          generate_release_notes: true
```

## Appendix: reference `.github/dependabot.yml`

```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule: { interval: weekly }
    open-pull-requests-limit: 5
  - package-ecosystem: github-actions
    directory: "/"
    schedule: { interval: weekly }
```

## Appendix: `.github/PULL_REQUEST_TEMPLATE.md`

```markdown
## What & why
<!-- one-line summary + linked issue -->

## Testing
- [ ] Full Auto install (local repack zip)
- [ ] Manual step-by-step run
- [ ] Re-run over existing install (characters survive)
- [ ] No-client scenario

## Screenshots / logs
<!-- if UI-visible, attach a screenshot -->

## Checklist
- [ ] CHANGELOG updated (Unreleased) if user-visible
- [ ] Docs updated if config/behavior changed
- [ ] No secrets/credentials committed
```
