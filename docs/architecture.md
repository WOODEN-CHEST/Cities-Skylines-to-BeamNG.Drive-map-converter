# Architecture

## Goal and scope

Convert a Cities: Skylines (CS1) city into a BeamNG.drive level that is drivable and as close to the original as practical: terrain, water, the road network with AI traffic, buildings, props and trees.

Non-goals:

- simulating CS1 (citizens, economy);
- converting Cities: Skylines II;
- shipping any game content.

## Pipeline

```text
Cities: Skylines (user's install + savegame + workshop assets)
  │  Exporter (CS1 mod, C#, .NET Framework 3.5, runs inside the game)
  ▼
City dump (versioned folder)
  │  Converter (.NET 10 library + CLI)
  ▼
BeamNG level in the user's BeamNG user folder (mods/unpacked/<mod>/levels/<level>/)
```

### Exporter (CS1 mod)

- Runs inside the game and reads the loaded city from the game's managers and prefabs.
- Uses the game's own code for anything derived (curves, corners, randomised placement, colours) instead of reimplementing it.
- Extracts the meshes and textures of the prefabs the city uses (later phases).
- Is strictly read-only: it never changes the simulation or the save. (For an unattended export it loads a save, as a player would from the main menu; the save itself is never written.)
- Is a thin layer over the game: it reads the game into plain data types in its `Core/` folder, where everything that needs no game types lives and is unit-tested without the game (D16).
- Runs an export as one action on the simulation thread, which reads the city and writes the dump while the simulation waits, so the dump is consistent; the main thread only prepares the export and shows its result (D17).

### Unattended export

`cstobeamng export <save>` exports a save without anyone clicking in the game, so agents and scripts can run the whole pipeline (D21). The tool and the mod talk through two files in `<CS1 user data>/CsToBeamNG/autorun/`:

1. The tool checks that the game isn't running and that Steam is, deletes an old `status.txt`, and writes `request.txt`: a random id, the time, the save's name, and whether to quit afterwards.
2. It starts the game's native executable directly (`Cities.x64` or `Cities.exe`), with the environment variable `SteamAppId=255710` that Steam gives the games it starts. Steam's own launch configuration would open the Paradox launcher, which waits for a click ([research](research/cs1-save-and-world.md#starting-the-game-without-the-launcher)).
3. The mod's `OnEnabled`, which the game calls at startup, renames the request to `request.consumed.txt` before acting on it, so a crash can't make every later start repeat it. A request older than 15 minutes, or of another format version, is reported as failed and otherwise ignored.
4. A component on its own game object (`Autorun/AutorunDriver`) runs `Core/AutorunSequence` every frame: once the main menu has been idle for 2.5 s, it loads the save the way the menu's Continue does, starts the same export as the button when the city is loaded, and finally quits the game through the game's own quit routine, forcing it after 60 s ([research](research/cs1-save-and-world.md#loading-a-save-from-a-mod)).
5. At each step the mod rewrites `status.txt`: `accepted`, `loading-save`, `exporting`, then `succeeded` or `failed`, with a message and the dump's folder. The tool reads it every 0.5 s, ignores statuses of other requests, prints the progress and the dump's folder, and waits for the game to exit.
6. If the game exits early, the tool explains it (a request that was never taken usually means the mod isn't enabled). After its timeout (30 minutes by default) it stops the game it started. Either way it deletes a request the game didn't take, so a later manual start doesn't export.

Both files are UTF-8 lines of `key=value`, including `format=1`. Keys are lowercase ASCII letters, digits and hyphens; a value runs to the end of the line, with `\\`, `\n` and `\r` escaped; blank lines and `#` comments are ignored, and so are unknown keys, so later versions can add some. `request.txt` has `id`, `created` (`yyyy-MM-ddTHH:mm:ssZ`, UTC), `save`, `quit` (`true` or `false`) and, optionally, `probes` (developer probes to run with the export, comma-separated; see D23); `status.txt` has `request-id`, `state`, `message` and `dump`. The types are `AutorunRequest`, `AutorunStatus` and `AutorunFiles` in `CsToBeamNG.Dump` (D22). Files are replaced by renaming a temporary file, and read with sharing that allows the other process to replace them.

### Dump

- A versioned folder: `manifest.json`, binary arrays for large data, JSON for structured data, and image/mesh files for assets.
- Coordinates and units are game-native.
- Specified in [dump-format.md](dump-format.md). The format and each section have their own `major.minor` version, so a breaking change in one section only makes readers skip that section.

Why an intermediate format: exporting requires starting the game and loading the city, which takes minutes. Converting should take seconds, so it can be iterated on: dump once, convert many times.

### Converter stages

1. Load and validate the dump.
2. Build the world model (game-native coordinates).
3. Plan the area: area of interest, terrain grid, chunk grid.
4. Terrain: heights, edits (road and building footprints), materials.
5. Water: sea, lakes, rivers.
6. Networks: drivable surfaces, AI roads, traffic signals, parking.
7. Objects: buildings, props, trees (proxies or converted assets).
8. Assets: meshes to Collada, textures to PNG for BeamNG's texture cooker, materials.
9. Level assembly: scene tree, `info.json`, spawn points, previews.
10. Write, install into the BeamNG user folder, report.

Stages 1–3 exist since step 1.1. `WorldLoader` (`src/CsToBeamNG.Converter/Loading/`) runs one loader per section type and builds the world model, `CityWorld`, which holds the manifest and whatever sections were loaded (D24); `AreaOfInterest.Plan` plans the area (D25). `cstobeamng inspect <dump>` runs these stages, prints what it found and, with `--render`, draws the world from above. An export step that adds a section registers its loader in `SectionLoaderRegistry.CreateDefault` and its debug render layer in `RenderLayers.CreateDefault`. Since step 1.2 the world model holds the terrain (`CityTerrain`, `src/CsToBeamNG.Converter/Terrain/`, D28) and the water (`CityWater`, `Water/`, D29), and since step 1.3 the networks (`CityNetPrefabs`, `CityNetNodes`, `CityNetSegments`, `CityNetLanes`, `Networks/`, D31).

### Output modes

- **Clean:** the layout comes from the city, but only BeamNG's own assets and generated geometry are used. It contains no CS1 or workshop content, so it is the only candidate for sharing (still check the rules; see [legal.md](legal.md)).
- **Full:** uses converted CS1 and workshop assets. Personal use only.

## Coordinate systems

- **CS1 (Unity):** metres, Y up, left-handed. X and Z are horizontal, the origin is the map centre, and the map spans ±8640 m.
- **BeamNG (Torque-based):** metres, Z up, right-handed. X and Y are horizontal.
- **Mapping:** BeamNG (x, y, z) = CS1 (x, z, y). This keeps the map unmirrored when seen from above.
- **Meshes:** apply the same swap to positions and normals and reverse the triangle winding, because Unity treats clockwise triangles as front-facing and Collada counter-clockwise. Tangent handedness flips.
- **Rotations:** CS1 rotates objects about the vertical axis with `AngleAxis(angle, Vector3.down)` (community sources). Our derivation says this equals a rotation of the same angle about BeamNG +Z. [inferred; verify with an asymmetric test object before relying on it] On the BeamNG side, a `rotationMatrix` lists the world directions of the object's local X, Y and Z axes, and spawned vehicles face their spawn point's local −Y axis [verified in game, step 0.5; `RotationMatrix`].
- **Heights:** BeamNG terrain heights can't be negative and are relative to the TerrainBlock's z. CS1 heights are 0–1024 m, so a direct mapping works.

All of this lives in one converter module, `CoordinateMapping` in `src/CsToBeamNG.Converter/Coordinates/`, with unit tests. Until a level confirms it (step 1.9), the module is marked as to verify. It turns CS1 angles into BeamNG rotation matrices with its own sine and cosine, so they are the same on every platform (D26).

## Scale strategy

CS1 maps are much larger than typical BeamNG levels: 17.28 km across, against about 4 km for the biggest official BeamNG terrain. Community maps reach about 12 km on 0.40.

- **Area of interest:** by default the built-up area plus a margin. The rest of the map is left out, or at most shown as a low-detail backdrop. It is a rectangle: the bounds the sections give for their content plus 500 m, clamped to the map; the unlocked tiles while no section gives bounds; or a block of tiles chosen with `--tiles` (D25).
- **Terrain:** one TerrainBlock, with a power-of-two size of up to 8192 samples. The square size is chosen to fit the area; CS1's own terrain detail is 4–16 m, so 2–4 m BeamNG squares lose nothing.
- **Chunking:** generated geometry (roads, proxies) is merged per tile to limit the object count.
- **Instancing:** repeated meshes (trees, props, repeated buildings) become Forest items.
- **LODs and texture budgets:** target a 6 GB GPU and downscale textures.
- **Material slots:** BeamNG 0.40 has 8,192 bindless material slots (a community project reports hitting the limit). Deduplicate materials and use texture atlases to stay under it.

## Roads and AI traffic

- **Driving surfaces:** generated from the network geometry in phase 1. Later (phase 3) they come from CS1's own road meshes, bent exactly as the game bends them ([research](research/cs1-networks.md#bending-network-meshes): the formulas are known and validated against the game; the exporter must supply the renderer's inputs for each piece).
- **AI:** invisible `DecalRoad`s (material `road_invisible`) follow the lanes and carry lane counts, direction and speed limits. BeamNG builds its navgraph from these, which gives AI traffic, GPS routes and the big map.
- **Traffic lights and parking:** traffic lights become `signals.json` entries, and parking lanes become parking spots.

## Robustness

The inputs depend on reverse-engineered game internals and on whatever mods and workshop assets a city uses.

- Validate the dump against its version, and fail with a clear message when it is incompatible.
- Skip, log and count objects that fail; the conversion report lists them.
- Keep game-specific knowledge isolated (in the exporter and in the BeamNG layer of the converter) and documented in [research/](research/README.md).

## Testing strategy

- Unit and golden-file tests in the converter, the dump library and the exporter's game-independent core, with synthetic fixtures.
- Purpose-built CS1 test cities (one road, junction or bridge type each), plus real saves.
- End-to-end checks in BeamNG through its MCP server: load, screenshots, ground heights, AI routes, performance.

## Decision log

| # | Date | Decision | Why |
|---|---|---|---|
| D1 | 2026-11-16 | Export from inside CS1 with a mod instead of parsing `.crp` saves | The save payload is undocumented serializer output and no external parser exists. The game parses its own data (including DLC and mod data) and also provides meshes, textures and placement logic. |
| D2 | 2026-11-16 | Versioned intermediate dump; converter runs outside the game | Fast iteration; game-specific code stays separated. |
| D3 | 2026-11-16 | C# throughout (net35 mod, .NET 10 converter) | CS1 mods must be .NET 3.5 C#. One language allows shared dump code, and it is the maintainer's preference. |
| D4 | 2026-11-16 | Collada (`.dae`) for meshes | The only mesh format in BeamNG's shipped content. |
| D5 | 2026-11-16 | AI through invisible DecalRoads | Official maps build AI routes this way, and the navgraph code reads them. |
| D6 | 2026-11-16 | Textures as PNG with `.color`/`.normal`/`.data` suffixes | BeamNG cooks them into compressed DDS itself. |
| D7 | 2026-11-16 | Clean and full output modes | Shareable output must contain no third-party content. |
| D8 | 2026-11-16 | Deterministic output | Meaningful diffs and golden tests. |
| D9 | 2026-11-16 | Name `CsToBeamNG` (CLI `cstobeamng`), MIT license, CI on GitHub Actions (Ubuntu and Windows) | The maintainer's choices at project setup. The name avoids "CS2" and "Skylines2", which suggest Cities: Skylines II. CI is the only regular Windows check. |
| D10 | 2026-11-16 | System.CommandLine 2.0.x for the CLI | Microsoft's command-line parser (MIT). The 2.0 line is the one serviced alongside .NET 10 LTS. |
| D11 | 2026-11-16 | xUnit.net v3 on Microsoft Testing Platform; `global.json` puts `dotnet test` in MTP mode | xUnit 4 defaults to MTP v2, which the .NET 10 SDK no longer runs through VSTest mode. It also needs fewer packages. |
| D12 | 2026-11-16 | net35 builds keep nullable analysis, with internal polyfills of the flow-analysis attributes (`src/Shared/`) | Disabling nullable for net35 would leave the exporter, which is net35 only, unchecked. |
| D13 | 2026-11-17 | Dump container: `manifest.json` plus one folder per section; binary record files with a 64-byte `CSBD` header whose record count is written last; a JSON string table per section; a `major.minor` version for the format and for each section, readable within a major version | Sections evolve independently, and a breaking change in one only makes readers skip it. The count written last marks interrupted files. Readers advance by the header's record size, so minor versions can append fields. |
| D14 | 2026-11-17 | All format code, including the .NET 10 readers that use System.Text.Json, lives in `CsToBeamNG.Dump`; the converter only interprets the data | The writer, the reader and each section's layout stay in one place, shared by the exporter and the converter, so they can't drift apart. |
| D15 | 2026-11-17 | The JSON writer computes the text of floating-point numbers itself (Burger and Dybvig's algorithm, exact integer arithmetic) instead of using the runtime's `ToString("R")` | On Mono, `R` formatting and parsing agree with each other but not with correct parsers (verified on Mono 6.8), so a write-and-check approach can't catch the error. Integer arithmetic gives the same text on every runtime, and tests compare it with .NET 10. |
| D16 | 2026-11-17 | The exporter is a thin layer that reads the game into plain types; its game-independent code lives in `src/CsToBeamNG.Exporter/Core/`, which `tests/CsToBeamNG.Exporter.Tests` compiles in from source | The exporter can't be loaded without the game's assemblies, and CI has none, so testable code must not depend on them. Compiling the files into the test project avoids a third DLL in the mod folder. |
| D17 | 2026-11-17 | An export reads the city and writes the dump in one action on the simulation thread (`SimulationManager.AddAction`); the main thread prepares it and shows the result | The simulation can't change the data while it is read, the UI stays responsive, and the game runs such actions even while paused. The simulation waits while the dump is written; if that gets long for large sections, they can copy the data in the action and write it on another thread. |
| D18 | 2026-11-17 | BeamNG writers return `OutputFile`s (a virtual path and a content writer); `ModInstaller` installs them into `mods/unpacked/cstobeamng_*`, lists them in `cstobeamng-files.json`, renames each new file into place and only then deletes the files of the previous install that are no longer produced | Only files the tool wrote are ever replaced or deleted, so files the user or the game adds survive; an interruption leaves nothing untracked; the game, which reloads changed mod files while running, never sees a missing `main/` or a half-written file. Writers stay free of file-system code and testable in memory. |
| D19 | 2026-11-17 | `persistentId`s are name-based UUIDs (version 5) of `<level>:<key>` in a fixed namespace (`7f355653-9e13-40ad-9d43-4394bb6f9725`) | Deterministic output (D8) with ids that stay the same for the same object across conversions, and differ between levels. |
| D20 | 2026-11-17 | BeamNG JSON is written with System.Text.Json (`BeamNGJson`), not the dump's `JsonWriter` | The converter runs only on .NET 10, whose float formatting is the same on every platform (D15 concerns Mono), and scene files need one compact object per line. Coordinates are written as floats, as the game stores them. |
| D21 | 2026-11-17 | Unattended export: the tool starts the game's executable directly, with `SteamAppId` set, and talks to the mod through a request file and a status file in the game's user-data folder. The command `cstobeamng export` does this on both OSes, instead of the shell and PowerShell wrapper scripts first planned | Steam's launch configuration only starts the Paradox launcher, which waits for a click, while the game's own Steam check accepts a process that carries Steam's environment variable (verified on Linux). Files need no network or IPC code in the mod and work on both OSes. One C# implementation reuses game discovery and runs in CI tests with a scripted fake game. |
| D22 | 2026-11-17 | The autorun files are `key=value` lines, read and written by `CsToBeamNG.Dump` | The mod must read requests, and .NET 3.5 has no JSON reader; a line format needs a few dozen lines of code. Dump is the only library both the mod and the tool use, so the two sides can't drift apart (as with D14). |
| D23 | 2026-11-17 | Developer probes: research data that the exporter writes on request into the dump's `probes/<name>/` folder, outside the dump format. They are requested with `cstobeamng export <save> --probe <name>` (the autorun request's `probes` key) or a button under Developer tools in the mod's options, and read by `cstobeamng dev` commands. The first is the geometry probe (step 0.7) | Reverse-engineering questions need the game's own output next to our reimplementation, captured unattended. Keeping probes out of the format avoids versioning research data, and one folder per dump keeps a probe with the city state it came from. A failing probe is logged and leaves the dump valid. |
| D24 | 2026-11-17 | Each section type has a loader (`ISectionLoader`), registered in `SectionLoaderRegistry` in dependency order. `WorldLoader` runs them and builds the world model, `CityWorld`: the manifest plus each loaded section's data as an `ISectionData`, found by type. A section no loader knows is skipped with a note, one of another major version with a warning, and a damaged one with an error; none of these stops loading. What loading reports is returned as `LoadMessage`s, not logged | Each export step adds its section without touching the others, and the dump format's reader rules are applied in one place. A newer or damaged section degrades the conversion instead of stopping it. Returned messages keep loading testable, and `inspect` and the converter can each present them; the logging package comes with the first stage that writes a log. |
| D25 | 2026-11-17 | The area of interest is a rectangle: the union of the bounds the sections give for their content (`ISectionData.AreaOfInterestBounds`) plus a 500 m margin, clamped to the map. Without such bounds, it is the unlocked tiles, and without those, the whole map. `--tiles x,z`, with ranges such as `3-5,4`, overrides it with a block of tiles | Players drive in the built-up area, and each section knows what of it counts as built-up: buildings and roads do, while the terrain and trees cover the whole map and don't. Until the export steps add such sections, the unlocked tiles bound where the city can be. A rectangle fits one square TerrainBlock. |
| D26 | 2026-11-17 | Rotations from CS1 angles use the converter's own sine and cosine (`DeterministicTrig`: Cody and Waite's reduction and Taylor series, in double arithmetic) instead of `Math.Sin` and `Math.Cos` | The platforms' C libraries can differ in the last bit, which would make generated files differ between Windows and Linux (D8). Basic IEEE arithmetic is the same everywhere, and the results match the correctly rounded values in the tested cases. |
| D27 | 2026-11-17 | PNG images are written by our own encoder (`Imaging/PngEncoder`: 8-bit grey, RGB and RGBA, adaptive row filters, CRC-32) on .NET's built-in zlib, without a package | It takes about 200 lines on top of `ZLibStream`, while a package would be one more dependency to vet and track. Decoded pixels are deterministic; the compressed bytes depend on the runtime's zlib. The textures of phase 2 can reuse it. |
| D28 | 2026-11-17 | The `terrain` section stores the game's height arrays as the game keeps them: raw and final heights on the 16 m grid, and the 4 m detail heights of each tile that has them, with the grids described in JSON. The converter reads the ground as the game's `SampleDetailHeight` does (`CityTerrain.SampleGround`) | The game levels the ground under roads and lots on the 4 m grid of the unlocked tiles and draws that; the raw heights miss it by up to 4 m. Unchanged arrays keep the exporter thin, and the heights probe (D23: `export --probe heights`, `dev height-probe`) showed that the converter reproduces the game's ground within 1 mm ([research](research/cs1-save-and-world.md#terrain)). |
| D29 | 2026-11-17 | The `water` section stores the water simulation's cells (depth, pollution, flow) with the ground the simulation fills, the terrain's block heights, plus the sea level and the water sources. The exporter copies the cells under the simulation's own read lock | The simulated water lies on its own ground, in which buildings are obstacles, so its surface is exact only as that ground plus the depth; the visible ground differs. The water simulation runs on its own thread, so the copy is taken the way the game's own samplers read it. |
| D30 | 2026-11-17 | Each section's format code lives in `CsToBeamNG.Dump.Sections`: a static class with its names and sizes, a content class, a writer for both targets and a reader for .NET 10. The converter's loader calls the reader and wraps the content in its world-model type | The layout is defined once (D14) and its writer and reader are round-trip tested together; the readers reuse the dump's JSON field checks and messages; test fixtures write sections with the real writer. |
| D31 | 2026-11-17 | The networks are four sections that refer to one another by the game's ids and prefab indices: `net-prefabs` (JSON) and `net-nodes`, `net-segments`, `net-lanes` (records). Segments carry the centre curve and the four corners computed with the game's own methods (`CalculateMiddlePoints`; `CalculateCorner` with the node's height offset, as the renderer and the lanes use it). Prefabs carry their item class (category names as the game names them, layers as raw bits), what the AI's own getters return (`IsHighway()` and the like), dimensions, terrain flags, the names of their elevation variants and their lanes. Flags are raw values | Separate sections are versioned on their own, and their record counts check against the manifest's counts. Junction surfaces and lanes need exactly the corners the game uses, which depend on neighbours and prefab rules, so the game computes them, as 0.7 recommended for its renderer's inputs; all four corners rebuild the game's 8,638 lanes of CityPalma within 0.5 mm. Everything else stays plain data that the converter can interpret anew without another export: the debug render's road categories, for example, come from the layers, lanes and `IsHighway()`. |
| D32 | 2026-11-17 | The networks bound the area of interest with their nodes inside the unlocked tiles, or all nodes if the tiles are unknown | The map's own highways, tracks and ship and air routes run to the map's edges and would stretch the area over the whole map; the city can only build inside its unlocked tiles. Pipes and power lines lie inside them too, so every kind of node counts. |
