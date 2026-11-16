# Game and folder locations

How Steam, Cities: Skylines and BeamNG.drive lay out their files on disk, as far as CsToBeamNG relies on it. The locator in `src/CsToBeamNG.Converter/Discovery/` implements this, and `cstobeamng doctor` shows what it finds. Overrides and the generic per-OS table are in [agents/environment.md](../../agents/environment.md).

Checked on 2026-11-17 on Linux Mint 22 with Debian-packaged Steam, Cities: Skylines Steam build 22724702 and BeamNG.drive 0.40.5.0 (build 302427). "Verified" means checked against those files.

## Steam

### Steam folders (roots)

- **Linux:** `~/.steam/steam` and `~/.steam/root` are symbolic links to the real Steam folder. With Debian/Ubuntu-packaged Steam that folder is `~/.steam/debian-installation`, and the `steam` link's target ends with a slash. [verified]
- Other Linux installs keep the folder in `~/.local/share/Steam`, `~/.var/app/com.valvesoftware.Steam/.local/share/Steam` (Flatpak) or `~/snap/steam/common/.local/share/Steam` (Snap). [community] That Steam uses `$XDG_DATA_HOME/Steam` when the variable is set is [inferred]; the locator tries it.
- **Windows:** the registry value `HKCU\Software\Valve\Steam\SteamPath` holds the folder, written with forward slashes and often in lower case. `HKLM\SOFTWARE\WOW6432Node\Valve\Steam\InstallPath` holds the install location. The default is `%ProgramFiles(x86)%\Steam`. [community]
- A Steam folder contains `steamapps`. Old installs used `SteamApps`, which matters on case-sensitive file systems. [community]

### Libraries

- The list of libraries is `steamapps/libraryfolders.vdf`. An identical copy is in `config/libraryfolders.vdf`. [verified]
- **Current format:** a top-level `libraryfolders` section with numbered sections `"0"`, `"1"`, ...
  - Each section has `path`, `label`, `contentid`, `totalsize`, two update counters and `apps` (app id → size on disk).
  - The Steam folder lists itself as `"0"`.
  - [verified]
- **Older format:** the numbered keys have the path as a plain value, next to keys such as `TimeNextStatsReport` and `ContentStatsID`. [community] The locator reads both and ignores non-numeric keys.
- A listed library can be missing, for example on a disconnected drive. The locator reports it and goes on.

### KeyValues (VDF) text

- **Syntax:** quoted keys and values, `{ }` sections, whitespace between tokens. Steam writes backslashes in paths as `\\`. [verified]
- **Keys** compare case-insensitively: Steam has written both `LibraryFolders` and `libraryfolders`. [community]
- **Other features of the format:** `//` comments, unquoted tokens, and conditions such as `[$WIN32]`. [community: Valve Developer Community, "KeyValues"] The parser accepts them all; it skips conditions.

### Apps

- **App manifest:** `steamapps/appmanifest_<appid>.acf` has an `AppState` section with `appid`, `name`, `installdir`, `buildid` and download state. [verified]
- **Game folder:** `<library>/steamapps/common/<installdir>`. [verified]
- **Workshop items:** `<library>/steamapps/workshop/content/<appid>/<item id>/`. [verified]
- **Steam Cloud's local copies:** `<steam folder>/userdata/<account id>/<appid>/remote/`. The account id is a number; never record it in the repository. [verified]
- **Proton prefixes:** `<library>/steamapps/compatdata/<appid>/pfx/`. Inside, the Windows user profile is `drive_c/users/steamuser/`, with `AppData/Local`. [verified for BeamNG.drive]

## Cities: Skylines (app 255710)

- **Install folder:** `installdir` is `Cities_Skylines`, and the managed DLLs are in `Cities_Data/Managed/` (`Assembly-CSharp.dll` marks a real install). [verified]
- **User data, Linux:** `$XDG_DATA_HOME/Colossal Order/Cities_Skylines`, by default `~/.local/share/...`. [verified for the default]
  - With Flatpak Steam the game probably sees `~/.var/app/com.valvesoftware.Steam/data` or `.../.local/share` as its data folder. [inferred; the locator tries both]
- **User data, Windows:** `%LOCALAPPDATA%\Colossal Order\Cities_Skylines`. [community] Under Proton the same path lies inside the prefix of app 255710. [inferred]
- **Savegames:**
  - Local saves are `.crp` files in `<user data>/Saves/`. [community]
  - Steam Cloud saves are `.crp` files in `userdata/<account id>/255710/remote/`, next to `sharedSettings.cgs`. [verified]
  - The local `Saves` folder can be empty when every save is in the cloud. [verified]
- **Game log:**
  - Native Linux build: `~/.config/unity3d/Colossal Order/Cities_ Skylines/Player.log`. That `$XDG_CONFIG_HOME` replaces `~/.config` when set is [inferred]. See also [cs1-save-and-world.md](cs1-save-and-world.md).
  - Windows build: `Cities_Data\output_log.txt`. [inferred from Unity 5.x conventions]

## BeamNG.drive (app 284160)

### Install and version

- **Install folder:** `installdir` is `BeamNG.drive`. [verified]
- **Version:** `integrity.json` in the install folder has the top-level keys `version` (`"0.40.5.0"`), `buildinfo` (`"buildbot 302427 on GitLab"`), `product`, `format`, and the large `integritydata` array. This describes the installed version even before the game first runs after an update, so the locator prefers it. [verified]
- **Log:** the first line of `beamng.log` reads `... -- Log started - v 0.40.5.0 - x64 - build 302427 - <date and time> -----`. [verified]

### User folder

- **Since 0.37:** `<data>/BeamNG/BeamNG.drive/current`.
  - On Linux, `<data>` is `$XDG_DATA_HOME` or `~/.local/share`. [official: BeamNG documentation, "Userfolder"; verified: the game logs `Virtual Filesystem: user path: .../BeamNG/BeamNG.drive/current/ (Default behaviour)`]
  - On Windows, `<data>` is `%LOCALAPPDATA%`. [verified inside a Proton prefix, where the Windows build created `AppData/Local/BeamNG/BeamNG.drive/current`]
- **`BeamNG.drive.ini`:** sits next to the `BeamNG.drive` folder and records the version that last ran, as `version = 0.40.5.0`. The game logs `Saving version '...' to ini file '...'`. [verified]
  - The file is UTF-8 with a byte order mark.
  - The Windows build also writes `installPath = <install folder>` and uses CRLF line endings. [verified]
- **Backups:** on a major update the game moves unknown files out of the user folder, into a backup folder next to `current` named after the date and versions, such as `2026-07-29_17-17-47__v0.38.4.0__update_to_v0.39.0.0`. [official: "Userfolder" page; verified]
- **Before 0.37:** the folder was `<data>/BeamNG.drive/<major.minor>`, such as `0.36`, with a `version.txt` next to it that holds the full version (`0.36.4.0`, no line break). [verified in a Proton prefix] The game's binary mentions both a "pre-v0.37" and a "post-v0.37" layout. [verified: strings in the binary] The locator reports such folders as unused.
- **Moving the user folder:** the launcher's "Manage User Folder" setting, and `UserPath` in the install folder's `startup.ini`. The file's own comments say:
  - an empty value is ignored;
  - a relative path is relative to the folder of the ini file;
  - an absolute path is used as it is.

  [verified: comments in `startup.ini`] That the game then uses `<UserPath>/current` is [inferred]: the binary speaks of a "user path without version". The locator uses `<UserPath>/current` only if it exists, and warns otherwise.
- **Launch options:** the game also accepts `-userpath <path>` and `-nouserpath`. [verified: strings in the binary] The locator can't see launch options; use `CSTOBEAMNG_BEAMNG_USER_DIR` for such setups.
- **Proton:** a Proton install keeps its own user folder inside the prefix of app 284160. The locator uses the native folder when both exist. [verified: both exist on the test machine]
