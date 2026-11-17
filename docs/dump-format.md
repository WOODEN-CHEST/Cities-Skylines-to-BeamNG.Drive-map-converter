# City dump format

The city dump is how the exporter, a Cities: Skylines mod, hands a city to the converter. This document specifies version **0.1** of its container: the folder layout, the manifest, record files, string tables, the JSON conventions and versioning. The sections that carry the city's data (terrain, networks, ...) are specified [at the end](#sections), as the export steps add them.

The reference implementation is `src/CsToBeamNG.Dump/` ([Implementation](#implementation)). Change the code and this document together.

## Layout

```text
<dump folder>/
├── manifest.json        what the dump contains (required)
├── export.log           the exporter's log (diagnostic, not part of the format)
├── probes/              developer probes, only when requested (not part of the format)
└── <section name>/      one folder per section
    └── ...              the section's files, named by its specification
```

- A dump is a folder. The format doesn't depend on the folder's name or location, but the exporter creates `<CS1 user data>/CsToBeamNG/dumps/<city>_<yyyyMMdd-HHmmss>/`:
  - `<CS1 user data>` is the game's user-data folder ([agents/environment.md](../agents/environment.md)).
  - `<city>` is the city's name in lowercase ASCII letters and digits, with a hyphen for each run of other characters, at most 32 characters, or `city` if nothing remains; `<yyyyMMdd-HHmmss>` is the local time the export started. For example, `new-york_20261117-153000`.
  - If that folder exists, `-2`, `-3` and so on are appended.
  - `DumpLocation` in the Dump library implements these rules.
- Only the files listed in the manifest belong to the dump. Readers ignore everything else, such as the log or a leftover `manifest.json.tmp`.
- `probes/` holds developer probes, one folder per probe, written only when requested (`cstobeamng export <save> --probe <name>`; `DumpProbes` in the Dump library). They are research data with their own formats, documented with the code that reads them; the geometry probe is described in [research/cs1-networks.md](research/cs1-networks.md#validation). The name `probes` is therefore not a valid section name.
- A section's files are in the folder named after the section, and may be in subfolders of it.
- A section exists only once it is complete: writers add it to the manifest after its last file is closed. A section whose export failed is simply absent.

## Conventions

### Names

| What | Rule | Examples |
|---|---|---|
| Section name | Lowercase letters and digits, in words joined by single hyphens, starting with a letter; at most 64 characters | `terrain`, `net-nodes` |
| File path in a section | Segments separated by `/`. Each segment has lowercase letters, digits, `_`, `-` and `.`, and starts and ends with a letter, digit or `_` | `nodes.bin`, `lod/0001.bin` |
| Record type | 1 to 40 ASCII letters, digits, `_`, `-` and `.`, starting with a letter | `netNode` |
| Keys of `counts` and `failureCounts` | camelCase: ASCII letters and digits, starting with a lowercase letter; at most 64 characters | `netSegments` |
| JSON properties | camelCase | `recordCount` |

- No section name or path segment may be a Windows device name (`con`, `prn`, `aux`, `nul`, `com1` to `com9`, `lpt1` to `lpt9`), with or without an extension.
- `<section name>/<file path>` has at most 120 characters. This keeps full paths within Windows' 260-character limit, which .NET 3.5 enforces, even below a long user-data folder.
- Lowercase names can't collide on case-insensitive file systems, and match exactly on case-sensitive ones.

### Encoding

- **Text:** UTF-8 without a byte order mark, `\n` line breaks.
- **Binary:** little-endian; floating-point numbers in IEEE 754 format.
- **Numbers in text:** invariant culture, so `.` is the decimal separator and there are no thousands separators.

### Data

- **Game-native values.** Coordinates, units and angles are stored as the game uses them: Unity world space (metres, Y up, left-handed, X and Z horizontal, origin at the map centre), and angles in radians unless a field says otherwise. The converter converts them ([architecture.md](architecture.md#coordinate-systems)).
- **Units.** Every field's specification states its unit.
- **Precision.** Values that are 32-bit floats in the game are stored as 32-bit floats.
- **Game ids** are stored as `u32`, even where the game uses 16-bit ids, because mods such as Extended Managers widen them.
- **Flags** are stored as the game's raw value. The section's specification lists the bits that the converter uses.

## Versioning

There are two kinds of version. Both are written as `major.minor`, two decimal numbers without leading zeros, such as `0.1` or `2.13`.

- The **format version** (`formatVersion` in the manifest) covers this container: the layout, conventions, manifest, record file header and string tables. Adding or changing a section doesn't change it.
- Each **section version** covers one section: its files, record layouts and JSON content.

| Change | New version |
|---|---|
| Add an optional field to the manifest or a JSON file | minor |
| Add an optional file to a section | minor |
| Append a field at the end of a record | minor |
| Add a section | none: the format version stays, and the new section starts at 0.1 |
| Anything else, such as removing, renaming or reordering fields, or changing a type, unit or meaning | major |

Readers:

- read data whose major version equals their own, whatever its minor version;
- for a **newer minor version**, ignore what they don't know: unknown JSON properties, unknown files, and the bytes after the fields they know at the end of each record. Records advance by the record size in the file's header, not by the size of the layout the reader knows;
- for an **older minor version**, treat the fields added since as absent and use their documented defaults. A field appended to a record is present exactly when the record size covers it;
- for **another major format version**, reject the whole dump with an error that names both versions;
- for **another major section version**, skip that section with a warning and convert without it, as if it were missing;
- for an **unknown section**, skip it with an information message.

The major version 0 has no special meaning. Prefer minor changes: a major change makes existing dumps unreadable, and re-exporting needs the user to run the game.

## manifest.json

```json
{
  "formatVersion": "0.1",
  "complete": true,
  "createdUtc": "2026-11-17T09:30:00Z",
  "exporter": {
    "name": "CsToBeamNG.Exporter",
    "version": "0.1.0"
  },
  "game": {
    "version": "1.17.1-f4",
    "mods": [
      {
        "name": "81 Tiles 2",
        "workshopId": "2862121823"
      }
    ]
  },
  "city": {
    "name": "Seaside",
    "environment": "Sunny",
    "leftHandTraffic": false,
    "unlockedTiles": [
      {
        "x": 4,
        "z": 4
      }
    ]
  },
  "counts": {
    "buildings": 567,
    "netNodes": 1234
  },
  "failureCounts": {
    "props": 2
  },
  "warnings": [
    "2 props were skipped: their prefab is missing."
  ],
  "sections": [
    {
      "name": "net-nodes",
      "version": "0.1",
      "recordCount": 1234,
      "files": [
        {
          "path": "nodes.bin",
          "bytes": 59296
        }
      ]
    }
  ]
}
```

The types are string, boolean, integer (a JSON number without a fraction or exponent), object and array. Required fields must be present and not `null`. Optional fields may be missing or `null`, which mean the same.

Writers write the manifest twice: at the start of the export with `complete: false`, and at the end with `true`. Each time, the text goes to `manifest.json.tmp`, which then replaces `manifest.json`, so an interrupted write never leaves a truncated manifest. Writers always write the collections, even when empty, and leave out optional strings that have no value. Readers rely on neither.

### Top level

| Field | Type | Required | Meaning |
|---|---|---|---|
| `formatVersion` | string | yes | The format version, `0.1`. Readers check it before anything else. |
| `complete` | boolean | yes | Whether the export finished: `false` while it runs and if it failed. Readers reject incomplete dumps unless asked to open one for diagnosis. |
| `createdUtc` | string | yes | When the export started, in UTC, as `yyyy-MM-ddTHH:mm:ssZ`. Readers also accept a fraction of a second. |
| `exporter` | object | yes | The program that wrote the dump. |
| `game` | object | yes | The game. |
| `city` | object | yes | The city. |
| `counts` | object | no | The number of objects in the game by kind: integers of 0 or more under camelCase keys ([`counts`](#counts)). |
| `failureCounts` | object | no | The number of objects the exporter skipped because of errors, by kind, with keys like those of `counts`. |
| `warnings` | array of strings | no | Short messages for the user. Details, such as the ids of skipped objects, go to `export.log`. |
| `sections` | array of objects | yes | The sections, in the order they were completed. |

### `exporter`

| Field | Type | Required | Meaning |
|---|---|---|---|
| `name` | string | yes | The program's name, such as `CsToBeamNG.Exporter`. |
| `version` | string | yes | Its version, in any form. |

### `game`

| Field | Type | Required | Meaning |
|---|---|---|---|
| `version` | string | yes | The game's version as the game reports it, such as `1.17.1-f4`. |
| `mods` | array of objects | no | The mods that were enabled, built-in ones included, sorted by name: `name` (string, required: the name Content Manager shows, or the mod's folder name if the mod reports none) and `workshopId` (the Steam Workshop item id as a string of decimal digits, absent for a local or built-in mod). |

### `city`

| Field | Type | Required | Meaning |
|---|---|---|---|
| `name` | string | yes | The city's name. It may be empty. |
| `mapName` | string | no | The map the city was started on. |
| `environment` | string | no | The environment (climate) as the game names it, such as `Sunny`. |
| `mapTheme` | string | no | The name of the custom map theme, if the city uses one. |
| `leftHandTraffic` | boolean | yes | Whether traffic drives on the left. |
| `unlockedTiles` | array of objects | no | The tiles the city has unlocked, each with `x` and `z`, integers from 0 to 8, ordered by z, then x. Absent or empty if the exporter couldn't read them; a warning then says why. |

The values come from the game's `SimulationMetaData` and `GameAreaManager` ([research](research/cs1-save-and-world.md#key-apis)).

The map is a grid of 9 × 9 tiles of 1920 m ([research](research/cs1-save-and-world.md#world-geometry)). Tile (x, z) covers world X from −8640 + 1920·x to −8640 + 1920·(x + 1) metres, and world Z likewise, so the centre tile is (4, 4). The game's own grid of unlocked tiles covers only the central 5 × 5 tiles unless a mod such as 81 Tiles 2 extends it; the exporter converts it to this grid ([research](research/cs1-save-and-world.md#map-tiles)).

### `counts`

The exporter counts the entries in the game's instance buffers whose `Created` flag is set ([research](research/cs1-save-and-world.md#instance-limits-vanilla)). These are the keys it writes:

| Key | Counted |
|---|---|
| `netNodes` | Network nodes: the ends and junctions of roads, paths, tracks, pipes and other networks. |
| `netSegments` | Network segments, which join two nodes. |
| `netLanes` | Network lanes. |
| `buildings` | Buildings, sub-buildings included. |
| `props` | Props placed on their own. Props that belong to buildings or networks aren't instances of their own and aren't counted. |
| `trees` | Trees. |

Readers accept keys they don't know, and missing keys, which mean the count is unknown.

`failureCounts` uses the same keys, plus `netPrefabs` for network prefabs the exporter couldn't describe (prefabs aren't objects in the city, so `counts` has no such key).

### `sections`

| Field | Type | Required | Meaning |
|---|---|---|---|
| `name` | string | yes | The section's name ([Names](#names)), unique in the dump. Its files are in the folder of this name. |
| `version` | string | yes | The section's version. |
| `recordCount` | integer | no | The number of items the section describes, as its specification defines them. For summaries; readers don't rely on it. |
| `files` | array of objects | yes | The section's files: `path` (string, required), relative to the section's folder and unique in the section; and `bytes` (integer, required), the file's size. |

Readers check that every listed file exists and has the listed size, and otherwise reject the dump as damaged.

## Record files

Large arrays of fixed-size records go into record files, conventionally named `*.bin`. A record file is a 64-byte header followed by the records, back to back.

### Header

| Offset | Size | Type | Field | Value |
|---|---|---|---|---|
| 0 | 4 | ASCII | magic | `CSBD` (bytes 43 53 42 44) |
| 4 | 2 | u16 | container version | 1 |
| 6 | 2 | u16 | header size | 64: the offset of the first record |
| 8 | 4 | u32 | flags | 0; no flags are defined |
| 12 | 4 | u32 | record size | the size of each record in bytes, 1 to 1,048,576 |
| 16 | 8 | u64 | record count | the number of records, or `0xFFFFFFFFFFFFFFFF` while the file is being written |
| 24 | 40 | ASCII | record type | the layout's name ([Names](#names)), padded with zero bytes |

The records start at offset 64: record count × record size bytes. Nothing follows them.

The container version changes only with the major format version.

### Writing

Writers first write the header with the "being written" marker as the record count, then the records, then the real count. A file that still has the marker was never finished, because the export failed or was interrupted.

### Reading

Readers reject a record file, with an error that names it, when:

- the magic isn't `CSBD`, or the header size isn't 64 (damaged);
- the container version isn't 1, or a flag is set (unsupported);
- the record count is the "being written" marker (incomplete);
- the record size is outside 1 to 1,048,576, or the record type is invalid or followed by anything but zero bytes (damaged);
- the record type isn't the expected one, or the record size is smaller than the layout's size in the section's first (x.0) version (damaged);
- the file's length isn't 64 + record size × record count (damaged: truncated or with extra bytes).

### Record layouts

The specification of each section gives the layout of each record type as a table:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|

These are the field types:

| Type | Bytes | Meaning |
|---|---|---|
| `u8`, `i8` | 1 | Unsigned and signed integers. |
| `u16`, `i16` | 2 | |
| `u32`, `i32` | 4 | |
| `u64`, `i64` | 8 | |
| `f32` | 4 | IEEE 754 binary32. |
| `f64` | 8 | IEEE 754 binary64. |
| `str` | 4 | A u32 index into the section's string table; 0 means no string. |
| `pad` | any | Reserved bytes: writers write zeros, readers ignore them. |

- Fields are aligned to their size: each starts at a multiple of its size (`str` at a multiple of 4). Insert `pad` where needed.
- The record size is a multiple of the size of its largest field. Pad the end if needed.
- Booleans are `u8` fields (0 or 1) or bits of a flags field.
- A minor version appends fields at the end, keeping these rules. It never moves or changes existing fields.

## String tables

The strings that records refer to, such as prefab names, are stored once per section in a string table, and records hold their index in `str` fields.

A string table is a JSON file, conventionally `strings.json` in the section's folder:

```json
{
  "strings": [
    "",
    "Basic Road",
    "Oak"
  ]
}
```

- Entry 0 is always the empty string, and index 0 means "no string".
- Writers store each other string once, in the order of first use. Readers don't rely on that.
- An index beyond the table makes the record that holds it damaged. Readers skip and count that object, like any damaged object.

## JSON files

These rules apply to every JSON file in a dump: the manifest, string tables and section files.

- **Document:** one object at the top level, UTF-8 without a byte order mark. Readers accept a byte order mark anyway.
- **Strict JSON:** no comments, no trailing commas, no duplicate property names. Readers reject them.
- **Properties:** camelCase names. Readers ignore properties they don't know.
- **Optional properties** may be missing or `null`; both mean absent.
- **Integers** have no fraction or exponent.
- **Floating-point numbers** are written in the shortest form that reads back as the same value, such as `0.1`, `8640.25` or `1E-07`, in the layout of `ToString("R")` on .NET Core 3.0 and later. Values that are 32-bit floats in the game are written as 32-bit floats, so `0.1f` becomes `0.1`, and readers parse them as 32-bit floats. NaN and infinities can't be written. An object with such a value is treated as damaged.
- **Strings** use JSON's escapes for `"`, `\` and control characters, and escape the line and paragraph separators U+2028 and U+2029. All other characters are written as UTF-8. Half of a UTF-16 surrogate pair, which is invalid, becomes U+FFFD.
- **Formatting:** two-space indentation and a final newline. Readers don't depend on the formatting.

The writer computes the text of floating-point numbers itself, with exact integer arithmetic (the algorithm of Burger and Dybvig, 1996). This is because the exporter runs on the game's old Mono runtime, whose own formatting can't be trusted. Mono 6.8 writes some doubles with 15 significant digits that a correct parser reads as the neighbouring double, and Mono's own parser reads them back as the original, so checking the text by parsing it back on the same runtime misses the error. Integer arithmetic gives the same text on every runtime. The tests compare it with .NET 10, whose formatting is correct except for two powers of two (2⁻²⁵ and 2⁻⁹⁵⁸, as of .NET 10.0.12).

## Sections

| Section | Version | Content | Since |
|---|---|---|---|
| [`terrain`](#terrain-01) | 0.1 | Terrain heights: raw and final on the 16 m grid, 4 m detail heights of some tiles | step 1.2 |
| [`water`](#water-01) | 0.1 | Sea level, the water simulation's cells, water sources | step 1.2 |
| [`net-prefabs`](#net-prefabs-01) | 0.1 | The network prefabs that nodes and segments use: class, AI, dimensions, terrain flags, elevation variants, lanes | step 1.3 |
| [`net-nodes`](#net-nodes-01) | 0.1 | Network nodes: position, flags, elevation, segments, building, prefab | step 1.3 |
| [`net-segments`](#net-segments-01) | 0.1 | Network segments: nodes, directions, flags, prefab, the centre curve and the corners the game computes | step 1.3 |
| [`net-lanes`](#net-lanes-01) | 0.1 | The segments' lanes: curve, length, flags, index in the prefab | step 1.3 |

Step 1.4 adds buildings, props and trees.

To add a section:

1. Choose its name ([Names](#names)). Its first version is 0.1.
2. Specify it below, following the template.
3. Define its record layouts and file names once, in `CsToBeamNG.Dump`, where both the exporter's writer and the converter's loader use them: in `Sections/`, a static class with the names and sizes, a content class, a writer (both targets) and a reader (.NET 10), as for [`terrain`](#terrain-01).
4. Add an entry to the [changelog](#changelog).

### Grids

Sections that sample the map on a regular grid describe it with a grid object in their JSON file:

```json
{
  "originX": -8640,
  "originZ": -8640,
  "spacing": 16,
  "resolution": 1081
}
```

| Field | Type | Meaning |
|---|---|---|
| `originX`, `originZ` | number | The world X and Z of point (0, 0), in metres. |
| `spacing` | number | The distance between neighbouring points, in metres, more than 0. |
| `resolution` | integer | The number of points along each side, 2 to 32,768. |

Point (i, j) lies at X = `originX` + `spacing` · i and Z = `originZ` + `spacing` · j, so i grows towards +X (east) and j towards +Z (north). Grid files store one record per point, row by row: point (i, j) is record j · `resolution` + i. Readers take the geometry from the file rather than assuming the game's: a grid whose files don't hold `resolution`² records is damaged.

### Vectors and curves

Record layouts store a position or direction as three consecutive `f32` fields, X, Y and Z (called `x`, `y`, `z` with a prefix below), in the game's coordinates ([Data](#data)). A cubic Bézier curve is twelve consecutive `f32` fields: its control points a, b, c and d, each as X, Y, Z. The curve starts at a, ends at d and is pulled towards b and c, as the game's `ColossalFramework.Math.Bezier3` holds it. In the Dump library these are `DumpVector3` and `DumpBezier3`.

### `terrain` (0.1)

The terrain's heights as the game stores them, written by the exporter since step 1.2 ([research: Terrain](research/cs1-save-and-world.md#terrain)):

- the **raw heights**: the terrain as the map and the landscaping tools shaped it (`TerrainManager.RawHeights`);
- the **final heights**: the raw heights after roads and buildings have levelled the ground (`FinalHeights`). Where a tile has no detail heights, they are the ground the game draws;
- the **detail heights**: the same levelled ground on a 4 m grid, which the game keeps only for some tiles (the unlocked ones) and draws there instead of the final heights.

| File | Content |
|---|---|
| `terrain.json` | the grids and the tiles that have detail heights |
| `raw-heights.bin` | records of type `terrainHeight`: the raw heights, one per point of `grid` |
| `final-heights.bin` | records of type `terrainHeight`: the final heights, one per point of `grid` |
| `detail-heights.bin` | records of type `terrainHeight`: the detail heights of the tiles listed in `detail.tiles`, tile after tile in that order, each `detail.tileResolution`² points row by row |

`recordCount`: not used.

`terrain.json`:

```json
{
  "grid": { "originX": -8640, "originZ": -8640, "spacing": 16, "resolution": 1081 },
  "detail": {
    "grid": { "originX": -8640, "originZ": -8640, "spacing": 4, "resolution": 4321 },
    "tileResolution": 481,
    "tiles": [ { "x": 4, "z": 3 }, { "x": 3, "z": 4 }, { "x": 4, "z": 4 } ]
  }
}
```

| Field | Type | Meaning |
|---|---|---|
| `grid` | [grid](#grids) | Where the raw and final heights lie. The game's: 1081 × 1081 points, 16 m apart, covering the whole map. |
| `detail.grid` | [grid](#grids) | The lattice that all detail heights lie on. It spans the whole map (the game's: 4321 × 4321 points, 4 m apart), but only the listed tiles have heights. |
| `detail.tileResolution` | integer | The number of detail points along each side of a tile, its edges included (the game's: 481). Neighbouring tiles share their edge points, so 9 · (`tileResolution` − 1) + 1 = `detail.grid.resolution`; readers reject other values. |
| `detail.tiles` | array of objects | The map tiles that have detail heights, each with `x` and `z` (0 to 8, as in [`city`](#city)), ordered by z, then x, each listed once. Empty if no tile has them. |

Tile (x, z) holds the detail points i = (`tileResolution` − 1) · x to (`tileResolution` − 1) · (x + 1), and j likewise; its block in `detail-heights.bin` starts at its −X, −Z corner.

Record type `terrainHeight`, 2 bytes:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|
| 0 | u16 | `height` | 1/64 m | The height above the game's zero, 0 to 1023.98 m. |

The game draws, and places objects on, the detail heights where a tile has them and the final heights elsewhere; between points it interpolates bilinearly. On a detail tile's −X or −Z edge next to a tile without detail heights it uses the final heights, and a point on the line between two tiles belongs to the tile on its +X or +Z side. [Research: Terrain](research/cs1-save-and-world.md#terrain) has the details; `CityTerrain.SampleGround` in the converter implements them.

### `water` (0.1)

The water as the game simulates it, written by the exporter since step 1.2 ([research: Water](research/cs1-save-and-world.md#water)): the sea level, one cell per point of the 16 m terrain grid with its water depth, and the water sources.

| File | Content |
|---|---|
| `water.json` | the sea level and the cells' grid |
| `cells.bin` | records of type `waterCell`, one per point of `grid`, row by row |
| `sources.bin` | records of type `waterSource`, ordered by id |

`recordCount`: the number of water sources.

`water.json`:

```json
{
  "seaLevel": 59.999004,
  "grid": { "originX": -8640, "originZ": -8640, "spacing": 16, "resolution": 1081 }
}
```

| Field | Type | Meaning |
|---|---|---|
| `seaLevel` | number (32-bit) | The sea level in metres: the water level the simulation keeps at the map's edges (`WaterSimulation.m_currentSeaLevel`). |
| `grid` | [grid](#grids) | Where the cells lie; the game's is the terrain's 16 m grid. |

Record type `waterCell`, 10 bytes:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|
| 0 | u16 | `ground` | 1/64 m | The bottom the water simulation fills: the terrain's final heights with buildings raised as obstacles and some areas dug out (the game's block heights). It differs from the ground the game draws. |
| 2 | u16 | `depth` | 1/64 m | The water above `ground`; 0 where the cell is dry. The water surface is `ground` + `depth`. |
| 4 | u16 | `pollution` | game units | The water's pollution, as the game stores it. |
| 6 | i16 | `velocityX` | game units | The flow along X, as the game stores it. |
| 8 | i16 | `velocityZ` | game units | The flow along Z, as the game stores it. |

The game draws water about 0.31 m below `ground` + `depth`, smoothed over neighbouring cells, so it shows only where `depth` is larger than that ([research: Water](research/cs1-save-and-world.md#water)).

Record type `waterSource`, 52 bytes:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|
| 0 | u32 | `id` | | The game's id of the source: its index in the game's list, plus 1. |
| 4 | u16 | `type` | | 1: natural (placed with the map editor); 2: a building's (a water pumping station's intake, a sewage outlet); 3: a water treatment plant's. Readers keep other values as unknown. |
| 6 | u16 | `target` | 1/64 m | The water surface height the source works towards: a natural source lets water out until the surface around its outlet reaches it. |
| 8 | f32 | `inputX` | m | Where the source takes water in. |
| 12 | f32 | `inputY` | m | |
| 16 | f32 | `inputZ` | m | |
| 20 | f32 | `outputX` | m | Where the source lets water out. |
| 24 | f32 | `outputY` | m | |
| 28 | f32 | `outputZ` | m | |
| 32 | u32 | `water` | game units | The water the source holds. |
| 36 | u32 | `pollution` | game units | The pollution the source holds. |
| 40 | u32 | `inputRate` | game units | How fast it takes water in. |
| 44 | u32 | `outputRate` | game units | How fast it lets water out. |
| 48 | u32 | `flow` | game units | The water it let out in the simulation's last step. |

Readers skip, with a warning, a source whose position isn't finite.

### Networks

Four sections describe the game's networks: roads, highways, tracks, paths, and also pipes, power lines, transport lines and the routes of ships and aircraft. The exporter writes them since step 1.3 ([research: CS1 networks](research/cs1-networks.md#data-model)). They refer to one another by the game's ids: a node lists its segments, a segment its nodes, a lane its segment, and nodes and segments their prefab by its index in the game's prefab collection. Ids are the objects' indices in the game's buffers, so 0 is never an object and means "none". All four come from one simulation-thread action, so they are consistent; a section whose export failed is absent, and readers then treat references to it as unknown. Objects the exporter couldn't read are missing and counted in `failureCounts`.

#### `net-prefabs` (0.1)

The network prefabs (`NetInfo`) that nodes and segments use.

| File | Content |
|---|---|
| `prefabs.json` | the prefabs, ordered by index |

`recordCount`: the number of prefabs.

```json
{
  "prefabs": [
    {
      "index": 0,
      "name": "Basic Road",
      "class": { "name": "Small Road", "service": "Road", "subService": "None", "level": "Level2", "layer": 1 },
      "ai": { "type": "RoadAI", "highway": false, "bridge": false, "tunnel": false, "underground": false, "overground": false },
      "halfWidth": 8,
      "pavementWidth": 3,
      "segmentLength": 64,
      "surfaceLevel": -0.3,
      "terrain": {
        "createPavement": true, "createGravel": false, "createRuining": false, "flattenTerrain": true,
        "lowerTerrain": false, "clipTerrain": true, "followTerrain": true, "flatJunctions": true
      },
      "variants": { "elevated": "Basic Road Elevated", "bridge": "Basic Road Bridge", "slope": "Basic Road Slope", "tunnel": "Basic Road Tunnel" },
      "lanes": [
        { "position": -7, "width": 2, "verticalOffset": 0, "stopOffset": 0, "speedLimit": 0.25,
          "direction": 3, "finalDirection": 3, "laneType": 2, "vehicleType": 0, "stopType": 1 }
      ]
    }
  ]
}
```

All properties are required, except the members of `variants`.

| Field | Type | Meaning |
|---|---|---|
| `index` | integer | The prefab's index in the game's prefab collection (`m_infoIndex` of nodes and segments), unique in the file. |
| `name` | string | The prefab's name. Workshop prefabs have names such as `123456789.My Road_Data`. |
| `class.name` | string | The name of the prefab's item class, such as `Small Road`, `Medium Road`, `Highway`. |
| `class.service`, `class.subService`, `class.level` | string | The item class's service, sub-service and level, as the game names them (`ItemClass.Service`, `SubService`, `Level`), such as `Road`, `PublicTransportTrain`, `Level2`. Readers keep names they don't know. |
| `class.layer` | integer | The item class's layers, the raw value of `ItemClass.Layer`. Bits: 0x1 default (networks on, above and below the ground), 0x2 water pipes, 0x4 transport lines, 0x8 metro tunnels; the routes of aircraft (0x10), ships (0x20) and others have layers of their own. |
| `ai.type` | string | The full type name of the prefab's AI, such as `RoadAI`, `TrainTrackBridgeAI`, `TransportLineAI`; empty if it has none. |
| `ai.highway`, `ai.bridge`, `ai.tunnel`, `ai.underground`, `ai.overground` | boolean | What the AI's `IsHighway()`, `IsBridge()`, `IsTunnel()`, `IsUnderground()` and `IsOverground()` return. `highway` means highway rules apply. |
| `halfWidth` | number (32-bit) | Half the network's width, in metres: its corners lie this far to each side of the centre line. |
| `pavementWidth` | number (32-bit) | The pavement's width on each side, in metres. |
| `segmentLength` | number (32-bit) | The length of the prefab's segment mesh, in metres. |
| `surfaceLevel` | number (32-bit) | The height of the driving surface relative to the nodes, in metres; −0.3 for roads with pavements. |
| `terrain.*` | boolean | How the network shapes the terrain: `NetInfo.m_createPavement`, `m_createGravel`, `m_createRuining` (paint), `m_flattenTerrain`, `m_lowerTerrain`, `m_clipTerrain`, `m_followTerrain`, `m_flatJunctions`. |
| `variants.elevated`, `.bridge`, `.slope`, `.tunnel` | string | The names of the prefab's elevated, bridge, slope and tunnel versions, from its AI's `m_elevatedInfo`, `m_bridgeInfo`, `m_slopeInfo` and `m_tunnelInfo`; absent where the AI has no such version. |
| `lanes` | array of objects | The prefab's lanes, in the game's order; a lane record's `index` refers to this order. |
| `lanes[].position` | number (32-bit) | The lane's centre across the segment, in metres from the centre line; negative is left, looking from the start node to the end node, except on an inverted segment, which mirrors its lanes. |
| `lanes[].width` | number (32-bit) | The lane's width, in metres. |
| `lanes[].verticalOffset` | number (32-bit) | The lane's height above the corners, in metres; −0.3 for the car lanes of roads with pavements. |
| `lanes[].stopOffset` | number (32-bit) | How far from the lane's centre vehicles stop, in metres. |
| `lanes[].speedLimit` | number (32-bit) | The speed limit in the game's units: 1 is 50 km/h ([research](research/cs1-networks.md#lanes)). |
| `lanes[].direction` | integer | The lane's direction as the prefab defines it, the raw value of `NetInfo.Direction`: 1 forward (from the start node towards the end node), 2 backward, 3 both; higher bits (4, 8: "avoid") are for the path finder. |
| `lanes[].finalDirection` | integer | The direction the game uses: `direction`, reversed for vehicle and parking lanes in cities that drive on the left. On a segment with the inverted flag, forward and backward swap. |
| `lanes[].laneType` | integer | The raw value of `NetInfo.LaneType`. Bits: 0x1 vehicles, 0x2 pedestrians, 0x4 parking, 0x8 public transport (stops), 0x10 cargo vehicles, 0x20 transport vehicles. |
| `lanes[].vehicleType` | integer | The vehicles allowed, the raw value of `VehicleInfo.VehicleType`. Bits: 0x1 cars, 0x2 metros, 0x4 trains, 0x8 ships, 0x10 aircraft, 0x20 bicycles, 0x40 trams, 0x800 monorails, 0x1000 cable cars. |
| `lanes[].stopType` | integer | The vehicles that stop on the lane, a `VehicleInfo.VehicleType` like `vehicleType`. |

Prefabs the game hasn't loaded (a missing asset) aren't listed; nodes and segments may then refer to an index the file lacks, which readers report.

#### `net-nodes` (0.1)

Every node of the networks: ends, bends and junctions of roads, tracks, paths, pipes and the rest (`NetNode`).

| File | Content |
|---|---|
| `nodes.bin` | records of type `netNode`, ordered by id |

`recordCount`: the number of nodes, which equals `counts.netNodes` less `failureCounts.netNodes`.

Record type `netNode`, 68 bytes:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|
| 0 | u32 | `id` | | The node's id. |
| 4 | u32 | `flags` | | `NetNode.m_flags`, raw. Bits used: 0x1 created, 0x10 dead end, 0x20 middle (one segment continues into the next), 0x40 bend, 0x80 junction, 0x400 outside connection, 0x4000 on the ground, 0x80000 underground, 0x100000 transition (with underground: a tunnel portal), 0x200000 level crossing, 0x800000 traffic lights, 0x80000000 traffic lights switched by the player. |
| 8 | u32 | `flags2` | | `NetNode.m_flags2`, raw. |
| 12 | u32 | `prefab` | | The index of the node's prefab in `net-prefabs`. |
| 16 | f32 | `x` | m | The node's position. |
| 20 | f32 | `y` | m | |
| 24 | f32 | `z` | m | |
| 28 | u32 | `building` | | The id of the node's building, such as a pillar or a pylon; 0 for none. |
| 32 | u32 × 8 | `segment0` … `segment7` | | The ids of the connected segments in the game's eight slots; 0 is an empty slot. |
| 64 | u8 | `elevation` | m | The elevation the network tool gave the node: for elevated and underground networks, roughly the height above (or the depth below) the ground at the time. 0 on the ground. |
| 65 | u8 | `heightOffset` | 1/64 m | A height the game adds to the corners at this node ([`net-segments`](#net-segments-01)). |
| 66 | pad 2 | | | |

#### `net-segments` (0.1)

Every segment of the networks (`NetSegment`), with the geometry the game derives for it: the centre curve and the four corners.

| File | Content |
|---|---|
| `segments.bin` | records of type `netSegment`, ordered by id |

`recordCount`: the number of segments, which equals `counts.netSegments` less `failureCounts.netSegments`.

The corners are what the game's `NetSegment.CalculateCorner` returns, with the node's height offset, as the renderer and the lanes use them ([research: Corners](research/cs1-networks.md#corners)): where an edge of the segment's surface meets its node, and the edge's direction there, pointing into the segment. A corner's side is seen from its own node, looking into the segment, so the start's left corner and the end's right corner lie on the same side: the left one, looking from the start node to the end node. At middle nodes and dead ends the corners lie half the prefab's width to each side of the node; at junctions and bends the game moves them back along the edges.

Record type `netSegment`, 212 bytes:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|
| 0 | u32 | `id` | | The segment's id. |
| 4 | u32 | `flags` | | `NetSegment.m_flags`, raw. Bits used: 0x1 created, 0x10 inverted (the lanes are mirrored and their directions reversed). |
| 8 | u32 | `flags2` | | `NetSegment.m_flags2`, raw. |
| 12 | u32 | `prefab` | | The index of the segment's prefab in `net-prefabs`. |
| 16 | u32 | `startNode` | | The id of the start node. |
| 20 | u32 | `endNode` | | The id of the end node. |
| 24 | f32 × 3 | `startDirectionX`, `Y`, `Z` | | The centre line's direction at the start node, pointing into the segment (`m_startDirection`). |
| 36 | f32 × 3 | `endDirectionX`, `Y`, `Z` | | The centre line's direction at the end node, pointing into the segment (`m_endDirection`). |
| 48 | f32 × 3 | `middleX`, `Y`, `Z` | m | The middle of the segment's surface (`m_middlePosition`). |
| 60 | f32 × 12 | `bezier` | m | The centre line from the start node to the end node, as the game computes it: from node to node with `NetSegment.CalculateMiddlePoints`, smooth at middle nodes. |
| 108 | f32 × 6 | `startLeftX`, `Y`, `Z`, `startLeftDirectionX`, `Y`, `Z` | m | The start's left corner and the edge's direction there. |
| 132 | f32 × 6 | `startRight…` | m | The start's right corner and direction. |
| 156 | f32 × 6 | `endLeft…` | m | The end's left corner and direction, seen from the end node. |
| 180 | f32 × 6 | `endRight…` | m | The end's right corner and direction, seen from the end node. |
| 204 | f32 | `averageLength` | m | The game's average length of the segment's lanes (`m_averageLength`). |
| 208 | u8 | `cornerFlags` | | The smoothness the game returned with each corner: 0x1 start left, 0x2 start right, 0x4 end left, 0x8 end right. Set where the corner's node is a middle node. |
| 209 | pad 3 | | | |

#### `net-lanes` (0.1)

Every lane of the segments (`NetLane`). A lane's settings, such as its type, direction and speed limit, are those of lane `index` of its segment's prefab in `net-prefabs`.

| File | Content |
|---|---|
| `lanes.bin` | records of type `netLane`, ordered by id |

`recordCount`: the number of lanes, which equals `counts.netLanes` less `failureCounts.netLanes`.

Record type `netLane`, 64 bytes:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|
| 0 | u32 | `id` | | The lane's id. |
| 4 | u32 | `segment` | | The id of the lane's segment. |
| 8 | u16 | `index` | | The lane's index among its prefab's lanes: its place in the segment's lane list. |
| 10 | u16 | `flags` | | `NetLane.m_flags`, raw. Bits used: 0x1 created, 0x4 set up for left-hand traffic, 0x10, 0x20, 0x40 the lane's arrows: straight on, left, right. |
| 12 | f32 × 12 | `bezier` | m | The lane's curve. It always runs from the segment's start towards its end; the direction of travel comes from the prefab's lane and the segment's flags. |
| 60 | f32 | `length` | m | The lane's length as the game computes it: the length of the curve's control polygon (`m_length`). |

Readers skip, with a warning, nodes, segments and lanes that have a value that isn't finite, or an id that was listed already; they keep objects whose references the other sections lack and report them.

### Template: `<name>` (0.1)

What the section contains and where in the game it comes from, linking the research notes. Which step writes it.

| File | Content |
|---|---|
| `items.bin` | records of type `item` |
| `strings.json` | the string table |

`recordCount`: what it counts.

Record type `item`, N bytes:

| Offset | Type | Field | Unit | Meaning |
|---|---|---|---|---|

Also: the JSON schemas, the optional fields with their defaults and the version that added them, and how readers treat inconsistent data.

## Diagnostics

`export.log` in the dump folder is the exporter's detailed log. It isn't part of the format: readers ignore it, and its content may change at any time.

It is UTF-8 text with one line per message: the UTC time to the millisecond, the level (`detail`, `info`, `warning` or `error`) and the message. An error is followed by the exception's full text. Every line except details also goes to the game log, prefixed with `[CsToBeamNG]`.

## Implementation

`src/CsToBeamNG.Dump/` implements the format. Everything compiles for .NET 3.5 (the exporter, in the game) and .NET 10 (the converter), except the readers that use System.Text.Json, which exist only in the .NET 10 build.

| Part | Types | Namespace |
|---|---|---|
| Writing a dump | `DumpWriter`, `SectionWriter`; `DumpLocation` (where the exporter puts dumps) | `CsToBeamNG.Dump` |
| Reading a dump (.NET 10) | `DumpReader`, `DumpSection`, `DumpReadOptions`, `DumpNotice` | `CsToBeamNG.Dump` |
| Manifest | `DumpManifest` and its parts, `ManifestWriter`; `ManifestReader` (.NET 10) | `CsToBeamNG.Dump.Manifest` |
| Record files | `RecordFileWriter`, `RecordFileReader`, `RecordFileFormat` | `CsToBeamNG.Dump.Records` |
| String tables | `StringTableBuilder`; `StringTable` (.NET 10) | `CsToBeamNG.Dump` |
| JSON | `JsonWriter` | `CsToBeamNG.Dump.Json` |
| Versions and errors | `DumpFormat`, `DumpVersion`, `DumpException` | `CsToBeamNG.Dump` |
| Sections | per section: `TerrainSection` (names and sizes), `TerrainSectionContent`, `TerrainSectionWriter`, `TerrainSectionReader` (.NET 10); the same for `Water…`, `NetPrefabs…`, `NetNodes…`, `NetSegments…` and `NetLanes…`; records `WaterSourceRecord`, `NetPrefabRecord`, `NetPrefabLane`, `NetNodeRecord`, `NetSegmentRecord`, `NetCorner`, `NetLaneRecord`; `GridGeometry`, `DumpVector3`, `DumpBezier3` | `CsToBeamNG.Dump.Sections` |

Readers report a dump they can't read with a `DumpException` whose `Kind` says why: `NotFound`, `Corrupt`, `Incompatible` or `Incomplete`. The message names the file and, for JSON, the location in it, such as `$.sections[2].version`.

`DumpReader` checks the container and lists every section with its version. Skipping unknown sections, and sections whose major version a loader doesn't read (`DumpVersion.CanRead`), is the job of the converter's section loaders (`WorldLoader` in `src/CsToBeamNG.Converter/Loading/`).

## Changelog

| Version | Date | Change |
|---|---|---|
| Format 0.1 | 2026-11-17 | First version (step 0.3): layout, names, manifest, record files, string tables, JSON conventions. Completed before the first dump (step 0.4): the `counts` keys, the dump folder's location and name, the content of `game.mods`. The `probes` folder for developer probes, outside the format (step 0.7). |
| `terrain` 0.1, `water` 0.1 | 2026-11-17 | First sections (step 1.2), with the [grid](#grids) convention. |
| `net-prefabs` 0.1, `net-nodes` 0.1, `net-segments` 0.1, `net-lanes` 0.1 | 2026-11-17 | The networks (step 1.3), with the [vector and curve](#vectors-and-curves) convention; the `netPrefabs` failure key. |
