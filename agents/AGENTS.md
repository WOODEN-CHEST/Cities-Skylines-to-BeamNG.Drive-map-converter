# AGENTS.md

The entry point for AI coding agents in this repository. Claude Code loads it automatically through the root `CLAUDE.md`; other agents should read it first. Keep it short and accurate: when you change something it describes, update it in the same change. Details live in the linked files.

## The project

A tool that converts a **Cities: Skylines** city (the 2015 game, "CS1", not Cities: Skylines II) into a **drivable BeamNG.drive level**. It covers terrain, water, the road network with working AI traffic, buildings, props and trees, matching the original as closely as practical.

Constraints behind every decision:

1. **No game assets are ever committed or shipped.** Meshes and textures are extracted from the user's own installation when they convert. BeamNG's own assets are referenced by path, never copied. Converted levels are for personal use only. See [docs/legal.md](../docs/legal.md).
2. **Windows and Linux are both first-class**, for building and for running everything.
3. **The inputs are reverse-engineered and will break sometimes.** Keep game-specific knowledge isolated, validate inputs, degrade gracefully per object, and record what you learn in [docs/research/](../docs/research/README.md).

## How it works

```text
Cities: Skylines (user's install + savegame + workshop assets)
  │  Exporter: a CS1 mod (C#, .NET Framework 3.5) running inside the game.
  │  Reads the loaded city through the game's own code and writes a "city dump".
  │  Never modifies game state.
  ▼
City dump: a versioned folder (JSON manifest, binary arrays, mesh and texture data)
  │  Converter: .NET 10 library + CLI. All interpretation and generation happen
  │  here, so it can be iterated without relaunching the game.
  ▼
BeamNG level: <BeamNG user folder>/mods/unpacked/<mod>/levels/<level>/
```

Rationale, conversion stages and the decision log are in [docs/architecture.md](../docs/architecture.md).

## Repository layout

| Path | Contents | In git |
|---|---|---|
| `src/CsToBeamNG.Exporter/` | CS1 mod (net35); its `Core/` folder holds the game-independent part, which the tests compile in | yes |
| `src/CsToBeamNG.Dump/` | dump format: model, readers, writers; the autorun files that the CLI and the mod exchange (net35 + net10.0) | yes |
| `src/CsToBeamNG.Converter/` | conversion library (net10.0) | yes |
| `src/CsToBeamNG.Cli/` | command-line front end (net10.0), executable `cstobeamng` | yes |
| `src/Shared/` | source files compiled into several projects (net35 polyfills) | yes |
| `tests/` | xUnit test projects | yes |
| `docs/` | architecture, legal notes, research (reverse-engineering knowledge) | yes |
| `agents/` | agent guidance: this file, conventions, verification, environment | yes |
| `agents/local/` | current development plan, step files, progress, scratch | **no** (gitignored) |

Shared build settings are in `Directory.Build.props`/`.targets`, package versions in `Directory.Packages.props`, code style in `.editorconfig`, the SDK pin in `global.json`, CI in `.github/workflows/ci.yml`, and the registration of BeamNG.drive's MCP server in `.mcp.json`.

In the converter, everything that knows BeamNG's formats lives in `src/CsToBeamNG.Converter/BeamNG/`: virtual paths, the scene tree, terrain, meshes, materials, `info.json` and the installer. The pipeline starts in `Loading/`, whose section loaders build the world model in `World/` (with the map's geometry); each section's data and loader live in a folder of their own, such as `Terrain/` and `Water/`, and the dump's side of each section in `CsToBeamNG.Dump`'s `Sections/`. `Planning/` plans the area of interest, `Coordinates/` is the only code that converts CS1 coordinates into BeamNG's, and `Rendering/` and `Imaging/` draw the top-down debug images of `inspect` and write PNGs. `Networks/` holds the networks' world model and loaders and bends CS1's network meshes as the game does; `Buildings/`, `Props/` and `Trees/` hold those objects' world model and loaders, with the building lots' geometry. `Development/` holds developer tools: test levels such as the one `dev hello-level` installs, and the geometry- and height-probe checks. In the exporter, `Probes/` writes developer probes on request (D23). `Discovery/` finds the games and their folders, and `Automation/` starts Cities: Skylines for an unattended export, whose game side is the exporter's `Autorun/` folder ([docs/architecture.md, Unattended export](../docs/architecture.md#unattended-export)).

## Build, run, test

From the repository root, on either OS:

```sh
dotnet build                                        # everything; deploys the mod when CS1 is found
dotnet test                                         # all tests
dotnet test --project tests/CsToBeamNG.Dump.Tests   # one test project
dotnet format --verify-no-changes                   # style check; plain `dotnet format` fixes most issues
dotnet run --project src/CsToBeamNG.Cli -- --help   # the CLI
dotnet run --project src/CsToBeamNG.Cli -- doctor   # where the games and their folders are on this machine
dotnet run --project src/CsToBeamNG.Cli -- inspect <dump> --render out.png   # summarises a dump and draws it from above (--area map, --size 2048)
dotnet run --project src/CsToBeamNG.Cli -- dev hello-level   # installs a generated test level into BeamNG.drive
dotnet run --project src/CsToBeamNG.Cli -- export CityPalma   # starts CS1, exports the save into a dump, quits (ask the user first)
dotnet run --project src/CsToBeamNG.Cli -- export CityPalma --probe geometry   # the same, plus the road-geometry probe in the dump's probes/ folder
dotnet run --project src/CsToBeamNG.Cli -- dev geometry-probe <dump>   # checks the converter's road-mesh bending against that probe
dotnet run --project src/CsToBeamNG.Cli -- dev height-probe <dump>     # checks the converter's terrain heights against the game's (export with --probe heights)
```

- Warnings are errors, including code style (`.editorconfig`) and analyzers.
- Tests use xUnit.net v3 on Microsoft Testing Platform. `global.json` switches `dotnet test` to MTP mode, so it takes `--project`/`--solution` instead of a bare path, and VSTest options such as `--logger` don't apply.
- The exporter builds only where the CS1 assemblies are found, and then deploys itself to `<CS1 user data>/Addons/Mods/CsToBeamNG/` (our DLLs only). Elsewhere it is skipped with warning CSTB001 (a plain message on CI), and the rest still builds and tests, including the tests of the exporter's `Core/`.
- Game paths come from the MSBuild properties listed in [agents/environment.md](environment.md). Override them with `-p:Name=value`, a gitignored `Directory.Build.local.props` (copy the `.example`), or environment variables, in that order of precedence. `-p:DeployMod=false` skips deployment.

## Working agreements

- **Git is read-only for agents.** The maintainer stages and commits every change and writes the commit messages, so agents must never commit and never change files, the index, refs or config through git. Mutating git commands have lost work before.
  - **Allowed:** read-only commands such as `git status`, `git diff`, `git log`, `git show`, `git blame`, `git grep`, `git ls-files` and `git check-ignore`. Use `git --no-pager` so output doesn't block on a pager.
  - **Forbidden:** `add`, `commit`, `stash`, `checkout`/`switch`, `restore`, `reset`, `rm`, `mv`, `clean`, `merge`, `rebase`, `cherry-pick`, `revert`, `pull`, `push`, `fetch`, `tag`, `apply`/`am`, `config` writes, and creating, deleting or renaming branches.
  - End your task with a suggested commit message; it is only a starting point for the maintainer.
- **Never edit game installation folders.** BeamNG test content goes into the BeamNG user folder under `mods/unpacked/`, and only into folders named `cstobeamng_*`; other mods there belong to the user. Install through `ModInstaller`, which replaces only the files listed in the folder's `cstobeamng-files.json`. The exporter is deployed to the CS1 user-data `Addons/Mods/` folder.
- **Never put game material in the repository:** game DLLs, assets (meshes, textures, shaders, audio), savegames, city dumps, generated levels or decompiled game code. Record facts learned from them in `docs/research/`, in your own words, with sources.
- **Stay in scope.** Do the task you were given; record follow-ups instead of silently widening it.
- **Ask the user first** before adding a dependency with a non-permissive license, choosing a license, adding MCP servers or other tool configuration, or deleting anything outside the repository. Also ask whenever a game must be run, and tell them exactly what to do.

## Code quality and portability

The full rules are in [agents/conventions.md](conventions.md). The essentials:

- Dependencies point one way: Exporter → Dump ← Converter ← Cli. CS1 knowledge lives in the Exporter, BeamNG knowledge in the Converter's BeamNG layer.
- Nullable reference types, warnings as errors, analyzers on, `dotnet format` clean, tests for everything testable.
- Use invariant culture for every number and date written or parsed. The maintainer's machine formats numbers with decimal commas.
- Build file-system paths with `Path.Combine`/`Path.Join`. Build BeamNG virtual paths (`/levels/...`, always `/`) with the dedicated helper.
- Deterministic output: the same dump and options produce identical files.
- Exporter code runs on Unity 5.6's old Mono with the .NET 3.5 library. That means no async/Task, no ValueTuple, Unity calls on the main thread only, and never mutating game state.

## Verifying work

The procedures are in [agents/verification.md](verification.md).

- Always build and run the tests.
- CS1-side changes need a fresh dump. With the user's OK to run the game, export one yourself with `cstobeamng export`; otherwise ask the user precisely what to do.
- BeamNG-side changes can be verified by the agent through BeamNG's built-in MCP server (registered in `.mcp.json`; the user starts the game with `-enablemcp`): load the level, take screenshots, read logs, inspect the AI navgraph, measure performance.

## Knowledge base

- [docs/architecture.md](../docs/architecture.md): design, conversion stages, decision log.
- [docs/research/](../docs/research/README.md): CS1 internals and BeamNG formats, with sources and confidence tags. **Read the relevant file before touching game-specific code.**
- [docs/legal.md](../docs/legal.md): what may be committed and how outputs may be used.
- [docs/dump-format.md](../docs/dump-format.md): the dump specification. Every section an export step adds is specified there.
- [agents/environment.md](environment.md): where games, user folders and logs live on each OS.

## Current development plan (local only)

If `agents/local/` exists, development follows its plan:

1. Read `agents/local/README.md` (the workflow), then `agents/local/PLAN.md` (status and decisions).
2. Execute only the step you were given (`agents/local/steps/phase-N/NN-*.md`), after checking that its dependencies are done.
3. Record the outcome in the step file and update the status in `PLAN.md`.

Facts about the maintainer's machine are in `agents/local/environment.local.md`. In a fresh clone this folder does not exist; ignore this section.

## Keeping documentation current

- New durable knowledge about CS1 or BeamNG goes in `docs/research/`, with a source and confidence tag.
- Changed build commands, layout, conventions or procedures go in this file or the other files in `agents/`.
- Design decisions go in the decision log in `docs/architecture.md`.
- Plans, progress and experiments stay in `agents/local/` only.
