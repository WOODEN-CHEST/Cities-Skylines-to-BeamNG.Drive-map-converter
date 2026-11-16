# CsToBeamNG

Turn a **Cities: Skylines** city into a drivable **BeamNG.drive** level: terrain, water, the road network with working AI traffic, buildings, props and trees, as close to the original as practical.

This is for the original Cities: Skylines (2015), not Cities: Skylines II.

## Status

Early development; **not usable yet**. The build system and project skeleton exist, and the conversion itself is being built step by step.

## How it works

1. **Exporter:** a Cities: Skylines mod. You load your city in the game and export it. The mod reads the city through the game's own code and writes a *city dump* (a folder of data files).
2. **Converter:** a command-line tool (`cstobeamng`) that turns the dump into a BeamNG.drive level in your BeamNG user folder.

Details are in [docs/architecture.md](docs/architecture.md).

## Game assets

This repository contains no game assets, and the tool never ships any: meshes and textures are extracted from your own installation when you convert, and BeamNG's assets are referenced by path. Levels that contain converted game or workshop content are for personal use only; see [docs/legal.md](docs/legal.md).

## Requirements

- Windows or Linux (both are supported).
- Cities: Skylines (Steam) and BeamNG.drive 0.40 or later, installed.
- To build: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

## Building

```sh
dotnet build
dotnet test
```

The exporter mod compiles against the game's own assemblies, so it is built only where Cities: Skylines is installed. A successful build copies it into the game's mods folder. Elsewhere it is skipped with a notice, and everything else still builds. If the game is installed in a non-default place, copy [Directory.Build.local.props.example](Directory.Build.local.props.example) to `Directory.Build.local.props` and set the paths there.

## Documentation

- [docs/architecture.md](docs/architecture.md): design, conversion stages and the decision log.
- [docs/legal.md](docs/legal.md): asset policy and how converted levels may be used.
- [docs/research/](docs/research/README.md): what we know about the internals of both games.
- [agents/AGENTS.md](agents/AGENTS.md): contributor guide (written for AI coding agents, useful for humans too).

## License

[MIT](LICENSE). Third-party packages and their licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
