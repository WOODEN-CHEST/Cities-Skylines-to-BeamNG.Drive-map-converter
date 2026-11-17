# BeamNG.drive: level format

Written against BeamNG.drive 0.40.5 (native Linux, Vulkan). Official documentation: https://documentation.beamng.com/modding/levels/

"Verified in game" below means checked on 2026-11-17 with the generated test level of step 0.5 (`cstobeamng dev hello-level`): loaded through the game's MCP server, then probed with ground-height queries, raycasts, object transforms, screenshots and an AI drive.

## Where levels live

- Official levels ship as zips in `<install>/content/levels/`; `template.zip` is a minimal official level. [verified]
- Mods are developed unpacked in `<user folder>/mods/unpacked/<mod>/`, mirroring the game's folder structure, and a level is `levels/<level>/` inside it. Use unique, lowercase names, and never override game files. [verified: `NOTE_FOR_MODDERS.md` shipped with 0.40]
- **Mounting and reloading:** the game mounts each unpacked mod folder at the root of its virtual file system and watches it while running. Changed level files are reloaded (log lines `reloadResource| File changed [...]` and `Importing COLLADA`). [verified in game]
  - While a level folder briefly lacks `main/`, as during a delete-then-write update, the level list logs the error `No entry point for level found: /levels/<level>. Ignoring level.` [verified in game] The installer therefore renames new files into place before it deletes stale ones.
  - The level list is rebuilt when files under `/levels/` change (`core/levels.lua`, `onFilesChanged`). Whether a mod folder created while the game runs is picked up without a restart is untested.
- **Caches:** the game writes nothing into an unpacked mod folder when it loads the level. Compiled meshes go to `<user folder>/temp/levels/<level>/…/*.cdae`, cooked terrain textures to `<user folder>/temp/art/terrainmaterialcache/` (see Terrain materials). [verified in game]
- **Level list:** `core/levels.lua` reads `info.json` for each `/levels/*` folder with a `main/` folder. When `info.json` has no `previews`, the list shows the folder name instead of the `title`. [verified: our level was listed as `cstobeamng_hello`]
- **Referencing other assets:**
  - Assets of other levels can be referenced by absolute virtual path; the template's forest items point to `/levels/jungle_rock_island/art/shapes/...`, and our terrain materials use the template's base textures this way. [verified in game]
  - `.link` files (JSON with `path` and `type`) redirect to shared assets under `/assets/...`. [verified; [official](https://documentation.beamng.com/modding/levels/assets/)]
  - Materials in the global `/art/` and `/assets/` folders are loaded at startup (`lua/ge/client/init.lua`), so a level can use them by name; `road_invisible` is one (`/art/shapes/common/decalroads/main.materials.json`). [verified]

## Files of a level

[verified against `template.zip`; [official](https://documentation.beamng.com/modding/levels/level_formats/)]

| File | Purpose |
|---|---|
| `info.json` | Title, description, previews, size, spawn points, flags (`supportsTraffic`, `supportsTimeOfDay`), `defaultDate`, minimap |
| `main/items.level.json` | Root SimGroup `MissionGroup` |
| `main/MissionGroup/<Group>/.../items.level.json` | Objects, one JSON object per line. A SimGroup named X loads the subfolder X, so the names must match. |
| `<terrain>.ter`, `<terrain>.terrain.json` | Terrain heights and materials; metadata for the editor (optional) |
| `art/terrains/main.materials.json` | Terrain materials and texture set |
| `art/shapes/**/*.dae` + `*.materials.json` | Meshes and their materials (`.cdae` files are the engine's compiled cache) |
| `art/forest/managedItemData.json`, `forest/*.forest4.json` | Forest item types and instances |
| `main.decals.json` | Static decals (optional) |
| `map.json` | Optional manual AI path segments |
| `signals.json`, `*.sites.json` | Traffic signals; sites, including parking spots (see [beamng-ai-and-traffic.md](beamng-ai-and-traffic.md)) |

- **Load order** (`lua/ge/server/server.lua`): every `*materials.json` file anywhere below the level folder, in sorted order; then `*Data.json` files such as `managedItemData.json`; then the scene tree from `main/` (in the engine); then the AI map and `main.decals.json`. [verified: the game's Lua]
- **Missing optional data** only produces notes and warnings: without `<terrain>.ter.depth.png` the log says "all fluids will be disabled as ground type"; without minimap data the UI warns "Failed to update minimap draw transform"; without a level-specific licence plate the game falls back to the default one with a warning. [verified in game]

## Scene objects (`items.level.json`)

[[official](https://documentation.beamng.com/modding/levels/level_formats/items/); verified against the template and in game]

- One object per line, never wrapped in an array. An invalid line is skipped and logged.
- Common keys: `class`, `name`, `persistentId` (UUID), `__parent` (group name), `position` [3], `rotationMatrix` [9], `scale` [3], plus class-specific fields. `persistentId` is optional; the template leaves it out on some objects.
- **Field order:** the editor writes `name`, `class`, `persistentId`, `__parent` and `position`, then the other fields in ordinal order. Our files follow it; the loader doesn't depend on it. [verified]
- **`rotationMatrix`** lists the world directions of the object's local X, Y and Z axes, one after another: the columns of the rotation matrix M in world = M × local. [verified in game: `[0,1,0,-1,0,0,0,0,1]` gave a transform whose columns are (0, 1, 0), (−1, 0, 0) and (0, 0, 1)]
- **A minimal level** has:
  - a `LevelInfo` named `theLevelInfo` (`server.lua` deletes any old one by that name before loading);
  - a `TimeOfDay` named `tod` (`core/sounds.lua` uses `scenetree.tod`) and a `ScatterSky` named `sunsky` (`ge_utils.lua` finds it by name);
  - a `TerrainBlock`;
  - a `SpawnSphere` referenced by `info.json`.

  `CloudLayer` is optional: LevelInfo, TimeOfDay and ScatterSky with the template's values render a normal sky. [official: level_classes/levelinfo, level_classes/skyweather, level_creation/section1; verified in game]
- **Time of day:** `TimeOfDay.time` is a fraction of a day starting at noon: 0 is 12:00, 0.25 is 18:00, 0.5 is midnight. [verified: `core/environment.lua`, `core/celestial.lua`]
- **Spawning:** the player's vehicle spawns at the SpawnSphere named by `info.json` `defaultSpawnPointName`, with the sphere's rotation, and faces the sphere's local −Y axis (vehicles face −Y). [verified in game] Without a default, `spawn.lua` takes a random object from the groups `CameraSpawnPoints`, `PlayerSpawnPoints` or `PlayerDropPoints`. [verified: the game's Lua]

## Terrain

- **`.ter` layout** [verified 2026-11-16 by parsing the template's `theTerrain.ter`; the parse ends exactly at the file length; verified in game with our own files]:
  1. u8 version (9)
  2. u32 size (samples per side)
  3. u16 heights[size × size]
  4. u8 material index[size × size]
  5. u32 material count
  6. material names, each a u8 length followed by ASCII

  All little-endian. The `binaryFormat` string in `.terrain.json` also mentions a `layerTextureMap`, but the actual file has none.
- **Sample layout:** samples are stored row by row from the lower-left (south-west) corner: index = y × size + x, x towards +X, rows towards +Y. Sample (x, y) lies at the TerrainBlock's position + (x, y) × squareSize. [verified in game: an asymmetric hill showed where intended, and the transposed and mirrored positions were flat]
- **Heights:** world z = the TerrainBlock's z + stored × maxHeight / 65536. The largest stored value, 65535, is one step below maxHeight. [verified in game: with maxHeight 100, nine probes matched stored / 65536 to five decimals (59.99908 m for 39321, where / 65535 would give 60.00000), and on a 441-point grid the terrain was within 0.61 mm, under half a step, of the intended surface]
  - Stored heights can't be negative.
  - Material index 255 is a hole: no rendering and no collision. Up to 254 materials. [official]
- **`TerrainBlock` fields:** `terrainFile`, `squareSize` (metres per sample, default 1), `maxHeight`, `materialTextureSet`, and `position` = the lower-left corner. World width ≈ size × squareSize. Object scale is not supported. [official: level_classes/terrainblock; verified in game] Some official levels also set `baseTexSize`, whose meaning we haven't checked.
- **`.terrain.json` is optional:** the game loads the terrain from `terrainFile`; smallgrid ships a `.ter` without one. Ours (the official fields without `heightmapImage`) loads without complaint. [verified]
- **Sizes:**
  - Power of two from 128 to 8192 (the import rule). A BeamNG team member and users confirm that 8192 works. [official/community: https://beamng.com/posts/979171, https://beamng.com/posts/1761369]
  - Multiple TerrainBlocks: reportedly only one gets collision. [community, uncertain]
- **Reference sizes** [verified]:
  - Italy: 4096 samples, about 4.1 km.
  - Johnson Valley: 4096 × 0.75 m, about 3.1 km.
  - West Coast USA, East Coast USA, Utah: 2048.
  - Community: magliasinaNG is 12.3 km (8192 × 1.5 m) on 0.40.
- **Precision** [community: BeamNG developer posts https://beamng.com/goto/post?id=1220773, https://www.beamng.com/goto/post?id=1767510]:
  - Vehicle physics run in local space, so 10 km from the origin is no problem for them.
  - Graphics may jitter far from the origin; the camera was stabilised in 2024.
  - No official maximum extent.

## Terrain materials

[official: level_formats/terrain; verified against the official levels and in game]

- **Definition:** a `TerrainMaterial` per material and one `TerrainMaterialTextureSet` per level, in `art/terrains/main.materials.json`. The `.ter` file's material list names materials by `internalName`; the editor names the object itself `<internalName>-<persistentId>`.
- **Layers:** each material has three texture layers that blend with distance: Base (far), Macro (medium scale) and Detail (close up). Each layer has up to five maps, `baseColor`, `normal`, `roughness`, `ao` and `height`, as `<map><Layer>Tex` fields, with `<map><Layer>TexSize` (metres per repeat; on base and macro maps) and `<map><Layer>Strength` (a pair; on macro and detail maps). `macroDistances` and `detailDistances` set the fade distances. `groundmodelName` sets the physics surface.
- **The base layer gives the colour:** the macro and detail layers only modulate it. Official levels bake their own base maps, which are nearly uniform (the template's grass base map is a flat green 512 × 512 texture; its asphalt one a flat grey). Shared macro textures in the base slot rendered grass grey and asphalt magenta and yellow; the template's base maps, referenced by absolute path, rendered correctly. [verified in game]
- **Shared textures:** `/assets/materials/terrain/<kind>/<family>/t_<family>_{b,nm,r,ao,h}.png`, 1024 × 1024. Plain `.png` references work. [verified in game] The template uses these for its grass detail and macro layers. [verified]
- **Texture set:** `baseTexSize`, `macroTexSize` and `detailTexSize` give the pixel sizes of the three texture arrays, and the textures must have those sizes. Official levels use base 2048 or 4096 (their own maps) and 1024 for macro and detail. [verified] The hello level uses 512, 1024 and 1024. [verified in game]
- **Cooking:** at the first load the game packs the material textures into BC7 DDS files in `<user folder>/temp/art/terrainmaterialcache/` (about 14 s for two materials on this machine); later loads take about 1 s. [verified in game]
- **Textures in general:** PNG, power of two, matching the set's sizes. [official]

## Meshes

[official: [export_dae](https://documentation.beamng.com/modding/levels/export_dae/), level_classes/tsstatic; verified against official meshes and in game]

- **Format:** Collada `.dae`; the engine compiles `.cdae` caches (into the user folder's `temp/`, see above). Shipped content contains no glTF: Italy has 710 `.dae` files, West Coast USA 1305, and `art_shapes` 673. [verified]
- **Node tree** of official meshes [verified: 349 official meshes with a `base00` node]:
  ```text
  base00
    collision-1            (declares the collision detail level; no geometry)
    nulldetail<px>         (optional: nothing is drawn below this size)
    start01
      <mesh>_a<px>         (one node per level of detail, e.g. _a200, _a100, _a40)
      Colmesh-1            (collision mesh; also Colmesh_<name>-1)
  bb__autobillboard<px>    (optional: billboard level of detail)
  ```
  The collision mesh sits under `start01`, next to the visible levels of detail; `collision-1` only declares the level. (An earlier version of this note put `Colmesh-1` under `collision-1`; the official meshes don't.)
- **A minimal mesh the game accepts:** `<asset>` with `Z_UP` and metres; one material, bound through the Collada material's `name`, which equals the Material's `mapTo`; triangles with `VERTEX`, `NORMAL` and `TEXCOORD` inputs at offsets 0, 1 and 2; identity node matrices; the tree above without `nulldetail`. The box of the hello level rendered, cast a shadow and collided (raycasts hit its top and sides with outward normals; the log reported static collision with 8 vertices and 12 triangles). [verified in game]
- **A single level of detail** (`_a100`) stayed visible from 1,080 m up without a `nulldetail` node. [verified in game, one observation]
- **`TSStatic.collisionType`:** `None`, `Bounds`, `Collision Mesh` (the default, and recommended), `Visible Mesh`, `Visible Mesh Final`. Official levels rarely write the field. Too many collidable TSStatics are expensive. [official; verified against East Coast USA]
- **Batching:** multi-draw batching works on Vulkan and D3D12. [official: materials/bindless_rendering]

## Forest (instancing)

[official: level_formats/forest, level_classes/forest; verified against the template]

- **Instances:** `forest/<type>.forest4.json` has one instance per line: `{"ctxid":…, "pos":[x,y,z], "rotationMatrix":[9], "scale":s, "type":"<item>"}`. Scale is uniform.
- **Item types:** `art/forest/managedItemData.json` is a JSON object keyed by item name, with `class: ForestItemData`, `internalName`, `shapeFile` (absolute virtual path), `radius`, wind parameters and `annotation`.
- **Collision:** only collidable item types contribute collision, which is cached per shape.
- **Imposters:** a billboard as the lowest LOD lets items, and whole cells, render as imposters.

## Materials

[official: file_formats/materials, materials/vehicle/typicalmaterials, materials/texture_cooker]

- **Format:** a JSON object keyed by material name, with:
  - `class: "Material"`, `mapTo` = the Collada material name, `version: 1.5`;
  - `Stages` [4], each with `baseColorMap`/`baseColorFactor`, `normalMap`, `roughnessMap`/`roughnessFactor`, `metallicMap`/`metallicFactor`, `ambientOcclusionMap`, `opacityMap`/`opacityFactor`, `emissiveMap`/`emissiveFactor`, `instanceBaseColor` and more; unused stages are empty objects;
  - top-level `translucent`, `alphaTest`, `alphaRef`, `castShadows`, `activeLayers`.
- **Constant factors work without textures:** `baseColorFactor` is `[r, g, b, a]`; `roughnessFactor` and `metallicFactor` are numbers. [verified: 82 of 225 official version 1.5 materials use `baseColorFactor`; in game, the hello level's box showed its colour]
- **Names are global:** all loaded materials share one name space, so generated names need a prefix (the hello level uses the level name). [inferred from the global `/art/` materials]
- **Per-instance colour:** `instanceDiffuse` makes a material colourable per instance. `TSStatic.instanceColor` needs `useInstanceRenderData`. [official]
- **Textures:** author PNGs named `*.color.png` (sRGB), `*.normal.png` and `*.data.png`. The game cooks them into BC7/BC5/BC4 DDS in `<user folder>/temp/`. They must be a power of two and at least 16 px; the default texel density is 512 px/m. [official; verified in `NOTE_FOR_MODDERS.md`]
- **Limit:** 8,192 bindless material slots. A community project hit it ("bindless material slots are full"). [community: magliasinaNG README] The 0.40.5 log reports "65536 texture and 256 cubemap slots" at startup. [verified]

## Water

[official: level_classes/water]

- `WaterPlane`: infinite; only z matters.
- `WaterBlock`: a finite box, split internally when large.
- `River`:
  - a Catmull-Rom spline with nodes `[x,y,z,width,depth,nx,ny,nz]`;
  - width 0.25–1000, depth 0.25–500;
  - flow follows the node order;
  - sloped or twisting rivers reflect less accurately.

## Open questions

- The static decal file format (`main.decals.json`).
- Whether our own generated base maps (flat-colour PNGs) work as well as the template's, for terrain that follows the city (Phase 1).
- What `TerrainBlock.baseTexSize` controls.
- Whether a mod folder created while the game runs appears in the level list without a restart.
