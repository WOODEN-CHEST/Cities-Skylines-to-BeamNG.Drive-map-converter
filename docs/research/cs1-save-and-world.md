# CS1: savegames, world data and game APIs

## Savegame container

- Saves are `.crp` packages. [community: [CRAP file format](https://skylines.paradoxwikis.com/CRAP_File_Format), [data types](https://wiki.biligame.com/csl/Data_types)]
  - **Header:** magic `CRAP`, u16 format version (6), package name, author, u32 package version, main asset name, u32 entry count, u64 data offset.
  - **Entry table:** name, checksum, u32 type, u64 offset, u64 size.
  - **Encoding:** little-endian; strings use .NET's 7-bit-encoded length prefix.
  - [verified 2026-11-16 on a local save] Magic `CRAP` and version 6. The entries are `<City>_Data` (the simulation), `<City>_SteamPreview`, `<City>_Snapshot` and `<City>` (save metadata).
- The `_Data` payload is the output of `ColossalFramework.IO.DataSerializer` [verified class name]:
  - Each manager (NetManager, BuildingManager, ...) serialises a nested `Data` class field by field, with version-dependent branches, and mods add their own data blobs.
  - No public byte-level specification exists, and no external tool decodes it. [community: [CSM dev notes](https://github-wiki-see.page/m/CitiesSkylinesMultiplayer/CSM/wiki/Development-Notes:-Save-Games), [Ccs Savegame Recoverer](https://steamcommunity.com/sharedfiles/filedetails/?id=2891024966)]
- Consequence ([decision D1](../architecture.md#decision-log)): we never parse saves ourselves. The exporter mod lets the game load the save and reads the managers in memory.

## Save locations

- Local saves are in `<CS1 user data>/Saves/`.
- Steam Cloud saves are in `<Steam root>/userdata/<account id>/255710/remote/*.crp`.
- The game lists both. [verified: the maintainer's saves exist only in the cloud folder]
- A save's file name isn't the city's name: `CityPalma.crp` holds the city "San Palma". [verified 2026-11-17 by the first dump]

## World geometry

- The map is 9 × 9 tiles of 1920 m, so 17,280 m square, centred on the origin: −8640 … +8640 on X and Z. [community: [Map Editor wiki](https://skylines.paradoxwikis.com/Map_Editor)]
- Vanilla only allows building inside the central 5 × 5 tiles. How many of those can be bought depends on version and settings; community sources say 9 by default. 81-tile mods (e.g. 81 Tiles 2) unlock the whole 9 × 9 map. [community: EML `EGameAreaManager`]
- Coordinates are Unity world space: metres, Y up, left-handed, with X and Z horizontal.
- The conversion to BeamNG is described in [architecture.md, Coordinate systems](../architecture.md#coordinate-systems).

## Map tiles

[verified 2026-11-17 by reading the IL of `GameAreaManager` (Steam build 22724702) and of 81 Tiles 2 (workshop item 2862121823)]

- `GameAreaManager.m_areaGrid` (`int[]`) records which tiles are unlocked. In vanilla it covers the central 5 × 5 tiles (`AREAGRID_RESOLUTION` = 5; `TOTAL_AREA_RESOLUTION` = 9 is the whole map; `AREAGRID_CELL_SIZE` = 1920).
  - Layout: row by row, index `z * 5 + x`, with x growing with world X and z with world Z.
  - Position: grid tile x covers world X from (x − 2.5) · 1920 to (x − 1.5) · 1920 m, and z likewise, so grid tile (x, z) is tile (x + 2, z + 2) of the 9 × 9 map grid used in the dump.
  - Values: 0 means locked. Unlocking a tile stores the incremented `m_areaCount`, so the value is the order in which the tile was unlocked, and the starting tile holds 1. [Also verified in game on 2026-11-17: CityPalma's grid holds 1 at the centre (grid tile (2, 2)), and 2 and 3 at two neighbours.]
  - `IsUnlocked(x, z)` and `GetArea(x, z)` take grid coordinates. `GetArea` returns the stored value, −1 for a tile outside the 5 × 5 grid but on the map, and −2 off the map.
- 81 Tiles 2 replaces `m_areaGrid` with an 81-entry array in the same row-by-row layout over the whole 9 × 9 map, and patches the methods that assume 5. When it converts a save without its own data, it copies vanilla entry (x, z) to (x + 2, z + 2).
- So the grid's size follows from the array's length: 25 entries for vanilla, 81 with 81 Tiles 2. The exporter centres any odd square grid on the 9 × 9 map ([dump-format.md, `city`](../dump-format.md#city)).

## Terrain

- **Height grid:** 1081 × 1081 points at 16 m spacing, covering the whole map. Heights are stored as u16 / 64, giving 0–1024 m.
- **Height arrays:** `TerrainManager` exposes `RawHeights` (as authored or terraformed) and `FinalHeights` (after modifications). [community: [ExtraLandscapingTools TerrainToolDetour.cs](https://github.com/bloodypenguin/Skylines-ExtraLandscapingTools/blob/master/NaturalResourcesBrush/Detours/TerrainToolDetour.cs), [GeoSkylines](https://github.com/gonzikcz/GeoSkylines)] [verified: both names exist in Assembly-CSharp]
- **Detail grid:** 480 × 480 cells per 1920 m tile (4 m cells), but only for tiles that have detail data allocated. Which tiles get detail data in vanilla is unverified. [community: [EML ETerrainManager.cs](https://github.com/Quistar-LAB/EManagersLib/blob/master/ETerrainManager.cs)]
- **Sampling helpers** exist: `SampleDetailHeight`, `SampleRawHeightSmooth`, `SampleRawHeightSmoothWithWater`, `SampleRawHeightWithWater`, `GetBlockHeight`, and `GetSurfaceCell` (surface painting such as pavement, gravel, ruined). [verified: the names exist; their semantics are community or inferred]
- **Open:** which height source matches the rendered ground under roads and buildings. Step 1.2 investigates.

## Water

- **Sea level** is a map setting, 0–500 according to the map editor wiki. [community: Map Editor wiki]
- **Water sources** have a target height (default 50) and keep producing water until the surface reaches it. [community: Map Editor wiki]
- **Sampling:** surface heights can be read with `SampleRawHeightSmoothWithWater` and `SampleRawHeightWithWater`. Sources are in the `WaterSimulation`'s `m_waterSources`. [verified: the names exist; semantics community]
- **Open:** the storage format and resolution of the water grid, and where the sea level value lives. There is no field called `m_seaLevel` [verified].

## Instance limits (vanilla)

The limits are the managers' `MAX_*_COUNT` constants. [verified 2026-11-17 in Assembly-CSharp, Steam build 22724702]

| Type | Constant | Limit | Notes |
|---|---|---|---|
| Buildings | `BuildingManager.MAX_BUILDING_COUNT` | 49,152 | |
| Net segments | `NetManager.MAX_SEGMENT_COUNT` | 36,864 | |
| Net nodes | `NetManager.MAX_NODE_COUNT` | 32,768 | |
| Net lanes | `NetManager.MAX_LANE_COUNT` | 262,144 | |
| Props | `PropManager.MAX_PROP_COUNT` | 65,536 (map editor: 50,000 [community]) | ×4 with Extended Managers / Prop Anarchy, using uint ids [community: [EML EPropManager.cs](https://github.com/Quistar-LAB/EManagersLib/blob/master/EPropManager.cs)] |
| Trees | `TreeManager.MAX_TREE_COUNT` | 262,144 (map editor: 250,000 [community]) | ×4 with Tree Anarchy [community: [TreeAnarchy TAMod.cs](https://github.com/Quistar-LAB/TreeAnarchy/blob/master/TAMod.cs)] |

How instances are stored [verified 2026-11-17 in the IL]:

- Each manager keeps its instances in an array wrapper whose `m_buffer` has the limit's length: `Array16<T>` for nodes (`NetManager.m_nodes`), segments (`m_segments`), buildings (`BuildingManager.m_buildings`) and props (`PropManager.m_props`); `Array32<T>` for lanes (`NetManager.m_lanes`) and trees (`TreeManager.m_trees`). The index is the instance's id.
- An entry is in use when its `Created` flag (value 1) is set. `NetNode`, `NetSegment` and `Building` have typed `m_flags`; `NetLane`, `PropInstance` and `TreeInstance` store theirs in a `ushort m_flags` that holds their `Flags` enum.
- The managers' counters (`m_nodeCount`, `m_segmentCount`, `m_laneCount`, `m_buildingCount`, `m_propCount`, `m_treeCount`) are set to the wrapper's item count minus one (id 0 is never used) when an instance is created or released and after loading. [Also verified in game on 2026-11-17: in CityPalma, all six counters equal the number of entries with `Created` set.]
- Sub-buildings are building instances of their own ([cs1-assets.md](cs1-assets.md)), linked through `Building.m_parentBuilding` and `m_subBuilding` [verified names]. Props that belong to buildings or networks are placed from their prefabs ([cs1-assets.md](cs1-assets.md), [cs1-networks.md, Lane props](cs1-networks.md#lane-props)), so `PropManager` holds only the props placed on their own [community].

Prop positions are stored quantised: x and z as short × 0.263671875 m, y as u16 / 64 m, and the angle as u16 × 2π/65536. EML adds extra precision. Read positions through the game's accessors instead of decoding the fields. [community: [EML EPropInstance.cs](https://github.com/Quistar-LAB/EManagersLib/blob/master/EPropInstance.cs)]

## Threads

[verified 2026-11-17 by reading the IL of Assembly-CSharp and ColossalManaged, Steam build 22724702]

- **Simulation thread.** The simulation runs on its own thread (`SimulationManager.m_simulationThread`). Each `FixedUpdate` of `SimulationManager` on the main thread adds to a counter (up to a maximum number of frames behind) and wakes the thread, which then runs `SimulationStep`.
- **Actions run even while paused.** `SimulationStep` first runs every action queued with `SimulationManager.AddAction`, and only then advances the simulation, unless it is paused. The game never changes Unity's `Time.timeScale`, so fixed updates, and with them queued actions, keep running while the game is paused, for example while Options is open.
  - `AddAction(Action)` returns an `AsyncAction`. Overloads take an `IEnumerator` for work spread over several steps.
  - Code in an action sees the simulation between two steps, so a whole read in one action is consistent.
- **Back to the main thread.** `ColossalFramework.Threading.ThreadHelper.dispatcher.Dispatch(Action)` queues an action for the main thread, where `ThreadHelper.Update` runs it. The game itself dispatches this way from the simulation thread.
- **ICities equivalents.** `IThreading.QueueSimulationThread` calls `AddAction`, and `QueueMainThread` calls `Dispatch`; each runs the action at once if it is already on the right thread.
- **Singletons.** `Singleton<T>.instance` (ColossalFramework, `T : Component`) creates the singleton when it doesn't exist yet, with `Object.FindObjectOfType` or a new `GameObject`: Unity calls that are only allowed on the main thread. `Singleton<T>.exists` checks without creating. The managers derive from it, so `NetManager.instance` and the like should be resolved on the main thread.
- **Logging.** The game's own logging ends in `UnityEngine.Debug.Log`, which it also calls on the simulation thread, so Unity's logging works from other threads.
- **Logged errors.** The game's `ErrorManager` subscribes to `Application.logMessageReceivedThreaded` and collects messages of the types Error, Assert and Exception. What it then shows the user is [inferred]: its error display. A mod that handles and reports its own problems should log them as warnings.

## Runtime and modding facts

- **Game build:** Unity 5.6.7f1. The managed assemblies are in `Cities_Data/Managed/` (Assembly-CSharp, ColossalManaged, ICities, UnityEngine and others), and there is a native Linux build (`Cities.x64`). [verified 2026-11-16]
- **Mods** are .NET Framework 3.5 class libraries in `<user data>/Addons/Mods/<Name>/`; the game also compiles `Source/*.cs` at startup. [community: [Modding API](https://skylines.paradoxwikis.com/Modding_API)]
  - Our mod built with the .NET 10 SDK (C# 14, net35 reference assemblies from NuGet, compiler-generated nullable attributes) loads without errors and is listed in Content Manager → Mods. [verified 2026-11-16, native Linux, Steam build 22724702]
  - The game loads every DLL in a mod folder. The log shows `Loading <path>` and then `Assembly <name>, Version=... loaded.`, both tagged `[Mods - Internal]`. A folder without `Source/` gets `No source files found: <folder>`. [verified 2026-11-16]
  - Every mod assembly, built-in ones such as UnlockAll included, also produces `Non platform assembly: data-0x... (this message is harmless)` and `Fallback handler could not load library .../data-0x....so`. These lines are normal. [verified 2026-11-16]
- **Mod entry points** come from ICities: `IUserMod` and `LoadingExtensionBase` [verified].
- **Settings UI** [verified 2026-11-17 in the IL]:
  - The game's `OptionsMainPanel` finds `OnSettingsUI(UIHelperBase)` on the mod's `IUserMod` instance with `GetMethod("OnSettingsUI", Public | Instance)`, so it must be a public instance method; it isn't part of the interface.
  - `UIHelperBase` offers buttons, checkboxes, sliders, dropdowns, text fields, groups and spaces, but no labels. Its implementation, `UIHelper` (global namespace), exposes the group's panel as `self` (a `UIComponent`), to which a `UILabel` can be added with `AddUIComponent<UILabel>()`.
  - The game shows its own error messages with `UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(title, message, isError)`. The exporter uses the same panel, also for messages that aren't errors, which it shows with an OK button. [verified in game 2026-11-17]
- **Loading extensions** [verified 2026-11-17 in the IL]:
  - The game calls `OnLevelLoaded(LoadMode)` on every `ILoadingExtension` and catches what they throw, reporting it as "The Mod … has caused an error" through `UIView.ForwardException`.
  - `ILoading.currentMode` maps the tool controller's mode (`ItemClass.Availability`) to `AppMode`: Game, MapEditor, AssetEditor, ThemeEditor or ScenarioEditor; any other value counts as Game. It is the simplest test for "a city is being played".
  - `LoadMode` has the same values as `SimulationManager.UpdateMode`, where `NewGame` is called `NewGameFromMap`.
- **Global namespace.** Assembly-CSharp declares the game's types in the global namespace, including common names such as `ModInfo`, `NetInfo` and `Building` [verified]. In a mod, such a type wins over a type of the same name imported with `using`, and a `using` alias of that name is a compile error (CS0576). That is why the Dump library calls its mod type `GameModInfo`.
- **Enabled mods** [verified 2026-11-17 in the IL]: `PluginManager.instance.GetPluginsInfo()` lists `PluginManager.PluginInfo`s with `isEnabled`, `isBuiltin`, `name` (the mod folder's name), `modPath`, `publishedFileID` (a `PublishedFileId`; `AsUInt64` is the workshop item id, and `PublishedFileId.invalid` marks local and built-in mods) and `userModInstance` (the mod's `IUserMod`, whose `Name` is what Content Manager shows).
- **Harmony patching**, if needed, goes through `CitiesHarmony.API` (NuGet, MIT): call `HarmonyHelper.DoOnHarmonyReady`, and keep Harmony types out of the `IUserMod` class. [community: [CitiesHarmony](https://github.com/boformer/CitiesHarmony)]
- **Paths in-game:** `ColossalFramework.IO.DataLocation` has `localApplicationData`, `addonsPath`, `modsPath`, `saveLocation`, `gameContentPath` and `executableDirectory`. [verified: the property getters exist]
  - On Linux, `localApplicationData` is `$XDG_DATA_HOME`, or else .NET's local application data folder (`~/.local/share`), plus `Colossal Order/Cities_Skylines`. The getter creates the folder if it is missing and makes no Unity calls. [verified 2026-11-17 in the IL]
- **Game log:**
  - Linux: `~/.config/unity3d/Colossal Order/Cities_ Skylines/Player.log`. The folder name has an underscore where the product name has a colon, because Unity replaces the `:`. [verified 2026-11-16 on the native Linux build: the log is there and starts with the game's Mono loading lines.] The previously recorded `.../Cities: Skylines/Player.log` (from [CSM developer resources](https://github.com/CitiesSkylinesMultiplayer/CSM/wiki/Developer-Resources)) does not exist.
  - Windows: `Cities_Data/output_log.txt` [inferred from Unity 5.x conventions; verify].
- **Native Linux stability:** some reports say mods are less stable on the native Linux build than under Proton. These are anecdotal. [community: ProtonDB]
- **Runtime assemblies:** `Cities_Data/Managed/` has `mscorlib` 2.0.0.0, `System` 2.0.0.0 and `System.Core` 3.5.0.0, matching the .NET 3.5 reference assemblies that mods compile against. [verified 2026-11-17 with monodis]
- **Floating-point text on Mono is unreliable.** The round-trip format `R` can produce text that a correctly rounding parser reads as the neighbouring value. [verified 2026-11-17 on Mono 6.8.0, running the net35 build of CsToBeamNG.Dump]
  - 11 of 20,000 random doubles were written with 15 significant digits where 17 are needed, such as `-5.08788337932957E-20`, each 1 ulp off.
  - Mono's own `double.Parse` reads that text back as the original value, so formatting with `R` and checking by parsing back on the same runtime passes.
  - Negative zero is written as `0`.
  - The game runs Unity 5.6's older Mono 2.x-based runtime, which is [inferred] no better. The Dump library therefore computes the text of floating-point numbers itself ([dump-format.md, JSON files](../dump-format.md#json-files)).

## Starting the game without the launcher

[verified 2026-11-17 on the native Linux build, Steam build 22724702, unless tagged otherwise]

- **Steam's launch configuration** for app 255710 (the `config/launch` entries in Steam's app info cache, `appcache/appinfo.vdf`) starts `dowser` on Linux and `dowser.exe` on Windows: the Paradox launcher, which waits for the user to click Play. A Windows-only entry for the beta branch `baseline` starts `Cities.exe`. `launcher-settings.json` in the install folder tells the launcher to run `Cities.x64` on Linux.
- **Steam check.** The game's native plugin (`Cities_Data/Plugins/x86_64/libColossalNative.so`) imports `SteamAPI_InitSafe` and `SteamAPI_RestartAppIfNecessary`. Started directly, with the environment variable `SteamAppId=255710` (Steam sets it for the games it starts; we also set `SteamGameId`) and the Steam client running, the game starts normally: the log shows the Steam ID and the DLC status, and mods load. This worked twice in a row. Started without the variable, Steam would presumably restart the game through its launch configuration, which opens the Paradox launcher [inferred from the Steamworks documentation; untested]. Without a running Steam client, `PlatformServiceBehaviour.Awake` logs "Platform service is not running" and quits [inferred from the IL].
- **Windows:** the same approach with `Cities.exe` [inferred; untested].
- **Command-line options.** `Starter.Awake` parses `Environment.GetCommandLineArgs()` with ColossalFramework's `OptionSet` (a copy of Mono.Options, which accepts `-`, `--` and `/` prefixes [inferred]). The options [verified in the IL]:

  | Option | Effect (the game's own description) |
  |---|---|
  | `v`, `verboseLog` | verbose log |
  | `w`, `windowed` | windowed mode |
  | `b`, `disableInBG` | don't run in the background |
  | `generateDevConf` | generate a developer configuration file |
  | `disableMods` | disable mods |
  | `screen-fullscreen=`, `screen-height=`, `screen-width=` | Unity's screen settings |
  | `limitfps=<n>` | target frame rate (disables vsync) |
  | `pdx-launcher-session-token=` | the Paradox launcher's session |
  | `continuelastsave` | continue the last save |
  | `noWorkshop` | disable the Steam Workshop |
  | `refreshWorkshop` | refresh the workshop items' details at startup |

- **`continuelastsave`** sets a flag that the main menu reads when it is shown: if there are saves, it calls its Continue 2.5 s later, which loads the newest save (`SaveHelper.GetLatestSaveGame`, by the saves' `timeStamp`). It can't choose a save, so the exporter loads saves itself (below).

## Loading a save from a mod

[verified 2026-11-17 in the IL of Assembly-CSharp and ColossalManaged, Steam build 22724702, and in game by step 0.6's unattended exports of CityPalma and CItyRockville]

- **When a mod starts.** While loading the plugins in `Starter.Awake`, before the intro and the main menu load, the game's `PluginManager` calls a public instance method `OnEnabled()` on every enabled mod's `IUserMod`, found by reflection (`Public | Instance`); it isn't part of the interface. In game, our `OnEnabled` ran about 1 s after the process started. The game also calls it when the user enables the mod in Content Manager.
- **When the main menu is ready.** `LoadingManager.LoadIntro` loads the scenes `IntroScreen` and `IntroScreen2` (each replacing the previous scenes), then `MainMenu` additively, then calls the `m_introLoaded` handlers and sets `m_currentlyLoading` to false. So the menu is ready when the scene `MainMenu` is loaded (`SceneManager.GetSceneByName("MainMenu").isLoaded`) and `LoadingManager`'s `m_currentlyLoading` and `m_applicationQuitting` are false. In game, this was the case about 12 s after the start.
- **Loading a save** the way the main menu's Continue (`MainMenu.ContinueGame`) and the Load panel (`LoadPanel.LoadRoutine`) do:
  1. List the saves with `PackageManager.FilterAssets(UserAssetType.SaveGameMetaData)`, skipping assets that aren't enabled and demo saves (`PackageHelper.IsDemoModeSave`), as the Load panel does. The Load panel lists each save by its asset's `name`, the package's main asset name, which is normally the file name without `.crp` (`CityPalma`, not the city's name "San Palma").
  2. Read the save's metadata with `asset.Instantiate<SaveGameMetaData>()`: `cityName`, `timeStamp`, `mapThemeRef`, and `assetRef`, the asset that holds the city data.
  3. Create a `SimulationMetaData` with `m_CityName` = the city's name and `m_updateMode` = `SimulationManager.UpdateMode.LoadGame` (3). For a save from the Workshop (its package has a valid published file id), set `m_disableAchievements` to `MetaBool.True`. If `mapThemeRef` names a map theme that `PackageManager.FindAssetByName(…, UserAssetType.MapThemeMetaData)` finds, instantiate it as `MapThemeMetaData`, call its `SetSelfRef(asset)`, and set it as `m_MapThemeMetaData`.
  4. Call `LoadingManager.instance.LoadLevel(assetRef, "Game", "InGame", simulationMetaData, false)`. It returns null and does nothing while the game is loading or quitting; otherwise it sets `m_currentlyLoading` at once and loads in a coroutine.
  - The Load panel can also override the map theme and the environment; Continue and our loader keep the save's own. The Load panel also records the save's name as the default for the next save (`SavePanel.lastLoadedName`); our loader doesn't, since it never saves.
  - Before loading, the menu asks whether random disasters should be on if the Natural Disasters DLC is owned and the player hasn't chosen yet (`CheckForDisasterActivation`). Calling `LoadLevel` directly skips the question.
  - Steam Cloud saves have the package path `cloud:<file>.crp`, not a file-system path. [verified in game]
  - Loading took about 50 s for CityPalma and 40 s for CItyRockville on the maintainer's machine. Then the loading extensions' `OnLevelLoaded(LoadGame)` fired, as for a manual load.
- **Quitting:** `LoadingManager.instance.QuitApplication()`, which the main menu's Exit confirmation also calls, unloads the level and quits. It returns null if the game is already quitting. In game the process ended about 10 s later, with the usual shutdown lines in the log.

## Key APIs

Most names below are verified to exist in the game assemblies. Their use and semantics are [inferred] until an exporter step uses them; upgrade the tags then.

- **Networks:** `NetManager.instance.m_nodes` / `m_segments` / `m_lanes`, each with `.m_buffer` [verified: counted by step 0.4; see Instance limits]. `NetLane.m_bezier` [verified].
- **Buildings:** `BuildingManager.instance.m_buildings.m_buffer` [verified: counted by step 0.4].
- **Props:** `PropManager.instance.m_props.m_buffer`. **Trees:** `TreeManager.instance.m_trees.m_buffer` [verified: counted by step 0.4].
- **Terrain and water:** `TerrainManager.instance`, `WaterSimulation` [verified].
- **City metadata:** `SimulationManager.instance.m_metaData` (`SimulationMetaData`) [verified names and types 2026-11-17]:
  - `m_CityName` (the city's name), `m_MapName` (the map the city was started on) and `m_environment` (the climate, such as `Sunny`), all strings;
  - `m_MapThemeMetaData` (`MapThemeMetaData`, not saved with the city): the custom map theme, or null; its `name` is the theme's name, and it has its own `environment`;
  - `m_invertTraffic` (`SimulationMetaData.MetaBool`: `Undefined` 0, `False` 1, `True` 2): `True` means left-hand traffic.
  - [verified in game 2026-11-17, first dump] CityPalma gave `m_CityName` "San Palma", `m_MapName` "Diamond Coast", `m_environment` "Tropical" and no map theme, which match the city. Only right-hand traffic has been seen so far, so what `True` looks like in a left-hand-traffic city is still [inferred].
- **Simulation thread access:** `SimulationManager.instance.AddAction` [verified; see Threads].
- **Tiles:** `GameAreaManager.instance.m_areaGrid` [verified; see Map tiles].
- **Mods list:** `ColossalFramework.Plugins.PluginManager` [verified; see Runtime and modding facts].
- **Game version:** `BuildConfig.applicationVersion` and `applicationVersionFull`, static string properties [verified]: `1.21.1-f9` and `1.21.1-f9-steam-linux` on the maintainer's native Linux install (2026-11-17). Read the properties: the `APPLICATION_VERSION*` fields are constants, which the compiler would copy into a mod at build time.
- **Loading a save:** `LoadingManager.LoadLevel` with a `SaveGameMetaData` and a `SimulationMetaData` [verified; see Loading a save from a mod]. **Quitting:** `LoadingManager.QuitApplication` [verified].
- **Randomness:** `ColossalFramework.Math.Randomizer` [verified class], seeded per instance id. Call it in-game instead of reimplementing it.
