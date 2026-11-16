# Research notes

What we know about the two games' internals and file formats, collected while building the converter. This is the project's memory for reverse-engineered behaviour. Read the relevant file before changing game-specific code, and update it when you learn something.

| File | Topic |
|---|---|
| [cs1-save-and-world.md](cs1-save-and-world.md) | Savegame container, how the simulation is stored, map geometry, terrain, water, instance limits, key game APIs |
| [cs1-networks.md](cs1-networks.md) | Roads, rails, paths: data model, render data, mesh deformation, lane props |
| [cs1-assets.md](cs1-assets.md) | Prefabs, meshes, texture channel packing, colour variation, placement randomness |
| [beamng-level-format.md](beamng-level-format.md) | Level structure, terrain file, objects, meshes, materials, forest, limits |
| [beamng-ai-and-traffic.md](beamng-ai-and-traffic.md) | AI navgraph from decal roads, traffic signals, parking, the MCP server |
| [game-locations.md](game-locations.md) | Where Steam, Cities: Skylines and BeamNG.drive keep their files: libraries, manifests, user folders, saves, logs, versions |
| [prior-art.md](prior-art.md) | Related tools and what they teach us |

## Rules

- Write in your own words. Never paste decompiled game code, shader dumps or asset data. Describe behaviour, layouts and formulas, and link to public sources.
- Tag every claim with its confidence:
  - **[verified]**: checked by us against the installed game or its files. Say how, with the game version and date.
  - **[official]**: official documentation.
  - **[community]**: mod source code, wikis, forums.
  - **[inferred]**: our reasoning, not yet checked.
- Prefer upgrading a claim's tag over adding a duplicate note. When a claim turns out wrong, fix it and say what was wrong.
- Versions at the time of writing: CS1 final patch line (Unity 5.6.7f1), BeamNG.drive 0.40.5.
