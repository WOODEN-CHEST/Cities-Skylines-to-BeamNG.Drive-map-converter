# Legal notes and asset policy

This is not legal advice. It summarises the terms we found and the rules this project follows. When in doubt, ask the copyright holder.

## What this repository contains

Only original code and documentation written for this project. It never contains:

- game binaries or DLLs (the exporter builds against the user's installed copies);
- game or workshop assets: meshes, textures, shaders, audio, prefab data;
- savegames, city dumps or generated levels;
- decompiled game code. Knowledge about game internals is described in our own words in [research/](research/README.md), with links to public sources.

## How the tool handles game content

Everything copyrighted stays on the user's machine:

- The exporter reads data from the user's own licensed copy of Cities: Skylines while it runs.
- The converter writes a level into the user's own BeamNG user folder.
- BeamNG's own assets are referenced by their in-game paths, never copied.

## Using converted levels

- A level converted in **full mode** contains derivatives of Paradox/Colossal Order content and of workshop creators' content. Keep it for personal use; do not upload or share it.
- BeamNG's repository rejects such content: "You are not allowed to use copyrighted content and content that belongs to other users without permission in your mods." (BeamNG Modding Guidelines)
- **Clean mode** output contains only the city layout, BeamNG assets and generated geometry. It is the only candidate for sharing. Check BeamNG's rules before uploading anything.

## Terms considered (as found on 2026-11-16)

### Paradox (Cities: Skylines)

- [Paradox User Agreement](https://legal.paradoxplaza.com/eula?locale=en) (updated 2026-01-21):
  - §1 grants a personal, non-commercial licence.
  - §5: "We love when our community creates user-generated content, including mods, and we encourage you to do so!"
  - §5: "We hereby grant you the right to create, enjoy and make publicly available UGC, for non-commercial purposes."
  - Rights in UGC "only extend to the new, original content you create".
- The older [EULA v1.2](https://items.gog.com/paradox_eula/EULA_v.1.2_20161028_EN.pdf) (2016), §3(iv), prohibited reverse engineering and decompiling "other than as may be permitted according to applicable law".
- We found no Paradox statement about using extracted assets outside the game.

What this means for us: the tool is non-commercial user-generated content. It works through the established modding route (a mod that references the game's assemblies) and by calling game code at runtime. Outputs that contain game content stay private.

### Steam

- The [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/):
  - §2.G restricts copying, reverse engineering and derivative works of Steam content, except as permitted.
  - §6.B: workshop contributions are subscriptions, and other users get the same rights and restrictions for them.
- Workshop assets belong to their creators. Converting them for private use is not redistribution, but never share the results.

### BeamNG

- [EULA](https://beamng.com/game/support/policies/eula/):
  - §4.2: "Modding of the Software's content files is not reverse engineering".
  - §3.3 permits modding as allowed by the modding guidelines.
  - §11.10: uploaders warrant that their uploads don't infringe third-party rights.
- [Modding Guidelines](https://beamng.com/game/support/policies/modding-guidelines/): no copyrighted content or other users' content without permission, and don't overwrite default game data.
- The modders' note shipped with the game (`NOTE_FOR_MODDERS.md`, 0.40) says the same: "Generated content based on copyrighted brands, models or textures is still copyrighted, and won't pass approval without explicit permission from the owner."
- We reference BeamNG's assets by path instead of copying them. Official levels do the same, also through `.link` files. We found nothing that states whether bundling copies would be allowed.
