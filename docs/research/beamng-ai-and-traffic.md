# BeamNG.drive: AI navigation, traffic, signals, parking, MCP

## The AI navgraph

[verified: `lua/ge/map.lua` in 0.40.5; [official](https://documentation.beamng.com/modding/levels/level_formats/map/)]

The navgraph is built at level load from:

1. `BeamNGWaypoint` objects (manual waypoints);
2. every `DecalRoad` with `drivability > 0`;
3. optional `map.json` segments.

Overlapping nodes are merged, with the merge radius taken from the road width. T- and X-junctions are resolved automatically, and nodes are merged onto nearby lines. `autoJunction: false` opts a road out of this.

DecalRoad fields the navgraph reads (verified in `map.lua` unless noted otherwise):

| Field | Meaning |
|---|---|
| `nodes` | `[x, y, z, width]` per node |
| `drivability` | Greater than 0 makes the road an AI road; higher values are preferred |
| `oneWay`, `flipDirection` | Direction follows node order unless flipped |
| `lanesLeft`, `lanesRight` | Used when `autoLanes` is false |
| `speedLimit` | Read into the edge. The docs only describe it for `map.json`, in m/s; verify the units for our use. |
| `gatedRoad`, `hiddenInNavi` | Access and visibility flags [official] |
| `improvedSpline`, `startTangent`/`endTangent`, `useSubdivisions` | Shape handling |
| `overObjects` | Needed when there is no terrain underneath (elevated roads) [official/community] |

Edges without `speedLimit` get an estimate from curvature, one-way status and drivability.

## Invisible AI roads

- Official maps mix visible decal roads that carry AI data with invisible ones (`material: road_invisible`) that exist only for the AI. [verified]
  - East Coast USA has 407 AI-drivable decal roads. 102 of them are invisible, grouped under an `ai_roads` SimGroup.
- Community practice is to lay invisible AI decal roads over mesh roads, with `overObjects` set, and about 0.4 m narrower than the surface. [community: [decal road on mesh road](https://www.beamng.com/threads/decal-road-on-top-of-mesh-road-with-notepad-working.63592/), [BeamNG.tech forum](https://forum.beamng.tech/t/i-cant-place-an-ai-road-created-with-decal-road-editor-on-a-road-created-with-mesh-road-editor/164)]
- MeshRoads are not a navgraph source. [official: level_classes/meshroad]

## Traffic signals: `signals.json`

[verified: west_coast_usa; [official](https://documentation.beamng.com/modding/levels/level_formats/signals/)]

- **`controllers`:** light state machines, e.g. green 10 s → yellow 4 s → red 1.5 s, with `type: lightsBasic`.
- **`instances`:** one per signal, with `pos`, `dir` (travel direction), `controllerId`, `sequenceId`, `group` and `name`. They are snapped to navgraph segments at load. West Coast USA has 335.
- **`sequences`:** phases that cycle the controllers, e.g. north–south, then east–west.
- **Visible props:** traffic-light props link to instances through the dynamic field `signalInstance`. [official]

## Parking spots

[verified: west_coast_usa `city.sites.json`; [official](https://documentation.beamng.com/modding/levels/level_formats/sites/)]

- `city.sites.json` loads automatically.
- `parkingSpots` is a list of `{name, pos, rot (quaternion), scl, customFields, ...}`; a typical `scl` is `[2.5, 6, 3]`.
- The traffic system parks cars in these spots.

## `info.json` gameplay keys

[official: level_formats/info, gameplay_data]

- The keys are `supportsTraffic`, `roadRules` (e.g. `rightHandDrive`, `turnOnRed`), `spawnPoints`, `defaultSpawnPointName` and `minimap` (tiles).
- Spawn points also become points of interest on the big map.
- Routes on the minimap and big map come from the navgraph.

## MCP server (BeamNG 0.40+)

[verified in `lua/ge/extensions/mcp/`]

- **Enabling:** launch argument `-enablemcp`, or the setting `enableMcp` (off by default).
- **Endpoint:** Streamable HTTP at `http://127.0.0.1:29292/mcp`. If that port is busy it tries the next ones; `-mcpport <n>` chooses the port. `-mcpremote` binds to all interfaces; don't use it.
- **Tool modules:** ai, camera, debug, editor, env, fs, input, perf, scene, sim, spawn, system, telemetry, ui, vehicle, world. The most useful tools are listed in `agents/verification.md`.
