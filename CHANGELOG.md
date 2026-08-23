# Changelog

All notable changes to the Azaroth Core One-Click Installer.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning follows [SemVer](https://semver.org/).

## [1.1.0] - 2026-08-23

### Added

- **New "World & Options" wizard step** — a beautiful, detailed GUI for everything you might want for a personal server:
  - Realm identity: custom **realm name** (written to the auth DB), client **locale** picker, expansion card (3.3.5a WotLK)
  - **Progression & economy**: XP rate (1×–100×), Honor rate, Gold rate, **level cap** (80/70/60/50)
  - **PlayerBots population**: randombot count, auto-login, AddClass pool (instant raid members), max summonable bots, bot guilds, "only while I'm online"
  - **Module picker**: auto-detects every module DLL in the repack (transmog, AHBot, guild houses, …) — checkbox enable/disable (disabled modules move to `modules_disabled\`)
  - **Extra modules by URL** (prebuilt `.dll`/`.zip` dropped into the server folder)
  - **GM Genie** GM-tools client add-on (auto-installed into `Interface\AddOns`)
  - GM account/character editor + **instant-raid command card** (`lfg`, `bot addclass`, … — verified from the official PlayerBots wiki)
- **Dual AzerothCore config support**: writes DB credentials in both the new `LoginDatabaseInfo`/`WorldDatabaseInfo`/`CharacterDatabaseInfo` style (2026 AzerothCore) and the classic 4-key style (older repacks); realm name via the auth DB; module enablement via DLL presence (auto-load) and/or `ModuleList` (old style)
- All world options are also settable in `config.json → "world"` for unattended installs
- README/docs updated (wizard table, instant-raid cheat sheet, extra-module guidance, `world` config reference)

## [1.0.0] - 2026-08-23

First public release.

### Added

- **Single self-contained `setup.exe`** (bundles .NET 8 runtime, custom icon, no prerequisites, no terminal)
- **9-step wizard** with **⚡ Full Auto Install** mode (zero-click after UAC) and full manual mode
- **System Check**: CPU (model/cores/threads), RAM, GPU(s) + VRAM, Windows version, all fixed drives with free space; auto-picks the best install drive (≥ 30 GB, configurable)
- **Server Core**: local zip picker or direct-URL download (retries + URL fallbacks), streaming zip extraction with progress and zip-slip protection, automatic layout detection (worldserver/authserver/realmserver, `.conf`/`.conf.dist`, bundled MySQL + datadir, bundled `data/`, SQL dumps, nested zips)
- **Data & PlayerBots**: downloads AC Data (default v20) **only when the repack lacks `data/`**; fetches `playerbots.conf` from mod-playerbots when missing; adds `Mod_PlayerBots` to `ModuleList`
- **Database**: bundled-with-repack MySQL (prebuilt DB reused as-is) → existing local MySQL/MariaDB service (reused) → fresh silent MySQL 8 install; reuses existing `azaroth_*` databases (characters survive re-runs); optional forced re-import
- **Game Client**: registry + full-disk scan for WoW 3.3.5 clients (scores wow.exe/Data/Interface/WTF, flags `_retail_` modern clients); manual folder picker; realmlist.wtf patching
- **Verify & Finish**: writes auth/world/realm configs with correct DB credentials + ports, creates GM account (`gm`/`gm1234`, gmsec 3) + starter character, firewall rule, Start/Stop/Play desktop shortcuts, and a **live smoke test** (auth :3724, realm :8085, worldserver liveness) with automatic log-tail diagnostics on failure
- **Repair mode**: detects existing installs (`azaroth-installer.json` marker) and reuses DB + files
- **Live log panel** with timestamps, per-download progress/speed, marquee progress bar
- Configurable defaults via **`config.json`** next to the exe (JSONC with comments)
- GitHub: CI build workflow (win-x64 single-file publish on push/PR), issue/PR templates, troubleshooting + config docs
