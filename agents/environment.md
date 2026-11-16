# Environment: where things are

Default locations on both supported OSes. Code must find these through the locator (step 0.2) and allow overrides; never hard-code them. Facts specific to the maintainer's machine are in `agents/local/environment.local.md`, which is not in git.

## Tools

- .NET SDK 10. `global.json` pins the 10.0.400 feature band and rolls forward to newer 10.0 feature bands. The same SDK builds the net35 exporter, using reference-assembly packages and the game's own DLLs.
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
| Game log | `Cities_Data\output_log.txt` (verify) | `~/.config/unity3d/Colossal Order/Cities_ Skylines/Player.log` (underscore and space, not a colon) |
| Workshop items | `<library>\steamapps\workshop\content\255710\<item id>\` | same layout |

Inside the game, mods should use `ColossalFramework.IO.DataLocation` (`localApplicationData`, `addonsPath`, `modsPath`, `saveLocation`) instead of these tables. The exporter writes dumps to `<user data>/CsToBeamNG/dumps/<city>_<timestamp>/`; step 0.4 confirms this.

### Build-time locations (MSBuild properties)

The build finds the game through these properties, defined in `Directory.Build.props`. Overrides: see "Build, run, test" in `agents/AGENTS.md`.

| Property | Default on Windows | Default on Linux |
|---|---|---|
| `CitiesSkylinesDir` | `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines` | `$HOME/.steam/steam/steamapps/common/Cities_Skylines` |
| `CitiesSkylinesManagedDir` | `<CitiesSkylinesDir>/Cities_Data/Managed` | same |
| `CitiesSkylinesUserDir` | `%LOCALAPPDATA%\Colossal Order\Cities_Skylines` | `$XDG_DATA_HOME` (default `$HOME/.local/share`) + `/Colossal Order/Cities_Skylines` |
| `CitiesSkylinesModsDir` | `<CitiesSkylinesUserDir>/Addons/Mods` | same |
| `DeployMod` | `true` when the mods folder exists | same |

The defaults cover only the default Steam library. A game in another library needs an override; the converter's locator (step 0.2) searches all libraries.

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
