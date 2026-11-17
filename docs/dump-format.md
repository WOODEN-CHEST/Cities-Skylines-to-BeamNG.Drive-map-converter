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

Steps 1.3 and 1.4 add networks, buildings, props and trees.

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
| Sections | per section: `TerrainSection` (names and sizes), `TerrainSectionContent`, `TerrainSectionWriter`, `TerrainSectionReader` (.NET 10); the same for `Water…`; `GridGeometry` | `CsToBeamNG.Dump.Sections` |

Readers report a dump they can't read with a `DumpException` whose `Kind` says why: `NotFound`, `Corrupt`, `Incompatible` or `Incomplete`. The message names the file and, for JSON, the location in it, such as `$.sections[2].version`.

`DumpReader` checks the container and lists every section with its version. Skipping unknown sections, and sections whose major version a loader doesn't read (`DumpVersion.CanRead`), is the job of the converter's section loaders (`WorldLoader` in `src/CsToBeamNG.Converter/Loading/`).

## Changelog

| Version | Date | Change |
|---|---|---|
| Format 0.1 | 2026-11-17 | First version (step 0.3): layout, names, manifest, record files, string tables, JSON conventions. Completed before the first dump (step 0.4): the `counts` keys, the dump folder's location and name, the content of `game.mods`. The `probes` folder for developer probes, outside the format (step 0.7). |
| `terrain` 0.1, `water` 0.1 | 2026-11-17 | First sections (step 1.2), with the [grid](#grids) convention. |
