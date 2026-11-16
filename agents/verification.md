# Verification

A change is done when:

1. it builds without warnings and the tests pass,
2. its behaviour is verified at the right level (below),
3. the docs it affects are updated,
4. the evidence is reported to the user.

## 1. Build and tests

The commands are listed in `agents/AGENTS.md` (Build, run, test). Run the full test suite, not just new tests, and run `dotnet format --verify-no-changes`.

## 2. Cities: Skylines side (needs the user)

Agents cannot operate the CS1 user interface. When a change needs a fresh dump:

1. Build. The exporter deploys itself into the CS1 mods folder.
2. Ask the user for exactly what is needed, as a numbered list. For example:
   > Start Cities: Skylines. In Content Manager → Mods, enable *CS to BeamNG Exporter* (first time only). Load CityPalma. Open Options → CS to BeamNG and click *Export*. Tell me when the message says the export has finished.
3. Afterwards, read the newest dump folder, its `export.log` and the game log (locations in `agents/environment.md`). Look for exceptions and `[CsToBeamNG]` warnings.

If several changes need the game, ask once. If step 0.6 makes unattended exports possible, its tooling replaces this procedure; update this section then.

## 3. BeamNG side (the agent can do this)

BeamNG.drive 0.40 and later have a built-in MCP server.

**Setup** (once per session):

- The user starts BeamNG with the `-enablemcp` argument (Steam → BeamNG.drive → Properties → Launch options) or enables the `enableMcp` setting. The server listens on `http://127.0.0.1:29292/mcp`, or the next free port if that one is busy; `-mcpport <n>` chooses the port.
- The project registers the server as `beamng` in `.mcp.json`; step 0.5 adds this with the user's approval. Claude Code asks the user to approve the server once.

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

1. **Loads cleanly:** `load_level`, then `get_logs`. There should be no errors or warnings that mention our files.
2. **Looks right:** screenshots from defined camera poses, an overview from above and views at ground level. Save the images under `agents/local/scratch/<step>/` when that folder exists, and refer to them in the report.
3. **Geometry is right:** compare `get_ground_at_point` or `raycast` heights with the dump at sample points.
4. **Drivable:** spawn a vehicle on a road and let the AI drive a route (`drive_to`) across the area. Watch for falling through surfaces, getting stuck and bumps at junctions.
5. **AI network sane:** `get_navgraph` counts are plausible, the AI reaches its targets, and traffic doesn't jam at junctions.
6. **Performance:** FPS and VRAM via `get_performance_metrics` and `get_vram_usage`. The maintainer's GPU has 6 GB of VRAM; note any regressions.

Without MCP, ask the user for screenshots and the BeamNG log (`<BeamNG user folder>/beamng.log`).

## 4. Comparing with Cities: Skylines

- Define camera poses once in CS1 world coordinates, then convert them with the converter's coordinate module so both games show the same view.
- Top-down debug renders from `cstobeamng inspect` (step 1.1) are the quickest way to check layout and alignment.
- For appearance, use the save's own snapshot image and the user's CS1 screenshots as references.

## 5. Reporting

Before reporting, review your own changes with `git --no-pager status` and `git --no-pager diff`. These commands are read-only; never stage or commit.

End the task with:

- what changed (files, behaviour);
- how it was verified (commands and results, screenshot paths, numbers such as height errors or navgraph counts);
- known issues and follow-ups;
- a suggested commit message: an imperative subject line of at most 72 characters, with an optional body. The maintainer writes the final message.
