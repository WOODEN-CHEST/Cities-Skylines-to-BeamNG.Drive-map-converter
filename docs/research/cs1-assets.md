# CS1 assets: prefabs, meshes, textures, placement

## Prefabs

- `BuildingInfo`, `PropInfo`, `TreeInfo` and `NetInfo` describe what instances look like.
- Buildings, props and trees have a main mesh and material plus a LOD mesh and material (`m_lodMesh` [verified name]).
- Buildings also have:
  - sub-meshes (`m_subMeshes`), shown depending on the building's flags;
  - props (`m_props`);
  - sub-buildings (`m_subBuildings`).
  [verified names]
- Props and trees have variations (`m_variations` [verified name]).
- Workshop assets come from `.crp` packages that the game loads. The in-game exporter sees them like vanilla prefabs.

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
  - Vanilla network meshes are readable: the segment, node and LOD meshes of the probed roads, highways and ramps all had `isReadable` true, and their vertex data could be read [verified 2026-11-17, game 1.21.1-f9, geometry probe; see [cs1-networks.md](cs1-networks.md#readability)]. Buildings, props and trees are still unchecked.
  - Workshop asset meshes and textures are created at load time and are probably readable; Loading Screen Mod creates textures via `LoadRawTextureData` + `Apply`. [community/inferred]
  - Fallback if needed: read meshes offline from the user's game files with a Unity asset reader (e.g. AssetsTools.NET, MIT). [inferred]

## Colour variation and randomness

- **Building colour:** if `m_useColorVariations` is set, the game picks one of `m_color0`–`m_color3` with `Randomizer(buildingID).Int32(4)`. [community: Move It `MoveableBuilding`] [verified: both field names exist]
- **Trees** [community: [Move It MoveableTree.cs](https://github.com/Quboid/CS-MoveIt/blob/HEAD/MoveIt/Moveable/MoveableTree.cs), [boformer gist](https://gist.github.com/boformer/5f8e9269b02d5b8809da)]:
  - The randomizer is seeded with the tree's id: `Randomizer(treeID)`.
  - Scale = min + Int32(10000) × (max − min) × 0.0001.
  - Brightness comes from a second draw with the same formula, and the tint is the default colour × brightness.
  - Vanilla trees aren't rotated. Tree Anarchy adds per-tree scale data and rotation.
- **Props:** seeded with `Randomizer(propID)`. Scale is drawn first, then colour (`PropInfo.GetColor`). The transform is TRS(position, AngleAxis(angle°, down), scale). [community: Move It `MoveableProp`, EML]
- **Special props:**
  - Decal props (`m_requireHeightMap` [verified name]) are draped on the terrain by the shader.
  - Props with `m_requireWaterMap` float on water.
  - Props with `m_createRuining` paint "ruined" terrain.
  [community]
- **Building props** [community: [BOB OverlayTranspilers.cs](https://github.com/algernon-A/BOB/blob/HEAD/Code/Patches/OverlayTranspilers.cs)]:
  - The world transform is TRS(building position, −angle about Y).
  - Props shift along z by ±(m_length − cellLength) · 4 for yard expansion, depending on `m_expandFrontYard`.
  - Props with `m_fixedHeight` keep their height relative to the building.
  - How the probability roll is seeded is unverified.
- **Sub-buildings and sub-meshes:** sub-buildings are real Building instances in the save; sub-meshes depend on the building's flags. [community]
- **Rule:** in the exporter, call the game's own `Randomizer` and methods to get these values instead of reimplementing them. [decision]

## Open questions

- Readability of vanilla building, prop and tree meshes (network meshes are readable).
- Which prefab types use `_XYCAMap`.
- How building-prop probability is seeded.
- The internals of `PropInfo.GetColor` and `GetVariation`. This question goes away if the exporter calls them.
