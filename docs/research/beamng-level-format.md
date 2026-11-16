# BeamNG.drive: level format

Written against BeamNG.drive 0.40.5 (native Linux, Vulkan). Official documentation: https://documentation.beamng.com/modding/levels/

## Where levels live

- Official levels ship as zips in `<install>/content/levels/`; `template.zip` is a minimal official level. [verified]
- Mods are developed unpacked in `<user folder>/mods/unpacked/<mod>/`, mirroring the game's folder structure, and a level is `levels/<level>/` inside it. Use unique, lowercase names, and never override game files. [verified: `NOTE_FOR_MODDERS.md` shipped with 0.40]
- **Referencing other assets:**
  - Assets of other levels can be referenced by absolute virtual path; the template's forest items point to `/levels/jungle_rock_island/art/shapes/...`. [verified]
  - `.link` files (JSON with `path` and `type`) redirect to shared assets under `/assets/...`. [verified; [official](https://documentation.beamng.com/modding/levels/assets/)]

## Files of a level

[verified against `template.zip`; [official](https://documentation.beamng.com/modding/levels/level_formats/)]

| File | Purpose |
|---|---|
| `info.json` | Title, description, previews, size, spawn points, flags (`supportsTraffic`, `supportsTimeOfDay`), `defaultDate`, minimap |
| `main/items.level.json` | Root SimGroup `MissionGroup` |
| `main/MissionGroup/<Group>/.../items.level.json` | Objects, one JSON object per line. A SimGroup named X loads the subfolder X, so the names must match. |
| `<terrain>.ter`, `<terrain>.terrain.json` | Terrain heights and materials; metadata |
| `art/terrains/main.materials.json` | Terrain materials and texture set |
| `art/shapes/**/*.dae` + `*.materials.json` | Meshes and their materials (`.cdae` files are the engine's compiled cache) |
| `art/forest/managedItemData.json`, `forest/*.forest4.json` | Forest item types and instances |
| `main.decals.json` | Static decals |
| `map.json` | Optional manual AI path segments |
| `signals.json`, `*.sites.json` | Traffic signals; sites, including parking spots (see [beamng-ai-and-traffic.md](beamng-ai-and-traffic.md)) |

## Scene objects (`items.level.json`)

[[official](https://documentation.beamng.com/modding/levels/level_formats/items/); verified against the template]

- One object per line, never wrapped in an array. An invalid line is skipped and logged.
- Common keys: `class`, `name`, `persistentId` (UUID), `__parent` (group name), `position` [3], `rotationMatrix` [9], `scale` [3], plus class-specific fields.
- A minimal level has:
  - a `LevelInfo` named `theLevelInfo` (other systems look it up by name);
  - `TimeOfDay`, `ScatterSky` and `CloudLayer`;
  - a `TerrainBlock`;
  - a `SpawnSphere` referenced by `info.json`.
  [official: level_classes/levelinfo, level_classes/skyweather, level_creation/section1]

## Terrain

- **`.ter` layout** [verified 2026-11-16 by parsing the template's `theTerrain.ter`; the parse ends exactly at the file length]:
  1. u8 version (9)
  2. u32 size (samples per side)
  3. u16 heights[size × size]
  4. u8 material index[size × size]
  5. u32 material count
  6. material names, each a u8 length followed by ASCII

  All little-endian. The `binaryFormat` string in `.terrain.json` also mentions a `layerTextureMap`, but the actual file has none.
- **Heights and materials** [[official](https://documentation.beamng.com/modding/levels/level_formats/terrain/)]:
  - Height = value × maxHeight / 65536 + the TerrainBlock's z. Check whether the divisor is 65535 or 65536 with a test level.
  - Stored heights can't be negative.
  - Material index 255 is a hole: no rendering and no collision. Up to 254 materials.
- **`TerrainBlock` fields:** `terrainFile`, `squareSize` (metres per sample, default 1), `maxHeight`, `materialTextureSet`, and `position` = the lower-left corner. World width ≈ size × squareSize. Object scale is not supported. [official: level_classes/terrainblock; verified against the template]
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
- **Terrain materials (PBR):** a `TerrainMaterial` and a `TerrainMaterialTextureSet` in `art/terrains/main.materials.json`, with base, macro and detail texture groups. Textures are PNG, power-of-two, and must match the set's sizes. [official]

## Meshes

[official: [export_dae](https://documentation.beamng.com/modding/levels/export_dae/), level_classes/tsstatic]

- **Format:** Collada `.dae`; the engine compiles `.cdae` caches. Shipped content contains no glTF: Italy has 710 `.dae` files, West Coast USA 1305, and `art_shapes` 673. [verified]
- **Node naming:**
  - The hierarchy is `base00` → `start01` → mesh nodes.
  - The LOD suffix is a letter plus the switch pixel size, e.g. `_a800`.
  - Collision is `Colmesh-1` under `collision-1`.
  - `nulldetail<px>` stops rendering below a size, and `bb__autobillboard<px>` makes a billboard LOD.
- **`TSStatic.collisionType`:** `None`, `Bounds`, `Collision Mesh` (recommended), `Visible Mesh`, `Visible Mesh Final`. Too many collidable TSStatics are expensive. [official]
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
  - `Stages` [4], each with `baseColorMap`/`baseColorFactor`, `normalMap`, `roughnessMap`/`roughnessFactor`, `metallicMap`/`metallicFactor`, `ambientOcclusionMap`, `opacityMap`/`opacityFactor`, `emissiveMap`/`emissiveFactor`, `instanceBaseColor` and more;
  - top-level `translucent`, `alphaTest`, `alphaRef`, `castShadows`, `activeLayers`.
- **Per-instance colour:** `instanceDiffuse` makes a material colourable per instance. `TSStatic.instanceColor` needs `useInstanceRenderData`. [official]
- **Textures:** author PNGs named `*.color.png` (sRGB), `*.normal.png` and `*.data.png`. The game cooks them into BC7/BC5/BC4 DDS in `<user folder>/temp/`. They must be a power of two and at least 16 px; the default texel density is 512 px/m. [official; verified in `NOTE_FOR_MODDERS.md`]
- **Limit:** 8,192 bindless material slots. A community project hit it ("bindless material slots are full"). [community: magliasinaNG README]

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

- Which `.terrain.json` fields BeamNG requires when we write our own.
- The static decal file format (`main.decals.json`).
- Which vanilla terrain and road materials make the best references (step 0.5).
