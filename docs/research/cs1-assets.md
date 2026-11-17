# CS1 assets: prefabs, meshes, textures, placement

## Prefabs

- `BuildingInfo`, `PropInfo`, `TreeInfo` and `NetInfo` describe what instances look like. An instance refers to its prefab by its index in `PrefabCollection<T>` (`m_infoIndex`), which is the prefab's `m_prefabDataIndex`; `PrefabCollection<T>.GetPrefab(index)` is a plain array lookup in the simulation's list of prefabs. [verified 2026-11-17 in the IL of game 1.21.1-f9: `Building.Info`'s getter and setter]
- Buildings, props and trees have a main mesh and material plus a LOD mesh and material (`m_lodMesh` [verified name]). Trees are the exception: their prefabs have no LOD mesh of their own, only combined billboard meshes the game generates (`m_lodMesh1`, `m_lodMesh4`, `m_lodMesh8`, `m_lodMesh16`). [verified 2026-11-17 in the IL]
- Buildings also have:
  - sub-meshes (`m_subMeshes`), shown depending on the building's flags;
  - props (`m_props`);
  - sub-buildings (`m_subBuildings`).
  [verified names]
- Props and trees have variations (`m_variations` [verified name]). The prop and tree tools pick a variation with `GetVariation` when they place an instance, and the renderers draw an instance's `Info` without asking for a variation, so an instance's prefab index is presumably the final variation's. [verified in the IL that the tools call `GetVariation` and `PropInstance.RenderInstance`/`TreeInstance.RenderInstance` don't; the conclusion is inferred]
- Workshop assets come from `.crp` packages that the game loads. The in-game exporter sees them like vanilla prefabs.

**Building prefabs** [verified 2026-11-17 in the IL; the values in step 1.4's dumps of CityPalma and CItyRockville]:

- **Class:** `m_class` (`ItemClass`, a Unity `ScriptableObject`) gives `m_service`, `m_subService`, `m_level` and `m_layer`. The zones' growables have the services `Residential`, `Commercial`, `Industrial` and `Office`. Most buildings are on the default layer (`ItemClass.Layer.Default`, 0x1), which the normal view shows; in the dumps also water pipe junctions (0x2, `WaterPipes`, `WaterJunctionAI`), the outside connections of aircraft and ships (0x10 `AirplanePaths`, 0x20 `ShipPaths`) and wildlife spawn points (0x40 `Markers`, with an 8 × 8-cell lot), which the normal view doesn't show [inferred: the renderer selects by the prefab's Unity layer, `m_prefabDataLayer`, whose mapping to the item class's layer wasn't read]. Power-line poles, water pipe junctions, highway and railway pillars and the outside connections are the buildings that network nodes refer to (`NetNode.m_building`); poles and pillars are on the default layer.
- **Size:** `m_cellWidth` and `m_cellLength` in cells of 8 m. `m_generatedInfo` (`BuildingInfoGen`, a `ScriptableObject` saved with the asset) holds `m_min` and `m_max`, the smallest and largest vertex coordinates of the prefab's meshes, which `CalculateGeneratedInfo` computes (it starts them at ±100,000), and `m_size`: on X and Z twice the larger of −min and max, and the largest Y. `InitializePrefab` copies `m_size` into `BuildingInfo.m_size` and raises its Y to at least `m_maxHeightOffset` (half the larger lot side, clamped to 4–16 m, or a value from the lot's cells); the game uses it for culling and as `m_collisionHeight`.
- **The main mesh's Unity bounds aren't stable:** `BuildingAI.RenderMesh` lowers `m_mesh.bounds.min.y` to −`m_baseHeight` of the building it draws whenever that is lower, and writes the bounds back into the shared mesh. So they depend on which buildings have been drawn; the dump uses `m_generatedInfo`'s extents instead. The LOD mesh (`m_lodMesh`) isn't changed this way; the renderer changes only the combined LOD meshes (`m_lodMeshCombined1/4/8`).
- **Placement:** `m_placementMode` (`BuildingInfo.PlacementMode`: Roadside, Shoreline, OnWater, OnGround, OnSurface, OnTerrain, ShorelineOrGround, PathsideOrGround, Concourse, PitLane), `m_zoningMode` (Straight, CornerLeft, CornerRight, NotZoning), `m_circular`, `m_expandFrontYard`; terrain: `m_flattenTerrain`, `m_flattenFullArea`, `m_fullPavement`, `m_fullGravel`, `m_clipTerrain`; `m_requireHeightMap` (set when the prefab initialises) drapes the mesh over the terrain.
- **Colours:** `m_useColorVariations` and `m_color0` to `m_color3` (not serialised; set when the prefab initialises).
- **Sub-buildings:** `m_subBuildings` holds `SubInfo`s with `m_buildingInfo`, `m_position`, `m_angle` **in degrees** (the game multiplies it by π/180 wherever it uses it) and `m_fixedHeight`. The game creates them as buildings of their own, linked through `m_parentBuilding` and `m_subBuilding`. CityPalma has one (a toll booth's), CItyRockville none.

**Prop prefabs** [verified 2026-11-17 in the IL and the dumps]: `m_minScale`, `m_maxScale`, `m_useColorVariations`, `m_color0`–`m_color3`, `m_createRuining`, `m_effects` (an array of `PropInfo.Effect` with `m_effect`, an `EffectInfo`: a `LightEffect`, or a `MultiEffect` whose `m_effects` hold `SubEffect`s with their own `m_effect`), and the flags the prefab sets when it initialises: `m_isDecal`, `m_isMarker`, `m_requireHeightMap`, `m_requireWaterMap`. `m_generatedInfo` (`PropInfoGen`) holds a centre and a size that `CalculateGeneratedInfo` derives from the vertices (symmetric, like the buildings' size); the dump uses the meshes' own bounds. In CityPalma's 51 prop prefabs, the "High Vegetation" props have meshes about 30 × 30 m; two prefabs (street lights) have lights.

**Tree prefabs** [verified 2026-11-17 in the IL]: `m_minScale`, `m_maxScale`, `m_minBrightness`, `m_maxBrightness`, `m_createRuining`, and `m_defaultColor` (not serialised; set when the prefab initialises).

## Placement

How the game places, turns, scales and colours buildings, props and trees, and how the dump records it ([dump-format.md, Buildings, props and trees](../dump-format.md#buildings-props-and-trees)). [verified 2026-11-17 in the IL of game 1.21.1-f9 (Assembly-CSharp, ColossalManaged, UnityEngine), and in game with step 1.4's exports of CityPalma and CItyRockville, unless tagged otherwise]

**Turning.** Buildings and props are turned with `Quaternion.AngleAxis(angle × 57.29578°, Vector3.down)`, so an object's local +X axis lies towards (cos angle, 0, sin angle) and its local +Z axis towards (−sin angle, 0, cos angle): counter-clockwise seen from above with +X east and +Z north. Trees aren't turned.

**Buildings.**

- A building stands on a lot of `m_width` × `m_length` cells of 8 m centred on `m_position`, turned by `m_angle` (radians). Its front, which faces the road, is the local +Z side: `CalculateSidewalkPosition` puts the sidewalk at the position plus (−sin angle, 0, cos angle) × `length` × 4. In game, the middle of the front edge of 1,101 of CityPalma's 1,179 zoned buildings lies within 0.5 m of a road's edge (median 0.06 mm), and all 117 of CItyRockville's within 0.05 mm; the back edges lie a median 32 m away. 61 of the 78 others face a curved road, which a straight lot can't follow (up to 2.1 m).
- `CalculatePosition(offset)` maps a local offset differently: its X along (cos, 0, sin) but its Z along (sin, 0, −cos), the local −Z axis.
- **The mesh doesn't always stand at the lot's centre.** `CalculateMeshPosition()` moves it by (`length` − `m_cellLength`) × 4 m along (sin angle, 0, −cos angle), the local −Z axis, and the other way if the prefab doesn't expand its front yard (`m_expandFrontYard`). The renderer (`BuildingAI.RefreshInstance`) draws the mesh with TRS(mesh position, rotation, 1) and keeps TRS(position, rotation, 1) for the lot; building props are placed with the mesh's matrix. In game, 1,284 of CityPalma's 2,168 buildings and 435 of CItyRockville's 738 have a moved mesh; the dumped mesh positions match this formula within 0.3 mm.
- **Drawn or not:** `Building.RenderInstance` draws a building only if it is created and neither deleted nor hidden (flags & 0x8000003 = 0x1), and only on the camera layers of its prefab (`m_prefabDataLayer`). Flag 0x4 (`Original`) marks buildings the map editor placed: boulders and rock formations (CityPalma: 864, 149 of them inside its unlocked tiles; CItyRockville: 584).
- **Colour:** the game writes each building's colour into a colour map (`BuildingManager.UpdateColorMap`), which the building shader reads by the building's index. It is what the building's AI returns from `GetColor(id, ref building, current info mode, current sub-mode)`; the normal view is info mode `None` (0) and sub-mode `Default` (0). For that mode the base `BuildingAI.GetColor` returns `GetBaseColor`: `m_color0` to `m_color3` picked by `new Randomizer(id).Int32(4)` if `m_useColorVariations`, else `m_color0`. `CommonBuildingAI` uses an event's colour for buildings with an event, and sets alpha to 1 for buildings that show consumption, aren't burning, aren't an inactive event's and have electricity (or spread it themselves), else 0 [what the shader does with alpha: inferred, lit windows]. Hotels (the district's hotel colour), pit stops (the race team's colour) and race buildings (the event route's colour) override `GetBaseColor`. Calling the virtual `GetColor` covers all of them; it only reads. In game, all 2,055 + 613 buildings with colour variations had the variation `Randomizer(id).Int32(4)` picks and all 113 + 125 others `m_color0` (CityPalma + CItyRockville); alpha was 1 for 2,153 of 2,168 and 736 of 738.
- **Height:** the position of a building the player built lies a median 0.8 cm (CityPalma) and 1.1 cm (CItyRockville) above the ground the game draws ([cs1-save-and-world.md, Terrain](cs1-save-and-world.md#terrain)); 981 of 1,303 and 97 of 154 within 2 cm. Buildings of networks (water pipe junctions, power-line poles, ramp pillars) and of water (intakes, outlets, wind turbines on water) don't sit on the ground.

**Props.**

- Positions are stored quantised (`PropInstance`: `m_posX` and `m_posZ` as shorts, `m_posY` as a ushort, `m_angle` as a ushort). The accessors decode them: in a game, X and Z = short × 0.263671875 m (17,280 m / 65,536), Y = ushort / 64 m, angle = ushort × 2π/65,536; in the asset editor, X and Z use steps of 0.0164794921875 m instead. Mods such as Extended Managers replace the accessors to keep more precision [community: [EML EPropInstance.cs](https://github.com/Quistar-LAB/EManagersLib/blob/master/EPropInstance.cs)], so the exporter reads positions through `Position` and `Angle`. In game, all 10,708 + 11,085 props lay on that grid.
- `PropInstance.RenderInstance` skips props that are hidden or blocked (flags & 0x44). It seeds `new Randomizer(propID)` (the `int` overload), draws the scale = `m_minScale` + `Int32(10000)` × (`m_maxScale` − `m_minScale`) × 0.0001, then the colour with `PropInfo.GetColor(ref randomizer)`, which picks `m_color0` to `m_color3` with `Int32(4)` whether or not `m_useColorVariations` is set. It draws the mesh with TRS(position, rotation, (scale, scale, scale)). Floating props (`m_requireWaterMap`) are drawn at `SampleRawHeightWithWater` instead of their own height. In game, every prop's dumped scale and colour equal these formulas with the randomizer below.

**Trees.**

- Positions are stored like props' (`TreeInstance`), without an angle; `TreeInstance.Position` decodes them the same way. In game, all 183,346 + 228,970 trees lay on that grid.
- `TreeInstance.RenderInstance` skips trees whose grow state ((flags & 0xF00) >> 8) is 0 or that are hidden (0x4). It seeds `new Randomizer(treeID)` (the `uint` overload) and draws the scale and then the brightness with the same formula as the props' scale, using `m_minBrightness` and `m_maxBrightness` for the second. It draws the mesh with TRS(position, identity, (scale, scale, scale)), tinted with `m_defaultColor` × brightness (whose alpha it then replaces with the wind speed). In game, every tree's dumped scale and brightness equal these formulas.

**Heights of props and trees.** After the terrain changes (`AfterTerrainUpdated`), the game puts every created prop and tree without the fixed-height flag (0x20) on `TerrainManager.SampleDetailHeight` at its position, rounded to 1/64 m. In game, all 412,316 trees and 21,793 props of the two cities lay within 1/64 m of the converter's ground (`CityTerrain.SampleGround`); none had a fixed height.

**The randomizer.** `ColossalFramework.Math.Randomizer` is a 64-bit linear congruential generator with the multiplier 6364136223846793005 and the increment 1442695040888963407. The `int` and `uint` constructors set the seed to multiplier × value + increment (the `long` and `ulong` ones use the value as it is); `Int32(range)` returns ((seed >> 32) × range) >> 32 and then advances the seed by one step. [verified in the IL of ColossalManaged; checked against every tree, prop and building of both cities above]

**Reading on the simulation thread.** The exporter reads all of this on the simulation thread, as the game's own simulation code does. `Mesh.bounds` and Unity object names work there (step 0.7's geometry probe read meshes there too), and Unity 5.6's `Object ==` null check is managed code (`m_CachedPtr` against zero).

## Texture maps and channel packing

[community: [ModTools DumpUtil.cs](https://github.com/bloodypenguin/Skylines-ModTools/blob/HEAD/Debugger/Utils/DumpUtil.cs)]

| Map | R | G | B | A |
|---|---|---|---|---|
| `_MainTex` | diffuse R | diffuse G | diffuse B | |
| `_ACIMap` | 1 − alpha | 1 − colour mask | illumination | |
| `_XYSMap` | normal X | normal Y | 1 − specular | |
| `_APRMap` (networks) | 1 − alpha | 1 − pavement | road | |
| `_XYCAMap` | normal X | normal Y (inverted) | 1 − colour mask | 1 − alpha |

- [verified] The property names `_ACIMap`, `_XYSMap` and `_APRMap` exist in Assembly-CSharp.
- Which prefab types use `_XYCAMap` is unconfirmed; trees are likely.
- Some channel-packed textures need splitting plus a 0.45 gamma correction when dumped. [community: [cslmodding dump guide](https://cslmodding.info/dump/)]

## What the maps mean

[community: cslmodding pages for [buildings](https://cslmodding.info/asset/building/), [props](https://cslmodding.info/asset/prop/), [trees](https://cslmodding.info/asset/tree/), [networks](https://cslmodding.info/asset/network/)]

- **Colour mask:** white areas are multiplied by the instance's variation colour.
- **Illumination (buildings):** 0–120 is diffuse self-lighting; 128–255 gives randomly lit windows at night, with 192 neutral.
- **Specular (buildings):** values above about 205 become windows with a fake interior floor.
- **Vertex colour blue:** snow coverage (winter themes).
- **Tree colour map:** leaves black, the rest white. It drives leaf brightness variation. Tree LODs are billboards generated at runtime.
- **Networks:**
  - Black alpha shows the terrain beneath (Basic shader).
  - The pavement and road maps blend in the map theme's pavement and asphalt textures (gravel for rail).
  - The road map is multiplied by the segment colour.

## Reading meshes and textures at runtime

- Textures that aren't CPU-readable can be read by blitting them into a `RenderTexture` and calling `ReadPixels` (ModTools' `MakeReadable`). [community]
- The game converts textures to DXT and downscales them unless texture quality is set to High, so export with the game at High texture quality. [community]
- **Mesh readability:**
  - ModTools only dumps meshes whose `isReadable` is true. [community]
  - Vanilla network meshes are readable: the segment, node and LOD meshes of the probed roads, highways and ramps all had `isReadable` true, and their vertex data could be read [verified 2026-11-17, game 1.21.1-f9, geometry probe; see [cs1-networks.md](cs1-networks.md#readability)]. Buildings, props and trees are still unchecked; step 1.4 read only their bounds.
  - Workshop asset meshes and textures are created at load time and are probably readable; Loading Screen Mod creates textures via `LoadRawTextureData` + `Apply`. [community/inferred]
  - Fallback if needed: read meshes offline from the user's game files with a Unity asset reader (e.g. AssetsTools.NET, MIT). [inferred]

## Colour variation and randomness

- The game seeds `ColossalFramework.Math.Randomizer` with the instance's id for buildings, props and trees, and the renderers derive colours, scales and brightness from it as described under [Placement](#placement). [verified 2026-11-17; earlier community sources: Move It `MoveableBuilding`, [Move It MoveableTree.cs](https://github.com/Quboid/CS-MoveIt/blob/HEAD/MoveIt/Moveable/MoveableTree.cs), [boformer gist](https://gist.github.com/boformer/5f8e9269b02d5b8809da), Move It `MoveableProp`, EML]
- Vanilla trees aren't rotated. Tree Anarchy adds per-tree scale data and rotation. [community; vanilla verified]
- **Special props:**
  - Decals (`m_isDecal`) are drawn onto the terrain; meshes with `m_requireHeightMap` are draped over it.
  - Props with `m_requireWaterMap` float on water [verified: the renderer samples the water surface for them].
  - Props with `m_createRuining` paint "ruined" terrain.
  [community, unless tagged]
- **Building props** [community: [BOB OverlayTranspilers.cs](https://github.com/algernon-A/BOB/blob/HEAD/Code/Patches/OverlayTranspilers.cs)]:
  - The world transform is the building mesh's matrix (TRS(mesh position, rotation), see [Placement](#placement)) applied to the prop's position [verified: `BuildingAI.RefreshInstance` places them with it].
  - Props without `m_fixedHeight` are put on `SampleDetailHeight`; props with it keep their height relative to the building, or their height above `SampleDetailHeight` if the building's mesh needs the height map [verified in `BuildingAI.RefreshInstance`].
  - How the probability roll is seeded is unverified.
- **Sub-buildings and sub-meshes:** sub-buildings are real Building instances in the save [verified]; sub-meshes depend on the building's flags (`m_flagsRequired`, `m_flagsForbidden` and the `Flags2` counterparts) [verified names].
- **Rule:** in the exporter, call the game's own `Randomizer` and methods to get these values instead of reimplementing them. [decision]

## Open questions

- Readability of vanilla building, prop and tree meshes (network meshes are readable).
- Which prefab types use `_XYCAMap`.
- How building-prop probability is seeded.
- What the building colour map's alpha does in the shader (presumably lit windows at night).
- Whether a free prop's or tree's prefab index is always a final variation (the tools resolve variations when placing; mods could store the parent).
