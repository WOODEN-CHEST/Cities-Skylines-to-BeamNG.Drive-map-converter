# Verification

A change is done when:

1. it builds without warnings and the tests pass,
2. its behaviour is verified at the right level (below),
3. the docs it affects are updated,
4. the evidence is reported to the user.

## 1. Build and tests

The commands are listed in `agents/AGENTS.md` (Build, run, test). Run the full test suite, not just new tests, and run `dotnet format --verify-no-changes`.

## 2. Cities: Skylines side

CS1-side changes need a fresh dump from the game. Since step 0.6 the agent can export one itself, but running the game still needs the user's OK, because the game takes over the screen for a minute or two.

**Unattended export (preferred):**

1. Build. The exporter deploys itself into the CS1 mods folder.
2. Ask the user once per session whether you may start Cities: Skylines, possibly several times. Tell them it starts, loads the save, exports and quits by itself, and that they shouldn't click in it. It needs the Steam client running and the exporter mod enabled in Content Manager → Mods (the setting persists).
3. Run `dotnet run --project src/CsToBeamNG.Cli -- export <save>`, e.g. `export CityPalma` (`doctor` lists the saves). Run it in the background; it prints progress and ends with `Dump: <folder>`, or with `error: ...` and exit code 1. `--keep-running` leaves the game open afterwards; `--timeout <minutes>` (default 30) limits the wait, after which the game is stopped.
4. Check the dump as below. The game log is overwritten at every start, so copy it into `agents/local/scratch/<step>/` if you need it later.
5. For research on the game's own behaviour, `--probe <name>` also writes a developer probe into the dump's `probes/` folder (D23 in `docs/architecture.md`). `--probe geometry` captures road meshes and how the game bends them; `cstobeamng dev geometry-probe <dump>` compares the converter's bending with it and exits with 1 if a vertex is more than 1 mm off. `--probe heights` records the game's terrain heights at roads, buildings and test points; `cstobeamng dev height-probe <dump>` compares the converter's terrain with them and exits with 1 if a height is more than 5 mm off.

How it works and what its messages mean: [docs/architecture.md, Unattended export](../docs/architecture.md#unattended-export). If it fails because the game exits without taking the request, the mod is probably disabled; ask the user to enable it.

**Manual export (fallback):** ask the user for exactly what is needed, as a numbered list. For example:

> Start Cities: Skylines. In Content Manager → Mods, enable *CS to BeamNG Exporter* (first time only). Load CityPalma. Open Options, choose *CS to BeamNG Exporter* under Mod Settings, and click *Export city*. Tell me what the message says.

**Afterwards,** read the newest dump folder in `<CS1 user data>/CsToBeamNG/dumps/` (`cstobeamng inspect <dump>` summarises it and reports damaged or skipped sections), its `manifest.json` and `export.log`, and the game log (locations in `agents/environment.md`). Look for exceptions and `[CsToBeamNG]` warnings. If several changes need the game, run it once for all of them.

## 3. BeamNG side (the agent can do this)

BeamNG.drive 0.40 and later have a built-in MCP server.

**Setup** (once per session):

- The user starts BeamNG with the `-enablemcp` argument (Steam → BeamNG.drive → Properties → General → Launch options) or enables the `enableMcp` setting. The server listens on `http://127.0.0.1:29292/mcp`, or the next free port if that one is busy; `-mcpport <n>` chooses the port. It answers only while the game runs.
- `.mcp.json` at the repository root registers the server as `beamng`. Claude Code asks the user to approve it once; the tools then appear in sessions started after the approval, as `mcp__beamng__<tool>`.
- **Without the tools in the session** (another agent, or a session started before the approval), call the server directly: it speaks plain JSON-RPC over HTTP POST, with no session handshake, so `curl` or a few lines of Python suffice (example in [docs/research/beamng-ai-and-traffic.md](../docs/research/beamng-ai-and-traffic.md#mcp-server-beamng-040)). Step 0.5 worked this way; its helper scripts (call a tool, take a screenshot, reload a level and wait for it) are in `agents/local/scratch/0.5/tools/` when that folder exists.
- Install the level first (`cstobeamng dev hello-level`, or the converter's install step), then let the user start the game, so the level list contains it.

**Useful tools** (names as of 0.40.5):

| Purpose | Tools |
|---|---|
| Levels | `list_levels`, `load_level` |
| Camera | `set_free_camera` (pos, rot quaternion, fov), `get_camera_state`, `orbit_camera` |
| Screenshots | `screenshot_image` returns the image (call again if it reports a capture in progress); `screenshot` saves to the user folder |
| Logs and Lua | `get_logs`, `run_lua`, `reload_lua` |
| Ground truth | `get_ground_at_point`, `raycast`, `find_objects`, `get_object` |
| AI | `get_navgraph` (node/link counts, nodes near a point), `spawn`, `set_ai`, `drive_to`, `get_ai` |
| Performance | `get_performance_metrics`, `get_vram_usage`, `get_fps_limit_reason` |
| Time | `get_time_of_day`, `set_time_of_day` |

**Standard checks** for a generated level:

1. **Loads cleanly:** call `get_logs` once to set a baseline, then `load_level`, wait for the log line `Level loaded in`, and call `get_logs` again. It returns at most 12 KiB per call, so for a complete record also read the session from `beamng.log` in the user folder. There should be no errors or warnings that mention our files. Known harmless lines: the UI's "Failed to update minimap draw transform" (no minimap data yet), a missing level-specific licence plate, and `gameplay_achievement` errors.
2. **Looks right:** first `set_ui_state` with route `play` and `toggle_ui` with `show: false`, or the main menu covers the view. Then screenshots from defined camera poses, an overview from above and views at ground level. `set_free_camera` with rotation `{x: 0.7071, y: 0, z: 0, w: 0.7071}` looks straight down; check a pose with `run_lua` and `core_camera.getForward()`. Save the images under `agents/local/scratch/<step>/` when that folder exists, and refer to them in the report.
3. **Geometry is right:** compare `get_ground_at_point` or `raycast` heights with the dump at sample points. Asymmetric test features catch mirrored or transposed data.
4. **Drivable:** spawn a vehicle on a road and let the AI drive a route (`drive_to`) across the area, polling the vehicle's position with `run_lua`. Watch for falling through surfaces, getting stuck and bumps at junctions.
5. **AI network sane:** `get_navgraph` counts are plausible, the AI reaches its targets, and traffic doesn't jam at junctions.
6. **Performance:** FPS and VRAM via `get_performance_metrics` and `get_vram_usage`. The maintainer's GPU has 6 GB of VRAM; note any regressions.

A level can be reinstalled while the game runs; the game reloads changed files, and `load_level` again gives a clean state.

Without MCP, ask the user for screenshots and the BeamNG log (`<BeamNG user folder>/beamng.log`).

## 4. Comparing with Cities: Skylines

- Define camera poses once in CS1 world coordinates, then convert them with the converter's coordinate module so both games show the same view.
- Top-down debug renders are the quickest way to check layout and alignment: `cstobeamng inspect <dump> --render <file.png>` draws the area of interest with +X (east) right and +Z (north, BeamNG's +Y) up, as both games show the map from above. `--area map` draws the whole map, `--area 3-5,4` a block of tiles and `--area minX,minZ,maxX,maxZ` a rectangle in metres; `--size` sets the longer side in pixels. The layers show the terrain (shaded, tinted by height), the water with its sources, the networks by category, with one-way arrows and, from 1 m per pixel, the lines between the game's corners at segment ends, the trees (dots, or discs of their crowns when zoomed in), the building lots coloured by zone with, from 1 m per pixel, their front edges facing the road, and the props (dots, or their turned footprints); the legend in `inspect`'s output names each layer's colours. Save renders under `agents/local/scratch/<step>/` and look at them.
- For appearance, use the save's own snapshot image and the user's CS1 screenshots as references.

## 5. Reporting

Before reporting, review your own changes with `git --no-pager status` and `git --no-pager diff`. These commands are read-only; never stage or commit.

End the task with:

- what changed (files, behaviour);
- how it was verified (commands and results, screenshot paths, numbers such as height errors or navgraph counts);
- known issues and follow-ups;
- a suggested commit message: an imperative subject line of at most 72 characters, with an optional body. The maintainer writes the final message.
