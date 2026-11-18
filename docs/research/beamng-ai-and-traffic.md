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

- **Lanes:** with `autoLanes: false`, `lanesLeft` and `lanesRight` (counted looking along the node order) set the lanes; otherwise the game chooses. With `roadRules.rightHandDrive` (traffic on the left) the lane directions are mirrored. [verified: `map.lua`]
- **Shape:** with `improvedSpline` and the game's default subdivision, the graph follows the decal's subdivided spline: the hello level's road of 10 nodes became 28 graph nodes and 27 links, each node with a radius of half the road width (3.5 m for 7 m), two-way, drivability 1. [verified in game, 2026-11-17, step 0.5]
- **Driving:** on that invisible road the AI kept to the right-hand lane (mean offset −1.9 m from the centre line, lane centre −1.75 m), stayed on the road and held its 15 m/s limit through the curves. [verified in game]

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
- **Protocol:** plain JSON-RPC 2.0 in HTTP POST bodies, answered as `application/json`: `initialize`, `tools/list`, `tools/call`, `ping`. There is no session header, so any HTTP client works, such as `curl -X POST -H 'Content-Type: application/json' -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_levels","arguments":{}}}' http://127.0.0.1:29292/mcp`. [verified: `lua/ge/extensions/mcp/server.lua`, and used throughout step 0.5]
- **Tool modules:** ai, camera, debug, editor, env, fs, input, perf, scene, sim, spawn, system, telemetry, ui, vehicle, world; 102 tools in 0.40.5. The most useful tools are listed in `agents/verification.md`.
- **Behaviour worth knowing** [verified in game, step 0.5]:
  - `load_level` loads the level behind the main menu, which stays on screen until `set_ui_state` with route `play`; `toggle_ui` with `show: false` hides the remaining UI.
  - `screenshot_image` and `get_ai` are asynchronous: call again until the result arrives.
  - `get_logs` returns the log's tail on the first call, then only new lines, but at most 12 KiB per call, cut mid-line. For complete logs read `<user folder>/beamng.log`, which the game writes with a delay of up to minutes.
  - `set_free_camera` rotations: the identity looks along +Y; `{x: 0.7071, w: 0.7071}` looks straight down, and `{z: s, w: c}` with positive s turns the view towards +X. Both are the opposite sign of the right-hand rule.
  - `drive_to` aims at the first node of the road segment nearest to the given position, which isn't always the nearest node.
  - `raycast` with `renderGeometry: false` hits collision geometry; its result has no material name. `get_ground_at_point` measures down from the given point, so start above the ground.
- **More behaviour** [verified in game 2026-11-18, step 1.5, unless tagged otherwise]:
  - `get_ground_at_point` returns `surfaceHeight` 0 where there is no terrain below the point; `raycast` also hits water surfaces.
  - Camera rotations: compute them with the game's own `quatFromDir(direction, up)` through `run_lua` and pass the result to `set_free_camera`; quaternions composed by hand easily roll the view.
  - With `pause_physics`, `set_position` puts a vehicle where asked, and `step_physics` plus `run_lua_vehicle` with `obj:inWater(node)` then shows where water starts. Over a river bed in CItyRockville it put the vehicle on the bed instead. [observed once] `run_lua_vehicle` answers asynchronously, like `get_ai`: call it again for the result.
  - On the first load after the game started, while it cooked terrain textures, the log line `Level loaded in` came only after `set_ui_state` with route `play`; `Level loaded: <file>` came at once. Later loads logged both by themselves. [observed once]
  - `run_lua` with `shutdown(0)` quits the game, but in both sessions the game wrote a crash dump to `<user folder>/temp/crashReports/` during its last shutdown step; once its process and the crash reporter stayed until terminated. SIGTERM to the game's process (`BinLinux/BeamNG.drive.x64`) ends it. A `steam -applaunch` right after a session sometimes didn't start the game; a second one did. [observed twice]
