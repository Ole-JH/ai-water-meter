# Art & UI

Shadowfall uses free, openly licensed art. All 3D models are **CC0** (public domain). The UI uses Kenney's CC0 RPG pack, icons from game-icons.net (**CC BY 3.0**, credited below) and two **SIL OFL** fonts.

## Where things live

| Path | What |
| --- | --- |
| `Assets/Resources/Art/**.glb` | Characters, monsters, weapons, nature, buildings, props, graveyard (imported by **glTFast**) |
| `Assets/Resources/UI/Skin` | Panels, buttons and bars (Kenney UI Pack: RPG Expansion) |
| `Assets/Resources/UI/Icons` | Ability, item, skill, menu and class icons (rendered from game-icons.net) |
| `Assets/Resources/UI/Cursors` | Gauntlet, sword and hand cursors |
| `Assets/Resources/UI/Fonts` | Cinzel (headings), Alegreya Sans (text) |
| `tools/art/` | Download and repack scripts for the 3D models |
| `tools/ui/render_icons.js` | Icon renderer |

## How models are used

- `ArtLibrary` loads a model by path (for example `Nature/tree_oak`), scales it to a target height or width, and sets it on the ground.
- `CharacterView` plays the model's animations through the legacy `Animation` component that glTFast creates: idle, walk and run chosen from movement speed, plus attack, cast, hit and death.
- `CharacterLook` maps each monster, NPC and hero appearance to a model, size and animation set.
- **Fallback:** if a model can't be loaded, for example when the glTFast package is missing, the game falls back to the old primitive shapes and keeps working.

!!! note "The world layout never depends on the art"
    The world generator draws visual variety from its own random number generator, so swapping or adding models doesn't change the walkability map. The server's world hash stays the same and no `world:reset` is needed.

## Rebuilding or changing the art

```bash
task art:fetch     # download the source packs into .art-cache (needs: pip install gdown)
task art:build     # repack the models listed in tools/art/build_art.py into Assets/Resources/Art
task ui:icons      # re-render icons (needs playwright-core and CHROMIUM_PATH)
```

To use another model, add a line to `MODELS` in `tools/art/build_art.py`, listing the animations to keep, and reference its path from code. For example, to give the Goblin Warchief a different model, change its entry in `CharacterLook` (`Assets/Scripts/Characters/CharacterView.cs`).

## Credits

| Pack | Creator | License | Used for |
| --- | --- | --- | --- |
| KayKit Adventurers, Skeletons, Medieval Hexagon, Dungeon Remastered | Kay Lousberg | CC0 | Heroes, villagers, skeletons, buildings, props |
| Nature Kit, Fantasy Town Kit, Graveyard Kit, UI Pack RPG Expansion | Kenney | CC0 | Trees, rocks, town props, graveyard, zombie, UI skin, cursors |
| Ultimate Monsters, Ultimate Animated Animals | Quaternius | CC0 | Goblins, warchief, golem, wolves |
| game-icons.net | Lorc, Delapouite, DarkZaitzev, Faithtoken, Sbed | CC BY 3.0 | All UI icons (recolored) |
| Cinzel, Alegreya Sans | Natanael Gama; Huerta Tipográfica | SIL OFL 1.1 | Fonts |
