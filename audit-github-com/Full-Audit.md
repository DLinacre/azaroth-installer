# Full Audit — Azaroth Core One-Click Installer

Every section distinguishes **Observations** (evidence: file/line or page) from
**Recommendations**. Scores are out of 100.

---

## 2. Brand Review — **64/100**

### Observations
- **Name conflict.** The product is "**Azaroth** Core" but it installs
  "**Azeroth**Core" — a one-letter difference from the upstream project it depends
  on (`azerothcore/azerothcore-wotlk`). The typo is consistent across the repo
  (`README.md`, `config.json` `"serverName": "Azaroth"`, window title, shortcuts,
  firewall rule), so it is intentional branding rather than a slip — but it creates
  search confusion and looks like a misspelling to newcomers.
- **Visual identity is cohesive.** The `assets/banner.png` (1280×640, 1.09 MB) is a
  polished dark-fantasy illustration: dragon, crossed runic swords, gold ornamental
  border, gold "AZAROTH CORE" wordmark, subtitle "One-Click Installer · AzerothCore
  3.3.5a + PlayerBots" and the tagline "No terminal. No compiler. One setup.exe."
  It matches the in-wizard dark theme (`#12141A` background, gold `#FFC457`
  headings). `assets/icon.ico` is a valid multi-resolution ICO (header `00 00 01 00
  06 00` = 6 images, 32×32, embedded in the csproj as `<ApplicationIcon>`).
- **No logo mark separate from the banner.** There is no standalone SVG/PNG mark for
  favicon, social avatar, or release page. The GitHub avatar is the personal
  `DLinacre` avatar, not product branding.
- **Tone** is friendly, confident, and honest ("go get a coffee", "that's normal
  for community tools", explicit legal warning). Good fit for the private-server
  audience.
- **Trust signals are mixed.** Strong: open source, MIT, credits, legal section,
  detailed troubleshooting. Weak: unsigned exe, no `SECURITY.md`, no verified
  checksum, 0 stars/forks, single contributor, 2 commits in the history.
- **Positioning is clear in the README** ("No terminal · No compiler · No developer
  skills") but the **GitHub repo "tagline"** (repo description) repeats the long
  sentence rather than a punchy value prop, and there are **no topics**.

### Recommendations
- **Decide on the spelling deliberately.** If "Azaroth" is a brand fork, own it and
  add a one-line clarification: *"Azaroth Core is a distribution/name for an
  AzerothCore-based installer; not affiliated with the AzerothCore project."* If it
  was accidental, rename to "AzerothCore One-Click Installer" — this is the single
  biggest SEO/trust decision. (See Competitive Positioning.)
- Add a **standalone brand mark** (512×512 PNG + SVG) and set it as the GitHub repo
  social preview / avatar.
- Shorten the **repo description** to ~120 chars and add 8–12 **topics**
  (`azerothcore`, `world-of-warcraft`, `wotlk`, `335a`, `playerbots`, `installer`,
  `winforms`, `dotnet`, `private-server`, `wow`).
- Add a **"Why trust this?"** block to the README: open source + MIT, reproducible
  build, SHA-256 per release, how to verify, no telemetry.
- Define a mini **brand sheet**: primary `#FFC457` gold, dark `#12141A`, accent
  green `#238746`, danger red `#AA4646`, Segoe UI. It's already implicit — codify it.

---

## 3. User Experience — **70/100**

### Observations
- **The core user flow is excellent.** Download → UAC → "Full Auto Install" → coffee
  → Start/Play shortcuts. The 9-step map (System Check → Location → Core → Data →
  DB → Client → World/Options → Verify → Done) mirrors a user's mental model and
  each step has a clear, single purpose (`WizardForm.cs:_stepTitles`).
- **Progressive disclosure works:** Full-Auto hides complexity; manual mode exposes
  every knob; advanced users get `config.json`.
- **Good recoverability:** repair mode via `azaroth-installer.json` marker
  (`ServerBuilder.FindExistingInstall`), characters survive re-runs, live log panel
  with timestamps, log-tail dump on smoke-test failure (`DumpTailLogs`).
- **Friction points:**
  - The very first blocker after install is **SmartScreen** ("Windows protected your
    PC"). README documents "More info → Run anyway," but the wizard can't smooth
    this. It's the single biggest drop-off point.
  - The default `config.json` ships `downloads.serverRepack.urls: []`, so even Full
    Auto **stops at step 3** asking for a local zip unless the user edited config.
    The README says a default can be configured, but out of the box the "one-click"
    promise isn't actually one click.
  - GM credentials are shown in plaintext (`gm/gm1234`) in the README and UI — easy
    for the user, but a bad habit and a real risk (see Security).
  - No **progress time estimate** ("~3 minutes remaining") — only MB/sec.
  - No **uninstaller** or clean "remove" path documented; Stop_Azaroth kills
    processes but leaves MySQL data, firewall rule, shortcuts, realmlist backup.
  - The wizard window is a **fixed 1080×740** (`MinimumSize 960×680`) — on a 1366×768
    laptop the footer log (126px) and header (46px) leave ~580px; on smaller/125%
    DPI displays content can clip.
- **Navigation** is left-rail step labels that are **only clickable to go *back***
  (`if (idx < _step) ShowStep(idx)`); they don't communicate "you can't jump ahead"
  very clearly and aren't keyboard-focusable (they're `Label`s).

### Recommendations
- Ship a **working default repack URL or a prominent "I don't have a zip" path** so
  Full Auto genuinely completes unattended, or rephrase the CTA to "Start (you'll
  pick a server zip)" to set expectations.
- Add an **estimated-time remaining** and a **sticky "what's happening now"** line
  (the `_status` label exists — feed it ETA from average throughput).
- Add an **Uninstall/Remove** action (Stop services, optionally remove DB/data,
  delete firewall rule, shortcuts, restore `realmlist.wtf.orig`) and document it.
- Make the left nav real **Buttons** with `Enabled` state (see Accessibility); add
  a visible "completed / current / locked" affordance beyond color alone.
- Add a **post-install "First 5 minutes" card** (how to log in, the `lfg` command,
  where logs are) — most of this exists in README but not in the Done screen.
- Remember window size / make it **resizable with sane min-size**, and test at 125%
  and 150% DPI.

---

## 4. User Interface — **66/100**

### Observations
- **Coherent dark theme**, consistent with the banner: background
  `Color.FromArgb(18,20,26)` (`#12141A`), gold headings `Color.FromArgb(255,196,87)`
  (`#FFC457`), green primary button `Color.FromArgb(35,135,70)`, blue Next, grey
  Back, red Cancel (`WizardForm.cs:89-91, 117-118, 209-215`).
- **Layout uses `TableLayoutPanel` + `Dock`/`Anchor`**, which is the correct
  WinForms approach and is mostly responsive within the fixed window.
- **Weaknesses:**
  - Spacing is hand-rolled per step (lots of inline `new Padding(...)`, `Location`,
    `Size`); no shared design tokens / helper for card spacing, so step screens
    will drift visually.
  - The **footer is 126 px** with a 56 px button row + 22 px status + log; commit
    `27f3a39` explicitly "reduced footer (+42px space)" which suggests spacing was
    being tuned reactively rather than from a system.
  - **Emoji as the only iconography** (⚔ ⚡ ▶ ✖ ✓ 🎮). They render differently
    across Windows versions and fail for color-blind / no-color users when used as
    the sole status cue (✓ green vs grey).
  - **Log panel** is `Consolas 8.5pt` in "terminal green" `Color.FromArgb(140,210,140)`
    on `#0C0E12` — on-theme but low contrast and small.
  - No **focus visuals** are customized; default WinForms focus rectangle on
    custom-colored Buttons can be invisible.
  - Buttons use `FlatStyle`? Need confirmation; with custom `BackColor` they should
    set `FlatStyle.Flat` and explicit border colors or they look inconsistent.
  - The **ListView module picker** and drive/WoW combos have no alternating row
    styling or empty-state illustration.

### Recommendations
- Introduce a small **`Theme.cs`** static class with constants (`Bg`, `Surface`,
  `Border`, `Text`, `Muted`, `Gold`, `Primary`, `Danger`, `Radius`, `Spacing*`) and
  helper methods `Card()`, `PrimaryButton()`, `MutedLabel()` so every step uses the
  same spacing/radius/typography.
- Replace emoji-only status with **icon + text + color** (e.g. a green check SVG
  *and* "Verified"); never color alone.
- Bump log font to **9 pt**, raise contrast to ≥ 4.5:1 (e.g. `#9FE6B3` on `#0C0E12`
  is ~7:1), and offer a **"Copy log" / "Open log folder"** button.
- Standardize **card** components (title + body + optional footer) used by every
  step; use a 12/16/24 spacing scale.
- Add a visible **focus ring** (custom `OnPaint` or `FlatAppearance` border) for
  keyboard users.
- Add **empty states** with an icon + one-line help for: no repack zip, no WoW
  client found, no modules, no drives with enough space.
- Provide a 16/24/32 px **icon set** (Segoe Fluent Icons or a custom mini-set)
  instead of relying on emoji.

---

## 5. Content / Copy — **72/100**

### Observations
- **README is genuinely strong.** Tight value prop, 3-step quick start, a 9-step
  "how the wizard works" table, requirements table, PlayerBots cheat sheet,
  build-from-source, repo structure, credits, legal, license. It reads like it was
  written *for* the user, not at them.
- **`docs/CONFIG.md`** is an excellent full reference with a JSONC example and a
  per-key table. Honest about Mega/GDrive links not working.
- **`docs/TROUBLESHOOTING.md`** is well structured with a symptom→fix table.
- **Grammar/tone** is consistent and largely error-free.
- **Gaps / issues:**
  - README has **relative links** (`docs/CONFIG.md`, `LICENSE`, `#legal`) which work
    on GitHub and in raw renderers but break if the README is viewed on
    third-party sites or npm/PyPI-style mirrors. Not critical, but absolute URLs are
    more robust.
  - The **first-run blocker** (no default repack URL → user must find a zip) is
    buried; a first-time visitor can read the whole README and still not know where
    to get a "prebuilt Windows AzerothCore zip" beyond vague "OwnedCore, DrePack".
    This is a **content + legal** tight spot (can't link to copyrighted repacks),
    but it should be addressed with a clearer "you need to provide X" callout.
  - **Default credentials `gm/gm1234` are printed in the README** as if permanent.
    Should be "you'll set these in the wizard" / "change immediately".
  - `CONTRIBUTING.md` references a **PR template** that doesn't exist in the repo
    (only bug/feature issue templates are present), and says to add an "Unreleased"
    CHANGELOG section, but the CHANGELOG has no Unreleased section.
  - **CHANGELOG 1.1.0 is dated 2026-08-23 (today) but the latest release/tag is
    v1.0.0 and csproj `<Version>` is 1.0.0** — unreleased work is logged under a
    dated released-looking heading.
  - No **README badges** (build status, license, latest release, downloads,
    Discord/discussions) — these are cheap trust signals.
  - No **FAQ** beyond troubleshooting; common questions ("Is this legal?", "Will I
    get banned?", "Can I play with friends?", "Do I need the client?") deserve a
    short FAQ.

### Recommendations
- Add a prominent **"You will need"** callout near the top: a Windows 10/11 PC +
  your own 3.3.5a client + *a prebuilt Windows AzerothCore zip (the wizard can
  download one if you put a direct link in config.json)*.
- Replace the printed default GM password with **"the wizard generates a password
  for you / asks you to set one"** once credential fix ships.
- Add **badges**: License (MIT), Latest Release, Build (once CI works), Downloads,
  and a .NET 8 badge.
- Add an **FAQ section** (legal/banning/LAN/backups/uninstall).
- Add a **PR template** to match CONTRIBUTING, and an `## [Unreleased]` section to
  CHANGELOG; move 1.1.0 content there until tagged.
- Convert key doc links to **absolute URLs** for portability.
- Suggested repo **description** rewrite (120 chars):
  > "One-click Windows installer for a private AzerothCore 3.3.5a (WotLK) server
  > with PlayerBots. No terminal, no compiler — just setup.exe."

---

## 6. SEO Audit — **34/100**

> Scope: GitHub-hosted repository. There is no first-party website, so on-page SEO
> applies to the repo page, README, Releases, and (recommended) future GitHub Pages.

### Observations
- **Page title** (browser tab) is GitHub's auto-generated
  "GitHub - DLinacre/azaroth-installer: ⚔️ Azaroth Core — One-Click Installer for
  AzerothCore 3.3.5a..." — good, keyword-rich, but led by the username.
- **Meta description** is the repo description — also good, but long and not
  optimized for click-through.
- **Repository topics: EMPTY.** This is the single biggest SEO lever on GitHub
  search and is completely unused.
- **No GitHub social preview image** set under repo Settings → General → Social
  preview. Shares of the URL on Discord/Reddit/forums show a generic card.
- **No homepage/website link** in the repo "About" panel.
- **README heading hierarchy** is sound (one H1, H2 per section, tables), but:
  - The H1 is *below* the banner image; the very first content is an `<img>` with
    `alt="Azaroth Core - One-Click Installer"` (good alt text).
  - Banner is 1280×640 but served at full 1.09 MB — not optimized for the web.
- **Canonical / robots / sitemap** are controlled by GitHub (correct: `canonical`
  points to the repo, robots allow indexing). No custom site exists.
- **Keyword mismatch:** the product is "Azaroth" but users search
  "AzerothCore", "WoW 3.3.5a private server", "playerbots installer", "wotlk repack
  one click". The README mentions these, but the **repo name and brand don't**,
  which dampens ranking for the highest-volume terms.
- **0 stars, 0 forks, 1 watcher-narrative, no Discussions** — GitHub's internal
  search ranking weights engagement; a brand-new project is invisible.
- **No external backlinks** observed (no wiki, no Reddit thread, no Discord listed),
  though that's expected on launch day.
- Image SEO: `banner.png` has a good `alt`; `icon.ico` is binary; no other images.

### Recommendations (prioritized)
1. **Add 8–15 topics now:** `azerothcore`, `world-of-warcraft`, `wow`, `wotlk`,
   `wrath-of-the-lich-king`, `335a`, `playerbots`, `private-server`, `installer`,
   `one-click`, `winforms`, `dotnet`, `csharp`, `windows`, `gaming`.
2. **Set a social preview image** (1280×640, ≤1 MB) — reuse the banner but optimize.
3. **Add a homepage link** — either a future GitHub Pages site or the Releases page.
4. **Resolve the Azaroth/Azeroth naming** (see Brand). At minimum, ensure the repo
   description and README first line contain "AzerothCore 3.3.5a" verbatim.
5. **Optimize the banner** (see Performance): 1.09 MB → ~250 KB WebP/AVIF with PNG
   fallback.
6. Add a **GitHub Pages landing site** (`/docs` or a `docs/` Jekyll site) with a
   custom domain or `https://dlinacre.github.io/azaroth-installer/` — this gives a
   real indexable page, meta tags, Open Graph, JSON-LD, and a download CTA. (See
   `Metadata/`, `Schema/`, `Robots/` for ready assets.)
7. Add **README badges** and a **Discord/Discussions** link to drive engagement signals.
8. Publish a short **"How to set up a private WoW 3.3.5a server with PlayerBots in
   2026"** guide (GitHub Pages or a gist) targeting that long-tail query; link back
   to the repo.
9. Ensure **every release has release notes** (v1.0.0 currently has one sentence —
   expand with checksums, install steps, known issues).

---

## 7. Performance — **63/100**

> For a GitHub-hosted repo, page-load performance is mostly GitHub's. For the
> product, "performance" means installer footprint, startup, download efficiency,
> and asset weight.

### Observations
- **`setup.exe` is ~139 MiB** (145,940,105 bytes) — expected for a self-contained
  .NET 8 WinForms single-file publish, but large. Trimming / compression could help.
- **Banner assets are heavy for web use:** `banner.png` 1.09 MB, `banner-art.png`
  2.09 MB, `icon-art.png` 1.34 MB. Only `banner.png` is used in the README.
- **Downloader is reasonably well built:** streaming (`HttpCompletionOption.
  ResponseHeadersRead`), 256 KB buffer, progress throttled to 250 ms, 3 retries per
  URL, automatic decompression, UA header to bypass WAFs. Good.
  - **No HTTP/2 or parallel chunk download** — large files (1.5–2 GB data, ~140 MB
    DB MSI) download single-connection; resumable/range downloads aren't supported,
    so a dropped connection restarts from zero.
  - **No checksum/hash verification** after download (corruption or tampering
    undetected) — see Security.
- **Zip extraction is streaming and zip-slip safe** (`ZipEx`), 256 KB buffer,
  progress every 16 MB. Good. But it reads the whole zip twice (once to sum `total`,
  once to extract) and `OrderBy(e.FullName)` buffers the full entry list — minor for
  ~30k entries.
- **Smoke test uses fixed `Task.Delay(45000)`** for first boot — on a slow disk this
  may be too short (false failure); on a fast machine it wastes 45 s. Should poll
  the log for "world server is running" instead.
- **SysProbe uses WMI (`ManagementObjectSearcher`)** which can be slow (seconds) on
  some systems; runs synchronously. GPU `AdapterRAM` is a uint32 and wraps for >4 GB
  cards (classic WMI bug) — VRAM > 4 GB will report wrong.
- **No caching of downloaded files** across runs beyond "reuse if present"; partial
  `.part` files are deleted and restarted.
- csproj sets `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>` (good,
  trims satellite assemblies) but does **not** enable `<PublishTrimmed>` or
  `<EnableCompressionInSingleFile>` (would shrink the exe).

### Recommendations
- Enable **single-file compression** (`<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>`)
  and evaluate **trimming** carefully (WinForms + reflection often breaks under full
  trimming — test thoroughly; use `PublishTrimmed` with trim warnings resolved or
  leave it off).
- Add **resumable downloads** using HTTP `Range` requests (resume from `.part`),
  and optionally **parallel chunked download** for the large data zip.
- Add **SHA-256 verification** of every downloaded artifact (configurable expected
  hash; warn but allow override for community repacks).
- Replace fixed 45 s smoke wait with **log-polling** for the "World initialized" /
  "world server is running" line, with a generous timeout (e.g. 5 min).
- Fix **GPU VRAM reporting** to use `Win32_VideoController.AdapterRAM` only as a
  fallback; read `AdapterRAM` (uint32) with awareness of the 4 GB wrap, and prefer
  the registry `HardwareInformation.MemorySize` or DXGI for accurate VRAM.
- Optimize images: `banner.png` → export as **WebP/AVIF** (~150–300 KB) with PNG
  fallback; the `*-art.png` source files shouldn't be in the shallow clone at all
  (move to a separate `assets-source` branch or a release attachment) — they bloat
  every clone by ~3.4 MB.
- Make WMI probes **async with cancellation** and a timeout; show a subtle "Probing
  hardware…" status.

---

## 8. Accessibility — **41/100**

> WCAG 2.2 applied to both the README/web content and the WinForms product.

### Observations (Web/README)
- README has good **alt text on the banner** (`alt="Azaroth Core - One-Click
  Installer"`). GitHub's own UI provides keyboard nav, landmarks, focus styles, and
  dark/light themes — the project gets those for free.
- Tables use proper Markdown table syntax (header row) — screen readers handle them.
- Color is used in the wizard but not in the README (good).

### Observations (WinForms product — the real gaps)
- **No `AccessibleName` or `AccessibleRole` set anywhere** in `WizardForm.cs` (grep
  for `Accessible` returns nothing). Screen readers read control types and raw
  text, but unlabeled ListViews, icon-only-ish buttons and the nav get poor names.
- **Left navigation is `<Label>` controls with a `Click` handler**
  (`WizardForm.cs:150-168`). Labels are **not keyboard-focusable** and don't raise
  click on Enter/Space — keyboard-only users can't use the nav at all. They should
  be `Button`s (or `LinkLabel`s with proper roles).
- **Color-only status.** Completed steps turn green (`Color.FromArgb(130,215,130)`),
  current step gold, future grey — but the only text change is "✓" prefix vs
  number. Color-blind users can't distinguish; the ✓ helps partially but it's an
  emoji with inconsistent rendering.
- **Contrast risks:**
  - Log text `#8CD28C`-ish on `#0C0E12` — roughly 6:1 (passes AA for normal text at
    ~8.5 pt, but small + monospace is hard).
  - "Silver"/`Gainsboro` on `#12141A` passes.
  - Some muted labels use `Color.Silver`/`Color.FromArgb(160,165,180)` on
    `#181B24` — measure; the version label `Color.Silver` on dark is ~7:1 (OK).
  - Buttons: white text on `#238746` green / `#2369BE` blue / `#AA4646` red all
    pass; grey Back button white-on-`#3C404C` is ~4.6:1 (borderline).
- **Password inputs:** GM password box — confirm `UseSystemPasswordChar = true` is
  set (not visible in the read excerpt; needs verification). No "show password"
  toggle and no second "confirm password" field.
- **Keyboard:** Tab order isn't explicitly set (relies on z-order added to parent);
  no `AcceptButton`/`CancelButton` wired to Enter/Esc; the Cancel button has a
  handler but it's not set as the form's `CancelButton`.
- **Scaling/DPI:** `ApplicationHighDpiMode = SystemAware` (set in csproj) is OK but
  `PerMonitorV2` is better for 4K/mixed-DPI. Font sizes are in points (good) but
  many controls use fixed `Size`/`Location` (will clip at 150%).
- **Motion:** marquee progress bar is fine; no autoplaying media.
- **Error recovery:** errors go to the log (a TextBox), but there are no
  `ErrorProvider` icons or field-level inline error messages associated with the
  relevant control — a screen-reader user won't know which field failed.
- **No high-contrast / system-theme support** — the app forces a dark theme with
  hardcoded colors, ignoring Windows High Contrast mode (WCAG 1.4.11 / 1.4.3
  concerns; system color support is expected of accessible apps).
- **Emoji in accessible names/titles** ("⚡ Full Auto Install", "✖ Cancel",
  "🎮 Play") — screen readers announce "zap"/"cross"/"video game" before the label;
  either hide them from accessibility or use real icons.

### Recommendations
- **Replace nav Labels with Buttons** (`FlatStyle.Flat`, no border until focus), set
  `Enabled = false` for future steps. This single change fixes keyboard + screen
  reader nav.
- Set **`AccessibleName`** on every interactive control and major region
  (`_content.AccessibleName = "Step content"`; ListViews = "Detected World of
  Warcraft clients", etc.).
- Add **`AcceptButton = _autoBtn / _nextBtn`** and **`CancelButton = _cancelBtn`**.
- Ensure **`UseSystemPasswordChar = true`** on password boxes; add a Show/Hide
  toggle and a Confirm field for the GM password.
- Add **field-level errors** via `ErrorProvider` and `AccessibleDescription`; never
  put errors only in the log.
- Move to **`PerMonitorV2`** high-DPI mode and test at 100/125/150/200%.
- Support **system high-contrast** (detect `SystemInformation.HighContrast` and
  fall back to system colors) or at minimum ensure all UI text is ≥ 4.5:1 and
  interactive elements have a 2+ px focus indicator.
- Prefix status with a **text word**, not just color/emoji:
  "✓ Completed" / "● Current" / "○ Upcoming".
- Provide an **"Open log" / "Copy log to clipboard"** button and make the log
  `AccessibleName = "Installation log, read-only"`.
- See `Accessibility/accessibility-checklist.md` for a full WCAG 2.2 checklist.

---

## 9. Security & Privacy — **31/100** 🔴

> The product runs **elevated**, downloads executables/zips/MSIs from the internet,
> installs a database, opens firewall ports, and writes credentials. Security is the
> most important category for this project. Findings are static observations only.

### Observations

**🔴 A. Hardcoded default credentials (critical)**
- `src/config.json` and `AppConfig.cs` ship:
  - GM account `gm` / `gm1234`
  - DB user `azaroth` / `Azar0th!DB`
  - `root` password empty (`"rootPassword": ""`)
- The server binds `0.0.0.0` (`server.listenAddress`) and the installer adds a
  firewall rule for TCP 3724 + 8085 (`Misc.AddFirewallRules`). Any host where the
  user followed defaults is a GM-level-compromisable WoW server if exposed beyond
  localhost. The MySQL root blank password is a classic worm vector.
- The marker file `azaroth-installer.json` stores `dbPassword` and `gmPassword` in
  plaintext (`ServerBuilder.WriteMarker`).
- GM password is stored in the DB using **single-round SHA-1**
  (`SqlUtil.WoWPassSha1`) — this is the legacy WoW 3.3.5a auth scheme, required by
  the old client, so it's a protocol constraint; note it as weak-but-necessary and
  ensure the DB file is protected.

**🔴 B. No integrity verification of downloads (critical)**
- `Downloader.DownloadOneAsync` writes bytes straight to disk with no SHA-256 /
  signature / Authenticode check. A MITM, CDN compromise, or maliciously configured
  `config.json` URL yields code execution as administrator.
- Downloads include **MSI installers, EXEs, zips containing DLLs/EXEs** — all
  executed/extracted without verification.
- TLS itself is fine (HTTPS URLs), but TLS ≠ provenance.

**🟠 C. Unsigned executable (high)**
- `setup.exe` is Authenticode-unsigned, so SmartScreen warns on every run; users
  are trained to click "Run anyway," which is exactly the habit malware relies on.
  README acknowledges this. No `SECURITY.md`, no published checksum.

**🟠 D. SQL injection surface (high)**
- `SqlUtil.Query` builds SQL by string interpolation/concatenation:
  ```csharp
  "SELECT SCHEMA_NAME ... WHERE SCHEMA_NAME='" + name + "'"
  $"CREATE DATABASE IF NOT EXISTS `{name}` ..."
  $"CREATE USER ... IDENTIFIED BY '{esc}';"
  ```
  Database names and GM usernames come from config/UI. `EnsureUserAndGrants`
  escapes quotes/backslashes in the password, but `name`/`cfg.Login`/`cfg.AuthDb`
  are interpolated into backtick identifiers without backtick-escaping, and the GM
  username in `INSERT INTO account ... VALUES ('{user...}')` is uppercased but not
  quoted-escaped (`ServerBuilder.cs:1219`). A crafted realm name or GM username with
  a backtick/quote could inject SQL.
- SQL files from a repack are piped to `mysql.exe` wholesale — that's by design
  (repacks ship SQL dumps), but it underscores why **B** matters.

**🟠 E. Credentials in command-line arguments & temp batch files (high)**
- `SqlUtil.ImportSqlFile`/`Query` write a `.bat` file to `%TEMP%` containing
  `--password=<password>` in cleartext, then run it. The password is visible in:
  - The temp `.bat` file (briefly, though deleted after; on crash it can persist).
  - The process command line (`Get-Process`/Task Manager/WMI) while mysql runs.
  - Possibly in process-audit/ETW logs.
- The correct approach is `--defaults-extra-file=<temp-my.cnf>` with `[client]`
  `password=...` and restrictive ACLs, or `MYSQL_PWD` is deprecated — use
  `--defaults-extra-file` and delete it.

**🟠 F. Privilege & attack-surface posture (high)**
- The whole process runs **full admin** from start to finish (`Program.cs` re-launches
  elevated and never drops privileges). Downloading/extracting as admin is
  unnecessary privilege — only the DB install/firewall/shortcuts steps need it.
- `netsh advfirewall firewall add rule` opens ports broadly (`localport=3724,8085`
  with no remote-IP restriction) even though the default is a *local/personal*
  server. Default should bind to `127.0.0.1` and only open the firewall when the
  user explicitly enables LAN play.
- `Process.Start` of `worldserver.exe`/`authserver.exe` from a world-writable
  install location (if installed outside Program Files) is a DLL/EXE search-order
  risk; consider installing under `%ProgramFiles%` (which is why admin is needed)
  rather than the drive with most free space.

**🟡 G. Misc hardening (medium/low)**
- `build.ps1`/`build_repack.ps1` **hardcode `C:\Users\KingL\...`** — information
  disclosure (local username, tooling path) and broken builds for anyone else.
- `HttpClient` UA spoofs Chrome — fine for WAF bypass, but set a contactable UA too.
- No **telemetry/analytics** is present (good for privacy), but there's also no
  privacy statement because there's no data collection — say so explicitly.
- No **rate limiting / lockout** concerns for a local server; not applicable.
- `ZipEx.SafePath` correctly prevents zip-slip (good — this is a real strength).
- `Microsoft.Win32.Registry` 5.0.0 and other packages should be tracked with
  **Dependabot** for CVEs; no `dependabot.yml` exists.
- No **SBOM** (CycloneDX/SPDX) published for the 139 MB binary.

### Recommendations (defensive)
1. **Generate random defaults on first run** for GM password and DB password; force
   the user to set/confirm a GM password in the wizard; never commit a working
   credential. Blank out `rootPassword` and *never* try `""`, `"test"`, `"root"` as
   root passwords against arbitrary/existing MySQL servers (that logic in
   `ServerBuilder` lines 408–421 should only apply to the bundled fresh install).
2. **Add SHA-256 verification** for every download; support pinned hashes in
   `config.json` (`"sha256": "..."`) and refuse mismatches by default (with an
   "I trust this source" override + prominent warning). Publish SHA-256 for every
   release asset.
3. **Code-sign `setup.exe`** with an Authenticode cert (OV minimum; EV for instant
   SmartScreen reputation). Add a `SECURITY.md` with a disclosure policy and
   supported versions.
4. **Parameterize / safely quote** all SQL: escape backticks in identifiers
   (`` ` `` → `` `` ` ``), use `MySqlConnector` with prepared statements for the
   account/grants queries, or at minimum centralize quoting. Treat realm/GM names
   as untrusted input.
5. **Use `--defaults-extra-file`** for MySQL credentials; create the temp file with
   a random GUID name, set ACLs to the current user only, and delete it in a
   `finally`. Never put `--password=` on a command line.
6. **Default to localhost-only**: set `listenAddress=127.0.0.1` and do **not** add
   a firewall rule unless the user explicitly opts into LAN play (then bind to the
   LAN IP and scope the firewall rule to the local subnet).
7. **Split elevation**: run the UI as a normal user; spawn an elevated helper only
   for the steps that need it (DB install/firewall/Program Files writes). This
   limits the damage of a compromised download.
8. Install under `%ProgramFiles%` by default (requires admin, gets ACL protection),
   with a per-machine data dir under `%ProgramData%` for DB/data.
9. Add **Dependabot** (`dependabot.yml` for NuGet + GitHub Actions), publish an
   **SBOM** with each release, and enable **GitHub Advanced Security / code
   scanning** (CodeQL) on the repo.
10. Remove the **hardcoded local paths** from build scripts; parameterize with
    `$PSScriptRoot` and `Get-Command dotnet`.
11. Add a **privacy note**: "No telemetry, no phoning home; the installer only
    contacts the URLs in config.json."

See `Security/security-fixes.md` and `Security/SECURITY.md` template.

---

## 10. Technical / Bugs — **46/100**

### Observations
- 🔴 **`.github/workflows/` is in `.gitignore`** (`.gitignore` ends with
  `.github/workflows/`). The README, CHANGELOG and CONTRIBUTING all claim a CI
  build workflow exists ("The CI workflow (`.github/workflows/build.yml`) does
  exactly this on every push and uploads the installer as an artifact"). It is
  **not in the repository and never runs.** This is the most consequential bug:
  no automated build/test, no release artifacts from CI.
- 🔴 **`build.ps1` and `build_repack.ps1` are machine-specific.** Both hardcode
  `C:\Users\KingL\.gemini\antigravity-ide\scratch\dotnet-sdk\dotnet.exe` and
  `build_repack.ps1` hardcodes `c:\Users\KingL\.gemini\antigravity-ide\scratch\
  azaroth-installer`. They will not run on any other machine. `build_repack.ps1`
  also doesn't use `$PSScriptRoot`.
- 🟠 **`build_repack.ps1` is a developer test-fixture generator shipped to users.**
  It compiles two stub C# programs (`AuthServer` = a TCP listener that does
  nothing; `WorldServer` = writes a log line and sleeps forever), packages them as
  a fake "AzarothCore-Server-Repack.zip" with toy SQL (an `account` table with
  `gmsec`/`gmlevel` columns that don't match real AzerothCore schema). This is
  useful for testing the installer but **(a)** is confusing to ship, **(b)** its
  fake SQL could be mistaken for the real schema, and **(c)** it embeds the
  `Azar0th!DB` password again.
- 🟠 **Version mismatch across the project:**
  - `src/AzarothInstaller.csproj`: `<Version>1.0.0</Version>`
  - `WizardForm.cs` header text: `"v1.0"`
  - `CHANGELOG.md`: top entry `## [1.1.0] - 2026-08-23` (today, unreleased)
  - Release tag: `v1.0.0`
  - The new commit `27f3a39` ("UI overhaul, MySQL 403 fix, SQL temp leak") is
    effectively 1.1.0 work but shipped onto the v1.0.0 tag's lineage without a
    new tag/release.
- 🟠 **No tests.** 4,400 LOC, zero test projects. Layout detection, zip
  extraction, config merge, and SQL building are all pure-ish logic that would
  benefit from unit tests. The `build_repack.ps1` stub exists because there's no
  test harness.
- 🟡 **WMI GPU VRAM bug** (`SysProbe.cs:76`, `AdapterRAM` is `uint32`; wraps at 4
  GB) — VRAM reporting is wrong on modern cards.
- 🟡 **`FindExistingInstall` only checks three hardcoded paths**
  (`C:\Program Files\Azaroth Core`, `C:\Azaroth Core`, ProgramFiles) — but the
  installer's own logic auto-picks "the drive with the most free space," which is
  often `D:\`, `E:\`, etc. Repair mode will **not find installs on non-C drives**.
- 🟡 **Smoke test fixed 45 s delay** can false-fail on slow HDDs (see Performance).
- 🟡 **`KillProcess` swallows all exceptions** and also calls `taskkill /f /im
  worldserver.exe` — fine, but `p.Kill(entireProcessTree: true)` on the bundled
  MySQL could kill unrelated mysqld if reused; verify targeting.
- 🟡 **`logBox` truncated to 120,000 chars by `Clear()`** — a mid-failure clear
  wipes the diagnostic context the user needs; should truncate from the *top*.
- 🟡 **Icon detection:** `file(1)` reported `icon.ico` as "Targa image data" (a
  libmagic mis-identification); the actual header bytes (`00 00 01 00 06 00...`)
  confirm a valid ICO with 6 images — not a bug, but worth verifying the icon
  renders at 16/24/32/48/64/256.
- 🟡 **`HttpClient` is a static singleton with `Timeout.InfiniteTimeSpan`** —
  a hung connection holds forever; cancellation comes only from the UI token,
  which is correct, but set a sane per-request timeout fallback.
- 🟡 **No `global.json`** pinning the .NET SDK version; builds can drift.
- 🟢 **Strengths:** zip-slip protection, temp-file cleanup in `SqlUtil.finally`,
  idempotent DB creation (`IF NOT EXISTS`), nested-zip handling, registry +
  filesystem WoW scan with modern-client detection.

### Recommendations
- **Un-ignore and commit `.github/workflows/build.yml`** (remove that line from
  `.gitignore`); make CI build, run tests, and upload `setup.exe` + `config.json`
  + SHA-256 as a build artifact. (Ready workflow in `Developer-Tasks.md` / assets.)
- **Rewrite build scripts** to use `$PSScriptRoot`, `Get-Command dotnet`, and no
  user-specific paths; move `build_repack.ps1` to a `tools/` or `dev/` folder and
  mark it clearly as a test fixture (or delete it in favor of proper tests).
- **Add a test project** (`AzarothInstaller.Tests`/xUnit) covering: `ZipEx.
  SafePath`, `AppConfig` load/merge/JSONC, layout detection on synthetic zips,
  SQL identifier quoting, `SysProbe` parsing. Use the stub-repack logic as test
  data rather than a shipped script.
- **Unify versioning**: use
  [`<InformationalVersion>`](https://learn.microsoft.com/dotnet/core/tools/) /
  MinVer/Nerdbank.GitVersioning so assembly, UI header, and changelog agree.
- **Fix `FindExistingInstall`** to scan all fixed drives for the
  `azaroth-installer.json` marker (reuse `SysProbe` drive enumeration).
- Fix GPU VRAM (DXGI/CIM), log truncation direction, smoke-test polling, and add
  a per-request HTTP timeout.
- Add `global.json` pinning the SDK.

---

## 11. Conversion (CRO) — **58/100**

> Conversion here = repo visitor → downloader → successful installer → (community
> member). There's no checkout/signup funnel.

### Observations
- **Primary CTA** is "Download `setup.exe` from Releases" — but the README's first
  CTA cluster is a *nav row* (Configuration · Troubleshooting · Contributing ·
  Changelog · License), not a download button. The actual download link appears in
  step 1 of the "What is this?" numbered list. The **Releases page itself** is
  sparse (one sentence, 4 assets but the GitHub UI "loading/error" state on the
  assets widget — observed during audit).
- **No above-the-fold "Download for Windows" button.** The banner is an image with
  text baked in; it isn't a clickable CTA.
- **Trust barriers:**
  - SmartScreen warning (documented, still scary).
  - No checksum, no signature, 0 stars, single author.
  - The legal warning ("can violate Blizzard's ToU") is necessary but is a
    conversion risk; it's well-placed (a blockquote, not a wall) and honest.
- **Friction to actually play:** after downloading, the user must also provide a
  prebuilt AzerothCore zip, which the README doesn't source. That's a major
  drop-off between "I downloaded setup.exe" and "I'm playing."
- **No social proof:** no testimonials, no Discord, no "X users installed this,"
  no screenshots/GIF of the wizard. A 9-step installer with no visuals asks users
  to trust it blind.
- **No Discussions / issue-to-community path.** Bug template exists, but no
  "Questions? Join us" channel.

### Recommendations
- Add a **big, above-the-fold download button** (Markdown `[![Download
  setup.exe](...)](releases/latest)`) right under the tagline. Use a shields.io or
  a custom button image; link to `/releases/latest`.
- Add **2–3 screenshots or a short GIF** of the wizard (Welcome, System Check,
  Full Auto progress, Done screen) in the README. This is the highest-ROI content
  addition for conversion.
- Add a **"Your first 5 minutes"** mini-section: download → run → Full Auto →
  Start → Play → `lfg`.
- Add **checksum + how-to-verify** next to every release; once signed, show "✓
  Signed by DLinacre."
- Add **Discord or GitHub Discussions** and a badge; even a small community lowers
  abandonment.
- Re-state the **3-step path** as a visual numbered flow near the top (icon + one
  line each).
- Add a **"What you need"** checklist badge row: Windows 10/11 · 4 GB RAM · 30 GB
  · WoW 3.3.5a client · (server zip).
- Consider a **mild email/watch "star to be notified of updates"** nudge (no
  account needed — just star the repo).

---

## 12. AI Opportunities — **40/100**

### Observations
- The installer has a rich **live log** and structured failure modes
  (`DumpTailLogs` maps worldserver.log tails to symptoms in TROUBLESHOOTING). This
  is ideal fodder for an AI assistant, but none exists today.
- No analytics/telemetry (privacy-positive, but also no signal for improvement).
- Configuration is a JSONC file with a well-documented schema — an AI "config
  generator" would be straightforward.
- The PlayerBots in-game command surface (`lfg`, `.playerbots ...`) is confusing
  for new users and is currently a static cheat-sheet table.

### Recommendations (with expected value)
1. **AI log-diagnoser (highest value).** Add a "Diagnose with AI" button on the
   Verify/failure screen that sends the *local* log tail to a local or
   user-provided LLM (e.g. an OpenAI-compatible endpoint the user configures, or a
   fully local model) with a prompt built from TROUBLESHOOTING.md, returning likely
   cause + fix. Keep it **opt-in and privacy-preserving** (never send logs without
   explicit consent; offer offline/local model). Expected value: ~30–50% reduction
   in support/issue load; faster time-to-first-success.
2. **In-product PlayerBots assistant / command helper.** A small "What do you want
   to do?" panel: "Run a 25-man raid", "Summon a healer", "Level fast" → generates
   the exact `.playerbots`/`lfg` commands. Could be rule-based first (no LLM
   needed), AI-enhanced later. Expected value: better retention/usage of the
   product's headline feature.
3. **AI config generator.** A web form (GitHub Pages) — "I want a 1x realm with
   200 bots and LAN play for 3 friends" → generates a ready `config.json` to drop
   next to `setup.exe`. Expected value: fewer misconfigurations, sharable presets.
4. **Automated triage of GitHub issues.** A GitHub Action that labels new bugs
   with the failing step/error from the pasted log, asks for missing info, and
   suggests a TROUBLESHOOTING doc link. Expected value: maintainer time saved.
5. **Smart repack detection / suggestions (cautious).** Given a zip's layout,
   classify it (AC version, modules, bundled MySQL) and suggest config tweaks.
   Rule-based is safer than AI here; no need for ML.
6. **AI-generated release notes / changelog** from conventional commits — low
   effort, cosmetic value.

Guardrails: all AI features must be **off by default**, disclose data flow, work
without an API key (graceful fallback), and never auto-execute suggested commands
without explicit user confirmation.

---

## 13. Competitive Positioning — **60/100**

### Observations
The relevant comparison set (publicly known, from the audit's web research):
- **AzerothCore official installation** (`azerothcore.org/wiki/linux-core-installation`):
  `git clone`, compile from source on Linux — powerful but developer-only; no
  Windows one-click, no PlayerBots packaging.
- **`coc0nut/AzerothCore-with-Playerbots-Docker-Setup`**: a shell script that
  clones + sets up via Docker on Linux. Requires Docker, CLI comfort.
- **`stoudtlr/AzerothCore-Module-Installer`**: a PowerShell module picker, not a
  full server installer.
- **Community repacks** (OwnedCore, DrePack, ACBS): prebuilt zips, but manual
  setup, inconsistent quality, no verification, no unified wizard.

**Where Azaroth Core is ABOVE average:**
- First genuinely end-user ("no terminal, no compiler") Windows experience.
- Hardware probing, auto-drive selection, repack-agnostic layout detection,
  repair mode, boot smoke test, live log — none of the competitors have this
  combination.
- Documentation quality (CONFIG + TROUBLESHOOTING) is above typical community
  repacks.
- Zip-slip protection and "characters survive re-runs" show maturity.

**Where it is AVERAGE:**
- README/landing quality (good, but standard for a polished OSS project).
- Issue templates, CHANGELOG, CONTRIBUTING, CoC — present but with gaps (no PR
  template, no SECURITY, CI missing).
- Visual polish of the installer UI (dark theme, but no design system, no a11y).

**Where it is BEHIND:**
- **Trust/signing** — mature installers are code-signed and provide checksums.
- **Build/CI maturity** — competitors may be rough, but a project that *claims* CI
  should have it; currently it doesn't.
- **Discoverability** — zero topics, no social preview, no website, no community.
- **Accessibility** — competitors that are web/CLI-based inherit platform
  accessibility; a custom WinForms UI with no ARIA/AccessibleName is worse.
- **Cross-platform** — only Windows; Docker competitors run anywhere. This is a
  deliberate scope choice, worth stating.
- **Naming** — "Azaroth" vs "Azeroth" puts it behind in search for the exact term
  users type.

### Patterns worth adopting
- A **GitHub Pages docs/landing site** (like azerothcore.org has) with versioned
  docs, screenshots, and a one-click download.
- **Discord community** (de rigeur in the WoW private-server scene).
- **Reproducible builds + signed releases + SBOM** (modern OSS baseline).
- **Screenshot/GIF in README** (standard for desktop tools).
- **`awesome`-style module list** with compatibility notes for the module picker.

---

## 14. Missing Features — **45/100**

### High-value missing items
1. **`SECURITY.md`** — disclosure policy, supported versions, how to report vulns.
   (Critical for a tool that runs elevated.)
2. **Real CI workflow** committed to `.github/workflows/` (currently gitignored).
3. **`dependabot.yml`** + **CodeQL** + **SBOM** generation.
4. **SHA-256 / signature verification** in the downloader and release notes.
5. **Code signing** (Authenticode) — or at minimum a documented path.
6. **Uninstaller** (stop services, remove DB/firewall/shortcuts, restore
   realmlist).
7. **Screenshots/GIF** of the wizard in the README.
8. **GitHub social preview** + **topics** + **homepage link**.
9. **GitHub Discussions** or a Discord for support.
10. **PR template** (CONTRIBUTING references one that doesn't exist).
11. **Tests** (unit test project).
12. **`global.json`** SDK pin; reproducible build instructions.
13. **Accessibility statement** / WCAG target for the installer.
14. **Privacy statement** (even a short "no telemetry" note).
15. **LAN/expose toggle** in the UI (instead of always binding 0.0.0.0 + firewall).
16. **Resumable downloads** + ETA.
17. **"First 5 minutes" / post-install guidance** in the Done screen.
18. **Auto-updater** (notify of new `setup.exe` versions; download+verify+re-run).
19. **Backup/restore** of characters DB (one-click export/import `.sql`).
20. **Multi-language support** (the WoW community is global; at minimum
    enUS/esMX/zhCN/ruRU docs).
21. **A standalone brand mark / favicon** for a future site.
22. **`LICENSE` third-party notices** (AzerothCore AGPL, MySQL GPL, etc. —
    comply with attribution in the installer About box).

---

## 15. Priority Matrix — **70/100** (process score)

See `Priority-Roadmap.md` for the full phased plan and `Developer-Tasks.md` for
GitHub-issues-style tasks. Summary matrix:

| Priority | Items |
|---|---|
| 🔴 **Critical** | Random default credentials; SHA-256 download verification; un-ignore + commit CI; remove hardcoded local paths; fix `FindExistingInstall` non-C drive; `SECURITY.md` |
| 🟠 **High** | Code-signing plan; `--defaults-extra-file` for MySQL; parameterize/escape SQL; localhost-by-default + LAN opt-in; accessibility pass (keyboard nav, AccessibleName); PR template + Dependabot + CodeQL; screenshots + social preview + topics; version unification; fix smoke-test polling; GPU VRAM fix |
| 🟡 **Medium** | Uninstaller; resumable downloads + ETA; design system (`Theme.cs`); high-contrast/DPI; auto-updater; backup/restore; GitHub Pages site; Discussions/Discord; test project; privacy statement; third-party licenses |
| 🟢 **Low** | Badges; emoji→icons; FAQ; multi-language docs; AI log-diagnoser (opt-in); module catalog; perf trimming; changelog/Unreleased section |
