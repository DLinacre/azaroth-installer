# README / Listing Rewrite Suggestions

## 1. Repository description (About field — ~120 chars)

> One-click Windows installer for a private AzerothCore 3.3.5a (WotLK) server with
> PlayerBots. No terminal, no compiler — just setup.exe.

## 2. Suggested README top block (drop-in)

```markdown
<p align="center">
  <picture>
    <source srcset="assets/banner.avif" type="image/avif">
    <source srcset="assets/banner.webp" type="image/webp">
    <img src="assets/banner.png" alt="Azaroth Core — One-Click Installer" width="1280" height="640" style="max-width:100%;height:auto;">
  </picture>
</p>

<h1 align="center">Azaroth Core — One-Click Installer</h1>

<p align="center">
  <strong>Your own private WotLK 3.3.5a world with PlayerBots — from a single <code>setup.exe</code>.</strong><br>
  No terminal · No compiler · No developer skills
</p>

<p align="center">
  <a href="https://github.com/DLinacre/azaroth-installer/releases/latest">
    <img alt="Download setup.exe" src="https://img.shields.io/badge/Download-setup.exe-2f9a5a?style=for-the-badge&logo=windows">
  </a>
  <a href="https://github.com/DLinacre/azaroth-installer/releases/latest">
    <img alt="Latest release" src="https://img.shields.io/github/v/release/DLinacre/azaroth-installer?style=for-the-badge">
  </a>
  <a href="https://github.com/DLinacre/azaroth-installer/blob/main/LICENSE">
    <img alt="MIT License" src="https://img.shields.io/badge/license-MIT-blue?style=for-the-badge">
  </a>
  <a href="https://github.com/DLinacre/azaroth-installer/actions">
    <img alt="Build status" src="https://img.shields.io/github/actions/workflow/status/DLinacre/azaroth-installer/build.yml?style=for-the-badge">
  </a>
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=.net">
</p>

<p align="center">
  <a href="docs/CONFIG.md"><b>Configuration</b></a> ·
  <a href="docs/TROUBLESHOOTING.md"><b>Troubleshooting</b></a> ·
  <a href="#frequently-asked-questions"><b>FAQ</b></a> ·
  <a href="CONTRIBUTING.md"><b>Contributing</b></a> ·
  <a href="CHANGELOG.md"><b>Changelog</b></a>
</p>

---
```

## 3. "You will need" callout (place right after the intro)

```markdown
> **You'll need:** Windows 10/11 64-bit · 4 GB RAM (8–16 recommended) ·
> 30 GB free disk space · **your own World of Warcraft 3.3.5a (build 12340)
> client** · and a **prebuilt Windows AzerothCore + PlayerBots zip** (host your
> own, or paste a direct HTTPS link into `config.json`). The installer never
> downloads or modifies your WoW client.
```

## 4. Replace printed default credentials

Instead of:
> a ready GM account (`gm` / `gm1234`)

Use:
> a ready GM account with a **password generated (or chosen by you) during
> install** — shown once on the Done screen and saved in the install folder.
> **Change it before exposing the server to a LAN.**

## 5. FAQ to append to README

```markdown
## Frequently asked questions

**Is this legal?** The installer is open source (MIT) and never redistributes
Blizzard game files — it reuses the 3.3.5a client you already own. Running a
private WoW server can violate Blizzard's EULA/ToU; use it privately and
non-commercially at your own risk. See [Legal](#legal).

**Why does SmartScreen warn me?** The build isn't code-signed yet (common for
community tools). Click **More info → Run anyway**, and verify the
[SHA-256 checksum](https://github.com/DLinacre/azaroth-installer/releases/latest)
on the release page. Signed builds are on the roadmap.

**Can I play with friends?** Yes — enable **LAN play** in the wizard; it binds
to your LAN IP and adds a local-subnet firewall rule. Don't expose the server to
the internet without changing the generated passwords.

**What if I have no repack zip?** The wizard is repack-agnostic and accepts any
prebuilt Windows AzerothCore 3.3.5a zip. Put a direct HTTPS link in
`config.json → downloads.serverRepack.urls` or pick a local `.zip` at the Server
Core step.

**Will reinstalling wipe my characters?** No. The installer detects an existing
install and reuses the `azaroth_*` databases; characters survive re-runs. Use
"re-import database files" only if you deliberately want a reset.
```

## 6. Release notes template

```markdown
## Azaroth Core vX.Y.Z

**SHA-256 (setup.exe):** `<hash>`
**Signature:** Authenticode signed by `<cert subject>` / unsigned (community)

### What's new
- …

### Install
1. Download `setup.exe` (and `config.json` if customizing).
2. Verify: `Get-FileHash .\setup.exe -Algorithm SHA256`
3. Double-click → **More info → Run anyway** (unsigned builds) → **Full Auto Install**.

### Known issues
- …

### Checksums
| File | SHA-256 |
|------|---------|
| setup.exe | `…` |
| config.json | `…` |
```
