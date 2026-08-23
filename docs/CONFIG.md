# `config.json` — full reference

The wizard reads **`config.json` from the folder containing `setup.exe`** (Notepad
editable; `//` comments are allowed). If the file is missing, the wizard writes this
default set for you. Values here are the *defaults shown in the wizard* — everything
can still be changed per-run in the UI.

```jsonc
{
  "serverName": "Azaroth",
  "installFolderName": "Azaroth Core",
  "minFreeSpaceGB": 30,
  "autoModeDefault": true,

  "downloads": {
    "serverRepack":   { "urls": [] },
    "acData":         { "urls": [ "https://github.com/wowgaming/client-data/releases/download/v20.0/Data.zip" ], "onlyIfMissing": true },
    "databaseServer": { "urls": [ "https://downloads.mysql.com/archives/get/p/23/file/mysql-8.0.42-winx64.msi" ], "onlyIfMissing": true },
    "playerBotsConf": { "urls": [ "https://raw.githubusercontent.com/mod-playerbots/mod-playerbots/master/conf/playerbots.conf.dist" ] }
  },

  "database": {
    "login": "azaroth", "password": "Azar0th!DB",
    "rootLogin": "root", "rootPassword": "",
    "authDb": "azaroth_auth", "charactersDb": "azaroth_characters", "worldDb": "azaroth_world"
  },

  "server": {
    "authAddress": "127.0.0.1", "authPort": 3724,
    "realmAddress": "127.0.0.1", "realmPort": 8085,
    "listenAddress": "0.0.0.0", "maxPlayers": 500,
    "gmUsername": "gm", "gmPassword": "gm1234", "gmCharacterName": "Azaroth",
    "firewallRules": true
  },

  "playerBots": { "enabled": true },

  "wowClient": { "autoScan": true, "extraScanDirs": [], "downloadUrls": [] }
}
```

## Top level

| Key | Type | Default | Meaning |
|-----|------|---------|---------|
| `serverName` | string | `"Azaroth"` | Realm name shown in the character-select screen, console titles, shortcut names, firewall rule. |
| `installFolderName` | string | `"Azaroth Core"` | Folder created on the chosen drive. |
| `minFreeSpaceGB` | number | `30` | Free-space threshold the wizard checks before installing. |
| `autoModeDefault` | bool | `true` | Pre-emphasizes the ⚡ Full Auto button. |

## `downloads.*` — every download has a URL *list* (fallbacks, 3 retries each)

| Key | Meaning |
|-----|---------|
| `serverRepack.urls` | **Direct HTTPS link(s) to the prebuilt Windows AzerothCore + PlayerBots zip.** Empty → the wizard asks you to pick a local `.zip`. Use a direct file link (Mega/GDrive links are not supported). |
| `acData.urls` | Game data zip (dbc/maps/vmaps/mmaps). Default: **AC Data v20** from wowgaming/client-data. With `onlyIfMissing: true` it is skipped when the repack already contains a `data/` folder. |
| `databaseServer.urls` | MySQL MSI, used **only** when the repack has no bundled MySQL *and* no local MySQL/MariaDB service exists. |
| `playerBotsConf.urls` | Fallback source for `playerbots.conf` when the repack doesn't ship one. |

## `database`

| Key | Meaning |
|-----|---------|
| `login` / `password` | Dedicated DB user the server runs as (created automatically). |
| `rootLogin` / `rootPassword` | Account used to *set up* the DB on an existing local server (blank password first, then this one, then `test`). |
| `authDb` / `charactersDb` / `worldDb` | Database names. |

## `server`

| Key | Meaning |
|-----|---------|
| `authAddress` / `authPort` | Where the client connects (default `127.0.0.1:3724`). Use your LAN IP here **and** in `listenAddress` for LAN play. |
| `realmAddress` / `realmPort` | Realmlist listener (default `127.0.0.1:8085`). |
| `listenAddress` | Bind address for auth (default `0.0.0.0` — accepts LAN). |
| `maxPlayers` | `MaxPlayers` in worldserver.conf. |
| `gmUsername` / `gmPassword` | GM account created at install time (gmsec 3). **Change these before running on a shared LAN.** |
| `gmCharacterName` | Starter character (Human Mage, level 1, Stormwind). |
| `firewallRules` | Add the inbound TCP rule for 3724/8085. |

## `world` (World & Options step defaults)

| Key | Meaning |
|-----|---------|
| `realmName` | Realm name shown on the character-select screen (written to the auth DB `realm` table). |
| `clientLocale` | `auto` or a fixed locale (`enUS`, `enGB`, …) used for the realmlist file. |
| `xpRate` | Experience multiplier (applied to `Rate.XP.*` or `Rate.XP` — whichever the repack uses). |
| `honorRate` | `Rate.Honor` multiplier. |
| `goldRate` | `Rate.Gold` multiplier (only written when the repack's config has the key). |
| `levelCap` | `MaxPlayerLevel` (80 = no cap; 70/60/50 also offered). |
| `randomBots` | Number of randombots (`AiPlayerbot.MinRandomBots`/`MaxRandomBots`). |
| `botsAutologin` | `AiPlayerbot.RandomBotAutologin` — bots log in when the world starts. |
| `addClassPool` | `AiPlayerbot.AddClassAccountPoolSize` — pool for instant raid members. |
| `maxAddedBots` | `AiPlayerbot.MaxAddedBots` — max bots you can summon at once. |
| `botGuilds` | `AiPlayerbot.RandomBotGuildCount` — bot guilds in the world. |
| `botsOnlyWhenPlayerOnline` | `AiPlayerbot.DisabledWithoutRealPlayer`. |
| `extraModules` | List of `{ "name": "...", "url": "https://.../Mod_X.dll" }` or `.zip` links, dropped into the server folder. |
| `gmGenieAddon` | Install the [GM Genie](https://github.com/azerothcore/GMGenie) GM-tools add-on into the detected client's `Interface\AddOns`. |

## `playerBots`

| Key | Meaning |
|-----|---------|
| `enabled` | Add `Mod_PlayerBots` to `ModuleList` and ensure `playerbots.conf` exists. Set `false` for a botless world. |

## `wowClient`

| Key | Meaning |
|-----|---------|
| `autoScan` | Auto-scan the PC for a 3.3.5 client when the Game Client step is opened. |
| `extraScanDirs` | Extra folders to check (e.g. an external drive with an old client). |
| `downloadUrls` | Reserved for a future client download helper (client files are never redistributed by this project). |
