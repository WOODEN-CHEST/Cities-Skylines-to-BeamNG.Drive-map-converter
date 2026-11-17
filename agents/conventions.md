# Code conventions

The goal is a codebase one maintainer can keep developing for years, even though its inputs come from reverse-engineered game internals that will occasionally change or surprise us. Prefer clarity, isolation of fragile parts and good diagnostics over cleverness.

## Architecture rules

- Dependencies point one way: `Exporter → Dump ← Converter ← Cli`.
  - **Exporter** (net35): the only code that touches CS1. It stays thin: read what the game knows, write the dump. No BeamNG knowledge.
  - **Dump** (net35 + net10.0): the format model, readers and writers. No dependencies beyond the base class library, and no game or BeamNG knowledge.
  - **Converter** (net10.0): all interpretation and generation. BeamNG-specific code (file formats, object classes, conventions) stays in its own namespace, so format changes in BeamNG updates are contained.
  - **Cli** (net10.0): argument parsing and presentation only.
- The converter is a pipeline of stages (see `docs/architecture.md`). Stages build in-memory results; only the writer and installer stages touch the file system.
- Code that depends on reverse-engineered behaviour cites its source in a comment that links the relevant section of `docs/research/`, and that section links its evidence. If you are the one discovering the behaviour, write the research entry in the same change.
- Validate inputs at the boundaries (dump reader, game APIs). Inside, trust the model types.

## C# style

- Follow `.editorconfig`; `dotnet format --verify-no-changes` must pass. The code-style rules it sets to `warning` fail the build, and `dotnet format` fixes most of them.
- Use file-scoped namespaces, and make the namespace match the folder path. Usings go at the top of the file.
- Nullable reference types are enabled, warnings are errors, and .NET analyzers run at the recommended level (`AnalysisLevel` 10.0, pinned in `Directory.Build.props`). Never suppress a warning without a comment explaining why.
- Always use braces, and always state accessibility.
- `var`: spell out built-in types (`int`, `float`, `double`, `string`, ...) so that float/double mix-ups stay visible; elsewhere `var` is fine.
- Keep types small and focused. Prefer immutable data (records, readonly structs) for model types. No static mutable state outside clearly marked caches.
- Naming:
  - PascalCase for types, members and constants (private ones too); `_camelCase` for all other private fields, static or not; camelCase for locals and parameters. No Hungarian prefixes.
  - Put units in names where they would otherwise be ambiguous: `heightMeters`, `angleRadians`, `sizeCells`.
- Comments explain *why*, not *what*. Public types and members of `Dump` and `Converter` get XML doc comments; the build enforces this in those projects (`RequireXmlDocs` makes CS1591 an error).
- No magic numbers for format or game constants. Name them (`TerrainResolution = 1081`) and point to `docs/research/` for the source.

## .NET 3.5 and Unity 5.6 constraints (Exporter, and the net35 build of Dump)

The game runs mods on Unity 5.6's old Mono with the .NET Framework 3.5 base class library. The compiler catches missing APIs, but these are the usual traps:

- **Not available:**
  - `Task`/`async`, `ValueTuple`/`Tuple`, `Span<T>`, `Lazy<T>`
  - `IReadOnlyList<T>` and the other `IReadOnly*` interfaces (use `IList<T>` or arrays in shared APIs), `Array.Empty<T>()`
  - `string.IsNullOrWhiteSpace`, `Enum.HasFlag`, `Stream.CopyTo`, `StringBuilder.Clear`, `BitConverter.SingleToInt32Bits`
  - `Path.Combine` with more than two arguments, `File.Move` onto an existing file
- **Don't trust the runtime's floating-point text.** Mono's `double.ToString("R")` and `double.Parse` can both be off by one unit in the last place, in ways that agree with each other (verified on Mono 6.8). Write JSON only with the Dump library's `JsonWriter`, which computes the digits exactly, and binary data with `RecordFileWriter`. Never format numbers for the dump by hand.
- **The game's global types hide ours.** Assembly-CSharp puts its types in the global namespace (`ModInfo`, `NetInfo`, `Building`, ...). In exporter code such a type wins over one of the same name imported with `using`, and an alias of that name doesn't compile (CS0576). So public types in `Dump` must not share a name with a global game type; this is why the manifest's mod type is `GameModInfo`.
- **Avoid language features that need runtime or library support:** default interface members, `ref struct`, records and `init` (they would need an `IsExternalInit` polyfill; check in the game before relying on them).
- **Nullable analysis is on for net35 too.** The compiler generates the attributes it needs. The flow-analysis attributes (`NotNullWhen`, `MemberNotNull`, ...) come from internal polyfills in `src/Shared/NullableAttributes.cs`, which every net35 project compiles in. Never expose them to another net35 assembly through `InternalsVisibleTo`: the copies would conflict (CS0436).
- **Code compiled for both targets (Dump):** the analyzers also check the net10.0 build, where they suggest APIs that net35 lacks; for example, CA1510 asks for `ArgumentNullException.ThrowIfNull`. Use `#if NET` for such calls, or a small helper in Dump (`Guard` has the common checks); don't disable the rule.
  - Code that only the converter needs, such as the readers built on System.Text.Json, may use the whole .NET 10 library. Wrap such a file's entire content in `#if NET` ... `#endif`, so it is plainly absent from the net35 build.
  - Records and `init` are fine in those net10-only files.
- **Threads:** call Unity APIs only on the main thread. Read simulation data consistently: on the simulation thread (`SimulationManager.AddAction`), or justify any other approach in a comment.
  - Resolve managers (`NetManager.instance` and the like) on the main thread, because `Singleton<T>.instance` creates a missing one through Unity, and check `Singleton<T>.exists` first.
  - Read everything that must be consistent in one simulation-thread action, then show UI on the main thread again through `ThreadHelper.dispatcher.Dispatch`. `ExportController` and `ExportJob` follow this pattern; the facts are in [research, Threads](../docs/research/cs1-save-and-world.md#threads).
- **Never modify game state.** The exporter only reads. Don't pause the simulation, change settings, or write into managers.
- **One bad object must not abort an export.** Catch per object, log its id and prefab name, count the failures, and report them in the manifest.
- **Locations:** use `ColossalFramework.IO.DataLocation`; never hard-code paths.
- **Keep the exporter "dumb".** Dump what the game knows and interpret it in the converter, where iteration is cheap.

## Cross-platform rules

- **File-system paths:** build them with `Path.Combine` or `Path.Join`; never concatenate separators. Don't assume a case-insensitive file system.
- **BeamNG virtual paths** inside level files always use `/` and start at the virtual root (`/levels/<level>/...`). Build them with `VirtualPath` (`src/CsToBeamNG.Converter/BeamNG/`), never with `Path.Combine`; `ToFileSystemPath` maps one into a mod folder.
- **Generated names:** file and folder names are lowercase ASCII (`a-z0-9_-.`), and references must match them exactly, because Linux is case-sensitive (`BeamNGNames.IsGeneratedName`).
  - The exception is the scene tree: group folders carry the group's name, and the game expects some names with capitals, such as `MissionGroup` and `PlayerDropPoints` (`BeamNGNames.IsSceneName`). The writer creates both the name and the folder, so they always match.
- **Culture:** format and parse every number and date with `CultureInfo.InvariantCulture`.
  - Many machines, including the maintainer's (`LC_NUMERIC` is a decimal-comma locale), use decimal commas, which would corrupt JSON and other text formats.
  - Tests that write numbers must also run once under a decimal-comma culture.
- **Text output:** UTF-8 without BOM, `\n` line endings.
- **BeamNG files** are written by the writers in `src/CsToBeamNG.Converter/BeamNG/`, which return `OutputFile`s for `ModInstaller` to install. JSON goes through `BeamNGJson` (System.Text.Json): invariant, coordinates as `float`s in their shortest form, `-0` as `0`. The dump's `JsonWriter` is for the dump only.
- **Windows-only APIs** (such as the registry) go behind `OperatingSystem.IsWindows()`, with a Linux equivalent provided.
- **Game locations:** never assume them. Go through `GameEnvironmentLocator` (`src/CsToBeamNG.Converter/Discovery/`), which applies the user's overrides. New knowledge about where games keep files goes in [docs/research/game-locations.md](../docs/research/game-locations.md).

## Data, coordinates and determinism

- The dump stores game-native data: CS1/Unity coordinates, units and angles exactly as the game uses them. All conversion to BeamNG conventions happens in one converter module (see `docs/architecture.md`, "Coordinate systems").
- Use metres and radians internally; convert units only at the boundaries.
- Binary formats are little-endian, with explicit versioned headers, and are documented in `docs/dump-format.md`.
- Output is deterministic, which keeps diffs and golden tests meaningful:
  - stable ordering;
  - no timestamps or random GUIDs in generated files (BeamNG `persistentId`s come from `PersistentId.ForObject(level, key)`, with keys derived from the input, such as a CS1 object id);
  - for output that must match across platforms, avoid `Math.Sin`, `Math.Exp` and similar in generated values: .NET takes them from each platform's C library, whose last bits can differ. Arithmetic and `Math.Sqrt` are exact everywhere (`HelloLevel` and `RotationMatrix.YawTowards` work this way);
  - parallel work merged in a fixed order.

## Errors, logging, diagnostics

- Fail fast on unreadable or incompatible input, with a message that names the file, the version found and the version expected.
- Degrade gracefully on individual objects: skip the object, log a warning with its identity, count it, and include the counts in the conversion report.
- **Converter:** log through the `Microsoft.Extensions.Logging` abstractions; the first step that logs adds the package. The CLI prints a concise summary and writes a detailed log next to the output.
- **Exporter:** write `export.log` in the dump folder, plus `[CsToBeamNG]`-prefixed lines in the game log; `ExportLog` does both. Log problems the exporter handles as warnings, never with `Debug.LogError`: the game collects logged errors for its own error display, and the exporter tells the user itself.

## Tests

- xUnit.net v3 (on Microsoft Testing Platform) covers `Dump` (its net10.0 build) and `Converter`. Tests must pass on Linux and Windows.
- Test methods are named as PascalCase sentences, such as `RejectsWrongMagic`; underscores are not allowed (CA1707).
- Kinds of test:
  - unit tests for math (Béziers, transforms, resampling);
  - golden-file tests for writers;
  - round-trip tests for readers and writers.
- Fixtures are synthetic or contain only layout data (positions, names). Never commit game meshes, textures or dumps as fixtures.
- Every bug fix gets a regression test where feasible.
- The exporter can't run outside the game, so keep testable logic (data shaping, encoding) in `Dump`, or in the exporter's `Core/` folder. Code in `Core/` must not use game types: `tests/CsToBeamNG.Exporter.Tests` compiles it in and tests it without the game, also on CI. The rest of the exporter is a thin layer that reads the game into `Core` types.

## Dependencies

- Use central package management (`Directory.Packages.props`) and pin versions.
- Allowed licenses: MIT, BSD, Apache-2.0, zlib, public domain. Anything else needs the maintainer's approval. Record every dependency (name, license, purpose) in `THIRD-PARTY-NOTICES.md`.
- Prefer pure managed packages; avoid native binaries, which complicate Windows/Linux parity.
- The exporter uses no NuGet dependencies besides what the game provides. The one exception is `CitiesHarmony.API`, if patching ever becomes necessary (prefer not to patch).

## Performance

Cities are big: tens of thousands of buildings, hundreds of thousands of trees and props, millions of vertices.

- Stream large data; don't hold several copies of it.
- Avoid LINQ and per-element allocations in hot loops; use arrays and `Span<T>` in the converter.
- Parallelise independent work (per chunk, per asset), but keep the results deterministic.
- In the exporter, keep the game responsive where practical (export in batches, show progress) and keep memory spikes bounded.
