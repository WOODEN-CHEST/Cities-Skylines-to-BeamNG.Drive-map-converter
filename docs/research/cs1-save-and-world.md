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

## World geometry

- The map is 9 × 9 tiles of 1920 m, so 17,280 m square, centred on the origin: −8640 … +8640 on X and Z. [community: [Map Editor wiki](https://skylines.paradoxwikis.com/Map_Editor)]
- Vanilla only allows building inside the central 5 × 5 tiles. How many of those can be bought depends on version and settings; community sources say 9 by default. 81-tile mods (e.g. 81 Tiles 2) unlock the whole 9 × 9 map. [community: EML `EGameAreaManager`]
- Coordinates are Unity world space: metres, Y up, left-handed, with X and Z horizontal.
- The conversion to BeamNG is described in [architecture.md, Coordinate systems](../architecture.md#coordinate-systems).

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

| Type | Limit | Notes |
|---|---|---|
| Buildings | 49,152 | [community; derived from offsets in decompiled code] |
| Net segments | 36,864 | [community; same derivation] |
| Net nodes | 32,768 | [community; unverified] |
| Net lanes | 262,144 | [community; unverified] |
| Props | 65,536 (map editor: 50,000) | ×4 with Extended Managers / Prop Anarchy, using uint ids [community: [EML EPropManager.cs](https://github.com/Quistar-LAB/EManagersLib/blob/master/EPropManager.cs)] |
| Trees | 262,144 (map editor: 250,000) | ×4 with Tree Anarchy [community: [TreeAnarchy TAMod.cs](https://github.com/Quistar-LAB/TreeAnarchy/blob/master/TAMod.cs)] |

Prop positions are stored quantised: x and z as short × 0.263671875 m, y as u16 / 64 m, and the angle as u16 × 2π/65536. EML adds extra precision. Read positions through the game's accessors instead of decoding the fields. [community: [EML EPropInstance.cs](https://github.com/Quistar-LAB/EManagersLib/blob/master/EPropInstance.cs)]

## Runtime and modding facts

- **Game build:** Unity 5.6.7f1. The managed assemblies are in `Cities_Data/Managed/` (Assembly-CSharp, ColossalManaged, ICities, UnityEngine and others), and there is a native Linux build (`Cities.x64`). [verified 2026-11-16]
- **Mods** are .NET Framework 3.5 class libraries in `<user data>/Addons/Mods/<Name>/`; the game also compiles `Source/*.cs` at startup. [community: [Modding API](https://skylines.paradoxwikis.com/Modding_API)]
  - Our mod built with the .NET 10 SDK (C# 14, net35 reference assemblies from NuGet, compiler-generated nullable attributes) loads without errors and is listed in Content Manager → Mods. [verified 2026-11-16, native Linux, Steam build 22724702]
  - The game loads every DLL in a mod folder. The log shows `Loading <path>` and then `Assembly <name>, Version=... loaded.`, both tagged `[Mods - Internal]`. A folder without `Source/` gets `No source files found: <folder>`. [verified 2026-11-16]
  - Every mod assembly, built-in ones such as UnlockAll included, also produces `Non platform assembly: data-0x... (this message is harmless)` and `Fallback handler could not load library .../data-0x....so`. These lines are normal. [verified 2026-11-16]
- **Mod entry points** come from ICities: `IUserMod` and `LoadingExtensionBase` [verified]. The settings UI comes from a public `OnSettingsUI(UIHelperBase)` method that the game finds by reflection, so it is not part of the interface. [community]
- **Harmony patching**, if needed, goes through `CitiesHarmony.API` (NuGet, MIT): call `HarmonyHelper.DoOnHarmonyReady`, and keep Harmony types out of the `IUserMod` class. [community: [CitiesHarmony](https://github.com/boformer/CitiesHarmony)]
- **Paths in-game:** `ColossalFramework.IO.DataLocation` has `localApplicationData`, `addonsPath`, `modsPath`, `saveLocation`, `gameContentPath` and `executableDirectory`. [verified: the property getters exist]
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

## Key APIs

Most names below are verified to exist in the game assemblies. Their use and semantics are [inferred] until an exporter step uses them; upgrade the tags then.

- **Networks:** `NetManager.instance.m_nodes` / `m_segments` / `m_lanes`, each with `.m_buffer`. `NetLane.m_bezier` [verified].
- **Buildings:** `BuildingManager.instance.m_buildings.m_buffer`.
- **Props:** `PropManager.instance.m_props.m_buffer`. **Trees:** `TreeManager.instance.m_trees.m_buffer`.
- **Terrain and water:** `TerrainManager.instance`, `WaterSimulation` [verified].
- **City metadata:** `SimulationManager.instance.m_metaData`, with `m_CityName`, `m_environment`, `m_MapThemeMetaData` and `m_invertTraffic` (left-hand traffic) [verified names].
- **Simulation thread access:** `SimulationManager.instance.AddAction` [verified name].
- **Tiles:** `GameAreaManager.instance` [verified class].
- **Mods list:** `ColossalFramework.Plugins.PluginManager` [verified class].
- **Game version:** `BuildConfig.applicationVersion` [verified names].
- **Loading a save:** `LoadingManager`, `SaveGameMetaData`, `LoadLevel` [verified names].
- **Randomness:** `ColossalFramework.Math.Randomizer` [verified class], seeded per instance id. Call it in-game instead of reimplementing it.
