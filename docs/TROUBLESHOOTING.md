# Troubleshooting

The wizard keeps a live log (bottom of the window) and writes the tail of the
worldserver log into it when the smoke test fails. **Always read the log first** —
it tells you which step and which file went wrong.

## SmartScreen says “Windows protected your PC”

The release build is **unsigned** — that is normal for community server tools.
Click **More info → Run anyway**. If you build it yourself, the same applies until
you add a code-signing certificate.

## “No repack source available”

The wizard needs a prebuilt **Windows** AzerothCore zip.

- **Local:** Server Core step → *Browse for .zip* → pick your Azaroth/repack zip.
- **URL:** put a **direct HTTPS link to the .zip** in `config.json → downloads.serverRepack.urls`
  (Mega.nz / Google Drive links don't work — they need special API handling).

## Server core step: “worldserver.exe not found”

The zip isn't a Windows server package (or is a source-code archive). You need a
*prebuilt Windows* repack — e.g. your own Azaroth build, OwnedCore, DrePack, or any
AzerothCore WoTLK Windows release. Nested zips are followed automatically.

## Smoke test fails — “worldserver process exited”

The log shows the last ~12 lines of `logs/worldserver.log`. Common causes:

| Symptom in log | Cause / fix |
|----------------|-------------|
| `Can't find ... data/dbc` / `Data dir` errors | Game data missing. Re-run step 4 (Data & PlayerBots) or copy a `data/` folder into the server dir. |
| `Unable to connect to database` / `Access denied` | Wrong DB credentials. Check which source the Database step reported (bundled / local service / fresh install) and its login. For a fresh install the wizard writes `azaroth`/`Azar0th!DB` — make sure it actually connected (log line “database user ready”). |
| `Core version mismatch` / table errors | DB from a different core version than the repack. Tick *“re-import database files”* in the Database step (resets characters) or use the repack's bundled DB. |
| `Module ... not found` | Repack doesn't include the module it expects; use a matching repack or set `playerBots.enabled: false`. |

## Auth port 3724 won't open (authserver fails)

- Another server stack is already running → *Stop Azaroth Server*, or `taskkill /f /im worldserver.exe authserver.exe`.
- A different MySQL on port 3306 is blocking: the wizard *reuses* local MySQL
  services — check the Database step output for which one was used.

## MySQL port 3306 already in use

That's expected if a bundled/local MySQL is already running — the wizard detects and
reuses it (see the Database step log). If it's *some other* database you care about,
stop it before running the installer or point the repack at your own DB via its
config.

## Client can't find the server / login screen spins

1. The client must be **3.3.5a (build 12340)** — a modern Battle.net client will never work. The wizard flags `_retail_` installs as “not 3.3.5”.
2. The server must be running (*Start Azaroth Server* → wait for “world server is running”).
3. The wizard writes `Data\<locale>\realmlist.wtf → SET realmlist 127.0.0.1` and launches `wow.exe 127.0.0.1`. If you changed the login, edit `Play_Azaroth.bat`.
4. Windows Firewall: the wizard adds the rule for 3724/8085; if you removed it, re-add it or set `server.firewallRules: true` and re-run the Verify step.
5. LAN play: set `server.authAddress`/`realmAddress` to the server PC's LAN IP in `config.json`, re-run Verify, and launch the client on other machines with that IP.

## A module I enabled in the wizard isn't working

- Module DLLs must **match the repack's core version** — a DLL built for a different
  AzerothCore commit can fail to load (the worldserver log line names the module).
- Disabled modules are moved to `modules_disabled\` — move the file back to the server
  folder (or tick it in the wizard and re-run Verify) to re-enable.
- The wizard only installs **files**; it never compiles. For source-only repos from
  the [azerothcore org](https://github.com/azerothcore), get prebuilt DLLs from your
  repack author or a build you trust.

## First boot is slow

Normal. A fresh world imports/loads a large world database; the first “world server
is running” can take several minutes. Subsequent starts are much faster.

## The installer window closes instantly / UAC issue

- Run it as administrator (the wizard re-launches itself elevated; if UAC is off or
  blocked by policy it will say so).
- Some AV suites quarantine unsigned exes — allow it, or build from source (this repo).

## Re-running the installer (repair / update)

The wizard detects an existing install (marker file `azaroth-installer.json` in the
install folder) and **reuses the database and existing files** — characters are safe.
Use *“re-import database files”* only for a deliberately broken DB.

## Where are the files?

- Server: `<install drive>\Azaroth Core\...` (see the Done screen)
- Shortcuts: desktop
- Wizard state: `<install>\azaroth-installer.json`
- Server logs: `<server dir>\logs\`
- Temp downloads: `%TEMP%\AzarothInstaller`
