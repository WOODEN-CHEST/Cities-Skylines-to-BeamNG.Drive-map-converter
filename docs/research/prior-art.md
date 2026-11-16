# Prior art

Projects related to this one, what they do, and what we can learn from them. Licences matter: only reuse code from permissively licensed projects, and credit them. Unlicensed or non-commercial-licensed projects are for reference only.

Surveyed on 2026-11-16.

## From Cities: Skylines to other formats

| Project | What it does | Licence / status | Relevance |
|---|---|---|---|
| [yousifj129/CS1-to-BeamNG](https://github.com/yousifj129/CS1-to-BeamNG) | In-game C# exporter → JSON → Python → BeamNG. Roads become DecalRoads (subdivided to within 0.5 m of the curve), buildings proxy boxes; terrain is interpolated from road heights. | MIT; 2 commits, Oct 2026. The README says the level was never loaded in BeamNG. | Same architecture idea. No terrain, water or assets. |
| [CSL Map View](https://steamcommunity.com/sharedfiles/filedetails/?id=845665815) | The mod exports `.cslmap`; a Windows viewer draws 2D maps | Repo holds only UI and translations, no licence; last update 2020 | 2D only |
| [Cimtographer](https://github.com/PropaneDragon/Cimtographer) | Exports OSM XML: roads (Béziers flattened to 3 extra points), building rectangles, water contours | No licence; last commit 2016 | Lossy |
| [GeoSkylines](https://github.com/gonzikcz/GeoSkylines) | CSV/WKT export of roads, buildings, zones and trees; also imports | MIT; active in 2024 | Reference for reading the managers |
| [Move It](https://github.com/Quboid/CS-MoveIt) | Exports a selection to XML for re-import | MIT | Reference for instance transforms |
| [ModTools](https://github.com/bloodypenguin/Skylines-ModTools) | Per-object dumps: meshes (OBJ, ASCII FBX), textures (PNG), a single node or segment | MIT; last update 2023 | Texture channel unpacking; the readable-texture trick |
| Network Dump Tools | Bulk network mesh dumps | Source and licence not found | |
| [AD-EYE wiki](https://github-wiki-see.page/m/AD-EYE/AD-EYE_Core/wiki/Extraction-of-3d-models-from-Cities-Skylines-game-and-incorporation-in-PreScan) | Single assets via ModTools/AssetStudio → Blender → PreScan | — | Colour mismatches, broken OBJs, no roads |

## Savegame parsers

None of these decodes the simulation data.

| Project | Notes |
|---|---|
| [crp-extract](https://github.com/LiamBrandt/crp-extract) | Extracts PNG/DDS from `.crp`; leaves other data unsorted |
| [crp-parser](https://github.com/tony56a/crp-parser) | C#, MIT, 2016; scope undocumented |
| [CrpViewer](https://github.com/Auskennfuchs/CrpViewer) | LGPL-3.0; scope undocumented |

## Generating BeamNG levels

| Project | What it does | Licence | Relevance |
|---|---|---|---|
| [magliasinaNG](https://github.com/wintrymichi/magliasinaNG) | Python. Real-world region → `.ter`, scene NDJSON, materials, DAE, invisible AI DecalRoads, forest with imposters, on 0.40. 12.3 km map, 12,792 buildings, about 700k trees; 7.99 GB unpacked, 20 GB peak RAM. | CC BY-NC 4.0 (reference only) | Proves the scale works. Hit the 8,192 bindless material slot limit; reports AI traffic stalling. |
| [World-to-BeamNG](https://github.com/Tachy/World-to-BeamNG) | Python: terrain, DecalRoads, rivers and water blocks, forest using east_coast_usa models (copied from the user's install at build time) | MIT | |
| [Grille.BeamNG.Lib / LevelTemplateCreator](https://github.com/Grille/BeamNG_LevelTemplateCreator) | C#: reads and writes `.ter` v9 and scene JSON | MIT; NuGet `grille.beamng.lib` | Candidate reference or dependency |
| [BeamNG Mapping Tools / LevelCleanUp](https://github.com/alexkleinwaechter/BeamNG_LevelCleanUp) | C#: GeoTIFF + OSM terrain, DecalRoads, OSM buildings, TSStatic → forest | No licence (reference only) | |
| [BeamNGpy tools](https://github.com/BeamNG/BeamNGpy/tree/master/src/beamngpy/tools) | OSM, heightmap and OpenDRIVE import by driving a running BeamNG.tech | MIT | Works at runtime; doesn't generate files |
| [MapNG](https://mapng.com) | Web app: heightmaps, `.ter`, experimental level zips | Unverified | |
| [BeamNGLevelImporter](https://github.com/PrzemekWolw/BeamNGLevelImporter) | Blender add-on that reads BeamNG levels | MIT | Format reference |
