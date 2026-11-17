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
- Is strictly read-only: it never changes the simulation or the save.
- Is a thin layer over the game: it reads the game into plain data types in its `Core/` folder, where everything that needs no game types lives and is unit-tested without the game (D16).
- Runs an export as one action on the simulation thread, which reads the city and writes the dump while the simulation waits, so the dump is consistent; the main thread only prepares the export and shows its result (D17).

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

All of this lives in one converter module with unit tests.

## Scale strategy

CS1 maps are much larger than typical BeamNG levels: 17.28 km across, against about 4 km for the biggest official BeamNG terrain. Community maps reach about 12 km on 0.40.

- **Area of interest:** by default the built-up area plus a margin. The rest of the map is left out, or at most shown as a low-detail backdrop.
- **Terrain:** one TerrainBlock, with a power-of-two size of up to 8192 samples. The square size is chosen to fit the area; CS1's own terrain detail is 4–16 m, so 2–4 m BeamNG squares lose nothing.
- **Chunking:** generated geometry (roads, proxies) is merged per tile to limit the object count.
- **Instancing:** repeated meshes (trees, props, repeated buildings) become Forest items.
- **LODs and texture budgets:** target a 6 GB GPU and downscale textures.
- **Material slots:** BeamNG 0.40 has 8,192 bindless material slots (a community project reports hitting the limit). Deduplicate materials and use texture atlases to stay under it.

## Roads and AI traffic

- **Driving surfaces:** generated from the network geometry in phase 1. Later (phase 3) they come from CS1's own road meshes, bent exactly as the game bends them.
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
