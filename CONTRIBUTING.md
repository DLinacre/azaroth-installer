# Contributing

Thanks for making the installer easier for the next person. This is a small,
deliberately boring codebase — keep it that way.

## Ground rules

1. **End-user first.** Every change must not require a terminal, a .NET install,
   or any prior knowledge from the person running `setup.exe`.
2. **Never destroy user data.** Re-runs must be safe: existing databases,
   characters, and a user's WoW client folder must survive a reinstall.
3. **Log everything, crash nothing.** Failures go into the live log with a
   human-readable explanation — no raw stack traces on screen.
4. **Prefer what's on disk.** If a file (data, DB, client, server) already exists,
   reuse it instead of re-downloading.

## Development

```powershell
# on Windows (or anywhere with .NET 8 SDK)
dotnet publish src -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

Test on a **clean Windows VM** (10 and 11, 64-bit) — at minimum:

- Full Auto run with a local repack zip
- Manual step-by-step run
- A second run over an existing install (repair path) — verify characters survive
- No-client scenario (server-only install)

## Pull requests

- Small PRs win. One behavior per PR.
- Update `CHANGELOG.md` (Unreleased section) and `docs/` if user-visible.
- Fill in the PR template's testing checklist honestly.

## Releasing (maintainers)

1. Bump `<Version>` in `src/AzarothInstaller.csproj`.
2. Tag: `git tag v1.x.y && git push origin v1.x.y`.
3. Run the CI workflow (or `build.ps1` locally).
4. GitHub → Releases → new release `v1.x.y` → attach `dist/setup.exe` and
   `dist/config.json` (the 145 MB exe stays in Releases, not in git).
5. Update `CHANGELOG.md` dates and the README "Releases" links.

## Assets

- Banner: `assets/banner.png` (regenerate with `python3 assets/build_assets.py`
  after replacing `banner-art.png` / `icon-art.png`)
- Icon: `assets/icon.ico` (embedded via `<ApplicationIcon>` in the csproj)
