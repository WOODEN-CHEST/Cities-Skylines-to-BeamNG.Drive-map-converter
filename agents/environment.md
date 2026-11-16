# Environment: where things are

Default locations on both supported OSes. Code must find these through the locator (step 0.2) and allow overrides; never hard-code them. Facts specific to the maintainer's machine are in `agents/local/environment.local.md`, which is not in git.

## Tools

- .NET SDK 10, pinned in `global.json` after step 0.1. It also builds the net35 exporter, using reference-assembly packages and the game's own DLLs.
- Optional: Python 3 for throwaway analysis scripts. Keep them out of the product.

## Steam

| | Windows | Linux |
|---|---|---|
| Steam root | registry `HKCU\Software\Valve\Steam` value `SteamPath`, usually `C:\Program Files (x86)\Steam` | `~/.steam/steam`, a symlink (Debian/Ubuntu-packaged Steam resolves to `~/.steam/debian-installation`); also `~/.local/share/Steam`, and for Flatpak `~/.var/app/com.valvesoftware.Steam/.local/share/Steam` |
| Library list | `<root>\steamapps\libraryfolders.vdf` | `<root>/steamapps/libraryfolders.vdf` |
| App install dir | `<library>\steamapps\common\<installdir>`, with `installdir` from `appmanifest_<appid>.acf` | same layout |

App ids: Cities: Skylines **255710**, BeamNG.drive **284160**.

## Cities: Skylines

| | Windows | Linux |
|---|---|---|
| Install | `<library>\steamapps\common\Cities_Skylines` | `<library>/steamapps/common/Cities_Skylines` (native build, `Cities.x64`) |
| Managed DLLs | `Cities_Data\Managed\` | `Cities_Data/Managed/` |
| User data | `%LOCALAPPDATA%\Colossal Order\Cities_Skylines` | `$XDG_DATA_HOME/Colossal Order/Cities_Skylines` (default `~/.local/share/...`) |
| Local mods | `<user data>\Addons\Mods\<ModName>\` | same layout |
| Saves | `<user data>\Saves\`, and Steam Cloud saves in `<steam root>\userdata\<account>\255710\remote\` | same layout |
| Game log | `Cities_Data\output_log.txt` (verify) | `~/.config/unity3d/Colossal Order/Cities: Skylines/Player.log` |
| Workshop items | `<library>\steamapps\workshop\content\255710\<item id>\` | same layout |

Inside the game, mods should use `ColossalFramework.IO.DataLocation` (`localApplicationData`, `addonsPath`, `modsPath`, `saveLocation`) instead of these tables. The exporter writes dumps to `<user data>/CsToBeamNG/dumps/<city>_<timestamp>/`; step 0.4 confirms this.

## BeamNG.drive

| | Windows | Linux |
|---|---|---|
| Install (read-only!) | `<library>\steamapps\common\BeamNG.drive` | `<library>/steamapps/common/BeamNG.drive` (native binary `BinLinux/BeamNG.drive.x64`, Vulkan) |
| User folder | `%LOCALAPPDATA%\BeamNG\BeamNG.drive\current` (verify; older versions used `%LOCALAPPDATA%\BeamNG.drive\<version>`) | `$XDG_DATA_HOME/BeamNG/BeamNG.drive/current` |
| Version | `BeamNG.drive.ini` next to the `BeamNG.drive` folder (`version = ...`), or the first line of `beamng.log` | same |
| Mods in development | `<user folder>\mods\unpacked\<mod>\` | same layout |
| Log | `<user folder>\beamng.log` | same layout |
| Texture cache | `<user folder>\temp\` | same layout |

- The user folder can be moved in the BeamNG launcher ("Manage User Folder"), so it must be overridable.
- Under Proton, the Windows layout lives inside `steamapps/compatdata/284160/pfx/`.
- Official levels ship as zips in `<install>/content/levels/`; `template.zip` is a minimal official level. Read them for reference, but never modify them or copy their assets.
- The game folder contains `NOTE_FOR_MODDERS.md` with BeamNG's rules for mods (0.40+).

## BeamNG MCP server

Start BeamNG with the `-enablemcp` launch argument (or turn on the `enableMcp` setting). The server is then at `http://127.0.0.1:29292/mcp`. See `agents/verification.md`.
