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
| `src/CsToBeamNG.Exporter/` | CS1 mod (net35) | planned (step 0.1) |
| `src/CsToBeamNG.Dump/` | dump format: model, readers, writers (net35 + net10.0) | planned |
| `src/CsToBeamNG.Converter/` | conversion library (net10.0) | planned |
| `src/CsToBeamNG.Cli/` | command-line front end (net10.0) | planned |
| `tests/` | xUnit test projects | planned |
| `docs/` | architecture, legal notes, research (reverse-engineering knowledge) | yes |
| `agents/` | agent guidance: this file, conventions, verification, environment | yes |
| `agents/local/` | current development plan, step files, progress, scratch | **no** (gitignored) |

`CsToBeamNG` is a working name; step 0.1 confirms or changes it.

## Build, run, test

Not set up yet. Step 0.1 creates the solution and replaces this section with the real commands. The plan is:

- `dotnet build` and `dotnet test` from the repository root, on both OSes.
- The exporter builds only where the CS1 managed assemblies are found (see [agents/environment.md](environment.md)). Elsewhere it is skipped with a warning, so the rest builds without the game (CI, contributors).

## Working agreements

- **Git is read-only for agents.** The maintainer stages and commits every change and writes the commit messages, so agents must never commit and never change files, the index, refs or config through git. Mutating git commands have lost work before.
  - **Allowed:** read-only commands such as `git status`, `git diff`, `git log`, `git show`, `git blame`, `git grep`, `git ls-files` and `git check-ignore`. Use `git --no-pager` so output doesn't block on a pager.
  - **Forbidden:** `add`, `commit`, `stash`, `checkout`/`switch`, `restore`, `reset`, `rm`, `mv`, `clean`, `merge`, `rebase`, `cherry-pick`, `revert`, `pull`, `push`, `fetch`, `tag`, `apply`/`am`, `config` writes, and creating, deleting or renaming branches.
  - End your task with a suggested commit message; it is only a starting point for the maintainer.
- **Never edit game installation folders.** BeamNG test content goes into the BeamNG user folder under `mods/unpacked/`, and only into folders named `cstobeamng_*`; other mods there belong to the user. The exporter is deployed to the CS1 user-data `Addons/Mods/` folder.
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
- CS1-side changes need the user to run the game; ask precisely.
- BeamNG-side changes can be verified by the agent through BeamNG's built-in MCP server: load the level, take screenshots, read logs, inspect the AI navgraph, measure performance.

## Knowledge base

- [docs/architecture.md](../docs/architecture.md): design, conversion stages, decision log.
- [docs/research/](../docs/research/README.md): CS1 internals and BeamNG formats, with sources and confidence tags. **Read the relevant file before touching game-specific code.**
- [docs/legal.md](../docs/legal.md): what may be committed and how outputs may be used.
- `docs/dump-format.md`: the dump specification (created in step 0.3).
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
